/********************************************************************************
* HttpMessageRouter.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Text;
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

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> CreateJsonResponse(HttpStatusCode statusCode, string content) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }
}
