/********************************************************************************
* ApiGatewayV2RouterTests.cs                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using Moq;
using NUnit.Framework;

namespace NanoRoute.AwsLambda.Tests
{
    [TestFixture]
    internal sealed class ApiGatewayV2RouterTests
    {
        [Test]
        public async Task Route_ShouldRouteApiGatewayRequest()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router
                .CreateBuilder()
                .AddHandler("GET", "/health/", static (context, _) =>
                {
                    Assert.That(context.Request.Headers.TryGetValues("accept", out IEnumerable<string>? values), Is.True);
                    Assert.That(values, Is.EquivalentTo(new[] { "text/plain" }));

                    HttpResponseMessage response = new(HttpStatusCode.OK)
                    {
                        Content = new StringContent("ok")
                    };
                    response.Headers.TryAddWithoutValidation("x-fixture", "aws-lambda");

                    return Task.FromResult(response);
                })
                .CreateRouter();

            APIGatewayHttpApiV2ProxyResponse response = await router.Route
            (
                CreateRequest("GET", "/health", headers: new Dictionary<string, string>
                {
                    ["accept"] = "text/plain"
                }),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CreateContext(TimeSpan.FromSeconds(10))
            );

            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(response.Body, Is.EqualTo("ok"));
            Assert.That(response.Headers["x-fixture"], Is.EqualTo("aws-lambda"));
        }

        [Test]
        public async Task Route_ShouldReturnGatewayTimeoutWhenRemainingTimeIsTooShort()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router
                .CreateBuilder()
                .AddHandler("GET", "/health/", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter();

            APIGatewayHttpApiV2ProxyResponse response = await router.Route
            (
                CreateRequest("GET", "/health", requestId: "trace-id"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CreateContext(TimeSpan.FromSeconds(1))
            );

            Assert.That(response.StatusCode, Is.EqualTo(504));
            Assert.That(response.Headers["Content-Type"], Is.EqualTo("application/json"));

            using JsonDocument body = JsonDocument.Parse(response.Body);

            Assert.That(body.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(504));
            Assert.That(body.RootElement.GetProperty("title").GetString(), Is.EqualTo("Gateway Timeout"));
            Assert.That(body.RootElement.GetProperty("traceId").GetString(), Is.EqualTo("trace-id"));
        }

        [Test]
        public async Task Route_ShouldRespectConfiguredLambdaTimeoutBuffer()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router
                .CreateBuilder()
                .AddHandler("GET", "/health/", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter(static config =>
                {
                    config.LambdaTimeoutBuffer = TimeSpan.FromSeconds(5);
                });

            APIGatewayHttpApiV2ProxyResponse response = await router.Route
            (
                CreateRequest("GET", "/health"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CreateContext(TimeSpan.FromSeconds(5))
            );

            Assert.That(response.StatusCode, Is.EqualTo(504));
        }

        [Test]
        public async Task Route_ShouldAllowZeroLambdaTimeoutBuffer()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router
                .CreateBuilder()
                .AddHandler("GET", "/health/", static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
                .CreateRouter(static config =>
                {
                    config.LambdaTimeoutBuffer = TimeSpan.Zero;
                });

            APIGatewayHttpApiV2ProxyResponse response = await router.Route
            (
                CreateRequest("GET", "/health"),
                new Mock<IServiceProvider>(MockBehavior.Strict).Object,
                CreateContext(TimeSpan.FromSeconds(1))
            );

            Assert.That(response.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public void LambdaTimeoutBuffer_ShouldRejectNegativeValues()
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>
            (
                static () => new ApiGatewayV2RouterConfig { LambdaTimeoutBuffer = TimeSpan.FromTicks(-1) }
            )!;

            Assert.That(ex.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void RequestScheme_ShouldRejectNullValues()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>
            (
                static () => new ApiGatewayV2RouterConfig { RequestScheme = null! }
            )!;

            Assert.That(ex.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void Route_ShouldBeNullCheckedForRequest()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>
            (
                () => router.Route(null!, new Mock<IServiceProvider>(MockBehavior.Strict).Object, CreateContext(TimeSpan.FromSeconds(10)))
            )!;

            Assert.That(ex.ParamName, Is.EqualTo("request"));
        }

        [Test]
        public void Route_ShouldBeNullCheckedForServices()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>
            (
                () => router.Route(CreateRequest("GET", "/health"), null!, CreateContext(TimeSpan.FromSeconds(10)))
            )!;

            Assert.That(ex.ParamName, Is.EqualTo("services"));
        }

        [Test]
        public void Route_ShouldBeNullCheckedForContext()
        {
            ApiGatewayV2Router router = ApiGatewayV2Router.CreateBuilder().CreateRouter();

            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>
            (
                () => router.Route(CreateRequest("GET", "/health"), new Mock<IServiceProvider>(MockBehavior.Strict).Object, null!)
            )!;

            Assert.That(ex.ParamName, Is.EqualTo("context"));
        }

        private static ILambdaContext CreateContext(TimeSpan remainingTime)
        {
            Mock<ILambdaContext> context = new(MockBehavior.Strict);
            context.SetupGet(static c => c.RemainingTime).Returns(remainingTime);
            return context.Object;
        }

        private static APIGatewayHttpApiV2ProxyRequest CreateRequest
        (
            string method,
            string rawPath,
            IDictionary<string, string>? headers = null,
            string requestId = "request-id"
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
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    DomainName = "example.com",
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
