# DocPilot — Agentic Documentation Orchestration

## Goal
Automatizzare la manutenzione della documentazione di un repository creando **pull request di sola documentazione** (reviewabili), usando **GitHub Actions** come orchestrazione e **GitHub Copilot SDK** per generare/raffinare contenuti.

**Success criteria**
- Ridurre “doc drift” dopo modifiche al codice.
- PR di docs pertinenti, piccole e verificabili.
- Zero merge automatici: approvazione umana obbligatoria.

## Scope
**In scope**
- Aggiornare docs in base a diff di PR e cambi su `main`.
- Creare docs iniziali per nuove feature/moduli quando viene rilevata nuova “surface area”.
- Migliorare docs esistenti (chiarezza/grammatica/completeness) con job periodici.
- Aprire **PR separate** per docs (mai commit dentro la PR feature).
- Tracciabilità: ogni PR include *rationale* e riferimenti a file/paths/simboli.
- Esecuzione in **CI** e **locale (CLI)** con la stessa logica.

**Out of scope (MVP)**
- Modifiche al codice applicativo.
- Auto-merge delle PR di docs.
- Riscritture massive non necessarie (preferire update mirati).
- Decisioni di prodotto/architettura non derivabili dal repo/diff.

## Runtime e tecnologia
- **Orchestrator/CLI: C#/.NET**
- **AI: GitHub Copilot SDK** (non CLI)
- Config repo: **`docpilot.yml`**
- Diagrammi: **Mermaid inline**

## Documentation areas (priorità)
1. `README.md`, `docs/`, guide “getting started”
2. Module READMEs (`packages/<name>/README.md`, ecc.)
3. API docs (se già presenti strumenti/strutture)
4. Troubleshooting / operations / onboarding (v1)

## Triggers (GitHub Actions)
### A) PR-driven Maintenance Update
- Eventi: `pull_request` (opened, synchronize, reopened, ready_for_review) + label opzionale `docpilot`.
- Input: diff PR, file tree, docs esistenti.
- Output: **nuova PR docs-only** con update mirati e summary.

### B) Initial Documentation Creation (new module/feature)
- Eventi: `pull_request` quando vengono aggiunti nuovi moduli/cartelle o nuove API pubbliche (euristiche).
- Input: nuovo codice + PR description + eventuali issue/ADR collegati.
- Output: PR docs-only con docs baseline (overview, usage, esempi, parametri).

### C) Documentation Improvement (periodico)
- Eventi: `schedule` (settimanale/mensile) + `workflow_dispatch`.
- Input: docs esistenti + struttura repo.
- Output: PR docs-only “polish” conservativa (senza cambiare il significato tecnico).

## Modalità Locale (CLI)
Comandi minimi:
- `docpilot analyze` → classificazione + doc targets + confidence
- `docpilot generate` → patch docs locale + summary
- `docpilot check` → quality gates
- `docpilot pr` (opzionale) → branch + commit + PR docs-only

Supportare input diff:
- `--staged`, `--worktree`, `--base <ref> --head <ref>`

## Heuristics: mapping change → doc targets
Il sistema usa euristiche configurabili per:
1) classificare il change (feature/bugfix/refactor/breaking)
2) individuare i target docs
3) assegnare un confidence score (low/med/high)

Esempi di regole:
- Cambi in `src/**` o `packages/<name>/**` → README modulo + guide correlate in `docs/`
- Cambi in endpoint/API/public interfaces → sezione “Usage/API” in README + docs API
- Cambi in `infra/**` / `terraform/**` / `bicep/**` → `docs/infra/**` + runbook/ops
- Cambi in `.github/workflows/**` → `docs/devops/**` + contributor docs (se impatta)

Output mapping (strutturato):
- doc targets
- motivazione (evidenze)
- confidence + azione (PR / draft / richiesta chiarimenti)

Se confidence bassa: PR in **draft** o commento di richiesta info.

## Architecture (multi-agent orchestration)
**Orchestrator (controller)**
- Riceve trigger GitHub Actions o CLI locale.
- Recupera diff e contesto repo.
- Esegue pipeline, applica policy.

**Pipeline agents**
1. Change Analyzer
2. Doc Mapper
3. Doc Writer
4. Doc Reviewer
5. Quality Gatekeeper
6. PR Bot

**Domain specialist agents (on-demand)**
- Architecture
- Infrastructure
- Security
- Operations/SRE
- Feature/API
- Developer Experience (opzionale)

Gli specialisti producono **Doc Brief** e **Diagram Brief** strutturati; solo il Doc Writer scrive file finali.

## Diagrammi
- Formato default: **Mermaid inline**
- Tipi: component, sequence, deployment, data flow, ops flow
- Ogni diagramma deve avere evidenza in repo o essere marcato “assumption”
- Validazione sintassi Mermaid (quality gate)

## PR strategy (docs-only, separate)
- Sempre PR separate (branch `docpilot/<context>`).
- PR piccole: max N file / N righe (config).
- Titolo/commit: `docs(<area>): <change>`.
- PR description include:
  - cosa è cambiato e perché
  - riferimenti al diff / file / simboli
  - assunzioni e limiti

## Configurazione repo (`docpilot.yml`)
- `paths.allowlist` (docs-only guardrail)
- `heuristics.rules`
- `limits.maxFiles`, `limits.maxLines`
- `labels.enable`, `labels.skip`
- `diagram.format`, `diagram.location`
- `qualityGates.*`

## Quality gates
- Markdown lint
- Link check (configurabile)
- Doc build (se detect: mkdocs/docusaurus/docfx/typedoc)
- Policy anti‑hallucination (claim con evidenza)
- Security: niente secrets o contenuti sensibili

## Metrics
- % PR docs-only accettate
- tempo medio review
- # richieste di cambi per PR
- false positives
- copertura docs per moduli

## Roadmap
**MVP**
- Workflow A (PR-driven)
- CLI locale `analyze/generate`
- euristiche base + docs-only PR

**v1**
- Workflow B + C
- confidence + draft mode
- link check + doc build
- config repo completa

**vNext**
- diagrammi avanzati
- release notes/changelog assistiti
- dashboard metriche + tuning euristiche
