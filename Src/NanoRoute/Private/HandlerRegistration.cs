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
    internal record struct HandlerRegistration(RequestHandlerDelegate Handler, string Pattern, Dictionary<string, object?>? AttachedParameters = null)
    {
        /// <summary>
        /// Returns true if the registration should match as a prefix.
        /// </summary>
        public bool IsPrefix { get; } = Pattern.EndsWith("/");
    }
}