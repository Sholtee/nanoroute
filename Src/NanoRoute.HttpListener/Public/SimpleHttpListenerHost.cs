/********************************************************************************
* SimpleHttpListenerHost.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute.HttpListener
{
    using Internals;

    /// <summary>
    /// 
    /// </summary>
    public sealed class SimpleHttpListenerHost(string uriPrefix, int workerCount, int queueCapacity, HttpListenerRouter router, IServiceProvider rootScope)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public async Task Run(CancellationToken cancellation)
        {
            TaskCompletionSource<HttpListenerContext> cancellationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation.Register(() => cancellationTcs.SetException(new OperationCanceledException()));

            using System.Net.HttpListener listener = new();
            listener.Prefixes.Add(uriPrefix);
            listener.Start();

            using WorkerPool workers = new(workerCount, queueCapacity);

            while (true)
            {
                HttpListenerContext context = await await Task.WhenAny(listener.GetContextAsync(), cancellationTcs.Task);

                if (!workers.TryQueue(ProcessRequest))
                    context.Response.Abort();

                async Task ProcessRequest(CancellationToken cancellation)
                {
                    using IServiceScope scope = rootScope.CreateScope();

                    await router.Route(context, scope.ServiceProvider, cancellation);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void RunUntilCancelKeyPress()
        {
            using CancellationTokenSource cts = new();

            Console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                Run(cts.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown 
            }
            finally
            {
                Console.CancelKeyPress -= OnCancelKeyPress;
            }

            void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
            {
                args.Cancel = true;  // suppress termination

                if (cts.IsCancellationRequested)
                    // forced shutdown
                    Environment.Exit(1);

                cts.Cancel();
            }
        }
    }
}
