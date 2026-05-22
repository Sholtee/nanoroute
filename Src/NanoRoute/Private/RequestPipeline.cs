/********************************************************************************
* RequestPipeline.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.Internals
{
    using Properties;

    internal sealed class RequestPipeline : RouteMatchCursor
    {
        #region Private
        private readonly HttpRequestMessage _request;

        private readonly CallNextHandlerDelegate _callNextHandler;

        private Task<HttpResponseMessage> CallNextHandler()
        {
            ValueTask<bool> matched = MoveNextAsync();

            if (!matched.IsCompletedSuccessfully)
                return CallNextHandlerAwaitedAsync(matched);

            if (!matched.Result)
                ThrowNotFound();

            return InvokeCurrentHandler();
        }

        private async Task<HttpResponseMessage> CallNextHandlerAwaitedAsync(ValueTask<bool> matched)
        {
            if (!await matched.ConfigureAwait(false))
                ThrowNotFound();

            return await InvokeCurrentHandler().ConfigureAwait(false);
        }

        private Task<HttpResponseMessage> InvokeCurrentHandler()
        {
            RouteMatch match = Current;

            RouterEventSource.Info.Write("MatchingHandler", static (request, match) => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method,
                Pattern = match.HandlerRegistration.Pattern,
                ParameterCount = match.AttachedParameters.Count
            }, _request, match);

            RequestContext requestContext = new()
            {
                Parameters = match.AttachedParameters,
                RemainingPath = match.RemainingPath,
                Services = Services,
                Request = _request,
                Cancellation = Cancellation
            };

            return match.HandlerRegistration.Handler(requestContext, _callNextHandler);
        }

        private void ThrowNotFound()
        {
            RouterEventSource.Info.Write("NoMatchingHandler", static request => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method
            }, _request);

            HttpRequestException.Throw(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
        }

        private static HttpVerb ParseVerb(string verb)
        {
            if (!HttpVerb.TryParseFast(verb, out HttpVerb parsed))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, verb), nameof(verb)
                );
            return parsed;
        }
        #endregion

        public RequestPipeline(RouteNode root, HttpRequestMessage request, IServiceProvider services, RouterConfig routerConfig, CancellationToken cancellation) : base
        (
            root,
            ParseVerb(request.Method.Method),
            request.RequestUri,
            services,
            routerConfig,
            cancellation
        )
        {
            // Reusing one closed delegate avoids repeated instance method group
            // conversions, which cost both allocation and time on the handler path.
            _callNextHandler = CallNextHandler;
            _request = request;
        }

        public Task<HttpResponseMessage> RunAsync()
        {
            Debug.Assert(!Completed, $"{nameof(RunAsync)} can be called only once.");

            return CallNextHandler();
        }
    }
}
