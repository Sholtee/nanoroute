/********************************************************************************
* Delegates.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute
{

    /// <summary>
    /// 
    /// </summary>
    public delegate bool ParameterParserDelegate(string segment, out object? parsed);

    /// <summary>
    /// Defines the layout of request handlers.
    /// </summary>
    /// <param name="requestContext">Request context</param>
    /// <param name="next">Invokes the next handler that matches the current route.</param
    /// <returns></returns>
    public delegate TResponse RequestHandler<TRequest, TResponse>(RequestContext<TRequest> requestContext, Func<TResponse> callNext);

}
