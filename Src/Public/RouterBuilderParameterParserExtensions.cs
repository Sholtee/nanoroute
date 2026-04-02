/********************************************************************************
* RouterBuilderParameterParserExtensions.cs                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Provides convenience methods for registering parameter parsers.
    /// </summary>
    /// <remarks>
    /// These helpers build on top of <see cref="RouteBuilder.AddParameterParser(string, ParameterParserDelegate)"/>.
    /// </remarks>
    public static class RouterBuilderParameterParserExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Registers a synchronous parser by adapting it to <see cref="ParameterParserDelegate"/>.
            /// </summary>
            /// <param name="parserName">The name used in route patterns such as <c>{id:int}</c>.</param>
            /// <param name="tryParseDelegate">The synchronous parser to adapt.</param>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            public TBuilder AddParameterParser(string parserName, SyncParameterParserDelegate tryParseDelegate)
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(parserName);
                Ensure.NotNull(tryParseDelegate);

                routeBuilder.AddParameterParser(parserName, (string segment, IServiceProvider _, out object? parsed) =>
                    new ValueTask<bool>(tryParseDelegate(segment, out parsed)));

                return routeBuilder;
            }

            /// <summary>
            /// Registers the built-in parameter parsers for common scalar route segments.
            /// </summary>
            /// <returns>The current <paramref name="routeBuilder"/> instance.</returns>
            /// <remarks>
            /// This convenience method registers parsers named <c>int</c>, <c>guid</c>, <c>bool</c>, and <c>str</c>.
            /// Existing registrations with the same names are overwritten.
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeBuilder"/> is <see langword="null"/>.</exception>
            /// <example>
            /// <code>
            /// builder
            ///     .AddDefaultParsers()
            ///     .AddHandler("GET", "/users/{id:int}", (context, next) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            public TBuilder AddDefaultParsers()
            {
                Ensure.NotNull(routeBuilder);

                routeBuilder
                    .AddParameterParser("int", static (string segment, out object? parsed) =>
                    {
                        bool success = int.TryParse(segment, out int value);
                        parsed = success ? value : null;
                        return success;
                    })
                    .AddParameterParser("guid", static (string segment, out object? parsed) =>
                    {
                        bool success = Guid.TryParse(segment, out Guid value);
                        parsed = success ? value : null;
                        return success;
                    })
                    .AddParameterParser("bool", static (string segment, out object? parsed) =>
                    {
                        bool success = bool.TryParse(segment, out bool value);
                        parsed = success ? value : null;
                        return success;
                    })
                    .AddParameterParser("str", static (string segment, out object? parsed) =>
                    {
                        parsed = segment;
                        return true;
                    });

                return routeBuilder;
            }
        }
    }
}
