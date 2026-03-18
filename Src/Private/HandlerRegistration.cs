/********************************************************************************
* HandlerRegistration.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Represents a request <paramref cref="Handler"/> registration against a given <paramref name="Pattern"/>.
    /// </summary>
    internal sealed record HandlerRegistration(RequestHandler Handler, string Pattern)
    {
        /// <summary>
        /// Gets the parameter snapshot associated with the current match.
        /// </summary>
        public Dictionary<string, object?>? AttachedParameters { get; init; }

        /// <summary>
        /// Returns true if the registration should match as a prefix.
        /// </summary>
        public bool IsPrefix { get; } = Pattern.EndsWith("/");
    }
}