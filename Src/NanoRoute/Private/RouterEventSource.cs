/********************************************************************************
* RouterEventSource.cs                                                          *
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
    [EventSource(Name = EVENT_SOURCE_NAME)]
    internal sealed class RouterEventSource : EventSource
    {
        private RouterEventSource()
        {
        }

        /// <summary>
        /// The name associated with the event source declaration.
        /// </summary>
        public const string EVENT_SOURCE_NAME = "NanoRoute";

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static RouterEventSource Instance { get; } = new();

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
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We won't use composite types when calling this method")]
        public void Write<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T, TParam>(string eventName, Func<TParam, T> attributesFactory, TParam p)
        {
            if (target.IsEnabled(Level, EventKeywords.None))
                target.Write(eventName, _options, attributesFactory(p));
        }

        /// <summary>
        /// Logs a message with the given <see cref="Level"/>. The <paramref name="attributesFactory"/> is called only when the log <see cref="Level"/> is enabled.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We won't use composite types when calling this method")]
        public void Write<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T, TParam_1, TParam_2>(string eventName, Func<TParam_1, TParam_2, T> attributesFactory, TParam_1 p1, TParam_2 p2)
        {
            if (target.IsEnabled(Level, EventKeywords.None))
                target.Write(eventName, _options, attributesFactory(p1, p2));
        }

        public EventLevel Level { get; } = level;

        public override string ToString() => Level.ToString();
    }
}
