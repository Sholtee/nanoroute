/********************************************************************************
* Router.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static IEnumerable<HandlerRegistration> FindMatches(RouteNode node, HttpVerb verb, StringSegment? segment, Dictionary<string, object?> paramz)
        {
            if (node.HandlerRegistrations.TryGetValue(verb, out List<HandlerRegistration>? handlerRegistrations))
                foreach (HandlerRegistration handlerRegistration in handlerRegistrations)
                    if (handlerRegistration.IsPrefix || segment?.Value is null)
                        yield return handlerRegistration with { AttachedParameters = paramz };

            if (segment?.Value is null)
                yield break;

            if (node.LiteralChildren.TryGetValue(segment.Value, out RouteNode literalChild))
            {
                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    literalChild,
                    verb,
                    segment.Next,
                    paramz
                );
                foreach (HandlerRegistration match in matches)
                    yield return match;
            }

            string decodedSegment = HttpUtility.UrlDecode(segment.Value);

            foreach (RouteNode parameterizedChild in node.ParameterizedChildren)
            {
                if (!parameterizedChild.ParameterParser!.TryParse(decodedSegment, out object? parsed))
                    continue;

                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    parameterizedChild,
                    verb,
                    segment.Next,
                    parameterizedChild.ParameterParser?.ParameterName is { Length: > 0 } parameterName
                        ? new Dictionary<string, object?>(paramz, StringComparer.OrdinalIgnoreCase) { [parameterName] = parsed }
                        : paramz
                );
                foreach (HandlerRegistration match in matches)
                    yield return match;
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
                cancellation.ThrowIfCancellationRequested();

                if (!matches.MoveNext())
                    throw new HttpException(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
 
                Debug.Assert(matches.Current.AttachedParameters is not null, "Parameters must be attached here");

                RouterEventSource.Log.Info("MatchingHandler", () => new
                {
                    RequestPath = requestPath,
                    Verb = verb,
                    matches.Current.Pattern
                });

                RequestContext requestContext = new()
                {
                    Parameters = matches.Current.AttachedParameters!,
                    Request = request,
                    Services = services,
                    Cancellation = cancellation
                };

                return await matches.Current.Handler(requestContext, CallNextHandler);
            }
        }
    }
}
