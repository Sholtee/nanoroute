/********************************************************************************
* RouterEventSourceTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;

using Moq;
using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class RouterEventSourceTests
    {
        private sealed class TestEventSource : EventSource;

        public static IEnumerable<EventSourceWriter> ESWriters
        {
            get
            {
                yield return RouterEventSource.Debug;
                yield return RouterEventSource.Info;
                yield return RouterEventSource.Warning;
                yield return RouterEventSource.Error;
            }
        }

        [Test]
        public void LogHelpers_ShouldEmitEventsWithTheExpectedLevelsAndPayload([ValueSource(nameof(ESWriters))] EventSourceWriter writer)
        {
            using DebugEventListener listener = new(EventLevel.LogAlways);

            writer.Write("Event", static value => new { Value = value }, "data");

            Assert.That(SpinWait.SpinUntil(() => listener.Events.Count == 1, 1000), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(listener.Events[0].EventName, Is.EqualTo("Event"));
                Assert.That(listener.Events[0].Level, Is.EqualTo(writer.Level));
                Assert.That(listener.Events[0].PayloadNames, Is.EquivalentTo(new[] { "Value" }));
                Assert.That(listener.Events[0].Payload, Is.EquivalentTo(new object?[] { "data" }));
            });
        }

        [Test]
        public void LogHelpers_ShouldEmitEventsWithTheExpectedLevelsAndPayload_DoubleParam([ValueSource(nameof(ESWriters))] EventSourceWriter writer)
        {
            using DebugEventListener listener = new(EventLevel.LogAlways);

            writer.Write("Event", static (first, second) => new { First = first, Second = second }, "one", 2);

            Assert.That(SpinWait.SpinUntil(() => listener.Events.Count == 1, 1000), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(listener.Events[0].EventName, Is.EqualTo("Event"));
                Assert.That(listener.Events[0].Level, Is.EqualTo(writer.Level));
                Assert.That(listener.Events[0].PayloadNames, Is.EquivalentTo(new[] { "First", "Second" }));
                Assert.That(listener.Events[0].Payload, Is.EquivalentTo(new object?[] { "one", 2 }));
            });
        }

        [Test]
        public void LogHelpers_ShouldNotInvokePayloadFactoryWhenDisabled_SingleParam()
        {
            using TestEventSource eventSource = new();

            EventSourceWriter writer = new(eventSource, EventLevel.Informational);

            Mock<Func<int, object>> mockPayloadFactory = new(MockBehavior.Strict);

            writer.Write("DisabledEvent", mockPayloadFactory.Object, 1986);

            mockPayloadFactory.Verify(factory => factory.Invoke(It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void LogHelpers_ShouldNotInvokePayloadFactoryWhenDisabled_DoubleParam()
        {
            using TestEventSource eventSource = new();

            EventSourceWriter writer = new(eventSource, EventLevel.Informational);

            Mock<Func<int, int, object>> mockPayloadFactory = new(MockBehavior.Strict);

            writer.Write("DisabledEvent", mockPayloadFactory.Object, 1986, 1026);

            mockPayloadFactory.Verify(factory => factory.Invoke(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }
}
