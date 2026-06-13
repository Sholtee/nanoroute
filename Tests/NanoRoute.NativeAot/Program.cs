/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace NanoRoute.NativeAot
{
    internal static partial class Program
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Console smoke test exits with a non-zero code after writing the failure.")]
        private static async Task<int> Main()
        {
            try
            {
                HttpListenerRouter router = CreateRouter();

                await AssertBasicRoutes(router).ConfigureAwait(false);
                await AssertInMemoryRoutes().ConfigureAwait(false);
                await AssertTypedHandlerRoutes(router).ConfigureAwait(false);
                await AssertJsonRoutes(router).ConfigureAwait(false);
                await AssertNotFoundResponse(router).ConfigureAwait(false);

                await Console.Out.WriteLineAsync("Native AOT smoke tests passed.").ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
                return 1;
            }
        }

        private static HttpListenerRouter CreateRouter()
        {
            RouterBuilder<HttpListenerRouter, HttpListenerRouterConfig> builder = HttpListenerRouter.CreateBuilder();

            ConfigureErrorResponses(builder);
            ConfigureBasicRoutes(builder);
            ConfigureTypedHandlerRoutes(builder);
            ConfigureJsonRoutes(builder);

            return builder.CreateRouter();
        }
    }
}
