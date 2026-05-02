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

        public static HttpRequestMessage CreateRequestMessage(this APIGatewayHttpApiV2ProxyRequest request)
        {
            UriBuilder uriBuilder = new()
            {
                // https://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-known-issues.html#api-gateway-known-issues-http-apis
                Scheme = request.Headers.TryGetValue("forwarded" /*AWS lowercases the header names*/, out string forwarded) && s_protoMatcher.Match(forwarded) is { Success: true } match
                    ? match.Groups["proto"].Value
                    : throw new InvalidOperationException(Resources.ERR_UNKNOWN_SCHEME),
                Host = request.Headers.TryGetValue("host", out string host) ? host : throw new InvalidOperationException(Resources.ERR_UNKNOWN_HOST),
                Path = request.RawPath,
                Query = request.RawQueryString
            };

            HttpRequestMessage requestMessage = new(new HttpMethod(request.RequestContext.Http.Method), uriBuilder.Uri.AbsoluteUri)
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
