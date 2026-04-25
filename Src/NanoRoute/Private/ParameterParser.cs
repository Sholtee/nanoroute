/********************************************************************************
* ParameterParser.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a parser bound to a route parameter definition.
    /// </summary>
    internal sealed record ParameterParser(ParameterDefinition Definition, ValueParserDelegate Parse, object? Arguments);
}
