/********************************************************************************
* Router.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Executes the route matching pipeline built by <see cref="RouteBuilder"/>.
    /// </summary>
    /// <remarks>
    /// A router is created from a builder snapshot. Matching walks the configured route tree, attaches bound parameters, and invokes compatible
    /// handlers in order until one returns a response without delegating further.
    /// </remarks>
    public abstract class Router: RoutingContext
    {
        /// <summary>
        /// The request property key that stores the trace identifier associated with the current request.
        /// </summary>
        public const string TRACE_ID_NAME = "TraceId";

        /// <summary>
        /// The request property key that stores the original transport-specific request object.
        /// </summary>
        public const string ORIGINAL_REQUEST_NAME = "OriginalRequest";

        private readonly record struct MatchingContext
        (
            RouteNode Node,
            HttpVerb Verb,
            UriSegment? Segment,
            IServiceProvider Services,
            Dictionary<string, object?> Parameters,
            CancellationToken Cancellation
        );

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly IReadOnlyList<Func<MatchingContext, IAsyncEnumerable<HandlerRegistration>>> _findMatchDelegates = null!;

        private async IAsyncEnumerable<HandlerRegistration> FindMatches(MatchingContext matchingContext)
        {
            (RouteNode Node, HttpVerb Verb, UriSegment? Segment, _, Dictionary<string, object?> Parameters, _) = matchingContext;

            if (Node.HandlerRegistrations.TryGetValue(Verb, out List<HandlerRegistration>? handlerRegistrations))
                foreach (HandlerRegistration handlerRegistration in handlerRegistrations)
                {
                    if (Segment?.Value is not null && !handlerRegistration.IsPrefix)
                        continue;

                    yield return handlerRegistration with { AttachedParameters = Parameters };
                }

            if (Segment?.Value is null)
                yield break;

            foreach (Func<MatchingContext, IAsyncEnumerable<HandlerRegistration>> findMatchesDelegate in _findMatchDelegates)
                await foreach (HandlerRegistration match in findMatchesDelegate(matchingContext))
                    yield return match;
        }

        private async IAsyncEnumerable<HandlerRegistration> FindLiteralMatches(MatchingContext matchingContext)
        {
            (RouteNode Node, _, UriSegment? Segment, _, _, _) = matchingContext;

            Debug.Assert(Segment?.Value is not null, "Invalid segment");

            if (!Node.LiteralChildren.TryGetValue(Segment?.Value!, out RouteNode literalChild))
                yield break;

            await foreach (HandlerRegistration match in FindMatches(matchingContext with { Node = literalChild, Segment = Segment!.Next }))
                yield return match;
        }

        private async IAsyncEnumerable<HandlerRegistration> FindParameterMatches(MatchingContext matchingContext)
        {
            (RouteNode Node, _, UriSegment? Segment, IServiceProvider Services, Dictionary<string, object?> Parameters, CancellationToken Cancellation) = matchingContext;

            Debug.Assert(Segment?.Value is not null, "Invalid segment");

            if (Node.ParsedChildren.Count is 0)
                yield break;

            SegmentParserContext parserContext = new
            (
                HttpUtility.UrlDecode(Segment?.Value!),
                Services,
                null,
                Cancellation
            );

            foreach (RouteNode parsedChild in Node.ParsedChildren)
            {
                Debug.Assert(parsedChild.SegmentParser is not null, "Child node must have segment parser assigned");

                if (await parsedChild.SegmentParser!.Parse(parserContext with { Arguments = parsedChild.SegmentParser.Arguments }) is not { Success: true } parsed)
                    continue;

                Dictionary<string, object?> extended = parsedChild.SegmentParser?.ParameterName is { Length: > 0 } parameterName
                    ? new(Parameters, StringComparer.OrdinalIgnoreCase)
                    {
                        [parameterName] = parsed.Parsed
                    }
                    : Parameters;

                await foreach (HandlerRegistration match in FindMatches(matchingContext with { Node = parsedChild, Segment = Segment!.Next, Parameters = extended }))
                    yield return match;
            }
        }

        private static RouteNode CopyRoot(RouteBuilder routeBuilder)
        {
            // The base() ctor invocation runs first so we have to do the validation here
            Ensure.NotNull(routeBuilder);
            return routeBuilder.GetRoot();
        }

        /// <summary>
        /// Initializes a router from a route builder snapshot and router configuration.
        /// </summary>
        /// <param name="routeBuilder">The builder whose current route tree should be frozen into this router.</param>
        /// <param name="config">The router configuration that controls runtime behavior.</param>
        [SuppressMessage("ApiDesign", "RS0022:Constructor make noninheritable base class inheritable")]
        protected Router(RouteBuilder routeBuilder, RouterConfig config): base(CopyRoot(routeBuilder))
        {
            //Ensure.NotNull(routeBuilder);
            Ensure.NotNull(config);

            MatchingBehavior = config.MatchingBehavior;
            Timeout = config.Timeout;
        }

        /// <summary>
        /// Gets the configured precedence between literal and parameterized child segments.
        /// </summary>
        public MatchingBehavior MatchingBehavior
        {
            get;
            private init
            {
                _findMatchDelegates = value switch
                {
                    MatchingBehavior.LiteralFirst => [FindLiteralMatches, FindParameterMatches],
                    MatchingBehavior.ParameterizedChildrenFirst => [FindParameterMatches, FindLiteralMatches],
                    _ => throw new ArgumentOutOfRangeException(nameof(value))
                };
                field = value;
            }
        }

        /// <summary>
        /// Gets the maximum time the router allows a request to remain in the matching and handler pipeline.
        /// </summary>
        /// <remarks>
        /// The effective pipeline token is canceled when either the caller-supplied cancellation token is canceled
        /// or this timeout elapses.
        /// </remarks>
        public TimeSpan Timeout { get; private init; }

        /// <summary>
        /// Routes an <see cref="HttpRequestMessage"/> through the configured handler pipeline.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="services">The service provider exposed to segment parsers and handlers.</param>
        /// <param name="cancellation">A token that can cancel request processing.</param>
        /// <returns>The <see cref="HttpResponseMessage"/> produced by the matching handlers.</returns>
        /// <remarks>
        /// Prefix routes can participate in the same pipeline as exact routes. When several handlers match, NanoRoute
        /// evaluates compatible matches from shorter prefixes toward more specific matches and honors
        /// <see cref="MatchingBehavior"/> when both literal and parameterized segments are available at the same depth.
        /// </remarks>
        /// <exception cref="HttpRequestException">Thrown when no handler matches the request path.</exception>
        /// <exception cref="ArgumentException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels the <paramref name="cancellation"/> or when the configured <see cref="Timeout"/>
        /// elapses.
        /// </exception>
        #if DEBUG
        internal
        #endif
        protected async Task<HttpResponseMessage> Handle(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            if (!Enum.TryParse(request.Method.Method, ignoreCase: true, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, request.Method.Method), nameof(request)
                );

            string requestPath = request
                .RequestUri
                .AbsolutePath;  // escaped path, not percent decoded

            RouterEventSource.Log.Info("RequestProcessingStarted", () => new
            {
                RequestPath = requestPath,
                Verb = verb
            });

            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            linked.CancelAfter(Timeout);
            cancellation = linked.Token;

            await using IAsyncEnumerator<HandlerRegistration> matches = FindMatches
            (
                new MatchingContext
                (
                    _root,
                    verb,
                    new UriSegment(requestPath),
                    services,
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                    cancellation
                )
            )
            .GetAsyncEnumerator();

            return await CallNextHandler();

            async Task<HttpResponseMessage> CallNextHandler()
            {
                cancellation.ThrowIfCancellationRequested();

                if (!await matches.MoveNextAsync())
                {
                    RouterEventSource.Log.Info("NoMatchingHandler", () => new
                    {
                        RequestPath = requestPath,
                        Verb = verb
                    });

                    HttpRequestException.Throw(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
                }

                HandlerRegistration match = matches.Current;

                Debug.Assert(match.AttachedParameters is not null, "Parameters must be attached here");

                RouterEventSource.Log.Info("MatchingHandler", () => new
                {
                    RequestPath = requestPath,
                    Verb = verb,
                    match.Pattern,
                    ParameterCount = match.AttachedParameters!.Count
                });

                RequestContext requestContext = new
                (
                    match.AttachedParameters!,
                    services,
                    request,
                    cancellation
                );

                return await match.Handler(requestContext, CallNextHandler);
            }
        }
    }
}