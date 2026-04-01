/********************************************************************************
* RoutingContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Provides shared access to the route tree used during building and routing.
    /// </summary>
    public abstract class RoutingContext
    {
        private protected readonly RouteNode _root;

        private protected RoutingContext(RouteNode root) => _root = root;
    }
}
