# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

<a id="unreleased"></a>
## [Unreleased]

<a id="v1-1-2"></a>
## [1.1.2] - 2026-02-12
### Added
- add TUI generated tasks snapshot reader
<a id="v1-1-1"></a>
## [1.1.1] - 2026-02-12
### Added
- add tui mode with cancellable prompts
### Fixed
- refresh unreleased and harden generation flow
- restore transcript follow and show keybinds
- render banner in tui and wait for key on completion
### Other
- Make transcript pane auto-follow visible tail
- Make transcript pane read-only and fix End key conflict
- tui: auto-follow transcript until user scrolls up
- Delete generated_tasks backlog after successful completion
- Fix init config parse handling and PRD label classification
<a id="v1-1-0"></a>
## [1.1.0] - 2026-02-11
### Added
- add --working-dir for repo-targeted runs
- manage loop artifact ignores in .gitignore
- gate PRD fan-out and add conservative defaults
### Changed
- log issue 75 completion
- update gitignore
- update progress.txt
### Fixed
- correct release ranges and backfill entries
- adapt language prompt feedback loops
- tighten prompt workflow detection
- embed prompt templates for init
- embed issues sample for init
### Other
- Stop tracking generated_tasks.json
<a id="v1-0-11"></a>
## [1.0.11] - 2026-02-10
### Changed
- ignore generated tasks list
### Other
- Adds provider options to CLI
- Extends OpenAI provider configuration
- Normalize file paths in FileSystemTests for consistency
- Add unit tests for ProviderConfigFactory.Create method
- Add provider config options to ArgParser
- Adds support for OpenAI-compatible providers

<a id="v1-0-10"></a>
## [1.0.10] - 2026-02-10
### Added
- add --init workflow
- add PowerShell Core init script for cross-platform setup
- Add guidance for extending tech stack support
- Add coralph-init script for automated project setup
- Add IFileSystem abstraction for improved testability
- improve task backlog generation
- modernize with GeneratedRegex, SearchValues, and field keyword
### Changed
- remove generated tasks
- update README
- update progress.txt
- confirm legacy init removal
- remove generated tasks list
- update base model to gpt-5.2-codex
- mark all tasks for issue #71 as done
- mark task 70-002 as done (parent issue #70 closed)
- Extract ConfigurationService to improve SRP compliance
- Use C# 14 primary constructors to reduce boilerplate
- Add ConfigureAwait(false) to async library code
- mark task 67-002 as done (documentation only)
### Fixed
- surface init errors
- ensure init works offline
- add guardrails to check for open tasks
### Other
- Bump GitHub.Copilot.SDK from 0.1.22 to 0.1.23

<a id="v1-0-9"></a>
## [1.0.9] - 2026-02-08
### Added
- add persisted PRD task backlog generation
- cache file reads and batch event stream flushes
- add JSON model listing output
- add model discovery command
- add tool allow/deny permission policy
- persist Copilot session across iterations
- Add Azure DevOps and Azure Boards integration (#64)
### Changed
- update copilot instructions
- remove pr workflow
### Fixed
- repair model listing JSON payload
- harden repo parsing and permissions
- correct help output and PR feedback parsing
### Other
- Bump GitHub.Copilot.SDK from 0.1.21 to 0.1.22
- Bump GitHub.Copilot.SDK from 0.1.20 to 0.1.21

<a id="v1-0-8"></a>
## [1.0.8] - 2026-02-01
### Added
- implement local changelog generation with pipeline enforcement
- automate CHANGELOG.md generation in release pipeline
- Add CHANGELOG.md validation to just tag command
- add copilot diagnostics and token handling
- reuse copilot config in docker sandbox
- add changelog support
- add docker sandbox option
- add streaming event output
- add structured logging with Serilog (#46)
- reach Level 3 agent readiness
- add devcontainer support for .NET 10 development
### Changed
- correct wrong formatting
- update docker base image
- sanity check loop functionality (#52)
### Fixed
- require comment before closing issues
- allow prerelease runtime in docker
- stop loop on terminal signals
- add push instruction for Direct Push Mode (fixes #53)
### Other
- Bump GitHub.Copilot.SDK from 0.1.19 to 0.1.20

<a id="v1-0-7"></a>
## [1.0.7] - 2026-01-29
### Changed
- Maintenance and documentation updates.

<a id="v1-0-6"></a>
## [1.0.6] - 2026-01-27
### Changed
- Maintenance release.

<a id="v1-0-5"></a>
## [1.0.5] - 2026-01-27
### Changed
- Maintenance release.

<a id="v1-0-4"></a>
## [1.0.4] - 2026-01-27
### Changed
- Maintenance release.

<a id="v1-0-3"></a>
## [1.0.3] - 2026-01-26
### Changed
- Maintenance release.

<a id="v1-0-2"></a>
## [1.0.2] - 2026-01-26
### Changed
- Maintenance release.

<a id="v1-0-1"></a>
## [1.0.1] - 2026-01-26
### Changed
- Maintenance release.

<a id="v1-0-0"></a>
## [1.0.0] - 2026-01-26
### Added
- Initial release.

[Unreleased]: https://github.com/dariuszparys/coralph/compare/v1.1.2...HEAD
[1.1.2]: https://github.com/dariuszparys/coralph/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/dariuszparys/coralph/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/dariuszparys/coralph/compare/v1.0.11...v1.1.0
[1.0.11]: https://github.com/dariuszparys/coralph/compare/v1.0.10...v1.0.11
[1.0.10]: https://github.com/dariuszparys/coralph/compare/v1.0.9...v1.0.10
[1.0.9]: https://github.com/dariuszparys/coralph/compare/v1.0.8...v1.0.9
[1.0.8]: https://github.com/dariuszparys/coralph/compare/v1.0.7...v1.0.8
[1.0.7]: https://github.com/dariuszparys/coralph/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/dariuszparys/coralph/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/dariuszparys/coralph/compare/v1.0.4...v1.0.5
[1.0.4]: https://github.com/dariuszparys/coralph/compare/v1.0.3...v1.0.4
[1.0.3]: https://github.com/dariuszparys/coralph/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/dariuszparys/coralph/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/dariuszparys/coralph/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/dariuszparys/coralph/releases/tag/v1.0.0
