/********************************************************************************
* WorkerPoolTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
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

        [Test]
        public void Constructor_ShouldRejectInvalidWorkerCount([Values(0, -1)] int workerCount)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerPool(workerCount, 1))!;
            Assert.That(ex.ParamName, Is.EqualTo("workerCount"));
        }

        [Test]
        public void Constructor_ShouldRejectInvalidCapacity([Values(0, -1)] int maxCapacity)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerPool(1, maxCapacity))!;
            Assert.That(ex.ParamName, Is.EqualTo("maxCapacity"));
        }

        [Test]
        public void TryQueue_ShouldExecuteQueuedWork([Values(1, 2)] int workerCount)
        {
            using WorkerPool pool = new(workerCount, 1);

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
        public void TryQueue_ShouldUseAvailableWorkers([Values(2, 3)] int workerCount)
        {
            using WorkerPool pool = new(workerCount, 2);

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
        public void TryQueue_ShouldRejectWorkWhenPendingQueueIsFull()
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
            using WorkerPool pool = new(1, 2);

            TaskCompletionSource<bool>
                firstWorkStarted = CreateCompletionSource(),
                secondWorkStarted = CreateCompletionSource();

            Assert.That(pool.TryQueue(_ =>
            {
                firstWorkStarted.SetResult(true);
                throw new InvalidOperationException("Worker failure.");
            }), Is.True);

            Assert.That(pool.TryQueue(_ =>
            {
                secondWorkStarted.SetResult(true);
                return Task.CompletedTask;
            }), Is.True);

            Assert.That(firstWorkStarted.Task.Wait(s_timeout), Is.True);
            Assert.That(secondWorkStarted.Task.Wait(s_timeout), Is.True);
            Assert.That(SpinWait.SpinUntil(() => pool.Capacity == 2, s_timeout), Is.True);
        }

        [Test]
        public void Dispose_ShouldCancelRunningWork()
        {
            WorkerPool pool = new(1, 1);

            TaskCompletionSource<bool>
                cancellationObserved = CreateCompletionSource(),
                workStarted = CreateCompletionSource();

            Assert.That(pool.TryQueue(cancellation =>
            {
                cancellation.Register(() => cancellationObserved.SetResult(true));
                workStarted.SetResult(true);
                return cancellationObserved.Task;
            }), Is.True);

            Assert.That(workStarted.Task.Wait(s_timeout), Is.True);

            pool.Dispose();
            Assert.That(cancellationObserved.Task.Wait(s_timeout), Is.True);
        }
    }
}
