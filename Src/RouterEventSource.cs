/********************************************************************************
* RouterEventSource.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace NanoRoute
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
        public static RouterEventSource Log { get; } = new();
    }

    /// <summary>
    /// Defines some extensions methods over the <see cref="EventSource"/> class.
    /// </summary>
    internal static class EventSourceExtensions
    {
        /// <summary>
        /// Logs a message with the given <paramref name="level"/>. The <paramref name="attributesFactory"/> is called only when the log <paramref name="level"/> is enabled.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We won't use composite types when calling this method")]
        public static void Write<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this EventSource src, string eventName, EventLevel level, Func<T> attributesFactory)
        {
            if (src.IsEnabled(level, EventKeywords.None))
                src.Write
                (
                    eventName,
                    new EventSourceOptions { Level = level },
                    attributesFactory()
                );
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void Debug<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this EventSource src, string eventName, Func<T> attributesFactory) =>
            src.Write(eventName, EventLevel.Verbose, attributesFactory);

        /// <summary>
        /// Logs an information message
        /// </summary>
        public static void Info<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this EventSource src, string eventName, Func<T> attributesFactory) =>
            src.Write(eventName, EventLevel.Informational, attributesFactory);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void Warn<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this EventSource src, string eventName, Func<T> attributesFactory) =>
            src.Write(eventName, EventLevel.Warning, attributesFactory);

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void Error<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this EventSource src, string eventName, Func<T> attributesFactory) =>
            src.Write(eventName, EventLevel.Error, attributesFactory);
    }
}
