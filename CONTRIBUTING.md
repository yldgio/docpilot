# Contributing to DocPilot

Thank you for your interest in contributing to DocPilot! 🎉

## Development Setup

### Prerequisites

- .NET 10.0 SDK
- GitHub Copilot CLI installed and authenticated
- Git

### Getting Started

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/docpilot.git
   cd docpilot
   ```
3. Create a branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
4. Install dependencies:
   ```bash
   dotnet restore
   ```
5. Build and test:
   ```bash
   dotnet build
   dotnet test
   ```

## Code Style

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and small

## Testing

- Write unit tests for new functionality
- Ensure all existing tests pass
- Aim for high test coverage on critical paths

## Pull Request Process

1. Update documentation if needed
2. Add tests for new features
3. Ensure CI passes
4. Request review from maintainers

## Commit Messages

Follow conventional commits:

```
type(scope): description

feat(analyzer): add support for TypeScript files
fix(pr): handle repositories without main branch
docs(readme): update installation instructions
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

## Questions?

Open an issue or start a discussion!
