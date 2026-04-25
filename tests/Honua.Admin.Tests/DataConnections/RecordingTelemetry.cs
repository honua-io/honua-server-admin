using System.Collections.Concurrent;
using System.Collections.Generic;
using Honua.Admin.Services.DataConnections;

namespace Honua.Admin.Tests.DataConnections;

public sealed class RecordingTelemetry : IDataConnectionTelemetry
{
    public ConcurrentBag<TelemetryEvent> Events { get; } = new();

    public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        Events.Add(new TelemetryEvent(eventName, properties, null));
    }

    public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
    {
        Events.Add(new TelemetryEvent(eventName, properties, elapsedMillis));
    }
}

public sealed record TelemetryEvent(string Name, IReadOnlyDictionary<string, object?>? Properties, long? ElapsedMillis);
