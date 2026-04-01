/********************************************************************************
* RouterConfig.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    /// <summary>
    /// Configures runtime behavior of <see cref="Router"/> instances.
    /// </summary>
    public class RouterConfig
    {
        /// <summary>
        /// Gets or sets how NanoRoute prioritizes literal and parameterized child segments at the same depth.
        /// </summary>
        public MatchingBehavior MatchingBehavior { get; set; }
    }
}
