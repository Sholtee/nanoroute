/********************************************************************************
* SegmentParserDefinition.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NanoRoute.Internals
{
    using Properties;

    internal readonly struct SegmentParserDefinition
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
            ARGS_PATTERN = $@"^\s*{NAME_VALUE_PAIR}(?:\s*,\s*{NAME_VALUE_PAIR})*\s*$",

            // Matches and captures a complete parser-backed segment definition.
            PARSER_DEFINITION_PATTERN = $@"^\{{(?:(?<parameterName>{IDENTIFIER}):)?(?<parserName>{IDENTIFIER})(?:\((?<arguments>.*)\))?\}}$";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly IReadOnlyDictionary<string, string> s_empty = new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

        private static readonly Regex
            s_argumentParser = new(ARGS_PATTERN),
            s_parserDefinitionParser = new(PARSER_DEFINITION_PATTERN);

        /// <summary>
        /// Parses a parser-argument list such as <c>min=1, text='it\'s ok'</c>.
        /// </summary>
        /// <param name="args">The raw argument list between the parentheses, or an empty value when no arguments were supplied.</param>
        /// <returns>The normalized argument map keyed case-insensitively.</returns>
        /// <exception cref="ArgumentException">Thrown when the argument list is malformed.</exception>
        #if DEBUG
        internal
        #else
        private
        #endif
        static IReadOnlyDictionary<string, string> ParseArguments(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return s_empty;

            Match parsed = s_argumentParser.Match(args);

            if (!parsed.Success)
                throw new ArgumentException(Resources.ERR_INVALID_PARSERS_ARGS, nameof(args));

            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

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
                    throw new ArgumentException(Resources.ERR_DUPLICATE_PARSER_ARGS, nameof(args));

                result.Add(name, value);
            }

            return result;
        }

        public static SegmentParserDefinition Create(string definition)
        {
            if (s_parserDefinitionParser.Match(definition) is not { Success: true } parsed)
                throw new ArgumentException(Resources.ERR_INVALID_PATTERN, nameof(definition));

            string parserName = parsed.Groups["parserName"].Value;
            Debug.Assert(!string.IsNullOrEmpty(parserName), "Parser name could not be extracted");

            return new SegmentParserDefinition
            {
                Name = parserName,
                RawArguments = ParseArguments(parsed.Groups["arguments"].Value),
                ParameterName = parsed.Groups["parameterName"].Value is { Length: > 0 } parameterName
                    ? parameterName
                    : null
            };
        }

        public string Name { get; private init; }
           
        public IReadOnlyDictionary<string, string> RawArguments { get; private init; }
        
        public string? ParameterName { get; private init; }

        /// <summary>
        /// <see cref="ParameterName"/> is not part of the equality contract.
        /// </summary>
        public override bool Equals(object other)
        {
            if (other is not SegmentParserDefinition otherDef || !otherDef.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
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