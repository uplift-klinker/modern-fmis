# Repository Rule: Test-Driven Development (TDD)

**Applies to:** all production code in this repository, every layer, every phase. This is non-negotiable and a core concept of how code here is developed.

## The Three Laws of TDD

We follow Robert C. Martin's Three Laws of TDD (<http://butunclebob.com/ArticleS.UncleBob.TheThreeRulesOfTdd>):

1. **You are not allowed to write any production code unless it is to make a failing unit test pass.**
2. **You are not allowed to write any more of a unit test than is sufficient to fail — and compilation failures are failures.**
3. **You are not allowed to write any more production code than is sufficient to pass the one failing unit test.**

These laws lock you into a tight cycle measured in seconds, not minutes: you write a fragment of a test, it fails to compile (Law 2 — that counts), you write just enough production code to compile and fail the assertion, then just enough to pass (Laws 1 & 3), and refactor. You are never more than a few seconds from a passing test.

In red → green → refactor terms:

1. **RED** — Write only as much test as is needed to fail (a missing type that won't compile is a sufficient failure). Run it; confirm it fails for the expected reason.
2. **GREEN** — Write only as much production code as makes that one failing test pass. Nothing more.
3. **REFACTOR** — Clean up production and test code while keeping the test green.

Then repeat for the next behavior.

## What this means in practice

- The **test is written and committed-to first**. If you find yourself writing a handler, controller, validator, or any production type before its test exists and fails, stop — you are not doing TDD.
- This applies to **every layer**, not just Core:
  - Core slices (commands/queries/handlers/validators): write the failing handler/validation test first.
  - **Api controllers/endpoints: write the failing integration test (through the real HTTP pipeline) first, then the controller.** Do not build the controller and add tests afterward.
  - Any other testable unit: test first.
- Tests exercise **real behavior** through the real composition — the command/query bus, or the HTTP pipeline — never mocks. Seed data through the application's own commands. (See [`backend-code-conventions.md`](backend-code-conventions.md).)
- A bug fix starts with a **failing test that reproduces the bug**, then the fix.
- **A plan's code block is the destination, not a license to write it.** A task in an implementation plan often shows a complete file (a slice, a provider/seam, a controller). That is the *end state* reached over several red→green cycles — **not** something to paste in wholesale to satisfy one failing test. Write only the slice the current failing test exercises; let later tests (consumers, route guards, app shell, feature pages) drive out the rest of the file — even for seams, providers, and adapters the plan lists in full. Transcribing a whole file to pass a single assertion is a Law 3 violation, regardless of what the plan shows.

## Plans must be ordered test-first

When writing an implementation plan, order the steps so the failing test precedes the production code it covers, and so a feature's test task is not deferred to after its production code is already built. A plan that writes production code in one task and its tests several tasks later is a TDD violation and must be restructured.

## Verifying the discipline

For each behavior, the transcript should show, in order: the test written → the test run failing → the production code → the test run passing. "Implemented X, then added tests" is the smell this rule exists to eliminate.
