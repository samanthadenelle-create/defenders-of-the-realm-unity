<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_301 — Party persistence (wallet-keyed roster in GameState)

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
**Branch:** feat/tower-core-loop · **Lane:** 7 (Persistence/Backend) · **Depends on:** none (coordinate SaveSchema)

## Context
The party must survive logins. For now assume the player always connects with the same wallet / identifier.
Pieces exist: a `Wallet` module (identifier) and `GameStateService` (single source of truth that already
saves/loads). The combat HUD (WO-303) renders this roster.

## Goal
Persist the party **roster** (not live HP) keyed by wallet, with a local fallback id when no wallet is
connected; load on boot; save on change; default starter party if empty.

## Files to edit / create
- `Assets/_Modules/.../GameState*` — add a `PartyRoster` field (list of `{ classId/heroId, level, xp }`).
- New `PartyService` (Village or Core) — `GetRoster()`, `AddMember`, `RemoveMember`, `SetMember`, raises `RosterChanged`.
- Save profile keying: derive profile id from `Wallet` connected address; fallback to a stable local id
  (e.g. device/local GUID) when disconnected. One owner for `SaveSchema`/version bump.

## Scope
- Additive GameState field (coordinate with any other in-flight GameState edits — one at a time).
- Load roster on boot → if empty, seed the starter party (current default heroes/companions).
- Save on `RosterChanged` (debounced).
- Provide migration hook so pet unlock state (currently a PlayerPrefs blob in `PetUnlockTracker`) can fold
  into the same wallet-keyed save in a later WO (don't migrate pets here; just don't block it).

## Acceptance criteria
- [ ] Quit + relaunch with the same wallet/identifier → party roster is identical (members, levels, xp).
- [ ] No wallet connected → uses local fallback id; still persists across relaunch.
- [ ] Empty/first run → starter party seeded.
- [ ] GameState change is additive; SaveSchema version bumped by the single schema owner; no migration crash on old saves.
- [ ] Brace check passes; CompileGate OK; Windows build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't fork EconomyService/GameState save plumbing — extend it.
