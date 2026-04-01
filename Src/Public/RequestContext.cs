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
    /// <see cref="Cancellation"/> to respect request aborts or timeouts.
    /// </remarks>
    public sealed class RequestContext
    {
        /// <summary>
        /// Route parameters parsed from the matched URI together with values added by earlier handlers.
        /// </summary>
        public required Dictionary<string, object?> Parameters { get; init; }

        /// <summary>
        /// The available services.
        /// </summary>
        public required IServiceProvider Services { get; init; }

        /// <summary>
        /// The request currently being processed.
        /// </summary>
        public required HttpRequestMessage Request { get; init; }

        /// <summary>
        /// <see cref="CancellationToken"/> associated with this request.
        /// </summary>
        public required CancellationToken Cancellation { get; init; }
    }
}
