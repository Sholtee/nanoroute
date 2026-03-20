/********************************************************************************
* HttpListenerRouterTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        private async Task HandleRequest(CancellationToken cancellation = default)
        {
            HttpListenerContext context = await _listener.GetContextAsync();
            await _router.Route(context, new Mock<IServiceProvider>(MockBehavior.Strict).Object, cancellation);
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

        private sealed class HelloRequest
        {
            public required string Name { get; set; }
        }

        [Test]
        public void Route_ShouldBeNullCheckedForContext()
        {
            CreateRouter(_ => { });

            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>(() => _router.Route(null!, new Mock<IServiceProvider>(MockBehavior.Strict).Object))!;
            Assert.That(ex.ParamName, Is.EqualTo("context"));
        }

        [Test]
        public async Task Route_ShouldBeNullCheckedForServices()
        {
            CreateRouter(_ => { });

            Task<HttpResponseMessage> resp = _client.GetAsync("");
            HttpListenerContext context = await _listener.GetContextAsync();

            ArgumentNullException ex = Assert.ThrowsAsync<ArgumentNullException>(() => _router.Route(context, null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("services"));

            context.Response.Abort();
        }

        [Test]
        public async Task Route_ShouldHandlePostRequests()
        {
            CreateRouter(bldr => bldr
                .AddHandler("POST", "/welcome", async (context, _) =>
                {
                    Assert.That(context.Request.Headers.TryGetValues("X-Custom-Request-Header", out IEnumerable<string>? values), Is.True);
                    Assert.That(values, Is.EquivalentTo(new string[] { "cica" }));

                    HttpResponseMessage resp = new(HttpStatusCode.OK)
                    {
                        Content = new StringContent
                        (
                            "Hello " + JsonSerializer.Deserialize<HelloRequest>(await context.Request.Content!.ReadAsStringAsync())!.Name
                        )
                    };
                    resp.Headers.Add("X-Custom-Response-Header", "kutya");

                    return resp;
                }));

            _client.DefaultRequestHeaders.Add("X-Custom-Request-Header", "cica");
            Task<HttpResponseMessage> resp = _client.PostAsync("welcome", new StringContent(JsonSerializer.Serialize(new HelloRequest { Name = "Spikey" }), Encoding.UTF8, "application/json"));

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
        public async Task Route_ShouldHandleGetRequests()
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
        public async Task Route_ShouldReturnNotFoundForUnknownRoutes()
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

        [Test]
        public async Task Route_ShouldCopyContentHeadersToRequestContent()
        {
            CreateRouter(bldr => bldr
                .AddHandler("POST", "/welcome", async (context, _) =>
                {
                    Assert.That(context.Request.Content, Is.Not.Null);
                    Assert.That(context.Request.Content!.Headers.ContentType, Is.Not.Null);
                    Assert.That(context.Request.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
                    Assert.That(context.Request.Content.Headers.ContentType!.CharSet, Is.EqualTo("utf-8"));

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent
                        (
                            "Hello " + JsonSerializer.Deserialize<HelloRequest>(await context.Request.Content!.ReadAsStringAsync())!.Name
                        )
                    };
                }));

            Task<HttpResponseMessage> resp = _client.PostAsync("welcome", new StringContent(JsonSerializer.Serialize(new HelloRequest{ Name = "Spikey" }), Encoding.UTF8, "application/json"));

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("Hello Spikey"));
        }

        [Test]
        public async Task Route_ShouldExposeOriginalHttpListenerRequest()
        {
            CreateRouter(bldr => bldr
                .AddParameterParser("str", (string segment, out object? parsed) => { parsed = segment; return true; })
                .AddHandler("GET", "", async (context, _) =>
                {
                    Assert.That(context.Request.Properties.TryGetValue("OriginalRequest", out object? originalRequest), Is.True);
                    Assert.That(originalRequest, Is.InstanceOf<HttpListenerRequest>());

                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));

            Task<HttpResponseMessage> resp = _client.GetAsync("");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task Route_ShouldAbortTheResponseWhenCancelled()
        {
            Mock<RequestHandler> mockRequestHandler = new(MockBehavior.Strict);

            CreateRouter(bldr => bldr.AddHandler("GET", "", mockRequestHandler.Object));

            using CancellationTokenSource cts = new();
            cts.Cancel();

            Task<HttpResponseMessage> resp = _client.GetAsync("");

            await HandleRequest(cancellation: cts.Token);

            Assert.That(() => resp.GetAwaiter().GetResult(), Throws.TypeOf<HttpRequestException>().Or.TypeOf<TaskCanceledException>());
            mockRequestHandler.Verify(h => h.Invoke(It.IsAny<RequestContext>(), It.IsAny<Func<Task<HttpResponseMessage>>>()), Times.Never);
        }

        [Test]
        public async Task Route_ShouldIgnoreReservedResponseHeaders()
        {
            CreateRouter(bldr => bldr
                .AddHandler("GET", "", async (_, _) =>
                {
                    HttpResponseMessage resp = new(HttpStatusCode.OK) { Content = new StringContent("Hello") };

                    resp.Headers.Add("X-Custom-Response-Header", "kutya");
                    resp.Headers.Add("Server", "CustomServer");
                    resp.Headers.Add("Keep-Alive", "timeout=5");
                    resp.Content.Headers.Add("Content-Length", "999");

                    return resp;
                }));

            Task<HttpResponseMessage> resp = _client.GetAsync("");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(msg.Headers.TryGetValues("X-Custom-Response-Header", out IEnumerable<string>? values), Is.True);
            Assert.That(values, Is.EquivalentTo(new string[] { "kutya" }));
            Assert.That(!msg.Headers.TryGetValues("Server", out IEnumerable<string>? serverValues) || !serverValues.Contains("CustomServer"));
            Assert.That(!msg.Headers.TryGetValues("Keep-Alive", out IEnumerable<string>? keepAliveValues) || !keepAliveValues.Contains("timeout=5"));
            Assert.That(msg.Content.Headers.ContentLength, Is.Not.EqualTo(999));
        }

        [Test]
        public async Task Route_ShouldCopyResponseContentHeaders()
        {
            CreateRouter(bldr => bldr
                .AddHandler("GET", "/welcome", async (_, _) =>
                {
                    StringContent content = new(JsonSerializer.Serialize(new HelloRequest { Name = "Spikey" }));
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                    {
                        CharSet = "utf-8"
                    };
                    content.Headers.ContentLanguage.Add("en");

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = content
                    };
                }));

            Task<HttpResponseMessage> resp = _client.GetAsync("welcome");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(msg.Content.Headers.ContentType, Is.Not.Null);
            Assert.That(msg.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            Assert.That(msg.Content.Headers.ContentType!.CharSet, Is.EqualTo("utf-8"));
            Assert.That(msg.Content.Headers.ContentLanguage, Does.Contain("en"));
            Assert.That(JsonSerializer.Deserialize<HelloRequest>(await msg.Content.ReadAsStringAsync())!.Name, Is.EqualTo("Spikey"));
        }

        [Test]
        public async Task Route_ShouldHandleResponsesWithoutContent()
        {
            CreateRouter(bldr => bldr
                .AddHandler("GET", "", async (_, _) => new HttpResponseMessage(HttpStatusCode.NoContent)));

            Task<HttpResponseMessage> resp = _client.GetAsync("");

            await HandleRequest();

            HttpResponseMessage msg = await resp;

            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.Empty);
        }
    }
}
