# Repository Rule: Git Workflow

**Applies to:** all commits in this repository.

## Do not amend or force-push

- **Never amend an existing commit** to incorporate follow-up changes. If a change is needed, make a **new commit**.
- **Never force-push** (`git push --force` / `--force-with-lease`) to share history.

History is append-only. New work is a new commit, even when it's a small fix or revision to something just committed. This keeps history honest and review-friendly, and avoids rewriting commits others may have based work on.
