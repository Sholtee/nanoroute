/********************************************************************************
* HttpRequestMessageExtensions.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Net.Http;

namespace NanoRoute.Tests
{
    internal static class HttpRequestMessageExtensions
    {
        public static void SetProperty(this HttpRequestMessage request, string key, object? value)
        {
#if NET5_0_OR_GREATER
            request.Options.Set(new HttpRequestOptionsKey<object?>(key), value);
#else
            request.Properties[key] = value;
#endif
        }

        public static bool TryGetProperty(this HttpRequestMessage request, string key, out object? value)
        {
#if NET5_0_OR_GREATER
            return request.Options.TryGetValue(new HttpRequestOptionsKey<object?>(key), out value);
#else
            return request.Properties.TryGetValue(key, out value);
#endif
        }
    }
}
