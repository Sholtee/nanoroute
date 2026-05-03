/********************************************************************************
* DtoMappingExtensions.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;

namespace NanoRoute.AwsLambda
{
    using Properties;

    /// <summary>
    /// https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html
    /// </summary>
    internal static class DtoMappingExtensions
    {
        private static readonly Regex s_protoMatcher = new(@"(?:^|;\s*)proto=(?:""(?<proto>[^""]+)""|(?<proto>[^;]+))");

        public static Uri CreateUri(this APIGatewayHttpApiV2ProxyRequest request)
        {
            if 
            (
                HostAndPort(request.Headers) is not { Length: > 0 } hostAndPort ||
                Scheme(request.Headers) is not { Length: > 0 } scheme ||
                // Parse the base URI as a URI so host:port and IPv6 literals are handled correctly
                !Uri.TryCreate($"{scheme}://{hostAndPort}", UriKind.Absolute, out Uri baseUri)
            )
                throw new InvalidOperationException(Resources.ERR_UNKNOWN_URI);

            UriBuilder builder = new(baseUri)
            {
                Path = request.RawPath is { Length: > 0 } path ? path : "/",
                Query = request.RawQueryString is { Length: > 0 } query ? query : null
            };

            return builder.Uri;

            static string? HostAndPort(IDictionary<string, string> headers)
            {
                if (headers.TryGetValue("host" /*AWS lowercases the header names*/, out string hostAndPort))
                    return hostAndPort;

                return null;
            }

            static string? Scheme(IDictionary<string, string> headers)
            {
                if (headers.TryGetValue("forwarded", out string forwarded) && s_protoMatcher.Match(forwarded) is { Success: true } match)
                    return match.Groups["proto"].Value;

                if (headers.TryGetValue("x-forwarded-proto", out string proto))
                    return proto;

                return null;
            }
        }

        public static HttpRequestMessage CreateRequestMessage(this APIGatewayHttpApiV2ProxyRequest request)
        {
            HttpRequestMessage requestMessage = new(new HttpMethod(request.RequestContext.Http.Method), request.CreateUri().AbsoluteUri)
            {
                Content = request switch
                {
                    { IsBase64Encoded: false } and { Body.Length: > 0 } => new StringContent(request.Body),
                    { IsBase64Encoded: true }  and { Body.Length: > 0 } => new StreamContent
                    (
                        new MemoryStream
                        (
                            Convert.FromBase64String(request.Body)
                        )
                    ),
                    _ => null
                }
            };

            foreach (KeyValuePair<string, string> header in request.Headers)
                _ = 
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value) ||
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value) is true;

            requestMessage.Properties[Router.ORIGINAL_REQUEST_NAME] = request;
            requestMessage.Properties[Router.TRACE_ID_NAME] = request.RequestContext.RequestId;

            return requestMessage;
        }
 
        public static async Task<APIGatewayHttpApiV2ProxyResponse> CreateResponse(this HttpResponseMessage responseMessage)
        {
            responseMessage.GetFlattenedHeaders(out Dictionary<string, string> headers, out List<string> cookies);

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = (int) responseMessage.StatusCode,
                Body = responseMessage.Content switch
                {
                    null => null,
                    StringContent stringContent => await stringContent.ReadAsStringAsync(),
                    _ => Convert.ToBase64String(await responseMessage.Content.ReadAsByteArrayAsync())
                },
                IsBase64Encoded = responseMessage.Content is (not null) and (not StringContent),
                Headers = headers,
                Cookies = cookies.ToArray()
            };
        }

        public static void GetFlattenedHeaders(this HttpResponseMessage responseMessage, out Dictionary<string, string> headers, out List<string> cookies)
        {
            headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            cookies = new List<string>();

            Copy(responseMessage.Headers, headers, cookies);

            if (responseMessage.Content is not null)
                Copy(responseMessage.Content.Headers, headers, cookies);

            static void Copy(IEnumerable<KeyValuePair<string, IEnumerable<string>>> source, Dictionary<string, string> headers, List<string> cookies)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in source)
                {
                    if (string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                    {
                        cookies.AddRange(header.Value);
                        continue;
                    }

                    headers[header.Key] = string.Join(",", header.Value);
                }
            }
        }
    }
}
