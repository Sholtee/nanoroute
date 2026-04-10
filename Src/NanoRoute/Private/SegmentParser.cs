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
    internal sealed record SegmentParser(SegmentParserDefinition Definition, SegmentParserDelegate Parse, object? Arguments);
}