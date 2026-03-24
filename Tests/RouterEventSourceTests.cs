/********************************************************************************
* RouterEventSourceTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics.Tracing;
using System.Threading;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class RouterEventSourceTests
    {
        [Test]
        public void LogHelpers_ShouldEmitEventsWithTheExpectedLevelsAndPayload()
        {
            using DebugEventListener listener = new(EventLevel.LogAlways);

            RouterEventSource.Log.Debug("DebugEvent", () => new { Value = "debug" });
            RouterEventSource.Log.Info("InfoEvent", () => new { Value = "info" });
            RouterEventSource.Log.Warn("WarnEvent", () => new { Value = "warn" });
            RouterEventSource.Log.Error("ErrorEvent", () => new { Value = "error" });

            Assert.That(SpinWait.SpinUntil(() => listener.Events.Count == 4, 1000), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(listener.Events[0].EventName, Is.EqualTo("DebugEvent"));
                Assert.That(listener.Events[0].Level, Is.EqualTo(EventLevel.Verbose));
                Assert.That(listener.Events[0].PayloadNames, Is.EquivalentTo(new[] { "Value" }));
                Assert.That(listener.Events[0].Payload, Is.EquivalentTo(new object?[] { "debug" }));

                Assert.That(listener.Events[1].EventName, Is.EqualTo("InfoEvent"));
                Assert.That(listener.Events[1].Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(listener.Events[1].PayloadNames, Is.EquivalentTo(new[] { "Value" }));
                Assert.That(listener.Events[1].Payload, Is.EquivalentTo(new object?[] { "info" }));

                Assert.That(listener.Events[2].EventName, Is.EqualTo("WarnEvent"));
                Assert.That(listener.Events[2].Level, Is.EqualTo(EventLevel.Warning));
                Assert.That(listener.Events[2].PayloadNames, Is.EquivalentTo(new[] { "Value" }));
                Assert.That(listener.Events[2].Payload, Is.EquivalentTo(new object?[] { "warn" }));

                Assert.That(listener.Events[3].EventName, Is.EqualTo("ErrorEvent"));
                Assert.That(listener.Events[3].Level, Is.EqualTo(EventLevel.Error));
                Assert.That(listener.Events[3].PayloadNames, Is.EquivalentTo(new[] { "Value" }));
                Assert.That(listener.Events[3].Payload, Is.EquivalentTo(new object?[] { "error" }));
            });
        }
    }
}
