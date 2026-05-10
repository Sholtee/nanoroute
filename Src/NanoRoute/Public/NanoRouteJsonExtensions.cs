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
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NanoRoute.Json
{
    using Internals;
    using Properties;

    [JsonSerializable(typeof(ErrorDetails))]
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = false)]
    internal partial class JsonContext : JsonSerializerContext  // cannot be nested =(
    {
    }

    /// <summary>
    /// Configures how <see cref="NanoRouteJsonExtensions.AddJsonErrorDetails{TBuilder}(TBuilder)"/> creates JSON
    /// <see cref="ErrorDetails"/> responses.
    /// </summary>
    /// <remarks>
    /// Instances are stored in <see cref="RouteBuilder.Metadata"/> by
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
    /// extension methods on <see cref="RouteBuilder"/> and <see cref="HttpResponseMessage"/>.
    /// </remarks>
    public static class NanoRouteJsonExtensions
    {
        private const string JSON_MEDIA_TYPE =
#if NETSTANDARD2_1_OR_GREATER
            MediaTypeNames.Application.Json;
#else
            "application/json";
#endif

        extension<TBuilder>(TBuilder routeBuilder) where TBuilder: RouteBuilder
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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
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
            ///     .AddHandler("POST", "/users", (context, _) =&gt;
            ///     {
            ///         CreateUserRequest body = (CreateUserRequest) context.Parameters["body"]!;
            ///         return Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.Created, body));
            ///     });
            /// </code>
            /// </example>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, string pattern, JsonTypeInfo typeInfo, string paramName)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);
                Ensure.NotNull(typeInfo);
                Ensure.NotNull(paramName);

                routeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
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
                });

                return routeBuilder;

                [DoesNotReturn]
                static void BadRequest(string error) => HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, error);
            }

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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(string verb, string pattern, JsonTypeInfo typeInfo, string paramName) => routeBuilder.AddJsonBody([verb /*will be null checked*/], pattern, typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should require a JSON body.</param>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, JsonTypeInfo typeInfo, string paramName) => routeBuilder.AddJsonBody(verbs, "/", typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for <c>POST</c> and <c>PUT</c>.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(string pattern, JsonTypeInfo typeInfo, string paramName) => routeBuilder.AddJsonBody(HttpVerb.HavingBody, pattern, typeInfo, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for <c>POST</c> and <c>PUT</c>.
            /// </summary>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(JsonTypeInfo typeInfo, string paramName) => routeBuilder.AddJsonBody(HttpVerb.HavingBody, "/", typeInfo, paramName);

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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, string pattern, Type type, string paramName)
            {
                Ensure.NotNull(type);

                return routeBuilder.AddJsonBody
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
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(string verb, string pattern, Type type, string paramName) => routeBuilder.AddJsonBody([verb /*will be null checked*/], pattern, type, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should require a JSON body.</param>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(IEnumerable<string> verbs, Type type, string paramName) => routeBuilder.AddJsonBody(verbs, "/", type, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata for <c>POST</c> and <c>PUT</c>.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(string pattern, Type type, string paramName) => routeBuilder.AddJsonBody(HttpVerb.HavingBody, pattern, type, paramName);

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata for <c>POST</c> and <c>PUT</c>.
            /// </summary>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonBody(Type type, string paramName) => routeBuilder.AddJsonBody(HttpVerb.HavingBody, "/", type, paramName);

            /// <summary>
            /// Updates the JSON error-detail configuration visible from the current builder scope.
            /// </summary>
            /// <param name="configure">
            /// A callback that receives the current configuration and returns the replacement configuration.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The configuration is stored in <see cref="RouteBuilder.Metadata"/>. Child builders created after this
            /// method is called inherit the updated configuration; existing child builders keep their own scoped copy.
            /// Registered JSON error-detail middleware snapshots the configuration that is current at registration time.
            /// </remarks>
            public TBuilder ConfigureJsonErrorDetails(ConfigureBuilderDelegate<JsonErrorDetailsConfig> configure)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(configure);

                JsonErrorDetailsConfig config = configure(routeBuilder.Metadata.GetOrDefault(JsonErrorDetailsConfig.Default));
                Ensure.NotNull(config);

                routeBuilder.Metadata.Set(config);

                return routeBuilder;
            }

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for all supported HTTP methods.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonErrorDetails() => routeBuilder.AddJsonErrorDetails("/");

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for all supported HTTP methods.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the error-detail middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope JSON error responses to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonErrorDetails(string pattern) => routeBuilder.AddJsonErrorDetails(HttpVerb.Names, pattern);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for a single HTTP method.
            /// </summary>
            /// <param name="verb">The HTTP method that should use the error-detail middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the error-detail middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope JSON error responses to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonErrorDetails(string verb, string pattern) => routeBuilder.AddJsonErrorDetails([verb /*will be null checked*/], pattern);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the error-detail middleware.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddJsonErrorDetails(IEnumerable<string> verbs) => routeBuilder.AddJsonErrorDetails(verbs, "/");

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the error-detail middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the error-detail middleware should be inserted. Use <c>/</c> to apply it to
            /// the whole pipeline, or a narrower prefix/exact pattern to scope JSON error responses to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
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
            ///     .AddHandler("GET", "/items/{id:int}", (context, _) =&gt;
            ///         throw new InvalidOperationException("Unexpected state"));
            /// </code>
            /// </example>
            public TBuilder AddJsonErrorDetails(IEnumerable<string> verbs, string pattern)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                JsonErrorDetailsConfig config = routeBuilder.Metadata.GetOrDefault(JsonErrorDetailsConfig.Default);

                routeBuilder
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

                return routeBuilder;
            }
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
