# Getting Started

This guide walks you through your first DocPilot workflow.

## Prerequisites

Before starting, ensure you have:

- ✅ DocPilot installed ([Installation Guide](installation.md))
- ✅ GitHub Copilot CLI authenticated (`gh copilot --version`)
- ✅ A git repository with some changes to analyze

## Step 1: Navigate to Your Repository

```bash
cd /path/to/your/project
```

DocPilot works within git repositories and analyzes changes between commits.

## Step 2: Analyze Changes

### Analyze Staged Changes

The most common workflow is analyzing what you're about to commit:

```bash
# Stage some files
git add src/MyService.cs

# Analyze staged changes
docpilot analyze --staged
```

### Analyze Between Commits

Compare documentation needs between two commits:

```bash
# Compare with previous commit
docpilot analyze --base HEAD~1 --head HEAD

# Compare branches
docpilot analyze --base main --head feature/new-api
```

### Understanding the Output

DocPilot outputs analysis in JSON format by default:

```json
{
  "stage": "Analysis",
  "success": true,
  "diff": {
    "totalFilesChanged": 3,
    "totalLinesAdded": 127,
    "totalLinesDeleted": 15
  },
  "mapping": {
    "overallChangeType": "Feature",
    "overallConfidence": "High",
    "averageConfidence": 0.85,
    "targets": [
      {
        "filePath": "docs/api/authentication.md",
        "section": "## OAuth Flow",
        "confidence": "High",
        "confidenceScore": 0.92,
        "sourceFiles": ["src/Auth/OAuthHandler.cs"]
      }
    ]
  }
}
```

For human-readable output:

```bash
docpilot analyze --staged --output text
```

## Step 3: Generate Documentation

Once you've analyzed changes, generate documentation patches:

### Preview Mode (Dry Run)

Always start with a dry run to see what will be generated:

```bash
docpilot generate --staged --dry-run
```

This shows you:
- Which files will be created or modified
- The content that will be added
- Confidence scores for each change

### Apply Changes

When satisfied with the preview:

```bash
docpilot generate --staged
```

DocPilot will:
1. Create new documentation files as needed
2. Update existing files with new sections
3. Add Mermaid diagrams where appropriate

## Step 4: Review Generated Documentation

Before committing, review the generated documentation:

```bash
# See what files were modified
git status

# Review changes
git diff docs/

# Stage documentation changes
git add docs/
```

## Step 5: Create a Pull Request (Optional)

For team workflows, create a dedicated documentation PR:

```bash
docpilot pr --target-branch main
```

This will:
1. Create a new branch (`docpilot/feature-<timestamp>`)
2. Commit the documentation changes
3. Open a pull request with a structured description
4. Apply labels based on confidence level

### Draft PRs for Low Confidence

If DocPilot is uncertain about the changes (confidence < 50%), it creates a draft PR:

```bash
# Force draft PR
docpilot pr --target-branch main --draft
```

## Complete Workflow Example

Here's a complete example workflow:

```bash
# 1. Make code changes
vim src/Services/PaymentService.cs

# 2. Stage your changes
git add src/Services/PaymentService.cs

# 3. Analyze what documentation is needed
docpilot analyze --staged --output text

# 4. Preview documentation generation
docpilot generate --staged --dry-run

# 5. Apply documentation changes
docpilot generate --staged

# 6. Review and commit everything
git add .
git commit -m "feat(payments): add refund support with documentation"

# OR create a separate docs PR
docpilot pr --target-branch main --title "docs: add payment refund documentation"
```

## Configuration (Optional)

Create a `docpilot.yml` in your repository root to customize behavior:

```yaml
heuristics:
  rules:
    - pattern: "src/Api/**/*.cs"
      target: "docs/api/{filename}.md"
      section: "## API Reference"

paths:
  allowlist:
    - "docs/**"
    - "*.md"

limits:
  maxFiles: 50
```

See [Configuration Reference](configuration.md) for all options.

## Common Scenarios

### New Feature Documentation

```bash
# After implementing a new feature on a branch
git checkout feature/user-auth
docpilot analyze --base main --head HEAD --output text
docpilot generate --base main --head HEAD
```

### Pre-commit Hook

Add to `.git/hooks/pre-commit`:

```bash
#!/bin/bash
docpilot analyze --staged --output text
echo "Run 'docpilot generate --staged' to update documentation"
```

### CI/CD Integration

See [GitHub Actions Guide](github-actions.md) for automated documentation updates.

## Next Steps

- [Configuration Reference](configuration.md) — Fine-tune DocPilot behavior
- [GitHub Actions Integration](github-actions.md) — Automate documentation
- [Architecture Overview](architecture.md) — Understand how DocPilot works
