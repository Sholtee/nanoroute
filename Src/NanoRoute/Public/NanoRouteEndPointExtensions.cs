/********************************************************************************
* NanoRouteEndPointExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace NanoRoute
{
    using Internals;
    using Properties;

    /// <summary>
    /// 
    /// </summary>
    public sealed class EndPointBuilder
    {
        private readonly string _matchKind;

        private readonly IReadOnlyCollection<string> _verbs;

        private readonly RouteScopeBuilder _prefix;

        internal EndPointBuilder(RouteScopeBuilder scope, IEnumerable<string> verbs, string pattern)
        {
            Ensure.NotNull(scope);
            Ensure.NotNull(pattern);
            Ensure.NotNull(verbs);

            switch (pattern)
            {
                case string _ when pattern.EndsWith(RouteScopeBuilder.CurrentExact):
                    pattern += "*";
                    _matchKind = RouteScopeBuilder.CurrentExact;
                    break;

                case string _ when pattern.EndsWith(RouteScopeBuilder.CurrentPrefix):
                    _matchKind = RouteScopeBuilder.CurrentPrefix;
                    break;

                default:
                    throw new ArgumentException(string.Format(Resources.Culture, Resources.ERR_INVALID_PATTERN, pattern.Length > 0 ? pattern.Length - 1 : "-"), nameof(pattern));
            }

            _prefix = scope.CreatePrefix(pattern);

            _verbs = [.. verbs];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public EndPointBuilder WithHandler(RequestHandlerDelegate handler)
        {
            Ensure.NotNull(handler);

            _prefix.AddHandler(_verbs, _matchKind, handler);

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public BuilderMetadata Metadata => _prefix.Metadata;
    }

    /// <summary>
    /// 
    /// </summary>
    public static class NanoRouteEndPointExtensions
    {
        extension<TBuilder>(TBuilder routeScopeBuilder) where TBuilder : RouteScopeBuilder
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="verbs"></param>
            /// <param name="pattern"></param>
            /// <returns></returns>
            public EndPointBuilder CreateEndPoint(IEnumerable<string> verbs, string pattern)
            {
                Ensure.NotNull(routeScopeBuilder);
                Ensure.NotNull(verbs);
                Ensure.NotNull(pattern);

                return new EndPointBuilder(routeScopeBuilder, verbs, pattern);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="verb"></param>
            /// <param name="pattern"></param>
            /// <returns></returns>
            public EndPointBuilder CreateEndPoint(string verb, string pattern)
            {
                Ensure.NotNull(verb);

                return routeScopeBuilder.CreateEndPoint([verb], pattern);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="verbs"></param>
            /// <param name="pattern"></param>
            /// <param name="configureEndPoint"></param>
            /// <returns></returns>
            public TBuilder AddEndPoint(IEnumerable<string> verbs, string pattern, Action<EndPointBuilder> configureEndPoint)
            {
                Ensure.NotNull(configureEndPoint);

                configureEndPoint
                (
                    routeScopeBuilder.CreateEndPoint(verbs, pattern)
                );

                return routeScopeBuilder;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="verb"></param>
            /// <param name="pattern"></param>
            /// <param name="configureEndPoint"></param>
            /// <returns></returns>
            public TBuilder AddEndPoint(string verb, string pattern, Action<EndPointBuilder> configureEndPoint)
            {
                Ensure.NotNull(verb);

                return routeScopeBuilder.AddEndPoint([verb], pattern, configureEndPoint);
            }
        }
    }
}

