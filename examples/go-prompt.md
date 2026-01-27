# Go Project Prompt Template

## Repo context

- Go project using standard Go toolchain
- Source code in `cmd/` and `internal/` directories
- Tests co-located with source files (`*_test.go`)

## Build and test

- Build: `go build ./...`
- Run tests: `go test ./...`
- Run tests with coverage: `go test -cover ./...`
- Lint: `golangci-lint run`
- Format: `gofmt -w .`

## Project structure

```
├── cmd/
│   └── myapp/
│       └── main.go
├── internal/
│   └── pkg/
│       ├── handler.go
│       └── handler_test.go
├── go.mod
├── go.sum
└── README.md
```

## Feedback loops

Before committing, run:

1. `go test ./...` - All tests must pass
2. `go build ./...` - Build must succeed
3. `gofmt -l .` - No formatting issues (output should be empty)
4. `golangci-lint run` - No linting errors (if available)

## Coding standards

- Follow Effective Go guidelines
- Use meaningful package and function names
- Keep functions small and focused
- Write table-driven tests
- Handle errors explicitly, don't ignore them

## Changes and logging

After completing work, append progress to `progress.txt` using the format in `prompt.md`.
