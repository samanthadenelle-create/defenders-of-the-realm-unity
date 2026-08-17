# Dead Code Registry — built but never wired

**Status: KNOWN DICTIONARY** (durable registry, per memory `audit-outputs-as-known-dictionaries`).
Created 2026-08-16. Read-only sweep — **no code changed, no Unity run, no commit.**

Companion to `docs/reference/STACK_UTILIZATION_2026-08-09.md` (the file-level "what did we build that
never got plugged in?" pass). **This document is the member-level pass** — the exhaustive category-4
sweep (*public methods with zero call sites*) that the 2026-08-15 cross-silo sweep explicitly flagged
as its one remaining gap.

> **The one-line finding:** the tree's dominant failure mode is not dead files and not dead methods.
> It is **produced-but-never-consumed LAYER BOUNDARIES** — a finished system on one side of a seam and
> nothing on the other. A single-language reference scan structurally cannot see any of them.

---

## ⛔ Read this before you delete anything on this list

**A wrongly-declared-dead system that then gets deleted is a far worse outcome than a missed one.**
Every row below was checked against five false-positive classes, and each row states which checks ran:

| # | Class | Why a name-grep lies |
|---|---|---|
| **F1** | **Reflection / string binding** | `AdminOverlay` reaches Village types by reflection **by design** (the asmdef forbids the reference); `TownHudBridge` binds `GetMethod("SetPassiveXp")`; the regression harness binds suites by string |
| **F2** | **Unity lifecycle** | `Awake/Start/Update/OnTriggerEnter/OnDestroy`, `[RuntimeInitializeOnLoadMethod]`, `[ContextMenu]`, Animation Events — all engine-invoked. **22 of 38 zero-reference Village files self-install this way and ARE LIVE** |
| **F3** | **Scene / prefab GUID refs** | a MonoBehaviour attached in a scene has no `.cs` caller; UnityEvent wiring lives in YAML as `m_MethodName:` |
| **F4** | **`[MenuItem]` / batchmode** | invoked by string from `*.ps1` / `*.py` / `tools/` |
| **F5** | **Interface dispatch** | the implementation has no direct caller; the *interface* does |

Three further false-positive classes were discovered **during** this sweep and are folded into the
method (see §7 Methodology):
**F6** method-group delegate registration (`PanelRouter.Register(PanelId.X, OpenOverlay)`);
**F7** `internal` members called elsewhere in their own file;
**F8** nested-class members.

---

## Classification key

| Class | Meaning | Action |
|---|---|---|
| ★ **WIRE IT** | finished and valuable, one connection from working | the payoff class — ticket it |
| **DELETE IT** | genuinely superseded — *the replacement is named with `file:line`* | safe to remove |
| **KEEP, DOCUMENT WHY** | deliberately dormant: a seam awaiting a consumer, a fallback, or a type pinned by a regression's reflection | **deleting these breaks something** |
| **OWNER RULING** | dead because a design question was never answered | one owner sentence unblocks it |

---
