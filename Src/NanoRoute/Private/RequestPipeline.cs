/********************************************************************************
* RequestPipeline.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    using Properties;

    internal sealed class RequestPipeline(RouteNode root, RouterConfig routerConfig, HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation) : IAsyncDisposable
    {
        #region Private
        private readonly RouteMatchCursor _matches = new
        (
            root,
            ParseVerb(request),
            request.RequestUri,
            services,
            routerConfig,
            cancellation
        );

        private static HttpVerb ParseVerb(HttpRequestMessage request)
        {
            if (!HttpVerb.TryParseFast(request.Method.Method, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, request.Method.Method), nameof(request)
                );

            return verb;
        }

        private Task<HttpResponseMessage> CallNextHandler()
        {
            ValueTask<bool> matched = _matches.MoveNextAsync();

            if (!matched.IsCompletedSuccessfully)
                return CallNextHandlerAwaitedAsync(matched);

            if (!matched.Result)
                ThrowNotFound();

            return InvokeCurrentHandler();
        }

        private async Task<HttpResponseMessage> CallNextHandlerAwaitedAsync(ValueTask<bool> matched)
        {
            if (!await matched)
                ThrowNotFound();

            return await InvokeCurrentHandler();
        }

        private Task<HttpResponseMessage> InvokeCurrentHandler()
        {
            RouteMatch match = _matches.Current;

            RouterEventSource.Info.Write("MatchingHandler", static (request, match) => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method,
                Pattern = match.HandlerRegistration.Pattern,
                ParameterCount = match.AttachedParameters.Count
            }, request, match);

            RequestContext requestContext = new()
            {
                Parameters = match.AttachedParameters,
                Services = services,
                Request = request,
                Cancellation = cancellation
            };

            return match.HandlerRegistration.Handler(requestContext, CallNextHandler);
        }

        private void ThrowNotFound()
        {
            RouterEventSource.Info.Write("NoMatchingHandler", static request => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method
            }, request);

            HttpRequestException.Throw(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
        }
        #endregion

        public ValueTask DisposeAsync() => _matches.DisposeAsync();

        public Task<HttpResponseMessage> RunAsync() => CallNextHandler();
    }
}
