/********************************************************************************
* MatchingPrecedence.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
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
}
