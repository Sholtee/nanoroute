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

    internal sealed class RequestPipeline(RouteNode root, MatchingBehavior matchingBehavior, HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation) : IAsyncDisposable
    {
        #region Private
        private readonly RouteMatchCursor _matches = new
        (
            root,
            ParseVerb(request),
            request.RequestUri,
            services,
            matchingBehavior,
            cancellation
        );

        private static HttpVerb ParseVerb(HttpRequestMessage request)
        {
            if (!Enum.TryParse(request.Method.Method, ignoreCase: true, out HttpVerb verb))
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
            HandlerRegistration match = _matches.Current;

            Debug.Assert(match.AttachedParameters is not null, "Parameters must be attached here");

            RouterEventSource.Log.Info("MatchingHandler", () => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method,
                match.Pattern,
                ParameterCount = match.AttachedParameters!.Count
            });

            RequestContext requestContext = new()
            {
                Parameters = match.AttachedParameters!,
                Services = services,
                Request = request,
                Cancellation = cancellation
            };

            return match.Handler(requestContext, CallNextHandler);
        }

        private void ThrowNotFound()
        {
            RouterEventSource.Log.Info("NoMatchingHandler", () => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method
            });

            HttpRequestException.Throw(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
        }
        #endregion

        public ValueTask DisposeAsync() => _matches.DisposeAsync();

        public Task<HttpResponseMessage> RunAsync() => CallNextHandler();
    }
}
