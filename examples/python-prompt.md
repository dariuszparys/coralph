# Python Project Prompt Template

## Repo context

- Python project using standard tooling (pytest, flake8, black)
- Source code in `src/` directory
- Tests in `tests/` directory

## Build and test

- Install dependencies: `pip install -r requirements.txt` or `pip install -e .`
- Run tests: `pytest`
- Run linter: `flake8 .`
- Format code: `black .`
- Type checking: `mypy src/`

## Project structure

```
├── src/
│   └── my_package/
│       ├── __init__.py
│       └── main.py
├── tests/
│   ├── __init__.py
│   └── test_main.py
├── requirements.txt
├── pyproject.toml
└── README.md
```

## Feedback loops

Before committing, run:

1. `pytest` - All tests must pass
2. `flake8 .` - No linting errors
3. `black . --check` - Code must be formatted

## Coding standards

- Follow PEP 8 style guidelines
- Use type hints for function signatures
- Write docstrings for public functions and classes
- Keep functions small and focused

## Changes and logging

After completing work, append progress to `progress.txt` using the format in `prompt.md`.
