#!/bin/bash
# Validates .github/copilot-instructions.md stays in sync with codebase

set -e

INSTRUCTIONS_FILE=".github/copilot-instructions.md"

echo "Validating $INSTRUCTIONS_FILE..."

# Check file exists
if [ ! -f "$INSTRUCTIONS_FILE" ]; then
    echo "❌ ERROR: $INSTRUCTIONS_FILE not found"
    exit 1
fi

# Required sections that must be present
REQUIRED_SECTIONS=(
    "## Repo context"
    "## Build and test"
    "## Run loops"
)

for section in "${REQUIRED_SECTIONS[@]}"; do
    if ! grep -q "$section" "$INSTRUCTIONS_FILE"; then
        echo "❌ ERROR: Missing required section: $section"
        exit 1
    fi
    echo "✓ Found section: $section"
done

# Validate that documented commands reference existing files
echo ""
echo "Validating referenced files..."

# Check that Coralph.sln exists (mentioned in repo context)
if ! grep -q "Coralph.sln" "$INSTRUCTIONS_FILE" || [ ! -f "Coralph.sln" ]; then
    echo "❌ ERROR: Coralph.sln referenced but not found"
    exit 1
fi
echo "✓ Coralph.sln exists"

# Check that src/Coralph exists (mentioned in run loops)
if ! grep -q "src/Coralph" "$INSTRUCTIONS_FILE" || [ ! -d "src/Coralph" ]; then
    echo "❌ ERROR: src/Coralph referenced but not found"
    exit 1
fi
echo "✓ src/Coralph directory exists"

# Check that prompt.md exists (mentioned in repo context)
if grep -q "prompt.md" "$INSTRUCTIONS_FILE" && [ ! -f "prompt.md" ]; then
    echo "❌ ERROR: prompt.md referenced but not found"
    exit 1
fi
echo "✓ prompt.md exists"

# Check that progress.txt exists (mentioned in repo context)
if grep -q "progress.txt" "$INSTRUCTIONS_FILE" && [ ! -f "progress.txt" ]; then
    echo "❌ ERROR: progress.txt referenced but not found"
    exit 1
fi
echo "✓ progress.txt exists"

echo ""
echo "✅ All validations passed!"
