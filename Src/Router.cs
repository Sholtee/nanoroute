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
    /// Minimalistic router implementation
    /// </summary>
    public abstract class Router<TRequest, TResponse>(MatchingStrategy matchingStrategy)
    {
        #region Private
        private sealed record ParameterParser(string Name, ParameterParserDelegate TryParse)
        {
            /// <summary>
            /// The parameter to which the matched output is bound.
            /// </summary>
            public string? ParameterName { get; init; }
        }

        private sealed record HandlerRegistration(RequestHandler<TRequest, TResponse> Handler, int RegistrationOrder, bool Prefix)
        {
            public Dictionary<string, object?>? AttachedParameters { get; init; }
        }

        private sealed class RouteSegmentProcessing
        {
            public List<HandlerRegistration> HandlerRegistrations { get; } = [];

            /// <summary>
            /// Not null in case of conditional continuation.
            /// </summary>
            public ParameterParser? ParameterParser { get; set; }

            /// <summary>
            /// Set when <see cref="HandlerRegistrations"/> is not empty.
            /// </summary>
            public string? Pattern { get; set; }

            public Dictionary<string, RouteSegmentProcessing> ExactContinuation { get; } = new(StringComparer.OrdinalIgnoreCase);

            public List<RouteSegmentProcessing> ConditionalContinuation { get; } = [];
        }

        private static readonly Regex s_matcherDefinition = new("\\{(?:(?<parametername>\\w+):)?(?<name>\\w+)\\}", RegexOptions.Compiled);

        private readonly Dictionary<HttpVerb, RouteSegmentProcessing> _root = HttpVerb.GetValues().ToDictionary
        (
            static v => v,
            static _ => new RouteSegmentProcessing()
        );

        private readonly Dictionary<string, ParameterParser> _parameterParsers = [];

        private int _handlerCount;

        private static IEnumerable<HandlerRegistration> FindMatches(RouteSegmentProcessing node, string[] segments, int segmentIndex, Dictionary<string, object?> paramz, ILogger<Router<TRequest, TResponse>>? logger)
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

            if (node.ExactContinuation.TryGetValue(segment, out RouteSegmentProcessing exactContinuation))
            {
                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    exactContinuation,
                    segments,
                    segmentIndex + 1,
                    paramz,
                    logger
                );
                foreach (HandlerRegistration match in matches)
                    yield return match;
            }

            foreach (RouteSegmentProcessing conditionalContinuation in node.ConditionalContinuation)
            {
                if (!conditionalContinuation.ParameterParser!.TryParse(segment, out object? parsed))
                    continue;

                IEnumerable<HandlerRegistration> matches = FindMatches
                (
                    conditionalContinuation,
                    segments,
                    segmentIndex + 1,
                    conditionalContinuation.ParameterParser?.ParameterName is { Length: > 0 } parameterName
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
        /// Gets the <see cref="Uri"/> associated with <paramref name="request"/>.
        /// </summary>
        protected abstract Uri GetUri(TRequest request);

        /// <summary>
        /// Gets the unique request id associated with <paramref name="request"/>.
        /// </summary>
        protected abstract string GetRequestId(TRequest request);

        /// <summary>
        /// Gets the <see cref="HttpVerb"/> associated with <paramref name="request"/>.
        /// </summary>
        protected abstract HttpVerb GetVerb(TRequest request);
        #endregion

        public Router<TRequest, TResponse> AddParameterParser(string parserName, ParameterParserDelegate tryParseDelegate)
        {
            _parameterParsers[parserName ?? throw new ArgumentNullException(nameof(parserName))] = new ParameterParser
            (
                parserName,
                tryParseDelegate ?? throw new ArgumentNullException(nameof(parserName))
            );

            return this;
        }

        public Router<TRequest, TResponse> AddHandler(string pattern, RequestHandler<TRequest, TResponse> handler) =>
            AddHandler
            (
                HttpVerb.GetValues(),
                pattern,
                handler
            );

        public Router<TRequest, TResponse> AddHandler(IEnumerable<HttpVerb> verbs, string pattern, RequestHandler<TRequest, TResponse> handler)
        {
            foreach (HttpVerb verb in verbs)
                AddHandler(verb, pattern, handler);

            return this;
        }

        public Router<TRequest, TResponse> AddHandler(HttpVerb verb, string pattern, RequestHandler<TRequest, TResponse> handler)
        {
            RouteSegmentProcessing target = _root[verb];

            foreach (string segment in pattern.Split(['/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (s_matcherDefinition.Match(segment) is { Success: true } parserDefinition)
                {
                    string parserName = parserDefinition.Groups["name"].Value;
                    Debug.Assert(!string.IsNullOrEmpty(parserName), "Parser name could not be extracted");

                    if (!_parameterParsers.TryGetValue(parserName, out ParameterParser parser))
                        throw new InvalidOperationException
                        (
                            string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARAMETER_PARSER, parserName)
                        );

                    if (parserDefinition.Groups["parametername"] is { Success: true } parameterName)
                        parser = parser with { ParameterName = parameterName.Value };

                    if (target.ConditionalContinuation.SingleOrDefault(cc => cc.ParameterParser!.Name.Equals(parser.Name, StringComparison.OrdinalIgnoreCase)) is not { } conditionalContinuation)
                    {
                        conditionalContinuation = new RouteSegmentProcessing
                        {
                            ParameterParser = parser
                        };
                        target.ConditionalContinuation.Add(conditionalContinuation);
                    }

                    target = conditionalContinuation;
                }
                else
                {
                    if (!target.ExactContinuation.TryGetValue(segment, out RouteSegmentProcessing exactContinuation))
                    {
                        exactContinuation = new();
                        target.ExactContinuation.Add(segment, exactContinuation);
                    }

                    target = exactContinuation;
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

        public TResponse Handle(TRequest request, IServiceProvider services)
        {
            ILogger<Router<TRequest, TResponse>>? requestLogger = services.GetService<ILogger<Router<TRequest, TResponse>>>();

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

            Stopwatch sp = Stopwatch.StartNew();

            requestLogger?.LogDebug(Resources.DBG_REQUEST_PROCESSING_STARTED);

            IEnumerable<HandlerRegistration> matches = FindMatches
            (
                _root[GetVerb(request)],
                requestPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries),
                0,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                requestLogger
            );

            if (matchingStrategy is MatchingStrategy.RegistrationOrderMatching)
                matches = matches.OrderBy(static match => match.RegistrationOrder);

            using IEnumerator<HandlerRegistration> enumerator = matches.GetEnumerator();

            TResponse response = CallNextHandler();

            requestLogger?.LogDebug(Resources.DBG_REQUEST_HANDLER_RETURNED, sp.Elapsed);

            return response;

            TResponse CallNextHandler()
            {
                if (!enumerator.MoveNext())
                {
                    requestLogger?.LogDebug(Resources.DBG_UNRPOCESSED_REQUEST);
                    throw new InvalidOperationException(Resources.ERR_NOT_FOUND);
                }

                Debug.Assert(enumerator.Current.AttachedParameters is not null, "Parameters must be attached here");

                RequestContext<TRequest> requestContext = new()
                {
                    Parameters = enumerator.Current.AttachedParameters!,
                    Request = request,
                    Services = services
                };

                return enumerator.Current.Handler(requestContext, CallNextHandler);
            }
        }
    }
}
