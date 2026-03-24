/********************************************************************************
* EventListeners.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace NanoRoute.Tests
{
    using Internals;

    internal sealed class DebugEventListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == RouterEventSource.EVENT_SOURCE_NAME)
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == RouterEventSource.EVENT_SOURCE_NAME)
            {
                string dump = $"[{eventData.EventSource.Name} - {eventData.Level}] {eventData.EventName}:";

                if (eventData.PayloadNames is { Count: > 0 } names && eventData.Payload is { Count: > 0 } values)
                    for (int i = 0; i < names.Count; i++)
                        dump += $"{Environment.NewLine}{names[i]} = {values[i]}";

                Debug.WriteLine(dump);
            }
        }
    }

    internal sealed class TestEventListener(EventLevel level) : EventListener
    {
        private readonly List<EventWrittenEventArgs> _events = [];

        public List<EventWrittenEventArgs> Events => _events;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == RouterEventSource.EVENT_SOURCE_NAME)
                EnableEvents(eventSource, level);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData) => _events.Add(eventData);
    }
}
