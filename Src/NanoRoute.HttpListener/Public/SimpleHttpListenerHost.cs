/********************************************************************************
* SimpleHttpListenerHost.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute.HttpListener
{
    using Internals;

    /// <summary>
    /// Runs a small prefix-based <see cref="System.Net.HttpListener"/> host for a configured <see cref="HttpListenerRouter"/>.
    /// </summary>
    /// <param name="uriPrefix">The listener URI prefix, for example <c>http://localhost:8080/</c>.</param>
    /// <param name="maxConcurrency">The maximum number of request workers to run concurrently. The value must be greater than zero.</param>
    /// <param name="queueCapacity">The maximum number of accepted requests that may be running or waiting for a worker. The value must be greater than zero.</param>
    /// <param name="router">The router used to process accepted listener contexts.</param>
    /// <param name="rootScope">The root service provider used to create one dependency-injection scope per request.</param>
    /// <remarks>
    /// This host is intended for simple local or embedded listener scenarios. Applications that need custom accept
    /// loops, diagnostics, shutdown policies, or concurrency control can call <see cref="HttpListenerRouter.Route"/>
    /// from their own <see cref="System.Net.HttpListener"/> loop instead.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxConcurrency"/> or <paramref name="queueCapacity"/> is less than or equal to zero.
    /// </exception>
    public sealed class SimpleHttpListenerHost(string uriPrefix, int maxConcurrency, int queueCapacity, HttpListenerRouter router, IServiceProvider rootScope)
    {
        private readonly int _maxConcurrency = ValidateGreaterThanZero(maxConcurrency, nameof(maxConcurrency));

        private readonly int _queueCapacity = ValidateGreaterThanZero(queueCapacity, nameof(queueCapacity));

        private static int ValidateGreaterThanZero(int value, [CallerArgumentExpression(nameof(value))] string? name = null)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(name);

            return value;
        }

        /// <summary>
        /// Starts the listener and processes accepted requests until cancellation is requested.
        /// </summary>
        /// <param name="cancellation">A token that stops the listener accept loop and is passed to queued workers.</param>
        /// <returns>A task that completes when the host stops accepting requests.</returns>
        /// <remarks>
        /// Each accepted request is processed in a service scope created from the root provider. If all in-flight request slots
        /// are busy, the newly accepted response is aborted.
        /// </remarks>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellation"/> is cancelled.</exception>
        public async Task Run(CancellationToken cancellation)
        {
            TaskCompletionSource<HttpListenerContext> cancellationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation.Register(() => cancellationTcs.SetException(new OperationCanceledException()));

            using System.Net.HttpListener listener = new();
            listener.Prefixes.Add(uriPrefix);
            listener.Start();

            using WorkerPool workers = new(_maxConcurrency, _queueCapacity);

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
        /// Runs the host until the user presses Ctrl+C.
        /// </summary>
        /// <remarks>
        /// The first Ctrl+C requests graceful cancellation. A second Ctrl+C exits the process immediately.
        /// </remarks>
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
