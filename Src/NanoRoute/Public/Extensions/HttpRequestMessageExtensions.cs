/********************************************************************************
* HttpRequestMessageExtensions.cs                                               *
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
    /// <summary>
    /// <see cref="HttpResponseMessage"/> extensions.
    /// </summary>
    /// <example>
    /// <code>
    /// bool isContentHeader = HttpRequestMessage.ContentHeaders.Contains("Content-Type");
    /// </code>
    /// </example>
    public static class HttpRequestMessageExtensions
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly FrozenSet<string> s_contentHeaders = new List<string>
        {
            "Allow",
            "Content-Disposition",
            "Content-Encoding",
            "Content-Language",
            "Content-Length",
            "Content-Location",
            "Content-MD5",
            "Content-Range",
            "Content-Type",
            "Expires",
            "Last-Modified"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        extension(HttpRequestMessage)
        {
            /// <summary>
            /// Content header names.
            /// </summary>
            /// <example>
            /// <code>
            /// if (HttpRequestMessage.ContentHeaders.Contains(headerName))
            ///     request.Content!.Headers.TryAddWithoutValidation(headerName, values);
            /// </code>
            /// </example>
            public static FrozenSet<string> ContentHeaders => s_contentHeaders;
        }
    }
}
