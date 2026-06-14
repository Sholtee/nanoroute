/********************************************************************************
* ApiGatewayV2RouterMappingTests.cs                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;

using NUnit.Framework;

namespace NanoRoute.AwsLambda.Tests
{
    [TestFixture]
    internal sealed class ApiGatewayV2RouterMappingTests
    {
        [Test]
        public void CreateRequestMessage_ShouldBuildUriFromRequestContextDomainNameAndHttps()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            APIGatewayHttpApiV2ProxyRequest request = CreateRequest
            (
                method: "POST",
                headers: new Dictionary<string, string>
                {
                    ["host"] = "evil.example",
                    ["x-forwarded-proto"] = "http",
                    ["forwarded"] = "for=192.0.2.60;proto=http;host=evil.example"
                }
            );

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(requestMessage.RequestUri, Is.EqualTo(new Uri("https://example.com/items/42?filter=active")));
            Assert.That(requestMessage.OriginalRequest, Is.SameAs(request));
            Assert.That(requestMessage.TraceId, Is.EqualTo("request-id"));
        }

        [Test]
        public void CreateRequestMessage_ShouldUseConfiguredRequestOrigin()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router
                .CreateBuilder()
                .ConfigureRouting(static config => config with
                {
                    RequestScheme = "http",
                    RequestDomain = "localhost:8080"
                })
                .CreateRouter();

            APIGatewayHttpApiV2ProxyRequest request = CreateRequest(domainName: "ignored.example");

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.RequestUri, Is.EqualTo(new Uri("http://localhost:8080/items/42?filter=active")));
        }

        [Test]
        public void CreateRequestMessage_ShouldUseSlashWhenRawPathIsEmpty()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            APIGatewayHttpApiV2ProxyRequest request = CreateRequest(rawPath: "");

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.RequestUri, Is.EqualTo(new Uri("https://example.com/?filter=active")));
        }

        [Test]
        public void CreateRequestMessage_ShouldOmitQueryWhenRawQueryStringIsEmpty()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            APIGatewayHttpApiV2ProxyRequest request = CreateRequest(rawQueryString: "");

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.RequestUri, Is.EqualTo(new Uri("https://example.com/items/42")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a host")]
        public void CreateRequestMessage_ShouldThrowWhenRequestUriCannotBeDetermined(string? domainName)
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();
            APIGatewayHttpApiV2ProxyRequest request = CreateRequest(domainName: domainName);

            Assert.Throws<InvalidOperationException>(() => router.CreateRequestMessage(request));
        }

        [Test]
        public void CreateRequestMessage_ShouldMapRequestHeaders()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();
            APIGatewayHttpApiV2ProxyRequest request = CreateRequest
            (
                headers: new Dictionary<string, string>
                {
                    ["accept"] = "application/json"
                }
            );

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.Headers.GetValues("accept"), Is.EqualTo(new[] { "application/json" }));
        }

        [Test]
        public void CreateRequestMessage_ShouldMapContentHeaders()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();
            APIGatewayHttpApiV2ProxyRequest request = CreateRequest
            (
                headers: new Dictionary<string, string>
                {
                    ["content-md5"] = "digest",
                    ["content-type"] = "application/json"
                },
                body: "hello"
            );

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.Content!.Headers.GetValues("content-md5"), Is.EqualTo(new[] { "digest" }));
            Assert.That(requestMessage.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            Assert.That(requestMessage.Headers.Any(header => string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void CreateRequestMessage_ShouldMapTextBody()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();
            APIGatewayHttpApiV2ProxyRequest request = CreateRequest(body: "hello");

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.Content, Is.TypeOf<StringContent>());
            Assert.That(requestMessage.Content!.ReadAsStringAsync().GetAwaiter().GetResult(), Is.EqualTo("hello"));
        }

        [Test]
        public void CreateRequestMessage_ShouldDecodeBase64Body()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            byte[] bytes = { 0, 1, 2, 253, 254, 255 };
            APIGatewayHttpApiV2ProxyRequest request = CreateRequest
            (
                body: Convert.ToBase64String(bytes),
                isBase64Encoded: true
            );

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.Content, Is.TypeOf<StreamContent>());
            Assert.That(requestMessage.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult(), Is.EqualTo(bytes));
        }

        [TestCase(null)]
        [TestCase("")]
        public void CreateRequestMessage_ShouldLeaveContentNullWhenBodyIsMissingOrEmpty(string? body)
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();
            APIGatewayHttpApiV2ProxyRequest request = CreateRequest(body: body);

            using HttpRequestMessage requestMessage = router.CreateRequestMessage(request);

            Assert.That(requestMessage.Content, Is.Null);
        }

        [Test]
        public async Task CreateResponse_ShouldMapStringContent()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.Created)
            {
                Content = new StringContent("created", Encoding.UTF8, "text/plain")
            };

            APIGatewayHttpApiV2ProxyResponse response = await ApiGatewayV2Router.CreateResponse(responseMessage);

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

            APIGatewayHttpApiV2ProxyResponse response = await ApiGatewayV2Router.CreateResponse(responseMessage);

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

            APIGatewayHttpApiV2ProxyResponse response = await ApiGatewayV2Router.CreateResponse(responseMessage);

            Assert.That(response.Headers["x-shared"], Is.EqualTo("response,content"));
        }

        [Test]
        public async Task CreateResponse_ShouldMapCookies()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.OK);

            responseMessage.Headers.TryAddWithoutValidation("Set-Cookie", new[] { "a=1", "b=2" });

            APIGatewayHttpApiV2ProxyResponse response = await ApiGatewayV2Router.CreateResponse(responseMessage);

            Assert.That(response.Cookies, Is.EquivalentTo(new[] { "a=1", "b=2" }));
        }

        [Test]
        public async Task CreateResponse_ShouldBase64EncodeBinaryContent()
        {
            byte[] bytes = { 3, 4, 5, 250, 251, 252 };
            using HttpResponseMessage responseMessage = new(HttpStatusCode.Accepted)
            {
                Content = new ByteArrayContent(bytes)
            };

            APIGatewayHttpApiV2ProxyResponse response = await ApiGatewayV2Router.CreateResponse(responseMessage);

            Assert.That(response.StatusCode, Is.EqualTo(202));
            Assert.That(response.Body, Is.EqualTo(Convert.ToBase64String(bytes)));
            Assert.That(response.IsBase64Encoded, Is.True);
        }

        [Test]
        public async Task CreateResponse_ShouldAllowNullBodyWhenContentIsMissing()
        {
            using HttpResponseMessage responseMessage = new(HttpStatusCode.NoContent);

            APIGatewayHttpApiV2ProxyResponse response = await ApiGatewayV2Router.CreateResponse(responseMessage);

            Assert.That(response.StatusCode, Is.EqualTo(204));
            Assert.That(response.Body, Is.Null);
            Assert.That(response.IsBase64Encoded, Is.False);
            Assert.That(response.Headers, Is.Empty);
            Assert.That(response.Cookies, Is.Empty);
        }

        private static APIGatewayHttpApiV2ProxyRequest CreateRequest
        (
            string method = "GET",
            string rawPath = "/items/42",
            string rawQueryString = "filter=active",
            IDictionary<string, string>? headers = null,
            string? domainName = "example.com",
            string requestId = "request-id",
            string? body = null,
            bool isBase64Encoded = false
        )
        {
            Dictionary<string, string> allHeaders = new(StringComparer.OrdinalIgnoreCase);

            if (headers is not null)
                foreach (KeyValuePair<string, string> header in headers)
                    allHeaders[header.Key] = header.Value;

            return new APIGatewayHttpApiV2ProxyRequest
            {
                Headers = allHeaders,
                RawPath = rawPath,
                RawQueryString = rawQueryString,
                Body = body,
                IsBase64Encoded = isBase64Encoded,
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    DomainName = domainName,
                    RequestId = requestId,
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = method
                    }
                }
            };
        }
    }
}
