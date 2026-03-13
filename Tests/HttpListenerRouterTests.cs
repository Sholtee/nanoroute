/********************************************************************************
* HttpListenerRouterTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class HttpListenerRouterTests
    {
        private HttpListener _listener = null!;

        private HttpListenerRouter _router = null!;

        private HttpClient _client = null!;

        [SetUp]
        public void Setup()
        {
            _router = new HttpListenerRouter();
            _router.AddDefaultHandler();

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:1986/");
            _listener.Start();

            _client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:1986/")
            };

        }

        private async Task HandleRequest()
        {
            HttpListenerContext context = await _listener.GetContextAsync();
            await _router.Route(context, new Mock<IServiceProvider>(MockBehavior.Strict).Object);
        }

        [TearDown]
        public void TearDown()
        {
            _listener?.Close();
            _listener = null!;

            _router = null!;

            _client?.Dispose();
            _client = null!;
        }

        [Test]
        public async Task Post_Test()
        {
            _router.AddHandler("POST", "/welcome", async (context, _) =>
            {
                Assert.That(context.Request.Headers.TryGetValues("X-Custom-Request-Header", out IEnumerable<string>? values), Is.True);
                Assert.That(values, Is.EquivalentTo(new string[] { "cica" }));

                HttpResponseMessage resp = new(HttpStatusCode.OK)
                {
                    Content = new StringContent($"Hello {await context.Request.Content!.ReadAsStringAsync()}")
                };
                resp.Headers.Add("X-Custom-Response-Header", "kutya");

                return resp;
            });

            _client.DefaultRequestHeaders.Add("X-Custom-Request-Header", "cica");
            Task<HttpResponseMessage> resp = _client.PostAsync("welcome", new StringContent("Spikey"));

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.Headers.TryGetValues("X-Custom-Response-Header", out IEnumerable<string>? values), Is.True);
            Assert.That(values, Is.EquivalentTo(new string[] { "kutya" }));

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(msg.Content, Is.Not.Null);
            Assert.That(msg.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/plain"));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("Hello Spikey"));
        }

        [Test]
        public async Task Get_Test()
        {
            _router
                .AddParameterParser("str", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("Get", "/welcome/{name:str}", async (context, _) =>
                {
                    Assert.That(context.Request.Headers.TryGetValues("X-Custom-Request-Header", out IEnumerable<string>? values), Is.True);
                    Assert.That(values, Is.EquivalentTo(new string[] { "cica" }));

                    HttpResponseMessage resp = new(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"Hello {context.Parameters["name"]}")
                    };
                    resp.Headers.Add("X-Custom-Response-Header", "kutya");

                    return resp;
                });

            _client.DefaultRequestHeaders.Add("X-Custom-Request-Header", "cica");
            Task<HttpResponseMessage> resp = _client.GetAsync("welcome/Spikey");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.Headers.TryGetValues("X-Custom-Response-Header", out IEnumerable<string>? values), Is.True);
            Assert.That(values, Is.EquivalentTo(new string[] { "kutya" }));

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(msg.Content, Is.Not.Null);
            Assert.That(msg.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/plain"));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("Hello Spikey"));
        }

        [Test]
        public async Task NotFound_Test()
        {
            Task<HttpResponseMessage> resp = _client.GetAsync("welcome");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(msg.Content, Is.Not.Null);
            Assert.That(msg.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("{\"Message\":\"Not found.\"}"));
        }
    }
}
