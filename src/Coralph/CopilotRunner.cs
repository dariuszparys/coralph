using System.Text;
using System.Linq;
using Serilog;
using GitHub.Copilot.SDK;

namespace Coralph;

internal static class CopilotRunner
{
    internal static async Task<string> RunOnceAsync(LoopOptions opt, string prompt, CancellationToken ct, EventStreamWriter? eventStream = null, int? turn = null)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(opt.CliPath)) clientOptions.CliPath = opt.CliPath;
        if (!string.IsNullOrWhiteSpace(opt.CliUrl)) clientOptions.CliUrl = opt.CliUrl;
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken)) clientOptions.GithubToken = opt.CopilotToken;

        await using var client = new CopilotClient(clientOptions);
        var started = false;
        string result;
        try
        {
            await client.StartAsync();
            started = true;

            var customTools = CustomTools.GetDefaultTools(opt.IssuesFile, opt.ProgressFile);

            await using (var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = opt.Model,
                Streaming = true,
                Tools = customTools,
                OnPermissionRequest = (request, invocation) =>
                    Task.FromResult(new PermissionRequestResult { Kind = "approved" }),
            }))
            {
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var output = new StringBuilder();

                bool inReasoningMode = false;
                bool inAssistantMode = false;
                string? activeAssistantMessageId = null;
                string? activeReasoningId = null;
                var syntheticMessageCounter = 0;
                var syntheticReasoningCounter = 0;
                var startedAssistantMessages = new HashSet<string>(StringComparer.Ordinal);
                var startedReasoningMessages = new HashSet<string>(StringComparer.Ordinal);
                var toolNamesByCallId = new Dictionary<string, string>(StringComparer.Ordinal);
                var toolParentByCallId = new Dictionary<string, string?>(StringComparer.Ordinal);
                string? copilotSessionId = null;

                void Emit(string type, int? eventTurn = null, string? messageId = null, string? toolCallId = null, IDictionary<string, object?>? fields = null)
                {
                    eventStream?.Emit(type, eventTurn ?? turn, messageId, toolCallId, fields);
                }

                string ResolveAssistantMessageId(string? messageId)
                {
                    if (!string.IsNullOrWhiteSpace(messageId))
                    {
                        activeAssistantMessageId = messageId;
                        return messageId;
                    }

                    if (!string.IsNullOrWhiteSpace(activeAssistantMessageId))
                    {
                        return activeAssistantMessageId;
                    }

                    activeAssistantMessageId = $"assistant-{++syntheticMessageCounter}";
                    return activeAssistantMessageId;
                }

                string ResolveReasoningId(string? reasoningId)
                {
                    if (!string.IsNullOrWhiteSpace(reasoningId))
                    {
                        activeReasoningId = reasoningId;
                        return reasoningId;
                    }

                    if (!string.IsNullOrWhiteSpace(activeReasoningId))
                    {
                        return activeReasoningId;
                    }

                    activeReasoningId = $"reasoning-{++syntheticReasoningCounter}";
                    return activeReasoningId;
                }

                void EnsureAssistantMessageStart(string messageId, string? parentToolCallId)
                {
                    if (!startedAssistantMessages.Add(messageId))
                    {
                        return;
                    }

                    Emit("message_start", messageId: messageId, fields: new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?>
                        {
                            ["id"] = messageId,
                            ["role"] = "assistant",
                            ["parentToolCallId"] = parentToolCallId
                        }
                    });
                }

                void EnsureReasoningMessageStart(string reasoningId)
                {
                    if (!startedReasoningMessages.Add(reasoningId))
                    {
                        return;
                    }

                    Emit("message_start", messageId: reasoningId, fields: new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?>
                        {
                            ["id"] = reasoningId,
                            ["role"] = "reasoning"
                        }
                    });
                }

                static List<Dictionary<string, object?>>? MapToolRequests(AssistantMessageDataToolRequestsItem[]? requests)
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

                using var sub = session.On(evt =>
                {
                    switch (evt)
                    {
                        case SessionStartEvent sessionStart:
                            copilotSessionId = sessionStart.Data.SessionId;
                            Emit("copilot_session_start", fields: new Dictionary<string, object?>
                            {
                                ["copilotSessionId"] = sessionStart.Data.SessionId,
                                ["selectedModel"] = sessionStart.Data.SelectedModel,
                                ["startTime"] = sessionStart.Data.StartTime,
                                ["version"] = sessionStart.Data.Version,
                                ["copilotVersion"] = sessionStart.Data.CopilotVersion,
                                ["producer"] = sessionStart.Data.Producer
                            });
                            break;
                        case AssistantTurnStartEvent assistantTurnStart:
                            Emit("assistant_turn_start", fields: new Dictionary<string, object?>
                            {
                                ["assistantTurnId"] = assistantTurnStart.Data.TurnId
                            });
                            break;
                        case AssistantTurnEndEvent assistantTurnEnd:
                            Emit("assistant_turn_end", fields: new Dictionary<string, object?>
                            {
                                ["assistantTurnId"] = assistantTurnEnd.Data.TurnId
                            });
                            break;
                        case AssistantMessageDeltaEvent delta:
                            {
                                var messageId = ResolveAssistantMessageId(delta.Data.MessageId);
                                EnsureAssistantMessageStart(messageId, delta.Data.ParentToolCallId);
                                Emit("message_update", messageId: messageId, fields: new Dictionary<string, object?>
                                {
                                    ["message"] = new Dictionary<string, object?>
                                    {
                                        ["id"] = messageId,
                                        ["role"] = "assistant"
                                    },
                                    ["delta"] = delta.Data.DeltaContent,
                                    ["parentToolCallId"] = delta.Data.ParentToolCallId,
                                    ["totalResponseSizeBytes"] = delta.Data.TotalResponseSizeBytes
                                });
                            }
                            if (!inAssistantMode)
                            {
                                if (inReasoningMode)
                                {
                                    ConsoleOutput.WriteLine();
                                    inReasoningMode = false;
                                }
                                inAssistantMode = true;
                            }
                            if (opt.ColorizedOutput)
                            {
                                ConsoleOutput.WriteAssistant(delta.Data.DeltaContent);
                            }
                            else
                            {
                                ConsoleOutput.Write(delta.Data.DeltaContent);
                            }
                            output.Append(delta.Data.DeltaContent);
                            break;
                        case AssistantReasoningDeltaEvent reasoning:
                            if (!opt.ShowReasoning)
                            {
                                break;
                            }
                            {
                                var reasoningId = ResolveReasoningId(reasoning.Data.ReasoningId);
                                EnsureReasoningMessageStart(reasoningId);
                                Emit("message_update", messageId: reasoningId, fields: new Dictionary<string, object?>
                                {
                                    ["message"] = new Dictionary<string, object?>
                                    {
                                        ["id"] = reasoningId,
                                        ["role"] = "reasoning"
                                    },
                                    ["delta"] = reasoning.Data.DeltaContent
                                });
                            }
                            if (!inReasoningMode)
                            {
                                if (inAssistantMode)
                                {
                                    ConsoleOutput.WriteLine();
                                    inAssistantMode = false;
                                }
                                inReasoningMode = true;
                            }
                            if (opt.ColorizedOutput)
                            {
                                ConsoleOutput.WriteReasoning(reasoning.Data.DeltaContent);
                            }
                            else
                            {
                                ConsoleOutput.Write(reasoning.Data.DeltaContent);
                            }
                            break;
                        case ToolExecutionStartEvent toolStart:
                            toolNamesByCallId[toolStart.Data.ToolCallId] = toolStart.Data.ToolName;
                            toolParentByCallId[toolStart.Data.ToolCallId] = toolStart.Data.ParentToolCallId;
                            Emit("tool_execution_start", toolCallId: toolStart.Data.ToolCallId, fields: new Dictionary<string, object?>
                            {
                                ["toolName"] = toolStart.Data.ToolName,
                                ["args"] = toolStart.Data.Arguments,
                                ["parentToolCallId"] = toolStart.Data.ParentToolCallId,
                                ["mcpToolName"] = toolStart.Data.McpToolName,
                                ["mcpServerName"] = toolStart.Data.McpServerName
                            });
                            if (inReasoningMode || inAssistantMode)
                            {
                                ConsoleOutput.WriteLine();
                                inReasoningMode = false;
                                inAssistantMode = false;
                            }
                            ConsoleOutput.WriteToolStart(toolStart.Data.ToolName);
                            break;
                        case ToolExecutionProgressEvent toolProgress:
                            toolNamesByCallId.TryGetValue(toolProgress.Data.ToolCallId, out var progressToolName);
                            Emit("tool_execution_update", toolCallId: toolProgress.Data.ToolCallId, fields: new Dictionary<string, object?>
                            {
                                ["toolName"] = progressToolName,
                                ["updateType"] = "progress",
                                ["progressMessage"] = toolProgress.Data.ProgressMessage
                            });
                            break;
                        case ToolExecutionPartialResultEvent toolPartial:
                            toolNamesByCallId.TryGetValue(toolPartial.Data.ToolCallId, out var partialToolName);
                            Emit("tool_execution_update", toolCallId: toolPartial.Data.ToolCallId, fields: new Dictionary<string, object?>
                            {
                                ["toolName"] = partialToolName,
                                ["updateType"] = "partial_result",
                                ["partialResult"] = toolPartial.Data.PartialOutput
                            });
                            break;
                        case ToolExecutionCompleteEvent toolComplete:
                            toolNamesByCallId.TryGetValue(toolComplete.Data.ToolCallId, out var completeToolName);
                            toolParentByCallId.TryGetValue(toolComplete.Data.ToolCallId, out var completeParentToolCallId);
                            Emit("tool_execution_end", toolCallId: toolComplete.Data.ToolCallId, fields: new Dictionary<string, object?>
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
                            });
                            var toolOutput = toolComplete.Data.Result?.Content;
                            if (!string.IsNullOrWhiteSpace(toolOutput))
                            {
                                var resolvedToolName = completeToolName ?? "unknown";
                                if (IsIgnorableToolOutput(resolvedToolName, toolOutput))
                                {
                                    toolNamesByCallId.Remove(toolComplete.Data.ToolCallId);
                                    toolParentByCallId.Remove(toolComplete.Data.ToolCallId);
                                    break;
                                }
                                var display = SummarizeToolOutput(toolOutput);
                                ConsoleOutput.WriteToolComplete(resolvedToolName, display);
                            }
                            toolNamesByCallId.Remove(toolComplete.Data.ToolCallId);
                            toolParentByCallId.Remove(toolComplete.Data.ToolCallId);
                            break;
                        case SessionCompactionStartEvent:
                            Emit("compaction_start");
                            break;
                        case SessionCompactionCompleteEvent compaction:
                            Emit("compaction_end", fields: new Dictionary<string, object?>
                            {
                                ["success"] = compaction.Data.Success,
                                ["error"] = compaction.Data.Error,
                                ["messagesRemoved"] = compaction.Data.MessagesRemoved,
                                ["tokensRemoved"] = compaction.Data.TokensRemoved,
                                ["preCompactionTokens"] = compaction.Data.PreCompactionTokens,
                                ["postCompactionTokens"] = compaction.Data.PostCompactionTokens,
                                ["preCompactionMessagesLength"] = compaction.Data.PreCompactionMessagesLength,
                                ["summaryContent"] = compaction.Data.SummaryContent
                            });
                            break;
                        case SessionSnapshotRewindEvent rewind:
                            Emit("retry", fields: new Dictionary<string, object?>
                            {
                                ["reason"] = "snapshot_rewind",
                                ["eventsRemoved"] = rewind.Data.EventsRemoved,
                                ["upToEventId"] = rewind.Data.UpToEventId
                            });
                            break;
                        case SessionUsageInfoEvent sessionUsage:
                            Emit("session_usage", fields: new Dictionary<string, object?>
                            {
                                ["currentTokens"] = sessionUsage.Data.CurrentTokens,
                                ["tokenLimit"] = sessionUsage.Data.TokenLimit,
                                ["messagesLength"] = sessionUsage.Data.MessagesLength
                            });
                            break;
                        case AssistantUsageEvent assistantUsage:
                            Emit("usage", fields: new Dictionary<string, object?>
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
                            });
                            break;
                        case AssistantMessageEvent:
                            {
                                var messageEvent = (AssistantMessageEvent)evt;
                                var messageId = ResolveAssistantMessageId(messageEvent.Data.MessageId);
                                EnsureAssistantMessageStart(messageId, messageEvent.Data.ParentToolCallId);
                                Emit("message_end", messageId: messageId, fields: new Dictionary<string, object?>
                                {
                                    ["message"] = new Dictionary<string, object?>
                                    {
                                        ["id"] = messageId,
                                        ["role"] = "assistant",
                                        ["content"] = messageEvent.Data.Content,
                                        ["parentToolCallId"] = messageEvent.Data.ParentToolCallId,
                                        ["toolRequests"] = MapToolRequests(messageEvent.Data.ToolRequests)
                                    }
                                });
                                activeAssistantMessageId = null;
                            }
                            ConsoleOutput.WriteLine();
                            inReasoningMode = false;
                            inAssistantMode = false;
                            break;
                        case AssistantReasoningEvent:
                            if (opt.ShowReasoning)
                            {
                                var reasoningEvent = (AssistantReasoningEvent)evt;
                                var reasoningId = ResolveReasoningId(reasoningEvent.Data.ReasoningId);
                                EnsureReasoningMessageStart(reasoningId);
                                Emit("message_end", messageId: reasoningId, fields: new Dictionary<string, object?>
                                {
                                    ["message"] = new Dictionary<string, object?>
                                    {
                                        ["id"] = reasoningId,
                                        ["role"] = "reasoning",
                                        ["content"] = reasoningEvent.Data.Content
                                    }
                                });
                                activeReasoningId = null;
                            }
                            ConsoleOutput.WriteLine();
                            inReasoningMode = false;
                            inAssistantMode = false;
                            break;
                        case SessionErrorEvent err:
                            Emit("session_error", fields: new Dictionary<string, object?>
                            {
                                ["errorType"] = err.Data.ErrorType,
                                ["message"] = err.Data.Message,
                                ["stack"] = err.Data.Stack,
                                ["copilotSessionId"] = copilotSessionId
                            });
                            done.TrySetException(new InvalidOperationException(err.Data.Message));
                            break;
                        case SessionIdleEvent:
                            Emit("copilot_session_end", fields: new Dictionary<string, object?>
                            {
                                ["copilotSessionId"] = copilotSessionId,
                                ["reason"] = "idle"
                            });
                            done.TrySetResult();
                            break;
                    }
                });

                await session.SendAsync(new MessageOptions { Prompt = prompt });

                using (ct.Register(() => done.TrySetCanceled(ct)))
                {
                    await done.Task;
                }

                result = output.ToString().Trim();
            }
        }
        finally
        {
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
        }

        return result;
    }

    internal static string SummarizeToolOutput(string toolOutput)
    {
        var normalized = toolOutput.Replace("\r\n", "\n").TrimEnd();
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        var lines = normalized.Split('\n');
        var totalLines = lines.Length;
        var totalChars = normalized.Length;

        const int maxLines = 6;
        const int maxChars = 800;
        var previewLines = lines.Take(maxLines);
        var preview = string.Join('\n', previewLines);

        if (preview.Length > maxChars)
        {
            preview = preview[..maxChars] + "\n... (truncated)";
        }

        if (totalLines > maxLines || preview.Length < totalChars)
        {
            return $"{preview}\n... ({totalLines} lines, {totalChars} chars)";
        }

        return preview;
    }

    internal static bool IsIgnorableToolOutput(string? toolName, string toolOutput)
    {
        if (!string.IsNullOrWhiteSpace(toolName) &&
            string.Equals(toolName, "report_intent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(toolOutput.Trim(), "Intent logged", StringComparison.OrdinalIgnoreCase);
    }
}
