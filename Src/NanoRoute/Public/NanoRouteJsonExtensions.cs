/********************************************************************************
* RouterBuilderJsonExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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
    /// Adds JSON-focused helpers for request body binding, structured error responses, and JSON responses.
    /// </summary>
    /// <remarks>
    /// These helpers are optional conveniences on top of the core routing pipeline. They are implemented as
    /// extension methods on <see cref="RouteBuilder"/> and <see cref="HttpResponseMessage"/>.
    /// </remarks>
    public static class NanoRouteJsonExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder: RouteBuilder
        {
            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for the selected HTTP methods.
            /// </summary>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <param name="pattern">
            /// The route pattern where the JSON-binding middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope body binding to selected routes.
            /// </param>
            /// <param name="verbs">
            /// The HTTP methods that should require a JSON body. When omitted, <c>POST</c> and <c>PUT</c> are used.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The helper inserts a handler at the supplied <paramref name="pattern"/>. Requests without content,
            /// requests with a non-JSON content type, and requests with invalid JSON do not produce responses on
            /// their own. Instead, this middleware throws <see cref="HttpRequestException"/> with the appropriate
            /// routing metadata attached.
            /// Add <see cref="AddJsonErrorDetails"/> if you want those exceptions to be translated into structured
            /// HTTP error responses; otherwise they continue as regular exceptions and must be handled by the caller
            /// or by custom middleware. The deserialized body is written into
            /// <see cref="RequestContext.Parameters"/>, and an existing value with the same key is overwritten.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddJsonBody(MyJsonContext.Default.CreateUserRequest, "body", "/users/", "POST")
            ///     .AddHandler("POST", "/users", (context, _) =&gt;
            ///     {
            ///         CreateUserRequest body = (CreateUserRequest) context.Parameters["body"]!;
            ///         return Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.Created, body));
            ///     });
            /// </code>
            /// </example>
            public TBuilder AddJsonBody(JsonTypeInfo typeInfo, string paramName, string pattern, params IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(typeInfo);
                Ensure.NotNull(paramName);
                Ensure.NotNull(verbs);

                if (verbs.Count is 0)
                    verbs = ["POST", "PUT"];

                routeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                {
                    context.Cancellation.ThrowIfCancellationRequested();

                    if (context.Request.Content is not { } content)
                    {
                        HttpRequestException.Throw(HttpStatusCode.MethodNotAllowed, Resources.ERR_METHOD_NOT_ALLOWED);
                        return null!;
                    }

                    if (!"application/json".Equals(content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase))
                        HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, Resources.ERR_BAD_CONTENT_TYPE);

                    Stream contentStream = await content.ReadAsStreamAsync();

                    object? body = null;

                    try
                    {
                        body = await JsonSerializer.DeserializeAsync(contentStream, typeInfo, context.Cancellation);
                    }
                    catch (JsonException ex)
                    {
                        HttpRequestException.Throw(HttpStatusCode.BadRequest, Resources.ERR_BAD_REQUEST, ex.Message);
                    }

                    context.Parameters[paramName] = body;

                    return await next();
                });

                return routeBuilder;
            }

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter for the selected HTTP methods.
            /// </summary>
            /// <param name="typeInfo">The metadata used to deserialize the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <param name="verbs">
            /// The HTTP methods that should require a JSON body. When omitted, <c>POST</c> and <c>PUT</c> are used.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload inserts the JSON-binding middleware at <c>/</c>, so it can participate in any matching
            /// route for the selected HTTP methods. Use the overload that also accepts a <c>pattern</c> argument if
            /// you want to scope binding to a specific prefix or route. Requests without content, requests with a
            /// non-JSON content type, and requests with invalid JSON throw <see cref="HttpRequestException"/>
            /// instead of producing responses on their own. Add <see cref="AddJsonErrorDetails"/> if you want those
            /// exceptions to be translated into structured HTTP error responses; otherwise they continue as regular
            /// exceptions and must be handled by the caller or by custom middleware. The deserialized body is written
            /// into <see cref="RequestContext.Parameters"/>, and an existing value with the same key is overwritten.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddJsonBody(MyJsonContext.Default.CreateUserRequest, "body", "POST")
            ///     .AddHandler("POST", "/users", (context, _) =&gt;
            ///     {
            ///         CreateUserRequest body = (CreateUserRequest) context.Parameters["body"]!;
            ///         return Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.Created, body));
            ///     });
            /// </code>
            /// </example>
            public TBuilder AddJsonBody(JsonTypeInfo typeInfo, string paramName, params IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(typeInfo);
                Ensure.NotNull(paramName);
                Ensure.NotNull(verbs);

                return routeBuilder.AddJsonBody(typeInfo, paramName, "/", verbs);
            }

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using runtime type metadata.
            /// </summary>
            /// <param name="type">The CLR type expected in the request body.</param>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <param name="verbs">
            /// The HTTP methods that should require a JSON body. When omitted, <c>POST</c> and <c>PUT</c> are used.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload inserts the JSON-binding middleware at <c>/</c>, so it can participate in any matching
            /// route for the selected HTTP methods. Requests without content, requests with a
            /// non-JSON content type, and requests with invalid JSON throw <see cref="HttpRequestException"/>
            /// instead of producing responses on their own. Add <see cref="AddJsonErrorDetails"/> if you want those
            /// exceptions to be translated into structured HTTP error responses; otherwise they continue as regular
            /// exceptions and must be handled by the caller or by custom middleware. The deserialized body is written
            /// into <see cref="RequestContext.Parameters"/>, and an existing value with the same key is overwritten.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddJsonBody(typeof(CreateUserRequest), "body", "POST")
            ///     .AddHandler("POST", "/users", (context, _) =&gt;
            ///     {
            ///         CreateUserRequest body = (CreateUserRequest) context.Parameters["body"]!;
            ///         return Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.Created, body));
            ///     });
            /// </code>
            /// </example>
            public TBuilder AddJsonBody(Type type, string paramName, params IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(type);
                Ensure.NotNull(paramName);
                Ensure.NotNull(verbs);

                return routeBuilder.AddJsonBody
                (
                    JsonSerializerOptions.Web.GetTypeInfo(type),
                    paramName,
                    verbs
                );
            }

            /// <summary>
            /// Deserializes JSON request bodies into a route parameter using <typeparamref name="TBody"/>.
            /// </summary>
            /// <param name="paramName">The parameter name under which the deserialized body will be stored.</param>
            /// <param name="verbs">
            /// The HTTP methods that should require a JSON body. When omitted, <c>POST</c> and <c>PUT</c> are used.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This overload inserts the JSON-binding middleware at <c>/</c>, so it can participate in any matching
            /// route for the selected HTTP methods. Requests without content, requests with a
            /// non-JSON content type, and requests with invalid JSON throw <see cref="HttpRequestException"/>
            /// instead of producing responses on their own. Add <see cref="AddJsonErrorDetails"/> if you want those
            /// exceptions to be translated into structured HTTP error responses; otherwise they continue as regular
            /// exceptions and must be handled by the caller or by custom middleware. The deserialized body is written
            /// into <see cref="RequestContext.Parameters"/>, and an existing value with the same key is overwritten.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddJsonBody&lt;CreateUserRequest&gt;("body")
            ///     .AddHandler("POST", "/users", (context, _) =&gt;
            ///     {
            ///         CreateUserRequest body = (CreateUserRequest) context.Parameters["body"]!;
            ///         return Task.FromResult(HttpResponseMessage.Json(HttpStatusCode.Created, body));
            ///     });
            /// </code>
            /// </example>
            public TBuilder AddJsonBody<TBody>(string paramName, params IReadOnlyCollection<string> verbs) => routeBuilder.AddJsonBody(typeof(TBody), paramName, verbs);

            /// <summary>
            /// Adds middleware that converts router exceptions into JSON <see cref="ErrorDetails"/> responses.
            /// </summary>
            /// <param name="populateErrorInfo">
            /// <see langword="true"/> to include developer-facing diagnostic details when they are attached to the
            /// underlying exception; otherwise <see langword="false"/>.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This helper wraps <see cref="HttpRequestException"/> values into JSON responses and also installs
            /// <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/> so unexpected
            /// exceptions are normalized before they reach the client. <see cref="OperationCanceledException"/> is
            /// not translated into JSON and continues to propagate to the caller unchanged.
            /// </remarks>
            /// <example>
            /// <code>
            /// routerBuilder
            ///     .AddJsonErrorDetails()
            ///     .AddHandler("GET", "/items/{id:int}", (context, _) =&gt;
            ///         throw new InvalidOperationException("Unexpected state"));
            /// </code>
            /// </example>
            public TBuilder AddJsonErrorDetails(bool populateErrorInfo = false)
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder
                    .AddHandler("/", async (RequestContext context, CallNextHandlerDelegate next) =>
                    {
                        try
                        {
                            return await next();
                        }
                        catch (HttpRequestException ex)
                        {
                            ErrorDetails errorDetails = ex.GetErrorDetails(populateErrorInfo, context.Request.Properties[Router.TRACE_ID_NAME] as string);

                            return HttpResponseMessage.Json
                            (
                                errorDetails.Status,
                                errorDetails,
                                JsonContext.Default.ErrorDetails
                            );
                        }
                    })
                    .AddExceptionHandler();

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
                        "application/json"
                    )
                };
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

                options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

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
    }
}
