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
        private protected readonly RouteNode _root;

        private protected RoutingContext(RouteNode root) => _root = root;
    }
}