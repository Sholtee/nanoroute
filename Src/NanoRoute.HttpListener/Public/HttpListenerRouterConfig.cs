/********************************************************************************
* HttpListenerRouterConfig.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.HttpListener
{
    /// <summary>
    /// Configuration settings for <see cref="HttpListenerRouter"/> snapshots.
    /// </summary>
    /// <remarks>
    /// The HttpListener adapter currently uses the shared <see cref="RouterConfig"/> settings, including
    /// <see cref="RouterConfig.MatchingPrecedence"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// HttpListenerRouterConfig config = new();
    /// </code>
    /// </example>
    public sealed class HttpListenerRouterConfig : RouterConfig
    {
    }
}
