/********************************************************************************
* RuntimeFeature.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute.Internals
{
    internal static class RuntimeFeature
    {
        // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeFeature.NonNativeAot.cs#L16C13-L16C17
        public static bool IsDynamicCodeSupported { get; } = !AppContext.TryGetSwitch("System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported", out bool isDynamicCodeSupported) || isDynamicCodeSupported;
    }
}
