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
            /// TODO
            /// </summary>
            SingleBranch,

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

        private readonly RouteNode _root;

        private char[]? _decodedSegmentBuffer;

        // Keep DelimitedSegment instead of Uri.Segments: UrlSegmentBenchmarks shows it avoids eager segment
        // array/string allocation and preserves this cursor's lazy traversal model.
        //
        // DelimitedSegment is mutable, so keep this field non-readonly to let MoveNext() update the cursor
        // itself instead of a defensive copy.
        private DelimitedSegment _segment;

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
            _phase = await branchMatched.ConfigureAwait(false) ? GetPhaseForCurrentNode() : MatchPhase.Done;
            return await MoveNextAsync().ConfigureAwait(false);
        }

        private async ValueTask<bool> TryBranchPairAwaitedAsync(ValueTask<bool> firstBranchMatched, ReadOnlyMemory<char> decodedSegment)
        {
            if (await firstBranchMatched.ConfigureAwait(false))
                return true;

            return await TryBranchAsync(_branchOrder.Second, decodedSegment).ConfigureAwait(false);
        }

        private async ValueTask<bool> TryParsedBranchAwaitedAsync(KeyValuePair<ParameterParser, RouteNode> parsedChild, ValueTask<ValueParseResult> parseResult, ReadOnlyMemory<char> decodedSegment, int branchIndex)
        {
            if (TryAcceptParsedBranch(parsedChild, await parseResult.ConfigureAwait(false)))
                return true;

            return await TryParsedBranchAsync(decodedSegment, branchIndex + 1).ConfigureAwait(false);
        }

        private async ValueTask<bool> TryAcceptParsedBranchAwaitedAsync(KeyValuePair<ParameterParser, RouteNode> parsedChild, ValueTask<ValueParseResult> parseResult)
        {
            if (!TryAcceptParsedBranch(parsedChild, await parseResult.ConfigureAwait(false)))
                return false;

            return await TrySingleBranchesAsync();
        }
        #endregion

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
        private MatchPhase GetPhaseForCurrentNode()
        {
            if (_node.SingleBranch is not null)
                return MatchPhase.SingleBranch;

            if (_node.HandlerRegistrations.Count > 0)
                return MatchPhase.EmitHandlers;

            return MatchPhase.Branch;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<bool> TryBranchAsync(BranchKind branchKind, ReadOnlyMemory<char> decodedSegment) => branchKind switch
        {
            BranchKind.Literal => new ValueTask<bool>(TryLiteralBranch(decodedSegment)),
            BranchKind.Parsed => TryParsedBranchAsync(decodedSegment),
            _ => throw new ArgumentOutOfRangeException(nameof(branchKind))
        };

        private static readonly ValueTask<bool>
            s_true = new(true),
            s_false = new(false);

        internal ValueTask<bool> TrySingleBranchesAsync()
        {
            while (_segment.HasValue && _node.SingleBranch is not null)
            {
                ReadOnlyMemory<char> decodedSegment = GetSegmentForMatching();

                switch (_node.SingleBranch)
                {
                    case KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch:
                        if (!ReadOnlyMemoryCharComparer.Instance.Equals(literalBranch.Key, decodedSegment))
                            return s_false;

                        AdvanceToNextSegment(literalBranch.Value);
                        break;

                    case KeyValuePair<ParameterParser, RouteNode> parsedBranch:
                        ValueTask<ValueParseResult> parseResult = ParseSegment(decodedSegment, parsedBranch.Key);

                        if (!parseResult.IsCompletedSuccessfully)
                            return TryAcceptParsedBranchAwaitedAsync(parsedBranch, parseResult);

                        if (!TryAcceptParsedBranch(parsedBranch, parseResult.Result))
                            return s_false;

                        break;

                    default:
                        Debug.Fail($"Unknown single branch type: {_node.SingleBranch.GetType().Name}");
                        return s_false;
                }    
            }

            return s_true;
        }

        private ValueTask<bool> TryBranchPairAsync()
        {
            // Decode only when the matcher is about to inspect the segment. Prefix handlers can still run
            // without paying this cost and they can catch invalid escape errors, too.
            ReadOnlyMemory<char> decodedSegment = GetSegmentForMatching();

            ValueTask<bool> firstBranchMatched = TryBranchAsync(_branchOrder.First, decodedSegment);

            if (!firstBranchMatched.IsCompletedSuccessfully)
                return TryBranchPairAwaitedAsync(firstBranchMatched, decodedSegment);

            if (firstBranchMatched.Result)
                return s_true;

            return TryBranchAsync(_branchOrder.Second, decodedSegment);
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

        private ValueTask<bool> TryParsedBranchAsync(ReadOnlyMemory<char> decodedSegment, int startIndex = 0)
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
            _segment.Reset();
            AdvanceToNextSegment(_root);
            _phase = GetPhaseForCurrentNode();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            while (!Completed)
            {
                switch (_phase)
                {
                    case MatchPhase.EmitHandlers:
                        if (TryEmitHandler())
                            return new ValueTask<bool>(true);

                        // No handler terminated the pipeline, go to the branch matching phase.
                        _phase = MatchPhase.Branch;
                        goto case MatchPhase.Branch;

                    case MatchPhase.SingleBranch:
                        if (!_segment.HasValue)
                        {
                            _phase = MatchPhase.Done;
                            break;
                        }

                        ValueTask<bool> singleBranchMatched = TrySingleBranchesAsync();
                        if (!singleBranchMatched.IsCompletedSuccessfully)
                            return MoveNextAwaitedAsync(singleBranchMatched);

                        _phase = singleBranchMatched.Result ? GetPhaseForCurrentNode() : MatchPhase.Done;
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

                        _phase = branchMatched.Result ? GetPhaseForCurrentNode() : MatchPhase.Done;
                        break;

                    default:
                        Debug.Fail($"Unknown phase: {_phase}");
                        return s_false;
                }
            }

            return s_false;
        }
    }
}
