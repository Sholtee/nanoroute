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
    public enum MatchingPrecedence
    {
        /// <summary>
        /// Instructs the router to select literal child segments before parameterized child segments.
        /// </summary>
        LiteralFirst,

        /// <summary>
        /// Instructs the router to select parameterized child segments before literal child segments.
        /// </summary>
        ParameterizedFirst
    }

    /// <summary>
    /// Configures runtime behavior of <see cref="Router"/> instances.
    /// </summary>
    public record RouterConfig
    {
        /// <summary>
        /// Gets or sets how NanoRoute prioritizes literal and parameterized child segments at the same depth.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is not a defined <see cref="MatchingPrecedence"/> value.</exception>
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

        /// <summary>
        /// Gets or sets the initial capacity of the request parameter dictionary.
        /// </summary>
        /// <remarks>
        /// Increase this value when typical requests add many route, query, or handler-shared values to
        /// <see cref="RequestContext.Parameters"/> and you want to reduce dictionary resizing.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is negative.</exception>
        public int ParametersCapacity
        {
            get;
            init
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                field = value;
            }
        } = 4;
    }
}
