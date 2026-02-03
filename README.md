# 📝 DocPilot

> Automated documentation PR generator powered by GitHub Copilot

DocPilot analyzes your code changes and automatically generates documentation updates, creating pull requests to keep your docs in sync with your code.

## ✨ Features

- 🔍 **Smart Analysis** - Analyzes git diffs to identify documentation-worthy changes
- 🤖 **AI-Powered** - Uses GitHub Copilot to generate contextual documentation
- 🎯 **Confidence Scoring** - Rates suggestions by confidence level
- 🔄 **GitHub Integration** - Creates PRs automatically via GitHub Actions
- ⚙️ **Configurable** - Customize behavior with `docpilot.yml`

## 🚀 Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [GitHub Copilot CLI](https://githubnext.com/projects/copilot-cli/) installed and authenticated
- Git repository with GitHub remote

### Installation

```bash
# Install as .NET global tool
dotnet tool install -g docpilot
```

### Usage

```bash
# Analyze changes between commits
docpilot analyze --base HEAD~1 --head HEAD

# Generate documentation (dry-run)
docpilot generate --dry-run

# Create documentation PR
docpilot pr --target-branch main
```

## 📖 Commands

### `docpilot analyze`

Analyzes code changes and identifies documentation targets.

```bash
docpilot analyze [options]

Options:
  -b, --base <commit>     Base commit/branch for comparison
  -h, --head <commit>     Head commit/branch for comparison
  -s, --staged            Analyze staged changes only
  -o, --output <format>   Output format: json, text (default: json)
  -c, --config <path>     Path to docpilot.yml
```

### `docpilot generate`

Generates documentation patches based on analysis.

```bash
docpilot generate [options]

Options:
  -b, --base <commit>     Base commit/branch for comparison
  -h, --head <commit>     Head commit/branch for comparison
  -s, --staged            Analyze staged changes only
  -n, --dry-run           Preview changes without applying
  -t, --target <path>     Target directory for docs
  -c, --config <path>     Path to docpilot.yml
```

### `docpilot pr`

Creates a documentation pull request.

```bash
docpilot pr [options]

Options:
  -b, --base <commit>     Base commit/branch for comparison
  -h, --head <commit>     Head commit/branch for comparison
  -s, --staged            Analyze staged changes only
  -t, --target-branch     Target branch for PR (default: main)
  -d, --draft             Create as draft PR
  --title <title>         Custom PR title
  -c, --config <path>     Path to docpilot.yml
```

## ⚙️ Configuration

Create a `docpilot.yml` file in your repository root:

```yaml
# Documentation paths
docs:
  paths:
    - docs/
    - README.md
  exclude:
    - docs/generated/

# Source code paths
source:
  paths:
    - src/
  extensions:
    - .cs
    - .ts
    - .py

# Confidence settings
confidence:
  threshold: 0.7
  draft_below: 0.5

# PR settings
pull_request:
  branch_pattern: "docpilot/{type}-{timestamp}"
  labels:
    - documentation
    - docpilot
```

## 🔄 GitHub Actions

Add DocPilot to your CI/CD pipeline:

```yaml
name: DocPilot
on:
  pull_request:
    paths-ignore:
      - 'docs/**'
      - '*.md'

jobs:
  docpilot:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - run: dotnet tool install -g docpilot
      
      - run: docpilot pr --base ${{ github.base_ref }} --head ${{ github.head_ref }}
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## 🏗️ Architecture

DocPilot uses a multi-agent architecture:

1. **Orchestrator Agent** - Coordinates analysis and planning
2. **DocWriter Agent** - Generates documentation content
3. **PR Bot** - Creates and manages pull requests

```
┌─────────────┐     ┌──────────────┐     ┌────────────┐
│  Git Diff   │────▶│ Orchestrator │────▶│ DocWriter  │
│  Analyzer   │     │    Agent     │     │   Agent    │
└─────────────┘     └──────────────┘     └────────────┘
                           │                    │
                           ▼                    ▼
                    ┌──────────────┐     ┌────────────┐
                    │  Heuristics  │     │   Patch    │
                    │   Engine     │     │  Applier   │
                    └──────────────┘     └────────────┘
                                               │
                                               ▼
                                        ┌────────────┐
                                        │  PR Bot    │
                                        │  (GitHub)  │
                                        └────────────┘
```

## 📊 Confidence Levels

DocPilot assigns confidence scores to each suggestion:

| Level | Score | Behavior |
|-------|-------|----------|
| High | > 0.8 | Creates PR with `ready-for-review` label |
| Medium | 0.5 - 0.8 | Creates normal PR |
| Low | < 0.5 | Creates draft PR |

## 🛠️ Development

```bash
# Clone the repository
git clone https://github.com/your-org/docpilot.git
cd docpilot

# Build
dotnet build

# Run tests
dotnet test

# Run locally
dotnet run --project src/DocPilot -- analyze --staged
```

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details.
