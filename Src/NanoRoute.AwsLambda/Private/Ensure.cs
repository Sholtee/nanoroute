/********************************************************************************
* Ensure.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.CompilerServices;

namespace NanoRoute.AwsLambda
{
    /// <summary>
    /// Guard.
    /// </summary>
    internal static class Ensure
    {
        /// <summary>
        /// Throws if the parameter is null.
        /// </summary>
        public static void NotNull(object p, [CallerArgumentExpression(nameof(p))] string? name = null)
        {
            if (p is null)
                throw new ArgumentNullException(name);
        }
    }
}
