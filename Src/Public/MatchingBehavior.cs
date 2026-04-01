/********************************************************************************
* MatchingBehavior.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    /// <summary>
    /// Controls how the router prioritizes literal and parameterized child segments during matching.
    /// </summary>
    public enum MatchingBehavior
    {
        /// <summary>
        /// Instructs the system to try literal child segments before parameterized child segments.
        /// </summary>
        LiteralFirst,

        /// <summary>
        /// Instructs the system to try parameterized child segments before literal child segments.
        /// </summary>
        ParameterizedChildrenFirst
    }
}
