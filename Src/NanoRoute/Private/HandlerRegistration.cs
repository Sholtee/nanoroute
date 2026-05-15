/********************************************************************************
* HandlerRegistration.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    /// <summary>
    /// Represents a request <paramref cref="Handler"/> registration.
    /// </summary>
    internal sealed record HandlerRegistration(RequestHandlerDelegate Handler, string Pattern)
    {
        /// <summary>
        /// Returns true if the registration should match as a prefix.
        /// </summary>
        public bool IsPrefix { get; } = Pattern.EndsWith(RouteBuilder.CurrentPrefix);
    }
}
