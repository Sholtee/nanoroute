/********************************************************************************
* RouterBuilderExceptionExtensions.cs                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
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
    /// Configures how <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/> normalizes
    /// unexpected exceptions.
    /// </summary>
    /// <remarks>
    /// The configuration is stored in <see cref="RouteScopeBuilder.Metadata"/> and follows normal builder scoping rules.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.ConfigureExceptionHandling(config =&gt; config with
    /// {
    ///     ExceptionNormalizers = config.ExceptionNormalizers.SetItems
    ///     ([
    ///         ExceptionNormalizer.For&lt;InvalidOperationException&gt;
    ///         (
    ///             static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
    ///         )
    ///     ])
    /// });
    /// </code>
    /// </example>
    public sealed record ExceptionHandlingConfig
    {
        /// <summary>
        /// Gets the exception normalizers keyed by concrete exception type.
        /// </summary>
        /// <remarks>
        /// When a handler throws a non-HTTP, non-cancellation exception, <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/>
        /// looks up the exception's exact runtime type in this dictionary. If no normalizer is registered, the
        /// exception is converted to a generic internal-server-error <see cref="HttpRequestException"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// builder.ConfigureExceptionHandling(config =&gt; config with
        /// {
        ///     ExceptionNormalizers = config.ExceptionNormalizers.SetItems
        ///     ([
        ///         ExceptionNormalizer.For&lt;InvalidOperationException&gt;
        ///         (
        ///             static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
        ///         )
        ///     ])
        /// });
        /// </code>
        /// </example>
        public ImmutableDictionary<Type, ExceptionNormalizer> ExceptionNormalizers
        {
            get;
            init
            {
                Ensure.NotNull(value);
                field = value;
            }
        } =
        [
            ExceptionNormalizer.For<AggregateException>
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
            )
        ];

        /// <summary>
        /// Gets the default exception-handling configuration.
        /// </summary>
        /// <remarks>
        /// The default configuration expands <see cref="AggregateException"/> into developer messages for its inner
        /// exceptions. Other unexpected exceptions are handled by the fallback internal-server-error normalizer in
        /// <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// ExceptionHandlingConfig config = ExceptionHandlingConfig.Default;
        /// </code>
        /// </example>
        public static ExceptionHandlingConfig Default { get; } = new ExceptionHandlingConfig();
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
        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// Updates the exception-handling configuration visible from the current builder scope.
            /// </summary>
            /// <param name="configure">A callback that receives the current configuration and returns the replacement configuration.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/> instance.</returns>
            /// <remarks>
            /// The configuration is stored in <see cref="RouteScopeBuilder.Metadata"/>. Child builders created after this
            /// method is called inherit the updated configuration; existing child builders keep their own scoped copy.
            /// Registered exception handlers snapshot the configuration that is current at registration time.
            /// </remarks>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="configure"/>, or the value returned
            /// by <paramref name="configure"/> is <see langword="null"/>.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.ConfigureExceptionHandling(config =&gt; config with
            /// {
            ///     ExceptionNormalizers = config.ExceptionNormalizers.Remove(typeof(AggregateException))
            /// });
            /// </code>
            /// </example>
            public TBuilder ConfigureExceptionHandling(ConfigureBuilderDelegate<ExceptionHandlingConfig> configure)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(configure);

                ExceptionHandlingConfig config = configure(routeScopeBuilder.Metadata.GetOrDefault(ExceptionHandlingConfig.Default));
                Ensure.NotNull(config);

                routeScopeBuilder.Metadata.Set(config);

                return routeScopeBuilder;
            }

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
            public TBuilder AddExceptionHandler() => routeScopeBuilder.AddExceptionHandler(RouteScopeBuilder.CurrentPrefix);

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
            public TBuilder AddExceptionHandler(string pattern) => routeScopeBuilder.AddExceptionHandler(HttpVerb.Names, pattern);

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
            public TBuilder AddExceptionHandler(string verb, string pattern) => routeScopeBuilder.AddExceptionHandler([verb /*will be null checked*/], pattern);

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
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs) => routeScopeBuilder.AddExceptionHandler(verbs, RouteScopeBuilder.CurrentPrefix);

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
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs, string pattern)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                FrozenDictionary<Type, ExceptionNormalizer> exceptionNormalizers = routeScopeBuilder
                    .Metadata
                    .GetOrDefault(ExceptionHandlingConfig.Default)
                    .ExceptionNormalizers
                    .ToFrozenDictionary();

                routeScopeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                {
                    try
                    {
                        return await next();
                    }
                    catch (Exception ex) when (ex is not (HttpRequestException or OperationCanceledException /*needs to be handled from user code*/))
                    {
                        if (exceptionNormalizers.TryGetValue(ex.GetType(), out ExceptionNormalizer? exceptionNormalizer))
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

        extension(ExceptionNormalizer)
        {
            /// <summary>
            /// Creates an exception-normalizer registration for a concrete exception type.
            /// </summary>
            /// <typeparam name="TException">The concrete exception type handled by <paramref name="normalizer"/>.</typeparam>
            /// <param name="normalizer">
            /// The typed normalizer that converts <typeparamref name="TException"/> into an enriched
            /// <see cref="HttpRequestException"/>.
            /// </param>
            /// <returns>
            /// A key/value pair suitable for adding to <see cref="ExceptionHandlingConfig.ExceptionNormalizers"/>.
            /// </returns>
            /// <remarks>
            /// The returned entry is keyed by <c>typeof(TException)</c>. Exception handlers perform exact runtime
            /// type lookup, so derived exception types need their own registrations.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="normalizer"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder.ConfigureExceptionHandling(config =&gt; config with
            /// {
            ///     ExceptionNormalizers = config.ExceptionNormalizers.SetItems
            ///     ([
            ///         ExceptionNormalizer.For&lt;InvalidOperationException&gt;
            ///         (
            ///             static ex =&gt; new HttpRequestException("Conflict", ex, HttpStatusCode.Conflict)
            ///         )
            ///     ])
            /// });
            /// </code>
            /// </example>
            public static KeyValuePair<Type, ExceptionNormalizer> For<TException>(TypedExceptionNormalizer<TException> normalizer) where TException : Exception
            {
                Ensure.NotNull(normalizer);

                return new KeyValuePair<Type, ExceptionNormalizer>(typeof(TException), ex => normalizer((TException) ex));
            }
        }
    }
}
