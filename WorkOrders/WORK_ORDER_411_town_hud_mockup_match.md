# WORK ORDER 411 — Town HUD must match `hud_mobile_town.png`

**Priority:** P1
**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** 4 — UI / HUD
**Type:** UI correctness (in-town / non-combat view → mockup parity)
**North-star:** `docs/UI_Mockups/hud_mobile_town.png` (mockup #42)
**Actual (current state):** `docs/UI_Mockups/WO-411_town_hud_actual_2026-06-11.png` *(owner to save/attach — pasted inline, not yet a file)*
**Filed:** 2026-06-11 (owner, via Notion). Notion WO DB is the live mirror; this is the repo spec.

---

## Problem
The in-town (non-combat) HUD does **not** match the owner's mockup. The HUD is the
separated presentation layer (`VillageHudController`, `DeNelle.HUD`) — fix is skin/layout
+ correct event wiring; **do NOT change data bindings** (WO-405 §69) and **never touch
gameplay objects** (ARCHITECTURE §2).

## The 11 deviations (each must be resolved to the mockup)
1. **Resources** — wrong corner + wrong icons (should be top-LEFT, correct resource icons).
2. **Duplicate hero health bars** — only one should exist.
3. **Missing Heart of Elarion** bar (should be top-left).
4. **Missing pet bars.**
5. **Quest tracker** — currently a persistent yellow panel overlapping the gear/"i" buttons;
   should be a **MODAL** (opened from the QUESTS action), not always-on chrome.
6. **BAG** — currently a gray panel; should be a **right-side icon** (a TOWN ACTION).
7. **Missing 4-button TOWN ACTIONS row** — BUILD · TALK · BAG · QUESTS (bottom-right).
8. **Wrong compass icon.**
9. **"Talk: Windmill" building prompt** — building interaction must route through the
   **TALK → NPC dialogue** path, not a bespoke building prompt.
10. **Missing wave timer / PLAY (Start Next Wave) banner** (top-center).
11. **Missing enemy INTEL panel** (top-right).

## Root-cause diagnosis (verified from code + the 07:56 build screenshot)
The deviations are TWO problems, not eleven independent ones:

**A. `VillageHudController`'s town chrome is GATED OFF on the castle hub.**
`EvaluateInVillage()` (VillageHudController.cs:1887) returns false unless the active scene
name equals `VillageSceneName = "Village2"` (line 64). The home hub is **`MainCastle_Hall`**,
so the check fails → `ApplyContext` hides `_townActionPanel`, `_castleBanner`, `_buildBtn`.
**This is why the TOWN ACTIONS row, Heart of Elarion bar, and BUILD are all "missing"**
(deviations #3, #7, #10-ish). The always-on top chrome (compass, gear, "i") DOES render,
which is why those show. **Fix:** recognize the hub scene(s) — mirror
`WorldSceneLoader.HubSceneNames` (`Village2`, `MainCastle_Hall`, `CastleHub*`) — so the
town chrome shows in the castle hub. (Do NOT ship this alone — see B, or it just adds the
chrome ON TOP of the stray panels and doubles the clutter.)

**B. Multiple INDEPENDENT bootstrapped HUDs render unconditionally and clutter the view**
(the rest of the deviations). From the screenshot, each wrong element maps to its own system:
- Duplicate hero bars (#2): a yellow "Hero" bar AND a green "Hero 100/100" bar — two HP systems.
- Resources top-right 0/0/0/0 (#1): a resource HUD in the wrong corner with placeholder icons.
- Quest tracker overlapping gear/"i" (#5): `DailyQuestHud`/`QuestTrackerHud` — persistent, must be a MODAL opened from QUESTS.
- Gray BAG panel (#6): `HeroEquipHud` — must be a right-side icon (the BAG town action).
- "Talk: Windmill" (#9): a *building* registering `MobileInteractButton` — building interaction must route through TALK→NPC dialogue, not a building Talk prompt.
- Compass (#8): wrong icon.
- Missing Heart/pet bars/wave-timer/INTEL (#3/#4/#10/#11): not built in the town chrome.

**So the real WO-411 work = CONSOLIDATE the HUD into ONE coherent layer** (VillageHudController
+ ElarionUiKit, layout groups), make the hub a recognized town context (A), and retire/fold the
redundant bootstrapped panels (B) — so the screen matches the mockup with no overlap.

## Consolidation map (verified via read-only HUD audit, 2026-06-11)
The clutter is independent bootstrapped HUDs overlapping VillageHudController. Per-element:
- **#2 duplicate hero bar:** `HeroHealth.cs` draws a SEPARATE OnGUI/IMGUI bar (line ~17) on
  top of VillageHudController's uGUI vitals → gate/remove the OnGUI bar (keep VillageHud vitals).
- **#6 BAG:** `HeroEquipHud` (own RuntimeInitialize bootstrap, gated to hub scenes) → gate OFF
  the hub; BAG becomes an icon in VillageHudController's action row (→ HeroInventoryController.Open).
- **#5 quest trackers:** `DailyQuestHud` (top-right chips) + `QuestTrackerHud` (top-left cards),
  both always-on bootstraps → gate OFF the hub; surface via a QUESTS modal. **Modal doesn't exist
  yet → dim QUESTS + log follow-up WO (don't ship dead button); removing the persistent tracker
  matches the mockup's "quests = modal" intent.**
- **#1 resources:** owned by VillageHudController (legacy strip + WO-339 town badges) → reposition
  to top-LEFT per mockup.
- **#3 Heart of Elarion:** VillageHudController.BuildCastleBanner (currently top-center) → top-left.
- **#7 TOWN ACTIONS row:** add BUILD·TALK·BAG·QUESTS to VillageHudController (now visible once
  fix-A recognizes the hub).
- **#8 compass:** `CompassHud` is a **UIDocument/UXML** component — UXML does NOT render in player
  builds (PIPELINE_STATE §8). Likely needs a code-built compass, not just an icon swap. (Separate fix.)
- **#9 "Talk: <building>":** NOT from `BuildingInteractable` (it correctly shows "Interact:"). The
  windmill is **misconfigured as a vendor/NPC** (registered "Talk:" via a vendor injector). → **moved
  to WO-413** (building-capability classification), out of the HUD layer.
- **#10 wave timer/PLAY · #11 INTEL:** build in VillageHudController town chrome.

## Requirements
- **UGUI layout groups** for the clusters — not hand-tuned anchoring (so it holds across
  resolutions / safe areas).
- **One design system** — build via `ElarionUiKit` (WO-405), no parallel theming.
- **Presentation-separated:** HUD exposes events/setters; Village bridges feed/consume them;
  Core (`IVillageHud`) stays minimal (least cross-level exposure).
- **No new per-frame scans** (honor the OuterWorld-leak hardening lesson).

## Permission-gate test (ships WITH this WO — ARCHITECTURE §2c)
The root cause (fix-A) is unit-testable, so it must ship with a test that locks it:
- **Extract the canonical hub-scene list to ONE shared source** (Core) read by BOTH
  `VillageHudController.EvaluateInVillage` AND `WorldSceneLoader` (today they keep two
  drifted lists — `"Village2"` vs `HubSceneNames` — and that drift IS the bug).
- **EditMode test** (mirror `Data/Tests/BuildingCatalogTest` pattern, no scene load):
  assert every canonical hub scene — `MainCastle_Hall`, `Village2`, `CastleHub*` — is
  recognized as town/village context by that single source. Adding a hub scene without
  registering it → test RED before a blank-chrome HUD can ship.
- This is the §2c "permission gate": the consolidation (fix-A/B) is only "done" if this
  test (and the verification gate below) pass.

## Verification gate (non-negotiable)
- **Side-by-side screenshot** of the built HUD vs `hud_mobile_town.png` + **owner sign-off.**
- **NO self-certification** — "compiles" / "looks right to me" is not acceptance.

## Partial down-payment already done (uncommitted, on `feat/tower-core-loop`)
A first pass toward #6/#7/#11 is built but **uncommitted**, pending this WO's proper
treatment + the gate:
- TOWN ACTIONS row (BUILD/TALK/BAG/QUESTS) bottom-right.
- BAG as a right-side icon (inventory open via `InventoryHudBridge`).
- INTEL slot top-right (gated/dimmed; `IntelRequested` event ready).
- Proximity-gated TALK (committed `05720be`) — addresses part of #9's intent.
These should be **folded into the full WO-411 implementation**, not shipped piecemeal.

## What NOT to touch
- No changes to data bindings / update logic / event semantics (skin + layout only).
- No gameplay-object edits.
- Do not implement while WO-405 is incomplete (this is BLOCKED on it).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
