/********************************************************************************
* Logger.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace NanoRoute.Internals
{
    /// <summary>
    /// Exposes events from this library.
    /// </summary>
    /// <remarks>This logger is not meant to log user errors.</remarks>
    internal sealed class Logger<TSource> : EventSource
    {
        private Logger(): base(typeof(TSource).FullName)
        {
        }

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static Logger<TSource> Instance { get; } = new();

        public static EventSourceWriter Debug { get; } = new EventSourceWriter(Instance, EventLevel.Verbose);

        public static EventSourceWriter Info { get; } = new EventSourceWriter(Instance, EventLevel.Informational);

        public static EventSourceWriter Warning { get; } = new EventSourceWriter(Instance, EventLevel.Warning);

        public static EventSourceWriter Error { get; } = new EventSourceWriter(Instance, EventLevel.Error);
    }

    /// <summary>
    /// Defines some extensions methods over the <see cref="EventSource"/> class.
    /// </summary>
    internal sealed class EventSourceWriter(EventSource target, EventLevel level)
    {
        private readonly EventSourceOptions _options = new() { Level = level };

        /// <summary>
        /// Logs a message with the given <see cref="Level"/>. The <paramref name="attributesFactory"/> is called only when the log <see cref="Level"/> is enabled.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The 'attributesFactory' won't return composite types")]
        public void Write<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T, TParam>(string eventName, TParam p, Func<TParam, T> attributesFactory)
        {
            if (target.IsEnabled(Level, EventKeywords.None))
                target.Write(eventName, _options, attributesFactory(p));
        }

        /// <summary>
        /// Logs a message with the given <see cref="Level"/>. The <paramref name="attributesFactory"/> is called only when the log <see cref="Level"/> is enabled.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The 'attributesFactory' won't return composite types")]
        public void Write<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T, TParam_1, TParam_2>(string eventName, TParam_1 p1, TParam_2 p2, Func<TParam_1, TParam_2, T> attributesFactory)
        {
            if (target.IsEnabled(Level, EventKeywords.None))
                target.Write(eventName, _options, attributesFactory(p1, p2));
        }

        public EventLevel Level => _options.Level;

        public override string ToString() => Level.ToString();
    }
}
