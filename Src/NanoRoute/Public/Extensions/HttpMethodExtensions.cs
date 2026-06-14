/********************************************************************************
* HttpMethodExtensions.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// <see cref="HttpMethod"/> helpers.
    /// </summary>
    /// <example>
    /// <code>
    /// HttpMethod method = HttpMethod.For("PATCH");
    /// </code>
    /// </example>
    public static class HttpMethodExtensions
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly FrozenDictionary<string, HttpMethod> s_httpMethods = new Dictionary<string, HttpMethod>(StringComparer.OrdinalIgnoreCase)
        {
            ["DELETE"] = HttpMethod.Delete,
            ["GET"] = HttpMethod.Get,
            ["HEAD"] = HttpMethod.Head,
            ["OPTIONS"] = HttpMethod.Options,
#if NETSTANDARD2_0
            ["PATCH"] = new HttpMethod("PATCH"),
#else
            ["PATCH"] = HttpMethod.Patch,
#endif
            ["POST"] = HttpMethod.Post,
            ["PUT"] = HttpMethod.Put,
            ["TRACE"] = HttpMethod.Trace
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        extension(HttpMethod)
        {
            /// <summary>
            /// Returns the shared <see cref="HttpMethod"/> instance for a known method name, or creates one for a custom method.
            /// </summary>
            /// <param name="method">The HTTP method name.</param>
            /// <returns>The matching <see cref="HttpMethod"/>.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="method"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="method"/> is empty or whitespace.</exception>
            /// <exception cref="FormatException">Thrown when <paramref name="method"/> is not a valid HTTP method token.</exception>
            /// <example>
            /// <code>
            /// HttpMethod method = HttpMethod.For("GET");
            /// </code>
            /// </example>
            public static HttpMethod For(string method)
            {
                Ensure.NotNull(method);
                return s_httpMethods.TryGetValue(method, out HttpMethod? httpMethod) ? httpMethod : new HttpMethod(method);
            }
        }
    }
}
