<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 460 — Surface the Building Perk/Research Track (unlock the dead Village-Tier gate)

**Status: READY TO IMPLEMENT**
**Classification: NEW (surfacing gap — the backend EXISTS but is unreachable; this is not a greenfield feature).**
**Lane:** 6 Economy/Progression (+ 4 UI/HUD for the Heart-tier upgrade control).
**Extends (does NOT duplicate):** WO-432 (building perk/research + tech-tree). The data model, services,
VM, View, SaveSchema, ModifierService folding, and icons from WO-432 are ALL built and correct. This WO
fixes the one missing piece that makes the whole track dead on the screen.
**Canon:** memory `building-upgrade-tier-perk-techtree`; §8 (code-built UI); §12 (instrument-first).
**Source ticket:** F8 (owner, MainCastle_Hall): *"there is a button to upgrade [building], but nothing to
upgrade a skill or perk."*

---

## 1. Problem — what is actually missing (proven from code, not inferred)

The F8 report reads as "the perk side was never built." It WAS built (WO-432, fully). The real defect is
that **the perk rows are permanently locked behind a Village/Stronghold Tier that nothing in the game can
ever raise.** The track renders but is dead.

**Proof (the data line that pinpoints the dead step):**
- `BuildingPerkService.CanResearch` (`Assets/_Modules/Village/Buildings/Progression/BuildingPerkService.cs:37-48`)
  gates a perk on TWO conditions:
  1. `ModifierService.TierOf(buildingId) < unlock` → "Upgrade the building to Tier N first." (achievable — the
     building upgrade button works.)
  2. `VillageTierService.Current < unlock` → **"Locked — needs Village Tier N."**
- Every authored perk's `unlock` = the tier it sits under in `building-tiers.json`. The *first* perk of each
  building sits under **tier 1**, so it needs `VillageTier >= 1`.
- `VillageTierService.Current` reads `GameState.VillageTier`, which **defaults to 0** and is raised ONLY by
  `VillageTierService.TryUpgrade()`.
- **`VillageTierService.TryUpgrade()` is called by NOTHING.** Grep of `Assets/_Modules` for `VillageTierService`
  returns only its own definition and the one read in `BuildingPerkService`. There is **no UI, no trigger, no
  NPC, no Heart-of-Elarion control** that raises the Village Tier.
- Net result: `VillageTier` is stuck at 0 forever → EVERY perk row shows "Locked — needs Village Tier 1+" and
  is non-interactable → the player sees a building-upgrade button and a column of locked perks they can never buy.
  That is exactly "nothing to upgrade a skill or perk."

**What is already correct (do NOT rebuild — be the SME):**
- Data: `Assets/Resources/Data/Canonical/building-tiers.json` (+ `StreamingAssets/` mirror) — perks fully
  authored per building/tier, with `isSignature` capstones and `iconId`s.
- Services: `BuildingPerkService.TryResearch` (spends Gold/Coins, records, persists, recomputes),
  `BuildingTierCatalog.FindPerk/PerkUnlockTier`, `VillageTierService` (cost ladder 250/500/750 Crystals).
- Folding: `ModifierService.Compute` (`ModifierService.cs:105-119`) folds owned perks into the active
  `GameModifiers` on top of tier modifiers — towers/troops/raids already consume them.
- Persistence: `SaveSchema` has `villageTier` + `ownedBuildingPerks` (v24); migration seeds them.
- VM/View: `BuildingUpgradeVM` emits perk rows ("perk:<id>", icon role, owned/locked/affordable);
  `BuildingUpgradePanelMvvm` renders them under the tier ladder and routes taps to `Select("perk:<id>")`.
  Flag `FeatureFlags.BuildingUpgradePanel` is **`defaultOn: true`** (`FeatureFlags.cs:72` — the XML doc-comment
  saying "Default OFF" is stale; the code is ON), so the MVVM panel IS the live one.
- Icons: resolve from `Resources/HudIcons/BuildingUpgrades/<iconId>.jpg` in the View
  (`BuildingUpgradePanelMvvm.cs:336`); the 16 authored `.jpg`s exist there. (NOTE: WO-432's prose said
  `HudItems/BuildingUpgrades/` — that path is stale/empty; the View+JSON+files all agree on `HudIcons/`.
  Do NOT repoint anything — it is consistent as-is.)

So the delta is small and targeted: **give the player a way to raise the Village/Stronghold Tier**, plus a
couple of UX clarity touches so a locked perk explains itself and a researchable one is obviously reachable.

---

## 2. Proposed design — wire the Heart-of-Elarion tier control

Owner-decided anchor (WO-432 §2 + VillageTierService header): the Village/Stronghold Tier is raised at the
**Heart of Elarion** (the Keep is removed, §7), bought with **Crystals** (the existing `VillageTierService`
cost ladder). We surface that with one button-driven panel, reusing the existing MVVM plumbing — no new
currency, no new service math.

