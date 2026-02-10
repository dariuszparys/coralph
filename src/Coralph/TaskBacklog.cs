using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Coralph;

internal static partial class TaskBacklog
{
    internal const string DefaultBacklogFile = "generated_tasks.json";

    private const int MaxTasksPerIssue = 25;
    private const int LargeIssueBodyThreshold = 3000;
    private const int MinimumLargeIssueTaskCount = 8;
    private const int LargeNonPrdIssueBodyThreshold = 4500;
    private const int LargeNonPrdTargetTaskCount = 6;
    private const int LargeNonPrdMaxTasks = 8;
    private const int PrdScoreThreshold = 5;

    [GeneratedRegex(@"^\s*[-*+]\s*\[(?<done>[ xX])\]\s+(?<text>.+)$")]
    private static partial Regex ChecklistLineRegex();

    [GeneratedRegex(@"^\s{0,3}#{2,4}\s+(?<title>.+?)\s*$")]
    private static partial Regex HeadingLineRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]|(?:\d+\.))\s+(?<text>.+)$")]
    private static partial Regex ListLineRegex();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>[^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"[`*_~]")]
    private static partial Regex MarkdownFormattingRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]|(?:\d+\.))\s+")]
    private static partial Regex ListPrefixRegex();

    [GeneratedRegex(@"coralph\s*:\s*task-mode\s*=\s*(?<mode>[a-z_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TaskModeOverrideRegex();

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> GenericHeadingTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "overview",
        "background",
        "context",
        "problem",
        "problem statement",
        "goals",
        "goal",
        "non goals",
        "non-goals",
        "scope",
        "in scope",
        "out of scope",
        "success metrics",
        "metrics",
        "dependencies",
        "open questions",
        "risks",
        "timeline",
        "rollout",
        "testing",
        "qa",
        "appendix",
        "references",
        "steps to reproduce",
        "reproduction steps",
        "reproduction",
        "environment",
        "actual behavior",
        "expected behavior",
        "observed behavior",
        "logs",
        "stack trace",
        "screenshots",
        "additional context",
    };

    private static readonly HashSet<string> NonImplementationSectionTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "steps to reproduce",
        "reproduction steps",
        "reproduction",
        "environment",
        "actual behavior",
        "expected behavior",
        "observed behavior",
        "stack trace",
        "logs",
        "log output",
        "screenshots",
        "additional context",
        "versions",
    };

    private static readonly HashSet<string> PrdHeadingSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        "goals",
        "goal",
        "non goals",
        "non-goals",
        "requirements",
        "acceptance criteria",
        "functional requirements",
        "non functional requirements",
        "success metrics",
        "user stories",
        "rollout",
        "milestones",
    };

    private enum IssueTaskMode
    {
        Default,
        Prd,
        LargeGeneric,
    }

    internal static async Task<string> EnsureBacklogAsync(string issuesJson, string backlogFile, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(backlogFile))
        {
            throw new ArgumentException("Backlog path cannot be null or empty.", nameof(backlogFile));
        }

        var existingRead = await FileContentCache.Shared.TryReadTextAsync(backlogFile, ct);
        var existingJson = existingRead.Exists ? existingRead.Content : null;
        var nextJson = BuildBacklogJson(issuesJson, existingJson);

        if (existingRead.Exists && string.Equals(existingJson, nextJson, StringComparison.Ordinal))
        {
            return existingJson!;
        }

        await File.WriteAllTextAsync(backlogFile, nextJson, ct).ConfigureAwait(false);
        FileContentCache.Shared.Invalidate(backlogFile);
        return nextJson;
    }

    internal static string BuildBacklogJson(string issuesJson, string? existingBacklogJson = null)
    {
        var issues = ParseOpenIssues(issuesJson);
        var existing = ParseBacklog(existingBacklogJson);
        var tasks = BuildTasks(issues, existing);

        var next = new GeneratedTaskBacklog
        {
            Version = 1,
            GeneratedAtUtc = DateTime.UtcNow,
            SourceIssueCount = issues.Count,
            Tasks = tasks,
        };

        if (existing is not null && IsEquivalent(existing, next))
        {
            next.GeneratedAtUtc = existing.GeneratedAtUtc;
        }

        return JsonSerializer.Serialize(next, SerializeOptions);
    }

    private static List<GeneratedTaskItem> BuildTasks(IReadOnlyList<IssueItem> issues, GeneratedTaskBacklog? existing)
    {
        var statusByStableKey = BuildStatusMap(existing);
        var tasks = new List<GeneratedTaskItem>();

        foreach (var issue in issues)
        {
            var drafts = BuildTaskDrafts(issue);
            var duplicateCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                var baseStableKey = $"{issue.Number}:{Slugify(draft.Title)}";
                if (!duplicateCounter.TryAdd(baseStableKey, 1))
                {
                    duplicateCounter[baseStableKey]++;
                }

                var duplicateIndex = duplicateCounter[baseStableKey];
                var stableKey = duplicateIndex > 1
                    ? $"{baseStableKey}-{duplicateIndex}"
                    : baseStableKey;

                var status = statusByStableKey.TryGetValue(stableKey, out var existingStatus)
                    ? existingStatus
                    : NormalizeStatus(draft.Status);

                tasks.Add(new GeneratedTaskItem
                {
                    Id = $"{issue.Number}-{i + 1:D3}",
                    StableKey = stableKey,
                    IssueNumber = issue.Number,
                    IssueTitle = issue.Title,
                    Title = draft.Title,
                    Description = draft.Description,
                    Status = status,
                    Origin = draft.Origin,
                    Order = i + 1,
                });
            }
        }

        return tasks;
    }

    private static List<IssueItem> ParseOpenIssues(string issuesJson)
    {
        var issues = new List<IssueItem>();
        if (string.IsNullOrWhiteSpace(issuesJson))
        {
            return issues;
        }

        using var doc = JsonDocument.Parse(issuesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return issues;
        }

        var syntheticNumber = 1;
        foreach (var issue in doc.RootElement.EnumerateArray())
        {
            if (issue.ValueKind != JsonValueKind.Object)
            {
                syntheticNumber++;
                continue;
            }

            if (issue.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String)
            {
                var state = stateProp.GetString();
                if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
                {
                    syntheticNumber++;
                    continue;
                }
            }

            var number = syntheticNumber;
            if (issue.TryGetProperty("number", out var numberProp) && numberProp.TryGetInt32(out var parsedNumber) && parsedNumber > 0)
            {
                number = parsedNumber;
            }

            var title = issue.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                ? titleProp.GetString()
                : null;
            var body = issue.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String
                ? bodyProp.GetString()
                : string.Empty;
            var comments = new List<string>();
            if (issue.TryGetProperty("comments", out var commentsProp) && commentsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var comment in commentsProp.EnumerateArray())
                {
                    if (comment.ValueKind == JsonValueKind.Object &&
                        comment.TryGetProperty("body", out var commentBodyProp) &&
                        commentBodyProp.ValueKind == JsonValueKind.String)
                    {
                        var commentBody = commentBodyProp.GetString();
                        if (!string.IsNullOrWhiteSpace(commentBody))
                        {
                            comments.Add(commentBody);
                        }

                        continue;
                    }

                    if (comment.ValueKind == JsonValueKind.String)
                    {
                        var commentBody = comment.GetString();
                        if (!string.IsNullOrWhiteSpace(commentBody))
                        {
                            comments.Add(commentBody);
                        }
                    }
                }
            }

            var labels = new List<string>();
            if (issue.TryGetProperty("labels", out var labelsProp) && labelsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var label in labelsProp.EnumerateArray())
                {
                    if (label.ValueKind == JsonValueKind.Object &&
                        label.TryGetProperty("name", out var labelNameProp) &&
                        labelNameProp.ValueKind == JsonValueKind.String)
                    {
                        var labelName = labelNameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(labelName))
                        {
                            labels.Add(labelName.Trim());
                        }

                        continue;
                    }

                    if (label.ValueKind == JsonValueKind.String)
                    {
                        var labelName = label.GetString();
                        if (!string.IsNullOrWhiteSpace(labelName))
                        {
                            labels.Add(labelName.Trim());
                        }
                    }
                }
            }

            var cleanTitle = string.IsNullOrWhiteSpace(title) ? $"Issue {number}" : title.Trim();
            issues.Add(new IssueItem(number, cleanTitle, body ?? string.Empty, comments, labels));
            syntheticNumber++;
        }

        return issues;
    }

    private static GeneratedTaskBacklog? ParseBacklog(string? backlogJson)
    {
        if (string.IsNullOrWhiteSpace(backlogJson))
        {
            return null;
        }

        try
        {
            var backlog = JsonSerializer.Deserialize<GeneratedTaskBacklog>(backlogJson, DeserializeOptions);
            if (backlog is null)
            {
                return null;
            }

            backlog.Tasks ??= [];
            foreach (var task in backlog.Tasks)
            {
                task.Status = NormalizeStatus(task.Status);
            }

            return backlog;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> BuildStatusMap(GeneratedTaskBacklog? existing)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (existing is null)
        {
            return map;
        }

        foreach (var task in existing.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.StableKey))
            {
                continue;
            }

            map[task.StableKey.Trim()] = NormalizeStatus(task.Status);
        }

        return map;
    }

    private static List<TaskDraft> BuildTaskDrafts(IssueItem issue)
    {
        var mode = ClassifyIssueTaskMode(issue);
        var commentChecklist = DedupeAndLimit(ExtractCommentChecklistTasks(issue.Comments));
        var checklist = DedupeAndLimit(ExtractChecklistTasks(issue.Body).Concat(commentChecklist));

        if (mode == IssueTaskMode.Default)
        {
            if (checklist.Count > 0)
            {
                return checklist;
            }

            return
            [
                new TaskDraft(
                    Title: issue.Title,
                    Description: BuildFallbackDescription(issue.Body),
                    Origin: "fallback",
                    Status: "open")
            ];
        }

        if (mode == IssueTaskMode.LargeGeneric)
        {
            if (checklist.Count > 0)
            {
                return checklist;
            }

            var largeIssueHeadings = DedupeAndLimit(ExtractHeadingTasks(issue.Body));
            var largeIssueListItems = DedupeAndLimit(ExtractListTasks(issue.Body, excludedSectionTitles: NonImplementationSectionTitles));
            var largeIssueChunks = DedupeAndLimit(ExtractParagraphTasks(issue));
            var merged = MergeDraftSources(largeIssueHeadings, largeIssueListItems, largeIssueChunks, [], LargeNonPrdTargetTaskCount);

            if (merged.Count == 0)
            {
                return
                [
                    new TaskDraft(
                        Title: issue.Title,
                        Description: BuildFallbackDescription(issue.Body),
                        Origin: "fallback",
                        Status: "open")
                ];
            }

            if (merged.Count > LargeNonPrdMaxTasks)
            {
                return merged.Take(LargeNonPrdMaxTasks).ToList();
            }

            return merged;
        }

        var commentListItems = DedupeAndLimit(ExtractCommentListTasks(issue.Comments));
        var hasCommentTasks = commentChecklist.Count > 0 || commentListItems.Count > 0;
        var headings = DedupeAndLimit(ExtractHeadingTasks(issue.Body));
        var listItems = DedupeAndLimit(ExtractListTasks(issue.Body).Concat(commentListItems));
        var chunks = DedupeAndLimit(ExtractParagraphTasks(issue));

        if (checklist.Count > 0 && ShouldExpandChecklistDrivenIssue(issue, checklist.Count))
        {
            return MergeDraftSources(checklist, headings, listItems, chunks, MinimumLargeIssueTaskCount);
        }

        if (checklist.Count > 0)
        {
            return checklist;
        }

        if (listItems.Count > 0 && (hasCommentTasks || headings.Count > 0 || issue.Body.Length >= 1500))
        {
            if (listItems.Count < MinimumLargeIssueTaskCount && (headings.Count > 0 || chunks.Count > 0))
            {
                return MergeDraftSources(listItems, headings, chunks, [], MinimumLargeIssueTaskCount);
            }

            return listItems;
        }

        if (headings.Count >= 2 || (issue.Body.Length >= 1500 && headings.Count > 0))
        {
            return headings;
        }

        if (listItems.Count >= 3 || (issue.Body.Length >= 1500 && listItems.Count > 0))
        {
            return listItems;
        }

        if (chunks.Count > 0)
        {
            return chunks;
        }

        return
        [
            new TaskDraft(
                Title: issue.Title,
                Description: BuildFallbackDescription(issue.Body),
                Origin: "fallback",
                Status: "open")
        ];
    }

    private static IssueTaskMode ClassifyIssueTaskMode(IssueItem issue)
    {
        var body = issue.Body ?? string.Empty;
        var overrideMode = GetTaskModeOverride(body);
        if (overrideMode is not null)
        {
            return overrideMode.Value;
        }

        var prdScore = 0;
        var normalizedTitle = NormalizePhrase(issue.Title);
        if (normalizedTitle.Contains("prd", StringComparison.OrdinalIgnoreCase))
        {
            return IssueTaskMode.Prd;
        }

        if (normalizedTitle.Contains("product requirement", StringComparison.OrdinalIgnoreCase) ||
            normalizedTitle.Contains("product requirements", StringComparison.OrdinalIgnoreCase))
        {
            prdScore += 4;
        }

        if (normalizedTitle.Contains("rfc", StringComparison.OrdinalIgnoreCase) ||
            normalizedTitle.Contains("design doc", StringComparison.OrdinalIgnoreCase))
        {
            prdScore += 2;
        }

        foreach (var label in issue.Labels)
        {
            var normalizedLabel = NormalizePhrase(label);
            if (normalizedLabel.Contains("prd", StringComparison.OrdinalIgnoreCase) ||
                normalizedLabel.Contains("requirements", StringComparison.OrdinalIgnoreCase) ||
                normalizedLabel.Contains("rfc", StringComparison.OrdinalIgnoreCase) ||
                normalizedLabel.Contains("design", StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedLabel.Contains("prd", StringComparison.OrdinalIgnoreCase))
                {
                    return IssueTaskMode.Prd;
                }

                prdScore += 2;
                break;
            }
        }

        var prdHeadingSignalCount = 0;
        foreach (var line in SplitLines(body))
        {
            var headingMatch = HeadingLineRegex().Match(line);
            if (!headingMatch.Success)
            {
                continue;
            }

            var normalizedHeading = NormalizePhrase(CleanTaskText(headingMatch.Groups["title"].Value));
            if (PrdHeadingSignals.Contains(normalizedHeading))
            {
                prdHeadingSignalCount++;
            }
        }

        if (prdHeadingSignalCount >= 2)
        {
            prdScore += 2;
        }

        if (prdHeadingSignalCount >= 4)
        {
            prdScore += 2;
        }

        if (body.Length >= LargeIssueBodyThreshold && prdHeadingSignalCount >= 2)
        {
            prdScore += 1;
        }

        if (prdScore >= PrdScoreThreshold)
        {
            return IssueTaskMode.Prd;
        }

        if (body.Length >= LargeNonPrdIssueBodyThreshold)
        {
            return IssueTaskMode.LargeGeneric;
        }

        return IssueTaskMode.Default;
    }

    private static IssueTaskMode? GetTaskModeOverride(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var match = TaskModeOverrideRegex().Match(body);
        if (!match.Success)
        {
            return null;
        }

        var mode = match.Groups["mode"].Value.Trim().ToLowerInvariant();
        return mode switch
        {
            "prd" => IssueTaskMode.Prd,
            "default" => IssueTaskMode.Default,
            "single" => IssueTaskMode.Default,
            "large" => IssueTaskMode.LargeGeneric,
            _ => null,
        };
    }

    private static IEnumerable<TaskDraft> ExtractChecklistTasks(string body, string origin = "checklist")
    {
        foreach (var line in SplitLines(body))
        {
            var match = ChecklistLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var title = CleanTaskText(match.Groups["text"].Value);
            if (title.Length < 6)
            {
                continue;
            }

            var doneFlag = match.Groups["done"].Value;
            yield return new TaskDraft(
                Title: title,
                Description: title,
                Origin: origin,
                Status: doneFlag.Trim().Length == 0 ? "open" : "done");
        }
    }

    private static IEnumerable<TaskDraft> ExtractCommentChecklistTasks(IEnumerable<string> comments)
    {
        foreach (var comment in comments)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                continue;
            }

            foreach (var draft in ExtractChecklistTasks(comment, "comment"))
            {
                yield return draft;
            }
        }
    }

    private static IEnumerable<TaskDraft> ExtractHeadingTasks(string body)
    {
        var lines = SplitLines(body);
        if (lines.Length == 0)
        {
            yield break;
        }

        var headings = new List<(string Title, int Index)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = HeadingLineRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var title = CleanTaskText(match.Groups["title"].Value);
            if (!IsUsefulHeading(title))
            {
                continue;
            }

            headings.Add((title, i));
        }

        for (var i = 0; i < headings.Count; i++)
        {
            var start = headings[i].Index + 1;
            var end = i + 1 < headings.Count ? headings[i + 1].Index : lines.Length;
            var sectionText = string.Join('\n', lines[start..end]);
            var description = BuildSectionDescription(sectionText);

            yield return new TaskDraft(
                Title: headings[i].Title,
                Description: description,
                Origin: "heading",
                Status: "open");
        }
    }

    private static IEnumerable<TaskDraft> ExtractListTasks(
        string body,
        string origin = "list",
        IReadOnlySet<string>? excludedSectionTitles = null)
    {
        var currentSection = string.Empty;
        foreach (var line in SplitLines(body))
        {
            var headingMatch = HeadingLineRegex().Match(line);
            if (headingMatch.Success)
            {
                currentSection = NormalizePhrase(CleanTaskText(headingMatch.Groups["title"].Value));
                continue;
            }

            if (ChecklistLineRegex().IsMatch(line))
            {
                continue;
            }

            var match = ListLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var text = CleanTaskText(match.Groups["text"].Value);
            if (text.Length < 12 || text.Length > 200)
            {
                continue;
            }

            if (LooksLikeMetadata(text))
            {
                continue;
            }

            if (excludedSectionTitles is not null &&
                currentSection.Length > 0 &&
                excludedSectionTitles.Contains(currentSection))
            {
                continue;
            }

            yield return new TaskDraft(
                Title: text,
                Description: text,
                Origin: origin,
                Status: "open");
        }
    }

    private static IEnumerable<TaskDraft> ExtractCommentListTasks(IEnumerable<string> comments)
    {
        foreach (var comment in comments)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                continue;
            }

            foreach (var draft in ExtractListTasks(comment, "comment"))
            {
                yield return draft;
            }
        }
    }

    private static IEnumerable<TaskDraft> ExtractParagraphTasks(IssueItem issue)
    {
        if (string.IsNullOrWhiteSpace(issue.Body) || issue.Body.Length < 500)
        {
            yield break;
        }

        var paragraphs = issue.Body
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanTaskText)
            .Where(p => p.Length >= 100)
            .Take(6)
            .ToList();

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var title = DeriveParagraphTitle(paragraph, issue.Title, i + 1);
            if (title.Length < 6)
            {
                continue;
            }

            yield return new TaskDraft(
                Title: title,
                Description: Truncate(paragraph, 320),
                Origin: "chunk",
                Status: "open");
        }
    }

    private static List<TaskDraft> DedupeAndLimit(IEnumerable<TaskDraft> drafts)
    {
        var result = new List<TaskDraft>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var draft in drafts)
        {
            var title = CleanTaskText(draft.Title);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var key = Slugify(title);
            if (!seen.Add(key))
            {
                continue;
            }

            result.Add(new TaskDraft(
                Title: Truncate(title, 140),
                Description: Truncate(string.IsNullOrWhiteSpace(draft.Description) ? title : CleanTaskText(draft.Description), 320),
                Origin: draft.Origin,
                Status: NormalizeStatus(draft.Status)));

            if (result.Count >= MaxTasksPerIssue)
            {
                break;
            }
        }

        return result;
    }

    private static bool ShouldExpandChecklistDrivenIssue(IssueItem issue, int checklistCount)
    {
        return issue.Body.Length >= LargeIssueBodyThreshold &&
               checklistCount < MinimumLargeIssueTaskCount;
    }

    private static List<TaskDraft> MergeDraftSources(
        IReadOnlyList<TaskDraft> primary,
        IReadOnlyList<TaskDraft> secondary,
        IReadOnlyList<TaskDraft> tertiary,
        IReadOnlyList<TaskDraft> quaternary,
        int targetCount)
    {
        var merged = new List<TaskDraft>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddUnique(primary, merged, seen);
        if (merged.Count >= targetCount || merged.Count >= MaxTasksPerIssue)
        {
            return merged;
        }

        AddUnique(secondary, merged, seen);
        if (merged.Count >= targetCount || merged.Count >= MaxTasksPerIssue)
        {
            return merged;
        }

        AddUnique(tertiary, merged, seen);
        if (merged.Count >= targetCount || merged.Count >= MaxTasksPerIssue)
        {
            return merged;
        }

        AddUnique(quaternary, merged, seen);
        return merged;
    }

    private static void AddUnique(IEnumerable<TaskDraft> source, List<TaskDraft> target, HashSet<string> seen)
    {
        foreach (var draft in source)
        {
            var key = Slugify(draft.Title);
            if (!seen.Add(key))
            {
                continue;
            }

            target.Add(draft);
            if (target.Count >= MaxTasksPerIssue)
            {
                return;
            }
        }
    }

    private static bool IsUsefulHeading(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 4)
        {
            return false;
        }

        var normalized = NormalizePhrase(title);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (var generic in GenericHeadingTitles)
        {
            if (string.Equals(normalized, generic, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeMetadata(string value)
    {
        var normalized = NormalizePhrase(value);
        if (normalized.StartsWith("http ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.StartsWith("note ", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("example ", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSectionDescription(string sectionText)
    {
        if (string.IsNullOrWhiteSpace(sectionText))
        {
            return "Implement this section end-to-end.";
        }

        var lines = SplitLines(sectionText)
            .Select(line => ListPrefixRegex().Replace(line, string.Empty))
            .Select(CleanTaskText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(3);

        var description = string.Join(' ', lines);
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Implement this section end-to-end.";
        }

        return Truncate(description, 320);
    }

    private static string BuildFallbackDescription(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Implement this issue end-to-end.";
        }

        var clean = CleanTaskText(body);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return "Implement this issue end-to-end.";
        }

        return Truncate(clean, 320);
    }

    private static string DeriveParagraphTitle(string paragraph, string issueTitle, int index)
    {
        var sentence = paragraph
            .Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(sentence))
        {
            var clean = CleanTaskText(sentence);
            if (clean.Length >= 8)
            {
                return Truncate(clean, 120);
            }
        }

        if (!string.IsNullOrWhiteSpace(issueTitle))
        {
            return $"{issueTitle} - part {index}";
        }

        return $"Task {index}";
    }

    private static string CleanTaskText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var clean = MarkdownLinkRegex().Replace(value, "${text}");
        clean = MarkdownFormattingRegex().Replace(clean, string.Empty);
        clean = WhitespaceRegex().Replace(clean, " ").Trim();
        clean = clean.Trim('-', ':', ';', '.', ',', ' ');
        return clean;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "task";
        }

        var sb = new StringBuilder(value.Length);
        var previousDash = false;

        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                sb.Append('-');
                previousDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length == 0)
        {
            return "task";
        }

        return Truncate(slug, 64).Trim('-');
    }

    private static string NormalizePhrase(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return WhitespaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "open";
        }

        var normalized = status.Trim().ToLowerInvariant()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        return normalized switch
        {
            "done" or "completed" or "complete" => "done",
            "in_progress" or "inprogress" => "in_progress",
            "blocked" => "blocked",
            _ => "open",
        };
    }

    private static string[] SplitLines(string value)
    {
        return value.Replace("\r\n", "\n").Split('\n');
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength].TrimEnd();
    }

    private static bool IsEquivalent(GeneratedTaskBacklog existing, GeneratedTaskBacklog next)
    {
        if (existing.Version != next.Version ||
            existing.SourceIssueCount != next.SourceIssueCount ||
            existing.Tasks.Count != next.Tasks.Count)
        {
            return false;
        }

        for (var i = 0; i < existing.Tasks.Count; i++)
        {
            var left = existing.Tasks[i];
            var right = next.Tasks[i];

            if (!string.Equals(left.Id, right.Id, StringComparison.Ordinal) ||
                !string.Equals(left.StableKey, right.StableKey, StringComparison.Ordinal) ||
                left.IssueNumber != right.IssueNumber ||
                !string.Equals(left.IssueTitle, right.IssueTitle, StringComparison.Ordinal) ||
                !string.Equals(left.Title, right.Title, StringComparison.Ordinal) ||
                !string.Equals(left.Description, right.Description, StringComparison.Ordinal) ||
                !string.Equals(NormalizeStatus(left.Status), NormalizeStatus(right.Status), StringComparison.Ordinal) ||
                !string.Equals(left.Origin, right.Origin, StringComparison.Ordinal) ||
                left.Order != right.Order)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record IssueItem(
        int Number,
        string Title,
        string Body,
        IReadOnlyList<string> Comments,
        IReadOnlyList<string> Labels);
    private sealed record TaskDraft(string Title, string Description, string Origin, string Status);

    private sealed class GeneratedTaskBacklog
    {
        public int Version { get; set; } = 1;
        public DateTime GeneratedAtUtc { get; set; }
        public int SourceIssueCount { get; set; }
        public List<GeneratedTaskItem> Tasks { get; set; } = [];
    }

    internal static bool HasOpenTasks(string backlogJson)
    {
        if (string.IsNullOrWhiteSpace(backlogJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(backlogJson);
            if (!doc.RootElement.TryGetProperty("tasks", out var tasks) ||
                tasks.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var task in tasks.EnumerateArray())
            {
                if (!task.TryGetProperty("status", out var statusProp) ||
                    statusProp.ValueKind != JsonValueKind.String)
                    continue;

                var status = statusProp.GetString();
                if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class GeneratedTaskItem
    {
        public string Id { get; set; } = string.Empty;
        public string StableKey { get; set; } = string.Empty;
        public int IssueNumber { get; set; }
        public string IssueTitle { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "open";
        public string Origin { get; set; } = "fallback";
        public int Order { get; set; }
    }
}
