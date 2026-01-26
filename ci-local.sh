#!/usr/bin/env bash
set -e

echo "🔨 Restoring dependencies..."
dotnet restore

echo "🏗️  Building solution (Release)..."
dotnet build --no-restore --configuration Release

echo "🧪 Running tests..."
dotnet test --no-build --configuration Release --verbosity normal

echo "✅ All checks passed!"
