param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Increment = "patch",
    [string]$FromVersion = ""
)

$ErrorActionPreference = "Stop"

function Normalize-Version {
    param([string]$Value)

    $normalized = $Value.Trim()
    if ($normalized.StartsWith("v")) {
        $normalized = $normalized.Substring(1)
    }

    return ($normalized -split '[+-]')[0]
}

if ([string]::IsNullOrWhiteSpace($FromVersion)) {
    $latestTag = git describe --tags --abbrev=0 --match 'v[0-9]*' 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($latestTag)) {
        throw "Could not detect a release tag. Pass -FromVersion (for example: 1.1.2)."
    }

    $FromVersion = $latestTag
}

$baseVersion = Normalize-Version $FromVersion
$parts = $baseVersion.Split('.')
if ($parts.Length -ne 3) {
    throw "Version '$FromVersion' is not semantic version format X.Y.Z."
}

$major = 0
$minor = 0
$patch = 0
if (
    -not [int]::TryParse($parts[0], [ref]$major) -or
    -not [int]::TryParse($parts[1], [ref]$minor) -or
    -not [int]::TryParse($parts[2], [ref]$patch)
) {
    throw "Version '$FromVersion' contains non-numeric semantic segments."
}

switch ($Increment) {
    "major" {
        $major++
        $minor = 0
        $patch = 0
    }
    "minor" {
        $minor++
        $patch = 0
    }
    default {
        $patch++
    }
}

$nextCoreVersion = "$major.$minor.$patch"
$nextDevVersion = "$nextCoreVersion-dev"
$nextAssemblyVersion = "$nextCoreVersion.0"

$projectPath = Join-Path $PSScriptRoot "../../src/Coralph/Coralph.csproj"
$projectPath = [System.IO.Path]::GetFullPath($projectPath)
if (-not (Test-Path $projectPath)) {
    throw "Project file not found at '$projectPath'."
}

$projectContent = Get-Content $projectPath -Raw
$projectContent = [regex]::Replace($projectContent, '<Version>[^<]+</Version>', "<Version>$nextDevVersion</Version>", 1)
$projectContent = [regex]::Replace($projectContent, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$nextAssemblyVersion</AssemblyVersion>", 1)
$projectContent = [regex]::Replace($projectContent, '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$nextAssemblyVersion</FileVersion>", 1)
$utf8Bom = [System.Text.UTF8Encoding]::new($true)
[System.IO.File]::WriteAllText($projectPath, $projectContent, $utf8Bom)

Write-Host "Base release version: $baseVersion"
Write-Host "Increment strategy: $Increment"
Write-Host "Updated Version: $nextDevVersion"
Write-Host "Updated Assembly/File version: $nextAssemblyVersion"
