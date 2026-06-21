/********************************************************************************
* DebugEventListener.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace NanoRoute.HttpListener.Tests
{
    internal sealed class DebugEventListener<Target>(EventLevel level) : EventListener
    {
        public List<EventWrittenEventArgs> Events { get; } = [];

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == typeof(Target).FullName)
                EnableEvents(eventSource, level);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == typeof(Target).FullName)
            {
                string dump = $"[{eventData.EventSource.Name} - {eventData.Level}] {eventData.EventName}:";

                if (eventData.PayloadNames is { Count: > 0 } names && eventData.Payload is { Count: > 0 } values)
                    for (int i = 0; i < names.Count; i++)
                        dump += $"{Environment.NewLine}{names[i]} = {values[i]}";

                Debug.WriteLine(dump);

                Events.Add(eventData);
            }
        }
    }
}
