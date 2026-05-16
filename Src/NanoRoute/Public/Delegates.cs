/********************************************************************************
* Delegates.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public delegate TRouter RouterFactoryDelegate<TRouter, TConfig>(RouterBuilder<TRouter, TConfig> routerBuilder) where TRouter : Router where TConfig : RouterConfig, new();

    /// <summary>
    /// Binds raw parser arguments to an opaque object that is cached with the route definition.
    /// </summary>
    /// <param name="rawArgs">
    /// The raw parser arguments as parsed from the route template, keyed case-insensitively.
    /// </param>
    /// <returns>
    /// A parser-specific object that will later be exposed through <see cref="ValueParserContext.Arguments"/>.
    /// Return <see langword="null"/> when the parser does not need a bound payload.
    /// </returns>
    /// <remarks>
    /// This delegate runs during route registration, not during request processing. It is the right place to
    /// validate parser arguments, parse numeric limits, or precompile regular expressions once.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddValueParser
    /// (
    ///     "int",
    ///     static rawArgs =>
    ///     (
    ///         Min: rawArgs.TryGetValue("min", out string? min) ? int.Parse(min) : null,
    ///         Max: rawArgs.TryGetValue("max", out string? max) ? int.Parse(max) : null
    ///     ),
    ///     static context =>
    ///     {
    ///         var args = ((int? Min, int? Max)) context.Arguments!;
    ///         return ValueTask.FromResult(new ValueParseResult(true, context.Segment));
    ///     }
    /// );
    /// </code>
    /// </example>
    public delegate object? BindArgumentsDelegate(IReadOnlyDictionary<string, string> rawArgs);

    /// <summary>
    /// Represents a synchronous value parser.
    /// </summary>
    /// <param name="segment">The decoded segment extracted from the request URI.</param>
    /// <param name="arguments">
    /// The parser-specific argument payload produced by <see cref="BindArgumentsDelegate"/> during route registration,
    /// or <see langword="null"/> when the parser was registered without arguments.
    /// </param>
    /// <param name="parsed">The parsed value when the delegate returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// routerBuilder.AddValueParser("int", (string segment, object? arguments, out object? parsed) =&gt;
    /// {
    ///     var limits = ((int? Min, int? Max)) arguments!;
    ///
    ///     if (int.TryParse(segment, out int value))
    ///     {
    ///         if (limits.Min.HasValue &amp;&amp; value &lt; limits.Min.Value)
    ///         {
    ///             parsed = null;
    ///             return false;
    ///         }
    ///
    ///         parsed = value;
    ///         return true;
    ///     }
    ///
    ///     parsed = null;
    ///     return false;
    /// });
    /// </code>
    /// </example>
    public delegate bool SyncValueParserDelegate(ReadOnlyMemory<char> segment, object? arguments, out object? parsed);

    /// <summary>
    /// Tries to parse a single route segment into a value that can optionally be stored in <see cref="RequestContext.Parameters"/>.
    /// </summary>
    /// <param name="context">
    /// The parser context, including the decoded route segment, request services, and the linked pipeline cancellation token.
    /// </param>
    /// <returns>A <see cref="ValueParseResult"/> that describes whether the segment matched and what value it produced.</returns>
    /// <example>
    /// <code>
    /// routerBuilder.AddValueParser("user", static async (ValueParserContext context) =>
    /// {
    ///     if (!Guid.TryParse(context.Segment, out Guid userId))
    ///         return new ValueParseResult(false, null);
    ///
    ///     object? user = await context
    ///         .Services
    ///         .GetRequiredService&lt;IUserRepository&gt;()
    ///         .TryGetAsync(userId, context.Cancellation);
    ///
    ///     return new ValueParseResult(user is not null, user);
    /// });
    /// </code>
    /// </example>
    public delegate ValueTask<ValueParseResult> ValueParserDelegate(ValueParserContext context);

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
    /// </remarks>
    public delegate TConfig ConfigureBuilderDelegate<TConfig>(TConfig config);

    /// <summary>
    /// Converts an unexpected exception into an enriched <see cref="HttpRequestException"/>.
    /// </summary>
    /// <param name="exception">The exception thrown by a later handler in the routing pipeline.</param>
    /// <returns>
    /// The <see cref="HttpRequestException"/> that should be thrown by the exception-handling middleware.
    /// </returns>
    /// <remarks>
    /// Normalizers are configured with <see cref="NanoRouteExceptionExtensions.ConfigureExceptionHandling{TBuilder}(TBuilder, ConfigureBuilderDelegate{ExceptionHandlingConfig})"/>.
    /// They run only for exception types registered in <see cref="ExceptionHandlingConfig.ExceptionNormalizers"/>.
    /// Existing <see cref="HttpRequestException"/> and <see cref="OperationCanceledException"/> values are not
    /// normalized by <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/>.
    /// </remarks>
    public delegate HttpRequestException ExceptionNormalizer(Exception exception);

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
    /// routerBuilder.AddHandler("GET", "/api/users/{id:int}/", async (requestContext, callNext) =>
    /// {
    ///     requestContext.Parameters["StartTime"] = DateTimeOffset.UtcNow;
    ///     return await callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> CallNextHandlerDelegate();

    /// <summary>
    /// Represents a typed endpoint handler in the router pipeline.
    /// </summary>
    /// <typeparam name="TRequestContext">
    /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
    /// </typeparam>
    /// <param name="requestContext">
    /// The typed request object built from the current <see cref="RequestContext"/>.
    /// </param>
    /// <returns>The response produced by the current handler.</returns>
    /// <remarks>
    /// This delegate is used by <see cref="NanoRouteHandlerExtensions"/> overloads that do not expose
    /// <see cref="CallNextHandlerDelegate"/>. The pipeline stops when the handler returns.
    /// </remarks>
    public delegate Task<HttpResponseMessage> TypedRequestEndpointHandlerDelegate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(TRequestContext requestContext) where TRequestContext : new();

    /// <summary>
    /// Represents a typed request handler in the router pipeline.
    /// </summary>
    /// <typeparam name="TRequestContext">
    /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
    /// </typeparam>
    /// <param name="requestContext">
    /// The typed request object built from the current <see cref="RequestContext"/>.
    /// </param>
    /// <param name="callNext">A delegate that invokes the next compatible handler in the pipeline.</param>
    /// <returns>
    /// The response produced by the current handler, or by a later handler when <paramref name="callNext"/> is invoked.
    /// </returns>
    /// <remarks>
    /// This delegate is used by <see cref="NanoRouteHandlerExtensions"/> overloads that expose
    /// <see cref="CallNextHandlerDelegate"/>.
    /// </remarks>
    public delegate Task<HttpResponseMessage> TypedRequestHandlerDelegate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(TRequestContext requestContext, CallNextHandlerDelegate callNext) where TRequestContext : new();

    /// <summary>
    /// Represents a request handler in the router pipeline.
    /// </summary>
    /// <param name="requestContext">The current request context, including parsed route parameters and services.</param>
    /// <param name="callNext">A delegate that invokes the next compatible handler in the pipeline.</param>
    /// <returns>
    /// The response produced by the current handler, or by a later handler when <paramref name="callNext"/> is invoked.
    /// </returns>
    /// <remarks>
    /// Middleware may signal HTTP failures by calling <c>HttpRequestException.Throw(...)</c>. When
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

