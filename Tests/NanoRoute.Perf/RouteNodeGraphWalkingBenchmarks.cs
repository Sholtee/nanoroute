/********************************************************************************
* RouteNodeGraphWalkingBenchmarks.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            LiteralBranches
        }

        private const string LiteralRoutePattern = "/api/v1/users/42/orders/7/items/3/details/";

        private static readonly Uri s_requestUri = new($"https://localhost:1986{LiteralRoutePattern}", UriKind.Absolute);

        private static readonly Task<HttpResponseMessage> s_responseTask = Task.FromResult(new HttpResponseMessage());

        private static readonly RequestHandlerDelegate s_handler = static (_, _) => s_responseTask;

        private RouteNode _root = null!;

        [GlobalSetup]
        public void Setup() => _root = InMemoryRouter
            .CreateBuilder()
            .AddHandler("GET", LiteralRoutePattern, s_handler)
            .CreateSnapshot();

        [Benchmark]
        [Arguments(BranchLookup.SingleBranch)]
        [Arguments(BranchLookup.LiteralBranches)]
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Dispose() on DelimitedSegment does nothing")]
        public object MatchLiteralSegmentsAndWalkGraph(BranchLookup branchLookupKind)
        {
            RouteNode node = _root;

            for (DelimitedSegment segment = new(LiteralRoutePattern.AsMemory(), '/'); segment.MoveNext();)
            {
                ReadOnlyMemory<char> current = segment.Current;

                if (!TryMatchLiteralBranch(branchLookupKind, node, current, out RouteNode nextNode))
                    throw new InvalidOperationException($"Failed to match segment '{current}'.");

                node = nextNode;
            }

            return node;
        }

        [Benchmark]
        [Arguments(BranchLookup.SingleBranch)]
        [Arguments(BranchLookup.LiteralBranches)]
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Dispose() on DelimitedSegment does nothing")]
        public object SearchPercentThenMatchLiteralSegmentsAndWalkGraph(BranchLookup branchLookupKind)
        {
            RouteNode node = _root;

            for (DelimitedSegment segment = new(LiteralRoutePattern.AsMemory(), '/'); segment.MoveNext();)
            {
                ReadOnlyMemory<char> current = segment.Current;

                if (current.Span.IndexOf('%') >= 0)
                    throw new InvalidOperationException($"Unexpected escaped segment '{current}'.");

                if (!TryMatchLiteralBranch(branchLookupKind, node, current, out RouteNode nextNode))
                    throw new InvalidOperationException($"Failed to match segment '{current}'.");

                node = nextNode;
            }

            return node;
        }

        [Benchmark(OperationsPerInvoke = 1000)]
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "DisposeAsync() on RouteMatchCursor does nothing when there was not % encoded segment")]
        public void WalkGraphUsingRouteMatchCursor()
        {
            RouteMatchCursor cursor = new(_root, HttpVerb.Get, s_requestUri, null!, null!, MatchingPrecedence.LiteralFirst, default);

            for (int i = 0; i < 1000; i++)
            {
                ValueTask<bool> result = cursor.TrySingleBranchesAsync();

                if (!result.IsCompletedSuccessfully)
                    throw new InvalidOperationException("Unexpected async processing");

                if (!result.Result)
                    throw new InvalidOperationException("Failed to match segment");

                cursor.Reset();
            }
        }

        private bool TryMatchLiteralBranch(BranchLookup branchLookupKind, RouteNode node, ReadOnlyMemory<char> segment, out RouteNode nextNode)
        {
            switch (branchLookupKind)
            {
                case BranchLookup.SingleBranch:
                    if (node.SingleBranch is KeyValuePair<ReadOnlyMemory<char>, RouteNode> literalBranch && ReadOnlyMemoryCharComparer.Instance.Equals(literalBranch.Key, segment))
                    {
                        nextNode = literalBranch.Value;
                        return true;
                    }

                    nextNode = null!;
                    return false;

                case BranchLookup.LiteralBranches:
                    if (node.LiteralBranches.TryGetValue(segment, out RouteNode? literalChild))
                    {
                        nextNode = literalChild!;
                        return true;
                    }

                    nextNode = null!;
                    return false;

                default:
                    throw new InvalidOperationException($"Unknown branch lookup kind: {branchLookupKind}.");
            }
        }
    }
}
