/********************************************************************************
* Delegates.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Json;

    /// <summary>
    /// Represents a synchronous parameter parser.
    /// </summary>
    /// <param name="segment">The raw path segment extracted from the request URI.</param>
    /// <param name="parsed">The parsed value when the delegate returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</returns>
    public delegate bool SyncParameterParserDelegate(string segment, out object? parsed);

    /// <summary>
    /// Tries to parse a single route segment into a value that can be stored in <see cref="RequestContext.Parameters"/>.
    /// </summary>
    /// <param name="segment">The raw path segment extracted from the request URI.</param>
    /// <param name="services">The <see cref="IServiceProvider"/> exposed to parsers.</param>
    /// <param name="parsed">The parsed value when the delegate returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// routerBuilder.AddParameterParser("int", (string segment, out object? parsed) =&gt;
    /// {
    ///     if (int.TryParse(segment, out int value))
    ///     {
    ///         parsed = value;
    ///         return true;
    ///     }
    ///
    ///     parsed = null;
    ///     return false;
    /// });
    /// </code>
    /// </example>
    public delegate ValueTask<bool> ParameterParserDelegate(string segment, IServiceProvider services, out object? parsed);

    /// <summary>
    /// Represents a request handler in the router pipeline.
    /// </summary>
    /// <param name="requestContext">The current request context, including parsed route parameters and services.</param>
    /// <param name="callNext">Invokes the next compatible handler in the pipeline.</param>
    /// <returns>
    /// The response produced by the current handler, or by a later handler when <paramref name="callNext"/> is invoked.
    /// </returns>
    /// <remarks>
    /// Handlers may signal HTTP failures by calling <c>HttpRequestException.Throw(...)</c>. When
    /// <see cref="NanoRouteJsonExtensions.AddJsonErrorDetails{TBuilder}(TBuilder, bool)"/>, or equivalent custom
    /// middleware is registered, those exceptions can be translated into structured error responses. Throwing other
    /// exception types is also supported, but they are treated as unexpected failures:
    /// <see cref="NanoRouteExceptionExtensions.AddExceptionHandler{TBuilder}(TBuilder)"/> converts them into internal
    /// server error responses, while without such middleware they propagate to the caller unchanged.
    /// </remarks>
    /// <example>
    /// <code>
    /// routerBuilder.AddHandler("GET", "/api/users/{user_id:int}/", (requestContext, callNext) =&gt;
    /// {
    ///     requestContext.Parameters["User"] = LoadUser((int) requestContext.Parameters["user_id"]!);
    ///     return callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> RequestHandlerDelegate(RequestContext requestContext, Func<Task<HttpResponseMessage>> callNext);
}
