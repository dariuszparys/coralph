using System.Collections;
using Serilog;
using GitHub.Copilot.SDK;

namespace Coralph;

/// <summary>
/// Coralph permission policy for Copilot tool requests.
/// </summary>
/// <remarks>
/// <para>
/// Default posture (empty allow and deny lists): <b>allow-all</b>.
/// Every tool request is approved unless restricted by a deny or allow list.
/// This is intentionally permissive so that unattended loop runs do not stall
/// waiting for approval.
/// </para>
/// <para>
/// Evaluation order:
/// <list type="number">
///   <item>If the tool matches a <c>--tool-deny</c> entry → <b>deny</b>.</item>
///   <item>If a <c>--tool-allow</c> list is provided and the tool does NOT match → <b>deny</b>.</item>
///   <item>Otherwise → <b>allow</b>.</item>
/// </list>
/// </para>
/// <para>
/// Deny takes precedence over allow when both lists are non-empty.
/// </para>
/// </remarks>
internal sealed class PermissionPolicy(LoopOptions opt, EventStreamWriter? eventStream)
{
    private readonly HashSet<string> _allow = NormalizeEntries(opt.ToolAllow);
    private readonly HashSet<string> _deny = NormalizeEntries(opt.ToolDeny);
    private readonly bool _hasAllowList = NormalizeEntries(opt.ToolAllow).Count > 0;
    private readonly EventStreamWriter? _eventStream = eventStream;

    internal Task<PermissionRequestResult> HandleAsync(PermissionRequest request, PermissionInvocation invocation)
    {
        var kind = request.Kind;
        if (string.IsNullOrWhiteSpace(kind))
        {
            kind = TryGetStringProperty(request, "RequestPermission") ?? "unknown";
        }

        var toolName = TryGetToolName(request) ?? TryGetStringProperty(invocation, "ToolName");
        var candidates = BuildCandidates(kind, toolName);

        var decision = EvaluateDecision(candidates, out var matchedRule);
        var resultKind = decision == PermissionDecision.Allow
            ? "approved"
            : "denied-interactively-by-user";

        EmitDecision(kind, toolName, candidates, decision, matchedRule);

        return Task.FromResult(new PermissionRequestResult { Kind = resultKind });
    }

    private PermissionDecision EvaluateDecision(IReadOnlyList<string> candidates, out string? matchedRule)
    {
        if (MatchesRuleSet(_deny, candidates, out matchedRule))
        {
            return PermissionDecision.Deny;
        }

        if (_hasAllowList)
        {
            return MatchesRuleSet(_allow, candidates, out matchedRule)
                ? PermissionDecision.Allow
                : PermissionDecision.Deny;
        }

        matchedRule = null;
        return PermissionDecision.Allow;
    }

    private void EmitDecision(
        string? kind,
        string? toolName,
        IReadOnlyList<string> candidates,
        PermissionDecision decision,
        string? matchedRule)
    {
        if (decision == PermissionDecision.Deny)
        {
            Log.Warning(
                "Permission denied (Kind={Kind}, Tool={Tool}, Rule={Rule})",
                kind ?? "unknown",
                toolName ?? "unknown",
                matchedRule ?? "(no match)");
        }

        _eventStream?.Emit("permission_decision", fields: new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["toolName"] = toolName,
            ["candidates"] = candidates,
            ["decision"] = decision == PermissionDecision.Allow ? "approved" : "denied",
            ["matchedRule"] = matchedRule
        });
    }

    private static IReadOnlyList<string> BuildCandidates(string? kind, string? toolName)
    {
        var candidates = new List<string>();
        AddCandidate(candidates, kind);
        AddCandidate(candidates, toolName);
        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        candidates.Add(value.Trim());
    }

    private static bool MatchesRuleSet(HashSet<string> rules, IReadOnlyList<string> candidates, out string? matchedRule)
    {
        foreach (var rule in rules)
        {
            if (MatchesRule(rule, candidates))
            {
                matchedRule = rule;
                return true;
            }
        }

        matchedRule = null;
        return false;
    }

    private static bool MatchesRule(string rule, IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (MatchesRule(rule, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesRule(string rule, string candidate)
    {
        if (rule == "*")
        {
            return true;
        }

        if (rule.EndsWith("*", StringComparison.Ordinal))
        {
            var prefix = rule[..^1];
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(rule, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> NormalizeEntries(IEnumerable<string>? entries)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (entries is null)
        {
            return result;
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            foreach (var token in entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    result.Add(token);
                }
            }
        }

        return result;
    }

    private static string? TryGetToolName(PermissionRequest request)
    {
        var extra = TryGetExtraDictionary(request);
        if (extra is null)
        {
            return null;
        }

        if (TryGetStringFromDictionary(extra, "toolName", out var toolName) ||
            TryGetStringFromDictionary(extra, "tool", out toolName) ||
            TryGetStringFromDictionary(extra, "name", out toolName))
        {
            return toolName;
        }

        return null;
    }

    private static IDictionary? TryGetExtraDictionary(object target)
    {
        var prop = target.GetType().GetProperty("Extra") ??
                   target.GetType().GetProperty("AdditionalProperties") ??
                   target.GetType().GetProperty("ExtensionData");

        return prop?.GetValue(target) as IDictionary;
    }

    private static bool TryGetStringFromDictionary(IDictionary dict, string key, out string? value)
    {
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is not string entryKey)
            {
                continue;
            }

            if (!string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = entry.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = value.Trim();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string? TryGetStringProperty(object target, string propertyName)
    {
        var prop = target.GetType().GetProperty(propertyName);
        var value = prop?.GetValue(target) as string;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private enum PermissionDecision
    {
        Allow,
        Deny
    }
}
