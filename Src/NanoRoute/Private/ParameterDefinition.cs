/********************************************************************************
* ParameterDefinition.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct ParameterDefinition
    {
        private const string
            IDENTIFIER = @"[A-Za-z_]\w*",
            PARAMETER_NAME = $@"{IDENTIFIER}+\??",

            // Matches and captures a complete parser-backed segment definition.
            PARSER_DEFINITION_PATTERN = $@"^\{{(?:(?<parameterName>{PARAMETER_NAME}):)?(?<parserDefinition>.+)\}}$";

        private static readonly Regex
            s_parserDefinitionParser = new(PARSER_DEFINITION_PATTERN);

        public static bool IsValidParameterName(string parameterName) =>  // TODO: remove
            !string.IsNullOrEmpty(parameterName) && Regex.IsMatch(parameterName, $"^{PARAMETER_NAME}$");

        public static ParameterDefinition Create(string definition)
        {
            if (s_parserDefinitionParser.Match(definition) is not { Success: true } parsed)
                throw new ArgumentException(Resources.ERR_INVALID_PATTERN, nameof(definition));

            string
                parameterName = parsed.Groups["parameterName"].Value,
                parserDefinition = parsed.Groups["parserDefinition"].Value;

            return new ParameterDefinition
            {
                ValueParser = ValueParserDefinition.Create(parserDefinition),
                ParameterName = parameterName.Length is 0 ? null : parameterName,
                IsOptional = parameterName.EndsWith("?", StringComparison.Ordinal) is true
            };
        }

        public ValueParserDefinition ValueParser { get; private init; }

        public string? ParameterName { get; private init; }

        public bool IsOptional { get; private init; }
    }
}
