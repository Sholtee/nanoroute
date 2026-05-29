/********************************************************************************
* RequestPipeline.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    using Properties;

    internal sealed class RequestPipeline(RouteNode root, RouterConfig routerConfig)
    {
        public async Task<HttpResponseMessage> RunAsync(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation)
        {
            if (!HttpVerb.TryParseFast(request.Method.Method, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, request.Method.Method), nameof(request)
                );

            RouteMatchCursor cursor = new
            (
                root,
                verb,
                request.RequestUri,
                services,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                routerConfig.MatchingPrecedence,
                cancellation
            );

            await using (cursor.ConfigureAwait(false))
                return await CallNextHandler().ConfigureAwait(false);

            async Task<HttpResponseMessage> CallNextHandler()
            {
                if (!await cursor.MoveNextAsync().ConfigureAwait(false))
                {
                    RouterEventSource.Info.Write("NoMatchingHandler", static request => new
                    {
                        RequestUri = request.RequestUri.OriginalString,
                        Verb = request.Method.Method
                    }, request);

                    HttpRequestException.Throw(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
                }

                RouterEventSource.Info.Write("MatchingHandler", static (request, cursor) => new
                {
                    RequestUri = request.RequestUri.OriginalString,
                    Verb = request.Method.Method,
                    cursor.Current.Pattern,
                    ParameterCount = cursor.Parameters.Count
                }, request, cursor);

                RequestContext requestContext = new()
                {
                    Parameters = cursor.Parameters,
                    RemainingPath = cursor.RemainingPath,
                    Services = services,
                    Request = request,
                    Cancellation = cancellation
                };

                return await cursor
                    .Current
                    .Handler(requestContext, CallNextHandler)
                    .ConfigureAwait(false);
            }
        }
    }
}
