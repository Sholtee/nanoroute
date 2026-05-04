/********************************************************************************
* HttpRequestMessageExtensions.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Net.Http;

namespace NanoRoute
{
    /// <summary>
    /// <see cref="HttpResponseMessage"/> extensions.
    /// </summary>
    public static class HttpRequestMessageExtensions
    {
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
            public static FrozenSet<string> ContentHeaders => s_contentHeaders;
        }
    }
}
