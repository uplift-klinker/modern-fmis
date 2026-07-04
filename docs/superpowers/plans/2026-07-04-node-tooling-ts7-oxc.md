# Node Tooling Migration (TS7 + oxlint + oxfmt) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the `frontend` + `e2e` pnpm workspaces to TypeScript 7 (RC), replace ESLint with oxlint, and adopt oxfmt as the first JS/TS formatter — with root scripts + CI wiring.

**Architecture:** Three isolated tool swaps, each with its own gate. TypeScript stays a CLI type-check gate only (Vite/vitest/Playwright transpile). oxlint runs as a single repo-level pass with nested per-workspace configs. oxfmt formats the Node/TS surface (frontend + e2e); backend C# formatting stays `dotnet format`.

**Tech Stack:** pnpm workspace (Corepack pnpm 11.7.0, Node 24), `typescript@7.0.1-rc`, `oxlint@1.72.0`, `oxfmt@0.57.0`, GitHub Actions.

## Global Constraints

- Exact pinned versions: `typescript@7.0.1-rc`, `oxlint@1.72.0`, `oxfmt@0.57.0` (install with pnpm `-E`/`--save-exact`).
- No code comments; rationale in docs. Append-only git (new commits only, never amend/force-push). Commit trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- All shell via `zsh -lc`, run from repo root `/Users/bryceklinker/code/uplift-delivery/modern-fmis`. pnpm via Corepack; `dotnet` against `Fmis.slnx`.
- Backend C# formatting and the backend/infra build are OUT OF SCOPE. No `oxlint-tsgolint` / type-aware linting (the current ESLint config is non-type-checked).
- Docker is available for the contract suite.
- Branch `node-tooling-oxc-ts7` (off `main`; spec already committed there).
- **Tool-schema verification clause (from the spec):** where a task says "confirm the exact key", the implementer verifies against the installed tool (`oxlint --rules`, the `.oxlintrc.json` `$schema`, `oxfmt --help`) rather than assuming. The load-bearing contract is that each task's gate command runs green.

---

## File Structure

- `frontend/package.json`, `e2e/package.json` — dependency + script changes (via `pnpm` CLI where possible).
- `package.json` (root) — `lint`/`format`/`format:check` scripts; oxlint + oxfmt devDeps.
- Delete: `frontend/eslint.config.js`, `e2e/eslint.config.js`.
- Create: `.oxlintrc.json` (root), `frontend/.oxlintrc.json`, `e2e/.oxlintrc.json`, `.oxfmtrc.json` (root).
- `.github/workflows/ci.yml` — Frontend job lint/format steps.

---

## Task 1: TypeScript 7 (RC) bump

**Files:**
- Modify: `frontend/package.json` (devDependencies.typescript), `e2e/package.json` (devDependencies.typescript)

**Interfaces:**
- Produces: both workspaces build/typecheck under `typescript@7.0.1-rc`. No command or tsconfig changes.

- [ ] **Step 1: Bump typescript in both workspaces (pinned exact)**

Run:
```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && corepack enable && pnpm --filter frontend add -DE typescript@7.0.1-rc && pnpm --filter e2e add -DE typescript@7.0.1-rc"
```
Then confirm both `package.json` files show `"typescript": "7.0.1-rc"` (exact, no `^`).

- [ ] **Step 2: Verify frontend typecheck + build under the RC**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter frontend run typecheck && pnpm --filter frontend run build"`
Expected: PASS — `tsc -p tsconfig.app.json --noEmit` clean, then `tsc -b && vite build` produces `dist`. (Diagnostic wording may differ from classic tsc; the gate is a clean exit, not identical output.)

- [ ] **Step 3: Verify e2e typecheck under the RC**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter e2e run typecheck"`
Expected: PASS (`tsc --noEmit` clean).

- [ ] **Step 4: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add frontend/package.json e2e/package.json pnpm-lock.yaml && git commit -m 'Adopt TypeScript 7.0.1-rc for the CLI typecheck gate

Bump typescript 6.0.3 -> 7.0.1-rc in the frontend and e2e workspaces. Commands
and tsconfig are unchanged (drop-in); TS is used only as a type-check gate.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Task 2: Replace ESLint with oxlint

**Files:**
- Modify: `frontend/package.json`, `e2e/package.json` (remove eslint deps + `lint` script), `package.json` (root: oxlint devDep + `lint` script)
- Delete: `frontend/eslint.config.js`, `e2e/eslint.config.js`
- Create: `.oxlintrc.json`, `frontend/.oxlintrc.json`, `e2e/.oxlintrc.json`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `pnpm run lint` runs `oxlint` over the whole repo and exits clean. No ESLint anywhere.

- [ ] **Step 1: Remove ESLint dependencies and add oxlint**