Two faithful-WC3 layers stay intact: building tier ladder (Gold/Wood/Food/Crystal per WO-430) + Gold-cost
research perks gated by `min(building tier, Village tier)`. This WO only adds the **Village-tier raise**.

### 2a. Village-Tier upgrade control (the missing primary)
Add a **"Advance the Realm" (Village/Stronghold Tier)** action reachable from the Heart of Elarion in
MainCastle_Hall. Preferred implementation, in priority order — pick the lowest-friction that fits the live
interaction model (CLI confirms against the scene):

- **Option A (preferred): a dedicated tier panel** — a small code-built MVVM panel (`VillageTierPanelMvvm`,
  §8 code-built uGUI, mirror `BuildingUpgradePanelMvvm` chrome) bound to a new `VillageTierVM`. Shows:
  current tier / max, the next-tier Crystal cost (`VillageTierService.NextCost()`), what it unlocks
  ("Unlocks Tier N building upgrades + research"), and a Gold-frame **"Advance the Realm"** button →
  `VillageTierService.TryUpgrade()`. Opened by interacting with the Heart (see `HeartHudBridge` /
  `HeartController` for the existing Heart interaction surface) OR a HUD entry point if the Heart has no
  tap affordance.
- **Option B (cheaper): fold it into the building-upgrade panel** when the focused building is the town
  center / Heart — add a "Realm Tier" row at the top of `BuildingUpgradeVM` for the Heart that routes to
  `VillageTierService.TryUpgrade()`. Only if the Heart is modeled as an upgradable building id; it is not
  today (`building-tiers.json` has no heart entry), so A is cleaner.

The VM/View must follow the MVVM seam: the View reads only the VM; the VM calls `VillageTierService` and
raises `Changed` on `ModifierService.Changed` + economy change (same wiring as `BuildingUpgradeVM`).

### 2b. UX clarity in the existing building panel (small)
- The perk row's locked reason already comes from `CanResearch` via `Select`, but on first paint a locked
  perk shows only the generic "LOCKED" chip. **Surface the specific reason on the row** (e.g. tooltip or the
  cost-line text): when `locked`, render the `CanResearch(out reason)` string instead of the Gold price, so
  the player reads "Needs Village Tier 1" / "Upgrade the building first" directly — not a bare LOCKED.
  Add a `LockReasonFor(perkId)` accessor to `BuildingUpgradeVM` (pure; calls `BuildingPerkService.CanResearch`)
  and have the View use it for locked perk rows. Keep the MVVM seam (View reads VM only).
- Optional polish (owner call): visually separate the **Research / Perks** section from the tier ladder with
  a small header row ("Research") so it reads as a distinct track, addressing the "I don't see a perk to
  upgrade" perception even before any are unlocked.

