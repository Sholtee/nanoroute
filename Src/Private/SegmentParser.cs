/********************************************************************************
* SegmentParser.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a named route-segment parser and its optional bound parameter name.
    /// </summary>
    internal sealed record SegmentParser(string Name, SegmentParserDelegate Parse, object? Arguments, string? ParameterName);
}