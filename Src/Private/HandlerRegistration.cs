/********************************************************************************
* HandlerRegistration.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Represents a request <paramref cref="Handler"/> registration.
    /// </summary>
    internal sealed record HandlerRegistration(RequestHandler Handler, bool IsPrefix)
    {
        /// <summary>
        /// Gets the parameter snapshot associated with the current match.
        /// </summary>
        public Dictionary<string, object?>? AttachedParameters { get; init; }
    }
}