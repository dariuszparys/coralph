using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coralph;

internal sealed class EventStreamWriter
{
    internal const int SchemaVersion = 1;

    private readonly TextWriter _writer;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private long _sequence;

    internal EventStreamWriter(TextWriter writer, string sessionId, int version = SchemaVersion, bool leaveOpen = true)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        Version = version;
        LeaveOpen = leaveOpen;

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    internal string SessionId { get; }
    internal int Version { get; }
    internal bool LeaveOpen { get; }

    internal void WriteSessionHeader(string cwd)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "session",
            ["version"] = Version,
            ["id"] = SessionId,
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["cwd"] = cwd,
            ["seq"] = NextSequence()
        };

        Write(payload);
    }

    internal void Emit(
        string type,
        int? turn = null,
        string? messageId = null,
        string? toolCallId = null,
        IDictionary<string, object?>? fields = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["sessionId"] = SessionId,
            ["seq"] = NextSequence()
        };

        if (turn.HasValue)
        {
            payload["turn"] = turn.Value;
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            payload["messageId"] = messageId;
        }

        if (!string.IsNullOrWhiteSpace(toolCallId))
        {
            payload["toolCallId"] = toolCallId;
        }

        if (fields is not null)
        {
            foreach (var (key, value) in fields)
            {
                if (!payload.ContainsKey(key))
                {
                    payload[key] = value;
                }
            }
        }

        Write(payload);
    }

    private long NextSequence() => Interlocked.Increment(ref _sequence);

    private void Write(Dictionary<string, object?> payload)
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(payload, _jsonOptions);
        }
        catch (Exception ex)
        {
            var fallback = new Dictionary<string, object?>
            {
                ["type"] = "event_error",
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["sessionId"] = SessionId,
                ["seq"] = NextSequence(),
                ["error"] = ex.Message,
                ["originalType"] = payload.TryGetValue("type", out var originalType) ? originalType : null
            };
            json = JsonSerializer.Serialize(fallback, _jsonOptions);
        }

        lock (_lock)
        {
            _writer.WriteLine(json);
            _writer.Flush();
        }
    }
}
