/********************************************************************************
* HttpVerb.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.Internals
{
    // Instead of string, we use this value type in dictionaries as it makes the lookup
    // much faster
    internal enum HttpVerb
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
