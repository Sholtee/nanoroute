/********************************************************************************
* RouterTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
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

        private RouterBuilder<HttpMessageRouter, RouterConfig> _routerBuilder = null!;

        private HttpRequestMessage _request = null!;

        [SetUp]
        public void Setup()
        {
            _request = new HttpRequestMessage() { Method = HttpMethod.Get };
            _debugEventListener = new DebugEventListener(EventLevel.LogAlways);
            _routerBuilder = HttpMessageRouter.CreateBuilder();
        }

        [TearDown]
        public void TearDown() => _debugEventListener?.Dispose();
    }
}
