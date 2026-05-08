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
    /// The configuration is stored in <see cref="RouteBuilder.Metadata"/> and follows normal builder scoping rules.
    /// </remarks>
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
            new KeyValuePair<Type, ExceptionNormalizer>
            (
                typeof(AggregateException),
                static ex =>
                {
                    HttpRequestException.Throw
                    (
                        HttpStatusCode.InternalServerError,
                        Resources.ERR_INTERNAL_ERROR, 
                        ex, 
                        developerMessages: 
                        [
                            ..((AggregateException) ex)
                                .InnerExceptions
                                .Select(static ex => ex.ToString())
                        ]
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
        public static ExceptionHandlingConfig Default { get; } = new ExceptionHandlingConfig();
    }

    /// <summary>
    /// Adds helpers for normalizing exceptions and extracting structured error details.
    /// </summary>
    public static class NanoRouteExceptionExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Updates the exception-handling configuration visible from the current builder scope.
            /// </summary>
            /// <param name="configure">A callback that receives the current configuration and returns the replacement configuration.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The configuration is stored in <see cref="RouteBuilder.Metadata"/>. Child builders created after this
            /// method is called inherit the updated configuration; existing child builders keep their own scoped copy.
            /// Registered exception handlers snapshot the configuration that is current at registration time.
            /// </remarks>
            public TBuilder ConfigureExceptionHandling(ConfigureBuilderDelegate<ExceptionHandlingConfig> configure)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(configure);

                ExceptionHandlingConfig config = configure(routeBuilder.Metadata.GetOrDefault(ExceptionHandlingConfig.Default));
                Ensure.NotNull(config);

                routeBuilder.Metadata.Set(config);

                return routeBuilder;
            }

            /// <summary>
            /// Adds an exception-handling middleware for all supported HTTP methods.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler() => routeBuilder.AddExceptionHandler(Enum.GetNames(typeof(HttpVerb)));

            /// <summary>
            /// Adds an exception-handling middleware for all supported HTTP methods.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler(string pattern)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(pattern);

                return routeBuilder.AddExceptionHandler(Enum.GetNames(typeof(HttpVerb)), pattern);
            }

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs, string pattern)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                FrozenDictionary<Type, ExceptionNormalizer> exceptionNormalizers = routeBuilder
                    .Metadata
                    .GetOrDefault(ExceptionHandlingConfig.Default)
                    .ExceptionNormalizers
                    .ToFrozenDictionary();

                routeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
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

                return routeBuilder;
            }

            /// <summary>
            /// Adds an exception-handling middleware for a single HTTP method.
            /// </summary>
            /// <param name="verb">The HTTP method that should use the exception-handling middleware.</param>
            /// <param name="pattern">
            /// The route pattern where the exception-handling middleware should be inserted. Use <c>/</c> to apply it
            /// to the whole pipeline, or a narrower prefix/exact pattern to scope normalization to selected routes.
            /// </param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler(string verb, string pattern)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verb);
                Ensure.NotNull(pattern);

                return routeBuilder.AddExceptionHandler([verb], pattern);
            }

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged. <see cref="OperationCanceledException"/> is intentionally
            /// not normalized so caller-driven cancellation can propagate unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler(IEnumerable<string> verbs)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);

                return routeBuilder.AddExceptionHandler(verbs, "/");
            }
        }

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store client-facing error messages.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string ErrorsName = "Errors";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store developer-facing diagnostic details.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string DeveloperMessagesName = "DeveloperMessages";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store the HTTP status code.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string StatusName = "StatusCode";

        extension(HttpRequestException)
        {
            /// <summary>
            /// Throws an <see cref="HttpRequestException"/> enriched with an HTTP status code and public error messages.
            /// </summary>
            /// <param name="status">The HTTP status code that should be associated with the exception.</param>
            /// <param name="title">The human-readable error title.</param>
            /// <param name="errors">Optional client-facing error messages that should not contain sensitive data.</param>
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
