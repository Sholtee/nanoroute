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

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// TODO
    /// </summary>
    public abstract class Router<TRequest, TResponse>: RoutingContext
    {
        #region Private
        private static IEnumerable<HandlerRegistration> FindMatches(RouteNode node, HttpVerb verb, string[] segments, int segmentIndex, Dictionary<string, object?> paramz)
        {
            if (node.HandlerRegistrations.TryGetValue(verb, out List<HandlerRegistration>? handlerRegistrations))
                foreach (HandlerRegistration handlerRegistration in handlerRegistrations)
                    if (handlerRegistration.IsPrefix || segmentIndex == segments.Length)
                        yield return handlerRegistration with { AttachedParameters = paramz };

            if (segmentIndex == segments.Length)
                yield break;

            string segment = segments[segmentIndex];

            if (node.ExactChildren.TryGetValue(segment, out RouteNode exactChild))
            {
                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    exactChild,
                    verb,
                    segments,
                    segmentIndex + 1,
                    paramz
                );
                foreach (HandlerRegistration match in matches)
                    yield return match;
            }

            foreach (RouteNode parameterizedChild in node.ParameterizedChildren)
            {
                if (!parameterizedChild.ParameterParser!.TryParse(segment, out object? parsed))
                    continue;

                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    parameterizedChild,
                    verb,
                    segments,
                    segmentIndex + 1,
                    parameterizedChild.ParameterParser?.ParameterName is { Length: > 0 } parameterName
                        ? new Dictionary<string, object?>(paramz, StringComparer.OrdinalIgnoreCase) { [parameterName] = parsed }
                        : paramz
                );
                foreach (HandlerRegistration match in matches)
                    yield return match;
            }
        }
        #endregion

        #region Protected
        /// <summary>
        /// TODO
        /// </summary>
        protected abstract Task<HttpRequestMessage> GetRequest(TRequest request);

        /// <summary>
        /// TODO
        /// </summary>
        protected abstract Task<TResponse> GetResponse(HttpResponseMessage response);
        #endregion

        /// <summary>
        /// TODO
        /// </summary>
        #if DEBUG
        internal
        #endif
        protected async Task<TResponse> Handle(TRequest request, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(services);

            HttpRequestMessage requestMessage = await GetRequest(request);
            if (!Enum.TryParse(requestMessage.Method.Method, ignoreCase: true, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, requestMessage.Method.Method), nameof(request)
                );

            requestMessage.Properties["OriginalRequest"] = request;

            string requestPath = requestMessage
                .RequestUri
                .AbsolutePath;  // escaped characters are normalized

            RouterEventSource.Log.Info("RequestProcessingStarted", () => new
            {
                RequestPath = requestPath,
                Verb = verb
            });

            using IEnumerator<HandlerRegistration> matches = FindMatches
            (
                Root,
                verb,
                requestPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries),
                0,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            )
            .GetEnumerator();

            return await GetResponse(await CallNextHandler());

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
                   // matches.Current.Pattern
                });

                RequestContext requestContext = new()
                {
                    Parameters = matches.Current.AttachedParameters!,
                    Request = requestMessage,
                    Services = services,
                    Cancellation = cancellation
                };

                return await matches.Current.Handler(requestContext, CallNextHandler);
            }
        }
    }
}
