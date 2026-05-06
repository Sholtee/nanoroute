/********************************************************************************
* RouterBuilderExceptionExtensions.cs                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Adds helpers for normalizing exceptions and extracting structured error details.
    /// </summary>
    public static class NanoRouteExceptionExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
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

                routeBuilder.AddHandler(verbs, pattern, async (RequestContext context, CallNextHandlerDelegate next) =>
                {
                    try
                    {
                        return await next();
                    }
                    catch (Exception ex) when (ex is not (HttpRequestException or OperationCanceledException /*needs to be handled from user code*/))
                    {
                        switch (ex)
                        {
                            case AggregateException aggregateException:
                                HttpRequestException.Throw(HttpStatusCode.InternalServerError, Resources.ERR_INTERNAL_ERROR, ex, developerMessage: [..aggregateException.InnerExceptions.Select(static ex => ex.ToString())]);
                                break;
                        }

                        HttpRequestException.Throw(HttpStatusCode.InternalServerError, Resources.ERR_INTERNAL_ERROR, ex, developerMessage: [ex.ToString()]);
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
        public const string ERRORS_NAME = "Errors";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store developer-facing diagnostic details.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string DEVELOPER_MESSAGE = "DeveloperMessage";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store the HTTP status code.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IEnumerable{string}, IEnumerable{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string STATUS_NAME = "StatusCode";

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
            /// <param name="developerMessage">Optional developer-facing messages that may contain sensitive data.</param>
            [DoesNotReturn]
            public static void Throw(HttpStatusCode status, string title, Exception? original = null, IEnumerable<string>? errors = null, IEnumerable<string>? developerMessage = null)
            {
                HttpRequestException ex = new(title, original);

                ex.Data[STATUS_NAME] = status;

                if (errors?.ToArray() is { Length: > 0 } err)
                    ex.Data[ERRORS_NAME] = err;  // On .NET FW the Data members must be serializable (string[] it is)

                if (developerMessage?.ToArray() is { Length: > 0 } dev)
                    ex.Data[DEVELOPER_MESSAGE] = dev;

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
                    Status = requestException.Data[STATUS_NAME] switch
                    {
                        HttpStatusCode status => status,
                        int intStatus => (HttpStatusCode) intStatus,
                        _ => HttpStatusCode.InternalServerError
                    },
                    Title = requestException.Message,
                    TraceId = traceId ?? Guid.NewGuid().ToString("N"),
                    Errors = requestException.Data[ERRORS_NAME] as IEnumerable<string>,
                    DeveloperMessage = populateErrorInfo ? requestException.Data[DEVELOPER_MESSAGE] as IEnumerable<string> : null
                };
            }
        }
    }
}
