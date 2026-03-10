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
    /// Controls how the router orders multiple matching handlers before executing the pipeline.
    /// </summary>
    /// <remarks>
    /// This affects only requests for which more than one registered handler is compatible.
    /// </remarks>
    public enum MatchingStrategy
    {
        /// <summary>
        /// Executes matching handlers from the shortest compatible prefix toward more specific matches.
        /// </summary>
        /// <remarks>
        /// Exact segment matches still take precedence over parameter matches at the same path depth. For a request to
        /// <c>/path/to/something</c>, the handlers would run in this order: <c>/</c>, <c>/path/to/</c>,
        /// <c>/path/to/something</c>, then <c>/path/to/{id:parser}</c>.
        /// </remarks>
        ShortestPrefixMatching,

        /// <summary>
        /// Executes matching handlers in the order they were registered.
        /// </summary>
        /// <remarks>
        /// For a request to <c>/path/to/something</c>, registration order wins even when a later route is more
        /// specific. If the handlers were registered as <c>/path/to/{id:parser}</c>, <c>/path/to/something</c>,
        /// <c>/path/to/</c>, then <c>/</c>, they execute in that same sequence.
        /// </remarks>
        RegistrationOrderMatching
    }

    /// <summary>
    /// Represents the supported HTTP verbs that can be associated with route handlers.
    /// </summary>
    public enum HttpVerb
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
        private static class GetValuesHelper<TEnum> where TEnum : Enum
        {
            public static readonly IReadOnlyList<TEnum> Values =
            [
                ..typeof(TEnum).GetEnumNames().Select
                (
                    static (_, i) => (TEnum) (object) i
                )
            ];
        }

        extension<TEnum>(TEnum) where TEnum : Enum
        {
            /// <summary>
            /// Returns the values of the constants in a SEQUENTIAL enumeration. In contrast of <see cref="Enum.GetValues(Type)"/> this method is AOT friendly.
            /// </summary>
            public static IReadOnlyList<TEnum> GetValues() => GetValuesHelper<TEnum>.Values;
        }
    }
}
