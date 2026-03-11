/********************************************************************************
* Router.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NanoRoute
{
    using Properties;

    /// <summary>
    /// Routes requests to one or more handlers based on HTTP method and URI path segments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Routes are registered as path patterns. Literal segments are matched case-insensitively, while parameter
    /// segments use parsers registered through <see cref="AddParameterParser(string, ParameterParserDelegate)"/>.
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
    /// Router&lt;HttpRequest, IResult&gt; router = new MyRouter()
    ///     .AddParameterParser("int", (string segment, out object? parsed) =&gt;
    ///     {
    ///         if (int.TryParse(segment, out int id))
    ///         {
    ///             parsed = id;
    ///             return true;
    ///         }
    ///
    ///         parsed = null;
    ///         return false;
    ///     })
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
    /// IResult response = router.Handle(request, services);
    /// </code>
    /// In this example, a request for <c>/api/users/42/details</c> first matches the prefix handler, which parses
    /// and stores <c>user_id</c>, then continues to the more specific handler that returns the response.
    /// </example>
    /// </remarks>
    public abstract class Router<TRequest, TResponse> where TRequest : class
    {
        #region Private
        private enum HttpVerb
        {
            Get,
            Post,
            Put,
            Delete,
            Patch,
            Head,
            Options,
            Trace
        }
        /// <summary>
        /// Stores a named route-segment parser and its optional bound parameter name.
        /// </summary>
        private sealed record ParameterParser(string Name, ParameterParserDelegate TryParse)
        {
            /// <summary>
            /// Gets the request-context parameter name that receives the parsed value.
            /// </summary>
            public string? ParameterName { get; init; }
        }

        /// <summary>
        /// Represents a handler attached to a matched route node.
        /// </summary>
        private sealed record HandlerRegistration(RequestHandler<TRequest, TResponse> Handler, int RegistrationOrder, bool Prefix)
        {
            /// <summary>
            /// Gets the parameter snapshot associated with the current match.
            /// </summary>
            public Dictionary<string, object?>? AttachedParameters { get; init; }
        }

        /// <summary>
        /// Represents a node in the per-verb route tree.
        /// </summary>
        private sealed class RouteNode
        {
            /// <summary>
            /// Gets the handlers registered for the current route node.
            /// </summary>
            public List<HandlerRegistration> HandlerRegistrations { get; } = [];

            /// <summary>
            /// Gets or sets the parser used by this node when it represents a parameter segment.
            /// </summary>
            public ParameterParser? ParameterParser { get; set; }

            /// <summary>
            /// Gets or sets the original route pattern registered for this node.
            /// </summary>
            public string? Pattern { get; set; }

            /// <summary>
            /// Gets literal child nodes keyed by case-insensitive segment value.
            /// </summary>
            public Dictionary<string, RouteNode> ExactChildren { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets parameter-based child nodes evaluated after literal matches.
            /// </summary>
            public List<RouteNode> ParameterizedChildren { get; } = [];
        }

        // avoid using the constructor that accepts RegexOptions, since it is not compatible with AOT
        private static readonly Regex s_matcherDefinition = new("\\{(?:(?<parametername>\\w+):)?(?<name>\\w+)\\}");

        // dict is faster against value types -> use HttpVerb instead of string
        private readonly Dictionary<HttpVerb, RouteNode> _root = typeof(HttpVerb)
            .GetEnumNames()
            .Select
            (
                static s =>
                {
                    Enum.TryParse(s, out HttpVerb result);
                    return result;
                }
            )
            .ToDictionary
            (
                static v => v,
                static _ => new RouteNode()
            );

        private readonly Dictionary<string, ParameterParser> _parameterParsers = [];

        private int _handlerCount;

        private static IEnumerable<HandlerRegistration> FindMatches(RouteNode node, string[] segments, int segmentIndex, Dictionary<string, object?> paramz, ILogger? logger)
        {
            if (node.HandlerRegistrations.Count > 0)
                foreach (HandlerRegistration handlerRegistration in node.HandlerRegistrations)
                    if (handlerRegistration.Prefix || segmentIndex == segments.Length)
                    {
                        logger?.LogDebug(Resources.DBG_COMPATIBLE_HANDLER_FOUND, node.Pattern);
                        yield return handlerRegistration with { AttachedParameters = paramz };
                    }

            if (segmentIndex == segments.Length)
                yield break;

            string segment = segments[segmentIndex];

            if (node.ExactChildren.TryGetValue(segment, out RouteNode exactChild))
            {
                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    exactChild,
                    segments,
                    segmentIndex + 1,
                    paramz,
                    logger
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
                    segments,
                    segmentIndex + 1,
                    parameterizedChild.ParameterParser?.ParameterName is { Length: > 0 } parameterName
                        ? new Dictionary<string, object?>(paramz, StringComparer.OrdinalIgnoreCase) { [parameterName] = parsed }
                        : paramz,
                    logger
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
        /// The URI whose <see cref="Uri.AbsolutePath"/> is split into path segments and matched against the
        /// registered route patterns.
        /// </returns>
        /// <remarks>
        /// Query string and fragment values are ignored by the router. Only the absolute path participates in
        /// matching.
        /// </remarks>
        protected abstract Uri GetUri(TRequest request);

        /// <summary>
        /// Extracts a request identifier for logging scopes.
        /// </summary>
        /// <param name="request">The incoming request instance.</param>
        /// <returns>A value that uniquely identifies the request in logs.</returns>
        /// <remarks>
        /// This value is not used for matching. It is attached only to the logger scope created while
        /// <see cref="Handle(TRequest, IServiceProvider)"/> processes the request.
        /// </remarks>
        protected abstract string GetRequestId(TRequest request);

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
        #endregion

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed parameter value.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current router instance.</returns>
        public Router<TRequest, TResponse> AddParameterParser(string parserName, ParameterParserDelegate tryParseDelegate)
        {
            Ensure.NotNull(parserName);
            Ensure.NotNull(tryParseDelegate);

            _parameterParsers[parserName] = new ParameterParser(parserName, tryParseDelegate);

            return this;
        }

        /// <summary>
        /// Registers a handler for all supported HTTP methods.
        /// </summary>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, and a trailing <c>/</c> turns the
        /// pattern into a prefix match. Without a trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">The handler to execute when the pattern matches.</param>
        /// <returns>The current router instance.</returns>
        /// <example>
        /// <code>
        /// router.AddHandler("/health", (context, next) =&gt; Results.Ok());
        /// </code>
        /// </example>
        public Router<TRequest, TResponse> AddHandler(string pattern, RequestHandler<TRequest, TResponse> handler)
        {
            Ensure.NotNull(pattern);
            Ensure.NotNull(handler);

            return AddHandler
            (
                Enum.GetNames
                (
                    typeof(HttpVerb)
                ),
                pattern,
                handler
            );
        }

        /// <summary>
        /// Registers the same handler for multiple HTTP methods.
        /// </summary>
        /// <param name="verbs">The HTTP methods that should use the handler.</param>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, and a trailing <c>/</c> turns the
        /// pattern into a prefix match. Without a trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">The handler to execute when the route matches.</param>
        /// <returns>The current router instance.</returns>
        /// <example>
        /// <code>
        /// router.AddHandler(
        ///     ["GET", "POST"],
        ///     "/api/items/{id:int}",
        ///     (context, next) =&gt; Results.Ok(context.Parameters["id"]));
        /// </code>
        /// </example>
        public Router<TRequest, TResponse> AddHandler(IEnumerable<string> verbs, string pattern, RequestHandler<TRequest, TResponse> handler)
        {
            Ensure.NotNull(verbs);
            Ensure.NotNull(pattern);
            Ensure.NotNull(handler);

            foreach (string verb in verbs)
                AddHandler(verb, pattern, handler);

            return this;
        }

        /// <summary>
        /// Registers a handler for a single HTTP method.
        /// </summary>
        /// <param name="verb">The HTTP method that activates the handler.</param>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, and a trailing <c>/</c> turns the
        /// pattern into a prefix match. Without a trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">
        /// The handler to execute. If several handlers match, calling the supplied <c>next</c> delegate continues
        /// the pipeline with the next compatible handler.
        /// </param>
        /// <returns>The current router instance.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not a supported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the pattern references a parameter parser that has not been registered yet.
        /// </exception>
        /// <example>
        /// <code>
        /// router.AddHandler("GET", "/files/{path:any}/", (context, next) =&gt;
        /// {
        ///     string path = (string) context.Parameters["path"]!;
        ///     return ServeFile(path);
        /// });
        /// </code>
        /// </example>
        public Router<TRequest, TResponse> AddHandler(string verb, string pattern, RequestHandler<TRequest, TResponse> handler)
        {
            Ensure.NotNull(verb);
            Ensure.NotNull(pattern);
            Ensure.NotNull(handler);

            if (!Enum.TryParse(verb, ignoreCase: true, out HttpVerb v))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, verb), nameof(verb)
                );

            RouteNode target = _root[v];

            foreach (string segment in pattern.Split(['/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (s_matcherDefinition.Match(segment) is { Success: true } parserDefinition)
                {
                    string
                        parserName = parserDefinition.Groups["name"].Value,  // cannot be empty
                        parameterName = parserDefinition.Groups["parametername"].Value;

                    Debug.Assert(!string.IsNullOrEmpty(parserName), "Parser name could not be extracted");

                    if (!_parameterParsers.TryGetValue(parserName, out ParameterParser parser))
                        throw new InvalidOperationException
                        (
                            string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARAMETER_PARSER, parserName)
                        );

                    if (target.ParameterizedChildren.SingleOrDefault(cc => cc.ParameterParser!.Name.Equals(parser.Name, StringComparison.OrdinalIgnoreCase)) is not { } parameterizedChild)
                    {
                        if (!string.IsNullOrEmpty(parserName))
                            parser = parser with { ParameterName = parameterName };

                        parameterizedChild = new RouteNode
                        {
                            ParameterParser = parser
                        };
                        target.ParameterizedChildren.Add(parameterizedChild);
                    }
                    else if (parameterizedChild.ParameterParser!.ParameterName?.Equals(parameterName) is false)
                        throw new InvalidOperationException(Resources.ERR_PARAMETER_OVERRIDE);

                    target = parameterizedChild;
                }
                else
                {
                    if (!target.ExactChildren.TryGetValue(segment, out RouteNode exactChild))
                    {
                        exactChild = new();
                        target.ExactChildren.Add(segment, exactChild);
                    }

                    target = exactChild;
                }
            }

            Debug.Assert(target.Pattern is null || target.Pattern == pattern, "Invalid handler setup");

            target.HandlerRegistrations.Add
            (
                new HandlerRegistration(handler, _handlerCount++, pattern[^1] is '/')
            );
            target.Pattern = pattern;

            return this;
        }

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
        /// IResult result = router.Handle(request, services);
        /// </code>
        /// </example>
        public TResponse Handle(TRequest request, IServiceProvider services)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            ILogger? requestLogger = services.GetService<ILoggerFactory>()?.CreateLogger
            (
                GetType().Name
            );

            string requestVerb = GetVerb(request);
            if (!Enum.TryParse<HttpVerb>(requestVerb, ignoreCase: true, out HttpVerb verb))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, requestVerb), nameof(request)
                );

            string requestPath = GetUri(request)
                .AbsolutePath;  // escaped characters are normalized

            using IDisposable? scope = requestLogger?.BeginScope
            (
                new Dictionary<string, string>
                {
                    ["AbsolutePath"] = requestPath,
                    ["RequestId"] = GetRequestId(request) 
                }
            );

            requestLogger?.LogDebug(Resources.DBG_REQUEST_PROCESSING_STARTED);

            using IEnumerator<HandlerRegistration> matches = FindMatches
            (
                _root[verb],
                requestPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries),
                0,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                requestLogger
            )
            .GetEnumerator();

            return CallNextHandler();

            TResponse CallNextHandler()
            {
                if (!matches.MoveNext())
                {
                    requestLogger?.LogDebug(Resources.DBG_UNPROCESSED_REQUEST);
                    throw new InvalidOperationException(Resources.ERR_NOT_FOUND);
                }

                Debug.Assert(matches.Current.AttachedParameters is not null, "Parameters must be attached here");

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
