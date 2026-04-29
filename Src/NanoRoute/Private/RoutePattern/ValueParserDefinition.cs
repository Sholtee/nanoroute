/********************************************************************************
* ValueParserDefinition.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct ValueParserDefinition
    {
        private const string
            // Matches a valid route parser or parameter identifier.
            // Rules:
            // - must start with a letter or underscore
            // - remaining characters can be letters, digits, or underscores
            IDENTIFIER = @"[A-Za-z_]\w*",

            // Matches a boolean literal.
            BOOLEAN = @"(?:true|false)",

            // Matches a numeric literal without exponent notation.
            NUMBER = @"-?\d+(?:\.\d+)?",

            // Matches a single-quoted string literal.
            STRING = @"'(?:\\'|[^'])*'",

            // Matches any supported parser-argument value type.
            VALUE = $"(?:null|{BOOLEAN}|{NUMBER}|{STRING})",

            // Captures a single name=value pair.
            NAME_VALUE_PAIR = $@"(?<name>{IDENTIFIER})\s*=\s*(?<value>{VALUE})",

            // Matches the full input string and captures every name/value pair.
            ARGS_PATTERN = $@"\s*(?:{NAME_VALUE_PAIR}(?:\s*,\s*{NAME_VALUE_PAIR})*)?\s*",

            // Matches and captures a complete parser-backed value definition.
            PARSER_DEFINITION_PATTERN = $@"\G(?<parserName>{IDENTIFIER})(?:\({ARGS_PATTERN}\))?";

        private static readonly Regex s_parserDefinition = new(PARSER_DEFINITION_PATTERN, RuntimeFeature.IsDynamicCodeSupported ? RegexOptions.Compiled : RegexOptions.None);

        private static bool TryExtractArguments(Match parsed, out Dictionary<string, string> result)
        {
            result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            CaptureCollection
                names = parsed.Groups["name"].Captures,
                values = parsed.Groups["value"].Captures;

            for (int i = 0; i < names.Count; i++)
            {
                string
                    name = names[i].Value,
                    value = values[i].Value;

                if (value[0] is '\'')
                    value = value
                        .Substring(1, value.Length - 2)
                        .Replace("\\'", "'");

                if (result.ContainsKey(name))
                    return false;

                result.Add(name, value);
            }

            return true;
        }

        public static ValueParserDefinition Parse(string pattern, ref int offset)
        {
            if (s_parserDefinition.Match(pattern, offset) is not { Success: true, Index: int index, Length: int length } parsed || index != offset)
                throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, offset));

            string parserName = parsed.Groups["parserName"].Value;
            Debug.Assert(!string.IsNullOrEmpty(parserName), "Parser name could not be extracted");

            ValueParserDefinition result = new()
            {
                Name = parserName,
                RawArguments = TryExtractArguments(parsed, out Dictionary<string, string> rawArguments)
                    ? rawArguments
                    : throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_INVALID_ARGUMENTS, parserName, offset))
            };

            offset += length;
            return result;
        }

        public required string Name { get; init; }

        public required IReadOnlyDictionary<string, string> RawArguments { get; init; }

        public override bool Equals(object other)
        {
            if (other is not ValueParserDefinition otherDef || !otherDef.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                return false;

            if (RawArguments.Count != otherDef.RawArguments.Count)
                return false;

            foreach (KeyValuePair<string, string> kvp in otherDef.RawArguments)
                if (!RawArguments.TryGetValue(kvp.Key, out string val) || !kvp.Value.Equals(val, StringComparison.OrdinalIgnoreCase))
                    return false;

            return true;
        }

        public override int GetHashCode() => throw new NotImplementedException();
    }
}
