/********************************************************************************
* RouterBuilder.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NanoRoute
{
    using Properties;

    /// <summary>
    /// TODO
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public class RouterBuilder<TRequest, TResponse> : RoutingContext<TRequest, TResponse>
    {
        #region Private
        // avoid using the constructor that accepts RegexOptions, since it is not AOT compatible
        private static readonly Regex s_matcherDefinition = new("\\{(?:(?<parametername>\\w+):)?(?<name>\\w+)\\}");

        private readonly Dictionary<string, ParameterParser> _parameterParsers = [];

        /// <summary>
        /// Gets or creates the <see cref="RoutingContext{TRequest, TResponse}.RouteNode"/> that matches the given <paramref name="pattern"/>.
        /// </summary>
        private RouteNode FindNode(string pattern)
        {
            RouteNode target = _root;

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
                        exactChild = new RouteNode();
                        target.ExactChildren.Add(segment, exactChild);
                    }

                    target = exactChild;
                }
            }

            Debug.Assert(target.Pattern is null || target.Pattern == pattern, "Invalid handler setup");

            return target;
        }

        private RouterBuilder(RouteNode root) : base(root) { }
        #endregion

        /// <summary>
        /// Creates a new see <see cref="RouterBuilder{TRequest, TResponse}"/> instance
        /// </summary>
        [SuppressMessage("ApiDesign", "RS0022:Constructor make noninheritable base class inheritable")]
        public RouterBuilder() : this(new RouteNode()) { }

        /// <summary>
        /// Registers a parser that can convert a route segment into a typed parameter value.
        /// </summary>
        /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
        /// <param name="tryParseDelegate">The delegate that validates and parses a single path segment.</param>
        /// <returns>The current <see cref="RouterBuilder{TRequest, TResponse}"/> instance.</returns>
        /// <remarks>
        /// If a parser is already registered under the same <paramref name="parserName"/>, the new registration
        /// replaces the existing one.
        /// </remarks>
        public RouterBuilder<TRequest, TResponse> AddParameterParser(string parserName, ParameterParserDelegate tryParseDelegate)
        {
            Ensure.NotNull(parserName);
            Ensure.NotNull(tryParseDelegate);

            _parameterParsers[parserName] = new ParameterParser(parserName, tryParseDelegate);

            return this;
        }

        /// <summary>
        /// Registers the built-in parameter parsers for common scalar route segments.
        /// </summary>
        /// <returns>The current <see cref="RouterBuilder{TRequest, TResponse}"/> instance.</returns>
        /// <remarks>
        /// This convenience method registers parsers named <c>int</c>, <c>guid</c>, <c>bool</c>, and <c>str</c>.
        /// Existing registrations with the same names are overwritten.
        /// </remarks>
        /// <example>
        /// <code>
        /// router
        ///     .AddDefaultParsers()
        ///     .AddHandler("GET", "/users/{id:int}", (context, next) =&gt; Results.Ok(context.Parameters["id"]));
        /// </code>
        /// </example>
        public RouterBuilder<TRequest, TResponse> AddDefaultParsers() =>
            AddParameterParser("int", static (string segment, out object? parsed) =>
            {
                bool success = int.TryParse(segment, out int value);
                parsed = success ? value : null;
                return success;
            })
            .AddParameterParser("guid", static (string segment, out object? parsed) =>
            {
                bool success = Guid.TryParse(segment, out Guid value);
                parsed = success ? value : null;
                return success;
            })
            .AddParameterParser("bool", static (string segment, out object? parsed) =>
            {
                bool success = bool.TryParse(segment, out bool value);
                parsed = success ? value : null;
                return success;
            })
            .AddParameterParser("str", static (string segment, out object? parsed) =>
            {
                parsed = segment;
                return true;
            });

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
        public RouterBuilder<TRequest, TResponse> AddHandler(string pattern, RequestHandler<TRequest, TResponse> handler)
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
        public RouterBuilder<TRequest, TResponse> AddHandler(IEnumerable<string> verbs, string pattern, RequestHandler<TRequest, TResponse> handler)
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
        public RouterBuilder<TRequest, TResponse> AddHandler(string verb, string pattern, RequestHandler<TRequest, TResponse> handler)
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
                new HandlerRegistration(handler, pattern[^1] is '/', target)
            );

            target.Pattern = pattern;

            return this;
        }

        /// <summary>
        /// Registers a catch-all handler that turns unhandled routing failures into JSON error responses.
        /// </summary>
        /// <param name="populateErrorInfo">
        /// <see langword="true"/> to include exception details in generated internal-server-error responses;
        /// otherwise only the public error message is returned.
        /// </param>
        /// <remarks>
        /// The default handler is registered as a prefix route for all supported HTTP methods. It calls the next
        /// matching handler and intercepts the terminal <c>not found</c> case as well as unhandled exceptions.
        /// </remarks>
        /// <example>
        /// <code>
        /// TODO
        /// </code>
        /// In this example, requests without a matching route receive the built-in JSON <c>404 Not Found</c>
        /// response instead of an unhandled exception.
        /// </example>
        public void AddDefaultHandler(bool populateErrorInfo = false) => AddHandler("/", (RequestContext<TRequest> context, Func<TResponse> next) =>
        {
            try
            {
                return next();
            }
            catch (HttpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return CreateJsonResponse
                (
                    HttpStatusCode.NotFound,
                    CreateResponse(Resources.ERR_NOT_FOUND, null)
                );
            }
            catch (Exception ex)
            {
                return CreateJsonResponse
                (
                    HttpStatusCode.InternalServerError,
                    CreateResponse
                    (
                        Resources.ERR_INERNAL_ERROR,
                        populateErrorInfo ? ex.ToString() : null
                    )
                );
            }

            static string CreateResponse(string message, string? reason)
            {
                return $"{{{KeyValue(message)}{(reason is not null ? "," + KeyValue(reason) : string.Empty)}}}";

                static string KeyValue(string value, [CallerArgumentExpression(nameof(value))] string? key = null) =>
                    $"\"{key}\":\"{HttpUtility.JavaScriptStringEncode(value)}\"";
            }
        });

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public RouterBuilder<TRequest, TResponse> WithBase(string pattern)
        {
            Ensure.NotNull(pattern);

            if (pattern.Length is 0 || pattern[^1] is not '/')
                throw new ArgumentException(Resources.ERR_NOT_PREFIX , nameof(pattern));

            return new RouterBuilder<TRequest, TResponse>
            (
                FindNode(pattern)
            );
        }
    }
}
