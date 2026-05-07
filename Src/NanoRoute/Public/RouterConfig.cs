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
    }
}
