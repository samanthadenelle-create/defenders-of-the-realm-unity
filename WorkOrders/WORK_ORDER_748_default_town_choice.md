# WORK ORDER 748 — Founding choice: "Default Town" vs "Build Your Own"

**Status:** SPEC — READY TO IMPLEMENT (owner-requested 2026-07-18; scoped read-only). Awaiting owner go.
**Classification:** NEW FEATURE (choice UI + apply path don't exist). No save-schema change.
**Owner (PO):** Sam — resurrect the old prebuilt-city option as a "Default Town" choice at onboarding.

---

## The idea
At founding, let the player choose: **"Default Town"** (drop in the prebuilt city we already had) or
**"Build Your Own"** (today's blank template + FTUE). Default-town buildings must stay **individually
movable** in build mode (owner's explicit requirement) — confirmed feasible below.

## The old switch + prebuilt-town source (found)
- The old toggle was the feature flag **`ff.strategicplacement`** (REMOVED by WO-695/682, tombstone
  `FeatureFlags.cs:599-601`). Flag **OFF** = the auto-placed prebuilt town ("build a new city");
  **ON** = place-yourself/blank (now the unconditional path).
- Prebuilt layout = **`StrategicPlacementMigration.BakedRows`** (`StrategicPlacementMigration.cs:85-114`)
  at the ring positions in **`CastleHubBuilder.cs:288-301`**. The 8 catalogued buildings + local ring pos:
  `workshop`(-22,0,-22) · `lumbermill`(22,0,-22) · `mill`(-22,0,22) · `pet-house`(22,0,22) ·
  `forge`(-32,0,0) · `arcane-tower`(32,0,0) · `market`(0,0,32) · `jeweler`(player-placed).
  (Stations `apothecary`/`jewelers-bench` at (±11,0,2) have **no catalog row** — omit or add rows.)
- **Apply mechanism already exists:** `StrategicPlacementMigration.RunIfNeeded()` (`:229-294`) writes the
  set into `GameState.BaseLayout` as `PlacedStructureData` records — reuse that.

## Movability — CONFIRMED YES (for the 8 catalogued)
Applied as BaseLayout records, each spawns as a selectable `PlacedStructure` (`BaseLayoutLoader.cs:317/360`),
tap-selects (`BuildModeController.cs:916/948`), and moves via `BeginMoveSelected`/`CommitMove`
(`:2293-2381`, rewrites the persisted record) — **identical to self-placed** (move is free, wallet
untouched). Setting `StrategicPlacementMigrated=true` stands down the old bakes/injectors
(`HubStructureVisualInjector.cs:258`) so there's no locked twin. **Do NOT** apply via re-enabling the old
bakes — those carry no `PlacedStructure` marker and are the immovable structures to avoid.

## Implementation plan
- **Choice UI:** new `Assets/_Modules/Onboarding/FoundingChoiceController.cs` (DeNelle.Onboarding -> Core
  only), a code-built uGUI Obsidian screen (no UXML, §8) inserted **after PetSelect, before first hub
  entry**. Two buttons: Default Town / Build Your Own.
- **Default Town** -> `DefaultTownLayout.Apply(GameState)` (new static in DeNelle.Village, or fold into
  StrategicPlacementMigration) writes the 8-record set into `BaseLayout` via `PlacementGrid.WorldToCell`
  (same as `TryWriteRecord`), keep `StrategicPlacementMigrated=true`, `Save()`. GRANTED (no `Place()`, no
  cost, does NOT touch `FreeBuildsUsed` — preserves the one-free-total first placement).
- **Build Your Own** -> no-op (current blank + FTUE).
- **FTUE:** Default auto-satisfies the founding build steps via the new
  `TutorialFlow.TryAutoCompleteAlreadyBuilt` (`:420-459`, checks BaseLayout). Echo/claim teaching still
  fires (not build-gated). Tower step still asks for a defense (no tower in the Default set) — fine.
- **Persistence:** none — `BaseLayout` is v14+ persisted; the v30 marker is already set on New Game.

## RISKS to resolve at implementation
1. **Coordinate/merged-world mismatch (MEDIUM):** the `CastleHubBuilder` ring positions are
   `CastleHubRoot`-local (liftY=3); the live `Main_Castle_Overworld` flattens the castle to y=0
   (`WorldMergeBuilder.LowerCastleToGround`). Author the Default table in **grid cells validated against
   the current hub** (or capture-once from a live scene) — do NOT trust the 2026-06 authored locals
   verbatim, or the town could collide with walls/gates/the Tree or land off-plaza.
2. **`lumbermill` vs `lumberyard` id drift:** the founding_stores step keys on `lumberyard`; the ring uses
   `lumbermill`. Add `lumberyard` to the Default set (or reconcile) so the FTUE step auto-satisfies.
3. **Uncatalogued stations** (`apothecary`, `jewelers-bench`): can't be movable records — omit, or add
   catalog rows first.

## Do NOT
- No UXML; ASCII-only; no `.unity` hand-edits. Do not apply Default Town by re-enabling the old
  injector/bake path (yields locked buildings). Onboarding -> Core only (never touch Village directly).
