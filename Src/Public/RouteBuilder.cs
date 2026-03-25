/********************************************************************************
* RouteBuilder.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// TODO
    /// </summary>
    public class RouteBuilder : RoutingContext
    {
        #region Private
        private const string
            // A path segment consists of one or more valid literal URI characters or valid percent-encoded sequences
            LITERAL_SEGMENT_DEFINITION = @"(?:(?:[\w.\-~!$&'()*+,;=:@]|%[0-9A-Fa-f]{2})+)",
            PARAMETER_SEGMENT_DEFINITION = @"\{(?:\w+:)?\w+\}",
            SEGMENT_DEFINITION = $@"(?:{LITERAL_SEGMENT_DEFINITION}|{PARAMETER_SEGMENT_DEFINITION})";

        // avoid using the constructor that accepts RegexOptions, since it is not AOT compatible
        private static readonly Regex
            s_parserDefinition = new(@"^\{(?:(?<parametername>\w+):)?(?<name>\w+)\}$"),
            s_patternValidator = new($@"^(?:/?|/?{SEGMENT_DEFINITION}(?:/{SEGMENT_DEFINITION})*/?)$");

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, ParameterParser> _parameterParsers;

        private readonly string _basePattern;

        /// <summary>
        /// Gets or creates the <see cref="RouteNode"/> that matches the given <paramref name="pattern"/>.
        /// </summary>
        private RouteNode FindNode(string pattern)
        {
            if (!s_patternValidator.IsMatch(pattern))
                throw new ArgumentException(Resources.ERR_INVALID_PATTERN, nameof(pattern));

            RouteNode target = Root;

            foreach (string segment in new StringSegment(pattern, '/').Enumerate())
            {
                if (s_parserDefinition.Match(segment) is { Success: true } parserDefinition)
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
                            ParameterParser = parser,
                            Segment = segment
                        };
                        target.ParameterizedChildren.Add(parameterizedChild);
                    }
                    else if (parameterizedChild.ParameterParser!.ParameterName?.Equals(parameterName) is false)
                        throw new InvalidOperationException(Resources.ERR_PARAMETER_OVERRIDE);

                    target = parameterizedChild;
                }
                else
                {
                    if (!target.LiteralChildren.TryGetValue(segment, out RouteNode exactChild))
                    {
                        exactChild = new RouteNode
                        {
                            Segment = segment
                        };
                        target.LiteralChildren.Add(segment, exactChild);
                    }

                    target = exactChild;
                }
            }

            return target;
        }

        private static string JoinPattern(string a, string b) => $"{a.TrimEnd('/')}/{b.TrimStart('/')}";

        private RouteBuilder(RouteBuilder parent, string baseUrl): base(parent.FindNode(baseUrl))
        {
            _parameterParsers = new Dictionary<string, ParameterParser>(parent._parameterParsers, StringComparer.OrdinalIgnoreCase);
            _basePattern = JoinPattern(parent._basePattern, baseUrl);
        }

        internal RouteBuilder(): base(new RouteNode { Segment = string.Empty })
        {
            _parameterParsers = new Dictionary<string, ParameterParser>(StringComparer.OrdinalIgnoreCase);
            _basePattern = string.Empty;
        }
        #endregion

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed parameter value.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current instance.</returns>
        /// <remarks>
        /// If a parser is already registered under the same <paramref name="parserName"/>, the new registration
        /// replaces the existing one.
        /// </remarks>
        public RouteBuilder AddParameterParser(string parserName, ParameterParserDelegate tryParseDelegate)
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
        public RouteBuilder AddHandler(string pattern, RequestHandler handler)
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
        public RouteBuilder AddHandler(IEnumerable<string> verbs, string pattern, RequestHandler handler)
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
        public RouteBuilder AddHandler(string verb, string pattern, RequestHandler handler)
        {
            Ensure.NotNull(verb);
            Ensure.NotNull(pattern);
            Ensure.NotNull(handler);

            if (!Enum.TryParse(verb, ignoreCase: true, out HttpVerb v))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, verb), nameof(verb)
                );

            RouteNode target = FindNode(pattern);

            if (!target.HandlerRegistrations.TryGetValue(v, out List<HandlerRegistration> handlerRegistrations))
            {
                handlerRegistrations = [];
                target.HandlerRegistrations.Add(v, handlerRegistrations);
            }

            handlerRegistrations.Add
            (
                new HandlerRegistration(handler, JoinPattern(_basePattern, pattern))
            );

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public RouteBuilder WithBase(string pattern)
        {
            Ensure.NotNull(pattern);

            if (!pattern.EndsWith("/"))
                throw new ArgumentException(Resources.ERR_NOT_PREFIX , nameof(pattern));

            return new RouteBuilder(this, pattern);
        }

        /// <summary>
        /// 
        /// </summary>
        public RouteBuilder WithBase(string pattern, Action<RouteBuilder> configureRoutes)
        {
            Ensure.NotNull(pattern);
            Ensure.NotNull(configureRoutes);

            configureRoutes
            (
                WithBase(pattern)
            );

            return this;
        }

        /// <summary>
        /// TODO
        /// Parameter parsers assigned to this instance.
        /// </summary>
        public IEnumerable<string> ParameterParsers => _parameterParsers.Keys;

        /// <summary>
        /// TODO
        /// </summary>
        public IEnumerable<string> Patterns
        {
            get
            {
                HashSet<string> patterns = [];

                Walk(Root, patterns);

                return patterns.OrderBy(static p => p);

                static void Walk(RouteNode node, HashSet<string> patterns)
                {
                    foreach (KeyValuePair<HttpVerb, List<HandlerRegistration>> handlerRegistrations in node.HandlerRegistrations)
                        foreach (HandlerRegistration handlerRegistration in handlerRegistrations.Value)
                            patterns.Add($"[{handlerRegistrations.Key}] {handlerRegistration.Pattern}");
 
                    foreach (RouteNode childNode in node.LiteralChildren.Values)
                        Walk(childNode, patterns);

                    foreach (RouteNode childNode in node.ParameterizedChildren)
                        Walk(childNode, patterns);
                }
            }
        }
    }
}
