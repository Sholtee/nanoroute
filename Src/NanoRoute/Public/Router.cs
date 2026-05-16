/********************************************************************************
* Router.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Executes the route matching pipeline built by <see cref="RouteScopeBuilder"/>.
    /// </summary>
    /// <remarks>
    /// A router is created from a builder snapshot. Matching walks the configured route tree, attaches bound parameters, and invokes compatible
    /// handlers in order until one returns a response without delegating further.
    /// </remarks>
    public abstract class Router
    {
        private readonly RouteNode _root;

        /// <summary>
        /// The request property key that stores the trace identifier associated with the current request.
        /// </summary>
        public const string TraceIdName = "TraceId";

        /// <summary>
        /// The request property key that stores the original transport-specific request object.
        /// </summary>
        public const string OriginalRequestName = "OriginalRequest";

        /// <summary>
        /// Creates a new <see cref="Router"/> instance.
        /// </summary>
        /// <param name="routeScopeBuilder">The builder scope whose registered routes are captured by the router.</param>
        /// <param name="config">The configuration assigned to the router instance.</param>
        protected Router(RouteScopeBuilder routeScopeBuilder, RouterConfig config)
        {
            Ensure.NotNull(routeScopeBuilder);
            Ensure.NotNull(config);

            _root = routeScopeBuilder.CreateSnapshot();
            Config = config;
        }

        /// <summary>
        /// Routes an <see cref="HttpRequestMessage"/> through the configured handler pipeline.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="services">The service provider exposed to value parsers and handlers.</param>
        /// <param name="cancellation">A token that can cancel request processing.</param>
        /// <returns>The <see cref="HttpResponseMessage"/> produced by the matching handlers.</returns>
        /// <remarks>
        /// Prefix routes can participate in the same pipeline as exact routes. Consecutive <c>/</c> separators in the
        /// request path are treated as a single separator during matching. When several handlers match, NanoRoute
        /// evaluates compatible matches from shorter prefixes toward more specific matches and honors
        /// <see cref="MatchingPrecedence"/> when both literal and parameterized segments are available at the same depth.
        /// Once a branch is selected at a given depth, NanoRoute does not return to sibling branches later in the pipeline.
        /// </remarks>
        /// <exception cref="HttpRequestException">Thrown when no handler matches the request path.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/> or <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when the request uses an unsupported HTTP method.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the caller cancels the <paramref name="cancellation"/>.
        /// </exception>
        #if DEBUG
        internal
        #endif
        protected async Task<HttpResponseMessage> Handle(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellation = default)
        {
            Ensure.NotNull(request);
            Ensure.NotNull(services);

            RouterEventSource.Info.Write("RequestProcessingStarted", static request => new
            {
                RequestUri = request.RequestUri.OriginalString,
                Verb = request.Method.Method
            }, request);

            await using RequestPipeline pipeline = new(_root, Config, request, services, cancellation);

            return await pipeline.RunAsync();
        }

        /// <summary>
        /// Configuration assigned to this instance.
        /// </summary>
        public RouterConfig Config { get; }
    }

    /// <summary>
    /// Provides the self-typed base for concrete router implementations with strongly typed configuration.
    /// </summary>
    /// <typeparam name="TDescendant">The concrete router type produced by <see cref="CreateBuilder"/>.</typeparam>
    /// <typeparam name="TConfig">The configuration type exposed by <see cref="Config"/>.</typeparam>
    /// <param name="bldr">The builder whose route snapshot and configuration initialize the router.</param>
    /// <remarks>
    /// Concrete routers derive from this type to inherit <see cref="CreateBuilder"/> and a typed
    /// <see cref="Config"/> property. The concrete router must expose a public or non-public constructor that accepts
    /// <see cref="RouterBuilder{TRouter, TConfig}"/> so the generated factory can create immutable router snapshots.
    /// </remarks>
    public abstract class Router<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicConstructors)] TDescendant, TConfig>(RouterBuilder<TDescendant, TConfig> bldr) : Router(bldr, bldr.RouterConfig) where TDescendant : Router<TDescendant, TConfig> where TConfig : RouterConfig, new()
    {
        private static readonly Lazy<RouterFactoryDelegate<TDescendant, TConfig>> s_factory = new
        (
            static () =>
            {
                ParameterExpression bldr = Expression.Parameter(typeof(RouterBuilder<TDescendant, TConfig>), nameof(bldr));

                return Expression
                    .Lambda<RouterFactoryDelegate<TDescendant, TConfig>>
                    (
                        Expression.New
                        (
                            typeof(TDescendant).GetConstructor
                            (
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                null,
                                [typeof(RouterBuilder<TDescendant, TConfig>)],
                                []
                            ) ?? throw new MissingMethodException(),
                            bldr
                        ),
                        bldr
                    )
                    .Compile
                    (
                        preferInterpretation: !RuntimeFeature.IsDynamicCodeSupported
                    );
            },
            isThreadSafe: true
        );

        /// <summary>
        /// Configuration assigned to this instance.
        /// </summary>
        public new TConfig Config => (TConfig) base.Config;

        /// <summary>
        /// Creates a strongly typed builder.
        /// </summary>
        /// <returns>A builder that can register handlers, value parsers, and router configuration.</returns>
        public static RouterBuilder<TDescendant, TConfig> CreateBuilder() => new(s_factory.Value);
    }
}
