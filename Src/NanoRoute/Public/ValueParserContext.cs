/********************************************************************************
* ValueParserContext.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

namespace NanoRoute
{
    /// <summary>
    /// Carries the current route segment and request-scoped services into an asynchronous value parser.
    /// </summary>
    public readonly record struct ValueParserContext
    {
        /// <summary>
        /// Gets the decoded route segment.
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
    }
}


