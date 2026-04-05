/********************************************************************************
* SegmentParserContext.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

namespace NanoRoute
{
    /// <summary>
    /// Carries the current route segment and request-scoped services into an asynchronous segment parser.
    /// </summary>
    /// <param name="Segment">The percent-decoded route segment currently being parsed.</param>
    /// <param name="Services">The request-scoped service provider available to the parser.</param>
    /// <param name="Cancellation">
    /// A linked token that is canceled when the caller cancels the request or the router timeout elapses.
    /// </param>
    public readonly record struct SegmentParserContext(string Segment, IServiceProvider Services, CancellationToken Cancellation);
}

