using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coralph;

internal sealed class EventStreamWriter(
    TextWriter writer,
    string sessionId,
    int version = EventStreamWriter.SchemaVersion,
    bool leaveOpen = true,
    bool flushEachEvent = false)
{
    internal const int SchemaVersion = 1;
    private const int FlushBatchSize = 32;
    private static readonly HashSet<string> ImmediateFlushTypes = new(StringComparer.Ordinal)
    {
        "session",
        "turn_end",
        "agent_end",
        "event_error"
    };

    private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly bool _flushEachEvent = flushEachEvent;
    private long _sequence;
    private int _pendingWritesSinceFlush;

    internal string SessionId { get; } = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
    internal int Version { get; } = version;
    internal bool LeaveOpen { get; } = leaveOpen;

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
            _pendingWritesSinceFlush++;

            if (ShouldFlush(payload))
            {
                _writer.Flush();
                _pendingWritesSinceFlush = 0;
            }
        }
    }

    private bool ShouldFlush(Dictionary<string, object?> payload)
    {
        if (_flushEachEvent)
        {
            return true;
        }

        if (_pendingWritesSinceFlush >= FlushBatchSize)
        {
            return true;
        }

        if (payload.TryGetValue("type", out var typeObj) && typeObj is string type)
        {
            return ImmediateFlushTypes.Contains(type);
        }

        return false;
    }
}
