import {
  buildCombinedPrompt,
  readIssuesJson,
  readProgressText,
  readPromptTemplate,
} from "./utils/files";

const args = process.argv.slice(2);

let maxIterations = 10;
let promptFile = "prompt.md";
let issuesFile = "issues.json";
let progressFile = "progress.txt";

for (let i = 0; i < args.length; i += 1) {
  const arg = args[i];
  if (arg === "--max-iterations") {
    const value = args[i + 1];
    if (!value || Number.isNaN(Number(value)) || Number(value) < 1) {
      console.error("ERROR: --max-iterations must be an integer >= 1");
      process.exit(2);
    }
    maxIterations = Number(value);
    i += 1;
    continue;
  }
  if (arg === "--prompt-file") {
    const value = args[i + 1];
    if (!value) {
      console.error("ERROR: --prompt-file is required");
      process.exit(2);
    }
    promptFile = value;
    i += 1;
    continue;
  }
  if (arg === "--issues-file") {
    const value = args[i + 1];
    if (!value) {
      console.error("ERROR: --issues-file is required");
      process.exit(2);
    }
    issuesFile = value;
    i += 1;
    continue;
  }
  if (arg === "--progress-file") {
    const value = args[i + 1];
    if (!value) {
      console.error("ERROR: --progress-file is required");
      process.exit(2);
    }
    progressFile = value;
    i += 1;
    continue;
  }
}

const prompt = await readPromptTemplate(promptFile);
const issues = await readIssuesJson(issuesFile);
let progress = await readProgressText(progressFile);

const combinedPrompt = buildCombinedPrompt(prompt, issues, progress);

let output = "";
for (let i = 1; i <= maxIterations; i += 1) {
  output = "<promise>COMPLETE</promise>";
  const entry = `\n\n---\n# Iteration ${i} (${new Date().toISOString()})\n\n${output}\n`;
  progress += entry;
  await Bun.write(progressFile, progress);
  if (output.includes("<promise>COMPLETE</promise>")) {
    break;
  }
}

console.log(combinedPrompt.length > 0 ? "" : "");
