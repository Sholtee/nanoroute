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

    internal sealed class RequestPipeline : IAsyncDisposable
    {
        #region Private
        private readonly RouteMatchCursor _matches;

        private readonly HttpRequestMessage _request;

        private readonly IServiceProvider _services;

        private readonly CancellationToken _cancellation;

        private readonly CallNextHandlerDelegate _callNextHandler;

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
            }, _request, match);

            RequestContext requestContext = new()
            {
                Parameters = match.AttachedParameters,
                Services = _services,
                Request = _request,
                Cancellation = _cancellation
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
        #endregion

        public RequestPipeline(RouteNode root, RouterConfig routerConfig, HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation)
        {
            if (!HttpVerb.TryParseFast(request.Method.Method, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, request.Method.Method), nameof(request)
                );

            _request = request;
            _services = services;
            _cancellation = cancellation;

            _matches = new
            (
                root,
                verb,
                request.RequestUri,
                services,
                routerConfig,
                cancellation
            );

            // Reusing one closed delegate avoids repeated instance method group
            // conversions, which cost both allocation and time on the handler path.
            _callNextHandler = CallNextHandler;
        }

        public ValueTask DisposeAsync() => _matches.DisposeAsync();

        public Task<HttpResponseMessage> RunAsync() => CallNextHandler();
    }
}
