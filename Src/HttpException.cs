/********************************************************************************
* HttpException.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;

namespace NanoRoute
{
    internal sealed class HttpException(HttpStatusCode statusCode, string message) : InvalidOperationException(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}
