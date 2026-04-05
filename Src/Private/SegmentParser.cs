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
    internal sealed record SegmentParser(string Name, SegmentParserDelegate Parse)
    {
        /// <summary>
        /// Gets the request-context parameter name that receives the parsed value.
        /// </summary>
        public string? ParameterName { get; init; }
    }
}