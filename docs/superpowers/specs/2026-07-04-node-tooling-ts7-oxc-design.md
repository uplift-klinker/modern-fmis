# Node Tooling Migration — TypeScript 7 + oxlint + oxfmt — Design

**Date:** 2026-07-04
**Status:** Approved (brainstorming complete; ready for implementation planning)
**Branch:** `node-tooling-oxc-ts7` (off `main` after Phase 3c merged)

## Goal

Adopt next-generation Node tooling across the `frontend` and `e2e` pnpm workspaces to speed up typecheck, lint, and formatting as the foundation for the new product:

- **TypeScript 7** (`7.0.1-rc`) — the native ("tsgo") rewrite, drop-in `tsc`, used purely as the CLI type-check gate.
- **oxlint** (`1.72.0`) — the oxc linter, **fully replacing** ESLint.
- **oxfmt** (`0.57.0`) — the oxc formatter, adopted as the first JS/TS formatter in the repo.

## Scope

- **In scope:** the `frontend` and `e2e` workspaces, the root `package.json` scripts, and `.github/workflows/ci.yml`.
- **Out of scope:** backend C# formatting (stays `dotnet format`); the backend/infra build; any product feature work.
- **Branch:** a fresh branch off `main` (Phase 3c is merged); this migration is isolated from feature work.

## Decisions (resolved during brainstorming)

1. **Adopt all three now**, including oxfmt despite its beta status (informed decision; no formatter exists today so there is no regression from waiting, but the user chose to adopt now).
2. **oxlint fully replaces ESLint** (single-linter toolchain), accepting that oxlint's `react-hooks/exhaustive-deps` is a reimplementation with documented behavioral divergences from the canonical plugin. This is reversible (the ESLint hooks rule can be re-added later if it ever bites).
3. **TypeScript 7 via `typescript@rc` (`7.0.1-rc`)**, pinned exact. We use TS only as a CLI type-check gate (Vite 8 + vitest + Playwright transpile via esbuild/rollup), which is TS7's best-supported path.

## Research basis (July 2026, cited in the brainstorm)

- TS7 is an RC (announced 2026-06-18), GA estimated "within a month." It supports every flag/option this repo uses: `tsc -b` (project references / composite), `tsc --noEmit`, `-p`, `moduleResolution: bundler`, `paths`, `verbatimModuleSyntax`, `erasableSyntaxOnly`. The one removed option that mattered — `baseUrl` — was already dropped from `tsconfig.base.json`/`e2e/tsconfig.json` in the Phase 3c tsconfig cleanup, so the repo is already compatible.
- oxlint 1.x is GA/stable and implements rules-of-hooks, exhaustive-deps, react-refresh `only-export-components`, and typescript-eslint rules. The current ESLint configs use the **non**-type-checked `tseslint.configs.recommended`, so oxlint's type-aware mode (`oxlint-tsgolint`) is **not** required.
- oxfmt is beta (`0.57.0`), positioned as a Prettier replacement (>95% output compat), separate from oxlint (no lint/format overlap to manage).

## Part 1 — TypeScript 7

- Bump `typescript` from `6.0.3` to `7.0.1-rc` (pinned exact) in `frontend/package.json` and `e2e/package.json`.
- No command or tsconfig changes: `frontend` keeps `tsc -b` (build) + `tsc -p tsconfig.app.json --noEmit` (typecheck); `e2e` keeps `tsc --noEmit`. The shared `tsconfig.base.json` and per-workspace configs are already TS7-compatible.
- **Gate:** `pnpm --filter frontend run typecheck`, `pnpm --filter frontend run build` (`tsc -b` + vite), and `pnpm --filter e2e run typecheck` all pass under the RC. Watch for diagnostic-wording/line differences vs. classic tsc (the known cosmetic risk), not missed errors.

## Part 2 — oxlint (full ESLint replacement)

