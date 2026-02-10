# PowerShell Core (PowerShell 7+) — LLM Reference

## Summary
PowerShell (a.k.a. PowerShell Core / PowerShell 7+) is the cross-platform PowerShell built on .NET, running on Windows, Linux, and macOS. It is open source and the actively developed line, while Windows PowerShell 5.1 is Windows-only and no longer receives new features.  
Sources: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/community/contributing/product-terminology.md, https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/differences-from-windows-powershell.md, https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/what-is-windows-powershell.md

## Key facts (fast recall)
- Executable: `pwsh` (separate install from Windows PowerShell 5.1; side-by-side supported).  
  Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/Migrating-from-Windows-PowerShell-51-to-PowerShell-7.md
- Cross-platform: Windows, Linux, macOS.  
  Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/differences-from-windows-powershell.md
- Support: **LTS** and **Current** releases; Current is supported for 6 months after the next LTS, LTS gets only security/servicing fixes.  
  Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/install/PowerShell-Support-Lifecycle.md

## Install (common paths)
### macOS (Homebrew, latest LTS)
```bash
brew install powershell/tap/powershell-lts
pwsh-lts
```
Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/install/install-powershell-on-macos.md

### Linux (Debian/Ubuntu via Microsoft repo)
```bash
sudo apt-get update
sudo apt-get install -y wget
source /etc/os-release
wget -q https://packages.microsoft.com/config/debian/$VERSION_ID/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y powershell
pwsh
```
Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/install/install-debian.md

### Linux (binary archive, all distros)
```bash
curl -L -o /tmp/powershell.tar.gz \
  https://github.com/PowerShell/PowerShell/releases/download/v7.5.4/powershell-7.5.4-linux-x64.tar.gz
sudo mkdir -p /opt/microsoft/powershell/7
```
Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/install/install-other-linux.md

## Run and verify
```powershell
pwsh
$PSVersionTable.PSVersion
```

## LTS vs Current (support model)
- **LTS**: aligned with .NET LTS; security/servicing fixes only, stable for production.  
- **Current**: feature innovations; supported for **6 months after next LTS**.  
- Microsoft supports only the **latest update** of a release.  
Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/install/PowerShell-Support-Lifecycle.md

## Compatibility with Windows PowerShell 5.1
- Windows PowerShell 5.1 is .NET Framework–based and Windows-only; no new features.  
- PowerShell 7+ is .NET-based and cross-platform; most language features are shared, with differences mainly in cmdlet availability/behavior across OS and .NET.  
Sources: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/what-is-windows-powershell.md, https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/differences-from-windows-powershell.md

## Execution policy (important edge case)
- On Linux/macOS, `Get-ExecutionPolicy` always returns **Unrestricted** and `Set-ExecutionPolicy` has **no effect**.  
- On Windows, you can set policy by scope: `Process`, `CurrentUser`, `LocalMachine`, etc.  
Sources: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/unix-support.md, https://github.com/microsoftdocs/powershell-docs/blob/main/reference/7.4/Microsoft.PowerShell.Security/Set-ExecutionPolicy.md

Example:
```powershell
Get-ExecutionPolicy -List
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope LocalMachine
```

## Remoting (cross-platform via SSH)
PowerShell 6+ supports SSH-based remoting for cross-platform scenarios.
```powershell
# Create or enter an SSH remote session
$session = New-PSSession -HostName linuxserver -UserName admin
Enter-PSSession -HostName linuxserver -UserName admin

# Run a remote command (SSH)
Invoke-Command -HostName linuxserver -UserName admin -ScriptBlock {
  Get-Process | Select-Object -First 5
}
```
Sources: https://context7.com/microsoftdocs/powershell-docs/llms.txt, https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/Migrating-from-Windows-PowerShell-51-to-PowerShell-7.md

## Module compatibility + requirements
- Modules can specify a minimum PowerShell version via `PowerShellVersion` in the module manifest (`.psd1`).  
Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/dev-cross-plat/resolving-dependency-conflicts.md

Example snippet:
```powershell
@{
  ModuleVersion = '0.0.1'
  RootModule = 'AlcModule.dll'
  PowerShellVersion = '7.0'
}
```

## Common commands (quick recall)
```powershell
Get-Command *service*
Get-Help Get-Process -Online
Get-Process | Select-Object -First 5
Get-ChildItem -Force
```

## Short “when to choose” guidance
- **Need cross-platform automation:** choose PowerShell 7+ (`pwsh`).  
- **Need legacy Windows-only modules:** Windows PowerShell 5.1 may still be required; run side-by-side.  
Source: https://github.com/microsoftdocs/powershell-docs/blob/main/reference/docs-conceptual/whats-new/Migrating-from-Windows-PowerShell-51-to-PowerShell-7.md
