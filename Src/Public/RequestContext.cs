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
    /// TODO
    /// </summary>
    public sealed class RequestContext
    {
        /// <summary>
        /// Request parameters
        /// </summary>
        public required Dictionary<string, object?> Parameters { get; init; }

        /// <summary>
        /// The available services.
        /// </summary>
        public required IServiceProvider Services { get; init; }

        /// <summary>
        /// The original request
        /// </summary>
        public required HttpRequestMessage Request { get; init; }

        /// <summary>
        /// <see cref="CancellationToken"/> associated with this request.
        /// </summary>
        public required CancellationToken Cancellation { get; init; }
    }
}
