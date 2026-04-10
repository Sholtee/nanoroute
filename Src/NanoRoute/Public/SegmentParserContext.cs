/********************************************************************************
* SegmentParserContext.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Web;

namespace NanoRoute
{
    /// <summary>
    /// Carries the current route segment and request-scoped services into an asynchronous segment parser.
    /// </summary>
    public record struct SegmentParserContext
    {
        /// <summary>
        /// Gets the raw route segment exactly as it appeared in the request URI path.
        /// </summary>
        public required ReadOnlyMemory<char> Segment { get; init; }
        
        /// <summary>
        /// Gets the request-scoped service provider.
        /// </summary>
        public required IServiceProvider Services { get; init; }
 
        /// <summary>
        /// Gets the parser arguments that were bound during route registration.
        /// </summary>
        public object? Arguments { get; init; }

        /// <summary>
        /// Gets the linked pipeline cancellation token.
        /// </summary>
        public CancellationToken Cancellation { get; init; }

        /// <summary>
        /// Gets the decoded route segment, computing the decoded representation only on first access per context instance.
        /// </summary>
        public ReadOnlyMemory<char> DecodedSegment
        {
            get
            {
                if (field.Equals(default))
                {
                    field = Segment.Span.IndexOf('%') < 0
                        ? Segment
                        : HttpUtility.UrlDecode(Segment.ToString()).AsMemory();
                }
                return field;
            }
        }
    }
}

