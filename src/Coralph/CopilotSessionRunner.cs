using System.Text;
using Serilog;
using GitHub.Copilot.SDK;

namespace Coralph;

internal sealed class CopilotSessionRunner : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly CopilotSession _session;
    private readonly CopilotSessionEventRouter _router;
    private readonly IDisposable _subscription;
    private bool _started;
    private bool _disposed;

    private CopilotSessionRunner(
        CopilotClient client,
        CopilotSession session,
        CopilotSessionEventRouter router,
        IDisposable subscription,
        bool started)
    {
        _client = client;
        _session = session;
        _router = router;
        _subscription = subscription;
        _started = started;
    }

    internal static async Task<CopilotSessionRunner> CreateAsync(LoopOptions opt, EventStreamWriter? eventStream)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(opt.CliPath)) clientOptions.CliPath = opt.CliPath;
        if (!string.IsNullOrWhiteSpace(opt.CliUrl)) clientOptions.CliUrl = opt.CliUrl;
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken)) clientOptions.GithubToken = opt.CopilotToken;

        var client = new CopilotClient(clientOptions);
        var started = false;
        CopilotSession? session = null;
        IDisposable? subscription = null;

        try
        {
            await client.StartAsync();
            started = true;

            var customTools = CustomTools.GetDefaultTools(opt.IssuesFile, opt.ProgressFile, opt.GeneratedTasksFile);
            var permissionPolicy = new PermissionPolicy(opt, eventStream);

            session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = opt.Model,
                Streaming = true,
                Tools = customTools,
                OnPermissionRequest = permissionPolicy.HandleAsync,
            });

            var router = new CopilotSessionEventRouter(opt, eventStream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: true);
            subscription = session.On(router.HandleEvent);

            return new CopilotSessionRunner(client, session, router, subscription, started);
        }
        catch
        {
            if (subscription is not null)
            {
                subscription.Dispose();
            }

            if (session is not null)
            {
                await session.DisposeAsync();
            }

            if (started)
            {
                try
                {
                    await client.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to stop Copilot client");
                }
            }

            await client.DisposeAsync();
            throw;
        }
    }

    internal async Task<string> RunTurnAsync(string prompt, CancellationToken ct, int turn)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CopilotSessionRunner));
        }

        var state = _router.StartTurn(turn);
        try
        {
            await _session.SendAsync(new MessageOptions { Prompt = prompt });

            using (ct.Register(() => state.Done.TrySetCanceled(ct)))
            {
                await state.Done.Task;
            }

            return state.Output.ToString().Trim();
        }
        finally
        {
            _router.EndTurn();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _router.EmitSessionEndIfNeeded("disposed");

        try
        {
            _subscription.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to dispose Copilot session subscription");
        }

        await _session.DisposeAsync();

        if (_started)
        {
            try
            {
                await _client.StopAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to stop Copilot client");
            }
        }

        await _client.DisposeAsync();
    }
}

