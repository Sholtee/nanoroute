/********************************************************************************
* BuilderMetadata.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace NanoRoute
{
    using Internals;

    /// <summary>
    /// Stores extension-defined builder settings keyed by their CLR type.
    /// </summary>
    /// <remarks>
    /// This type is public so third-party builder extensions can keep scoped build-time settings behind their own
    /// module-specific APIs. Application code usually should prefer those APIs, such as <c>ConfigureXxx()</c>
    /// methods, instead of reading or writing metadata directly.
    /// <para>
    /// Prefix builders receive a scoped copy of the parent metadata when they are created. Later changes made in
    /// either scope stay local to that scope.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Metadata.Set(new MyFeatureOptions { Enabled = true });
    ///
    /// MyFeatureOptions options = builder.Metadata.GetOrDefault(MyFeatureOptions.Default);
    /// </code>
    /// </example>
    public sealed class BuilderMetadata
    {
        private readonly Dictionary<Type, object> _items;

        internal BuilderMetadata() => _items = [];

        private BuilderMetadata(Dictionary<Type, object> items) => _items = items.ToDictionary
        (
            static kvp => kvp.Key,
            static kvp => kvp.Value is ICloneable cloneable ? cloneable.Clone() : kvp.Value
        );

        internal BuilderMetadata CreateScope() => new(_items);

        /// <summary>
        /// Gets the metadata value registered for <typeparamref name="T"/>, or <paramref name="defaultValue"/> when
        /// no value has been registered in this scope.
        /// </summary>
        /// <typeparam name="T">The metadata value type.</typeparam>
        /// <param name="defaultValue">The value to return when the metadata entry is absent.</param>
        /// <returns>The registered metadata value, or <paramref name="defaultValue"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="defaultValue"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// MyFeatureOptions options = builder.Metadata.GetOrDefault(MyFeatureOptions.Default);
        /// </code>
        /// </example>
        public T GetOrDefault<T>(T defaultValue) where T : notnull
        {
            Ensure.NotNull(defaultValue);

            return _items.TryGetValue(typeof(T), out object? value)
                ? (T) value
                : defaultValue;
        }

        /// <summary>
        /// Removes the metadata value registered for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The metadata value type.</typeparam>
        /// <returns><see langword="true"/> when an entry was removed; otherwise <see langword="false"/>.</returns>
        /// <example>
        /// <code>
        /// bool removed = builder.Metadata.Remove&lt;MyFeatureOptions&gt;();
        /// </code>
        /// </example>
        public bool Remove<T>() where T : notnull => _items.Remove(typeof(T));

        /// <summary>
        /// Registers or replaces the metadata value for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The metadata value type.</typeparam>
        /// <param name="value">The metadata value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// builder.Metadata.Set(new MyFeatureOptions { Enabled = true });
        /// </code>
        /// </example>
        public void Set<T>(T value) where T : notnull
        {
            Ensure.NotNull(value);

            _items[typeof(T)] = value;
        }

        /// <summary>
        /// Tries to get the metadata value registered for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The metadata value type.</typeparam>
        /// <param name="value">The registered metadata value, when one exists.</param>
        /// <returns><see langword="true"/> when a value exists; otherwise <see langword="false"/>.</returns>
        /// <example>
        /// <code>
        /// if (builder.Metadata.TryGet(out MyFeatureOptions? options))
        /// {
        ///     EnableFeature(options);
        /// }
        /// </code>
        /// </example>
        public bool TryGet<T>(out T? value) where T : notnull
        {
            if (_items.TryGetValue(typeof(T), out object? obj))
            {
                value = (T) obj;
                return true;
            }

            value = default;
            return false;
        }
    }
}
