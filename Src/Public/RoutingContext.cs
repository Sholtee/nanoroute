/********************************************************************************
* RoutingContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// TOTO
    /// </summary>
    public abstract class RoutingContext
    {
        internal RouteNode Root { get; init; } = null!;  // this cannot be required =(
    }
}