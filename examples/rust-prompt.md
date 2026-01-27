# Rust Project Prompt Template

## Repo context

- Rust project using Cargo
- Source code in `src/` directory
- Tests in `tests/` directory or inline with `#[cfg(test)]`

## Build and test

- Build: `cargo build`
- Run tests: `cargo test`
- Lint: `cargo clippy`
- Format: `cargo fmt`
- Check without building: `cargo check`

## Project structure

```
├── src/
│   ├── main.rs (or lib.rs)
│   └── utils.rs
├── tests/
│   └── integration_test.rs
├── Cargo.toml
├── Cargo.lock
└── README.md
```

## Feedback loops

Before committing, run:

1. `cargo test` - All tests must pass
2. `cargo build` - Build must succeed
3. `cargo fmt --check` - Code must be formatted
4. `cargo clippy -- -D warnings` - No clippy warnings

## Coding standards

- Follow Rust API Guidelines
- Use `Result` and `Option` types for error handling
- Write documentation comments (`///`) for public items
- Avoid `unwrap()` in production code
- Prefer iterators over manual loops

## Changes and logging

After completing work, append progress to `progress.txt` using the format in `prompt.md`.
