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

        private delegate IEnumerable<HandlerRegistration> FindMatchDelegate(RouteNode node, HttpVerb verb, StringSegment segment, Dictionary<string, object?> paramz);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly IReadOnlyList<FindMatchDelegate> _findMatchDelegates = null!;

        private IEnumerable<HandlerRegistration> FindMatches(RouteNode node, HttpVerb verb, StringSegment? segment, Dictionary<string, object?> paramz)
        {
            if (node.HandlerRegistrations.TryGetValue(verb, out List<HandlerRegistration>? handlerRegistrations))
                foreach (HandlerRegistration handlerRegistration in handlerRegistrations)
                    if (handlerRegistration.IsPrefix || segment?.Value is null)
                        yield return handlerRegistration with { AttachedParameters = paramz };

            if (segment?.Value is not null)
                foreach(FindMatchDelegate findMatchesDelegate in _findMatchDelegates)
                    foreach (HandlerRegistration match in findMatchesDelegate(node, verb, segment, paramz))
                        yield return match;
        }

        private IEnumerable<HandlerRegistration> FindLiteralMatches(RouteNode node, HttpVerb verb, StringSegment segment, Dictionary<string, object?> paramz)
        {
            Debug.Assert(segment.Value is not null, "Invalid segment");

            return node.LiteralChildren.TryGetValue(segment.Value!, out RouteNode literalChild)
                ? FindMatches
                (
                    literalChild,
                    verb,
                    segment.Next,
                    paramz
                )
                : Array.Empty<HandlerRegistration>();
        }

        private IEnumerable<HandlerRegistration> FindParameterMatches(RouteNode node, HttpVerb verb, StringSegment segment, Dictionary<string, object?> paramz)
        {
            Debug.Assert(segment.Value is not null, "Invalid segment");

            if (node.ParameterizedChildren.Count is 0)
                yield break;

            string decodedSegment = HttpUtility.UrlDecode(segment.Value);

            foreach (RouteNode parameterizedChild in node.ParameterizedChildren)
            {
                Debug.Assert(parameterizedChild.ParameterParser is not null, "Child node must have parameter parser assigned");

                if (!parameterizedChild.ParameterParser!.TryParse(decodedSegment, out object? parsed))
                    continue;

                Dictionary<string, object?> extended = parameterizedChild.ParameterParser?.ParameterName is { Length: > 0 } parameterName
                    ? new(paramz, StringComparer.OrdinalIgnoreCase)
                    {
                        [parameterName] = parsed
                    }
                    : paramz;

                foreach (HandlerRegistration match in FindMatches(parameterizedChild, verb, segment.Next, extended))
                    yield return match;
            }
        }

        private static RouteNode CopyRoot(RouteBuilder routeBuilder)
        {
            // We have to do it here since the base() constructor invocation runs first
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
                Root,
                verb,
                new StringSegment(requestPath, '/'),
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
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
