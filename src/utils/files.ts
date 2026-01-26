export async function readPromptTemplate(path: string): Promise<string> {
  return Bun.file(path).text();
}

export async function readIssuesJson(path: string): Promise<string> {
  const file = Bun.file(path);
  const exists = await file.exists();
  return exists ? await file.text() : "[]";
}

export async function readProgressText(path: string): Promise<string> {
  const file = Bun.file(path);
  const exists = await file.exists();
  return exists ? await file.text() : "";
}

export function buildCombinedPrompt(
  promptTemplate: string,
  issuesJson: string,
  progressText: string,
): string {
  const lines: string[] = [];
  lines.push("You are running inside a loop. Use the files and repository as your source of truth.");
  lines.push("Stop condition: when everything is done, output EXACTLY: <promise>COMPLETE</promise>.");
  lines.push("");
  lines.push("# ISSUES_JSON");
  lines.push("```json");
  lines.push(issuesJson.trim());
  lines.push("```");
  lines.push("");
  lines.push("# PROGRESS_SO_FAR");
  lines.push("```text");
  lines.push(progressText.trim() === "" ? "(empty)" : progressText.trim());
  lines.push("```");
  lines.push("");
  lines.push("# INSTRUCTIONS");
  lines.push(promptTemplate.trim());
  lines.push("");
  lines.push("# OUTPUT_RULES");
  lines.push("- If you are done, output EXACTLY: <promise>COMPLETE</promise>");
  lines.push("- Otherwise, output what you changed and what you will do next iteration.");
  return lines.join("\n");
}
