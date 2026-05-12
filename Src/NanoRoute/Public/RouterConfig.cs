/********************************************************************************
* RouterConfig.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute
{
    /// <summary>
    /// Configures runtime behavior of <see cref="Router"/> instances.
    /// </summary>
    public record RouterConfig
    {
        /// <summary>
        /// Gets or sets how NanoRoute prioritizes literal and parameterized child segments at the same depth.
        /// </summary>
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
