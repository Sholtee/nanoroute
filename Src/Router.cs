/********************************************************************************
* Router.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace NanoRoute
{
    using Properties;

    /// <summary>
    /// Routes requests to one or more handlers based on HTTP method and URI path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Routes are registered as path patterns. Literal segments are matched case-insensitively, while parameter
    /// segments use parsers registered through <see cref="RouterBuilder{TRequest, TResponse}.AddParameterParser(string, ParameterParserDelegate)"/>.
    /// A pattern ending with <c>/</c> acts as a prefix route, so it also matches longer paths that start with the
    /// same segments. A pattern without a trailing slash is treated as an exact match.
    /// </para>
    /// <para>
    /// When multiple handlers match the same request, they are invoked as a pipeline. Each handler receives a
    /// <see cref="RequestContext{TRequest}"/> and a <c>next</c> delegate. Calling <c>next()</c> transfers control
    /// to the next compatible handler; returning directly short-circuits the pipeline.
    /// </para>
    /// <para>
    /// When multiple handlers match the same request, the router evaluates the shortest compatible prefix first, then
    /// continues toward more specific matches. At the same path depth, literal segment matches are preferred over
    /// parameter matches. This allows broader prefix handlers to populate
    /// <see cref="RequestContext{TRequest}.Parameters"/> before more specific handlers continue the pipeline.
    /// </para>
    /// <example>
    /// <code>
    /// sealed class MyRouter : Router&lt;HttpRequest, IResult&gt;
    /// {
    ///     public IResult Route(HttpRequest request, IServiceProvider services) =&gt; Handle(request, services);
    ///
    ///     protected override Uri GetUri(HttpRequest request) =&gt; request.Url;
    ///     protected override string GetVerb(HttpRequest request) =&gt; request.Method;
    ///     protected override IResult CreateJsonResponse(HttpStatusCode statusCode, string content) =&gt;
    ///         Results.Text(content, "application/json", statusCode: (int) statusCode);
    /// }
    ///
    /// MyRouter router = new MyRouter();
    ///
    /// router
    ///     .AddDefaultParsers()
    ///     .AddHandler("GET", "/api/users/{user_id:int}/", (context, next) =&gt;
    ///     {
    ///         object user = LoadUser((int) context.Parameters["user_id"]!);
    ///         context.Parameters["User"] = user;
    ///         return next();
    ///     })
    ///     .AddHandler("GET", "/api/users/{user_id:int}/details", (context, next) =&gt;
    ///     {
    ///         return Results.Ok(context.Parameters["User"]);
    ///     });
    ///
    /// IResult response = router.Route(request, services);
    /// </code>
    /// In this example, a request for <c>/api/users/42/details</c> first matches the prefix handler, which parses
    /// and stores <c>user_id</c>, then continues to the more specific handler that returns the response.
    /// </example>
    /// </remarks>
    public abstract class Router<TRequest, TResponse>: RouterBuilder<TRequest, TResponse> where TRequest : class
    {
        #region Private
        private static IEnumerable<HandlerRegistration> FindMatches(RouteNode node, HttpVerb verb, string[] segments, int segmentIndex, Dictionary<string, object?> paramz)
        {
            if (node.HandlerRegistrations.TryGetValue(verb, out List<HandlerRegistration>? handlerRegistrations))
                foreach (HandlerRegistration handlerRegistration in handlerRegistrations)
                    if (handlerRegistration.Prefix || segmentIndex == segments.Length)
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
        /// Extracts the request URI used during route matching.
        /// </summary>
        /// <param name="request">The incoming request instance.</param>
        /// <returns>
        /// The URI whose <see cref="Uri.AbsolutePath"/> is matched against the registered route patterns.
        /// </returns>
        /// <remarks>
        /// Query string and fragment values are ignored by the router. Only the absolute path participates in
        /// matching.
        /// </remarks>
        protected abstract Uri GetUri(TRequest request);

        /// <summary>
        /// Extracts the HTTP method that selects the root route table.
        /// </summary>
        /// <param name="request">The incoming request instance.</param>
        /// <returns>
        /// The HTTP method string whose registered handlers should be considered for the request, for example
        /// <c>GET</c>, <c>POST</c>, or <c>DELETE</c>.
        /// </returns>
        /// <remarks>
        /// Method matching is case-insensitive. A path match is ignored when it was registered for a different method.
        /// </remarks>
        protected abstract string GetVerb(TRequest request);

        /// <summary>
        /// Creates a JSON response used by the built-in fallback handlers.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to apply to the generated response.</param>
        /// <param name="content">The serialized JSON payload to return.</param>
        /// <returns>The application-specific response object.</returns>
        /// <remarks>
        /// <see cref="AddDefaultHandler(bool)"/> uses this factory to produce the default <c>404 Not Found</c>
        /// and <c>500 Internal Server Error</c> responses.
        /// </remarks>
        protected abstract TResponse CreateJsonResponse(HttpStatusCode statusCode, string content);
        #endregion

        /// <summary>
        /// Resolves the registered handlers for the request and executes the matching pipeline.
        /// </summary>
        /// <param name="request">The request to route.</param>
        /// <param name="services">The service provider exposed through the created <see cref="RequestContext{TRequest}"/>.</param>
        /// <returns>The response returned by the first handler that completes the pipeline.</returns>
        /// <exception cref="ArgumentException">Thrown when the request exposes an unsupported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no handler matches the request.</exception>
        /// <example>
        /// <code>
        /// sealed class MyRouter : Router&lt;HttpRequest, IResult&gt;
        /// {
        ///     public IResult Route(HttpRequest request, IServiceProvider services) =&gt; Handle(request, services);
        ///
        ///     protected override Uri GetUri(HttpRequest request) =&gt; request.Url;
        ///     protected override string GetVerb(HttpRequest request) =&gt; request.Method;
        ///     protected override IResult CreateJsonResponse(HttpStatusCode statusCode, string content) =&gt;
        ///         Results.Text(content, "application/json", statusCode: (int) statusCode);
        /// }
        ///
        /// IResult result = new MyRouter().Route(request, services);
        /// </code>
        /// </example>
        #if DEBUG
        internal
        #endif
        protected TResponse Handle(TRequest request, IServiceProvider services)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            string requestVerb = GetVerb(request);
            if (!Enum.TryParse(requestVerb, ignoreCase: true, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, requestVerb), nameof(request)
                );

            string requestPath = GetUri(request)
                .AbsolutePath;  // escaped characters are normalized

            RouterEventSource.Log.Info("RequestProcessingStarted", () => new
            {
                RequestPath = requestPath,
                Verb = verb
            });

            using IEnumerator<HandlerRegistration> matches = FindMatches
            (
                _root,
                verb,
                requestPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries),
                0,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            )
            .GetEnumerator();

            return CallNextHandler();

            TResponse CallNextHandler()
            {
                if (!matches.MoveNext())
                    throw new HttpException(HttpStatusCode.NotFound, Resources.ERR_NOT_FOUND);
 
                Debug.Assert(matches.Current.AttachedParameters is not null, "Parameters must be attached here");

                RouterEventSource.Log.Info("MatchingHandler", () => new
                {
                    RequestPath = requestPath,
                    Verb = verb,
                    matches.Current.Node.Pattern
                });

                RequestContext<TRequest> requestContext = new()
                {
                    Parameters = matches.Current.AttachedParameters!,
                    Request = request,
                    Services = services
                };

                return matches.Current.Handler(requestContext, CallNextHandler);
            }
        }
    }
}
