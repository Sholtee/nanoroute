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
    /// 
    /// </summary>
    public enum ArgumentSource
    {
        /// <summary>
        /// 
        /// </summary>
        Context,

        /// <summary>
        /// 
        /// </summary>
        ServiceLocator
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ArgumentSourceAttribute(ArgumentSource source) : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        public ArgumentSource Source { get; } = source;

        /// <summary>
        /// 
        /// </summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// 
    /// </summary>
    public static class NanoRouteHandlerExtensions
    {
        private static readonly IReadOnlyDictionary<string, string> s_EmptyDict = new Dictionary<string, string>(0);

        private static Func<RequestContext, TRequestContext> CreateContextDelegate<TRequestContext>() where TRequestContext : new()
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
                        Expression.Property(result, prop.Name),
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
                // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeFeature.NonNativeAot.cs#L16C13-L16C17
                preferInterpretation: AppContext.TryGetSwitch("System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported", out bool isDynamicCodeSupported) && !isDynamicCodeSupported
            );
        }

        private static TBuilder AddHandlerCore<TBuilder, TRequestContext>(TBuilder routeBuilder, IReadOnlyCollection<string> verbs, string pattern, IReadOnlyDictionary<string, string> queryBindings, Func<TRequestContext, CallNextHandlerDelegate, Task<HttpResponseMessage>> handler) where TBuilder : RouteBuilder where TRequestContext : new()
        {
            Ensure.NotNull(routeBuilder);
            Ensure.NotNull(verbs);
            Ensure.NotNull(pattern);
            Ensure.NotNull(queryBindings);
            Ensure.NotNull(handler);

            if (queryBindings.Count > 0)
                routeBuilder.AddQueryBindings(verbs, pattern, queryBindings);

            Func<RequestContext, TRequestContext> createContext = CreateContextDelegate<TRequestContext>();

            routeBuilder.AddHandler(verbs, pattern, (context, next) => handler(createContext(context), next));

            return routeBuilder;
        } 

        extension<TBuilder>(TBuilder routeBuilder) where TBuilder : RouteBuilder
        {
            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="TRequestContext"></typeparam>
            /// <param name="verbs"></param>
            /// <param name="pattern"></param>
            /// <param name="handler"></param>
            /// <returns></returns>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, Func<TRequestContext, Task<HttpResponseMessage>> handler) where TRequestContext : new()
            {
                Ensure.NotNull(handler);
                return AddHandlerCore(routeBuilder, verbs, pattern, s_EmptyDict, (TRequestContext context, CallNextHandlerDelegate _) => handler(context));
            }

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="TRequestContext"></typeparam>
            /// <param name="verbs"></param>
            /// <param name="pattern"></param>
            /// <param name="handler"></param>
            /// <returns></returns>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, Func<TRequestContext, CallNextHandlerDelegate, Task<HttpResponseMessage>> handler) where TRequestContext : new() =>
                AddHandlerCore(routeBuilder, verbs, pattern, s_EmptyDict, handler);

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="TRequestContext"></typeparam>
            /// <param name="verbs"></param>
            /// <param name="pattern"></param>
            /// <param name="queryBindings"></param>
            /// <param name="handler"></param>
            /// <returns></returns>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, IReadOnlyDictionary<string, string> queryBindings, Func<TRequestContext, Task<HttpResponseMessage>> handler) where TRequestContext : new()
            {
                Ensure.NotNull(handler);
                return AddHandlerCore(routeBuilder, verbs, pattern, queryBindings, (TRequestContext context, CallNextHandlerDelegate _) => handler(context));
            }

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="TRequestContext"></typeparam>
            /// <param name="verbs"></param>
            /// <param name="pattern"></param>
            /// <param name="queryBindings"></param>
            /// <param name="handler"></param>
            /// <returns></returns>
            public TBuilder AddHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TRequestContext>(IReadOnlyCollection<string> verbs, string pattern, IReadOnlyDictionary<string, string> queryBindings, Func<TRequestContext, CallNextHandlerDelegate, Task<HttpResponseMessage>> handler) where TRequestContext : new() =>
                AddHandlerCore(routeBuilder, verbs, pattern, queryBindings, handler);
        }
    }
}
