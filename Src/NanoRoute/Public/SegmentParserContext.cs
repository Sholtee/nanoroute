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
    public readonly record struct SegmentParserContext(ReadOnlyMemory<char> Segment, IServiceProvider Services, object? Arguments, CancellationToken Cancellation);
}

