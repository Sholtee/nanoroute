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
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = false)]
    internal partial class JsonContext : JsonSerializerContext  // cannot be nested =(
    {
    }

    /// <summary>
    /// TODO
    /// </summary>
    public static class NanoRouteJsonExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder: RouteBuilder
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="typeInfo"></param>
            /// <param name="paramName"></param>
            /// <param name="verbs"></param>
            /// <returns></returns>
            public TBuilder AddJsonBody(JsonTypeInfo typeInfo, string paramName, params IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(typeInfo);
                Ensure.NotNull(paramName);
                Ensure.NotNull(verbs);

                routeBuilder.AddHandler(verbs: verbs, pattern: "/", async (RequestContext context, Func<Task<HttpResponseMessage>> next) =>
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
            /// 
            /// </summary>
            /// <returns></returns>
            public TBuilder AddJsonBody(Type type, string paramName, params IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(type);

                return routeBuilder.AddJsonBody
                (
                    JsonSerializerOptions.Web.GetTypeInfo(type),
                    paramName,
                    verbs
                );
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public TBuilder AddJsonBody<TBody>(string paramName, params IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(verbs);

                if (verbs.Count is 0)
                    verbs = ["POST", "PUT"];

                return routeBuilder.AddJsonBody(typeof(TBody), paramName, verbs);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="populateErrorInfo"></param>
            /// <returns></returns>
            public TBuilder AddJsonErrorDetails(bool populateErrorInfo = false)
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder
                    .AddHandler("/", async (RequestContext context, Func<Task<HttpResponseMessage>> next) =>
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
