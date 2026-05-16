/********************************************************************************
* RouterBuilderJsonExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Configures how <see cref="NanoRouteJsonExtensions.AddJsonErrorDetails{TBuilder}(TBuilder)"/> creates JSON
    /// <see cref="ErrorDetails"/> responses.
    /// </summary>
    /// <remarks>
    /// Instances are stored in <see cref="RouteScopeBuilder.Metadata"/> by
    /// <see cref="NanoRouteJsonExtensions.ConfigureJsonErrorDetails{TBuilder}(TBuilder, ConfigureBuilderDelegate{JsonErrorDetailsConfig})"/>.
    /// The configuration visible from the builder scope is captured when JSON error-detail middleware is registered.
    /// </remarks>
    public sealed record JsonErrorDetailsConfig
    {
        /// <summary>
        /// Gets a value indicating whether developer-facing diagnostic details should be included in JSON error responses.
        /// </summary>
        /// <remarks>
        /// Diagnostic details may contain exception messages or stack traces. Keep this value <see langword="false"/>
        /// for production responses unless the caller is trusted to see those details.
        /// </remarks>
        public bool PopulateErrorInfo { get; init; }

        /// <summary>
        /// Gets the JSON serialization metadata used for <see cref="ErrorDetails"/> responses.
        /// </summary>
        /// <remarks>
        /// Replace this value to use custom source-generated metadata, property naming, converters, or other
        /// serializer behavior for the error payload.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        public JsonTypeInfo<ErrorDetails> ErrorDetailsTypeInfo
        {
            get;
            init
            {
                Ensure.NotNull(value);
                field = value;
            }
        } = ErrorDetails.JsonTypeInfo;

        /// <summary>
        /// Gets the default JSON error-detail configuration.
        /// </summary>
        public static JsonErrorDetailsConfig Default { get; } = new();
    }


    /// <summary>
    /// Adds JSON-focused helpers for request body binding, structured error responses, and JSON responses.
    /// </summary>
    /// <remarks>
    /// These helpers are optional conveniences on top of the core routing pipeline. They are implemented as
    /// extension methods on <see cref="RouteScopeBuilder"/> and <see cref="HttpResponseMessage"/>.
    /// </remarks>
    public static class NanoRouteJsonExtensions
    {
        private const string JSON_MEDIA_TYPE =
#if NETSTANDARD2_1_OR_GREATER
            MediaTypeNames.Application.Json;
#else
            "application/json";
#endif
        private static RequestHandlerDelegate CreateHandler(JsonTypeInfo typeInfo, string paramName)
        {
            Ensure.NotNull(typeInfo);
            Ensure.NotNull(paramName);

            return async (RequestContext context, CallNextHandlerDelegate next) =>
            {
                context.Cancellation.ThrowIfCancellationRequested();

                if (context.Request.Content is not { } content)
                {
                    BadRequest(Resources.ERR_MISSING_BODY);
                    return null!;
                }

                if (!JSON_MEDIA_TYPE.Equals(content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase))
                    BadRequest(Resources.ERR_BAD_CONTENT_TYPE);

                Stream contentStream = await content.ReadAsStreamAsync();

                object? body = null;

                try
                {
                    body = await JsonSerializer.DeserializeAsync(contentStream, typeInfo, context.Cancellation);
                }
                catch (JsonException ex)
                {
                    BadRequest(ex.Message);
                }

                context.Parameters[paramName] = body;

                return await next();        
            };

            [DoesNotReturn]
            static void BadRequest(string error) => HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, error);
        }

        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder: RouteScopeBuilder
        {
            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should require a JSON body.</param>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// Requests without content, requests with a non-JSON content type, and requests with invalid JSON throw
            /// <see cref="HttpRequestException"/>. Add AddJsonErrorDetails to translate those into
            /// structured HTTP error responses. The deserialized body is written into
            /// <see cref="RequestContext.Parameters"/>, and an existing value with the same key is overwritten.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddJsonBody("POST", "/users/", MyJsonContext.Default.CreateUserRequest, "body")
            ///     .AddHandler("POST", "/users/", (context, _) =&gt;
            ///     {
            ///         CreateUserRequest body = (CreateUserRequest) context.Parameters["body"]!;
            ///         return Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.Created, body));
            ///     });
            /// </code>
            /// </example>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>,
            /// <paramref name="pattern"/>, <paramref name="typeInfo"/>, or <paramref name="paramName"/> is
            /// <see langword="null"/>.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has
            /// invalid route-template syntax.
            /// </exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, string pattern, JsonTypeInfo typeInfo, string paramName) =>
                routeScopeBuilder.AddHandler(verbs, pattern, CreateHandler(typeInfo, paramName));

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for a single HTTP method.
            /// </summary>
            /// <param name="verb">The HTTP method that should require a JSON body.</param>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>,
            /// <paramref name="pattern"/>, <paramref name="typeInfo"/>, or <paramref name="paramName"/> is
            /// <see langword="null"/>.
            /// </exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public TBuilder AddJsonBody(string verb, string pattern, JsonTypeInfo typeInfo, string paramName) =>
                routeScopeBuilder.AddJsonBody([verb /*will be null checked*/], pattern, typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should require a JSON body.</param>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the JSON-binding
            /// middleware is bound to the whole current builder scope for the selected HTTP methods.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="typeInfo"/>, or <paramref name="paramName"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not a supported HTTP method.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, JsonTypeInfo typeInfo, string paramName) =>
                routeScopeBuilder.AddJsonBody(verbs, RouteScopeBuilder.CurrentPrefix, typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for <c>POST</c>, <c>PUT</c>, and <c>PATCH</c>.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, <paramref name="typeInfo"/>, or <paramref name="paramName"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public TBuilder AddJsonBody(string pattern, JsonTypeInfo typeInfo, string paramName) =>
                routeScopeBuilder.AddJsonBody(HttpVerb.HavingBody, pattern, typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for <c>POST</c>, <c>PUT</c>, and <c>PATCH</c>.
            /// </summary>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the JSON-binding
            /// middleware is bound to the whole current builder scope for <c>POST</c>, <c>PUT</c>, and <c>PATCH</c>.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="typeInfo"/>, or <paramref name="paramName"/> is <see langword="null"/>.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public TBuilder AddJsonBody(JsonTypeInfo typeInfo, string paramName) =>
                routeScopeBuilder.AddJsonBody(HttpVerb.HavingBody, RouteScopeBuilder.CurrentPrefix, typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should require a JSON body.</param>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, string pattern, Type type, string paramName)
            {
                Ensure.NotNull(type);

                return routeScopeBuilder.AddJsonBody
                (
                    verbs,
                    pattern,
                    JsonSerializerOptions.Web.GetTypeInfo(type),
                    paramName
                );
            }

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata.
            /// </summary>
            /// <param name="verb">The HTTP method that should require a JSON body.</param>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
            public TBuilder AddJsonBody(string verb, string pattern, Type type, string paramName) =>
                routeScopeBuilder.AddJsonBody([verb /*will be null checked*/], pattern, type, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should require a JSON body.</param>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the JSON-binding
            /// middleware is bound to the whole current builder scope for the selected HTTP methods.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, Type type, string paramName) =>
                routeScopeBuilder.AddJsonBody(verbs, RouteScopeBuilder.CurrentPrefix, type, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata for <c>POST</c>, <c>PUT</c>, and <c>PATCH</c>.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
            public TBuilder AddJsonBody(string pattern, Type type, string paramName) =>
                routeScopeBuilder.AddJsonBody(HttpVerb.HavingBody, pattern, type, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata for <c>POST</c>, <c>PUT</c>, and <c>PATCH</c>.
            /// </summary>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the JSON-binding
            /// middleware is bound to the whole current builder scope for <c>POST</c>, <c>PUT</c>, and <c>PATCH</c>.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
            public TBuilder AddJsonBody(Type type, string paramName) =>
                routeScopeBuilder.AddJsonBody(HttpVerb.HavingBody, RouteScopeBuilder.CurrentPrefix, type, paramName);

            /// <summary>
            /// Updates the JSON error-detail configuration visible from the current builder scope.
            /// </summary>
            /// <param name="configure">
            /// A callback that receives the current configuration and returns the replacement configuration.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The configuration is stored in <see cref="RouteScopeBuilder.Metadata"/>. Child builders created after this
            /// method is called inherit the updated configuration; existing child builders keep their own scoped copy.
            /// Registered JSON error-detail middleware snapshots the configuration that is current at registration time.
            /// </remarks>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="configure"/>, or the value returned
            /// by <paramref name="configure"/> is <see langword="null"/>.
            /// </exception>
            public TBuilder ConfigureJsonErrorDetails(ConfigureBuilderDelegate<JsonErrorDetailsConfig> configure)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(configure);

                JsonErrorDetailsConfig config = configure(routeScopeBuilder.Metadata.GetOrDefault(JsonErrorDetailsConfig.Default));
                Ensure.NotNull(config);

                routeScopeBuilder.Metadata.Set(config);

                return routeScopeBuilder;
            }

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for all supported HTTP methods.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the error-detail
            /// middleware is bound to the whole current builder scope for all supported HTTP methods.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            public TBuilder AddJsonErrorDetails() =>
                routeScopeBuilder.AddJsonErrorDetails(RouteScopeBuilder.CurrentPrefix);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for all supported HTTP methods.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the error-detail middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope JSON error responses to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="pattern"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            public TBuilder AddJsonErrorDetails(string pattern) =>
                routeScopeBuilder.AddJsonErrorDetails(HttpVerb.Names, pattern);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for a single HTTP method.
            /// </summary>
            /// <param name="verb">The HTTP method that should use the error-detail middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the error-detail middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope JSON error responses to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, or <paramref name="pattern"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            public TBuilder AddJsonErrorDetails(string verb, string pattern) =>
                routeScopeBuilder.AddJsonErrorDetails([verb /*will be null checked*/], pattern);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the error-detail middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the error-detail
            /// middleware is bound to the whole current builder scope for the selected HTTP methods.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="verbs"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not a supported HTTP method.</exception>
            public TBuilder AddJsonErrorDetails(IEnumerable<string> verbs) =>
                routeScopeBuilder.AddJsonErrorDetails(verbs, RouteScopeBuilder.CurrentPrefix);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the error-detail middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the error-detail middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope JSON error responses to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// This helper wraps <see cref="HttpRequestException"/> values into JSON responses and also installs
            /// <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/> so unexpected
            /// exceptions are normalized before they reach the client. <see cref="OperationCanceledException"/> is
            /// not translated into JSON and continues to propagate to the caller unchanged. Use
            /// <see cref="ConfigureJsonErrorDetails{TBuilder}(TBuilder, ConfigureBuilderDelegate{JsonErrorDetailsConfig})"/> before
            /// calling this method to include developer diagnostics or replace the <see cref="ErrorDetails"/>
            /// serialization metadata.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddHandler("GET", "/items/{id:int}/", (context, _) =&gt;
            ///         throw new InvalidOperationException("Unexpected state"));
            /// </code>
            /// </example>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or <paramref name="pattern"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            public TBuilder AddJsonErrorDetails(IEnumerable<string> verbs, string pattern)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                JsonErrorDetailsConfig config = routeScopeBuilder.Metadata.GetOrDefault(JsonErrorDetailsConfig.Default);

                routeScopeBuilder
                    .AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                    {
                        try
                        {
                            return await next();
                        }
                        catch (HttpRequestException ex)
                        {
                            context.Request.Properties.TryGetValue(Router.TraceIdName, out object? traceId);

                            ErrorDetails errorDetails = ex.GetErrorDetails(config.PopulateErrorInfo, traceId as string);

                            return HttpResponseMessage.Json
                            (
                                errorDetails.Status,
                                errorDetails,
                                config.ErrorDetailsTypeInfo
                            );
                        }
                    })
                    .AddExceptionHandler(verbs, pattern);

                return routeScopeBuilder;
            }
        }

        extension(EndPointBuilder endPointBuilder)
        {
            /// <summary>
            /// Deserializes JSON request bodies into an endpoint parameter using source-generated or custom JSON metadata.
            /// </summary>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="endPointBuilder"/> instance.</returns>
            /// <remarks>
            /// The JSON-binding middleware is registered for the endpoint's captured HTTP methods and route match
            /// kind. The deserialized body is written into <see cref="RequestContext.Parameters"/>, and an existing
            /// value with the same key is overwritten.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endPointBuilder"/>, <paramref name="typeInfo"/>, or <paramref name="paramName"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public EndPointBuilder WithJsonBody(JsonTypeInfo typeInfo, string paramName)
            {
                Ensure.NotNull(endPointBuilder);

                return endPointBuilder.WithHandler
                (
                    CreateHandler(typeInfo, paramName)
                );
            }

            /// <summary>
            /// Deserializes JSON request bodies into an endpoint parameter using runtime type metadata.
            /// </summary>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="endPointBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endPointBuilder"/>, <paramref name="type"/>, or <paramref name="paramName"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public EndPointBuilder WithJsonBody(Type type, string paramName)
            {
                Ensure.NotNull(type);

                return endPointBuilder.WithJsonBody
                (
                    JsonSerializerOptions.Web.GetTypeInfo(type),
                    paramName
                );
            }

            /// <summary>
            /// Deserializes JSON request bodies into an endpoint parameter using runtime type metadata.
            /// </summary>
            /// <typeparam name="T">The CLR type expected in the request body.</typeparam>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="endPointBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endPointBuilder"/> or <paramref name="paramName"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported.</exception>
            /// <exception cref="HttpRequestException">Thrown during request processing when the body is missing, the content type is not JSON, or the JSON payload is invalid.</exception>
            /// <exception cref="OperationCanceledException">Thrown during request processing when the request cancellation token is canceled.</exception>
            public EndPointBuilder WithJsonBody<T>(string paramName) => endPointBuilder.WithJsonBody(typeof(T), paramName);
        }

        extension(HttpResponseMessage)
        {
            /// <summary>
            /// Creates a JSON response using the supplied type metadata.
            /// </summary>
            /// <param name="statusCode">The HTTP status code to assign to the response.</param>
            /// <param name="body">The value to serialize.</param>
            /// <param name="typeInfo">The metadata used to serialize <paramref name="body"/>.</param>
            /// <returns>A new <see cref="HttpResponseMessage"/> with JSON content.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is <see langword="null"/>.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the supplied JSON metadata is not compatible with <paramref name="body"/>.</exception>
            /// <exception cref="NotSupportedException">Thrown when the value cannot be serialized with the supplied JSON metadata.</exception>
            public static HttpResponseMessage Json(HttpStatusCode statusCode, object? body, JsonTypeInfo typeInfo)
            {
                Ensure.NotNull(typeInfo);

                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent
                    (
                        JsonSerializer.Serialize(body, typeInfo),
                        Encoding.UTF8,
                        JSON_MEDIA_TYPE
                    )
                };
            }

            /// <summary>
            /// Creates a JSON response using the supplied type metadata.
            /// </summary>
            /// <param name="statusCode">The HTTP status code to assign to the response.</param>
            /// <param name="body">The value to serialize.</param>
            /// <param name="typeInfo">The metadata used to serialize <paramref name="body"/>.</param>
            /// <returns>A new <see cref="HttpResponseMessage"/> with JSON content.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is <see langword="null"/>.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the supplied JSON metadata is not compatible with <paramref name="body"/>.</exception>
            /// <exception cref="NotSupportedException">Thrown when the value cannot be serialized with the supplied JSON metadata.</exception>
            public static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T? body, JsonTypeInfo<T> typeInfo)
            {
                Ensure.NotNull(typeInfo);

                return Json(statusCode, (object?) body, typeInfo);
            }

            /// <summary>
            /// Creates a JSON response using serializer <paramref name="options"/> to resolve metadata for <typeparamref name="T"/>.
            /// </summary>
            /// <typeparam name="T">The type of the response body.</typeparam>
            /// <param name="statusCode">The HTTP status code to assign to the response.</param>
            /// <param name="body">The value to serialize.</param>
            /// <param name="options">The serializer options used to resolve metadata and serialization behavior.</param>
            /// <returns>A new <see cref="HttpResponseMessage"/> with JSON content.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
            /// <exception cref="InvalidOperationException">Thrown when JSON metadata cannot be resolved or is not compatible with <paramref name="body"/>.</exception>
            /// <exception cref="NotSupportedException">Thrown when the value cannot be serialized with the resolved JSON metadata.</exception>
            public static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T? body, JsonSerializerOptions options)
            {
                Ensure.NotNull(options);

                if (options.TypeInfoResolver is null)
                    // do not change the original options
                    options = new JsonSerializerOptions(options)
                    {
                        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                    };

                return Json(statusCode, body, options.GetTypeInfo(typeof(T)));
            }

            /// <summary>
            /// Creates a JSON response using <see cref="JsonSerializerOptions.Web"/>.
            /// </summary>
            /// <typeparam name="T">The type of the response body.</typeparam>
            /// <param name="statusCode">The HTTP status code to assign to the response.</param>
            /// <param name="body">The value to serialize.</param>
            /// <returns>A new <see cref="HttpResponseMessage"/> with JSON content.</returns>
            public static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T? body) => Json(statusCode, body, JsonSerializerOptions.Web);

            /// <summary>
            /// Creates a JSON response with <see cref="HttpStatusCode.OK"/>. This method uses <see cref="JsonSerializerOptions.Web"/> when creating the response.
            /// </summary>
            /// <typeparam name="T">The type of the response body.</typeparam>
            /// <param name="body">The value to serialize.</param>
            /// <returns>A new <see cref="HttpResponseMessage"/> with JSON content.</returns>
            public static HttpResponseMessage Json<T>(T? body) => Json(HttpStatusCode.OK, body);
        }

        extension(ErrorDetails)
        {
            /// <summary>
            /// Provides the JSON serialization meta-data.
            /// </summary>
            public static JsonTypeInfo<ErrorDetails> JsonTypeInfo => JsonContext.Default.ErrorDetails;
        }
    }
}