internal sealed class CopilotSessionEventRouter(
    LoopOptions opt,
    EventStreamWriter? eventStream,
    bool emitSessionEndOnIdle,
    bool emitSessionEndOnDispose)
{
    private readonly LoopOptions _opt = opt ?? throw new ArgumentNullException(nameof(opt));
    private readonly EventStreamWriter? _eventStream = eventStream;
    private readonly bool _emitSessionEndOnIdle = emitSessionEndOnIdle;
    private readonly bool _emitSessionEndOnDispose = emitSessionEndOnDispose;

    private TurnState? _turnState;
    private string? _copilotSessionId;
    private Exception? _pendingSessionError;

    internal TurnState StartTurn(int? turn)
    {
        if (_pendingSessionError is not null)
        {
            throw new InvalidOperationException("Copilot session is in an error state.", _pendingSessionError);
        }

        if (_turnState is not null)
        {
            throw new InvalidOperationException("A Copilot turn is already in progress.");
        }

        var state = new TurnState(turn);
        _turnState = state;
        return state;
    }

    internal void EndTurn()
    {
        _turnState = null;
    }

    internal void EmitSessionEndIfNeeded(string reason)
    {
        if (!_emitSessionEndOnDispose)
        {
            return;
        }

        Emit("copilot_session_end", fields: _eventStream is null ? null : new Dictionary<string, object?>
        {
            ["copilotSessionId"] = _copilotSessionId,
            ["reason"] = reason
        });
    }

    internal void HandleEvent(SessionEvent evt)
    {
        var state = _turnState;
        switch (evt)
        {
            case SessionStartEvent sessionStart:
                _copilotSessionId = sessionStart.Data.SessionId;
                Emit("copilot_session_start", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["copilotSessionId"] = sessionStart.Data.SessionId,
                    ["selectedModel"] = sessionStart.Data.SelectedModel,
                    ["startTime"] = sessionStart.Data.StartTime,
                    ["version"] = sessionStart.Data.Version,
                    ["copilotVersion"] = sessionStart.Data.CopilotVersion,
                    ["producer"] = sessionStart.Data.Producer
                }, state: state);
                break;
            case AssistantTurnStartEvent assistantTurnStart:
                Emit("assistant_turn_start", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["assistantTurnId"] = assistantTurnStart.Data.TurnId
                }, state: state);
                break;
            case AssistantTurnEndEvent assistantTurnEnd:
                Emit("assistant_turn_end", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["assistantTurnId"] = assistantTurnEnd.Data.TurnId
                }, state: state);
                break;
            case AssistantMessageDeltaEvent delta:
                if (state is null)
                {
                    break;
                }
                {
                    var messageId = ResolveAssistantMessageId(state, delta.Data.MessageId);
                    EnsureAssistantMessageStart(state, messageId, delta.Data.ParentToolCallId);
                    Emit("message_update", messageId: messageId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?>
                        {
                            ["id"] = messageId,
                            ["role"] = "assistant"
                        },
                        ["delta"] = delta.Data.DeltaContent,
                        ["parentToolCallId"] = delta.Data.ParentToolCallId,
                        ["totalResponseSizeBytes"] = delta.Data.TotalResponseSizeBytes
                    }, state: state);
                }
                if (!state.InAssistantMode)
                {
                    if (state.InReasoningMode)
                    {
                        ConsoleOutput.WriteLine();
                        state.InReasoningMode = false;
                    }
                    state.InAssistantMode = true;
                }
                if (_opt.ColorizedOutput)
                {
                    ConsoleOutput.WriteAssistant(delta.Data.DeltaContent);
                }
                else
                {
                    ConsoleOutput.Write(delta.Data.DeltaContent);
                }
                state.Output.Append(delta.Data.DeltaContent);
                break;
            case AssistantReasoningDeltaEvent reasoning:
                if (!_opt.ShowReasoning || state is null)
                {
                    break;
                }
                {
                    var reasoningId = ResolveReasoningId(state, reasoning.Data.ReasoningId);
                    EnsureReasoningMessageStart(state, reasoningId);
                    Emit("message_update", messageId: reasoningId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?>
                        {
                            ["id"] = reasoningId,
                            ["role"] = "reasoning"
                        },
                        ["delta"] = reasoning.Data.DeltaContent
                    }, state: state);
                }
                if (!state.InReasoningMode)
                {
                    if (state.InAssistantMode)
                    {
                        ConsoleOutput.WriteLine();
                        state.InAssistantMode = false;
                    }
                    state.InReasoningMode = true;
                }
                if (_opt.ColorizedOutput)
                {
                    ConsoleOutput.WriteReasoning(reasoning.Data.DeltaContent);
                }
                else
                {
                    ConsoleOutput.Write(reasoning.Data.DeltaContent);
                }
                break;
            case ToolExecutionStartEvent toolStart:
                if (state is null)
                {
                    break;
                }
                state.ToolNamesByCallId[toolStart.Data.ToolCallId] = toolStart.Data.ToolName;
                state.ToolParentByCallId[toolStart.Data.ToolCallId] = toolStart.Data.ParentToolCallId;
                Emit("tool_execution_start", toolCallId: toolStart.Data.ToolCallId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["toolName"] = toolStart.Data.ToolName,
                    ["args"] = toolStart.Data.Arguments,
                    ["parentToolCallId"] = toolStart.Data.ParentToolCallId,
                    ["mcpToolName"] = toolStart.Data.McpToolName,
                    ["mcpServerName"] = toolStart.Data.McpServerName
                }, state: state);
                if (state.InReasoningMode || state.InAssistantMode)
                {
                    ConsoleOutput.WriteLine();
                    state.InReasoningMode = false;
                    state.InAssistantMode = false;
                }
                ConsoleOutput.WriteToolStart(toolStart.Data.ToolName);
                break;
            case ToolExecutionProgressEvent toolProgress:
                if (state is null)
                {
                    break;
                }
                state.ToolNamesByCallId.TryGetValue(toolProgress.Data.ToolCallId, out var progressToolName);
                Emit("tool_execution_update", toolCallId: toolProgress.Data.ToolCallId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["toolName"] = progressToolName,
                    ["updateType"] = "progress",
                    ["progressMessage"] = toolProgress.Data.ProgressMessage
                }, state: state);
                break;
            case ToolExecutionPartialResultEvent toolPartial:
                if (state is null)
                {
                    break;
                }
                state.ToolNamesByCallId.TryGetValue(toolPartial.Data.ToolCallId, out var partialToolName);
                Emit("tool_execution_update", toolCallId: toolPartial.Data.ToolCallId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["toolName"] = partialToolName,
                    ["updateType"] = "partial_result",
                    ["partialResult"] = toolPartial.Data.PartialOutput
                }, state: state);
                break;
            case ToolExecutionCompleteEvent toolComplete:
                if (state is null)
                {
                    break;
                }
                state.ToolNamesByCallId.TryGetValue(toolComplete.Data.ToolCallId, out var completeToolName);
                state.ToolParentByCallId.TryGetValue(toolComplete.Data.ToolCallId, out var completeParentToolCallId);
                Emit("tool_execution_end", toolCallId: toolComplete.Data.ToolCallId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["toolName"] = completeToolName,
                    ["success"] = toolComplete.Data.Success,
                    ["isError"] = !toolComplete.Data.Success,
                    ["error"] = toolComplete.Data.Error is null ? null : new Dictionary<string, object?>
                    {
                        ["code"] = toolComplete.Data.Error.Code,
                        ["message"] = toolComplete.Data.Error.Message
                    },
                    ["result"] = toolComplete.Data.Result?.Content,
                    ["detailedResult"] = toolComplete.Data.Result?.DetailedContent,
                    ["isUserRequested"] = toolComplete.Data.IsUserRequested,
                    ["parentToolCallId"] = completeParentToolCallId
                }, state: state);
                var toolOutput = toolComplete.Data.Result?.Content;
                if (!string.IsNullOrWhiteSpace(toolOutput))
                {
                    var resolvedToolName = completeToolName ?? "unknown";
                    if (CopilotRunner.IsIgnorableToolOutput(resolvedToolName, toolOutput))
                    {
                        state.ToolNamesByCallId.Remove(toolComplete.Data.ToolCallId);
                        state.ToolParentByCallId.Remove(toolComplete.Data.ToolCallId);
                        break;
                    }
                    var display = CopilotRunner.SummarizeToolOutput(toolOutput);
                    ConsoleOutput.WriteToolComplete(resolvedToolName, display);
                }
                state.ToolNamesByCallId.Remove(toolComplete.Data.ToolCallId);
                state.ToolParentByCallId.Remove(toolComplete.Data.ToolCallId);
                break;
            case SessionCompactionStartEvent:
                Emit("compaction_start", state: state);
                break;
            case SessionCompactionCompleteEvent compaction:
                Emit("compaction_end", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["success"] = compaction.Data.Success,
                    ["error"] = compaction.Data.Error,
                    ["messagesRemoved"] = compaction.Data.MessagesRemoved,
                    ["tokensRemoved"] = compaction.Data.TokensRemoved,
                    ["preCompactionTokens"] = compaction.Data.PreCompactionTokens,
                    ["postCompactionTokens"] = compaction.Data.PostCompactionTokens,
                    ["preCompactionMessagesLength"] = compaction.Data.PreCompactionMessagesLength,
                    ["summaryContent"] = compaction.Data.SummaryContent
                }, state: state);
                break;
            case SessionSnapshotRewindEvent rewind:
                Emit("retry", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["reason"] = "snapshot_rewind",
                    ["eventsRemoved"] = rewind.Data.EventsRemoved,
                    ["upToEventId"] = rewind.Data.UpToEventId
                }, state: state);
                break;
            case SessionUsageInfoEvent sessionUsage:
                Emit("session_usage", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["currentTokens"] = sessionUsage.Data.CurrentTokens,
                    ["tokenLimit"] = sessionUsage.Data.TokenLimit,
                    ["messagesLength"] = sessionUsage.Data.MessagesLength
                }, state: state);
                break;
            case AssistantUsageEvent assistantUsage:
                Emit("usage", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["apiCallId"] = assistantUsage.Data.ApiCallId,
                    ["providerCallId"] = assistantUsage.Data.ProviderCallId,
                    ["model"] = assistantUsage.Data.Model,
                    ["inputTokens"] = assistantUsage.Data.InputTokens,
                    ["outputTokens"] = assistantUsage.Data.OutputTokens,
                    ["cacheReadTokens"] = assistantUsage.Data.CacheReadTokens,
                    ["cacheWriteTokens"] = assistantUsage.Data.CacheWriteTokens,
                    ["cost"] = assistantUsage.Data.Cost,
                    ["duration"] = assistantUsage.Data.Duration,
                    ["initiator"] = assistantUsage.Data.Initiator,
                    ["quotaSnapshots"] = assistantUsage.Data.QuotaSnapshots
                }, state: state);
                break;
            case AssistantMessageEvent:
                if (state is null)
                {
                    break;
                }
                {
                    var messageEvent = (AssistantMessageEvent)evt;
                    var messageId = ResolveAssistantMessageId(state, messageEvent.Data.MessageId);
                    EnsureAssistantMessageStart(state, messageId, messageEvent.Data.ParentToolCallId);
                    Emit("message_end", messageId: messageId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?>
                        {
                            ["id"] = messageId,
                            ["role"] = "assistant",
                            ["content"] = messageEvent.Data.Content,
                            ["parentToolCallId"] = messageEvent.Data.ParentToolCallId,
                            ["toolRequests"] = MapToolRequests(messageEvent.Data.ToolRequests)
                        }
                    }, state: state);
                    state.ActiveAssistantMessageId = null;
                }
                ConsoleOutput.WriteLine();
                state.InReasoningMode = false;
                state.InAssistantMode = false;
                break;
            case AssistantReasoningEvent:
                if (state is null)
                {
                    break;
                }
                if (_opt.ShowReasoning)
                {
                    var reasoningEvent = (AssistantReasoningEvent)evt;
                    var reasoningId = ResolveReasoningId(state, reasoningEvent.Data.ReasoningId);
                    EnsureReasoningMessageStart(state, reasoningId);
                    Emit("message_end", messageId: reasoningId, fields: _eventStream is null ? null : new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?>
                        {
                            ["id"] = reasoningId,
                            ["role"] = "reasoning",
                            ["content"] = reasoningEvent.Data.Content
                        }
                    }, state: state);
                    state.ActiveReasoningId = null;
                }
                ConsoleOutput.WriteLine();
                state.InReasoningMode = false;
                state.InAssistantMode = false;
                break;
            case SessionErrorEvent err:
                Emit("session_error", fields: _eventStream is null ? null : new Dictionary<string, object?>
                {
                    ["errorType"] = err.Data.ErrorType,
                    ["message"] = err.Data.Message,
                    ["stack"] = err.Data.Stack,
                    ["copilotSessionId"] = _copilotSessionId
                }, state: state);
                var exception = new InvalidOperationException(err.Data.Message);
                _pendingSessionError = exception;
                state?.Done.TrySetException(exception);
                break;
            case SessionIdleEvent:
                if (_emitSessionEndOnIdle)
                {
                    Emit("copilot_session_end", fields: _eventStream is null ? null : new Dictionary<string, object?>
                    {
                        ["copilotSessionId"] = _copilotSessionId,
                        ["reason"] = "idle"
                    }, state: state);
                }
                else
                {
                    Emit("copilot_session_idle", fields: _eventStream is null ? null : new Dictionary<string, object?>
                    {
                        ["copilotSessionId"] = _copilotSessionId,
                        ["reason"] = "idle"
                    }, state: state);
                }
                state?.Done.TrySetResult();
                break;
        }
    }

    private void Emit(
        string type,
        int? eventTurn = null,
        string? messageId = null,
        string? toolCallId = null,
        IDictionary<string, object?>? fields = null,
        TurnState? state = null)
    {
        if (_eventStream is null)
        {
            return;
        }

        _eventStream.Emit(type, eventTurn ?? state?.Turn, messageId, toolCallId, fields);
    }

    private static List<Dictionary<string, object?>>? MapToolRequests(AssistantMessageDataToolRequestsItem[]? requests)
    {
        if (requests is null || requests.Length == 0)
        {
            return null;
        }

        var result = new List<Dictionary<string, object?>>();
        foreach (var request in requests)
        {
            result.Add(new Dictionary<string, object?>
            {
                ["toolCallId"] = request.ToolCallId,
                ["name"] = request.Name,
                ["type"] = request.Type?.ToString(),
                ["arguments"] = request.Arguments
            });
        }

        return result;
    }

    private string ResolveAssistantMessageId(TurnState state, string? messageId)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            state.ActiveAssistantMessageId = messageId;
            return messageId;
        }

        if (!string.IsNullOrWhiteSpace(state.ActiveAssistantMessageId))
        {
            return state.ActiveAssistantMessageId;
        }

        state.ActiveAssistantMessageId = $"assistant-{++state.SyntheticMessageCounter}";
        return state.ActiveAssistantMessageId;
    }

    private string ResolveReasoningId(TurnState state, string? reasoningId)
    {
        if (!string.IsNullOrWhiteSpace(reasoningId))
        {
            state.ActiveReasoningId = reasoningId;
            return reasoningId;
        }

        if (!string.IsNullOrWhiteSpace(state.ActiveReasoningId))
        {
            return state.ActiveReasoningId;
        }

        state.ActiveReasoningId = $"reasoning-{++state.SyntheticReasoningCounter}";
        return state.ActiveReasoningId;
    }

    private void EnsureAssistantMessageStart(TurnState state, string messageId, string? parentToolCallId)
    {
        if (!state.StartedAssistantMessages.Add(messageId))
        {
            return;
        }

        Emit("message_start", messageId: messageId, fields: _eventStream is null ? null : new Dictionary<string, object?>
        {
            ["message"] = new Dictionary<string, object?>
            {
                ["id"] = messageId,
                ["role"] = "assistant",
                ["parentToolCallId"] = parentToolCallId
            }
        }, state: state);
    }

    private void EnsureReasoningMessageStart(TurnState state, string reasoningId)
    {
        if (!state.StartedReasoningMessages.Add(reasoningId))
        {
            return;
        }

        Emit("message_start", messageId: reasoningId, fields: _eventStream is null ? null : new Dictionary<string, object?>
        {
            ["message"] = new Dictionary<string, object?>
            {
                ["id"] = reasoningId,
                ["role"] = "reasoning"
            }
        }, state: state);
    }

    internal sealed class TurnState(int? turn)
    {
        internal int? Turn { get; } = turn;
        internal TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal StringBuilder Output { get; } = new();
        internal bool InReasoningMode { get; set; }
        internal bool InAssistantMode { get; set; }
        internal string? ActiveAssistantMessageId { get; set; }
        internal string? ActiveReasoningId { get; set; }
        internal int SyntheticMessageCounter { get; set; }
        internal int SyntheticReasoningCounter { get; set; }
        internal HashSet<string> StartedAssistantMessages { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> StartedReasoningMessages { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, string> ToolNamesByCallId { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, string?> ToolParentByCallId { get; } = new(StringComparer.Ordinal);
    }
}
