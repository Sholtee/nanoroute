/********************************************************************************
* DtoMappingExtensionsTests.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;

using NUnit.Framework;

namespace NanoRoute.AwsLambda.Tests
{
    using AwsLambda;

    [TestFixture]
    internal sealed class DtoMappingExtensionsTests
    {
        [TestCase("for=192.0.2.60;proto=http;host=example.com")]
        [TestCase("for=192.0.2.60; proto=\"http\"; host=example.com")]
        public void CreateUri_ShouldUseProtoFromForwardedHeader(string forwarded)
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https",
                    ["forwarded"] = forwarded
                },
                RawPath = "/items/42",
                RawQueryString = "filter=active"
            };

            Assert.That(request.CreateUri(), Is.EqualTo(new Uri("http://example.com/items/42?filter=active")));
        }

        [Test]
        public void CreateUri_ShouldUseXForwardedProtoHeader()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "http"
                },
                RawPath = "/items/42",
                RawQueryString = "filter=active"
            };

            Assert.That(request.CreateUri(), Is.EqualTo(new Uri("http://example.com/items/42?filter=active")));
        }

        [Test]
        public void CreateUri_ShouldPreserveHostPort()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com:8443",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                RawQueryString = "filter=active"
            };

            Assert.That(request.CreateUri(), Is.EqualTo(new Uri("https://example.com:8443/items/42?filter=active")));
        }

        [Test]
        public void CreateUri_ShouldHandleIpv6Host()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "[::1]",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                RawQueryString = "filter=active"
            };

            Assert.That(request.CreateUri(), Is.EqualTo(new Uri("https://[::1]/items/42?filter=active")));
        }

        [Test]
        public void CreateUri_ShouldUseSlashWhenRawPathIsEmpty()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "",
                RawQueryString = "filter=active"
            };

            Assert.That(request.CreateUri(), Is.EqualTo(new Uri("https://example.com/?filter=active")));
        }

        [Test]
        public void CreateUri_ShouldOmitQueryWhenRawQueryStringIsEmpty()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                RawQueryString = ""
            };

            Assert.That(request.CreateUri(), Is.EqualTo(new Uri("https://example.com/items/42")));
        }

        [TestCase(null, "https")]
        [TestCase("", "https")]
        [TestCase("example.com", null)]
        [TestCase("example.com", "")]
        [TestCase("not a host", "https")]
        public void CreateUri_ShouldThrowWhenUriCannotBeDetermined(string? host, string? scheme)
        {
            Dictionary<string, string> headers = [];

            if (host is not null)
                headers["host"] = host;

            if (scheme is not null)
                headers["x-forwarded-proto"] = scheme;

            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = headers,
                RawPath = "/items/42",
                RawQueryString = "filter=active"
            };

            Assert.Throws<InvalidOperationException>(() => request.CreateUri());
        }

        [Test]
        public void CreateRequestMessage_ShouldMapMethodUriProperties()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                RawQueryString = "filter=active",
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    RequestId = "request-id",
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "POST"
                    }
                }
            };

            using HttpRequestMessage requestMessage = request.CreateRequestMessage();

            Assert.That(requestMessage.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(requestMessage.RequestUri, Is.EqualTo(new Uri("https://example.com/items/42?filter=active")));
            Assert.That(GetProperty(requestMessage, Router.ORIGINAL_REQUEST_NAME), Is.SameAs(request));
            Assert.That(GetProperty(requestMessage, Router.TRACE_ID_NAME), Is.EqualTo("request-id"));
        }

        [Test]
        public void CreateRequestMessage_ShouldMapRequestHeaders()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https",
                    ["accept"] = "application/json"
                },
                RawPath = "/items/42",
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "GET"
                    }
                }
            };

            using HttpRequestMessage requestMessage = request.CreateRequestMessage();

            Assert.That(requestMessage.Headers.GetValues("accept"), Is.EqualTo(new[] { "application/json" }));
        }

        [Test]
        public void CreateRequestMessage_ShouldMapContentHeaders()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https",
                    ["content-md5"] = "digest"
                },
                RawPath = "/items/42",
                Body = "hello",
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "GET"
                    }
                }
            };

            using HttpRequestMessage requestMessage = request.CreateRequestMessage();

            Assert.That(requestMessage.Content!.Headers.GetValues("content-md5"), Is.EqualTo(new[] { "digest" }));
        }

        [Test]
        public void CreateRequestMessage_ShouldMapTextBody()
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                Body = "hello",
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "GET"
                    }
                }
            };

            using HttpRequestMessage requestMessage = request.CreateRequestMessage();

            Assert.That(requestMessage.Content, Is.TypeOf<StringContent>());
            Assert.That(requestMessage.Content!.ReadAsStringAsync().GetAwaiter().GetResult(), Is.EqualTo("hello"));
        }

        [Test]
        public void CreateRequestMessage_ShouldDecodeBase64Body()
        {
            byte[] bytes = { 0, 1, 2, 253, 254, 255 };
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                Body = Convert.ToBase64String(bytes),
                IsBase64Encoded = true,
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "GET"
                    }
                }
            };

            using HttpRequestMessage requestMessage = request.CreateRequestMessage();

            Assert.That(requestMessage.Content, Is.TypeOf<StreamContent>());
            Assert.That(requestMessage.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult(), Is.EqualTo(bytes));
        }

        [TestCase(null)]
        [TestCase("")]
        public void CreateRequestMessage_ShouldLeaveContentNullWhenBodyIsMissingOrEmpty(string? body)
        {
            APIGatewayHttpApiV2ProxyRequest request = new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["host"] = "example.com",
                    ["x-forwarded-proto"] = "https"
                },
                RawPath = "/items/42",
                Body = body,
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = "GET"
                    }
                }
            };

            using HttpRequestMessage requestMessage = request.CreateRequestMessage();

            Assert.That(requestMessage.Content, Is.Null);
        }

        [Test]
        public async Task CreateResponse_ShouldMapStringContent()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.Created)
            {
                Content = new StringContent("created", Encoding.UTF8, "text/plain")
            };

            APIGatewayHttpApiV2ProxyResponse response = await responseMessage.CreateResponse();

            Assert.That(response.StatusCode, Is.EqualTo(201));
            Assert.That(response.Body, Is.EqualTo("created"));
            Assert.That(response.IsBase64Encoded, Is.False);
        }

        [Test]
        public async Task CreateResponse_ShouldMapFlattenedHeaders()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.OK)
            {
                Content = new StringContent("created", Encoding.UTF8, "text/plain")
            };

            responseMessage.Headers.TryAddWithoutValidation("x-result", new[] { "one", "two" });
            responseMessage.Content.Headers.TryAddWithoutValidation("content-language", new[] { "en", "hu" });

            APIGatewayHttpApiV2ProxyResponse response = await responseMessage.CreateResponse();

            Assert.That(response.Headers["x-result"], Is.EqualTo("one,two"));
            Assert.That(response.Headers["content-language"], Is.EqualTo("en,hu"));
        }

        [Test]
        public async Task CreateResponse_ShouldMergeDuplicateHeaders()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            };

            responseMessage.Headers.TryAddWithoutValidation("x-shared", "response");
            responseMessage.Content.Headers.TryAddWithoutValidation("X-Shared", "content");

            APIGatewayHttpApiV2ProxyResponse response = await responseMessage.CreateResponse();

            Assert.That(response.Headers["x-shared"], Is.EqualTo("response,content"));
        }

        [Test]
        public async Task CreateResponse_ShouldMapCookies()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.OK);

            responseMessage.Headers.TryAddWithoutValidation("Set-Cookie", new[] { "a=1", "b=2" });

            APIGatewayHttpApiV2ProxyResponse response = await responseMessage.CreateResponse();

            Assert.That(response.Cookies, Is.EqualTo(new[] { "a=1", "b=2" }));
        }

        [Test]
        public async Task CreateResponse_ShouldBase64EncodeBinaryContent()
        {
            byte[] bytes = { 3, 4, 5, 250, 251, 252 };
            using HttpResponseMessage responseMessage = new(HttpStatusCode.Accepted)
            {
                Content = new ByteArrayContent(bytes)
            };

            APIGatewayHttpApiV2ProxyResponse response = await responseMessage.CreateResponse();

            Assert.That(response.StatusCode, Is.EqualTo(202));
            Assert.That(response.Body, Is.EqualTo(Convert.ToBase64String(bytes)));
            Assert.That(response.IsBase64Encoded, Is.True);
        }

        [Test]
        public async Task CreateResponse_ShouldAllowNullBodyWhenContentIsMissing()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.NoContent);

            APIGatewayHttpApiV2ProxyResponse response = await responseMessage.CreateResponse();

            Assert.That(response.StatusCode, Is.EqualTo(204));
            Assert.That(response.Body, Is.Null);
            Assert.That(response.IsBase64Encoded, Is.False);
            Assert.That(response.Headers, Is.Empty);
            Assert.That(response.Cookies, Is.Empty);
        }

        private static object? GetProperty(HttpRequestMessage request, string key)
        {
#if NET5_0_OR_GREATER
            return request.Options.TryGetValue(new HttpRequestOptionsKey<object?>(key), out object? value) ? value : null;
#else
            return request.Properties.TryGetValue(key, out object? value) ? value : null;
#endif
        }
    }
}
