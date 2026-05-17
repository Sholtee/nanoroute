/********************************************************************************
* NanoRouteHandlerExtensions.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// Describes how a typed handler property is populated.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class UserRequest
    /// {
    ///     [ValueSource(ValueSource.Parameter, Name = "id")]
    ///     public int Id { get; init; }
    /// }
    /// </code>
    /// </example>
    public enum ValueSource
    {
        /// <summary>
        /// Leaves the property untouched.
        /// </summary>
        /// <example>
        /// <code>
        /// [ValueSource(ValueSource.Skip)]
        /// public string? Ignored { get; init; }
        /// </code>
        /// </example>
        Skip,

        /// <summary>
        /// Reads the value from <see cref="RequestContext.Parameters"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// [ValueSource(ValueSource.Parameter, Name = "id")]
        /// public int Id { get; init; }
        /// </code>
        /// </example>
        Parameter,

        /// <summary>
        /// Resolves the value from <see cref="RequestContext.Services"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// [ValueSource(ValueSource.ServiceLocator)]
        /// public IUserRepository Users { get; init; } = null!;
        /// </code>
        /// </example>
        ServiceLocator
    }

    /// <summary>
    /// Overrides the default binding behavior for a typed handler request property.
    /// </summary>
    /// <param name="source">The source used to populate the annotated property.</param>
    /// <example>
    /// <code>
    /// public sealed class UserRequest
    /// {
    ///     [ValueSource(ValueSource.Parameter, Name = "user_id")]
    ///     public int Id { get; init; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ValueSourceAttribute(ValueSource source) : Attribute
    {
        /// <summary>
        /// Gets the binding source used for the annotated property.
        /// </summary>
        public ValueSource Source { get; } = source;

        /// <summary>
        /// Gets or sets an optional binding name.
        /// </summary>
        /// <remarks>
        /// For <see cref="ValueSource.Parameter"/>, this overrides the key looked up in
        /// <see cref="RequestContext.Parameters"/>. For <see cref="ValueSource.ServiceLocator"/>,
        /// this is treated as the keyed service name. <see cref="ValueSource.Skip"/> does not allow
        /// a name because no value is read.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when a name is assigned while <see cref="Source"/> is <see cref="ValueSource.Skip"/>.</exception>
        /// <example>
        /// <code>
        /// [ValueSource(ValueSource.Parameter, Name = "user_id")]
        /// public int Id { get; init; }
        /// </code>
        /// </example>
        public string? Name
        {
            get;
            init
            {
                if (Source is ValueSource.Skip && value is not null)
                    throw new InvalidOperationException(Resources.ERR_SKIPPED_VALUE_SOURCE_NAME);

                field = value;
            }
        }
    }

    /// <summary>
    /// Adds typed handler overloads that project a <see cref="RequestContext"/> into a request object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, writable public properties are bound from <see cref="RequestContext.Parameters"/>
    /// using the property name as the lookup key.
    /// </para>
    /// <para>
    /// Properties of type <see cref="RequestContext"/> and <see cref="CancellationToken"/> are populated
    /// automatically from the current request.
    /// </para>
    /// <para>
    /// Use <see cref="ValueSourceAttribute"/> to bind a property from a specific parameter key or service.
    /// Missing required parameter values and services throw <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder
    ///     .AddDefaultValueParsers()
    ///     .AddHandler&lt;UserRequest&gt;("GET", "/users/{id:int}/", request =&gt;
    ///         Results.Ok(request.Id));
    /// </code>
    /// </example>
    public static class NanoRouteHandlerExtensions
    {
        #region Private
        private static class ContextMapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext> where TRequestContext : new()
        {
            private static readonly Lazy<Func<RequestContext, TRequestContext>> s_MapperFunction = new
            (
                static () =>
                {
                    ParameterExpression
                        source = Expression.Parameter(typeof(RequestContext), nameof(source)),
                        result = Expression.Variable(typeof(TRequestContext), nameof(result));

                    List<Expression> propSetters = 
                    [
                        Expression.Assign
                        (
                            result,
                            Expression.New
                            (
                                typeof(TRequestContext).GetConstructor(Type.EmptyTypes)
                            )
                        )
                    ];

                    foreach (PropertyInfo prop in typeof(TRequestContext).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!prop.CanWrite)
                            continue;

                        ValueSourceAttribute? valueSource = prop.GetCustomAttribute<ValueSourceAttribute>();

                        switch (valueSource?.Source)
                        {
                            case ValueSource.Skip:
                                continue;
                            case ValueSource.ServiceLocator:
                            {
                                SetProperty
                                (
                                    valueSource?.Name is { } name
                                        ? context => context.Services.GetRequiredKeyedService(prop.PropertyType, name)
                                        : context => context.Services.GetRequiredService(prop.PropertyType)
                                );
                                continue;
                            }
                            case ValueSource.Parameter:
                            {
                                string name = valueSource?.Name ?? prop.Name;
                                SetProperty
                                (
                                    context => context.Parameters.TryGetValue(name, out object? arg)
                                        ? arg!
                                        // InvalidOperationException may map to HTTP 500 but the developer message will contain the reason
                                        : throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_MISSING_REQUIRED_PARAMETER, name))
                                );
                                continue;
                            }
                            case null:
                                switch (prop.PropertyType)
                                {
                                    case Type x when x == typeof(RequestContext):
                                        SetPropertyValue(source);
                                        continue;
                                    case Type x when x == typeof(CancellationToken):
                                        SetProperty(static context => context.Cancellation);
                                        continue;                 
                                }
                                goto case ValueSource.Parameter;
                            default:
                                Debug.Fail($"Unknown source: {valueSource.Source}");
                                break;
                        }

                        void SetPropertyValue(Expression value) => propSetters.Add
                        (
                            Expression.Assign
                            (
                                Expression.Property(result, prop),
                                Expression.Convert(value, prop.PropertyType)
                            )
                        );

                        void SetProperty(Func<RequestContext, object> del) => SetPropertyValue
                        (
                            Expression.Invoke
                            (
                                Expression.Constant(del, typeof(Func<RequestContext, object>)),
                                source
                            )
                        );
                    }

                    propSetters.Add(result);  // return result;

                    // In native AOT context this will be interpreted rather than compiled
                    return Expression
                        .Lambda<Func<RequestContext, TRequestContext>>
                        (
                            Expression.Block([result], propSetters),
                            source
                        ).
                        Compile
                        (
                            preferInterpretation: !RuntimeFeature.IsDynamicCodeSupported
                        );
                },
                isThreadSafe: true
            );

            public static Func<RequestContext, TRequestContext> Value => s_MapperFunction.Value;
        }

        private static TBuilder AddTypedHandlerCore<TBuilder, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(TBuilder routeScopeBuilder, IEnumerable<string> verbs, string pattern, TypedRequestHandlerDelegate<TRequestContext> handler) where TBuilder : RouteScopeBuilder where TRequestContext : new()
        {
            Ensure.NotNull(routeScopeBuilder);
            Ensure.NotNull(verbs);
            Ensure.NotNull(pattern);
            Ensure.NotNull(handler);

            Func<RequestContext, TRequestContext> mapContext = ContextMapper<TRequestContext>.Value;

            routeScopeBuilder.AddHandler(verbs, pattern, (context, next) => handler(mapContext(context), next));

            return routeScopeBuilder;
        }

        private static EndPointBuilder WithTypedHandlerCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(EndPointBuilder endPointBuilder, TypedRequestHandlerDelegate<TRequestContext> handler) where TRequestContext : new()
        {
            Ensure.NotNull(endPointBuilder);
            Ensure.NotNull(handler);

            Func<RequestContext, TRequestContext> mapContext = ContextMapper<TRequestContext>.Value;

            return endPointBuilder.WithHandler((context, next) => handler(mapContext(context), next));
        }
        #endregion

        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// Registers a typed handler that receives a request object built from the current <see cref="RequestContext"/>.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="pattern">The route pattern to register for all supported HTTP methods.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown for unsupported route-template features, missing value parsers, conflicting parser-backed branches, or when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// builder.AddHandler&lt;UserRequest&gt;("/users/{id:int}/", request =&gt;
            ///     Results.Ok(request.Id));
            /// </code>
            /// </example>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(string pattern, TypedRequestEndpointHandlerDelegate<TRequestContext> handler) where TRequestContext : new() =>
                routeScopeBuilder.AddHandler(HttpVerb.Names, pattern, handler);

            /// <summary>
            /// Registers a typed handler that receives a request object built from the current <see cref="RequestContext"/>.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verb">The HTTP verb handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown for unsupported route-template features, missing value parsers, conflicting parser-backed branches, or when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// builder.AddHandler&lt;UserRequest&gt;("GET", "/users/{id:int}/", request =&gt;
            ///     Results.Ok(request.Id));
            /// </code>
            /// </example>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(string verb, string pattern, TypedRequestEndpointHandlerDelegate<TRequestContext> handler) where TRequestContext : new() =>
                routeScopeBuilder.AddHandler([verb /*will be null checked*/], pattern, handler);

            /// <summary>
            /// Registers a typed handler that receives a request object built from the current <see cref="RequestContext"/>.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verbs">The HTTP verbs handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown for unsupported route-template features, missing value parsers, conflicting parser-backed branches, or when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// builder.AddHandler&lt;UserRequest&gt;(["GET", "HEAD"], "/users/{id:int}/", request =&gt;
            ///     Results.Ok(request.Id));
            /// </code>
            /// </example>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IEnumerable<string> verbs, string pattern, TypedRequestEndpointHandlerDelegate<TRequestContext> handler) where TRequestContext : new()
            {
                Ensure.NotNull(handler);
                return AddTypedHandlerCore(routeScopeBuilder, verbs, pattern, (TRequestContext context, CallNextHandlerDelegate _) => handler(context));
            }

            /// <summary>
            /// Registers a typed handler that receives a request object and the next handler in the pipeline.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="pattern">The route pattern to register for all supported HTTP methods.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown for unsupported route-template features, missing value parsers, conflicting parser-backed branches, or when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// builder.AddHandler&lt;UserRequest&gt;("/users/{id:int}/*", (request, next) =&gt; next());
            /// </code>
            /// </example>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(string pattern, TypedRequestHandlerDelegate<TRequestContext> handler) where TRequestContext : new() =>
                routeScopeBuilder.AddHandler(HttpVerb.Names, pattern, handler);

            /// <summary>
            /// Registers a typed handler that receives a request object and the next handler in the pipeline.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verb">The HTTP verb handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verb"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown for unsupported route-template features, missing value parsers, conflicting parser-backed branches, or when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// builder.AddHandler&lt;UserRequest&gt;("GET", "/users/{id:int}/*", (request, next) =&gt; next());
            /// </code>
            /// </example>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(string verb, string pattern, TypedRequestHandlerDelegate<TRequestContext> handler) where TRequestContext : new() =>
                routeScopeBuilder.AddHandler([verb /*will be null checked*/], pattern, handler);

            /// <summary>
            /// Registers a typed handler that receives a request object and the next handler in the pipeline.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verbs">The HTTP verbs handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeScopeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown for unsupported route-template features, missing value parsers, conflicting parser-backed branches, or when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// builder.AddHandler&lt;UserRequest&gt;(["GET", "POST"], "/users/{id:int}/*", (request, next) =&gt; next());
            /// </code>
            /// </example>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IEnumerable<string> verbs, string pattern, TypedRequestHandlerDelegate<TRequestContext> handler) where TRequestContext : new() =>
                AddTypedHandlerCore(routeScopeBuilder, verbs, pattern, handler);

            /// <summary>
            /// Registers a handler for all supported HTTP methods.
            /// </summary>
            /// <param name="pattern">
            /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
            /// registered parsers in the form <c>{parameterName:parserName}</c>. Exact patterns must end with
            /// <c>/</c>, prefix patterns must end with <c>/*</c>, and repeated <c>/</c> separators are invalid.
            /// </param>
            /// <param name="handler">The handler to execute when the pattern matches.</param>
            /// <returns>The current router instance.</returns>
            /// <example>
            /// <code>
            /// builder.AddHandler("/health/", (context, next) =&gt; Results.Ok());
            /// </code>
            /// </example>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            public TBuilder AddHandler(string pattern, RequestHandlerDelegate handler) => routeScopeBuilder.AddHandler(HttpVerb.Names, pattern, handler);

            /// <summary>
            /// Registers the same handler for multiple HTTP methods.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the handler.</param>
            /// <param name="pattern">
            /// The route pattern to match. Literal segments are matched case-insensitively, parameter segments use
            /// registered parsers in the form <c>{parameterName:parserName}</c>. Exact patterns must end with
            /// <c>/</c>, prefix patterns must end with <c>/*</c>, and repeated <c>/</c> separators are invalid.
            /// </param>
            /// <param name="handler">The handler to execute when the route matches.</param>
            /// <returns>The current router instance.</returns>
            /// <example>
            /// <code>
            /// builder.AddHandler(
            ///     ["GET", "POST"],
            ///     "/api/items/{id:int}/",
            ///     (context, next) =&gt; Results.Ok(context.Parameters["id"]));
            /// </code>
            /// </example>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, <paramref name="pattern"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not supported or <paramref name="pattern"/> has invalid route-template syntax.</exception>
            /// <exception cref="InvalidOperationException">Thrown when <paramref name="pattern"/> uses unsupported route-template features, references a missing value parser, or conflicts with an existing parser-backed branch.</exception>
            public TBuilder AddHandler(IEnumerable<string> verbs, string pattern, RequestHandlerDelegate handler)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);

                foreach (string verb in verbs)
                    routeScopeBuilder.AddHandler(verb, pattern, handler);

                return routeScopeBuilder;
            }

            /// <summary>
            /// Registers the same handler for multiple HTTP methods at the current builder root.
            /// </summary>
            /// <param name="verbs">The HTTP methods that should use the handler.</param>
            /// <param name="handler">The handler to execute when a matching request enters this builder scope.</param>
            /// <returns>The current router instance.</returns>
            /// <remarks>
            /// This overload uses <see cref="RouteScopeBuilder.CurrentPrefix"/> as the route pattern, so the handler is
            /// bound to the whole current builder scope. If the handler calls <c>next</c>, routing continues with
            /// the next compatible handler on the selected branch.
            /// </remarks>
            /// <example>
            /// <code>
            /// builder.AddHandler(["GET", "POST"], (context, next) =&gt; next());
            /// </code>
            /// </example>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="routeScopeBuilder"/>, <paramref name="verbs"/>, or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="verbs"/> is not a supported HTTP method.</exception>
            public TBuilder AddHandler(IEnumerable<string> verbs, RequestHandlerDelegate handler) => routeScopeBuilder.AddHandler(verbs, RouteScopeBuilder.CurrentPrefix, handler);
        }

        extension(EndPointBuilder endPointBuilder)
        {
            /// <summary>
            /// Registers a typed endpoint handler that receives a request object built from the current <see cref="RequestContext"/>.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="handler">The typed endpoint handler delegate.</param>
            /// <returns>The current <paramref name="endPointBuilder"/> instance.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endPointBuilder"/> or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported.</exception>
            /// <exception cref="InvalidOperationException">Thrown when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// endpoint.WithHandler&lt;UserRequest&gt;(request =&gt; Results.Ok(request.Id));
            /// </code>
            /// </example>
            public EndPointBuilder WithHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(TypedRequestEndpointHandlerDelegate<TRequestContext> handler) where TRequestContext : new()
            {
                Ensure.NotNull(handler);
                return WithTypedHandlerCore(endPointBuilder, (TRequestContext context, CallNextHandlerDelegate _) => handler(context));
            }

            /// <summary>
            /// Registers a typed endpoint handler that receives a request object and the next handler in the pipeline.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="handler">The typed endpoint handler delegate.</param>
            /// <returns>The current <paramref name="endPointBuilder"/> instance.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ValueSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="endPointBuilder"/> or <paramref name="handler"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Thrown when the endpoint's captured HTTP method is not supported.</exception>
            /// <exception cref="InvalidOperationException">Thrown when request-time typed binding cannot resolve a required parameter or service.</exception>
            /// <example>
            /// <code>
            /// endpoint.WithHandler&lt;UserRequest&gt;((request, next) =&gt; next());
            /// </code>
            /// </example>
            public EndPointBuilder WithHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(TypedRequestHandlerDelegate<TRequestContext> handler) where TRequestContext : new() =>
                WithTypedHandlerCore(endPointBuilder, handler);
        }
    }
}
