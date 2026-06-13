/********************************************************************************
* RouterConfig.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute
{
    /// <summary>
    /// Defines how the router prioritizes literal and parameterized child segments during matching.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.ConfigureRouting(config =&gt; config with { MatchingPrecedence = MatchingPrecedence.ParameterizedFirst });
    /// </code>
    /// </example>
    public enum MatchingPrecedence
    {
        /// <summary>
        /// Instructs the router to select literal child segments before parameterized child segments.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.ConfigureRouting(config =&gt; config with { MatchingPrecedence = MatchingPrecedence.LiteralFirst });
        /// </code>
        /// </example>
        LiteralFirst,

        /// <summary>
        /// Instructs the router to select parameterized child segments before literal child segments.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.ConfigureRouting(config =&gt; config with { MatchingPrecedence = MatchingPrecedence.ParameterizedFirst });
        /// </code>
        /// </example>
        ParameterizedFirst
    }

    /// <summary>
    /// Configures runtime routing behavior for router instances.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.ConfigureRouting(config =&gt; config with
    /// {
    ///     MatchingPrecedence = MatchingPrecedence.ParameterizedFirst
    /// });
    /// </code>
    /// </example>
    public record RouterConfig
    {
        /// <summary>
        /// Gets or sets how NanoRoute prioritizes literal and parameterized child segments at the same depth.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is not a defined <see cref="MatchingPrecedence"/> value.</exception>
        /// <example>
        /// <code>
        /// builder.ConfigureRouting(config =&gt; config with { MatchingPrecedence = MatchingPrecedence.ParameterizedFirst });
        /// </code>
        /// </example>
        public MatchingPrecedence MatchingPrecedence
        {
            get;
            init
            {
                if (!Enum.IsDefined(typeof(MatchingPrecedence), value))
                    throw new ArgumentOutOfRangeException(nameof(value));

                field = value;
            }
        }
    }
}
