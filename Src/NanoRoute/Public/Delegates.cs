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
    /// <param name="routerBuilder">
    /// The builder that contains the current route registrations, parser registrations, metadata, and configuration.
    /// </param>
    /// <returns>
    /// A <typeparamref name="TRouter"/> instance backed by the builder's current route snapshot.
    /// </returns>
    /// <remarks>
    /// Exceptions thrown by the factory propagate to the caller of <see cref="RouterBuilder{TRouter, TConfig}.CreateRouter"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = new RouterBuilder&lt;MyRouter, RouterConfig&gt;(static b =&gt; new MyRouter(b));
    /// </code>
    /// </example>
    public delegate TRouter RouterFactoryDelegate<TRouter, TConfig>(RouterBuilder<TRouter, TConfig> routerBuilder) where TConfig : RouterConfig, new();

    /// <summary>
    /// Updates a typed builder configuration object.
    /// </summary>
    /// <typeparam name="TConfig">The configuration object type.</typeparam>
    /// <param name="config">The configuration currently visible from the builder scope.</param>
    /// <returns>The replacement configuration.</returns>
    /// <remarks>
    /// Configuration delegates run during route registration, not during request processing. Extension methods that
    /// use <see cref="RouteScopeBuilder.Metadata"/> can use this delegate shape for scoped builder settings.
    /// A module registration should capture the configuration visible when it is registered; later
    /// <c>ConfigureXxx()</c> calls affect later registrations, not registrations that already exist.
    /// Exceptions thrown by the delegate propagate from the configuration method that invoked it.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.ConfigureRouting(config =&gt; config with { MatchingPrecedence = MatchingPrecedence.ParameterizedFirst });
    /// </code>
    /// </example>
    public delegate TConfig ConfigureBuilderDelegate<TConfig>(TConfig config);

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
    /// <see cref="NanoRouteJsonExtensions.ConfigureJsonErrorDetails{TBuilder}(TBuilder, ConfigureBuilderDelegate{JsonErrorDetailsConfig})"/>
    /// before registering JSON error handling when you need to include developer diagnostics. Throwing other
    /// exception types is also supported, but they are treated as unexpected failures:
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
