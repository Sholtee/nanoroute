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
    /// TODO
    /// </summary>
    public abstract class Router: RoutingContext
    {
        /// <summary>
        /// 
        /// </summary>
        public const string TRACE_ID_NAME = "TraceId";

        /// <summary>
        /// 
        /// </summary>
        public const string ORIGINAL_REQUEST_NAME = "OriginalRequest";

        private sealed record MatchingContext(RouteNode Node, HttpVerb Verb, StringSegment? Segment, Dictionary<string, object?> Parameters);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly IReadOnlyList<Func<MatchingContext, IEnumerable<HandlerRegistration>>> _findMatchDelegates = null!;

        private IEnumerable<HandlerRegistration> FindMatches(MatchingContext context)
        {
            (RouteNode Node, HttpVerb Verb, StringSegment? Segment, Dictionary<string, object?> Parameters) = context;

            if (Node.HandlerRegistrations.TryGetValue(Verb, out List<HandlerRegistration>? handlerRegistrations))
                foreach (HandlerRegistration handlerRegistration in handlerRegistrations)
                {
                    if (Segment?.Value is not null && !handlerRegistration.IsPrefix)
                        continue;

                    yield return handlerRegistration with { AttachedParameters = Parameters };
                }

            if (Segment?.Value is null)
                yield break;

            foreach(Func<MatchingContext, IEnumerable<HandlerRegistration>> findMatchesDelegate in _findMatchDelegates)
                foreach (HandlerRegistration match in findMatchesDelegate(context))
                    yield return match;
        }

        private IEnumerable<HandlerRegistration> FindLiteralMatches(MatchingContext context)
        {
            (RouteNode Node, _, StringSegment? Segment, _) = context;

            Debug.Assert(Segment?.Value is not null, "Invalid segment");

            if (!Node.LiteralChildren.TryGetValue(Segment!.Value!, out RouteNode literalChild))
                yield break;

            foreach (HandlerRegistration match in FindMatches(context with { Node = literalChild, Segment = Segment.Next }))
                yield return match;
        }

        private IEnumerable<HandlerRegistration> FindParameterMatches(MatchingContext context)
        {
            (RouteNode Node, _, StringSegment? Segment, Dictionary<string, object?> Parameters) = context;

            Debug.Assert(Segment?.Value is not null, "Invalid segment");

            if (Node.ParameterizedChildren.Count is 0)
                yield break;

            string decodedSegment = HttpUtility.UrlDecode(Segment!.Value!);

            foreach (RouteNode parameterizedChild in Node.ParameterizedChildren)
            {
                Debug.Assert(parameterizedChild.ParameterParser is not null, "Child node must have parameter parser assigned");

                if (!parameterizedChild.ParameterParser!.TryParse(decodedSegment, out object? parsed))
                    continue;

                Dictionary<string, object?> extended = parameterizedChild.ParameterParser?.ParameterName is { Length: > 0 } parameterName
                    ? new(Parameters, StringComparer.OrdinalIgnoreCase)
                    {
                        [parameterName] = parsed
                    }
                    : Parameters;

                foreach (HandlerRegistration match in FindMatches(context with { Node = parameterizedChild, Segment = Segment.Next, Parameters = extended }))
                    yield return match;
            }
        }

        private static RouteNode CopyRoot(RouteBuilder routeBuilder)
        {
            // The base() ctor invocation runs first so we have to do the validation here
            Ensure.NotNull(routeBuilder);
            return routeBuilder.Root.Copy();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routeBuilder"></param>
        /// <param name="config"></param>
        [SuppressMessage("ApiDesign", "RS0022:Constructor make noninheritable base class inheritable")]
        protected Router(RouteBuilder routeBuilder, RouterConfig config): base(CopyRoot(routeBuilder))
        {
            //Ensure.NotNull(routeBuilder);
            Ensure.NotNull(config);

            MatchingBehavior = config.MatchingBehavior;
        }

        /// <summary>
        /// 
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
        /// TODO
        /// </summary>
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
                .AbsolutePath;

            RouterEventSource.Log.Info("RequestProcessingStarted", () => new
            {
                RequestPath = requestPath,
                Verb = verb
            });

            using IEnumerator<HandlerRegistration> matches = FindMatches
            (
                new MatchingContext
                (
                    Root,
                    verb,
                    new StringSegment(requestPath, '/'),
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                )
                
            )
            .GetEnumerator();

            return await CallNextHandler();

            async Task<HttpResponseMessage> CallNextHandler()
            {
                if (!matches.MoveNext())
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

                RequestContext requestContext = new()
                {
                    Parameters = match.AttachedParameters!,
                    Request = request,
                    Services = services,
                    Cancellation = cancellation
                };

                return await match.Handler(requestContext, CallNextHandler);
            }
        }
    }
}
