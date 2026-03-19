/********************************************************************************
* HttpListenerRouterTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class HttpListenerRouterTests
    {
        private HttpListener _listener = null!;

        private HttpClient _client = null!;

        private HttpListenerRouter _router = null!;

        private void CreateRouter(Action<RouterBuilder<HttpListenerRouter>> configureRouter)
        {
            RouterBuilder<HttpListenerRouter> routerBuilder = HttpListenerRouter
                .CreateBuilder()
                .AddDefaultHandler();

            configureRouter(routerBuilder);

            _router = routerBuilder.CreateRouter();
        }
        
        [SetUp]
        public void Setup()
        {
            Uri baseAddress = new ($"http://localhost:{GetFreePort()}/");

            _listener = new HttpListener();
            _listener.Prefixes.Add(baseAddress.AbsoluteUri);
            _listener.Start();

            _client = new HttpClient
            {
                BaseAddress = baseAddress
            };
        }

        private static int GetFreePort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();

            int port = ((IPEndPoint) listener.LocalEndpoint).Port;

            listener.Stop();

            return port;
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

            _client?.Dispose();
            _client = null!;

            _router = null!;
        }

        [Test]
        public async Task Post_Test()
        {
            CreateRouter(bldr => bldr
                .AddHandler("POST", "/welcome", async (context, _) =>
                {
                    Assert.That(context.Request.Headers.TryGetValues("X-Custom-Request-Header", out IEnumerable<string>? values), Is.True);
                    Assert.That(values, Is.EquivalentTo(new string[] { "cica" }));

                    HttpResponseMessage resp = new(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"Hello {await context.Request.Content!.ReadAsStringAsync()}")
                    };
                    resp.Headers.Add("X-Custom-Response-Header", "kutya");

                    return resp;
                }));

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
            CreateRouter(bldr => bldr
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
                }));

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
            CreateRouter(_ => { });

            Task <HttpResponseMessage> resp = _client.GetAsync("welcome");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(msg.Content, Is.Not.Null);
            Assert.That(msg.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("{\"message\":\"Not found.\"}"));
        }
    }
}
