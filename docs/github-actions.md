# GitHub Actions Integration

Automate documentation updates on every pull request with GitHub Actions.

## Quick Setup

Create `.github/workflows/docpilot.yml`:

```yaml
name: DocPilot

on:
  pull_request:
    types: [opened, synchronize, reopened]
    paths-ignore:
      - 'docs/**'
      - '*.md'

jobs:
  docpilot:
    runs-on: ubuntu-latest
    permissions:
      contents: write
      pull-requests: write
      
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
          
      - name: Download DocPilot
        run: |
          curl -L https://github.com/yldgio/docpilot/releases/latest/download/docpilot-linux-x64 -o docpilot
          chmod +x docpilot
          
      - name: Setup GitHub Copilot CLI
        run: gh extension install github/gh-copilot
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          
      - name: Analyze Changes
        run: |
          ./docpilot analyze \
            --base ${{ github.event.pull_request.base.sha }} \
            --head ${{ github.event.pull_request.head.sha }} \
            --output text
            
      - name: Generate Documentation
        run: |
          ./docpilot generate \
            --base ${{ github.event.pull_request.base.sha }} \
            --head ${{ github.event.pull_request.head.sha }}
            
      - name: Create Documentation PR
        run: |
          ./docpilot pr \
            --target-branch ${{ github.head_ref }}
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Workflow Options

### Option 1: Separate Documentation PR

Creates a new PR for documentation changes:

```yaml
- name: Create Documentation PR
  run: ./docpilot pr --target-branch main
  env:
    GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### Option 2: Commit to Same Branch

Adds documentation to the current PR:

```yaml
- name: Generate Documentation
  run: ./docpilot generate --base ${{ github.event.pull_request.base.sha }} --head ${{ github.event.pull_request.head.sha }}
  
- name: Commit Documentation
  run: |
    git config user.name "github-actions[bot]"
    git config user.email "github-actions[bot]@users.noreply.github.com"
    git add docs/ *.md
    git diff --staged --quiet || git commit -m "docs: auto-update documentation"
    git push
```

### Option 3: Comment on PR

Post analysis as a PR comment instead of making changes:

```yaml
- name: Analyze and Comment
  run: |
    ANALYSIS=$(./docpilot analyze --base ${{ github.event.pull_request.base.sha }} --head ${{ github.event.pull_request.head.sha }} --output text)
    gh pr comment ${{ github.event.pull_request.number }} --body "## 📝 Documentation Analysis

    $ANALYSIS
    
    Run \`docpilot generate\` locally to apply these changes."
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Complete Workflow Examples

### Production-Ready Workflow

```yaml
name: DocPilot

on:
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]
    paths-ignore:
      - 'docs/**'
      - '*.md'
      - '.github/**'

concurrency:
  group: docpilot-${{ github.head_ref }}
  cancel-in-progress: true

jobs:
  docpilot:
    # Don't run on draft PRs
    if: github.event.pull_request.draft == false
    runs-on: ubuntu-latest
    
    permissions:
      contents: write
      pull-requests: write
      
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: ${{ github.head_ref }}
          
      - name: Download DocPilot
        run: |
          curl -L https://github.com/yldgio/docpilot/releases/latest/download/docpilot-linux-x64 -o docpilot
          chmod +x docpilot
          
      - name: Setup GitHub Copilot CLI
        run: gh extension install github/gh-copilot
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          
      - name: Check for Documentation Needs
        id: analyze
        run: |
          OUTPUT=$(./docpilot analyze \
            --base ${{ github.event.pull_request.base.sha }} \
            --head ${{ github.event.pull_request.head.sha }} \
            --output json)
          echo "analysis=$OUTPUT" >> $GITHUB_OUTPUT
          
          # Check if documentation is needed
          TARGETS=$(echo $OUTPUT | jq '.mapping.targets | length')
          echo "targets=$TARGETS" >> $GITHUB_OUTPUT
          
      - name: Generate Documentation
        if: steps.analyze.outputs.targets > 0
        run: |
          ./docpilot generate \
            --base ${{ github.event.pull_request.base.sha }} \
            --head ${{ github.event.pull_request.head.sha }}
            
      - name: Commit Changes
        if: steps.analyze.outputs.targets > 0
        run: |
          git config user.name "docpilot[bot]"
          git config user.email "docpilot[bot]@users.noreply.github.com"
          
          git add docs/ *.md
          
          if git diff --staged --quiet; then
            echo "No documentation changes to commit"
          else
            git commit -m "docs: auto-update documentation [skip ci]"
            git push
          fi
          
      - name: Add Label
        if: steps.analyze.outputs.targets > 0
        run: gh pr edit ${{ github.event.pull_request.number }} --add-label "documentation"
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### Scheduled Documentation Audit

```yaml
name: Documentation Audit

on:
  schedule:
    - cron: '0 9 * * 1'  # Every Monday at 9 AM
  workflow_dispatch:

jobs:
  audit:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
          
      - name: Download DocPilot
        run: |
          curl -L https://github.com/yldgio/docpilot/releases/latest/download/docpilot-linux-x64 -o docpilot
          chmod +x docpilot
          
      - name: Analyze Last Week
        run: |
          LAST_WEEK=$(git rev-list -1 --before="1 week ago" HEAD)
          ./docpilot analyze --base $LAST_WEEK --head HEAD --output text
          
      - name: Create Issue if Needed
        run: |
          ANALYSIS=$(./docpilot analyze --base $LAST_WEEK --head HEAD --output json)
          TARGETS=$(echo $ANALYSIS | jq '.mapping.targets | length')
          
          if [ "$TARGETS" -gt 0 ]; then
            gh issue create \
              --title "📝 Documentation Update Needed" \
              --body "DocPilot detected $TARGETS documentation targets from the last week's changes.
              
              Run \`docpilot generate\` to update documentation."
          fi
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Path Filtering

Prevent infinite loops by excluding documentation paths:

```yaml
on:
  pull_request:
    paths-ignore:
      - 'docs/**'
      - '*.md'
      - '.github/**'
```

## Permissions

Required permissions for the workflow:

```yaml
permissions:
  contents: write      # Push commits
  pull-requests: write # Create/update PRs, add labels
```

For organization repositories, you may need to enable "Allow GitHub Actions to create and approve pull requests" in repository settings.

## Secrets

| Secret | Required | Description |
|--------|----------|-------------|
| `GITHUB_TOKEN` | Yes | Automatic token, no setup needed |
| `GH_TOKEN` | Optional | PAT for cross-repo operations |

## Troubleshooting

### "Permission denied" errors

Ensure the workflow has write permissions:

```yaml
permissions:
  contents: write
  pull-requests: write
```

### Infinite loop (workflow triggers itself)

Add path filters to exclude documentation:

```yaml
paths-ignore:
  - 'docs/**'
  - '*.md'
```

Or use `[skip ci]` in commit messages:

```yaml
git commit -m "docs: update [skip ci]"
```

### GitHub Copilot CLI authentication fails

The workflow uses `GITHUB_TOKEN` for Copilot CLI. Ensure the token has sufficient scopes.

## Next Steps

- [Configuration Reference](configuration.md) — Customize heuristics
- [Architecture](architecture.md) — Understand the system
