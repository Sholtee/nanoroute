/********************************************************************************
* SegmentParserRegistration.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a named route-segment parser together with its argument binder.
    /// </summary>
    internal sealed record SegmentParserRegistration
    (
        string Name,
        SegmentParserDelegate Parse,
        Func<IReadOnlyDictionary<string, string>, object?> BindArguments
    );
}
