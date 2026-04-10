/********************************************************************************
* RouteMatchCursor.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    internal sealed class RouteMatchCursor(RouteNode root, HttpVerb verb, Uri uri, IServiceProvider services, MatchingBehavior matchingBehavior, CancellationToken cancellation)
    {
        private readonly BranchOrder _branchOrder = matchingBehavior switch
        {
            MatchingBehavior.LiteralFirst => new BranchOrder(BranchKind.Literal, BranchKind.Parsed),
            MatchingBehavior.ParameterizedChildrenFirst => new BranchOrder(BranchKind.Parsed, BranchKind.Literal),
            _ => throw new ArgumentOutOfRangeException(nameof(matchingBehavior))
        };

        private Frame[] _stack =
        [
            new Frame
            {
                Node = root,
                Verb = verb,
                Segment = NextSegment
                (
                    // Escaped path, not percent decoded -> "/path%2Fto%2Fsomewhere/" will be treated as a single segment
                    new UriSegment(uri.AbsolutePath)
                ),
                Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                Phase = MatchPhase.EmitHandlers
            },
            default,
            default,
            default,
            default,
            default,
            default,
            default
        ];

        private int _stackLength = 1;

        private ref Frame TopFrame => ref _stack[_stackLength - 1];

        private static UriSegment NextSegment(UriSegment segment)
        {
            segment.MoveNext();
            return segment;
        }

        public HandlerRegistration Current { get; private set; } = null!;

        public override string ToString()
        {
            StringBuilder builder = new();

            builder
                .Append(nameof(RouteMatchCursor))
                .Append(" { Stack = [");

            for (int i = 0; i < _stackLength; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                Frame frame = _stack[i];

                builder.AppendFormat
                (
                    "{0}: {{ Phase = {1}, Segment = '{2}', HandlerIndex = {3}, ParsedChildIndex = {4} }}",
                    i,
                    frame.Phase,
                    frame.Segment.Current,
                    frame.HandlerIndex,
                    frame.ParsedChildIndex
                );
            }

            return builder
                .Append("] }")
                .ToString();
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            while (_stackLength > 0)
            {
                cancellation.ThrowIfCancellationRequested();

                ref Frame frame = ref TopFrame;

                switch (frame.Phase)
                {
                    case MatchPhase.EmitHandlers:
                        if (TryEmitHandler(out HandlerRegistration match))
                        {
                            Current = match;
                            return true;
                        }

                        frame.Phase = frame.Segment.HasValue
                            ? MatchPhase.FirstBranch
                            : MatchPhase.Done;

                        break;

                    case MatchPhase.FirstBranch:
                        frame.Phase = MatchPhase.SecondBranch;

                        if (_branchOrder.First is BranchKind.Literal)
                        {
                            TryPushLiteralBranch();
                            break;
                        }

                        await TryPushParsedBranchAsync();
                        break;

                    case MatchPhase.SecondBranch:
                        frame.Phase = MatchPhase.Done;

                        if (_branchOrder.Second is BranchKind.Literal)
                        {
                            TryPushLiteralBranch();
                            break;
                        }

                        await TryPushParsedBranchAsync();
                        break;

                    case MatchPhase.Done:
                        PopFrame();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return false;
        }

        private void PushFrame(Frame frame)
        {
            if (_stackLength == _stack.Length)
                Array.Resize(ref _stack, _stack.Length * 2);

            _stack[_stackLength++] = frame;
        }

        private void PopFrame() => _stack[--_stackLength] = default;

        private bool TryEmitHandler(out HandlerRegistration match)
        {
            ref Frame frame = ref TopFrame;

            match = null!;

            if (!frame.Node.HandlerRegistrations.TryGetValue(frame.Verb, out List<HandlerRegistration>? handlers))
                return false;

            while (frame.HandlerIndex < handlers.Count)
            {
                HandlerRegistration candidate = handlers[frame.HandlerIndex++];

                if (frame.Segment.HasValue && !candidate.IsPrefix)
                    continue;

                match = candidate with { AttachedParameters = frame.Parameters };
                return true;
            }

            return false;
        }

        private bool TryPushLiteralBranch()
        {
            Frame frame = TopFrame;

            if (!frame.Segment.HasValue)
                return false;

            if (!frame.Node.LiteralChildren.TryGetValue(frame.Segment.Current, out RouteNode literalChild))
                return false;

            UriSegment nextSegment = NextSegment(frame.Segment);

            PushFrame
            (
                new Frame
                {
                    Node = literalChild,
                    Verb = frame.Verb,
                    Segment = nextSegment,
                    Parameters = frame.Parameters,
                    Phase = MatchPhase.EmitHandlers
                }
            );

            return true;
        }

        private async ValueTask<bool> TryPushParsedBranchAsync()
        {
            // This copy captures only stable values used across awaits; ParsedChildIndex must still be read from TopFrame below.
            Frame frame = TopFrame;

            if (!frame.Segment.HasValue)
                return false;

            UriSegment nextSegment = NextSegment(frame.Segment);

            while (TopFrame.ParsedChildIndex < frame.Node.ParsedChildren.Count)
            {
                RouteNode parsedChild = frame.Node.ParsedChildren[TopFrame.ParsedChildIndex++];

                SegmentParseResult parsed = await parsedChild.SegmentParser!.Parse
                (
                    new SegmentParserContext
                    {
                        Segment = frame.Segment.Current,
                        Services = services,
                        Arguments = parsedChild.SegmentParser.Arguments,
                        Cancellation = cancellation
                    }
                );

                if (!parsed.Success)
                    continue;

                Dictionary<string, object?> extended = parsedChild.SegmentParser.ParameterName is { Length: > 0 } parameterName
                    ? new(frame.Parameters, StringComparer.OrdinalIgnoreCase)
                    {
                        [parameterName] = parsed.Parsed
                    }
                    : frame.Parameters;

                PushFrame
                (
                    new Frame
                    {
                        Node = parsedChild,
                        Verb = frame.Verb,
                        Segment = nextSegment,
                        Parameters = extended,
                        Phase = MatchPhase.EmitHandlers
                    }
                );

                return true;
            }

            return false;
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
            /// The frame has no more work to do and can be removed from the stack.
            /// </summary>
            Done
        }

        private struct Frame
        {
            public RouteNode Node;

            public HttpVerb Verb;

            public UriSegment Segment;

            public Dictionary<string, object?> Parameters;

            public MatchPhase Phase;

            public int HandlerIndex;

            public int ParsedChildIndex;
        }
    }
}