Run:
```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && \
  pnpm --filter frontend remove eslint @eslint/js typescript-eslint eslint-plugin-react-hooks eslint-plugin-react-refresh globals && \
  pnpm --filter e2e remove eslint @eslint/js typescript-eslint globals && \
  pnpm add -DE -w oxlint@1.72.0"
```
(`-w` installs oxlint at the workspace root; `-E` pins it exact.)

- [ ] **Step 2: Delete the ESLint configs**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git rm frontend/eslint.config.js e2e/eslint.config.js"`

- [ ] **Step 3: Create the root oxlint config**

Create `.oxlintrc.json`:
```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["typescript", "unicorn", "oxc"],
  "categories": { "correctness": "error" },
  "rules": {}
}
```

- [ ] **Step 4: Create the frontend oxlint config**

Create `frontend/.oxlintrc.json`. This enables the `react` plugin (which turns on `react/exhaustive-deps` and the react correctness rules) and the browser env. Then confirm the exact keys for the rules-of-hooks and react-refresh checks via `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm exec oxlint --rules | grep -iE 'hook|export-component|refresh'"`, and add those keys to `rules` at the severities shown (rules-of-hooks = error, exhaustive-deps = warn, only-export-components = warn — matching the prior ESLint config). If the installed oxlint does not provide `only-export-components`, omit it and note the accepted coverage gap in the commit message (it is a dev-time HMR nicety, not a correctness rule).

```json
{
  "$schema": "../node_modules/oxlint/configuration_schema.json",
  "plugins": ["typescript", "unicorn", "oxc", "react"],
  "categories": { "correctness": "error" },
  "env": { "builtin": true, "browser": true },
  "rules": {
    "react/exhaustive-deps": "warn"
  }
}
```
(Add the confirmed rules-of-hooks / only-export-components keys to `rules` per the verification above. The `$schema` uses `../node_modules/...` because oxlint is installed at the workspace root, not under `frontend/node_modules`; the `$schema` is only for editor validation and is non-fatal if the path differs.)

- [ ] **Step 5: Create the e2e oxlint config**

Create `e2e/.oxlintrc.json`:
```json
{
  "$schema": "../node_modules/oxlint/configuration_schema.json",
  "plugins": ["typescript", "unicorn", "oxc"],
  "categories": { "correctness": "error" },
  "env": { "builtin": true, "node": true },
  "ignorePatterns": ["playwright-report", "test-results"]
}
```
Confirm the `ignorePatterns` key name against `oxlint --rules`/the schema; if oxlint uses a different key for ignore globs, use that. (node_modules and dist are ignored by oxlint automatically / via .gitignore.)

- [ ] **Step 6: Update the root `lint` script and drop per-workspace lint scripts**

In the root `package.json`, set `"lint": "oxlint"`. Remove the `"lint"` script from `frontend/package.json` and from `e2e/package.json` (oxlint runs at the repo level; there is no per-workspace lint anymore).

- [ ] **Step 7: Run oxlint's autofixer, then get to a clean pass**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm exec oxlint --fix; pnpm run lint"`
oxlint's rule set is not identical to ESLint's, so it may surface new findings. Fix them as **real code changes**. Only disable a rule (in the relevant `.oxlintrc.json` `rules` block, set to `"off"`) if a finding is a genuine false positive or inappropriate for this project, and say which and why in the commit message. Do NOT add inline disable comments (the no-comments convention).
Expected final: `pnpm run lint` exits 0 with no errors.

