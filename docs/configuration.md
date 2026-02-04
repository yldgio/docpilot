# Configuration Reference

DocPilot is configured via a `docpilot.yml` file in your repository root.

## Quick Start

Create `docpilot.yml`:

```yaml
heuristics:
  rules:
    - pattern: "src/**/*.cs"
      target: "docs/api/{module}.md"

paths:
  allowlist:
    - "docs/**"
    - "*.md"
```

## Full Configuration Schema

```yaml
# Heuristic rules for mapping code changes to documentation targets
heuristics:
  rules:
    - pattern: "glob-pattern"        # Required: File pattern to match
      target: "target-path"          # Required: Documentation target path
      section: "## Section Name"     # Optional: Section within target file
      confidence: 0.8                # Optional: Base confidence (0.0-1.0)

# Path restrictions (docs-only guardrail)
paths:
  allowlist:                         # Only these paths can be modified
    - "docs/**"
    - "*.md"
    - "README.md"

# Processing limits
limits:
  maxFiles: 50                       # Maximum files to process
  maxLinesPerFile: 1000              # Skip files larger than this
```

## Configuration Sections

### `heuristics.rules`

Define how code changes map to documentation targets.

```yaml
heuristics:
  rules:
    # API documentation
    - pattern: "src/Api/**/*.cs"
      target: "docs/api/{filename}.md"
      section: "## Endpoints"
      confidence: 0.9

    # Service layer documentation  
    - pattern: "src/Services/**/*.cs"
      target: "docs/architecture.md"
      section: "## Services"
      confidence: 0.8

    # Configuration changes
    - pattern: "*.csproj"
      target: "docs/getting-started.md"
      section: "## Dependencies"
      confidence: 0.7

    # Infrastructure
    - pattern: "terraform/**/*.tf"
      target: "docs/infrastructure.md"
      confidence: 0.85

    # Test documentation
    - pattern: "tests/**/*Tests.cs"
      target: "docs/testing.md"
      section: "## Test Coverage"
      confidence: 0.6
```

#### Pattern Placeholders

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{filename}` | File name without extension | `UserService.cs` → `UserService` |
| `{module}` | Parent directory name | `src/Auth/Login.cs` → `Auth` |
| `{path}` | Full relative path | `src/Api/v1/Users.cs` → `src/Api/v1/Users` |

#### Glob Pattern Syntax

| Pattern | Matches |
|---------|---------|
| `*.cs` | All `.cs` files in root |
| `**/*.cs` | All `.cs` files recursively |
| `src/**/*.cs` | All `.cs` files under `src/` |
| `src/Api/*.cs` | `.cs` files directly in `src/Api/` |
| `src/{Api,Services}/**` | Files in `Api` or `Services` |

### `paths.allowlist`

Restrict which paths DocPilot can modify (safety guardrail).

```yaml
paths:
  allowlist:
    - "docs/**"           # All files in docs/
    - "*.md"              # Markdown files in root
    - "README.md"         # Explicit file
    - "CHANGELOG.md"
```

If no allowlist is specified, DocPilot defaults to:
- `docs/**`
- `*.md`

### `limits`

Control processing limits to avoid overwhelming large repositories.

```yaml
limits:
  maxFiles: 50            # Max files to analyze per run
  maxLinesPerFile: 1000   # Skip files with more lines
```

## Example Configurations

### Monorepo with Multiple Packages

```yaml
heuristics:
  rules:
    - pattern: "packages/api/**/*.ts"
      target: "packages/api/README.md"
      section: "## API Reference"
      
    - pattern: "packages/core/**/*.ts"
      target: "packages/core/README.md"
      section: "## Core Library"
      
    - pattern: "packages/cli/**/*.ts"
      target: "packages/cli/README.md"
      section: "## CLI Commands"

paths:
  allowlist:
    - "packages/*/README.md"
    - "packages/*/docs/**"
    - "docs/**"
```

### .NET Solution

```yaml
heuristics:
  rules:
    - pattern: "src/**/Controllers/**/*.cs"
      target: "docs/api/{module}.md"
      section: "## Endpoints"
      confidence: 0.9
      
    - pattern: "src/**/Services/**/*.cs"
      target: "docs/services.md"
      confidence: 0.8
      
    - pattern: "src/**/Models/**/*.cs"
      target: "docs/models.md"
      section: "## Data Models"
      confidence: 0.7
      
    - pattern: "*.csproj"
      target: "docs/dependencies.md"
      confidence: 0.6

paths:
  allowlist:
    - "docs/**"
    - "README.md"

limits:
  maxFiles: 100
  maxLinesPerFile: 2000
```

### Python Project

```yaml
heuristics:
  rules:
    - pattern: "src/**/*.py"
      target: "docs/api/{module}.md"
      confidence: 0.8
      
    - pattern: "requirements*.txt"
      target: "docs/installation.md"
      section: "## Dependencies"
      confidence: 0.9
      
    - pattern: "pyproject.toml"
      target: "docs/installation.md"
      section: "## Installation"
      confidence: 0.85

paths:
  allowlist:
    - "docs/**"
    - "*.md"
    - "*.rst"
```

### Minimal Configuration

For simple projects, a minimal config works well:

```yaml
heuristics:
  rules:
    - pattern: "src/**/*"
      target: "README.md"
      section: "## Usage"
```

## Default Behavior

If no `docpilot.yml` exists, DocPilot uses sensible defaults:

```yaml
heuristics:
  rules:
    - pattern: "src/**/*.cs"
      target: "docs/{module}.md"
      confidence: 0.7
      
    - pattern: "src/**/*.ts"
      target: "docs/{module}.md"
      confidence: 0.7
      
    - pattern: "src/**/*.py"
      target: "docs/{module}.md"
      confidence: 0.7
      
    - pattern: "*.csproj"
      target: "README.md"
      section: "## Dependencies"
      confidence: 0.5
      
    - pattern: "package.json"
      target: "README.md"
      section: "## Dependencies"
      confidence: 0.5

paths:
  allowlist:
    - "docs/**"
    - "*.md"

limits:
  maxFiles: 50
  maxLinesPerFile: 1000
```

## Configuration Loading

DocPilot searches for configuration in this order:

1. Path specified via `--config` flag
2. `docpilot.yml` in current directory
3. `docpilot.yaml` in current directory
4. `.docpilot.yml` in current directory
5. Default configuration

## Validating Configuration

Test your configuration without making changes:

```bash
docpilot analyze --staged --config docpilot.yml --output text
```

## Next Steps

- [Getting Started](getting-started.md) — Apply configuration in practice
- [GitHub Actions](github-actions.md) — Use configuration in CI/CD
