#!/bin/bash
set -euo pipefail

REPO_URL="https://github.com/dariuszparys/coralph.git"
TOOL_NAME="coralph"
TMP_DIR=""

cleanup() {
  if [[ -n "$TMP_DIR" && -d "$TMP_DIR" ]]; then
    rm -rf "$TMP_DIR"
  fi
}
trap cleanup EXIT

# 1. Check for dotnet CLI
if ! command -v dotnet &>/dev/null; then
  echo "Error: 'dotnet' CLI is not installed or not in PATH." >&2
  echo "Install the .NET SDK from https://dotnet.microsoft.com/download and try again." >&2
  exit 1
fi

# Check for git
if ! command -v git &>/dev/null; then
  echo "Error: 'git' is not installed or not in PATH." >&2
  exit 1
fi

echo "==> Cloning $REPO_URL ..."
TMP_DIR=$(mktemp -d)
if ! git clone --depth 1 "$REPO_URL" "$TMP_DIR"; then
  echo "Error: Failed to clone repository from $REPO_URL." >&2
  exit 1
fi

PACKAGE_DIR="$TMP_DIR/src/Coralph/bin/Release"

echo "==> Building NuGet package ..."
pushd "$TMP_DIR/src/Coralph" > /dev/null
dotnet pack -c Release --nologo -o "$PACKAGE_DIR"

# 2. Install or update the global tool
echo "==> Installing $TOOL_NAME globally ..."
if dotnet tool list --global | grep -qw "$TOOL_NAME"; then
  echo "    '$TOOL_NAME' is already installed; running 'dotnet tool update' instead."
  dotnet tool update --global "$TOOL_NAME" --add-source "$PACKAGE_DIR"
else
  dotnet tool install --global "$TOOL_NAME" --add-source "$PACKAGE_DIR"
fi
popd > /dev/null

echo ""
echo "✓ '$TOOL_NAME' is now available globally."
echo "  Run 'coralph --version' to verify the installation."
