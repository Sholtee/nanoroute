/********************************************************************************
* RouteMatchCursor.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct RouteMatch
    {
        public required HandlerRegistration HandlerRegistration { get; init; }

        public required Dictionary<string, object?> AttachedParameters { get; init; }

        public required ReadOnlyMemory<char> RemainingPath { get; init; }
    }

    internal class RouteMatchCursor : IAsyncEnumerator<RouteMatch>
    {
        #region Private
        private enum BranchKind
        {
            Literal,
            Parsed
        }

        private enum MatchPhase
        {
            /// <summary>
            /// Emit all handlers that belong to the current node before descending into child nodes.
            /// </summary>
            EmitHandlers,

            /// <summary>
            /// Explore child branches according to the configured precedence.
            /// </summary>
            Branch,

            /// <summary>
            /// Terminating step
            /// </summary>
            Done
        }

        private readonly record struct BranchOrder(BranchKind First, BranchKind Second)
        {
            public static BranchOrder From(MatchingPrecedence matchingPrecedence) => matchingPrecedence switch
            {
                MatchingPrecedence.LiteralFirst => new BranchOrder(BranchKind.Literal, BranchKind.Parsed),
                MatchingPrecedence.ParameterizedFirst => new BranchOrder(BranchKind.Parsed, BranchKind.Literal),
                _ => throw new ArgumentOutOfRangeException(nameof(matchingPrecedence))  // dead code (valid values enforced by the RouterConfig class)
            };
        }

        private static readonly ArrayPool<char> s_arrayPool = ArrayPool<char>.Create();

        private readonly BranchOrder _branchOrder;

        private readonly Dictionary<string, object?> _parameters;

        private char[]? _decodedSegmentBuffer;

        // Keep DelimitedSegment instead of Uri.Segments: UrlSegmentBenchmarks shows it avoids eager segment
        // array/string allocation and preserves this cursor's lazy traversal model.
        //
        // DelimitedSegment is mutable, so keep this field non-readonly to let MoveNext() update the cursor
        // itself instead of a defensive copy.
        private DelimitedSegment _segment;

        private MatchPhase _phase;

        private ReadOnlyMemory<char> _remainingPath;

        private IList<HandlerRegistration>? _handlers;

        private int
            _handlerIndex,
            _nextDecodedSegment;

        private RouteNode _node;

        private ReadOnlyMemory<char> GetSegmentForMatching()
        {
            ReadOnlyMemory<char> current = _segment.Current;

            // This check is fast since span operations are vectorized
            if (current.Span.IndexOf('%') < 0)
                return current;

            char[] decodedSegmentBuffer = _decodedSegmentBuffer ??= s_arrayPool.Rent(_segment.Original.Length);

            if (!UrlUtils.TryDecodeUrl(current.Span, decodedSegmentBuffer.AsSpan(_nextDecodedSegment), UrlDecodeMode.Path, out int charsWritten))
                HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, Resources.ERR_DECODING_FAILED);

            ReadOnlyMemory<char> decodedSegment = decodedSegmentBuffer.AsMemory(_nextDecodedSegment, charsWritten);
            _nextDecodedSegment += charsWritten;
            return decodedSegment;
        }

        // Keep MoveNextAsync() state-machine-free while branch matching completes synchronously
        private async ValueTask<bool> MoveNextAwaitedAsync(ValueTask<bool> branchMatched)
        {
            _phase = await branchMatched ? MatchPhase.EmitHandlers : MatchPhase.Done;
            return await MoveNextAsync();
        }

        private void AdvanceToNextSegment(RouteNode nextNode)
        {
            Cancellation.ThrowIfCancellationRequested();

            _node = nextNode;
            _handlerIndex = 0;
            _handlers = null;
            _remainingPath = _segment.Remaining;

            _segment.MoveNext();
        }

        private bool TryEmitHandler()
        {
            // Retrieve the handler list on the first iteration
            if (_handlers is null && !_node.HandlerRegistrations.TryGetValue(Verb, out _handlers))
                return false;

            while (_handlerIndex < _handlers.Count)
            {
                HandlerRegistration candidate = _handlers[_handlerIndex++];

                if (_segment.HasValue && !candidate.IsPrefix)
                    continue;

                Current = new RouteMatch
                {
                    HandlerRegistration = candidate,
                    RemainingPath = _remainingPath,
                    AttachedParameters = _parameters
                };
                return true;
            }

            return false;
        }

        private ValueTask<bool> TryBranchAsync(BranchKind branchKind, ReadOnlyMemory<char> decodedSegment) => branchKind switch
        {
            BranchKind.Literal => new ValueTask<bool>(TryLiteralBranch(decodedSegment)),
            BranchKind.Parsed => TryParsedBranchAsync(decodedSegment),
            _ => throw new ArgumentOutOfRangeException(nameof(branchKind))
        };

        private ValueTask<bool> TryBranchPairAsync()
        {
            // Decode only when the matcher is about to inspect the segment. Prefix handlers can still run
            // without paying this cost and they can catch invalid escape errors, too.
            ReadOnlyMemory<char> decodedSegment = GetSegmentForMatching();

            ValueTask<bool> firstBranchMatched = TryBranchAsync(_branchOrder.First, decodedSegment);

            if (!firstBranchMatched.IsCompletedSuccessfully)
                return TryBranchPairAwaitedAsync(firstBranchMatched, decodedSegment);

            if (firstBranchMatched.Result)
                return new ValueTask<bool>(true);

            return TryBranchAsync(_branchOrder.Second, decodedSegment);
        }

        // Keep TryBranchPairAsync() state-machine-free while the preferred branch completes synchronously
        private async ValueTask<bool> TryBranchPairAwaitedAsync(ValueTask<bool> firstBranchMatched, ReadOnlyMemory<char> decodedSegment)
        {
            if (await firstBranchMatched)
                return true;

            return await TryBranchAsync(_branchOrder.Second, decodedSegment);
        }

        private bool TryLiteralBranch(ReadOnlyMemory<char> decodedSegment)
        {
            if (!_node.LiteralChildren.TryGetValue(decodedSegment, out RouteNode branchNode))
                return false;

            AdvanceToNextSegment(branchNode);
            return true;
        }

        private ValueTask<bool> TryParsedBranchAsync(ReadOnlyMemory<char> decodedSegment, int startIndex = 0)
        {
            for (int i = startIndex; i < _node.ParsedChildren.Count; i++)
            {
                RouteNode branchNode = _node.ParsedChildren[i];
                ParameterParser parser = branchNode.ParameterParser!;

                ValueTask<ValueParseResult> parseResult = parser.Parse
                (
                    new ValueParserContext
                    {
                        Segment = decodedSegment,
                        Services = Services,
                        Arguments = parser.Arguments,
                        Cancellation = Cancellation
                    }
                );

                if (!parseResult.IsCompletedSuccessfully)
                    return TryParsedBranchAwaitedAsync(parseResult, decodedSegment, i, branchNode);

                if (TryAcceptParsedBranch(branchNode, parseResult.Result))
                    return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        // Keep TryParsedBranchAsync() state-machine-free while branch matching completes synchronously
        private async ValueTask<bool> TryParsedBranchAwaitedAsync(ValueTask<ValueParseResult> parseResult, ReadOnlyMemory<char> decodedSegment, int branchIndex, RouteNode branchNode)
        {
            if (TryAcceptParsedBranch(branchNode, await parseResult))
                return true;

            return await TryParsedBranchAsync(decodedSegment, branchIndex + 1);
        }

        private bool TryAcceptParsedBranch(RouteNode branchNode, ValueParseResult parseResult)
        {
            if (!parseResult.Success)
                return false;

            if (branchNode.ParameterParser!.Definition.ParameterName is { Length: > 0 } parameterName)
                // This will overwrite any existing parameter on the given key
                _parameters[parameterName] = parseResult.Parsed;

            AdvanceToNextSegment(branchNode);
            return true;
        }
        #endregion

        public RouteMatchCursor(RouteNode node, HttpVerb verb, Uri uri, IServiceProvider services, RouterConfig routerConfig, CancellationToken cancellation)
        {
            _segment = new DelimitedSegment
            (
                // Escaped path, not percent decoded -> "/path%2Fto%2Fsomewhere/" will be treated as a single segment
                uri.AbsolutePath.AsMemory(),
                '/'
            );
            _branchOrder = BranchOrder.From(routerConfig.MatchingPrecedence);
            _parameters = new Dictionary<string, object?>(routerConfig.ParametersCapacity, StringComparer.OrdinalIgnoreCase);
            _phase = MatchPhase.EmitHandlers;
            _node = node;

            Cancellation = cancellation;
            Services = services;
            Verb = verb;

            AdvanceToNextSegment(node);
        }

        public RouteMatch Current { get; private set; }

        public CancellationToken Cancellation { get; }

        public IServiceProvider Services { get; }

        public HttpVerb Verb { get; }

        public bool Completed => _phase is MatchPhase.Done;

        public ValueTask DisposeAsync()
        {
            if (_decodedSegmentBuffer is not null)
                s_arrayPool.Return(_decodedSegmentBuffer, clearArray: false);

            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            while (_phase is not MatchPhase.Done)
            {
                Cancellation.ThrowIfCancellationRequested();

                switch (_phase)
                {
                    case MatchPhase.EmitHandlers:
                        if (TryEmitHandler())
                            return new ValueTask<bool>(true);

                        // No handler terminated the pipeline, go to the branch matching phase.
                        _phase = MatchPhase.Branch;
                        break;

                    case MatchPhase.Branch:
                        if (!_segment.HasValue)
                        {
                            _phase = MatchPhase.Done;
                            break;
                        }

                        ValueTask<bool> branchMatched = TryBranchPairAsync();
                        if (!branchMatched.IsCompletedSuccessfully)
                            return MoveNextAwaitedAsync(branchMatched);

                        _phase = branchMatched.Result ? MatchPhase.EmitHandlers : MatchPhase.Done;
                        break;

                    default:
                        Debug.Fail($"Unknown phase: {_phase}");
                        break;
                }
            }

            return new ValueTask<bool>(false);
        }
    }
}
