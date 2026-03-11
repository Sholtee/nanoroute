/********************************************************************************
* Enums.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace NanoRoute
{
    /// <summary>
    /// Represents the supported HTTP verbs that can be associated with route handlers.
    /// </summary>
    internal enum HttpVerb
    {
        /// <summary>
        /// The HTTP GET method.
        /// </summary>
        Get,

        /// <summary>
        /// The HTTP POST method.
        /// </summary>
        Post,

        /// <summary>
        /// The HTTP PUT method.
        /// </summary>
        Put,

        /// <summary>
        /// The HTTP DELETE method.
        /// </summary>
        Delete,

        /// <summary>
        /// The HTTP PATCH method.
        /// </summary>
        Patch,

        /// <summary>
        /// The HTTP HEAD method.
        /// </summary>
        Head,

        /// <summary>
        /// The HTTP OPTIONS method.
        /// </summary>
        Options,

        /// <summary>
        /// The HTTP TRACE method.
        /// </summary>
        Trace
    }

    internal static class EnumHelpers
    {
        private static class GetValuesHelper<TEnum> where TEnum : struct
        {
            public static readonly IReadOnlyList<TEnum> Values =
            [
                ..typeof(TEnum).GetEnumNames().Select
                (
                    static s =>
                    {
                        Enum.TryParse(s, ignoreCase: true, out TEnum result);
                        return result;
                    }
                )
            ];
        }

        extension<TEnum>(TEnum) where TEnum : struct
        {
            /// <summary>
            /// Returns the values of the constants in a SEQUENTIAL enumeration. In contrast of <see cref="Enum.GetValues(Type)"/> this method is AOT friendly.
            /// </summary>
            public static IReadOnlyList<TEnum> GetValues() => GetValuesHelper<TEnum>.Values;
        }
    }
}
