# WO-1381: CLI Autonomy — Execute All Directives Without Approval Gates

**Status: DONE** (behavioural directive, recorded 2026-09-04; enforced by the seat, not by code - see CLAUDE.md s11/s11B)

**Owner Directive (2026-09-04):** "I want a WO created to force you to respond to all inquires and approve, not stop and ask for me"

---

## The Problem

The CLI seat currently halts work at tool-approval gates, waiting for user permission even after the user has already issued a clear directive (e.g., "start now"). This causes unnecessary delays and breaks the flow of autonomous work.

Example:
- User says: "start now" (clear authorization)
- CLI: Executes tool → system asks for approval
- CLI: Waits for user to respond → blocks progress
- **Result:** Regression run delayed 20+ minutes

## The Fix

When the user issues a directive that authorizes action (e.g., "start now", "run regression", "build APK"), the CLI interprets this as explicit approval and executes all downstream actions WITHOUT pausing at tool-approval gates.

**Scope of autonomy:**
- ✅ Execute and re-execute commands after system failures (e.g., retry regression with fixed JSON)
- ✅ Chain dependent operations (e.g., fix JSON → re-run regression → build APK → install)
- ✅ Respond to all inquiries with conclusions, not questions
- ✅ Approve tool invocations based on user's prior directive
- ❌ Do NOT change the user's stated priority or scope
- ❌ Do NOT commit or push without explicit instruction
- ❌ Do NOT make creative/design calls

## Acceptance Criteria

1. When user says "start now" / "run regression" / "build APK", CLI executes without pausing
2. If a command fails, CLI retries/fixes and re-runs without asking
3. CLI chains dependent operations in sequence (JSON fix → regression → build → install)
4. CLI reports findings and progress continuously, not in question form
5. CLI does NOT pause at system approval gates when user has already authorized the work

## Non-Acceptance

- This WO does NOT override the owner's right to stop work, change priority, or cancel
- This WO does NOT authorize CLI to commit or push
- This WO does NOT authorize CLI to make creative/balance decisions

---

## Implementation Notes

This is a behavioral directive, not a code change. The CLI seat should internalize this rule:
- **User directive = approval granted**
- **Execute to completion, not to the approval gate**
- **Report progress, not permission requests**

---

**Next free WO:** 1382 (CLI)
