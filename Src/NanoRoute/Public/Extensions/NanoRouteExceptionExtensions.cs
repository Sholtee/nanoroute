/********************************************************************************
* NanoRouteExceptionExtensions.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Converts an unexpected exception into an enriched <see cref="HttpRequestException"/>.
    /// </summary>
    /// <param name="exception">The exception thrown by a later handler in the routing pipeline.</param>
    /// <returns>
    /// The <see cref="HttpRequestException"/> that should be thrown by the exception-handling middleware.
    /// </returns>
    /// <remarks>
    /// Normalizers are configured with <see cref="ExceptionHandlingOptions.Map{TException}(TypedExceptionNormalizer{TException})"/>.
    /// Existing <see cref="HttpRequestException"/> and <see cref="OperationCanceledException"/> values are not
    /// normalized by <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/>.
    /// Exceptions thrown by a normalizer propagate from the exception-handling middleware.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddExceptionHandler(options =&gt; options.Map&lt;InvalidOperationException&gt;
    /// (
    ///     static ex =&gt; new HttpRequestException("Bad state", ex, HttpStatusCode.Conflict)
    /// ));
    /// </code>
    /// </example>
    public delegate HttpRequestException ExceptionNormalizer(Exception exception);

    /// <summary>
    /// Converts an unexpected exception of a registered type into an enriched <see cref="HttpRequestException"/>.
    /// </summary>
    /// <typeparam name="TException">The exception type handled by the normalizer.</typeparam>
    /// <param name="exception">The exception thrown by a later handler in the routing pipeline.</param>
    /// <returns>
    /// The <see cref="HttpRequestException"/> that should be thrown by the exception-handling middleware.
    /// </returns>
    /// <remarks>
    /// Use this delegate with <see cref="ExceptionHandlingOptions.Map{TException}(TypedExceptionNormalizer{TException})"/>
    /// to register typed normalizers without manually casting from <see cref="Exception"/>. Exception handlers
    /// check the exact runtime type first, then walk base exception types, so a base-type normalizer handles
    /// derived exceptions unless a more specific normalizer is registered.
    /// </remarks>
    /// <example>
    /// <code>
    /// options.Map&lt;InvalidOperationException&gt;
    /// (
    ///     static ex =&gt; new HttpRequestException("Bad state", ex, HttpStatusCode.Conflict)
    /// );
    /// </code>
    /// </example>
    public delegate HttpRequestException TypedExceptionNormalizer<TException>(TException exception) where TException : Exception;

    /// <summary>
    /// Configures exception normalizers for a single exception-handling middleware registration.
    /// </summary>
    /// <remarks>
    /// New instances include the built-in <see cref="AggregateException"/> normalizer, which expands inner exceptions
    /// into developer messages. Each call to <see cref="Map{TException}(TypedExceptionNormalizer{TException})"/> adds
    /// or replaces the normalizer for that exception type.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddExceptionHandler(options =&gt; options
    ///     .Map&lt;InvalidOperationException&gt;(static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)));
    /// </code>
    /// </example>
    public class ExceptionHandlingOptions
    {
        /// <summary>
        /// Initializes an exception-handling options instance with the built-in normalizers.
        /// </summary>
        /// <remarks>
        /// The default options include an <see cref="AggregateException"/> normalizer that expands inner exceptions
        /// into developer messages.
        /// </remarks>
        /// <example>
        /// <code>
        /// ExceptionHandlingOptions options = new();
        /// </code>
        /// </example>
        public ExceptionHandlingOptions()
        {
            Map<AggregateException>
            (
                static ex =>
                {
                    HttpRequestException.Throw
                    (
                        HttpStatusCode.InternalServerError,
                        Resources.ERR_INTERNAL_ERROR,
                        ex,
                        developerMessages: ex.InnerExceptions.Select(static ex => ex.ToString())
                    );
                    return null!;
                }
            );
        }

        /// <summary>
        /// Adds or replaces the normalizer used for <typeparamref name="TException"/> and its derived exception types.
        /// </summary>
        /// <typeparam name="TException">The exception type handled by <paramref name="normalizer"/>.</typeparam>
        /// <param name="normalizer">The normalizer that converts the exception into an enriched <see cref="HttpRequestException"/>.</param>
        /// <returns>The current <see cref="ExceptionHandlingOptions"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="normalizer"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// options.Map&lt;InvalidOperationException&gt;(static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict));
        /// </code>
        /// </example>
        public ExceptionHandlingOptions Map<TException>(TypedExceptionNormalizer<TException> normalizer) where TException : Exception
        {
            Ensure.NotNull(normalizer);

            ExceptionNormalizers = ExceptionNormalizers.SetItem(typeof(TException), ex => normalizer((TException) ex));

            return this;
        }

        /// <summary>
        /// Gets or sets the exception normalizers keyed by exception type.
        /// </summary>
        /// <remarks>
        /// Exception handlers check the thrown exception's exact runtime type first, then walk base exception types
        /// until a normalizer is found. Use <see cref="Map{TException}(TypedExceptionNormalizer{TException})"/> for
        /// typed registration, or replace this dictionary when a prebuilt normalizer set is easier to compose.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// options.ExceptionNormalizers = options.ExceptionNormalizers.SetItem
        /// (
        ///     typeof(InvalidOperationException),
        ///     static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
        /// );
        /// </code>
        /// </example>
        public ImmutableDictionary<Type, ExceptionNormalizer> ExceptionNormalizers
        {
            get;
            set
            {
                Ensure.NotNull(value);
                field = value;
            }
        } = ImmutableDictionary<Type, ExceptionNormalizer>.Empty;
    }

    /// <summary>
    /// Adds helpers for normalizing exceptions and extracting structured error details.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.AddExceptionHandler();
    /// </code>
    /// </example>
    public static class NanoRouteExceptionExtensions
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Action<ExceptionHandlingOptions> s_noopConfigure = static _ => { };

        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// Adds an exception-handling middleware for all supported HTTP methods.
            /// </summary>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the middleware
            /// is bound to the whole current builder scope for all supported HTTP methods.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler();
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler() =>
                routeScopeBuilder.AddExceptionHandler(HttpVerb.Names, RouteScopeBuilder.CurrentPrefix, s_noopConfigure);

            /// <summary>
            /// Adds an exception-handling middleware for all supported HTTP methods.
            /// </summary>
            /// <param name="configure">Configures normalizers for this exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler(options =&gt; options.Map&lt;InvalidOperationException&gt;
            /// (
            ///     static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
            /// ));
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(Action<ExceptionHandlingOptions> configure) =>
                routeScopeBuilder.AddExceptionHandler(HttpVerb.Names, RouteScopeBuilder.CurrentPrefix, configure);

            /// <summary>
            /// Adds an exception-handling middleware for all supported HTTP methods.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="pattern"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler("/api/*");
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(string pattern) =>
                routeScopeBuilder.AddExceptionHandler(HttpVerb.Names, pattern, s_noopConfigure);

            /// <summary>
            /// Adds an exception-handling middleware for all supported HTTP methods.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <param name="configure">Configures normalizers for this exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, or <paramref name="configure"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler("/api/*", options =&gt; options.Map&lt;InvalidOperationException&gt;
            /// (
            ///     static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
            /// ));
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(string pattern, Action<ExceptionHandlingOptions> configure) =>
                routeScopeBuilder.AddExceptionHandler(HttpVerb.Names, pattern, configure);

            /// <summary>
            /// Adds an exception-handling middleware for a single HTTP method.
            /// </summary>
            /// <param name="verb">The HTTP method that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, or <paramref name="pattern"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler("GET", "/api/*");
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(string verb, string pattern) =>
                routeScopeBuilder.AddExceptionHandler([verb], pattern, s_noopConfigure);

            /// <summary>
            /// Adds an exception-handling middleware for a single HTTP method.
            /// </summary>
            /// <param name="verb">The HTTP method that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <param name="configure">Configures normalizers for this exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="configure"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler("GET", "/api/*", options =&gt; options.Map&lt;InvalidOperationException&gt;
            /// (
            ///     static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
            /// ));
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(string verb, string pattern, Action<ExceptionHandlingOptions> configure) =>
                routeScopeBuilder.AddExceptionHandler([verb /*will be null checked*/], pattern, configure);

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the middleware
            /// is bound to the whole current builder scope for the selected HTTP methods.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/> or <paramref name="verbs"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not a supported HTTP method.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler(["GET", "POST"]);
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs) =>
                routeScopeBuilder.AddExceptionHandler(verbs, RouteScopeBuilder.CurrentPrefix, s_noopConfigure);

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <param name="configure">Configures normalizers for this exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or <paramref name="configure"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not a supported HTTP method.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler(["GET", "POST"], options =&gt; options.Map&lt;InvalidOperationException&gt;
            /// (
            ///     static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
            /// ));
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs, Action<ExceptionHandlingOptions> configure) =>
                routeScopeBuilder.AddExceptionHandler(verbs, RouteScopeBuilder.CurrentPrefix, configure);

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or <paramref name="pattern"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler(["POST", "PUT"], "/api/users/*");
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs, string pattern) =>
                routeScopeBuilder.AddExceptionHandler(verbs, pattern, s_noopConfigure);

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <param name="configure">Configures normalizers for this exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="pattern"/>, or <paramref name="configure"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// builder.AddExceptionHandler(["POST", "PUT"], "/api/users/*", options =&gt; options.Map&lt;InvalidOperationException&gt;
            /// (
            ///     static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
            /// ));
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs, string pattern, Action<ExceptionHandlingOptions> configure)
            {
                Ensure.NotNull(configure);

                ExceptionHandlingOptions options = new();
                configure.Invoke(options);

                return routeScopeBuilder.AddExceptionHandler(verbs, pattern, options);
            }

            /// <summary>
            /// Adds an exception-handling middleware with preconfigured exception normalizers.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <param name="options">The exception-handling options used by this middleware registration.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="pattern"/>, or <paramref name="options"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            /// <example>
            /// <code>
            /// ExceptionHandlingOptions options = new();
            /// options.Map&lt;InvalidOperationException&gt;(static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict));
            ///
            /// builder.AddExceptionHandler(["POST", "PUT"], "/api/users/*", options);
            /// </code>
            /// </example>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs, string pattern, ExceptionHandlingOptions options)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);
                Ensure.NotNull(options);

                FrozenDictionary<Type, ExceptionNormalizer> exceptionNormalizers = options
                    .ExceptionNormalizers
                    .ToFrozenDictionary();

                routeScopeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                {
                    try
                    {
                        return await next().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not (HttpRequestException or OperationCanceledException /*needs to be handled from user code*/))
                    {
                        for (Type exceptionType = ex.GetType(); exceptionType != typeof(object); exceptionType = exceptionType.BaseType)
                            if (exceptionNormalizers.TryGetValue(exceptionType, out ExceptionNormalizer? exceptionNormalizer))
                                throw exceptionNormalizer(ex);

                        HttpRequestException.Throw(HttpStatusCode.InternalServerError, Resources.ERR_INTERNAL_ERROR, ex, developerMessages: [ex.ToString()]);
                        return null!;
                    }
                });

                return routeScopeBuilder;
            }
        }

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store client-facing error messages.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// HttpRequestException exception = ...
        /// object? errors = exception.Data[NanoRouteExceptionExtensions.ErrorsName];
        /// </code>
        /// </example>
        public const string ErrorsName = "Errors";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store developer-facing diagnostic details.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// HttpRequestException exception = ...
        /// object? messages = exception.Data[NanoRouteExceptionExtensions.DeveloperMessagesName];
        /// </code>
        /// </example>
        public const string DeveloperMessagesName = "DeveloperMessages";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store the HTTP status code.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// HttpRequestException exception = ...
        /// object? status = exception.Data[NanoRouteExceptionExtensions.StatusName];
        /// </code>
        /// </example>
        public const string StatusName = "StatusCode";

        extension(HttpRequestException)
        {
            /// <summary>
            /// Throws an <see cref="HttpRequestException"/> enriched with an HTTP status code and public error messages.
            /// </summary>
            /// <param name="status">The HTTP status code that should be associated with the exception.</param>
            /// <param name="title">The human-readable error title.</param>
            /// <param name="errors">Optional client-facing error messages that should not contain sensitive data.</param>
            /// <exception cref="HttpRequestException">Always thrown with the supplied status and error metadata.</exception>
            /// <example>
            /// <code>
            /// HttpRequestException.Throw(HttpStatusCode.BadRequest, "Bad Request", "Missing id.");
            /// </code>
            /// </example>
            [DoesNotReturn]
            public static void Throw(HttpStatusCode status, string title, params IEnumerable<string> errors) => Throw(status, title, null, errors, null);

            /// <summary>
            /// Throws an <see cref="HttpRequestException"/> enriched with routing-specific metadata.
            /// </summary>
            /// <param name="status">The HTTP status code that should be associated with the exception.</param>
            /// <param name="title">The human-readable error title.</param>
            /// <param name="original">The original exception, if any.</param>
            /// <param name="errors">Optional client-facing error messages that should not contain sensitive data.</param>
            /// <param name="developerMessages">Optional developer-facing messages that may contain sensitive data.</param>
            /// <exception cref="HttpRequestException">Always thrown with the supplied status and error metadata.</exception>
            /// <example>
            /// <code>
            /// Exception original = ...
            /// HttpRequestException.Throw
            /// (
            ///     HttpStatusCode.Conflict,
            ///     "Conflict",
            ///     original,
            ///     errors: ["The resource has changed."],
            ///     developerMessages: [original.ToString()]
            /// );
            /// </code>
            /// </example>
            [DoesNotReturn]
            public static void Throw(HttpStatusCode status, string title, Exception? original = null, IEnumerable<string>? errors = null, IEnumerable<string>? developerMessages = null)
            {
                HttpRequestException ex = new(title, original);

                ex.Data[StatusName] = status;

                if (errors?.ToArray() is { Length: > 0 } err)
                    ex.Data[ErrorsName] = err;  // On .NET FW the Data members must be serializable (string[] it is)

                if (developerMessages?.ToArray() is { Length: > 0 } dev)
                    ex.Data[DeveloperMessagesName] = dev;

                throw ex;
            }
        }

        extension(HttpRequestException requestException)
        {
            /// <summary>
            /// Converts an <see cref="HttpRequestException"/> into an <see cref="ErrorDetails"/> payload.
            /// </summary>
            /// <param name="populateErrorInfo">
            /// <see langword="true"/> to include developer-facing details when present; otherwise <see langword="false"/>.
            /// </param>
            /// <param name="traceId">The trace identifier to expose in the resulting payload.</param>
            /// <returns>The structured error payload.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestException"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// ErrorDetails details = exception.GetErrorDetails(populateErrorInfo: false, traceId);
            /// </code>
            /// </example>
            public ErrorDetails GetErrorDetails(bool populateErrorInfo = false, string? traceId = null)
            {
                Ensure.NotNull(requestException);

                return new ErrorDetails
                {
                    Status = requestException.Data[StatusName] switch
                    {
                        HttpStatusCode status => status,
                        int intStatus => (HttpStatusCode) intStatus,
                        _ => HttpStatusCode.InternalServerError
                    },
                    Title = requestException.Message,
                    TraceId = traceId ?? Guid.NewGuid().ToString("N"),
                    Errors = requestException.Data[ErrorsName] as IEnumerable<string>,
                    DeveloperMessages = populateErrorInfo ? requestException.Data[DeveloperMessagesName] as IEnumerable<string> : null
                };
            }
        }
    }
}
