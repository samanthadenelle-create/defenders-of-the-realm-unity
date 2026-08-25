# WORK ORDER 835 — HUD action bar: show only APPLICABLE buttons, re-packed

**Status:** FIXED 2026-08-02 (`6f22a5fe4`) — awaiting owner felt-verify. *(Status audit 2026-08-24: BUCKET CORRECTION — the prior line predated the commit and still advertised gates/commit as owed; verified at source in `git log`, `6f22a5fe4` (2026-08-02) landed this work. Body unchanged. Prior line: IMPLEMENTED (pending gates) — 2026-08-02. Core `HudActionBarModel` + `PostureSignals.RaidCapable`)*
+ Village `RaidCapabilityHudBridge` + HudKit render-from-array repack + `upgradeButton` face/rows +
`HudActionBarModelTests` + `HudActionBarRegression` (orchestrator registers). Both §7 defaults applied
(Raids fully hidden when uncapable; constant width + centered group). ActionBar zone widened
0.270-0.730 (`HudAreasHost.cs`) so the 7-face max keeps near-today button size.
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** HUD/UI — primarily `HudKitController.cs` (View). No scene edits.
**Origin:** owner felt-test 2026-08-02, town HUD — *"HUD buttons should not show if not applicable … the collection
should hide that button so it doesn't show … as applicable it should show … the visible array should only be the
ones active."* Plus: *"if they cannot do raids — no troops or the building — no reason to confuse them with the
button"* and *"also allows quests to be active more often."*

---

## 1. Owner intent
The bottom action bar (Build · Talk · Bag · Raids · Map · Quests) should render **only the buttons that are
currently usable**, packed together with **no gaps**, re-evaluated live as context changes. A button that leads to
something the player can't do yet is confusing — hide it; show it the moment it becomes applicable.

## 2. RCA — current state (sourced from live code 2026-08-02)
**Two gating layers already exist; neither does per-button hide-and-repack inside town.**
1. **Posture occupancy** (`hud-areas.json` via `PostureEvaluator`/`HudAreasHost`) — the master gate: `calm(town)`
   shows all 6; `calm(explore)` shows only Talk+Bag; `build`/`hostile` drop the bar. This is CORRECT and stays.
2. **Per-button polls** in `HudKitController.Update()` (~L1631–1742) — but they only DIM or hole-hide:
   - **Bar is a FIXED ROW of absolutely-positioned buttons** — `HudKitController.BuildWidgets()` (~L422–517),
     `btnW = (1 − gap·5)/6`, width hard-wired to 6, **no `HorizontalLayoutGroup`**. Hiding a button leaves a HOLE.
   - **Talk** dims (interactable=false, alpha 0.45) via `OnTalkChanged` (~L1434/1437) — never hides.
   - **Raids** dims toward `Disabled` (~L1707–1725) — never hides.
   - **Map** hides its inner button on `!Onboarded` (~L1731–1741) — but leaves a HOLE (no repack).
   - **Quests** relabels **Quests↔Upgrade** on a focused building (~L1642–1656) — so Quests DISAPPEARS whenever a
     building is focused (this is the "quests not active often enough" the owner felt).

## 3. The design to build

### ★ ARCHITECTURE LAW (owner directive 2026-08-02 — BINDING, HP B2B, CLAUDE.md §"Architecture law")
**The applicability logic is managed in COMMON (a Core model/VM), NOT in the presentation class.** The View
"just does what it's passed" — it renders and sizes the array it receives and holds **zero** predicate/gate logic.
The active-button array is computed BEFORE the View, upstream; on a context change (e.g. hero walks near an NPC) the
model recomputes a **new array** and pushes it to the View, which re-renders + re-sizes to exactly that array.
This mirrors the existing `PostureSignals` (Core.HudModel) + MVVM pattern already in the codebase.

### 3a. Common model owns the array; the View is a dumb renderer
- **NEW `HudActionBarModel` (or `HudActionBarVM`) in `DeNelle.Core.HudModel`** (next to `PostureSignals`) — PURE,
  testable, no UnityEngine UI. It:
  - subscribes to the context signals (§3b) — all already Core-visible or mirrored into Core (below),
  - recomputes an **ordered `IReadOnlyList<ActionBarButtonId>` of ACTIVE buttons** whenever any input changes,
  - raises a single `ActiveButtonsChanged` event carrying the new array (edge-triggered — only on an actual set change).
