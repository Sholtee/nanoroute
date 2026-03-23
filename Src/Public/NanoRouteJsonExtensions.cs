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
using System.Threading.Tasks;

namespace NanoRoute.Json
{
    using Internals;
    using Properties;

    [JsonSerializable(typeof(ErrorDetails))]
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    internal partial class JsonContext : JsonSerializerContext  // cannot be nested =(
    {
    }

    /// <summary>
    /// TODO
    /// </summary>
    public static class NanoRouteJsonExtensions
    {
        extension<TRouter>(RouterBuilder<TRouter> routerBuilder) where TRouter: Router, new()
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="typeInfo"></param>
            /// <param name="paramName"></param>
            /// <param name="verbs"></param>
            /// <returns></returns>
            public RouterBuilder<TRouter> AddJsonBody(JsonTypeInfo typeInfo, string paramName, params IEnumerable<string> verbs)
            {
                Ensure.NotNull(routerBuilder);
                Ensure.NotNull(typeInfo);
                Ensure.NotNull(paramName);
                Ensure.NotNull(verbs);

                return routerBuilder.AddHandler(verbs: verbs, pattern: "/", async (RequestContext context, Func<Task<HttpResponseMessage>> next) =>
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
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public RouterBuilder<TRouter> AddJsonBody(Type type, string paramName, params IEnumerable<string> verbs)
            {
                Ensure.NotNull(type);

                return routerBuilder.AddJsonBody
                (
                    JsonSerializerOptions.Web.GetTypeInfo(type),
                    paramName,
                    verbs
                );
            }

            /// <summary>
            /// Registers a catch-all handler that turns unhandled routing failures into JSON error responses.
            /// </summary>
            /// <param name="populateErrorInfo">
            /// <see langword="true"/> to include exception details in generated internal-server-error responses;
            /// otherwise only the public error message is returned.
            /// </param>
            /// <remarks>
            /// The default handler is registered as a prefix route for all supported HTTP methods. It calls the next
            /// matching handler and intercepts the terminal <c>not found</c> case as well as unhandled exceptions.
            /// </remarks>
            /// <example>
            /// <code>
            /// TODO
            /// </code>
            /// In this example, requests without a matching route receive the built-in JSON <c>404 Not Found</c>
            /// response instead of an unhandled exception.
            /// </example>
            public RouterBuilder<TRouter> AddDefaultHandler(bool populateErrorInfo = false)
            {
                Ensure.NotNull(routerBuilder);

                return routerBuilder.AddHandler("/", async (RequestContext context, Func<Task<HttpResponseMessage>> next) =>
                {
                    try
                    {
                        context.Cancellation.ThrowIfCancellationRequested();

                        return await next();
                    }
                    catch (HttpRequestException ex) when (ex.Data[HttpRequestExceptionExtensions.STATUS_NAME] is HttpStatusCode status)
                    {
                        return CreateErrorResponse(status, ex.Message, errors: ex.Data[HttpRequestExceptionExtensions.ERRORS_NAME] as IEnumerable<string>);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
                    {
                        return CreateErrorResponse(HttpStatusCode.RequestTimeout, Resources.ERR_REQUEST_TIMED_OUT);
                    }
                    catch (Exception ex)
                    {
                        List<string>? developerMessage = null;

                        if (populateErrorInfo)
                        {
                            developerMessage = [];

                            if (ex is AggregateException aggregateException)
                                foreach (Exception innerException in aggregateException.InnerExceptions)
                                    developerMessage.Add(innerException.ToString());
                            else
                                developerMessage.Add(ex.ToString());
                        }

                        return CreateErrorResponse(HttpStatusCode.InternalServerError, Resources.ERR_INERNAL_ERROR, developerMessage: developerMessage);
                    }

                    HttpResponseMessage CreateErrorResponse(HttpStatusCode status, string title, IEnumerable<string>? errors = null, IEnumerable<string>? developerMessage = null) => HttpResponseMessage.Json
                    (
                        status,
                        new ErrorDetails
                        {
                            Status = (int) status,
                            Title = title,
                            TraceId = (string) context.Request.Properties[Router.TRACE_ID_NAME],
                            Errors = errors,
                            DeveloperMessage = developerMessage
                        },
                        JsonContext.Default.ErrorDetails
                    );
                });
            }
        }

        extension(HttpResponseMessage)
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="statusCode"></param>
            /// <param name="body"></param>
            /// <param name="typeInfo"></param>
            /// <returns></returns>
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
            /// 
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="statusCode"></param>
            /// <param name="body"></param>
            /// <param name="options"></param>
            /// <returns></returns>
            public static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T? body, JsonSerializerOptions options)
            {
                Ensure.NotNull(options);

                options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

                return Json(statusCode, body, options.GetTypeInfo(typeof(T)));
            }

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="statusCode"></param>
            /// <param name="body"></param>
            /// <returns></returns>
            public static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T? body) => Json(statusCode, body, JsonSerializerOptions.Web);

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="body"></param>
            /// <returns></returns>
            public static HttpResponseMessage Json<T>(T? body) => Json(HttpStatusCode.OK, body);
        }
    }
}
