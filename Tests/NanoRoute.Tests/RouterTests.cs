/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics.Tracing;
using System.Net.Http;

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

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router<TestRouter, RouterConfig>(builder)
        {
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
