/********************************************************************************
* ValueParser.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    /// <summary>
    /// Stores a named route-value parser and its bound arguments.
    /// </summary>
    internal sealed record ValueParser(ValueParserDefinition Definition, ValueParserDelegate Parse, object? Arguments);
}
