/********************************************************************************
* EnumExtensions.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace NanoRoute.Internals
{
    internal static class EnumExtensions
    {
        private static class Internal<TEnum> where TEnum : Enum
        {
            public static readonly IReadOnlyCollection<string> Names = Enum.GetNames(typeof(TEnum));

            public static readonly FrozenDictionary<string, TEnum> EnumMapper = 
                //Enum.GetValues(typeof(TEnum)) cannot be used here as it is not AOT compatible
                Names
                    .ToDictionary(static name => name, static name => (TEnum) Enum.Parse(typeof(TEnum), name))
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        extension<TEnum>(TEnum) where TEnum : Enum
        {
            public static IReadOnlyCollection<string> Names => Internal<TEnum>.Names;

            public static bool TryParseFast(string s, out TEnum val) => Internal<TEnum>.EnumMapper.TryGetValue(s, out val!);
        }
    }
}