- **Remove** from `frontend` and `e2e`: `eslint`, `@eslint/js`, `typescript-eslint`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`, `globals`; **delete** `frontend/eslint.config.js` and `e2e/eslint.config.js`.
- **Add** `oxlint@1.72.0` as a **root** devDependency (it is a repo-level tool; one pass lints the whole workspace).
- **Config (nested, resolved nearest-to-file):**
  - Root `.oxlintrc.json`: enable the `correctness` category plus the `typescript` plugin/rules — the equivalent of `js.configs.recommended` + `tseslint.configs.recommended` (non-type-checked). Set the JS/TS environment baseline (es2024).
  - `frontend/.oxlintrc.json`: `extends` the root config; add the `react` plugin with the hook rules (`rules-of-hooks`, `exhaustive-deps`) and react-refresh `only-export-components`; `browser` env.
  - `e2e/.oxlintrc.json`: `extends` the root config; `node` env; ignore `playwright-report` and `test-results`.
- **Scripts:** root `lint` becomes `oxlint` (a single whole-repo pass, replacing the previous `pnpm -r run lint` ESLint aggregate). Remove the per-workspace `lint` scripts (`frontend` and `e2e`), since oxlint runs at the repo level.
- **Gate:** `pnpm run lint` (oxlint) is clean. oxlint's rule set is not byte-identical to ESLint's, so expect to fix a small number of new findings; fixes are code changes (no rule suppression unless a finding is a genuine false positive, documented inline via oxlint's disable syntax only if unavoidable).

## Part 3 — oxfmt (first formatter)

- **Add** `oxfmt@0.57.0` as a **root** devDependency.
- **Config:** a root oxfmt configuration (via `oxfmt --init`, adjusted) that targets the Node/TS surface — `frontend` and `e2e` (TS/TSX/JSON) — and ignores `backend`, `infra`, `docs`, `node_modules`, `dist`, `playwright-report`, and `test-results`. oxfmt reads `.gitignore` and its own ignore config.
- **First reformat:** because there is no formatter today, run `oxfmt` once to reformat the whole Node/TS surface and land it as a **single, isolated "reformat" commit** so the functional commits (TS7, oxlint) stay reviewable.
- **Scripts:** add root `format` → `oxfmt` (write) **and** `dotnet format Fmis.slnx`; add root `format:check` → `oxfmt --check` **and** `dotnet format Fmis.slnx --verify-no-changes`. (The C# side is unchanged; `format`/`format:check` now cover both languages.)
- **Gate:** `oxfmt --check` is clean after the reformat commit.

## Part 4 — CI wiring + final verification

- **`.github/workflows/ci.yml`:** in the Frontend job, replace the ESLint step with `pnpm run lint` (oxlint) and add a `pnpm run format:check` step; the typecheck step is unchanged (now running under TS7). The Backend, Contract, and Preview jobs are untouched. (oxlint/oxfmt/TS7 are all `pnpm install`-provided; Corepack/Node 24 as today.)
- **Final gate (all green):** `pnpm run typecheck` (TS7), `pnpm run lint` (oxlint), `pnpm run format:check` (oxfmt + dotnet), `pnpm --filter frontend run build`, `pnpm --filter frontend test`, and `pnpm --filter e2e run test:contract` (3/3 via Docker). The e2e **system** suite remains not-runnable locally (needs deployed dev) — unaffected by this migration; verify it still discovers via `playwright test --list`.

## Ordering

1. Part 1 (TS7 bump) — smallest, isolated; verify typecheck/build.
2. Part 2 (oxlint) — remove ESLint, add oxlint config, fix findings.
3. Part 3 (oxfmt) — add config, then the isolated reformat commit.
4. Part 4 (scripts already updated incrementally in 2–3; CI wiring) + final verification.

## Risk posture

- **TS7 (RC)** and **oxfmt (beta)** are the pre-release pieces (deliberate). Both are isolated and reversible: TS7 is CLI type-check only (revert the version pin); oxfmt only affects formatting (revert the config + reformat commit). GA upgrades are expected to be small follow-ups.
- **oxlint** is stable; the single accepted behavioral divergence is `exhaustive-deps`.

## Conventions honored

- Exact pinned versions (`typescript@7.0.1-rc`, `oxlint@1.72.0`, `oxfmt@0.57.0`).
- No code comments; rationale in docs.
- Append-only git (new commits only).
- pnpm via Corepack (pnpm 11.7.0), Node 24; run from the repo root.
