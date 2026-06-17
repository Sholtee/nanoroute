/********************************************************************************
* BoundedWorkerPool.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.HttpListener.Internals
{
    internal delegate Task WorkItem(CancellationToken cancellation);

    internal sealed class WorkerPool : IDisposable
    {
        private readonly ConcurrentQueue<WorkItem> _queue = new();

        private readonly CancellationTokenSource _stopTokenSource = new();

        private readonly SemaphoreSlim _workerAvailableSignal = new(0);

        private readonly int _queueCapacity;

        private readonly Task[] _workers;

        private int _queueSize;

        private async Task WorkerLoopAsync()
        {
            while (!_stopTokenSource.IsCancellationRequested)
                try
                {
                    await _workerAvailableSignal.WaitAsync(_stopTokenSource.Token);

                    if (!_queue.TryDequeue(out WorkItem work))
                        continue;

                    Interlocked.Decrement(ref _queueSize);

                    await work(_stopTokenSource.Token);
                }
                catch (OperationCanceledException) when (_stopTokenSource.IsCancellationRequested)
                {
                    // shutting down
                    break;
                }
                catch (Exception ex)
                {
                    // write to EventSource
                    _ = ex;
                }
        }

        private static bool TryIncrementIfLessThan(ref int value, int maxExclusive)
        {
            while (true)
            {
                int current = Volatile.Read(ref value);

                if (current >= maxExclusive)
                    return false;

                int next = current + 1;

                if (Interlocked.CompareExchange(ref value, next, current) == current)
                    return true;
            }
        }

        public WorkerPool(int workerCount, int queueCapacity)
        {
            _workers = new Task[workerCount];

            for (int i = 0; i < _workers.Length; i++)
                _workers[i] = Task.Run(WorkerLoopAsync);

            _queueCapacity = queueCapacity;
        }

        /// <summary>
        /// Ensure that no more work items will be queued after calling this method. 
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
            if (_stopTokenSource.IsCancellationRequested)
                return false;

            if (!TryIncrementIfLessThan(ref _queueSize, _queueCapacity))
                return false;

            _queue.Enqueue(work);
            _workerAvailableSignal.Release();

            return true;
        }

        public int QueueSize => _queueSize;
    }
}