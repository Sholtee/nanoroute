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
    /// Builder responsible for route configuration.
    /// </summary>
        /// <remarks>
        /// Route patterns support literal segments and parser-backed parameter segments such as
        /// <c>/users/{id:int}</c>. Consecutive <c>/</c> separators are treated as a single separator. A trailing
        /// <c>/</c> marks the pattern as a prefix match, while patterns without a trailing slash must match the full
        /// path exactly.
        /// </remarks>
    public class RouteBuilder : RoutingContext
    {
        #region Private
        // Avoid using the constructor that accepts RegexOptions, It is not AOT compatible
        private static readonly Regex
            // A path segment consists of one or more valid literal URI characters or valid percent-encoded sequences.
            s_literalSegmentValidator = new(@"^(?:(?:[\w.\-~!$&'()*+,;=:@]|%[0-9A-Fa-f]{2})+)$"),
            // A parser-backed segment is recognized as a {...} shell here; the full interpretation happens in SegmentParserDefinitionParser.
            s_segmentParserValidator = new(@"^\{[^/{}]+\}$");

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, SegmentParserRegistration> _segmentParsers;

        private readonly string _basePattern;

        /// <summary>
        /// Gets or creates the <see cref="RouteNode"/> that matches the given <paramref name="pattern"/>.
        /// </summary>
        private RouteNode FindNode(string pattern)
        {
            RouteNode target = _root;

            foreach (string segment in new UriSegment(pattern).Enumerate())
            {
                if (s_segmentParserValidator.IsMatch(segment))
                {
                    SegmentParserDefinition parserDefinition = SegmentParserDefinitionParser.GetSegmentParserDefinition(segment);

                    if (!_segmentParsers.TryGetValue(parserDefinition.ParserName, out SegmentParserRegistration parserRegistration))
                        throw new InvalidOperationException
                        (
                            string.Format(Resources.Culture, Resources.ERR_NO_SUCH_SEGMENT_PARSER, parserDefinition.ParserName)
                        );

                    object? arguments = parserRegistration.BindArguments(parserDefinition.RawArguments);

                    RouteNode? parsedChild = target.ParsedChildren.SingleOrDefault
                    (
                        cc => cc.SegmentParser!.Name.Equals(parserDefinition.ParserName, StringComparison.OrdinalIgnoreCase) && EqualityComparer<object?>.Default.Equals(cc.SegmentParser.Arguments, arguments)
                    );

                    if (parsedChild is null)
                    {
                        parsedChild = new RouteNode
                        {
                            SegmentParser = new SegmentParser(parserRegistration.Name, parserRegistration.Parse, arguments, parserDefinition.ParameterName),
                            Segment = segment
                        };

                        target.ParsedChildren.Add(parsedChild);
                    }
                    else if (parsedChild.SegmentParser!.ParameterName?.Equals(parserDefinition.ParameterName) is false)
                        throw new InvalidOperationException(Resources.ERR_PARAMETER_OVERRIDE);

                    target = parsedChild;
                }
                else if (s_literalSegmentValidator.IsMatch(segment))
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
                else
                    throw new ArgumentException(Resources.ERR_INVALID_PATTERN, nameof(pattern));
            }

            return target;
        }

        private static string JoinPattern(string a, string b) => $"{a.TrimEnd('/')}/{b.TrimStart('/')}";

        private RouteBuilder(RouteBuilder parent, string baseUrl): base(parent.FindNode(baseUrl))
        {
            _segmentParsers = new Dictionary<string, SegmentParserRegistration>(parent._segmentParsers, StringComparer.OrdinalIgnoreCase);
            _basePattern = JoinPattern(parent._basePattern, baseUrl);
        }

        internal RouteBuilder(): base(new RouteNode { Segment = string.Empty })
        {
            _segmentParsers = new Dictionary<string, SegmentParserRegistration>(StringComparer.OrdinalIgnoreCase);
            _basePattern = string.Empty;
        }

        /// <summary>
        /// Creates an immutable snapshot of the current route tree.
        /// </summary>
        /// <returns>A copy of the configured root node.</returns>
        internal RouteNode GetRoot() => _root.Copy();
        #endregion

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed value and bind parser arguments once during route registration.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
        /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current instance.</returns>
        public RouteBuilder AddSegmentParser(string parserName, BindArgumentsDelegate bindArguments, SegmentParserDelegate tryParseDelegate)
        {
            Ensure.NotNull(parserName);
            Ensure.NotNull(bindArguments);
            Ensure.NotNull(tryParseDelegate);

            _segmentParsers[parserName] = new SegmentParserRegistration(parserName, tryParseDelegate, bindArguments);

            return this;
        }

        /// <summary>
        /// Registers a handler for all supported HTTP methods.
        /// </summary>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>, consecutive <c>/</c> separators are
        /// treated as a single separator, and a trailing <c>/</c> turns the pattern into a prefix match. Without a
        /// trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">The handler to execute when the pattern matches.</param>
        /// <returns>The current router instance.</returns>
        /// <example>
        /// <code>
        /// builder.AddHandler("/health", (context, next) =&gt; Results.Ok());
        /// </code>
        /// </example>
        public RouteBuilder AddHandler(string pattern, RequestHandlerDelegate handler)
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
        /// registered parsers in the form <c>{parameterName:parserName}</c>, consecutive <c>/</c> separators are
        /// treated as a single separator, and a trailing <c>/</c> turns the pattern into a prefix match. Without a
        /// trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">The handler to execute when the route matches.</param>
        /// <returns>The current router instance.</returns>
        /// <example>
        /// <code>
        /// builder.AddHandler(
        ///     ["GET", "POST"],
        ///     "/api/items/{id:int}",
        ///     (context, next) =&gt; Results.Ok(context.Parameters["id"]));
        /// </code>
        /// </example>
        public RouteBuilder AddHandler(IEnumerable<string> verbs, string pattern, RequestHandlerDelegate handler)
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
        /// registered parsers in the form <c>{parameterName:parserName}</c>, consecutive <c>/</c> separators are
        /// treated as a single separator, and a trailing <c>/</c> turns the pattern into a prefix match. Without a
        /// trailing slash, the pattern matches only the exact path.
        /// </param>
        /// <param name="handler">
        /// The handler to execute. If several handlers match, calling the supplied <c>next</c> delegate continues
        /// the pipeline with the next compatible handler.
        /// </param>
        /// <returns>The current router instance.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not a supported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the pattern references a segment parser that has not been registered yet.
        /// </exception>
        /// <example>
        /// <code>
        /// builder.AddHandler("GET", "/files/{path:any}/", (context, next) =&gt;
        /// {
        ///     string path = (string) context.Parameters["path"]!;
        ///     return ServeFile(path);
        /// });
        /// </code>
        /// </example>
        public RouteBuilder AddHandler(string verb, string pattern, RequestHandlerDelegate handler)
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
        /// Creates a child builder whose routes are rooted under the given prefix.
        /// </summary>
        /// <param name="pattern">
        /// The base prefix. It must be a valid route pattern ending in <c>/</c> so child routes can be appended to it.
        /// </param>
        /// <returns>A child builder that shares the current route tree but has its own parser registration scope.</returns>
        /// <remarks>
        /// Child builders inherit the parent's registered segment parsers at creation time. Additional parser
        /// registrations or overrides made on the child builder stay local to that branch.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is not a valid route pattern or does not end with <c>/</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <paramref name="pattern"/> references a segment parser that has not been registered yet.</exception>
        /// <example>
        /// <code>
        /// RouteBuilder api = builder.WithBase("/api/");
        ///
        /// api.AddHandler("GET", "/health", (context, _) =&gt; Results.Ok());
        /// </code>
        /// </example>
        public RouteBuilder WithBase(string pattern)
        {
            Ensure.NotNull(pattern);

            if (!pattern.EndsWith("/"))
                throw new ArgumentException(Resources.ERR_NOT_PREFIX , nameof(pattern));

            return new RouteBuilder(this, pattern);
        }

        /// <summary>
        /// Creates a child builder for the given prefix, invokes a configuration callback, and returns the current builder.
        /// </summary>
        /// <param name="pattern">
        /// The base prefix. It must be a valid route pattern ending in <c>/</c> so child routes can be appended to it.
        /// </param>
        /// <param name="configureRoutes">A callback that configures routes on the child builder.</param>
        /// <returns>The current builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is not a valid route pattern or does not end with <c>/</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <paramref name="pattern"/> references a segment parser that has not been registered yet.</exception>
        /// <example>
        /// <code>
        /// builder.WithBase("/api/", api =&gt; api
        ///     .AddHandler("GET", "/health", (context, _) =&gt; Results.Ok())
        ///     .AddHandler("GET", "/users", (context, _) =&gt; Results.Ok()));
        /// </code>
        /// </example>
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
        /// Gets the segment parser names currently registered on this builder instance.
        /// </summary>
        /// <remarks>
        /// For child builders created with <see cref="WithBase(string)"/>, this sequence reflects the inherited
        /// parsers plus any overrides added to that child scope.
        /// </remarks>
        public IEnumerable<string> SegmentParsers => _segmentParsers.Keys;

        /// <summary>
        /// Gets the distinct route patterns currently visible from this builder branch.
        /// </summary>
        /// <remarks>
        /// Each entry is formatted as <c>[Verb] Pattern</c>. Child builders list only the routes reachable from
        /// their base path, while the root builder lists the whole configured tree.
        /// </remarks>
        public IEnumerable<string> Patterns
        {
            get
            {
                HashSet<string> patterns = [];

                Walk(_root, patterns);

                return patterns.OrderBy(static p => p);

                static void Walk(RouteNode node, HashSet<string> patterns)
                {
                    foreach (KeyValuePair<HttpVerb, List<HandlerRegistration>> handlerRegistrations in node.HandlerRegistrations)
                        foreach (HandlerRegistration handlerRegistration in handlerRegistrations.Value)
                            patterns.Add($"[{handlerRegistrations.Key}] {handlerRegistration.Pattern}");
 
                    foreach (RouteNode childNode in node.LiteralChildren.Values)
                        Walk(childNode, patterns);

                    foreach (RouteNode childNode in node.ParsedChildren)
                        Walk(childNode, patterns);
                }
            }
        }
    }
}
