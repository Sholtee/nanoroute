/********************************************************************************
* WorkerPoolTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace NanoRoute.HttpListener.Tests
{
    using Internals;

    [TestFixture]
    internal sealed class WorkerPoolTests
    {
        private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

        private static TaskCompletionSource<bool> CreateCompletionSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private DebugEventListener<WorkerPool> _events = null!;

        private EventWrittenEventArgs WaitForEvent(string eventName)
        {
            SpinWait.SpinUntil(() => _events.Events.Any(CompatibleEvent), s_timeout);
            return _events.Events.Single(CompatibleEvent);

            bool CompatibleEvent(EventWrittenEventArgs e) => e.EventName == eventName;
        }

        [SetUp]
        public void Setup()
        {
            _events = new DebugEventListener<WorkerPool>(EventLevel.LogAlways);
        }

        [TearDown]
        public void TearDown()
        {
            _events.Dispose();
            _events = null!;
        }

        [Test]
        public void Constructor_ShouldRejectInvalidMaxConcurrency([Values(0, -1)] int maxConcurrency)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerPool(maxConcurrency, 1))!;
            Assert.That(ex.ParamName, Is.EqualTo("maxConcurrency"));
        }

        [Test]
        public void Constructor_ShouldRejectInvalidCapacity([Values(0, -1)] int maxCapacity)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerPool(1, maxCapacity))!;
            Assert.That(ex.ParamName, Is.EqualTo("maxCapacity"));
        }

        [Test]
        public void Lifecycle_ShouldBeLogged()
        {
            WorkerPool pool = new(1, 1);

            Assert.That(SpinWait.SpinUntil(() => _events.Events.Count is 1, s_timeout), Is.True);

            EventWrittenEventArgs workerStarted = WaitForEvent("StartingWorker");

            Assert.Multiple(() =>
            {
                Assert.That(workerStarted.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(workerStarted.PayloadNames, Is.EquivalentTo(new[] { "Index" }));
                Assert.That(workerStarted.Payload, Is.EquivalentTo(new object?[] { 0 }));
            });

            pool.Dispose();
            Assert.That(SpinWait.SpinUntil(() => _events.Events.Count is 2, s_timeout), Is.True);

            EventWrittenEventArgs workerTerminated = WaitForEvent("TerminatingWorker");

            Assert.Multiple(() =>
            {
                Assert.That(workerTerminated.Level, Is.EqualTo(EventLevel.Informational));
                Assert.That(workerTerminated.PayloadNames, Is.EquivalentTo(new[] { "Index" }));
                Assert.That(workerTerminated.Payload, Is.EquivalentTo(new object?[] { 0 }));
            });
        }

        [Test]
        public void TryQueue_ShouldExecuteQueuedWork([Values(1, 2)] int maxConcurrency)
        {
            using WorkerPool pool = new(maxConcurrency, 1);

            TaskCompletionSource<bool> work = CreateCompletionSource();

            Mock<WorkItem> mockWork = new(MockBehavior.Strict);
            mockWork
                .Setup(m => m.Invoke(It.Is<CancellationToken>(c => !c.IsCancellationRequested)))
                .Returns(work.Task);

            Assert.That(pool.TryQueue(mockWork.Object), Is.True);
            Assert.That(pool.Capacity, Is.Zero);

            work.SetResult(true);

            Assert.That(SpinWait.SpinUntil(() => pool.Capacity == 1, s_timeout), Is.True);
            mockWork.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void TryQueue_ShouldUseAvailableWorkers([Values(2, 3)] int maxConcurrency)
        {
            using WorkerPool pool = new(maxConcurrency, 2);

            using CountdownEvent countdown = new(2);

            async Task Worker(CancellationToken cancellation)
            {
                await Task.Yield();

                countdown.Signal();
                countdown.Wait(cancellation);  // wait until both workers reach here
            }

            Assert.That(pool.TryQueue(Worker), Is.True);
            Assert.That(pool.TryQueue(Worker), Is.True);
            Assert.That(countdown.Wait(s_timeout), Is.True);
            Assert.That(SpinWait.SpinUntil(() => pool.Capacity == 2, s_timeout), Is.True);
        }

        [Test]
        public void TryQueue_ShouldExecuteQueuedWorkItemsInOrder()
        {
            using WorkerPool pool = new(1, 3);

            TaskCompletionSource<bool>
                workerStarted = CreateCompletionSource(),
                work = CreateCompletionSource();

            Mock<WorkItem>
                mockWork_1 = new(MockBehavior.Strict),
                mockWork_2 = new(MockBehavior.Strict),
                mockWork_3 = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockWork_1
                .InSequence(seq)
                .Setup(m => m.Invoke(It.Is<CancellationToken>(c => !c.IsCancellationRequested)))
                .Returns<CancellationToken>
                (
                    cancellation =>
                    {
                        workerStarted.SetResult(true);
                        return work.Task;
                    }
                );

            mockWork_2
                .InSequence(seq)
                .Setup(m => m.Invoke(It.Is<CancellationToken>(c => !c.IsCancellationRequested)))
                .Returns(Task.CompletedTask);

            mockWork_3
                .InSequence(seq)
                .Setup(m => m.Invoke(It.Is<CancellationToken>(c => !c.IsCancellationRequested)))
                .Returns(Task.CompletedTask);

            Assert.That(pool.TryQueue(mockWork_1.Object), Is.True);
            Assert.That(pool.TryQueue(mockWork_2.Object), Is.True);
            Assert.That(pool.TryQueue(mockWork_3.Object), Is.True);

            Assert.That(workerStarted.Task.Wait(s_timeout), Is.True);

            mockWork_1.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Once);
            mockWork_2.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Never);
            mockWork_3.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Never);

            Assert.That(pool.Capacity, Is.Zero);

            work.SetResult(true);

            Assert.That(SpinWait.SpinUntil(() => pool.Capacity == 3, s_timeout), Is.True);

            mockWork_1.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Once);
            mockWork_2.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Once);
            mockWork_3.Verify(m => m.Invoke(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void TryQueue_ShouldRejectWorkWhenCapacityIsFull()
        {
            using WorkerPool pool = new(1, 1);

            TaskCompletionSource<bool> releaseFirstWork = CreateCompletionSource();

            Assert.That(pool.TryQueue(_ => releaseFirstWork.Task), Is.True);
            Assert.That(pool.Capacity, Is.Zero);
            Assert.That(pool.TryQueue(_ => Task.CompletedTask), Is.False);
            Assert.That(pool.Capacity, Is.Zero);

            releaseFirstWork.SetResult(true);

            Assert.That(SpinWait.SpinUntil(() => pool.Capacity == 1, s_timeout), Is.True);
            Assert.That(pool.TryQueue(_ => Task.CompletedTask), Is.True);
        }

        [Test]
        public void WorkerLoop_ShouldContinueAfterWorkItemThrows()
        {
            const string errorMsg = "Worker failure.";

            using WorkerPool pool = new(1, 2);

            TaskCompletionSource<bool>
                firstWorkStarted = CreateCompletionSource(),
                secondWorkStarted = CreateCompletionSource();

            Assert.That(pool.TryQueue(_ =>
            {
                firstWorkStarted.SetResult(true);
                throw new InvalidOperationException(errorMsg);
            }), Is.True);

            Assert.That(pool.TryQueue(_ =>
            {
                secondWorkStarted.SetResult(true);
                return Task.CompletedTask;
            }), Is.True);

            Assert.That(firstWorkStarted.Task.Wait(s_timeout), Is.True);
            Assert.That(secondWorkStarted.Task.Wait(s_timeout), Is.True);
            Assert.That(SpinWait.SpinUntil(() => pool.Capacity == 2, s_timeout), Is.True);

            EventWrittenEventArgs workerTerminated = WaitForEvent("UnhandledWorkerException");

            Assert.Multiple(() =>
            {
                Assert.That(workerTerminated.Level, Is.EqualTo(EventLevel.Error));
                Assert.That(workerTerminated.PayloadNames, Is.EquivalentTo(new[] { "Error", "Index" }));
                Assert.That(workerTerminated.Payload![1], Is.EqualTo(0));
                Assert.That(workerTerminated.Payload![0], Does.Contain(errorMsg));
            });
        }

        [Test]
        public void Dispose_ShouldCancelRunningWork()
        {
            WorkerPool pool = new(1, 1);

            TaskCompletionSource<bool> workStarted = CreateCompletionSource();
            CancellationToken cancellation = default;

            Assert.That(pool.TryQueue(c =>
            {
                cancellation = c;
                workStarted.SetResult(true);
                return Task.CompletedTask;
            }), Is.True);

            Assert.That(workStarted.Task.Wait(s_timeout), Is.True);
            Assert.That(cancellation.IsCancellationRequested, Is.False);
            pool.Dispose();
            Assert.That(cancellation.IsCancellationRequested, Is.True);
        }
    }
}
