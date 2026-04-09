/********************************************************************************
* SegmentParserDefinition.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Describes a parser-backed route segment such as <c>{id:int(min=1)}</c>.
    /// </summary>
    internal sealed record SegmentParserDefinition(string ParserName, IReadOnlyDictionary<string, string> RawArguments, string? ParameterName);
}
