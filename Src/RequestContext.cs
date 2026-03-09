/********************************************************************************
* MiniRequestContext.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute
{
    /// <summary>
    /// Request context
    /// </summary>
    public sealed class RequestContext<TRequest>
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
        public required TRequest Request { get; init; }
    }
}
