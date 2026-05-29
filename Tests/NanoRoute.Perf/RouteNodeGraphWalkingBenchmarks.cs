/********************************************************************************
* RouteNodeGraphWalkingBenchmarks.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace NanoRoute.Perf
{
    using Internals;

    [MemoryDiagnoser]
    public class RouteNodeGraphWalkingBenchmarks
    {
        private const string LiteralRoutePattern = "/api/v1/users/42/orders/7/items/3/details/";

        private static readonly Task<HttpResponseMessage> s_responseTask = Task.FromResult(new HttpResponseMessage());

        private static readonly RequestHandlerDelegate s_handler = static (_, _) => s_responseTask;

        private RouteNode _root = null!;

        [GlobalSetup]
        public void Setup() => _root = TestRouter
            .CreateBuilder()
            .AddHandler("GET", LiteralRoutePattern, s_handler)
            .CreateSnapshot();

        [Benchmark(Baseline = true)]
        public object WalkSingleBranchGraph()
        {
            RouteNode node = _root;

            while (node.SingleBranch is KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch)
                node = literalBranch.Value;

            return node;
        }

        [Benchmark]
        public object MatchLiteralSegmentsAndWalkGraph()
        {
            RouteNode node = _root;

            for (DelimitedSegment segment = new(LiteralRoutePattern.AsMemory(), '/'); segment.MoveNext();)
            {
                ReadOnlyMemory<char> current = segment.Current;

                if (node.SingleBranch is not KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch || !ReadOnlyMemoryCharComparer.Instance.Equals(literalBranch.Key, current))
                    throw new InvalidOperationException($"Failed to match segment '{current}'.");

                node = literalBranch.Value;
            }

            return node;
        }

        [Benchmark]
        public object SearchPercentThenMatchLiteralSegmentsAndWalkGraph()
        {
            RouteNode node = _root;

            for (DelimitedSegment segment = new(LiteralRoutePattern.AsMemory(), '/'); segment.MoveNext();)
            {
                ReadOnlyMemory<char> current = segment.Current;

                if (current.Span.IndexOf('%') >= 0)
                    throw new InvalidOperationException($"Unexpected escaped segment '{current}'.");

                if (node.SingleBranch is not KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch || !ReadOnlyMemoryCharComparer.Instance.Equals(literalBranch.Key, current))
                    throw new InvalidOperationException($"Failed to match segment '{current}'.");

                node = literalBranch.Value;
            }

            return node;
        }

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router<TestRouter, RouterConfig>(builder);
    }
}
