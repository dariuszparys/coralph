using System.Globalization;
using System.Text;
using System.Text.Json;
using Coralph;
using Coralph.Ui;
using GitHub.Copilot.SDK;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Coralph.Tests;

[Collection("ConsoleOutput")]
public class CopilotSessionEventRouterTests
{
    [Fact]
    public async Task HandleEvent_WithReasoningAndAssistantDeltas_EmitsExpectedJsonLAndConsoleOutput()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-1");
        var options = new LoopOptions
        {
            ShowReasoning = true,
            ColorizedOutput = false
        };

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);
            var turn = router.StartTurn(3);

            router.HandleEvent(CreateSessionStartEvent("session-1"));
            router.HandleEvent(new AssistantTurnStartEvent
            {
                Data = new AssistantTurnStartData
                {
                    TurnId = "turn-1",
                    InteractionId = "interaction-1"
                }
            });
            router.HandleEvent(new AssistantReasoningDeltaEvent
            {
                Data = new AssistantReasoningDeltaData
                {
                    ReasoningId = "reasoning-1",
                    DeltaContent = "Thinking"
                }
            });
            router.HandleEvent(new AssistantMessageDeltaEvent
            {
                Data = new AssistantMessageDeltaData
                {
                    MessageId = "message-1",
                    DeltaContent = "Answer",
                    ParentToolCallId = "tool-1"
                }
            });

            Assert.False(turn.Done.Task.IsCompleted);
            Assert.Equal("Thinking\nAnswer", backend.Output.ToString());

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal(6, lines.Count);
            Assert.Equal(new[]
            {
                "copilot_session_start",
                "assistant_turn_start",
                "message_start",
                "message_update",
                "message_start",
                "message_update"
            }, lines.Select(line => line.GetProperty("type").GetString()).ToArray());

            Assert.Equal("session-1", lines[0].GetProperty("copilotSessionId").GetString());
            Assert.Equal("reasoning-1", lines[2].GetProperty("messageId").GetString());
            Assert.Equal("reasoning", lines[2].GetProperty("message").GetProperty("role").GetString());
            Assert.Equal("Thinking", lines[3].GetProperty("delta").GetString());
            Assert.Equal("message-1", lines[4].GetProperty("messageId").GetString());
            Assert.Equal("assistant", lines[4].GetProperty("message").GetProperty("role").GetString());
            Assert.Equal("Answer", lines[5].GetProperty("delta").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_ToolExecutionComplete_ForReportIntent_SuppressesConsoleSummary()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-2");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);
            router.StartTurn(4);

            router.HandleEvent(CreateSessionStartEvent("session-2"));
            router.HandleEvent(new ToolExecutionStartEvent
            {
                Data = new ToolExecutionStartData
                {
                    ToolCallId = "tool-1",
                    ToolName = "report_intent",
                    Arguments = new Dictionary<string, object?>
                    {
                        ["intent"] = "test"
                    }
                }
            });
            router.HandleEvent(new ToolExecutionCompleteEvent
            {
                Data = new ToolExecutionCompleteData
                {
                    ToolCallId = "tool-1",
                    Success = true,
                    Result = new ToolExecutionCompleteResult
                    {
                        Content = "Intent logged"
                    }
                }
            });

            Assert.Contains("[tool-start:report_intent]", backend.Output.ToString());
            Assert.DoesNotContain("Intent logged", backend.Output.ToString());

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal(3, lines.Count);
            Assert.Equal("tool_execution_start", lines[1].GetProperty("type").GetString());
            Assert.Equal("tool_execution_end", lines[2].GetProperty("type").GetString());
            Assert.Equal("report_intent", lines[1].GetProperty("toolName").GetString());
            Assert.Equal("Intent logged", lines[2].GetProperty("result").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_SessionStart_UpdatesSelectedModelOnConsoleOutput()
    {
        var backend = new CapturingConsoleBackend();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(new LoopOptions(), eventStream: null, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);

            router.HandleEvent(CreateSessionStartEvent("session-model"));

            Assert.Equal("gpt-4.1", backend.SelectedModel);
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_SessionError_CompletesTurnAndRejectsFurtherTurns()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-3");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);
            var turn = router.StartTurn(5);

            router.HandleEvent(CreateSessionStartEvent("session-3"));
            router.HandleEvent(new SessionErrorEvent
            {
                Data = new SessionErrorData
                {
                    ErrorType = "fatal",
                    Message = "boom",
                    Stack = "stack",
                    ProviderCallId = "provider-call-1"
                }
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => turn.Done.Task);
            Assert.Equal("boom", exception.Message);
            Assert.Equal("Copilot session is in an error state.", Assert.Throws<InvalidOperationException>(() => router.StartTurn(6)).Message);

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal("session_error", lines[1].GetProperty("type").GetString());
            Assert.Equal("boom", lines[1].GetProperty("message").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Theory]
    [InlineData(false, "copilot_session_idle")]
    [InlineData(true, "copilot_session_end")]
    public async Task HandleEvent_SessionIdle_EmitsConfiguredTerminalEventAndCompletesTurn(
        bool emitSessionEndOnIdle,
        string expectedType)
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-4");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle, emitSessionEndOnDispose: false);
            var turn = router.StartTurn(7);

            router.HandleEvent(CreateSessionStartEvent("session-4"));
            router.HandleEvent(new SessionIdleEvent
            {
                Data = new SessionIdleData()
            });

            await turn.Done.Task;

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal(expectedType, lines[1].GetProperty("type").GetString());
            Assert.Equal("idle", lines[1].GetProperty("reason").GetString());
            Assert.Equal("session-4", lines[1].GetProperty("copilotSessionId").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_SystemNotification_EmitsStructuredEvent()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-5");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);

            router.HandleEvent(CreateSessionStartEvent("session-5"));
            router.HandleEvent(new SystemNotificationEvent
            {
                Data = new SystemNotificationData
                {
                    Content = "<system_notification>Shell completed</system_notification>",
                    Kind = new SystemNotificationShellCompleted
                    {
                        ShellId = "shell-1",
                        ExitCode = 0,
                        Description = "dotnet test"
                    }
                }
            });

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal("system_notification", lines[1].GetProperty("type").GetString());
            Assert.Equal("<system_notification>Shell completed</system_notification>", lines[1].GetProperty("content").GetString());
            Assert.Equal("shell_completed", lines[1].GetProperty("kind").GetProperty("type").GetString());
            Assert.Equal("shell-1", lines[1].GetProperty("kind").GetProperty("shellId").GetString());
            Assert.Equal(0, lines[1].GetProperty("kind").GetProperty("exitCode").GetInt32());
            Assert.Equal("dotnet test", lines[1].GetProperty("kind").GetProperty("description").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_SystemNotification_WithUnknownKind_EmitsTypeFallback()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-unknown-notification");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);

            router.HandleEvent(CreateSessionStartEvent("session-unknown-notification"));
            router.HandleEvent(new SystemNotificationEvent
            {
                Data = new SystemNotificationData
                {
                    Content = "<system_notification>New SDK notification</system_notification>",
                    Kind = new SystemNotification
                    {
                        Type = "new_notification_kind"
                    }
                }
            });

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal("system_notification", lines[1].GetProperty("type").GetString());
            Assert.Equal("new_notification_kind", lines[1].GetProperty("kind").GetProperty("type").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_AssistantMessage_WithToolRequests_EmitsStableToolRequestFields()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-tool-requests");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);
            router.StartTurn(8);

            router.HandleEvent(new AssistantMessageEvent
            {
                Data = new AssistantMessageData
                {
                    MessageId = "message-tool-request",
                    Content = "I need a tool",
                    ToolRequests =
                    [
                        new AssistantMessageToolRequest
                        {
                            ToolCallId = "tool-call-1",
                            Name = "list_open_issues",
                            Type = AssistantMessageToolRequestType.Function,
                            Arguments = new Dictionary<string, object?>
                            {
                                ["includeClosed"] = false
                            },
                            ToolTitle = "List issues",
                            McpServerName = "coralph",
                            IntentionSummary = "Read current issue state"
                        }
                    ]
                }
            });

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal("message_end", lines[1].GetProperty("type").GetString());
            var request = lines[1]
                .GetProperty("message")
                .GetProperty("toolRequests")[0];

            Assert.Equal("tool-call-1", request.GetProperty("toolCallId").GetString());
            Assert.Equal("list_open_issues", request.GetProperty("name").GetString());
            Assert.Equal("Function", request.GetProperty("type").GetString());
            Assert.False(request.GetProperty("arguments").GetProperty("includeClosed").GetBoolean());
            Assert.Equal("List issues", request.GetProperty("toolTitle").GetString());
            Assert.Equal("coralph", request.GetProperty("mcpServerName").GetString());
            Assert.Equal("Read current issue state", request.GetProperty("intentionSummary").GetString());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    [Fact]
    public async Task HandleEvent_AssistantUsage_EmitsReasoningTokens()
    {
        var backend = new CapturingConsoleBackend();
        var output = new StringWriter();
        var stream = new EventStreamWriter(output, "session-usage");
        var options = new LoopOptions();

        await ConsoleOutput.UseBackendAsync(backend);
        try
        {
            var router = new CopilotSessionEventRouter(options, stream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: false);

            router.HandleEvent(new AssistantUsageEvent
            {
                Data = new AssistantUsageData
                {
                    Model = "gpt-5.1-codex",
                    InputTokens = 100,
                    OutputTokens = 20,
                    ReasoningTokens = 7
                }
            });

            var lines = ParseJsonLines(output.ToString());
            Assert.Equal("usage", lines[0].GetProperty("type").GetString());
            Assert.Equal(7d, lines[0].GetProperty("reasoningTokens").GetDouble());
        }
        finally
        {
            await ConsoleOutput.ResetAsync();
        }
    }

    private static SessionStartEvent CreateSessionStartEvent(string sessionId)
    {
        return new SessionStartEvent
        {
            Data = new SessionStartData
            {
                SessionId = sessionId,
                Version = 1,
                Producer = "test-suite",
                CopilotVersion = "1.0.0",
                StartTime = new DateTimeOffset(2026, 3, 23, 10, 30, 0, TimeSpan.Zero),
                SelectedModel = "gpt-4.1"
            }
        };
    }

    private static List<JsonElement> ParseJsonLines(string jsonl)
    {
        var lines = jsonl
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<JsonElement>(lines.Length);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            result.Add(doc.RootElement.Clone());
        }

        return result;
    }

    private sealed class CapturingConsoleBackend : IConsoleOutputBackend
    {
        private readonly TestConsole _console = new();
        private readonly StringBuilder _buffer = new();

        public bool UsesTui => false;
        public IAnsiConsole Out => _console;
        public IAnsiConsole Error => _console;
        public Task<ConsoleOutputBackendExit>? ExitTask => null;

        public string Output => _buffer.ToString();
        public string? SelectedModel { get; private set; }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr)
        {
        }

        public void Reset()
        {
            _buffer.Clear();
        }

        public void Write(string text)
        {
            _buffer.Append(text);
        }

        public void WriteLine()
        {
            _buffer.AppendLine();
        }

        public void WriteLine(string text)
        {
            _buffer.AppendLine(text);
        }

        public void WriteError(string text)
        {
            _buffer.Append(text);
        }

        public void WriteErrorLine()
        {
            _buffer.AppendLine();
        }

        public void WriteErrorLine(string text)
        {
            _buffer.AppendLine(text);
        }

        public void WriteWarningLine(string text)
        {
            _buffer.AppendLine(text);
        }

        public void MarkupLine(string markup)
        {
            _buffer.AppendLine(markup);
        }

        public void MarkupLineInterpolated(FormattableString markup)
        {
            _buffer.AppendLine(markup.ToString(CultureInfo.InvariantCulture));
        }

        public void WriteReasoning(string text)
        {
            _buffer.Append(text);
        }

        public void WriteAssistant(string text)
        {
            _buffer.Append(text);
        }

        public void WriteToolStart(string toolName)
        {
            _buffer.Append($"[tool-start:{toolName}]");
        }

        public void WriteToolComplete(string toolName, string summary)
        {
            _buffer.Append($"[tool-complete:{toolName}:{summary}]");
        }

        public void WriteSectionSeparator(string title)
        {
            _buffer.AppendLine($"[section:{title}]");
        }

        public void SetSelectedModel(string? model)
        {
            SelectedModel = model;
        }

        public void RefreshGeneratedTasks()
        {
        }

        public Task<int> PromptSelectionAsync(string title, IReadOnlyList<string> options, int defaultIndex, CancellationToken ct = default)
        {
            return Task.FromResult(defaultIndex);
        }
    }
}
