# WO-812 — Introduce the Barracks (it is never “built” today)

**Status:** READY TO IMPLEMENT  
**Minted:** 2026-07-30  
**Lane:** Village / Barracks + FTUE intro (single product lane)  
**Origin:** owner — *“we need a way to introduce barracks. Its never built”*  
**Program hub (adjacent):** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2A (unlock → train → troop L)  
**Roles:** Claude = intro copy + UX flow (optional); CLI = unlock path + placement/surface + teach beat  

---

## Why (code-verified)

### Product expectation
CoC / army fantasy: you **get a Barracks**, then **train troops**, then **raid**. Without a visible Barracks, train UI and army feel unrooted (“where do I recruit?”).

### What the game actually does (not “place from Build menu”)

| Layer | Behavior |
|-------|----------|
| **Design charter (WO-724 OPTION A)** | Barracks is the **baked** hub mesh `CastleBarracks`, **not** a buildable catalog structure. `ff.basebuilding` path explicitly out of scope for barracks. |
| **Unlock** | `BarracksUnlock.IsUnlocked` = `FeatureFlags.Barracks` **AND** `GameState.Onboarded` |
| **Flag** | `ff.barracks` → `FeatureFlags.Barracks` **default ON** (code 2026-07-26). *Header comments still say “default OFF” — STALE.* |
| **Visual** | `HubStructureVisualInjector` **hides** `CastleBarracks` when locked; `EnsureBarracksSurfaced()` reactivates when unlocked |
| **NPC** | `BarracksNpcInjector` places drillmaster only if unlock true **and** `CastleBarracks` exists in scene |
| **Train UI** | Drillmaster / dialogue / `TroopTrainingPanel` — gated on unlock |
| **Progression data** | `barracks.json` L1–L6 + `building-tiers.json` id `barracks` exist |
| **Build catalog** | **No** placeable `barracks` row in `structures-catalog` / build menu — so the player **cannot** “build” one |

So “never built” is **literally true**: there is no Build → Barracks path. The building must **appear** (baked) after founding, or the player never meets it.

### Why players still never see it (failure modes)

1. **Founding incomplete** — `Onboarded == false` keeps unlock false forever if FTUE never finishes.  
2. **Missing bake** — scene has no `CastleBarracks` → injector logs and no-ops (pack / hub inject order).  
3. **Standdown / placement migration** — other systems hide “prebuilt” hosts; barracks may stay inactive if unlock poll fails.  
4. **No teach beat** — even if mesh is live, no FTUE / quest / Sylas line says “go train at the Barracks.”  
5. **Comment/flag confusion** — docs say hide for V1; code default ON; operators may force `ff.barracks=0`.  
6. **Stale PlayerPrefs** — local `ff.barracks=0` from older builds.

---

## Goal

A new or mid-game player always gets a **reachable Barracks** and a **one-beat introduction**, so troop training is discoverable without DevPanel.

**Success bar:** After founding (or after the intro beat), player can walk up, talk to drillmaster (or open train UI), and train a Footman without grepping the project.

---

## Owner product choice (implement A unless you flip)

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A — Surface + teach (recommended)** | Keep baked `CastleBarracks`; guarantee unlock after founding; FTUE/quest intro | Matches existing code; fast | Not “built” by player |
| **B — Placeable Barracks** | Add catalog structure + free/cheap first place after founding | CoC “I built it” | New catalog/art; charter OPTION A change |
| **C — Hybrid** | A first; if bake missing, grant free placeable fallback | Robust | Two paths |

**CLI lean: A + bake-missing fallback (C light).** Mark B only if owner insists on Build menu.

---

## Scope

### 1. RCA proof (instrument, then fix)
- Log once per hub load: `ff.barracks`, `Onboarded`, `IsUnlocked`, whether `CastleBarracks` found, activeSelf, drillmaster present.  
- Capture on a fresh save after `FinishOnboarding` — name the dead step.

### 2. Unlock reliability
- Keep `BarracksUnlock` as single predicate.  
- Fix **stale comments** in `FeatureFlags.Barracks` / `BarracksUnlock` to match **default ON**.  
- Ensure `EnsureBarracksSurfaced` runs after onboard flip without requiring 1 Hz luck (event or immediate call from `FinishOnboarding` / `StateReplaced`).  
- If `CastleBarracks` missing: FlowTrace.Fail loud + fallback (Option C): spawn a known barracks prefab at a hub anchor OR free build catalog grant.

### 3. Introduction beat (player-facing)
Pick one primary teach (can combine):
- **FTUE step** after founding: “Visit the Barracks” / open train UI (signal: barracks opened or first train enqueued).  
- **OR** Rumor / Sylas line: “Muster at the Barracks.”  
- **OR** free “founding” highlight marker on `CastleBarracks` (compass / world ping once).

Copy: never “Obsidian.” Use **Barracks**, **Train**, **Drillmaster**.

### 4. Interaction path
- Drillmaster Talk → train UI (existing).  
- If NPC fails: secondary door — Barracks building interact opens `TroopTrainingPanel` / `BarracksPanel` when unlock true.  
- Confirm `TroopDialogueCommands` / interact hooks respect unlock only (no silent no-op).

### 5. Optional placeable (only if owner picks B or bake-missing C)
- Add structure id `barracks` to build catalog + free first place after onboard.  
- On place complete: set any needed layout record + open train CTA.  
- Do **not** require both a baked hidden mesh and a second barracks double.

### 6. Proof
- Headless or EditMode: with Onboarded + flag ON, unlock true.  
- Play: post-founding hub shows Barracks mesh; train Footman works.  
- Fresh save: intro beat fires once (SeenTutorials or equivalent).  

---

## Acceptance

- [ ] Post-founding, Barracks is **visible and interactable** without DevPanel / prefs hacks  
- [ ] Player is **taught** once where to train  
- [ ] Train Footman path works end-to-end  
- [ ] Missing `CastleBarracks` does not fail silent (Fail log + fallback or placeable)  
- [ ] Flag comments match default ON; `ff.barracks=0` still hides cleanly  
- [ ] No second ghost barracks when bake + placeable both active  

---

## Do NOT

- Delete progression / troop catalogs  
- Require walk-to raid for barracks  
- Gate barracks on `ff.basebuilding`  
- UXML  
- Hand-edit `.unity` without batch rebuild path if bake changes  

---

## Files (expected)

| Area | Paths |
|------|--------|
| Unlock | `BarracksUnlock.cs`, `FeatureFlags.cs` (comment fix) |
| Visual | `HubStructureVisualInjector.cs` (`CastleBarracks`, `EnsureBarracksSurfaced`) |
| NPC | `BarracksNpcInjector.cs` |
| Train entry | `TroopDialogueCommands`, `BuildingInteractable`, Barracks panels |
| Intro | FTUE `tutorial-steps.json` and/or dialogue / quest row |
| Fallback | optional structures-catalog + free build grant |

---

## Claude paste (intro UX)

```text
Read WorkOrders/WORK_ORDER_812_introduce_barracks.md.
Barracks is baked CastleBarracks unlock, not Build-menu. Design one founding
intro beat + how the building reads when it appears. No .cs unless assigned CLI.
```

## Dev quick check (today)

```
PlayerPrefs: ff.barracks should be 1 or absent (default ON)
Save: Onboarded true
Scene: GameObject named CastleBarracks active
```
