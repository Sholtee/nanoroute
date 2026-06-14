/********************************************************************************
* Delegates.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NanoRoute
{
    /// <summary>
    /// Creates a router instance from a configured <see cref="RouterBuilder{TRouter, TConfig}"/>.
    /// </summary>
    /// <typeparam name="TRouter">The concrete router type produced by the factory.</typeparam>
    /// <typeparam name="TConfig">The strongly typed router configuration used by the builder.</typeparam>
    /// <param name="scope">
    /// The route scope that contains the current route and parser registrations.
    /// </param>
    /// <param name="config">The configuration assigned to the router instance being created.</param>
    /// <returns>
    /// A <typeparamref name="TRouter"/> instance backed by the builder's current route snapshot.
    /// </returns>
    /// <remarks>
    /// Exceptions thrown by the factory propagate to the caller of <see cref="RouterBuilder{TRouter, TConfig}.CreateRouter(Action{TConfig})"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = new RouterBuilder&lt;MyRouter, RouterConfig&gt;(static (scope, config) =&gt; new MyRouter(scope, config));
    /// </code>
    /// </example>
    public delegate TRouter RouterFactoryDelegate<TRouter, TConfig>(RouteScopeBuilder scope, TConfig config) where TRouter : RouterBase<TConfig> where TConfig : RouterConfig;

    /// <summary>
    /// Invokes the next compatible handler in the current routing pipeline.
    /// </summary>
    /// <returns>
    /// The <see cref="HttpResponseMessage"/> produced by the next matching handler.
    /// </returns>
    /// <remarks>
    /// This delegate is passed into <see cref="RequestHandlerDelegate"/> so handlers can opt into pipeline-style
    /// composition. When the current handler does not call it, the pipeline stops at the current handler. Matching
    /// continues only within the route branch already selected for the request; sibling branches are not revisited.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddHandler("GET", "/api/users/{id:int}/", async (requestContext, callNext) =&gt;
    /// {
    ///     requestContext.Parameters["StartTime"] = DateTimeOffset.UtcNow;
    ///     return await callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> CallNextHandlerDelegate();

    /// <summary>
    /// Represents a request handler in the router pipeline.
    /// </summary>
    /// <param name="requestContext">The current request context, including parsed route parameters, remaining path, and services.</param>
    /// <param name="callNext">A delegate that invokes the next compatible handler in the pipeline.</param>
    /// <returns>
    /// The response produced by the current handler, or by a later handler when <paramref name="callNext"/> is invoked.
    /// </returns>
    /// <remarks>
    /// Handler may signal HTTP failures by calling <c>HttpRequestException.Throw(...)</c>. When
    /// <see cref="NanoRouteJsonExtensions.AddJsonErrorDetails{TBuilder}(TBuilder)"/>, or equivalent custom
    /// middleware is registered, those exceptions can be translated into structured error responses. Use
    /// <see cref="NanoRouteJsonExtensions.AddJsonErrorDetails{TBuilder}(TBuilder, Action{JsonErrorDetailsOptions})"/>
    /// when you need to configure developer diagnostics or serialization metadata. Throwing other exception types is
    /// also supported, but they are treated as unexpected failures:
    /// <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/> converts them into internal
    /// server error responses, while without such middleware they propagate to the caller unchanged.
    /// <see cref="OperationCanceledException"/> is left untouched so caller-driven cancellation can propagate to the
    /// transport layer or hosting code.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddHandler("GET", "/api/users/{user_id:int}/*", (requestContext, callNext) =&gt;
    /// {
    ///     requestContext.Parameters["User"] = LoadUser((int) requestContext.Parameters["user_id"]!);
    ///     return callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> RequestHandlerDelegate(RequestContext requestContext, CallNextHandlerDelegate callNext);
}
