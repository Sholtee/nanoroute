/********************************************************************************
* NanoRouteHandlerExtensions.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace NanoRoute.HandlerExtensions
{
    using Internals;
    using Properties;

    /// <summary>
    /// Describes how a typed handler property is populated.
    /// </summary>
    public enum ArgumentSource
    {
        /// <summary>
        /// Reads the value from <see cref="RequestContext.Parameters"/>.
        /// </summary>
        Context,

        /// <summary>
        /// Resolves the value from <see cref="RequestContext.Services"/>.
        /// </summary>
        ServiceLocator
    }

    /// <summary>
    /// Overrides the default binding behavior for a typed handler request property.
    /// </summary>
    /// <param name="source">The source used to populate the annotated property.</param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ArgumentSourceAttribute(ArgumentSource source) : Attribute
    {
        /// <summary>
        /// Gets the binding source used for the annotated property.
        /// </summary>
        public ArgumentSource Source { get; } = source;

        /// <summary>
        /// Gets or sets an optional binding name.
        /// </summary>
        /// <remarks>
        /// For <see cref="ArgumentSource.Context"/>, this overrides the key looked up in
        /// <see cref="RequestContext.Parameters"/>. For <see cref="ArgumentSource.ServiceLocator"/>,
        /// this is treated as the keyed service name.
        /// </remarks>
        public string? Name { get; init; }
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
    /// Use <see cref="ArgumentSourceAttribute"/> to bind a property from a specific context key or service.
    /// Missing required context values and services throw <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    public static class NanoRouteHandlerExtensions
    {
        private const string EMPTY_QUERY_BINDINGS = "";

        private static Func<RequestContext, TRequestContext> CreateContextDelegate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>() where TRequestContext : new()
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

                ArgumentSourceAttribute? argumentSource = prop.GetCustomAttribute<ArgumentSourceAttribute>();

                switch (argumentSource?.Source)
                {
                    case ArgumentSource.ServiceLocator:
                    {
                        SetProperty
                        (
                            argumentSource?.Name is { } name
                                ? context => context.Services.GetRequiredKeyedService(prop.PropertyType, name)
                                : context => context.Services.GetRequiredService(prop.PropertyType)
                        );
                        continue;
                    }
                    case ArgumentSource.Context:
                    {
                        string name = argumentSource?.Name ?? prop.Name;
                        SetProperty
                        (
                            context => context.Parameters.TryGetValue(name, out object? arg)
                                ? arg!
                                // InvalidOperationException may map to HTTP 500 but the developer message will contain the reason
                                : throw new InvalidOperationException(string.Format(Resources.Culture, Resources.ERR_MISSING_REQUIRED_PARAMETER, name))
                        );
                        continue;
                    }
                    default:
                        switch (prop.PropertyType)
                        {
                            case Type x when x == typeof(RequestContext):
                                SetPropertyValue(source);
                                continue;
                            case Type x when x == typeof(CancellationToken):
                                SetProperty(static context => context.Cancellation);
                                continue;                 
                        }
                        goto case ArgumentSource.Context;
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
            return Expression.Lambda<Func<RequestContext, TRequestContext>>(Expression.Block([result], propSetters), source).Compile
            (
                preferInterpretation: !RuntimeFeature.IsDynamicCodeSupported
            );
        }

        private static TBuilder AddHandlerCore<TBuilder, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(TBuilder routeBuilder, IReadOnlyCollection<string> verbs, string pattern, string queryBindings, Func<TRequestContext, CallNextHandlerDelegate, Task<HttpResponseMessage>> handler) where TBuilder : RouteBuilder where TRequestContext : new()
        {
            Ensure.NotNull(routeBuilder);
            Ensure.NotNull(verbs);
            Ensure.NotNull(pattern);
            Ensure.NotNull(queryBindings);
            Ensure.NotNull(handler);

            if (queryBindings.Length > 0)
                routeBuilder.AddQueryBindings(verbs, pattern, queryBindings);

            Func<RequestContext, TRequestContext> createContext = CreateContextDelegate<TRequestContext>();

            routeBuilder.AddHandler(verbs, pattern, (context, next) => handler(createContext(context), next));

            return routeBuilder;
        } 

        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// Registers a typed handler that receives a request object built from the current <see cref="RequestContext"/>.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verbs">The HTTP verbs handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ArgumentSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, Func<TRequestContext, Task<HttpResponseMessage>> handler) where TRequestContext : new()
            {
                Ensure.NotNull(handler);
                return AddHandlerCore(routeBuilder, verbs, pattern, EMPTY_QUERY_BINDINGS, (TRequestContext context, CallNextHandlerDelegate _) => handler(context));
            }

            /// <summary>
            /// Registers a typed middleware handler that receives a request object and the next handler in the pipeline.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verbs">The HTTP verbs handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="handler">The typed middleware delegate.</param>
            /// <returns>The current <paramref name="routeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// Apply <see cref="ArgumentSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, Func<TRequestContext, CallNextHandlerDelegate, Task<HttpResponseMessage>> handler) where TRequestContext : new() =>
                AddHandlerCore(routeBuilder, verbs, pattern, EMPTY_QUERY_BINDINGS, handler);

            /// <summary>
            /// Registers a typed handler and the query-string bindings it depends on.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verbs">The HTTP verbs handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="queryBindings">
            /// A query-parameter descriptor that is applied before <paramref name="handler"/> is invoked.
            /// </param>
            /// <param name="handler">The typed handler delegate.</param>
            /// <returns>The current <paramref name="routeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// <paramref name="queryBindings"/> are registered before the request object is created, so their parsed values
            /// are available through the default context binding rules.
            /// </para>
            /// <para>
            /// Apply <see cref="ArgumentSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, string queryBindings, Func<TRequestContext, Task<HttpResponseMessage>> handler) where TRequestContext : new()
            {
                Ensure.NotNull(handler);
                return AddHandlerCore(routeBuilder, verbs, pattern, queryBindings, (TRequestContext context, CallNextHandlerDelegate _) => handler(context));
            }

            /// <summary>
            /// Registers a typed middleware handler and the query-string bindings it depends on.
            /// </summary>
            /// <typeparam name="TRequestContext">
            /// The request-object type populated from the current route parameters, query bindings, services, and special framework values.
            /// </typeparam>
            /// <param name="verbs">The HTTP verbs handled by the route.</param>
            /// <param name="pattern">The route pattern to register.</param>
            /// <param name="queryBindings">
            /// A query-parameter descriptor that is applied before <paramref name="handler"/> is invoked.
            /// </param>
            /// <param name="handler">The typed middleware delegate.</param>
            /// <returns>The current <paramref name="routeBuilder"/>.</returns>
            /// <remarks>
            /// <para>
            /// Writable public properties are bound from <see cref="RequestContext.Parameters"/> by default.
            /// </para>
            /// <para>
            /// A property of type <see cref="RequestContext"/> receives the current context, and a property of type
            /// <see cref="CancellationToken"/> receives the active request token.
            /// </para>
            /// <para>
            /// <paramref name="queryBindings"/> are registered before the request object is created, so their parsed values
            /// are available through the default context binding rules.
            /// </para>
            /// <para>
            /// Apply <see cref="ArgumentSourceAttribute"/> to bind a property from a different parameter name
            /// or from the request service provider.
            /// </para>
            /// </remarks>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, string queryBindings, Func<TRequestContext, CallNextHandlerDelegate, Task<HttpResponseMessage>> handler) where TRequestContext : new() =>
                AddHandlerCore(routeBuilder, verbs, pattern, queryBindings, handler);
        }
    }
}
