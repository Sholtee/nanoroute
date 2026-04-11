/********************************************************************************
* SegmentParserDefinition.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct SegmentParserDefinition
    {
        private const string
            IDENTIFIER = @"[A-Za-z_]\w*",

            // Matches and captures a complete parser-backed segment definition.
            PARSER_DEFINITION_PATTERN = $@"^\{{(?:(?<parameterName>{IDENTIFIER}):)?(?<valueDefinition>.+)\}}$";

        private static readonly Regex
            s_parserDefinitionParser = new(PARSER_DEFINITION_PATTERN);

        public static SegmentParserDefinition Create(string definition)
        {
            if (s_parserDefinitionParser.Match(definition) is not { Success: true } parsed)
                throw new ArgumentException(Resources.ERR_INVALID_PATTERN, nameof(definition));

            return new SegmentParserDefinition
            {
                ValueParser = ValueParserDefinition.Create(parsed.Groups["valueDefinition"].Value),
                ParameterName = parsed.Groups["parameterName"].Value is { Length: > 0 } parameterName
                    ? parameterName
                    : null
            };
        }

        public ValueParserDefinition ValueParser { get; private init; }

        public string? ParameterName { get; private init; }
    }
}
