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
        public enum BranchLookup
        {
            SingleBranch,
            LiteralChildren
        }

        private const string LiteralRoutePattern = "/api/v1/users/42/orders/7/items/3/details/";

        private static readonly Task<HttpResponseMessage> s_responseTask = Task.FromResult(new HttpResponseMessage());

        private static readonly RequestHandlerDelegate s_handler = static (_, _) => s_responseTask;

        private RouteNode _root = null!;

        [Params(BranchLookup.SingleBranch, BranchLookup.LiteralChildren)]
        public BranchLookup BranchLookupKind { get; set; }

        [GlobalSetup]
        public void Setup() => _root = TestRouter
            .CreateBuilder()
            .AddHandler("GET", LiteralRoutePattern, s_handler)
            .CreateSnapshot();

        [Benchmark]
        public object MatchLiteralSegmentsAndWalkGraph()
        {
            RouteNode node = _root;

            for (DelimitedSegment segment = new(LiteralRoutePattern.AsMemory(), '/'); segment.MoveNext();)
            {
                ReadOnlyMemory<char> current = segment.Current;

                if (!TryMatchLiteralBranch(node, current, out RouteNode nextNode))
                    throw new InvalidOperationException($"Failed to match segment '{current}'.");

                node = nextNode;
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

                if (!TryMatchLiteralBranch(node, current, out RouteNode nextNode))
                    throw new InvalidOperationException($"Failed to match segment '{current}'.");

                node = nextNode;
            }

            return node;
        }

        private bool TryMatchLiteralBranch(RouteNode node, ReadOnlyMemory<char> segment, out RouteNode nextNode)
        {
            switch (BranchLookupKind)
            {
                case BranchLookup.SingleBranch:
                    if (node.SingleBranch is KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch && ReadOnlyMemoryCharComparer.Instance.Equals(literalBranch.Key, segment))
                    {
                        nextNode = literalBranch.Value;
                        return true;
                    }

                    nextNode = null!;
                    return false;

                case BranchLookup.LiteralChildren:
                    if (node.LiteralChildren.TryGetValue(segment, out RouteNode? literalChild))
                    {
                        nextNode = literalChild!;
                        return true;
                    }

                    nextNode = null!;
                    return false;

                default:
                    throw new InvalidOperationException($"Unknown branch lookup kind: {BranchLookupKind}.");
            }
        }

        private sealed class TestRouter(RouterBuilder<TestRouter, RouterConfig> builder) : Router<TestRouter, RouterConfig>(builder);
    }
}