- **Village-side inputs get MIRRORED into Core** exactly like `PostureSignals.SetTalkAvailable` already is (Village
  writes, Core holds, HUD reads — the project's standard cross-assembly seam; Core cannot reference Village):
  - `TalkAvailable` — already in `PostureSignals` ✓
  - `RaidCapable` — NEW Core signal; the Village side (BarracksService/RaidEntryGate) publishes "has building AND ≥1
    troop AND FeatureFlags.Raid" into it (mirror pattern).
  - `MapAvailable` (Onboarded), `BuildingFocused` (`HudBuildingFocus.CurrentBuildingId`), current posture — surfaced
    to the model (Onboarded + focus are already Core-reachable; posture via the evaluator).
- **The View (`HudKitController` bar section) becomes pure presentation:** it subscribes to `ActiveButtonsChanged`,
  and on each new array it (a) `SetActive` exactly the buttons in the array, hides the rest, and (b) sizes/centers
  them. It reads NO gate, NO predicate, NO `RaidEntryGate`/`Onboarded`/`TalkAvailable` directly anymore — those reads
  move OUT of the View into the model. The View's only inputs are the array + its button GameObjects.
- **Layout (still the View's job, but purely mechanical):** given N active buttons, lay them left-to-right and
  **CENTER the group** within the `ActionBar` zone (`HudAreasHost.cs` ~L100, x 0.280–0.720) at a **CONSTANT per-button
  width** so a button never resizes as context changes (the group just grows/shrinks, centered). Replaces the
  fixed 6-slot `bx`/`btnW` math (~L422–426) with a render-from-array pass. `SetActive(false)` the inactive ones.
- **No per-frame relayout:** the model is event-driven (recompute on input change), so the View re-lays out ONLY when
  it receives a new array — not every `Update()`.
- **Zone capacity:** the active set can reach 7 (Upgrade split-out, §3c). Confirm the `ActionBar` zone fits the max
  at the chosen button width; widen the zone in `hud-areas.json` if 7 is tight.
- **Testability (the payoff of moving logic to Core):** a pure EditMode regression asserts the model's output array
  for representative signal combinations (no NPC → no Talk; near NPC → Talk present & ordered; no troops → no Raids;
  building focused → Upgrade present, Quests still present; etc.) — impossible when the logic lived in the View.

### 3b. Per-button applicability predicates (all signals verified in code)
| Button | Applicable when | Signal (file:line) | Change vs today |
|---|---|---|---|
| **Build** | always in town (posture already gates it; blocked only in enemy scene) | posture `calm(town)`; `!SceneOwnership.IsEnemyOwned` (`SceneOwnership.cs:39`) | keep always-on within town |
| **Bag** | always | none (always applicable) | keep always-on |
| **Map** | player onboarded | `GameStateService.State.Onboarded` (`HudKitController.cs:1731`) | hide → now also **repack** (kill the hole) |
| **Talk** | a talkable NPC is in range | `PostureSignals.TalkAvailable` + event `TalkChanged` (`PostureSignals.cs:133/136`) | **hide+repack** instead of dim |
| **Raids** | player CAN raid: has the raid building AND ≥1 troop (and `FeatureFlags.Raid`) | `RaidEntryGate.ArmyStatus` deployable count (`RaidEntryGate.cs:39–53`) + Barracks/raid-building exists (`BarracksService`); flag `FeatureFlags.Raid` | **hide+repack** instead of dim — see §3d |
| **Quests** | always in town | keep shown (owner: "active more often") | **freed from the Upgrade hijack** (§3c) |
| **Upgrade** *(new, split-out)* | a building is focused | `HudBuildingFocus.CurrentBuildingId` non-empty (`HudKitController.cs:1642`) | becomes its OWN context button that repacks IN when focused |

These predicates live in the **model** (§3a), NOT the View. The model subscribes to / is fed by: `TalkChanged`,
`RaidEntryGate.ArmyStatus.Version` (mirrored into the Core `RaidCapable` signal), `State.Onboarded`,
`HudBuildingFocus.CurrentBuildingId`, and posture changes. On any change the model recomputes the ordered active
array and raises `ActiveButtonsChanged`; the View just re-renders. (The Village publisher for `RaidCapable` may poll
BarracksService/ArmyStatus on the existing cadence, but that read lives Village-side, never in the View.)

### 3c. Split Quests and Upgrade (owner: "allows quests to be active more often")
Today ONE button relabels Quests↔Upgrade, so Quests vanishes when a building is focused. Split them:
- **Quests** = its own always-in-town button (opens RumorBoard/quests). No longer overwritten.
- **Upgrade** = a NEW context button, applicable only when `HudBuildingFocus.CurrentBuildingId` is set; repacks in
  when the player focuses a building, out when they don't. Route it to `PanelRouter.Open(PanelId.BuildingUpgrade, id)`
  (the same route the relabel used, `OpenQuestOrUpgrade` ~L1468–1476 — split into two handlers).

### 3d. Raids gate — hide, and keep it discoverable (OWNER CONFIRM / flag)
The owner wants Raids HIDDEN when the player can't raid (no troops or no building) — this **supersedes** the prior
"dim-not-disable so a dimmed tap redirects to the drillmaster" ruling (agent-noted, `RaidEntryGate`/`RaidSelectionScreen`).
Predicate: `FeatureFlags.Raid` AND raid-building exists AND deployable troop count ≥ 1.
**Discoverability flag:** hiding Raids until you have troops+building means a brand-new player never sees raids exist.
Recommend the raid feature be INTRODUCED through the build/troop path instead (a quest or the drillmaster NPC that
points at "build a Barracks → train troops → raid"), so hiding the button isn't the same as hiding the feature.
`OWNER CONFIRM`: OK to fully hide Raids (default), with discovery handled by quest/NPC — vs. keep the dim-redirect.

## 4. Files to edit
- **NEW `Assets/_Modules/Core/HudModel/HudActionBarModel.cs`** (`DeNelle.Core.HudModel`) — the COMMON logic: composes
  the context signals into the ordered active-button array + `ActiveButtonsChanged` event. Pure, no UnityEngine.UI.
- **NEW Core signal** for `RaidCapable` (add to `PostureSignals` or a sibling holder in `Core.HudModel`) — the mirror
  target the Village side writes.
- `Assets/_Modules/HUD/Kit/HudKitController.cs` — REMOVE the per-button gate reads from the View (`OnTalkChanged`
  dim, Raids dim poll, Map hide poll, Quests relabel); subscribe to `ActiveButtonsChanged`; replace the fixed 6-slot
  `BuildWidgets` geometry with a **render-from-array** pass (SetActive + center at constant width). Add the Upgrade
  button GameObject + split the Quests/Upgrade click handlers. The View holds NO predicates after this.
- Village publisher (e.g. a small `RaidCapabilityPublisher` or fold into `RaidEntryBridge`/`BarracksService`) —
  writes `RaidCapable` into the Core signal from "barracks exists AND ≥1 troop AND `FeatureFlags.Raid`"
  (`RaidEntryGate.ArmyStatus` deployable count + BarracksService — read for the exact fields).
- **NEW `Assets/Tests/EditMode/HudActionBarModelTests.cs`** — pure test of the model's array output per signal combo.
- `Assets/.../hud-areas.json` (+ Resources mirror) — only if the `ActionBar` zone must widen for the 7-button max
  (byte-identical copies).

## 5. Acceptance criteria (headless + felt)
- [ ] In town with no NPC near and no troops: the bar shows **Build · Bag · Map · Quests** only — packed, centered,
      NO holes. (Talk, Raids, Upgrade absent.)
- [ ] Walk next to a talkable NPC → **Talk** appears (repacks in) within ~0.25s; walk away → it leaves, bar re-packs.
- [ ] Have a Barracks + ≥1 troop → **Raids** appears; with neither, it is absent (not just dimmed).
- [ ] Focus a building → **Upgrade** appears as its own button; **Quests** stays present throughout (never replaced).
- [ ] Posture layer intact: explore still shows only its occupancy set; build/combat still drop the bar.
- [ ] No horizontal gap in any applicable-set combination (repack verified via `RunCaptureHeadless` / felt).
- [ ] **Architecture:** the View (`HudKitController`) contains NO applicability/gate reads — all predicates live in
      `HudActionBarModel`; the View only consumes `ActiveButtonsChanged` + renders. `HudActionBarModelTests` green,
      asserting the array output per signal combo.
- [ ] `CompileGate` green; posture occupancy behavior unchanged except the widen (if any).

## 6. Do NOT
- **Do NOT put applicability/gate logic in the View** (`HudKitController` / any presentation class) — it lives in the
  Core `HudActionBarModel`; the View only renders the passed array (owner architecture law 2026-08-02, HP B2B).
- Do NOT remove or bypass the posture-occupancy layer (`hud-areas.json`) — the repack layers ON it, within a posture.
- Do NOT relayout every frame — dirty-check the applicable-set signature; repack only on change.
- Do NOT resize individual buttons as context changes (keep constant width, center the group) — avoids jarring reflow.
- Do NOT hand-edit scenes. Keep `hud-areas.json` Resources/StreamingAssets copies byte-identical.

## 7. OWNER CONFIRM (defaults chosen; veto any — non-blocking)
1. Raids fully hidden when uncapable (default) vs. keep the dim-redirect — with raid discovery moved to a quest/NPC (§3d).
2. Constant button width + centered group (default) vs. stretch buttons to fill the zone as the count changes.
