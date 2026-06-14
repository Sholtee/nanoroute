/********************************************************************************
* HttpMethodExtensionsTests.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net.Http;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class HttpMethodExtensionsTests
    {
        [TestCase("DELETE", "delete")]
        [TestCase("GET", "get")]
        [TestCase("HEAD", "head")]
        [TestCase("OPTIONS", "options")]
        [TestCase("PATCH", "patch")]
        [TestCase("POST", "post")]
        [TestCase("PUT", "put")]
        [TestCase("TRACE", "trace")]
        public void For_ShouldReturnKnownHttpMethodInstance(string method, string methodWithDifferentCasing)
        {
            HttpMethod httpMethod = HttpMethod.For(method);

            Assert.That(httpMethod.Method, Is.EqualTo(method));
            Assert.That(HttpMethod.For(methodWithDifferentCasing), Is.SameAs(httpMethod));
        }

        [Test]
        public void For_ShouldCreateCustomHttpMethod()
        {
            HttpMethod method = HttpMethod.For("BREW");

            Assert.That(method.Method, Is.EqualTo("BREW"));
        }

        [Test]
        public void For_ShouldRejectNullMethod()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(static () => HttpMethod.For(null!))!;

            Assert.That(ex.ParamName, Is.EqualTo("method"));
        }

        [Test]
        public void For_ShouldRejectEmptyMethod()
        {
            Assert.Throws<ArgumentException>(static () => HttpMethod.For(""));
        }

        [Test]
        public void For_ShouldRejectInvalidMethod()
        {
            Assert.Throws<FormatException>(static () => HttpMethod.For("not a method"));
        }
    }
}
