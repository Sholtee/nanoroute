/********************************************************************************
* RequestContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace NanoRoute
{
    /// <summary>
    /// Carries request-specific data through the NanoRoute handler pipeline.
    /// </summary>
    /// <remarks>
    /// The same context instance is passed to each matching handler. Handlers can use
    /// <see cref="Parameters"/> to share values, <see cref="Services"/> to resolve dependencies, and
    /// <see cref="Cancellation"/> to observe either caller-initiated cancellation or router timeouts.
    /// <see cref="Parameters"/> is a shared mutable dictionary for the active pipeline, so later middleware or
    /// handlers may overwrite values that were written earlier by route binding, query binding, JSON body binding,
    /// or other handlers.
    /// </remarks>
    public readonly record struct RequestContext(Dictionary<string, object?> Parameters, IServiceProvider Services, HttpRequestMessage Request, CancellationToken Cancellation);
}
