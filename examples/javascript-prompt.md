# JavaScript/TypeScript Project Prompt Template

## Repo context

- Node.js/TypeScript project
- Source code in `src/` directory
- Tests in `__tests__/` or `tests/` directory

## Build and test

- Install dependencies: `npm install`
- Run tests: `npm test`
- Build: `npm run build`
- Lint: `npm run lint`
- Format: `npm run format` (if available)

## Project structure

```
├── src/
│   ├── index.ts
│   └── utils/
│       └── helpers.ts
├── __tests__/
│   └── index.test.ts
├── package.json
├── tsconfig.json
├── .eslintrc.js
└── README.md
```

## Feedback loops

Before committing, run:

1. `npm test` - All tests must pass
2. `npm run lint` - No linting errors
3. `npm run build` - Build must succeed

## Coding standards

- Use ESLint and Prettier for consistent formatting
- Prefer TypeScript over plain JavaScript
- Use async/await over callbacks
- Write Jest tests for new functionality

## Changes and logging

After completing work, append progress to `progress.txt` using the format in `prompt.md`.
