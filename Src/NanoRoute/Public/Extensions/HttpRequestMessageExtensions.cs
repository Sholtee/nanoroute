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
    using Internals;

    /// <summary>
    /// <see cref="HttpRequestMessage"/> extensions.
    /// </summary>
    /// <example>
    /// <code>
    /// bool isContentHeader = HttpRequestMessage.ContentHeaders.Contains("Content-Type");
    /// </code>
    /// </example>
    public static class HttpRequestMessageExtensions
    {
        #region Private
        private const string
            OriginalRequestPropertyKey = "OriginalRequest",
            TraceIdPropertyKey = "TraceId";

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

        private static object? GetProperty(HttpRequestMessage request, string key)
        {
            Ensure.NotNull(request);
            return request.Properties.TryGetValue(key, out object? value)
                ? value
                : null;
        }

        private static void SetProperty(HttpRequestMessage request, string key, object? value)
        {
            Ensure.NotNull(request);
            if (value is null)
                request.Properties.Remove(key);
            else
                request.Properties[key] = value;
        }
        #endregion

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

        extension(HttpRequestMessage request)
        {
            /// <summary>
            /// Gets or sets the transport-specific request object that was converted into this <see cref="HttpRequestMessage"/>.
            /// </summary>
            /// <exception cref="ArgumentNullException">Thrown when the extended request is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// request.OriginalRequest = listenerRequest;
            /// HttpListenerRequest? original = request.OriginalRequest as HttpListenerRequest;
            /// </code>
            /// </example>
            public object? OriginalRequest
            {
                get => GetProperty(request, OriginalRequestPropertyKey);
                set => SetProperty(request, OriginalRequestPropertyKey, value);
            }

            /// <summary>
            /// Gets or sets the trace identifier associated with this request.
            /// </summary>
            /// <exception cref="ArgumentNullException">Thrown when the extended request is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// request.TraceId = "trace-1";
            /// string? traceId = request.TraceId;
            /// </code>
            /// </example>
            public string? TraceId
            {
                get => GetProperty(request, TraceIdPropertyKey)?.ToString();
                set => SetProperty(request, TraceIdPropertyKey, value);
            }
        }
    }
}
