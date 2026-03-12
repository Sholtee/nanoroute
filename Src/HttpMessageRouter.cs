/********************************************************************************
* HttpMessageRouter.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NanoRoute
{
    /// <summary>
    /// Specializes <see cref="Router{TRequest, TResponse}"/> for <see cref="HttpRequestMessage"/> requests and
    /// asynchronous <see cref="HttpResponseMessage"/> responses.
    /// </summary>
    public abstract class HttpMessageRouter: Router<HttpRequestMessage, Task<HttpResponseMessage>>
    {
        /// <inheritdoc/>
        protected override Uri GetUri(HttpRequestMessage request) => request.RequestUri;

        /// <inheritdoc/>
        protected override string GetVerb(HttpRequestMessage request) => request.Method.Method;
    }
}
