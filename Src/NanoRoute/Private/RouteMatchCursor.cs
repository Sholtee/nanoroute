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

    internal sealed class RouteMatchCursor(RouteNode node, HttpVerb verb, Uri uri, IServiceProvider services, MatchingBehavior matchingBehavior, CancellationToken cancellation): IDisposable
    {
        private static readonly ArrayPool<char> s_arrayPool = ArrayPool<char>.Create();

        private readonly BranchOrder _branchOrder = matchingBehavior switch
        {
            MatchingBehavior.LiteralFirst => new BranchOrder(BranchKind.Literal, BranchKind.Parsed),
            MatchingBehavior.ParameterizedChildrenFirst => new BranchOrder(BranchKind.Parsed, BranchKind.Literal),
            _ => throw new ArgumentOutOfRangeException(nameof(matchingBehavior))
        };

        private readonly Dictionary<string, object?> _parameters = new(StringComparer.OrdinalIgnoreCase);

        private readonly char[] _decodedSegmentBuffer = s_arrayPool.Rent(uri.AbsolutePath.Length);

        // DelimitedSegment is a mutable struct: keep this field non-readonly so MoveNext() updates the cursor
        // itself instead of a defensive copy.
        private DelimitedSegment _segment = SplitUri(uri);

        private MatchPhase _phase = MatchPhase.EmitHandlers;

        private ReadOnlyMemory<char> _decodedSegment;

        private IList<HandlerRegistration>? _handlers;

        private int
            _handlerIndex,
            _nextDecodedSegment;

        private static DelimitedSegment SplitUri(Uri uri)
        {
            DelimitedSegment result = new
            (
                // Escaped path, not percent decoded -> "/path%2Fto%2Fsomewhere/" will be treated as a single segment
                uri.AbsolutePath.AsMemory(),
                '/'
            );
            result.MoveNext();
            return result;
        }

        public HandlerRegistration Current { get; private set; } = null!;

        public void Dispose() => s_arrayPool.Return(_decodedSegmentBuffer, clearArray: false);

        public async ValueTask<bool> MoveNextAsync()
        {
            while (_phase is not MatchPhase.Done)
            {
                cancellation.ThrowIfCancellationRequested();

                switch (_phase)
                {
                    case MatchPhase.EmitHandlers:
                        if (TryEmitHandler())
                            return true;

                        // No handler terminated the pipeline, go to the first branch
                        _phase = MatchPhase.FirstBranch;
                        break;

                    case MatchPhase.FirstBranch:
                        if (!_segment.HasValue)
                        {
                            _phase = MatchPhase.Done;
                            break;
                        }

                        // Decode only when the matcher is about to inspect the segment. Prefix handlers can still run
                        // without paying this cost, while invalid escapes are reported before any child branch is parsed.

                        if (!UrlUtils.TryDecodeUrl(_segment.Current.Span, _decodedSegmentBuffer.AsSpan(_nextDecodedSegment), UrlDecodeMode.Path, out int charsWritten))
                            HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, Resources.ERR_DECODING_FAILED);

                        _decodedSegment = _decodedSegmentBuffer.AsMemory(_nextDecodedSegment, charsWritten);
                        _nextDecodedSegment += charsWritten;

                        _phase = await TryBranchAsync(_branchOrder.First)
                            ? MatchPhase.EmitHandlers
                            : MatchPhase.SecondBranch;
                        break;

                    case MatchPhase.SecondBranch:
                        Debug.Assert(_segment.HasValue, "Second branch should not be reached when there is no segment to process");

                        _phase = await TryBranchAsync(_branchOrder.Second)
                            ? MatchPhase.EmitHandlers
                            : MatchPhase.Done;
                        break;
                }
            }

            return false;
        }

        private void AdvanceToNextSegment(RouteNode nextNode)
        {
            node = nextNode;

            _handlerIndex = 0;
            _handlers = null;
            _decodedSegment = default;
            
            _segment.MoveNext();
        }

        private bool TryEmitHandler()
        {
            if (_handlers is null && !node.HandlerRegistrations.TryGetValue(verb, out _handlers))
                return false;

            while (_handlerIndex < _handlers.Count)
            {
                HandlerRegistration candidate = _handlers[_handlerIndex++];

                if (_segment.HasValue && !candidate.IsPrefix)
                    continue;

                Current = candidate with { AttachedParameters = _parameters };
                return true;
            }

            return false;
        }

        private ValueTask<bool> TryBranchAsync(BranchKind branchKind) => branchKind switch
        {
            BranchKind.Literal => new ValueTask<bool>(TryLiteralBranch()),
            BranchKind.Parsed => TryParsedBranchAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(branchKind))
        };

        private bool TryLiteralBranch()
        {
            if (!node.LiteralChildren.TryGetValue(_decodedSegment, out RouteNode branchNode))
                return false;

            AdvanceToNextSegment(branchNode);
            return true;
        }

        private ValueTask<bool> TryParsedBranchAsync(int startIndex = 0)
        {
            for (int i = startIndex; i < node.ParsedChildren.Count; i++)
            {
                RouteNode branchNode = node.ParsedChildren[i];
                ParameterParser parser = branchNode.ParameterParser!;

                ValueTask<ValueParseResult> parseResult = parser.Parse
                (
                    new ValueParserContext
                    {
                        Segment = _decodedSegment,
                        Services = services,
                        Arguments = parser.Arguments,
                        Cancellation = cancellation
                    }
                );

                if (!parseResult.IsCompletedSuccessfully)
                    return TryParsedBranchAwaitedAsync(parseResult, i, branchNode);

                if (TryAcceptParsedBranch(branchNode, parseResult.Result))
                    return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        // Pay the state machine cost only for incomplete tasks 
        private async ValueTask<bool> TryParsedBranchAwaitedAsync(ValueTask<ValueParseResult> parseResult, int branchIndex, RouteNode branchNode)
        {
            if (TryAcceptParsedBranch(branchNode, await parseResult))
                return true;

            return await TryParsedBranchAsync(branchIndex + 1);
        }

        private bool TryAcceptParsedBranch(RouteNode branchNode, ValueParseResult parseResult)
        {
            if (!parseResult.Success)
                return false;

            if (branchNode.ParameterParser!.Definition.ParameterName is { Length: > 0 } parameterName)
                _parameters[parameterName] = parseResult.Parsed;

            AdvanceToNextSegment(branchNode);
            return true;
        }

        private readonly record struct BranchOrder(BranchKind First, BranchKind Second);

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
            /// Explore the branch category that currently has higher precedence.
            /// </summary>
            FirstBranch,

            /// <summary>
            /// Explore the remaining branch category after the preferred one has been attempted.
            /// </summary>
            SecondBranch,

            /// <summary>
            /// Terminating step
            /// </summary>
            Done
        }
    }
}
