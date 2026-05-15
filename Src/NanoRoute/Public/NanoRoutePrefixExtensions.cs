/********************************************************************************
* NanoRoutePrefixExtensions.cs                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Adds prefix-routing helpers that configure child route builders under a shared base pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefix builders inherit the value parsers and metadata visible when the child builder is created. Later
    /// changes made inside the prefix scope stay local to that child branch.
    /// </para>
    /// </remarks>
    public static class NanoRoutePrefixExtensions
    {
        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Creates a child builder for the given prefix, invokes a configuration callback, and returns the current builder.
            /// </summary>
            /// <param name="pattern">
            /// The base prefix. It must be a valid route pattern ending in <c>/*</c> so child routes can be appended to it.
            /// </param>
            /// <param name="configureRoutes">A callback that configures routes on the child builder.</param>
            /// <returns>The current builder.</returns>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> does not end with <c>/*</c>.</exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when <paramref name="pattern"/> is invalid or references a value parser that has not been
            /// registered yet.
            /// </exception>
            /// <example>
            /// <code>
            /// builder.AddPrefix("/api/*", api =&gt; api
            ///     .AddHandler("GET", "/health/", (context, _) =&gt; Results.Ok())
            ///     .AddHandler("GET", "/users/", (context, _) =&gt; Results.Ok()));
            /// </code>
            /// </example>
            public TBuilder AddPrefix(string pattern, Action<RouteBuilder> configureRoutes)  // we don't allow routers to be created from a child builder -> configureRoutes receives a RouteBuilder
            {
                Ensure.NotNull(routeBuilder);
                Ensure.NotNull(pattern);
                Ensure.NotNull(configureRoutes);

                configureRoutes
                (
                    routeBuilder.CreatePrefix(pattern)
                );

                return routeBuilder;
            }
        }
    }
}

