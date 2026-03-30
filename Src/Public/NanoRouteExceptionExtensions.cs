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
    /// 
    /// </summary>
    public static class NanoRouteExceptionExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public TBuilder AddExceptionHandler() => routeBuilder.AddExceptionHandler(Enum.GetNames(typeof(HttpVerb)));

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
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
        /// 
        /// </summary>
        public const string ERRORS_NAME = "Errors";

        /// <summary>
        /// 
        /// </summary>
        public const string DEVELOPER_MESSAGE = "DeveloperMessage";

        /// <summary>
        /// 
        /// </summary>
        public const string STATUS_NAME = "StatusCode";


        extension(HttpRequestException)
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="status"></param>
            /// <param name="title"></param>
            /// <param name="errors">Should not contain sensitive data</param>
            [DoesNotReturn]
            public static void Throw(HttpStatusCode status, string title, params IReadOnlyCollection<string> errors) => Throw(status, title, null, errors, null);

            /// <summary>
            /// 
            /// </summary>
            /// <param name="status"></param>
            /// <param name="title"></param>
            /// <param name="original"></param>
            /// <param name="errors">Should not contain sensitive data</param>
            /// <param name="developerMessage">May contain sensitive data</param>
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
            /// 
            /// </summary>
            /// <param name="populateErrorInfo"></param>
            /// <param name="traceId"></param>
            /// <returns></returns>
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
