# WORK ORDER 674 — Player Wall System (design-your-own walls + entrances)

**Status: READY TO IMPLEMENT** (owner directive 2026-07-11: "let players create their own walls —
resource-cost validated, place their entrances min 2 / max 4, wall placement block-by-block OR full
wall runs at once, with rotation").
**Lane:** Build Mode / Economy. **Flag:** `ff.playerwalls` (default OFF; flag-off = today byte-identical).
**Folds in + supersedes:** WO-442 (wall-drag pay validation — its premise assumed a drag path that
never existed; this WO builds the run mode AND its economy hardening together). Completes the
player half of `docs/PLAN_grid_coc_base_walls.md` Phase 2 (deferred 2026-06-13, never WO'd).
**Companion to:** WO-673 (strategic building placement — Town verb). This is the **Walls verb**.
`BuildType.Walls` already exists (`CatalogType.cs:24`). WO-673B (Pillager) unaffected.
**Numbering:** 674 assumed from filesystem max 673 — confirm/mint in `CLI_LANES_WO_NUMBERS.md` +
Notion on claim.

---

## Why (owner thesis)

WO-672 made structures damageable/repairable and WO-673 made the town player-designed. Walls are
the last authored-only layer: the catalog rows, `WallSegment`, tiers, gate behavior, and grid
placement ALL exist — walls are simply **locked out of the palette** (`build-categories.json`
Defense `lockedIds`: `wall_wood`, `wall_stone`, `gate_stone`). The player should shape their own
perimeter and choose where the enemy gets in. Entrances (min 2 / max 4) keep the design honest:
you can never seal the map, and every layout states its own risk.

## Verified reuse inventory (all cites read from code 2026-07-11 — reconcile, never greenfield)

| Seam | Where | State |
|---|---|---|
| Walls build verb | `CatalogType.cs:24` — `BuildType.Walls` | EXISTS (WO-673) |
| Catalog rows | `structures-catalog.json`: `wall_wood` (behaviorId WallSegment, cost wood 20, maxLevel 3 + 2-step upgradeCost ladder, footprint 3.0, navSurface Blocker), `wall_stone` (wood 30 / iron 60), `gate_stone` (behaviorId Gate, wood 60 / iron 50, footprint 4.0, `minDistanceFromGate: 8.0`) | EXIST, authored, currently locked |
| Grid | `PlacementGrid.cs:37` cellSize = 3 m; `FootprintCells` :212 → wall = 1 cell, gate ~2 cells | EXISTS |
| Single placement + atomic charge | `BuildModeController.Place()` :1010 — re-checks `CanAfford` :1016 + `ChargeLedger` at commit (WO-131); multi-resource `CostFor` w/ crystals fallback; shortfall toast :641 | EXISTS — block-by-block placement works today once unlocked |
| Stay-armed repeat (CoC) | BuildModeController :626-629 — `_armed` kept after commit, each copy re-validated + re-charged | EXISTS |
| Rotation | `_armedYawSteps & 3` (90° steps, key-cycle :582/:731), `TowerPlacementRotateMenu` :686, `OnRotateConfirmed` :695; persisted via `PlacedStructureData(yawSteps, yawOffset, worldY, wallMounted)` :1034 | EXISTS |
| Validity | `IsValidPlacement` :764-779: grid occupancy + world-collider AABB :843 + **gate-lane clearance** :858-862 / `IsTooCloseToGate` :940 (guards each gate's spawn→Heart doorway, `_gateClearance` 3 m :92) + `mustSitOn` surface roles | EXISTS |
| Behavior attach | `StructureFactory.AttachBehaviorImpl` — `case "WallSegment"` :580, `case "Gate"` :589 | EXISTS — placed gates auto-join the `IsTooCloseToGate` scan (it finds `Gate` components) |
| Wall gameplay | `WallSegment.cs` — `IDamageableStructure`, tier 1-3, toughness {1, 1.6, 2.56}, `SetTier`; `WallTierData.cs` Wood/Iron/Steel ladder (tier mesh slots await owner art import); upgrade verb at BuildModeController :1252 | EXISTS |
| Persistence/replay | `BaseLayout` → `BaseLayoutLoader` → `StructureFactory.Create`; NavMeshObstacle carve local to yawed root; save **v29 — NO schema change needed** (a run = N ordinary records) | EXISTS |
| Damage/repair | WO-672 lifecycle (hp==0 broken shell, damage tells, WaveDamageReport w/ repair costs, Repair All) reads `IDamageableStructure` — walls/gates join automatically | EXISTS |
| Edit verbs | `BuildSelectionUI` Move / Upgrade / Sell (50%) / Cancel | EXISTS |
| Timers | `ff.buildtimers` ON (`FeatureFlags.cs:75`) — Place hooks BuildTimerService | EXISTS |

**The genuinely NEW work: (1) the Walls palette row + unlock, (2) the run-drag placement mode with
atomic whole-run charging, (3) the entrance min-2/max-4 rule.** Everything else is wiring.

---

## Spec

### A. Walls verb + palette (data + menu)

1. `build-categories.json` (BOTH copies, byte-equal — CanonicalJson dual-copy rule): add row
   `{ "buildType": "Walls", "label": "Build Walls", "catalogTypes": ["Wall", "Gate"], "lockedIds": [] }`.
   **Gate moves INTO the Walls verb** (amends the WO-673 comment's "Defenses → Tower/Gate"):
   entrances are part of wall design — one tab, one activity. Defense row becomes
   `catalogTypes: ["Tower"]`; prune `wall_wood`/`wall_stone`/`gate_stone` from its `lockedIds`.
   Record the amendment in the `CatalogType.cs` BuildType doc-comment + `BuildPaletteUI._types` comment.
2. HUD Build menu gains the **Walls** entry → `EnterBuildMode(BuildType.Walls)` (generic entry
   :198 — menu wiring only, same shape as WO-673 L2's Town entry).
3. All of §A is served only when `ff.playerwalls` is ON; flag OFF ⇒ the Walls row is not offered
   and the Defense `lockedIds` behave exactly as today (byte-identical path).

### B. Placement modes — block-by-block AND full runs

**B1. Block-by-block — already works; verify, don't rebuild.** Arm `wall_wood` → ghost → tap →
`Place()` validates + charges per segment → stay-armed repeats. Rotation via the existing 90°
yaw-step cycle + rotate menu. Acceptance covers it; no new code expected beyond the unlock.

**B2. Run mode (NEW — the WO-442 gesture, built properly).**
- Drag (desktop: hold-drag; mobile: `IBuildInput`/LeanTouchBuildDriver seam) from an anchor cell:
  preview a **straight, axis-aligned run** of N ghost segments along the drag's dominant axis
  (CoC grammar; 3 m cells tile seam-to-seam). Segments auto-orient along the run axis
  (yawSteps derived from axis — no manual rotation needed mid-run).
- **Running total** = `CostFor(entry) × N`, shown on the ghost/HUD.
- **Whole-run RED** (commit blocked) when `!CanAfford(total)` **or any cell in the run is invalid**
  (occupied / world-collider hit / gate-lane intrusion — reuse `IsValidPlacement` per cell). Money-red
  vs invalid-red must be distinguishable (shortfall toast vs reject reason — reuse both existing reads;
  never color-only, owner is red/green colorblind: pair the tint with the reason text).
- On a valid release: **ONE atomic `TrySpend(total)`**, then place all N segments through the
  existing Place internals with the charge already taken (refactor `Place()` minimally into
  charge + commit halves, or add `PlaceRun(cells)` that calls the commit half N times — do NOT
  duplicate Place's body). No partial runs, no free segments, no double-charge. Stay-armed after.
- Drag-back shortens the run live (the WO-442 UX); Cancel intent aborts cleanly.
- Every blocked gate **names itself** (`[Flow:Build] RunCommit BLOCKED at <gate>: <state>` —
  the step-in/step-out standard, §12 / INSTRUMENTATION_STANDARD §2).

**B3. Rotation grain — walls/gates are 90° only.** Named divergence from WO-673's 45° building
ruling, with the reason on record: wall segments are grid CELLS that must tile seam-to-seam; a
45° segment gaps the ring and lies about its claimed cell. Buildings keep their 45°; walls snap
90° (owner may overrule — see Pins).

### C. Entrances — min 2, max 4 (player-placed gates)

- **Count = player-placed `gate_*` records in `BaseLayout`** (never `FindObjectsByType` — the
  authored castle-shell gates/drawbridges do NOT count; this rule governs the player's own wall
  design only).
- **Max 4 — enforced at placement:** with 4 placed, gate entries gray out in the palette (with
  reason text) and a 5th gate ghost is invalid (`BuildRejectReason` + toast "Maximum of 4
  entrances."). Reuse the `lockedIds`-style filter + the existing reject-reason plumbing.
- **Min 2 — enforced at the DEFEND trigger, not at placement** (you can't demand structures exist
  mid-edit; waves are player-triggered, so the wave gate is the natural seam): if player wall
  segments exist (`BaseLayout` wall count > 0) and player gate count < 2, the DEFEND start is
  refused with a toast ("Your walls need at least 2 entrances before the horde comes.").
  WaveManager DEFEND entry + StartWaveHudBridge read — one check, one owner.
- **Sell/Move stay free** (CoC editing freedom); dropping below 2 gates only re-blocks DEFEND.
- Each placed gate's doorway is automatically protected from being walled over by the existing
  gate-lane clearance (`IsTooCloseToGate` scans all `Gate` components — placed gates included by
  construction, :589).
- V1 rule is a **global count** — legible, one sentence. Multi-ring counting-per-connected-network
  is a named V2 refinement (see Pins), not silently attempted now.

### D. Economy

- Every piece charges its authored multi-resource `repo.cost` (wood/iron rows already authored) —
  single placements via the existing WO-131 validate+charge; runs via the §B2 atomic total.
- Ledger integrity acceptance (from WO-442, verbatim intent): **no way to obtain a wall segment
  without a corresponding ledger charge.**
- Build timers apply per the existing Place seam (`ff.buildtimers`); a run enqueues N timer jobs
  exactly as N taps would (do not invent run-batch timer semantics in V1).

### E. Tiers + upgrades (wire, don't build)

- Placed walls start tier 1 (Wood). The existing upgrade verb (:1252) + catalog `upgradeCost`
  rows + `WallSegment.SetTier` already carry single-segment upgrades — verify they work on
  player-placed segments and that tier persists via the existing `level` field on
  `PlacedStructureData` (it's written by Place; confirm the loader re-applies `SetTier(level)` on
  replay; fix if it doesn't — that's the one likely wiring gap).
- "Upgrade the ENTIRE wall at once" (owner-decided 2026-06-13, PLAN §3) = **fast-follow WO-674B**,
  not in this slice.
- Tier meshes: `WallTierData` prefab slots await the owner's Wood/Iron/Steel FBX import. V1 ships
  on `visualPrefabPath` + `StructureTierVisual` tint; wire tier meshes when the art lands (674B).
  CLI must VERIFY `Resources/Structures/Wall_Medieval_Wood` / `Wall_Medieval_Stone` /
  `Gate_Medieval_Medium` actually resolve (the orientation baker noted missing polyperfect
  sources) — a missing visual falls back per factory rules; name it in the RESULT if so.

### F. Instrumentation + gates (§12 / §2c — the permission gate)

- `[Flow:Build]` step-in/out on the full run lifecycle: arm → drag-grow (throttled) → validate →
  charge → commit-N → stay-armed. Every rejection names its gate and reason.
- **EditMode tests:** run-total math (N × cost); atomic charge (a run that fails mid-commit rolls
  back / never half-charges); max-4 gate reject; min-2 DEFEND block + unblock at 2; gate-count
  reads BaseLayout only (scene gates excluded).
- **DataRegression:** Walls category row parses + both canonical copies byte-equal; wall/gate ids
  resolve via CatalogRegistry.
- **Fleet probe (new):** headless — arm wall, place a 5-segment run, assert 5 BaseLayout records +
  exactly one ledger delta of 5×cost + 5 live WallSegments; place 2 gates; assert DEFEND arms;
  assert 5th gate rejected. Save round-trip: reload rebuilds the run at the same cells/yaw.
- `COMPILE_GATE_OK` + `REGRESSION_OK` + fleet green + **owner felt-pass** (walls are a felt
  feature — the drag must feel CoC-good) before the flag defaults ON.

## What NOT to touch

- `Place()`'s single-placement WO-131 semantics for non-wall structures — unchanged.
- MainCastle_Hall / the merged shipped scene, the authored castle shell walls + drawbridges —
  NO scene edits, NO rebake for this WO (navmesh impact is carve-only via NavMeshObstacle).
- `Village.unity` (abandoned). WallWalk tower seating (:794-811) — towers-on-walls keeps working;
  the fleet probe should confirm a tower still seats on a PLAYER-placed wall (nice free win —
  assert it, don't build for it).
- WO-673's Town lanes — file-disjoint where possible; coordinate on `build-categories.json` +
  `CatalogType.cs` comment (same-file = one agent, §9).
- No `git add -A`; commit by explicit path; push held for owner word.

## Acceptance (owner felt-pass list)

- [ ] Flag ON: Build menu shows **Walls**; palette offers Wooden Palisade / Stone Wall / Stone Gate.
- [ ] Tap-mode: place single segments, rotate 90°, each charges wood/iron correctly; shortfall blocks with toast.
- [ ] Drag-mode: pull a wall run, watch the running total, run turns red past your funds OR over an
      invalid cell, drag back to green, release → whole run built, charged once, atomically.
- [ ] Place gates: 2 required before DEFEND will start (clear toast when blocked); 5th gate refused.
- [ ] Enemies path to the gates, attack walls/gates (they take damage, show WO-672 damage states,
      appear in the wave damage report with repair costs).
- [ ] Sell refunds 50%; Move works; reload rebuilds the exact layout (cells, yaw, tiers).
- [ ] Flag OFF: game is byte-identical to today (fleet re-run proves no new tickets).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + new EditMode tests green + fleet probe green.

## Open pins (owner)

1. **45° walls?** Spec says 90°-only for walls/gates (seam-tiling honesty) while buildings get 45°
   (WO-673). Overrule if the expressive read matters more than the seam.
2. **Multi-ring entrance counting** (per-connected-network min-2) — V2 refinement or never?
3. **Do claimed outposts get the same 2-4 entrance rule** when Walls extends there (WO-673
   flip-a-base V2)? Default assumption: yes, same rule, per site.
4. **WO-674B scope nod:** entire-wall-upgrade-at-once + tier meshes when the Wood/Iron/Steel art
   lands — mint on claim or fold into a polish wave?

*Cross-refs:* WO-442 (superseded by §B2/§D) · WO-673 + `docs/WO673_ARCHITECTURE_REVIEW.md` ·
`docs/PLAN_grid_coc_base_walls.md` (Phase 2 origin, tier ladder + owner art) · WO-672 (damage
lifecycle) · `docs/BUILD_MODE_ARCHITECTURE.md` · CLAUDE.md §12/§13 · `docs/INSTRUMENTATION_STANDARD.md`.
*Note for CLI:* the Linux-mount view of `CatalogType.cs` read truncated mid-enum during this WO's
vetting while the Windows file was healthy — §0 read-side desync is live; trust Windows-path reads.
