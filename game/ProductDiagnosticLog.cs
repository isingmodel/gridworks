using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Game;

internal sealed class ProductDiagnosticLog : IDisposable
{
    private const string SchemaVersion = "gridworks.product.heatwave-maintenance.diagnostic.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    private readonly StreamWriter _writer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly string _sessionId;
    private long _sequence;

    public ProductDiagnosticLog(string path, string sessionId)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        _sessionId = sessionId;
    }

    public void WriteReady(object payload) => Write("READY", true, payload);

    public void WriteCommand(bool accepted, object payload) => Write("COMMAND", accepted, payload);

    public void WriteFinal(object payload) => Write("FINAL", true, payload);

    public void Dispose()
    {
        _writer.Dispose();
        _clock.Stop();
    }

    private void Write(string eventName, bool accepted, object payload)
    {
        var row = new DiagnosticRow(
            SchemaVersion,
            checked(++_sequence),
            _clock.ElapsedMilliseconds,
            _sessionId,
            eventName,
            accepted,
            payload);
        _writer.WriteLine(JsonSerializer.Serialize(row, JsonOptions));
        _writer.Flush();
    }

    private sealed record DiagnosticRow(
        [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
        [property: JsonPropertyName("sequence")] long Sequence,
        [property: JsonPropertyName("elapsedMs")] long ElapsedMs,
        [property: JsonPropertyName("sessionId")] string SessionId,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("accepted")] bool Accepted,
        [property: JsonPropertyName("payload")] object Payload);
}
