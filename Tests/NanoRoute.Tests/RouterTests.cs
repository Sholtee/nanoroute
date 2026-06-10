/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed partial class RouterTests
    {
        private static readonly HttpResponseMessage s_response = new();

        private DebugEventListener _debugEventListener = null!;

        private RouterBuilder<TestRouter, RouterConfig> _routerBuilder = null!;

        private HttpRequestMessage _request = null!;

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> routerBuilder)
        {
            private readonly RequestPipeline _pipeline = new(routerBuilder, routerBuilder.RouterConfig.MatchingPrecedence);

            public Task<HttpResponseMessage> Handle(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default) =>
                _pipeline.ExecuteAsync(request, services, cancellation);

            public static RouterBuilder<TestRouter, RouterConfig> CreateBuilder() => new(static builder => new TestRouter(builder));
        }

        [SetUp]
        public void Setup()
        {
            _request = new HttpRequestMessage() { Method = HttpMethod.Get };
            _debugEventListener = new DebugEventListener(EventLevel.LogAlways);
            _routerBuilder = TestRouter.CreateBuilder();
        }

        [TearDown]
        public void TearDown() => _debugEventListener?.Dispose();
    }
}
