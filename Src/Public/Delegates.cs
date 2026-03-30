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
    /// <summary>
    /// Tries to parse a single route segment into a value that can be stored in <see cref="RequestContext.Parameters"/>.
    /// </summary>
    /// <param name="segment">The raw path segment extracted from the request URI.</param>
    /// <param name="parsed">The parsed value when the delegate returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// router.AddParameterParser("int", (string segment, out object? parsed) =&gt;
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
    public delegate bool ParameterParserDelegate(string segment, out object? parsed);

    /// <summary>
    /// Represents a request handler in the router pipeline.
    /// </summary>
    /// <param name="requestContext">The current request context, including parsed route parameters and services.</param>
    /// <param name="callNext">Invokes the next compatible handler in the pipeline.</param>
    /// <returns>
    /// The response produced by the current handler, or by a later handler when <paramref name="callNext"/> is invoked.
    /// </returns>
    /// <example>
    /// <code>
    /// router.AddHandler("GET", "/api/users/{user_id:int}/", (requestContext, callNext) =&gt;
    /// {
    ///     requestContext.Parameters["User"] = LoadUser((int) requestContext.Parameters["user_id"]!);
    ///     return callNext();
    /// });
    /// </code>
    /// </example>
    public delegate Task<HttpResponseMessage> RequestHandler(RequestContext requestContext, Func<Task<HttpResponseMessage>> callNext);
}
