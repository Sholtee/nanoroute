/********************************************************************************
* ParameterParserContext.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

namespace NanoRoute
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="Segment"></param>
    /// <param name="Services"></param>
    /// <param name="Cancellation"></param>
    public readonly record struct ParameterParserContext(string Segment, IServiceProvider Services, CancellationToken Cancellation);
}