- [ ] **Step 8: Confirm typecheck still passes (fixes didn't break types)**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm run typecheck"`
Expected: PASS (frontend + e2e).

- [ ] **Step 9: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A && git commit -m 'Replace ESLint with oxlint

Full replacement: remove eslint + its plugins from both workspaces, delete the
eslint.config.js files, and add oxlint (root devDep) with nested per-workspace
.oxlintrc.json (react + browser for frontend, node for e2e). Root lint script
now runs a single oxlint pass. <note any rules disabled / coverage gaps here>.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Task 3: Adopt oxfmt (first formatter)

**Files:**
- Modify: `package.json` (root: oxfmt devDep + `format`/`format:check` scripts)
- Create: `.oxfmtrc.json` (root)
- Reformat: all TS/TSX/JSON under `frontend/` and `e2e/`

**Interfaces:**
- Consumes: nothing.
- Produces: `pnpm run format` (oxfmt write + `dotnet format`) and `pnpm run format:check` (oxfmt --check + `dotnet format --verify-no-changes`). The Node/TS surface is oxfmt-formatted.

- [ ] **Step 1: Add oxfmt and initialize its config**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm add -DE -w oxfmt@0.57.0 && pnpm exec oxfmt --init"`
This creates `.oxfmtrc.json` with defaults (Prettier-compatible). Keep the defaults.

- [ ] **Step 2: Add the root format scripts**

In the root `package.json`, add:
```json
"format": "oxfmt frontend e2e && dotnet format Fmis.slnx",
"format:check": "oxfmt --check frontend e2e && dotnet format Fmis.slnx --verify-no-changes"
```
Scoping oxfmt to `frontend e2e` keeps it off the backend/infra/docs; oxfmt skips `node_modules` by default and honors `.gitignore` (so `dist` is skipped).

- [ ] **Step 3: Commit the config + scripts (before the reformat, so the reformat is isolated)**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add .oxfmtrc.json package.json pnpm-lock.yaml && git commit -m 'Add oxfmt formatter config and format scripts

Add oxfmt (root devDep) + .oxfmtrc.json, and root format / format:check scripts
that run oxfmt over the frontend + e2e workspaces and dotnet format for C#.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

- [ ] **Step 4: Run the one-time reformat**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm exec oxfmt frontend e2e"`
This rewrites the TS/TSX/JSON files to oxfmt's formatting.

- [ ] **Step 5: Verify the reformat didn't break anything**

Run:
```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm run format:check && pnpm run typecheck && pnpm run lint && pnpm --filter frontend run build && pnpm --filter frontend test"
```
Expected: all PASS. (`format:check` clean confirms idempotent formatting; typecheck/lint/build/test confirm the reformat is behavior-neutral.)

- [ ] **Step 6: Commit the reformat as its own commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A && git commit -m 'Reformat the frontend + e2e workspaces with oxfmt

One-time formatting pass now that a formatter exists; no behavioral changes.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Task 4: CI wiring + final verification

**Files:**
- Modify: `.github/workflows/ci.yml` (Frontend job)

**Interfaces:**
- Consumes: the `lint` and `format:check` root scripts (Tasks 2–3).
- Produces: CI lints with oxlint and checks formatting; typecheck runs under TS7.

- [ ] **Step 1: Update the Frontend job's lint step and add a format check**

In `.github/workflows/ci.yml`, in the `frontend` job, change the Lint step to run the root oxlint pass and add a Format check step. The steps become:

```yaml
      - name: Lint
        run: pnpm run lint

      - name: Format check
        run: pnpm exec oxfmt --check frontend e2e

      - name: Typecheck
        run: pnpm --filter frontend run typecheck

      - name: Test
        run: pnpm --filter frontend test

      - name: Build
        run: pnpm --filter frontend run build
```

(Only the frontend job changes: the old `pnpm --filter frontend run lint` becomes `pnpm run lint` — the whole-repo oxlint pass — and a `Format check` step is added. The Format check uses `oxfmt --check` directly rather than the full `format:check` script, because CI's frontend job must not require the .NET SDK for `dotnet format`. Leave the `Backend`, `Contract test`, and `Preview` jobs unchanged.)

- [ ] **Step 2: Validate the workflow YAML**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && ruby -ryaml -e \"YAML.load_file('.github/workflows/ci.yml'); puts 'ci.yml OK'\""`
Expected: `ci.yml OK`.

- [ ] **Step 3: Full local verification sweep**

Run:
```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && \
  pnpm run typecheck && \
  pnpm run lint && \
  pnpm exec oxfmt --check frontend e2e && \
  pnpm --filter frontend run build && \
  pnpm --filter frontend test && \
  pnpm --filter e2e run test:contract && \
  E2E_FRONTEND_URL=http://placeholder pnpm --filter e2e exec playwright test --config playwright.config.ts --list"
```
Expected: typecheck (TS7) clean; oxlint clean; oxfmt clean; frontend build + unit tests pass; contract suite 3/3 via Docker; Playwright lists 3 system tests (1 `@smoke`).

- [ ] **Step 4: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add .github/workflows/ci.yml && git commit -m 'Wire CI to oxlint + oxfmt

Frontend job lints via the root oxlint pass and adds an oxfmt format check;
typecheck now runs under TypeScript 7. Backend/Contract/Preview unchanged.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Notes for the executor

- **Tasks run their gates for real.** Task 1 (typecheck+build), Task 2 (oxlint clean + typecheck), Task 3 (format:check + typecheck + lint + build + test), Task 4 (full sweep incl. contract 3/3) are all runnable here — run them.
- **The one execution-time verification** is the exact oxlint rule keys for rules-of-hooks / react-refresh only-export-components and the `ignorePatterns` key (Task 2 Steps 4–5): confirm via `pnpm exec oxlint --rules` and the config `$schema`, per the spec's clause. Everything else is fixed.
- **Order:** Task 1 → 2 → 3 → 4. Task 3's reformat is deliberately a separate commit from its config/scripts so the large formatting diff is isolated.
- **oxlint findings (Task 2 Step 7)** are the one unpredictable-size step: fix them as real code changes; disable a rule in config only for genuine false positives, documented in the commit.
