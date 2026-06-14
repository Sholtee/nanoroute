/********************************************************************************
* ApiGatewayV2Router.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace NanoRoute.AwsLambda
{
    using Properties;

    /// <summary>
    /// Routes API Gateway HTTP API and Lambda Function URL <see cref="APIGatewayHttpApiV2ProxyRequest"/> instances through a NanoRoute pipeline.
    /// </summary>
    /// <example>
    /// <code>
    /// ApiGatewayV2Router router = ApiGatewayV2Router
    ///     .CreateBuilder()
    ///     .AddJsonErrorDetails()
    ///     .AddDefaultValueParsers()
    ///     .AddHandler("GET", "/health/", (context, _) =&gt; HttpResponseMessage.Json(new { status = "ok" }))
    ///     .CreateRouter();
    /// </code>
    /// </example>
    public sealed class ApiGatewayV2Router : RouterBase<ApiGatewayV2RouterConfig>
    {
        #region Private
        internal HttpRequestMessage CreateRequestMessage(APIGatewayHttpApiV2ProxyRequest request)
        {
            if (!Uri.TryCreate($"{Config.RequestScheme}://{Config.RequestDomain ?? request.RequestContext.DomainName}", UriKind.Absolute, out Uri? baseUri))
                throw new InvalidOperationException(Resources.ERR_UNKNOWN_URI);

            UriBuilder uriBuilder = new(baseUri)
            {
                Path = request.RawPath is { Length: > 0 } path ? path : "/",
                Query = request.RawQueryString is { Length: > 0 } query ? query : null
            };

            HttpRequestMessage requestMessage = new
            (
                HttpMethod.For(request.RequestContext.Http.Method),
                uriBuilder.Uri
            )
            {
                Content = request switch
                {
                    { IsBase64Encoded: false } and { Body.Length: > 0 } => new StringContent(request.Body),
                    { IsBase64Encoded: true } and { Body.Length: > 0 } => new StreamContent
                    (
                        new Base64BodyReaderStream(request.Body)
                    ),
                    _ => null
                }
            };

            requestMessage.Content?.Headers.Clear();

            HttpHeaders
                requestHeaders = requestMessage.Headers,
                contentHeaders = requestMessage.Content?.Headers!;

            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                HttpHeaders headers = contentHeaders is not null && HttpRequestMessage.ContentHeaders.Contains(header.Key)
                    ? contentHeaders
                    : requestHeaders;

                headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            requestMessage.OriginalRequest = request;
            requestMessage.TraceId = request.RequestContext.RequestId;

            return requestMessage;
        }

        internal static async Task<APIGatewayHttpApiV2ProxyResponse> CreateResponse(HttpResponseMessage responseMessage)
        {
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            List<string> cookies = new();

            APIGatewayHttpApiV2ProxyResponse response = new()
            {
                StatusCode = (int) responseMessage.StatusCode
            };

            CopyHeaders(responseMessage.Headers, headers, cookies);

            if (responseMessage.Content is { } content)
            {
                CopyHeaders(content.Headers, headers, cookies);

                switch (content)
                {
                    case StringContent stringContent:
                    {
                        if (await stringContent.ReadAsStringAsync().ConfigureAwait(false) is { Length: > 0 } body)
                            response.Body = body;
                        break;
                    }
                    default:
                    {
                        using Base64BodyWriterStream destination = new();

                        await content.CopyToAsync(destination).ConfigureAwait(false);

                        if (destination.GetBody() is { Length: > 0 } body)
                        {
                            response.Body = body;
                            response.IsBase64Encoded = true;
                        }

                        break;
                    }
                }
            }

            response.Headers = headers;
            response.Cookies = cookies.ToArray();

            return response;

            static void CopyHeaders(HttpHeaders source, Dictionary<string, string> headers, List<string> cookies)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in source)
                {
                    if (string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                    {
                        cookies.AddRange(header.Value);
                        continue;
                    }

                    foreach (string value in header.Value)
                        headers[header.Key] = headers.TryGetValue(header.Key, out string? existing)
                            ? $"{existing},{value}"
                            : value;
                }
            }
        }

        private ApiGatewayV2Router(RouterBuilder<ApiGatewayV2Router, ApiGatewayV2RouterConfig> builder) : base(builder, builder.RouterConfig)
        {
        }
        #endregion

        /// <summary>
        /// Creates a strongly typed builder for <see cref="ApiGatewayV2Router"/>.
        /// </summary>
        /// <returns>A builder that can register handlers, value parsers, and router configuration.</returns>
        /// <example>
        /// <code>
        /// RouterBuilder&lt;ApiGatewayV2Router, ApiGatewayV2RouterConfig&gt; builder = ApiGatewayV2Router.CreateBuilder();
        /// </code>
        /// </example>
        public static RouterBuilder<ApiGatewayV2Router, ApiGatewayV2RouterConfig> CreateBuilder() => new(static builder => new ApiGatewayV2Router(builder));

        /// <summary>
        /// Routes an API Gateway HTTP API or Lambda Function URL payload-format-2.0 request and returns the corresponding proxy response.
        /// </summary>
        /// <param name="request">The API Gateway v2 request event.</param>
        /// <param name="services">The service provider exposed to handlers through <see cref="RequestContext.Services"/>.</param>
        /// <param name="context">
        /// The Lambda invocation context used to derive the cancellation deadline from <see cref="ILambdaContext.RemainingTime"/>.
        /// </param>
        /// <returns>The API Gateway v2 proxy response generated by NanoRoute.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/>, <paramref name="services"/>, or <paramref name="context"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the API Gateway request does not contain a recognizable URI.</exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when no handler matches the request path or a matched handler signals an HTTP failure that is not
        /// translated by middleware.
        /// </exception>
        /// <example>
        /// <code>
        /// APIGatewayHttpApiV2ProxyResponse response = await router.Route(request, services, context);
        /// </code>
        /// </example>
        public async Task<APIGatewayHttpApiV2ProxyResponse> Route(APIGatewayHttpApiV2ProxyRequest request, IServiceProvider services, ILambdaContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(context);

            TimeSpan cancellationDelay = context.RemainingTime - Config.LambdaTimeoutBuffer;
            if (cancellationDelay <= TimeSpan.Zero)
                return Timeout();

            using CancellationTokenSource cts = new();
            cts.CancelAfter(cancellationDelay);

            using HttpRequestMessage requestMessage = CreateRequestMessage(request);

            try
            {
                using HttpResponseMessage responseMessage = await Route(requestMessage, services, cts.Token).ConfigureAwait(false);

                return await CreateResponse(responseMessage).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Timeout();
            }

            APIGatewayHttpApiV2ProxyResponse Timeout() => new()
            {
                StatusCode = (int) HttpStatusCode.GatewayTimeout,
                Body = JsonSerializer.Serialize
                (
                    new ErrorDetails
                    {
                        Status = HttpStatusCode.GatewayTimeout,
                        Title = "Gateway Timeout",
                        TraceId = request.RequestContext.RequestId
                    },
                    ErrorDetails.JsonTypeInfo
                ),
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                }
            };
        }
    }
}
