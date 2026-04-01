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
using System.Threading.Tasks;

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
            /// values are allowed to flow through unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler() => routeBuilder.AddExceptionHandler(Enum.GetNames(typeof(HttpVerb)));

            /// <summary>
            /// Adds an exception-handling middleware for the selected HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the exception-handling middleware.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// The inserted middleware converts unexpected exceptions into <see cref="HttpRequestException"/> values
            /// with normalized status codes and diagnostic payloads. Existing <see cref="HttpRequestException"/>
            /// values are allowed to flow through unchanged.
            /// </remarks>
            public TBuilder AddExceptionHandler(IReadOnlyCollection<string> verbs)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(verbs);

                routeBuilder.AddHandler(verbs, "/", async (RequestContext context, Func<Task<HttpResponseMessage>> next) =>
                {
                    try
                    {
                        return await next();
                    }
                    catch (Exception ex) when (ex is not HttpRequestException)
                    {
                        switch (ex)
                        {
                            case OperationCanceledException or TimeoutException:
                                HttpRequestException.Throw(HttpStatusCode.RequestTimeout, Resources.ERR_REQUEST_TIMED_OUT, ex, developerMessage: [ex.StackTrace]);
                                break;
                            case AggregateException aggregateException:
                                HttpRequestException.Throw(HttpStatusCode.InternalServerError, Resources.ERR_INERNAL_ERROR, ex, developerMessage: [..aggregateException.InnerExceptions.Select(static ex => ex.ToString())]);
                                break;
                        }

                        HttpRequestException.Throw(HttpStatusCode.InternalServerError, Resources.ERR_INERNAL_ERROR, ex, developerMessage: [ex.ToString()]);
                        return null!;
                    }
                });

                return routeBuilder;
            }
        }

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store client-facing error messages.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IReadOnlyCollection{string}, IReadOnlyCollection{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string ERRORS_NAME = "Errors";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store developer-facing diagnostic details.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IReadOnlyCollection{string}, IReadOnlyCollection{string})"/>
        /// and read by <see cref="GetErrorDetails(HttpRequestException, bool, string)"/>.
        /// </remarks>
        public const string DEVELOPER_MESSAGE = "DeveloperMessage";

        /// <summary>
        /// The <see cref="Exception.Data"/> key used to store the HTTP status code.
        /// </summary>
        /// <remarks>
        /// Written by <see cref="Throw(HttpStatusCode, string, Exception, IReadOnlyCollection{string}, IReadOnlyCollection{string})"/>
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
            public static void Throw(HttpStatusCode status, string title, params IReadOnlyCollection<string> errors) => Throw(status, title, null, errors, null);

            /// <summary>
            /// Throws an <see cref="HttpRequestException"/> enriched with routing-specific metadata.
            /// </summary>
            /// <param name="status">The HTTP status code that should be associated with the exception.</param>
            /// <param name="title">The human-readable error title.</param>
            /// <param name="original">The original exception, if any.</param>
            /// <param name="errors">Optional client-facing error messages that should not contain sensitive data.</param>
            /// <param name="developerMessage">Optional developer-facing messages that may contain sensitive data.</param>
            [DoesNotReturn]
            public static void Throw(HttpStatusCode status, string title, Exception? original = null, IReadOnlyCollection<string>? errors = null, IReadOnlyCollection<string>? developerMessage = null)
            {
                HttpRequestException ex = new(title, original);

                ex.Data[STATUS_NAME] = status;

                if (errors?.Count > 0)
                    ex.Data[ERRORS_NAME] = errors;

                if (developerMessage?.Count > 0)
                    ex.Data[DEVELOPER_MESSAGE] = developerMessage;

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
