/********************************************************************************
* WorkerPool.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    internal delegate Task WorkItem(CancellationToken cancellation);

    internal sealed class WorkerPool : IDisposable
    {
        private readonly ConcurrentQueue<WorkItem> _queue = new();

        private readonly CancellationTokenSource _stopTokenSource = new();

        private readonly SemaphoreSlim _workerAvailableSignal = new(0);

        private readonly Task[] _workers;

        private int _capacity;

        private async Task WorkerLoopAsync(int index)
        {
            Logger<WorkerPool>.Info.Write("StartingWorker", index, static index => new { Index = index });

            while (!_stopTokenSource.IsCancellationRequested)
                try
                {
                    await _workerAvailableSignal.WaitAsync(_stopTokenSource.Token);

                    if (_queue.TryDequeue(out WorkItem work))
                        try
                        {
                            await work(_stopTokenSource.Token);
                        }
                        finally
                        {
                            Interlocked.Increment(ref _capacity);
                        }
                }
                catch (OperationCanceledException) when (_stopTokenSource.IsCancellationRequested)
                {
                    // shutting down
                    break;
                }
                catch (Exception ex)
                {
                    Logger<WorkerPool>.Error.Write("UnhandledWorkerException", ex, index, static (ex, index) => new
                    {
                        Error = ex.ToString(),
                        Index = index
                    });
                }

            Logger<WorkerPool>.Info.Write("TerminatingWorker", index, static index => new { Index = index });
        }

        private static bool TryDecrementIfGreaterThan(ref int value, int minExclusive)
        {
            while (true)
            {
                int current = Volatile.Read(ref value);

                if (current <= minExclusive)
                    return false;

                int next = current - 1;

                if (Interlocked.CompareExchange(ref value, next, current) == current)
                    return true;
            }
        }

        public WorkerPool(int maxConcurrency, int maxCapacity)
        {
            if (maxConcurrency <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

            if (maxCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity));

            _workers = new Task[maxConcurrency];

            for (int i = 0; i < _workers.Length; i++)
                _workers[i] = WorkerLoopAsync(i);

            _capacity = maxCapacity;
        }

        /// <summary>
        /// Ensure that no more work items will be queued after calling the <see cref="Dispose"/> method. 
        /// </summary>
        public void Dispose()
        {
            _stopTokenSource.Cancel();
            Task.WaitAll(_workers);  // should not throw due to the "catch" block in WorkerLoopAsync()

            _stopTokenSource.Dispose();
            _workerAvailableSignal.Dispose();
        }

        public bool TryQueue(WorkItem work)
        {
            if (!TryDecrementIfGreaterThan(ref _capacity, 0))
                return false;

            _queue.Enqueue(work);

            // This could throw if the object has already been disposed when the TryQueue gets called.
            // In practice this will never happen as the queue is created before and disposed after
            // the main loop.
            _workerAvailableSignal.Release();

            return true;
        }

        public int Capacity => _capacity;
    }
}
