# Justfile for Coralph - cross-platform CI automation with PowerShell
# See: https://just.systems and https://www.dariuszparys.com/just-your-commands/

shebang := if os() == 'windows' {
  'pwsh.exe'
} else {
  '/usr/bin/env pwsh'
}
set shell := ["pwsh", "-c"]
set windows-shell := ["pwsh.exe", "-NoProfile", "-Command"]

# Lists all available recipes
help:
    @just --list

# Restore dependencies
restore:
    dotnet restore

# Build the solution (Release configuration)
build: restore
    dotnet build --no-restore --configuration Release

# Run tests
test: build
    dotnet test --no-build --configuration Release --verbosity normal

# Run full CI pipeline: restore, build, test
ci: test
    #!{{shebang}}
    Write-Host "✅ All checks passed!" -ForegroundColor Green

# Create a tagged release (usage: just tag v1.0.0)
tag version:
    #!{{shebang}}
    if (-not "{{version}}".StartsWith("v")) {
        Write-Host "❌ Version must start with 'v' (e.g., v1.0.0)" -ForegroundColor Red
        exit 1
    }
    git tag "{{version}}"
    Write-Host "✅ Created tag {{version}}" -ForegroundColor Green
    Write-Host "Run 'git push origin {{version}}' to push the tag" -ForegroundColor Yellow
