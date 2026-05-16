/********************************************************************************
* RouteScopeBuilder.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Builder responsible for configuring a route scope and its child route tree.
    /// </summary>
    /// <remarks>
    /// Route patterns support literal segments and parser-backed parameter segments such as
    /// <c>/users/{id:int}/</c>. Patterns must start with <c>/</c>, exact patterns must end with <c>/</c>,
    /// prefix patterns must end with <c>/*</c>, and repeated <c>/</c> separators such as <c>//</c> are invalid.
    /// </remarks>
    public class RouteScopeBuilder
    {
        /// <summary>
        /// The route pattern that matches the current route scope exactly.
        /// </summary>
        public const string CurrentExact = "/";

        /// <summary>
        /// The route pattern that matches the current route scope as a prefix.
        /// </summary>
        public const string CurrentPrefix = "/*";

        #region Private
        private readonly RouteNode _root;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, ValueParserRegistration> _valueParsers;

        /// <summary>
        /// Gets or creates the <see cref="RouteNode"/> that matches the given <paramref name="pattern"/>.
        /// </summary>
        private RouteNode GetOrCreateNode(string pattern)
        {
            RouteNode target = _root;

            foreach(object definition in DslParser.ParseRoutePattern(pattern))
            {
                switch (definition)
                {
                    case ParameterDefinition parameterDefinition:
                        if (!_valueParsers.TryGetValue(parameterDefinition.ValueParser.Name, out ValueParserRegistration parserRegistration))
                            throw new InvalidOperationException
                            (
                                string.Format(Resources.Culture, Resources.ERR_NO_SUCH_PARSER, parameterDefinition.ValueParser.Name)
                            );

                        if (target.ParsedChildren.SingleOrDefault(cc => cc.ParameterParser!.Definition.ValueParser.Equals(parameterDefinition.ValueParser)) is not { } parsedChild)
                        {
                            parsedChild = new RouteNode()
                            {
                                ParameterParser = new ParameterParser
                                (
                                    parameterDefinition,
                                    parserRegistration.Parse,
                                    parserRegistration.BindArguments(parameterDefinition.ValueParser.RawArguments)
                                )
                            };

                            target.ParsedChildren.Add(parsedChild);
                        }
                        else if (!StringComparer.OrdinalIgnoreCase.Equals(parsedChild.ParameterParser!.Definition.ParameterName, parameterDefinition.ParameterName))
                            throw new InvalidOperationException(Resources.ERR_PARAMETER_OVERRIDE);

                        target = parsedChild;
                        break;
                    
                    case ReadOnlyMemory<char> literalSegmentDefinition:
                        if (!target.LiteralChildren.TryGetValue(literalSegmentDefinition, out RouteNode exactChild))
                        {
                            exactChild = new RouteNode();
                            target.LiteralChildren.Add(literalSegmentDefinition, exactChild);
                        }

                        target = exactChild;
                        break;

                    default:
                        Debug.Fail("Unknown definition");
                        break;
                }
            }

            return target;
        }

        private static string JoinPattern(string @base, string extensions)
        {
            Debug.Assert(@base.EndsWith(CurrentPrefix), "Base patterns must be prefix routes");

            return @base.TrimEnd('*') + extensions.TrimStart('/');
        }

        private RouteScopeBuilder(RouteScopeBuilder parent, string pattern)
        {
            _root = parent.GetOrCreateNode(pattern);
            _valueParsers = new Dictionary<string, ValueParserRegistration>(parent._valueParsers, StringComparer.OrdinalIgnoreCase);
            
            BasePattern = JoinPattern(parent.BasePattern, pattern);
            Metadata = parent.Metadata.CreateScope();
        }

        internal RouteScopeBuilder()
        {
            _root = new RouteNode();
            _valueParsers = new Dictionary<string, ValueParserRegistration>(StringComparer.OrdinalIgnoreCase);

            BasePattern = CurrentPrefix;
            Metadata = new BuilderMetadata();
        }

        /// <summary>
        /// Creates an immutable snapshot of the current route tree.
        /// </summary>
        /// <returns>A copy of the configured root node.</returns>
        internal RouteNode GetRoot(bool freeze) => _root.Copy(freeze);
        #endregion

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed value and bind parser arguments once during route registration.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int(min=1)}</c>.</param>
        /// <param name="bindArguments">Converts raw parser arguments into typed values once per route-template branch.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="parserName"/>, <paramref name="bindArguments"/>, or
        /// <paramref name="tryParseDelegate"/> is <see langword="null"/>.
        /// </exception>
        public RouteScopeBuilder AddValueParser(string parserName, BindArgumentsDelegate bindArguments, ValueParserDelegate tryParseDelegate)
        {
            Ensure.NotNull(parserName);
            Ensure.NotNull(bindArguments);
            Ensure.NotNull(tryParseDelegate);

            _valueParsers[parserName] = new ValueParserRegistration(parserName, tryParseDelegate, bindArguments);

            return this;
        }

        /// <summary>
        /// Registers a handler for a single HTTP method.
        /// </summary>
        /// <param name="verb">The HTTP method that activates the handler.</param>
        /// <param name="pattern">
        /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
        /// registered parsers in the form <c>{parameterName:parserName}</c>. Exact patterns must end with
        /// <c>/</c>, prefix patterns must end with <c>/*</c>, and repeated <c>/</c> separators are invalid.
        /// </param>
        /// <param name="handler">
        /// The handler to execute. If several handlers match, calling the supplied <c>next</c> delegate continues
        /// the pipeline with the next compatible handler from the already selected route branch.
        /// </param>
        /// <returns>The current route scope builder instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not a supported HTTP method.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="pattern"/> uses an unsupported optional parameter or list parser, references
        /// a value parser that has not been registered yet, or reuses a parser-backed branch with a different
        /// parameter name.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
        /// <example>
        /// <code>
        /// builder.AddHandler("GET", "/files/{path:any}/*", (context, next) =&gt;
        /// {
        ///     string path = (string) context.Parameters["path"]!;
        ///     return ServeFile(path);
        /// });
        /// </code>
        /// </example>
        public RouteScopeBuilder AddHandler(string verb, string pattern, RequestMiddlewareDelegate handler)
        {
            Ensure.NotNull(verb);
            Ensure.NotNull(pattern);
            Ensure.NotNull(handler);

            if (!Enum.TryParse(verb, ignoreCase: true, out HttpVerb v))
                throw new ArgumentException
                (
                    string.Format(Resources.Culture, Resources.ERR_INVALID_VERB, verb), nameof(verb)
                );

            RouteNode target = GetOrCreateNode(pattern);

            if (!target.HandlerRegistrations.TryGetValue(v, out IList<HandlerRegistration> handlerRegistrations))
            {
                handlerRegistrations = [];
                target.HandlerRegistrations.Add(v, handlerRegistrations);
            }

            handlerRegistrations.Add
            (
                new HandlerRegistration(handler, JoinPattern(BasePattern, pattern))
            );

            return this;
        }

        /// <summary>
        /// Creates a child route scope whose routes are rooted under the given prefix.
        /// </summary>
        /// <param name="pattern">
        /// The base prefix. It must be a valid route pattern ending in <c>/*</c> so child routes can be appended to it.
        /// </param>
        /// <returns>A child route scope builder that shares the current route tree but has its own parser registration scope.</returns>
        /// <remarks>
        /// Child route scopes inherit the parent's registered value parsers at creation time. Additional parser
        /// registrations or overrides made on the child scope stay local to that branch.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> does not end with <c>/*</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="pattern"/> uses an unsupported optional parameter or list parser, references
        /// a value parser that has not been registered yet, or reuses a parser-backed branch with a different
        /// parameter name.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
        /// <example>
        /// <code>
        /// RouteScopeBuilder api = builder.CreatePrefix("/api/*");
        ///
        /// api.AddHandler("GET", "/health/", (context, _) =&gt; Results.Ok());
        /// </code>
        /// </example>
        public RouteScopeBuilder CreatePrefix(string pattern)
        {
            Ensure.NotNull(pattern);

            if (!pattern.EndsWith(CurrentPrefix))
                throw new ArgumentException(Resources.ERR_NOT_PREFIX , nameof(pattern));

            return new RouteScopeBuilder(this, pattern);
        }

        /// <summary>
        /// Gets the value parser registrations currently visible from this route scope.
        /// </summary>
        /// <remarks>
        /// For child scopes created with <see cref="CreatePrefix(string)"/>, this dictionary reflects the inherited
        /// registrations plus any overrides added to that child scope.
        /// </remarks>
        public IReadOnlyDictionary<string, ValueParserRegistration> ValueParsers => _valueParsers;

        /// <summary>
        /// Gets the route pattern prefix for this route scope.
        /// </summary>
        /// <remarks>
        /// The root scope exposes <see cref="CurrentPrefix"/>. Child scopes created with
        /// <see cref="CreatePrefix(string)"/> expose the accumulated prefix inherited from their parent scopes.
        /// </remarks>
        public string BasePattern { get; }

        /// <summary>
        /// Gets extension-defined builder metadata visible from this route scope.
        /// </summary>
        /// <remarks>
        /// Metadata is public for extension authors who need scoped build-time settings behind module-specific
        /// configuration methods. Application code usually should prefer those module APIs instead of reading or
        /// writing metadata directly.
        /// <para>
        /// Child scopes created with <see cref="CreatePrefix(string)"/> inherit a scoped copy of their parent's
        /// metadata. Metadata updates made after the child scope is created stay local to the scope where they
        /// are made.
        /// </para>
        /// </remarks>
        public BuilderMetadata Metadata { get; }

        /// <summary>
        /// Gets the distinct route patterns currently visible from this route scope.
        /// </summary>
        /// <remarks>
        /// Each entry is formatted as <c>[Verb] Pattern</c>. Child scopes list only the routes reachable from
        /// their base path, while the root scope lists the whole configured tree.
        /// </remarks>
        public IEnumerable<string> Patterns
        {
            get
            {
                HashSet<string> patterns = [];

                Walk(_root, patterns);

                return patterns.OrderBy(static p => p, StringComparer.Ordinal);

                static void Walk(RouteNode node, HashSet<string> patterns)
                {
                    foreach (KeyValuePair<HttpVerb, IList<HandlerRegistration>> handlerRegistrations in node.HandlerRegistrations)
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

