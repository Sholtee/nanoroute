/********************************************************************************
* ApiGatewayHttpApiV2RouterIntegrationTests.cs                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.AwsLambda.Tests
{
    [TestFixture]
    [NonParallelizable]
    internal sealed class ApiGatewayHttpApiV2RouterIntegrationTests
    {
        private const string LAMBDA_URL_ENV_VAR = "NANOROUTE_TEST_LAMBDA_URL";

        private static readonly JsonSerializerOptions s_caseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

        private HttpClient _client = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            string? lambdaUrl = Environment.GetEnvironmentVariable(LAMBDA_URL_ENV_VAR);

            if (string.IsNullOrWhiteSpace(lambdaUrl))
                Assert.Ignore($"{LAMBDA_URL_ENV_VAR} is not set.");

            if (!Uri.TryCreate(lambdaUrl, UriKind.Absolute, out Uri? baseAddress))
                Assert.Fail($"{LAMBDA_URL_ENV_VAR} is not a valid absolute URL: {lambdaUrl}");

            _client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true })
            {
                BaseAddress = baseAddress
            };
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _client?.Dispose();

        [Test]
        public async Task Route_ShouldReturnHealth()
        {
            using HttpResponseMessage response = await _client.GetAsync(RelativeUri("health"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("ok"));
        }

        [Test]
        public async Task Route_ShouldBindRouteAndQueryParameters()
        {
            using HttpResponseMessage response = await _client.GetAsync(RelativeUri("items/42?filter=active"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(body.RootElement.GetProperty("id").GetInt32(), Is.EqualTo(42));
            Assert.That(body.RootElement.GetProperty("filter").GetString(), Is.EqualTo("active"));
        }

        [Test]
        public async Task Route_ShouldBindJsonBody()
        {
            using HttpResponseMessage response = await _client.PostAsync
            (
                RelativeUri("echo"),
                new StringContent("{\"message\":\"hello lambda\"}", Encoding.UTF8, "application/json")
            );

            string content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine(content);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content?.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

            EchoResponse body = JsonSerializer.Deserialize<EchoResponse>(content, s_caseInsensitiveJson)!;

            Assert.That(body.Message, Is.EqualTo("hello lambda"));
        }

        [Test]
        public async Task Route_ShouldReturnCookies()
        {
            using HttpResponseMessage response = await _client.GetAsync(RelativeUri("cookies"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.TryGetValues("X-NanoRoute-Fixture", out var fixture), Is.True);
            Assert.That(fixture, Is.EqualTo(new[] { "aws-lambda" }));
            Assert.That(response.Headers.TryGetValues("Set-Cookie", out var cookies), Is.True);
            Assert.That(cookies!.Any(cookie => cookie.StartsWith("nano-route-cookie=ok", StringComparison.Ordinal)), Is.True);
        }

        private sealed class EchoResponse
        {
            public string? Message { get; set; }
        }

        private static Uri RelativeUri(string value) => new(value, UriKind.Relative);
    }
}
