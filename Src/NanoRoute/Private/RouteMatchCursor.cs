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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    using Properties;

    internal sealed class RouteMatchCursor : IAsyncEnumerator<HandlerRegistration>
    {
        #region Private
        private enum BranchKind: byte
        {
            Literal,
            Parsed
        }

        private enum MatchPhase: byte
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

        private static readonly ValueTask<bool>
            s_true = new(true),
            s_false = new(false);

        private readonly BranchOrder _branchOrder;

        private readonly RouteNode _root;

        private char[]? _decodedSegmentBuffer;

        // Keep DelimitedSegment instead of Uri.Segments: UrlSegmentBenchmarks shows it avoids eager segment
        // array/string allocation and preserves this cursor's lazy traversal model.
        private readonly DelimitedSegment _segment;

        private MatchPhase _phase;

        private IList<HandlerRegistration>? _handlers;

        private int
            _handlerIndex,
            _nextDecodedSegment;

        private RouteNode _node;

        #region Async helpers
        // Keep MoveNextAsync() state-machine-free while branch matching completes synchronously
        private async ValueTask<bool> MoveNextAwaitedAsync(ValueTask<bool> branchMatched)
        {
            if (!await branchMatched.ConfigureAwait(false))
            {
                _phase = MatchPhase.Done;
                return false;
            }

            _phase = GetPhaseForCurrentNode();
            return await MoveNextAsync().ConfigureAwait(false);
        }

        private async ValueTask<bool> TryBranchPairAwaitedAsync(ValueTask<bool> firstBranchMatched, ReadOnlyMemory<char> decodedSegment) =>
            await firstBranchMatched.ConfigureAwait(false) ||
            await TryBranchAsync(_branchOrder.Second, decodedSegment).ConfigureAwait(false);

        private async ValueTask<bool> TryParsedBranchAwaitedAsync(KeyValuePair<ParameterParser, RouteNode> parsedChild, ValueTask<ValueParseResult> parseResult, ReadOnlyMemory<char> decodedSegment, int branchIndex) =>
            TryAcceptParsedBranch(parsedChild, await parseResult.ConfigureAwait(false)) ||
            await TryParsedBranchAsync(decodedSegment, branchIndex + 1).ConfigureAwait(false);

        private async ValueTask<bool> TryAcceptParsedBranchAwaitedAsync(KeyValuePair<ParameterParser, RouteNode> parsedChild, ValueTask<ValueParseResult> parseResult) =>
            TryAcceptParsedBranch(parsedChild, await parseResult.ConfigureAwait(false)) &&
            await TrySingleBranchesAsync();
        #endregion

        private ReadOnlyMemory<char> GetSegmentForMatching()
        {
            ReadOnlyMemory<char> segment = _segment.Current;

            // This check is fast since span operations are vectorized
            if (segment.Span.IndexOf('%') < 0)
                return segment;

            char[] decodedSegmentBuffer = _decodedSegmentBuffer ??= s_arrayPool.Rent(_segment.Original.Length);

            if (!UrlUtils.TryDecodeUrl(segment.Span, decodedSegmentBuffer.AsSpan(_nextDecodedSegment), UrlDecodeMode.Path, out int charsWritten))
                HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, Resources.ERR_DECODING_FAILED);

            ReadOnlyMemory<char> decodedSegment = decodedSegmentBuffer.AsMemory(_nextDecodedSegment, charsWritten);
            _nextDecodedSegment += charsWritten;
            return decodedSegment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceToNextSegment(RouteNode nextNode)
        {
            Cancellation.ThrowIfCancellationRequested();

            _node = nextNode;
            _handlerIndex = 0;
            _handlers = null;
            
            RemainingPath = _segment.Remaining;

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

                Current = candidate;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MatchPhase GetPhaseForCurrentNode() => _node.HandlerRegistrations.Count > 0
            ? MatchPhase.EmitHandlers
            : MatchPhase.Branch;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<bool> TryBranchAsync(BranchKind branchKind, ReadOnlyMemory<char> decodedSegment) => branchKind switch
        {
            BranchKind.Literal => new ValueTask<bool>(TryLiteralBranch(decodedSegment)),
            BranchKind.Parsed => TryParsedBranchAsync(decodedSegment, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(branchKind))
        };

        internal ValueTask<bool> TrySingleBranchesAsync()
        {
            while (_segment.HasValue && _node.SingleBranch is { } branch)
            {
                ReadOnlyMemory<char> segment = GetSegmentForMatching();

                switch (branch)
                {
                    case KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch:
                        if (!ReadOnlyMemoryCharComparer.Instance.Equals(literalBranch.Key, segment))
                            return s_false;

                        AdvanceToNextSegment(literalBranch.Value);
                        continue;

                    case KeyValuePair<ParameterParser, RouteNode> parsedBranch:
                        ValueTask<ValueParseResult> parseResult = ParseSegment(segment, parsedBranch.Key);

                        if (!parseResult.IsCompletedSuccessfully)
                            return TryAcceptParsedBranchAwaitedAsync(parsedBranch, parseResult);

                        if (!TryAcceptParsedBranch(parsedBranch, parseResult.Result))
                            return s_false;
        
                        continue;

                    default:
                        Debug.Fail($"Unknown single branch type: {branch.GetType().Name}");
                        return s_false;
                }
            }

            return s_true;
        }

        private ValueTask<bool> TryBranchPairAsync()
        {
            // Decode only when the matcher is about to inspect the segment. Prefix handlers can still run
            // without paying this cost and they can catch invalid escape errors, too.
            ReadOnlyMemory<char> segment = GetSegmentForMatching();

            ValueTask<bool> firstBranchMatched = TryBranchAsync(_branchOrder.First, segment);

            if (!firstBranchMatched.IsCompletedSuccessfully)
                return TryBranchPairAwaitedAsync(firstBranchMatched, segment);

            if (firstBranchMatched.Result)
                return s_true;

            return TryBranchAsync(_branchOrder.Second, segment);
        }

        private bool TryLiteralBranch(ReadOnlyMemory<char> decodedSegment)
        {
            if (!_node.LiteralChildren.TryGetValue(decodedSegment, out RouteNode branchNode))
                return false;

            AdvanceToNextSegment(branchNode);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<ValueParseResult> ParseSegment(ReadOnlyMemory<char> decodedSegment, ParameterParser parser) => parser.Parse
        (
            new ValueParserContext
            {
                Segment = decodedSegment,
                Services = Services,
                Arguments = parser.Arguments,
                Cancellation = Cancellation
            }
        );

        private ValueTask<bool> TryParsedBranchAsync(ReadOnlyMemory<char> decodedSegment, int startIndex)
        {
            for (int i = startIndex; i < _node.ParsedChildren.Count; i++)
            {
                KeyValuePair<ParameterParser, RouteNode> parsedChild = _node.ParsedChildren[i];

                ValueTask<ValueParseResult> parseResult = ParseSegment(decodedSegment, parsedChild.Key);

                if (!parseResult.IsCompletedSuccessfully)
                    return TryParsedBranchAwaitedAsync(parsedChild, parseResult, decodedSegment, i);

                if (TryAcceptParsedBranch(parsedChild, parseResult.Result))
                    return s_true;
            }

            return s_false;
        }

        private bool TryAcceptParsedBranch(KeyValuePair<ParameterParser, RouteNode> parsedChild, ValueParseResult parseResult)
        {
            if (!parseResult.Success)
                return false;

            if (parsedChild.Key.Definition.ParameterName is { Length: > 0 } parameterName)
                // This will overwrite any existing parameter on the given key
                Parameters[parameterName] = parseResult.Parsed;

            AdvanceToNextSegment(parsedChild.Value);
            return true;
        }
        #endregion

        public RouteMatchCursor(RouteNode node, HttpVerb verb, Uri uri, IServiceProvider services, IDictionary<string, object?> parameters, MatchingPrecedence matchingPrecedence, CancellationToken cancellation)
        {
            _segment = new DelimitedSegment
            (
                // Escaped path, not percent decoded -> "/path%2Fto%2Fsomewhere/" will be treated as a single segment
                uri.AbsolutePath.AsMemory(),
                '/'
            );
            _branchOrder = BranchOrder.From(matchingPrecedence);
            _root = _node = node;

            Cancellation = cancellation;
            Parameters = parameters;
            Services = services;
            Verb = verb;

            AdvanceToNextSegment(_root);
            _phase = GetPhaseForCurrentNode();
        }

        public HandlerRegistration Current { get; private set; } = null!;

        public ReadOnlyMemory<char> RemainingPath { get; private set; }

        public CancellationToken Cancellation { get; }

        public IServiceProvider Services { get; }

        public IDictionary<string, object?> Parameters { get; }

        public HttpVerb Verb { get; }

        public bool Completed => _phase is MatchPhase.Done;

        public ValueTask DisposeAsync()
        {
            if (_decodedSegmentBuffer is not null)
            {
                s_arrayPool.Return(_decodedSegmentBuffer, clearArray: false);
                _decodedSegmentBuffer = null;
            }

            return default;
        }

        public void Reset()
        {
            _nextDecodedSegment = 0;

            _segment.Reset();
            AdvanceToNextSegment(_root);
            _phase = GetPhaseForCurrentNode();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            while (!Completed)
            {
                if (_phase is MatchPhase.EmitHandlers)
                {
                    if (TryEmitHandler())
                        return s_true;

                    // No handler terminated the pipeline, go to the branch matching phase.
                    _phase = MatchPhase.Branch;
                }

                Debug.Assert(_phase is MatchPhase.Branch, $"Unknown phase: {_phase}");

                if (_segment.HasValue)
                {
                    ValueTask<bool> branchMatched = _node.SingleBranch is not null
                        // fast path
                        ? TrySingleBranchesAsync()
                        : TryBranchPairAsync();

                    if (!branchMatched.IsCompletedSuccessfully)
                        return MoveNextAwaitedAsync(branchMatched);

                    if (branchMatched.Result)
                    {
                        _phase = GetPhaseForCurrentNode();
                        continue;
                    }
                }

                _phase = MatchPhase.Done;
                break;
            }

            return s_false;
        }
    }
}
