/********************************************************************************
* RouteMatchCursorEmitHandlerBenchmarks.cs                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "_cursor is disposed in the Cleanup() step")]
    public class RouteMatchCursorEmitHandlerBenchmarks
    {
        private RouteMatchCursor _cursor = null!;

        [Params(0, 1, 5)]
        public int HandlerCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            HandlerRegistration[] handlers = new HandlerRegistration[HandlerCount];
            for (int i = 0; i < handlers.Length; i++)
                handlers[i] = new HandlerRegistration(static (_, _) => Task.FromResult(new HttpResponseMessage()), "/");

            RouteNode root = new();
            root.HandlerRegistrations[HttpVerb.Get] = handlers;

            _cursor = new RouteMatchCursor
            (
                root,
                HttpVerb.Get,
                new Uri("https://localhost:1986/", UriKind.Absolute),
                null!,
                null!,
                MatchingPrecedence.LiteralFirst,
                default
            );
        }

        [GlobalCleanup]
        public void Cleanup() => _cursor.Dispose();

        [Benchmark]
        public int TryEmitHandlers()
        {
            RouteMatchCursor cursor = _cursor;
            int emittedHandlers = 0;

            while (cursor.TryEmitHandler())
                emittedHandlers++;

            cursor.Reset();

            return emittedHandlers;
        }
    }
}
