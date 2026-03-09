/********************************************************************************
* Enums.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace NanoRoute
{
    /// <summary>
    /// Matching strategy to be sued. 
    /// </summary>
    public enum MatchingStrategy
    {
        /// <summary>
        /// Match the easiest, most generic route first. On collision the pattern containing exact segment will have the priority. This is the default.
        /// </summary>
        /// <remarks>
        /// Consider the following url: "/path/to/something"
        /// And the following pattern registrations:
        /// "/path/to/{id:id_parser}"  <-- will match 4th
        /// "/path/to/something" <--- will match 3rd (exact match always has the priority)
        /// "/path/to"  <-- doesn't match (it's an exact URL)
        /// "/path/to/" <-- will match 2nd
        /// "/"  <-- will match 1st
        /// </remarks>
        ShortestPrefixMatching,

        /// <summary>
        /// Match routes in the order they were registered
        /// </summary>
        /// Consider the following url: "/path/to/something"
        /// And the following pattern registrations:
        /// "/path/to/{id:id_parser}"  <-- will match 1st
        /// "/path/to/something" <--- will match 2nd
        /// "/path/to"  <-- doesn't match (it's an exact URL)
        /// "/path/to/" <-- will match 3rd
        /// "/"  <-- will match 4th
        /// </remarks>
        RegistrationOrderMatching
    }

    /// <summary>
    /// HTTP verbs.
    /// </summary>
    public enum HttpVerb
    {
        Get,
        Post,
        Put,
        Delete,
        Patch,
        Head,
        Options,
        Trace
    }
}
