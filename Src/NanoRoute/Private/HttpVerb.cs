/********************************************************************************
* HttpVerb.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;

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

    internal static class HttpVerbExtensions
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly IReadOnlyCollection<string> _names = Enum.GetNames(typeof(HttpVerb));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly IReadOnlyCollection<string> _havingBody = [HttpVerb.Post.ToString(), HttpVerb.Put.ToString(), HttpVerb.Patch.ToString()];

        extension(HttpVerb)
        {
            public static IReadOnlyCollection<string> Names => _names;

            public static IReadOnlyCollection<string> HavingBody => _havingBody;
        }
    }
}
