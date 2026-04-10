/********************************************************************************
* MatchingBehavior.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute
{
    /// <summary>
    /// Controls how the router selects between literal and parameterized child segments during matching.
    /// </summary>
    public enum MatchingBehavior
    {
        /// <summary>
        /// Instructs the router to select literal child segments before parameterized child segments.
        /// </summary>
        LiteralFirst,

        /// <summary>
        /// Instructs the router to select parameterized child segments before literal child segments.
        /// </summary>
        ParameterizedChildrenFirst
    }
}