### 2c. Reconcile the village-tier requirement vs. building tier (design check — confirm w/ owner)
With the current authoring, a tier-1 perk needs BOTH building-tier 1 AND Village-tier 1. That means the
player must (a) upgrade the building once and (b) advance the realm once before the FIRST perk is buyable.
That is the faithful-WC3 gate and is fine — **but it must be reachable**, which 2a fixes. No JSON change
required. (If the owner wants the very first perks buyable with just the building upgrade, the cleaner lever
is to set those perks' implicit gate via authoring; do NOT special-case in code. Leave as-is unless owner asks.)

---

## 3. Data model
No new data fields. `VillageTier` (int) + `OwnedBuildingPerks` (List<string>) already exist in
`GameState`/`SaveSchema` (v24). `VillageTierService.NextCost()` already defines the Crystal ladder
(250/500/750). Perks already authored in `building-tiers.json`. **Do not touch the JSON or SaveSchema.**

---

## 4. UI (code-built per §8 — no UXML)
- New `VillageTierPanelMvvm` (View) + `VillageTierVM` (pure VM) under
  `Assets/_Modules/Village/Buildings/Progression/`. Mirror `BuildingUpgradePanelMvvm`/`BuildingUpgradeVM`
  chrome + binding exactly (BuildModalCanvas 31000, Scrim, PanelFramed, Gold ButtonPack, Close).
- Register a new `PanelId.VillageTier` (add to the PanelId enum + PanelRouter, mirroring
  `PanelId.BuildingUpgrade`).
- Heart interaction → open it (extend the existing Heart tap/interact path; see `HeartHudBridge.cs`
  / `HeartController.cs` for where the Heart already surfaces UI — CLI to confirm the hook point).
- Behind a feature flag (`FeatureFlags.VillageTierPanel`, mirror `BuildingUpgradePanel`) so it can ship
  dark and be flipped after owner felt-check — but default it **ON** once verified (the whole perk track is
  dead without it; shipping it OFF reproduces the bug).

---

## 5. Files to CREATE
- `Assets/_Modules/Village/Buildings/Progression/VillageTierVM.cs` (pure VM, unit-testable, no UnityEngine UI types).
- `Assets/_Modules/Village/Buildings/Progression/VillageTierPanelMvvm.cs` (code-built View).
- (If a flag is added) extend `Assets/_Modules/Core/FeatureFlags.cs` with `VillageTierPanel`.

## 5b. Files to TOUCH
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs` — add `LockReasonFor(perkId)` accessor
  (§2b). No change to existing perk-row build logic beyond exposing the reason.
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` — render the specific lock
  reason on locked perk rows (§2b); optional "Research" section header (§2b polish).
- `PanelId` enum + `PanelRouter` registration site (wherever `PanelId.BuildingUpgrade` is declared) — add
  `VillageTier`.
- `Assets/_Modules/Village/Heart/HeartHudBridge.cs` (or `HeartController.cs`) — hook the Heart interaction
  to open the Village-Tier panel. CLI confirms the existing interaction surface; do NOT invent a new one.
- A bootstrap/injector to spawn `VillageTierPanelMvvm` in the hub (mirror the existing
  `BuildingUpgradePanelMvvm` bootstrap — find it via the panel's `Awake`/register pattern).

---

## 6. INSTRUMENT-FIRST note (§12 — BINDING)
Before AND after the wiring, prove the flow with captured data, not by reading code:
- Add `FlowTrace.Step`/`Warn` in `VillageTierService.TryUpgrade` (entry, cost, spend ok/fail, new tier) and
  in `BuildingPerkService.CanResearch`/`TryResearch` (which gate refused — building-tier vs village-tier vs
  afford). These pinpoint exactly which gate blocks a perk.
- Headless verify (AutoPilot fleet / EditMode): from a fresh state, assert (a) a tier-1 perk's
  `CanResearch` returns false with reason "needs Village Tier 1", (b) after `VillageTierService.TryUpgrade()`
  (and the building reaching tier 1) it returns true, (c) `TryResearch` spends Coins, persists, and
  `ModifierService.Active` changes by the perk's modifier, (d) reload keeps the owned perk. Capture the
  `[Flow:*]` lines proving the gate opened. This is the data that closes the ticket — not "it looks wired."

---

## 7. Acceptance criteria
- [ ] The player can raise the Village/Stronghold Tier from the Heart of Elarion (spends Crystals via
      `VillageTierService.TryUpgrade`), and it persists across reload.
- [ ] After raising both the building tier and the Village tier to the perk's unlock level, the perk row
      becomes researchable; tapping it spends Gold (Coins), marks it OWNED, persists, and changes
      `ModifierService.Active` by the perk's modifier (headless assert).
- [ ] Locked perk rows display the SPECIFIC reason ("Needs Village Tier N" / "Upgrade the building first"),
      not a bare LOCKED chip.
- [ ] MVVM seam intact: the new View reads only its VM; no game-state reads in any View.
- [ ] EditMode/headless test covers: village-tier raise, perk gate open, perk buy + modifier apply, reload
      persistence. `[Flow:*]` capture attached to the RESULT.
- [ ] Brace-balance gate + `COMPILE_GATE_OK` on every `.cs` touched.

## 8. What NOT to touch
- `building-tiers.json` / its StreamingAssets mirror (perks already authored correctly).
- `SaveSchema` (`villageTier` + `ownedBuildingPerks` already present — do NOT add fields).
- `ModifierService.Compute`/`Apply` (already folds perks correctly).
- `BuildingPerkService.TryResearch`/`CanResearch` gate math (correct — the bug is upstream: no way to raise
  Village Tier).
- The icon resolver path (`HudIcons/BuildingUpgrades/`) — it is consistent with the JSON + the files; the
  WO-432 `HudItems/` mention is stale. Do NOT repoint.
- The building-upgrade tier ladder cost/advance math (`BuildingUpgradeService.TryUpgrade`).
- The MVVM seam — no game-state reads in any View.
- Do NOT hand-edit scenes (§3). Heart-interaction hookup goes through script/bootstrap, not a scene hand-edit.

---

## 9. Reconciliation with WO-432
WO-432 delivered the entire perk/research backend + the in-panel perk rows + the `VillageTierService`, but
**never wired a way to raise the Village Tier** — so the gate it built can never open and the perks read as
"missing." WO-460 is the completion of WO-432: it adds the Village-Tier raise control (the WC3 Town-Hall→
Keep→Castle upgrade) and the lock-reason UX so the existing perk track becomes live and self-explanatory.
Mark WO-432 as superseded-by-460 for the surfacing portion; keep its data/service work as the foundation.
