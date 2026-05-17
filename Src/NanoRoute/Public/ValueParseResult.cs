/********************************************************************************
* ValueParseResult.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    /// <summary>
    /// Represents the outcome of a value parser.
    /// </summary>
    /// <param name="Success"><see langword="true"/> when the segment is accepted by the parser; otherwise <see langword="false"/>.</param>
    /// <param name="Parsed">The parsed value when <paramref name="Success"/> is <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <example>
    /// <code>
    /// return new ValueParseResult(true, parsedValue);
    /// </code>
    /// </example>
    public readonly record struct ValueParseResult(bool Success, object? Parsed);
}

