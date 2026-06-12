# Repository Rule: Test-Driven Development (TDD)

**Applies to:** all production code in this repository, every layer, every phase. This is non-negotiable and a core concept of how code here is developed.

## The rule

**No production code is written before a failing test that requires it exists and has been run and observed to fail.**

Follow the red → green → refactor cycle for every behavior:

1. **RED** — Write the smallest test that expresses the next bit of behavior. Run it. Confirm it fails, and that it fails for the *expected* reason (assertion/compile error you predicted) — not some unrelated error.
2. **GREEN** — Write the minimum production code to make that test pass. Nothing more. Run the test; confirm it passes.
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

## Plans must be ordered test-first

When writing an implementation plan, order the steps so the failing test precedes the production code it covers, and so a feature's test task is not deferred to after its production code is already built. A plan that writes production code in one task and its tests several tasks later is a TDD violation and must be restructured.

## Verifying the discipline

For each behavior, the transcript should show, in order: the test written → the test run failing → the production code → the test run passing. "Implemented X, then added tests" is the smell this rule exists to eliminate.
