using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Game;

internal sealed class DiagnosticLog : IDisposable
{
    private const string SchemaVersion = "gridworks.scope0b.diagnostic.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    private readonly StreamWriter _writer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly string _sessionId;
    private readonly string _variant;
    private long _sequence;

    public DiagnosticLog(string path, string sessionId, string variant)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        _sessionId = sessionId;
        _variant = variant;
    }

    public void Write(string eventName, bool accepted, string snapshotHash, object payload)
    {
        var row = new DiagnosticRow(
            SchemaVersion,
            checked(++_sequence),
            _clock.ElapsedMilliseconds,
            _sessionId,
            _variant,
            eventName,
            accepted,
            snapshotHash,
            payload);
        _writer.WriteLine(JsonSerializer.Serialize(row, JsonOptions));
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _clock.Stop();
    }

    private sealed record DiagnosticRow(
        [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
        [property: JsonPropertyName("sequence")] long Sequence,
        [property: JsonPropertyName("elapsedMs")] long ElapsedMs,
        [property: JsonPropertyName("sessionId")] string SessionId,
        [property: JsonPropertyName("variant")] string Variant,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("accepted")] bool Accepted,
        [property: JsonPropertyName("snapshotHash")] string SnapshotHash,
        [property: JsonPropertyName("payload")] object Payload);
}
