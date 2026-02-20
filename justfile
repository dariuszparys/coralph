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

# Update changelog for upcoming release (usage: just changelog v1.0.0)
# Refresh unreleased notes (usage: just changelog unreleased)
changelog version:
    #!{{shebang}}
    if ("{{version}}" -eq "unreleased") {
        bash ./.github/scripts/update-changelog.sh --unreleased
        Write-Host "✅ Updated CHANGELOG.md unreleased section" -ForegroundColor Green
        Write-Host "📝 Review the changes, then commit and push:" -ForegroundColor Cyan
        Write-Host "   git add CHANGELOG.md" -ForegroundColor Gray
        Write-Host "   git commit -m 'docs: refresh changelog unreleased'" -ForegroundColor Gray
        Write-Host "   git push" -ForegroundColor Gray
        exit 0
    }

    if (-not "{{version}}".StartsWith("v")) {
        Write-Host "❌ Version must start with 'v' (e.g., v1.0.0) or be 'unreleased'" -ForegroundColor Red
        exit 1
    }
    $cleanVersion = "{{version}}" -replace '^v', ''
    
    # Run the changelog generation script
    bash ./.github/scripts/update-changelog.sh $cleanVersion
    
    Write-Host "✅ Updated CHANGELOG.md for version {{version}}" -ForegroundColor Green
    Write-Host "📝 Review the changes, then commit and push:" -ForegroundColor Cyan
    Write-Host "   git add CHANGELOG.md" -ForegroundColor Gray
    Write-Host "   git commit -m 'docs: update changelog for {{version}}'" -ForegroundColor Gray
    Write-Host "   git push" -ForegroundColor Gray

# Create a tagged release (usage: just tag v1.0.0)
# IMPORTANT: Run 'just changelog v1.0.0' first and commit the changes!
tag version:
    #!{{shebang}}
    if (-not "{{version}}".StartsWith("v")) {
        Write-Host "❌ Version must start with 'v' (e.g., v1.0.0)" -ForegroundColor Red
        exit 1
    }
    $cleanVersion = "{{version}}" -replace '^v', ''

    # Verify CHANGELOG.md has an entry for this version
    $changelogContent = Get-Content CHANGELOG.md -Raw
    if ($changelogContent -notmatch "\[$cleanVersion\]") {
        Write-Host "❌ CHANGELOG.md is missing an entry for version $cleanVersion" -ForegroundColor Red
        Write-Host "   Run: just changelog {{version}}" -ForegroundColor Yellow
        exit 1
    }

    git tag "{{version}}"
    git push origin "{{version}}"
    Write-Host "✅ Created tag {{version}}" -ForegroundColor Green
    Write-Host "✅ Pushed tag {{version}}" -ForegroundColor Green

# Bump development version in Coralph.csproj (usage: just bump-dev [patch|minor|major])
bump-dev increment='patch':
    #!{{shebang}}
    if ("{{increment}}" -notin @("patch", "minor", "major")) {
        Write-Host "❌ Increment must be one of: patch, minor, major" -ForegroundColor Red
        exit 1
    }
    pwsh ./.github/scripts/bump-dev-version.ps1 -Increment "{{increment}}"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Write-Host "✅ Updated development version in src/Coralph/Coralph.csproj" -ForegroundColor Green

# Publish local build with version from latest git tag (usage: just publish-local osx-arm64)
publish-local rid:
    #!{{shebang}}
    $tag = git describe --tags --abbrev=0 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $tag) {
        $version = "0.0.1-dev"
        Write-Host "⚠️  No git tag found, using default version: $version" -ForegroundColor Yellow
    } else {
        $version = $tag -replace '^v', ''
        Write-Host "📦 Using version from tag $tag`: $version" -ForegroundColor Cyan
    }
    dotnet publish src/Coralph -c Release -r {{rid}} --self-contained /p:Version=$version
    Write-Host "✅ Published to src/Coralph/bin/Release/net10.0/{{rid}}/publish/" -ForegroundColor Green
