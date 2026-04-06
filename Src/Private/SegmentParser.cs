/********************************************************************************
* SegmentParser.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a named route-segment parser and its optional bound parameter name.
    /// </summary>
    internal sealed record SegmentParser(string Name, SegmentParserDelegate Parse)
    {
        /// <summary>
        /// Gets the normalized raw arguments extracted from the route template.
        /// </summary>
        public IReadOnlyDictionary<string, string> RawArguments { get; init; } = new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the bound parser arguments prepared at route registration time.
        /// </summary>
        public object? Arguments { get; init; }

        /// <summary>
        /// Gets the optional request-context parameter name that receives the parsed value.
        /// </summary>
        public string? ParameterName { get; init; }
    }
}