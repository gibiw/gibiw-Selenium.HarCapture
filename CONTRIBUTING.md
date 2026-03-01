# Contributing to Selenium.HarCapture

Thank you for your interest in contributing. This guide covers everything you need to build, test, and submit changes.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later)
- [Google Chrome](https://www.google.com/chrome/) — required for integration tests
- Git

## Clone and Build

```bash
git clone https://github.com/gibiw/Selenium.HarCapture.git
cd Selenium.HarCapture
dotnet build --configuration Release
```

## Running Tests

### Unit Tests

```bash
dotnet test tests/Selenium.HarCapture.Tests
```

Unit tests have no external dependencies and run on any platform.

### Integration Tests

```bash
dotnet test tests/Selenium.HarCapture.IntegrationTests
```

Integration tests require Google Chrome. Chrome runs in headless mode automatically — no display server is needed.

## Coding Conventions

- C# with `<Nullable>enable</Nullable>` — all public and internal APIs must be null-annotated
- `<LangVersion>latest</LangVersion>` — use modern C# features where appropriate
- Tests use [xUnit](https://xunit.net/) and [FluentAssertions](https://fluentassertions.com/)
- Internal implementation types are exposed to test projects via `[InternalsVisibleTo]`; do not make types public unless they are part of the public API contract

## Pull Request Process

1. **Fork** the repository and create a branch from `main`
2. Use a descriptive branch name following conventional-commits prefixes:
   - `feat/your-feature-name`
   - `fix/your-bug-description`
   - `refactor/your-refactor-description`
3. Make your changes and ensure all **unit tests pass**: `dotnet test tests/Selenium.HarCapture.Tests`
4. Run integration tests if your change touches CDP event handling or HAR output format
5. Open a PR targeting `main` and fill out the PR template checklist
6. A maintainer will review and provide feedback within a few days

## Reporting Issues

Please use the GitHub issue templates for [bug reports](https://github.com/gibiw/Selenium.HarCapture/issues/new?template=bug-report.yml) and [feature requests](https://github.com/gibiw/Selenium.HarCapture/issues/new?template=feature-request.yml).
