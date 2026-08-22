# Agent Openers — Current Architecture (2026-06-27)

> **Supersedes the pre-pivot version (2026-05-30).** That version had wrong agent lanes
> (DTT/PatriciaLight, party-of-4 ATB, Village.unity, old wallet merge). This is the
> current-state brief. Always paste the relevant block when spawning an agent.
>
> **Live anchor:** `CANON_GROUND_TRUTH_2026-06-26.md`
> **Branch:** `wip/village2-and-f8-tickets` · HEAD `8aa24c32` (nothing pushed)

---

## ★ MANDATORY PREAMBLE — paste into EVERY agent brief

```
PROJECT CANON (read before any code or analysis):
- Branch: wip/village2-and-f8-tickets · HEAD 8aa24c32 (nothing pushed yet)
- Game: Echoes of Elarion / Defenders of the Realm (Unity 6 / URP, WebGL on itch)
- V1 = ONE controllable hero (Knight "Grom", Tripo self-rigged model, static armor, NO mesh-swap)
  in MainCastle_Hall hub + OuterWorld (additive). Animated real-time combat = OVERWORLD BattleArena
  (lock-on, 9-zone HUD). ATB is separate/flat. Base-defense = V2-gated (ff.basebuilding).
- REMOVED (2026-06-09): Defend-the-Tower / PatriciaLight — module + scene gone. Ignore any WO
  referencing DTT, PatriciaLight, or PatriciaLightMode.
- JUNKED (2026-06-22): Blink full-body hero rig — any "Blink armor" or "HeroBodySwapper" is inert.
- Village.unity = ABANDONED (corruption-cursed, never touch). Home = MainCastle_Hall; raid = Village2.
- Sole committer = CLI. You (agent) write + signal ready. You do NOT gate, commit, or push.
- INSTRUMENT-FIRST (CLAUDE.md §12 HARD GATE): NO code edit on a non-trivial bug until
  CAPTURED DATA proves the cause. FlowTrace step-in/step-out → run headless → read the data →
  fix THAT. Never guess / inference-fix. This is the OPENING move, unprompted.
- Never hand-edit .unity scenes. Never use bash redirects for .cs files (mount-sync corrupts).
- Brace-balance every .cs you touch before signalling done.
- Read docs/INSTRUMENTATION_STANDARD.md before writing any new method.
```

---

## Current Parallel Lanes (file-disjoint; run simultaneously)

### Lane 1 — BattleArena / Combat (owns: BattleArena, LockOn, WO-512 family)
Files: `Assets/_Modules/Village/Arena/`, `BattleController.cs`, `ATBCombatManager.cs`

Brief:
```
TASK: [your specific task here]
YOUR LANE: BattleArena / Combat (Lane 1)
Files: Assets/_Modules/Village/Arena/ · BattleController.cs · ATBCombatManager.cs
Canon: BattleArena = overworld isolated arena (lock-on, 9-zone HUD, WO-512). ATB is SEPARATE
(flat/static, single hero vs static enemies). Enemy roster V1 = Orcs only (Tripo family). Do NOT
touch Village/ systems or the seam/gate logic.
```

### Lane 2 — World / Seam / OuterWorld (owns: RuntimeRegionGate, WorldSceneLoader, OuterWorld)
Files: `RuntimeRegionGate.cs`, `WorldSceneLoader.cs`, `OuterWorldBuilder.cs`, `region-gates.json`

Brief:
```
TASK: [your specific task here]
YOUR LANE: World / Seam (Lane 2)
Files: RuntimeRegionGate.cs · WorldSceneLoader.cs · OuterWorldBuilder.cs · region-gates.json
Canon: Castle↔OuterWorld = four-side warp gates (RuntimeRegionGate, rotation-generalized).
Castle moat + 4 drawbridges (ff.castlemoat). The seam is a WARP by design — stacked navmeshes
don't auto-connect. Debug "can't cross" as a navmesh bake or trigger-radius issue (hero stops at
navmesh edge), never as a collider issue. Do NOT touch Arena/ or HUD/.
```

### Lane 3 — Echo Workforce / Economy (owns: EchoService, EconomyService, save schema)
Files: `EchoService.cs`, `EconomyService.cs`, `SaveSchema.cs`, `echo-workforce.json`

Brief:
```
TASK: [your specific task here]
YOUR LANE: Economy / Echo Workforce (Lane 3)
Files: EchoService.cs · EconomyService.cs · SaveSchema.cs (save v25) · echo-workforce.json
Canon: 1–4 echoes, silo+dump, wave-unlock, offline via real clock, save v25. Village-tier upgrade
wired → unlocks WO-432 building-upgrade tree. ONE wallet (GameState canonical). You OWN the
SaveSchema/SaveMigrator version bump — coordinate with any other lane touching save fields.
Do NOT touch Arena/, HUD/, or seam code.
```

### Lane 4 — Store / Inventory / Gear (owns: ShopPanel, VendorStockContract, gear catalogs)
Files: `ShopPanel.cs`, `VendorStockContract.cs`, `GearCraftingRecipeCatalog.cs`, `gear-balance.json`

Brief:
```
TASK: [your specific task here]
YOUR LANE: Store / Inventory / Gear (Lane 4)
Files: ShopPanel.cs · VendorStockContract.cs · GearCraftingRecipeCatalog.cs · gear-balance.json
Canon: Store redesign WO-501 (type filter, slim list, 3D preview, buy/sell+equip). 27 weapons +
12 armor graded (WO-500). VendorStockContract = single source of truth for what each store TYPE
sells. PackStore exists (~70%) — do NOT greenfield. No UXML (UXML does not render in WebGL builds;
code-built UI only). Do NOT touch Arena/, seam code, or save schema without Lane 3 coordination.
```

### Lane 5 — Building Upgrade / Tech Tree (owns: BuildingUpgradeVM, PanelMvvm)
Files: `BuildingUpgradeVM.cs`, `PanelMvvm.cs`, `BuildingUpgradeCatalog.cs`

Brief:
```
TASK: [your specific task here]
YOUR LANE: Building Upgrade / Tech Tree (Lane 5)
Files: BuildingUpgradeVM.cs · PanelMvvm.cs · BuildingUpgradeCatalog.cs
Canon: Warcraft 3-style tiered upgrade (WO-432), tier gate at the Heart of Elarion (the world
tree / stone reliquary at 0,0,0). Unlocked this arc by village-tier upgrade wiring. MVVM strict:
VM holds all logic/state; View is a dumb skin that reads state, never pushes game state.
No UXML. Do NOT touch Arena/, seam code, or VillageSceneBuilder.
```

### Lane 6 — Quest / Dialogue / Narrative (owns: QuestService, dialogue system)
Files: `QuestService.cs`, `RumorBoardPanel.cs`, `dialogues.json`, `quest-catalog.json`

Brief:
```
TASK: [your specific task here]
YOUR LANE: Quest / Dialogue / Narrative (Lane 6)
Files: QuestService.cs · RumorBoardPanel.cs · dialogues.json · quest-catalog.json
Canon: Yarn Spinner is being DROPPED (WO-455) — custom MVVM dialogue replaces it. Quest system
in Core/Quests/QuestService.cs. SaveSchema includes QuestProgress (v24+). Do NOT touch Arena/,
seam code, or VillageSceneBuilder.
```

### Lane 7 — HUD / UI / MVVM (owns: VillageHudController, PanelRouter, UI skin)
Files: `VillageHudController.cs`, `PanelRouter.cs`, `Core/UI/Mvvm/*`, `OnboardingPanelGuard.cs`

Brief:
```
TASK: [your specific task here]
YOUR LANE: HUD / UI (Lane 7)
Files: VillageHudController.cs · PanelRouter.cs · Core/UI/Mvvm/* · OnboardingPanelGuard.cs
Canon: MVVM strict — VM state, View reads. HUD→Core only (never HUD→Village direct). Blink =
UI re-skin kit only (BlinkChrome flag) — NOT the hero body. No UXML in builds. The 9-zone
battle HUD is the owner's exact-vision layout for BattleArena. OnboardingPanelGuard enforces
that the onboarding panel intercepts input ONLY in Title/HeroSelect/PetSelect.
```

### Lane 8 — VFX / Audio (owns: VFXManager, AudioService, SfxClipLibrary)
Files: `VFXManager.cs`, `AudioService.cs`, `SfxClipLibrary.cs`, `VfxPool.cs`

Brief:
```
TASK: [your specific task here]
YOUR LANE: VFX / Audio (Lane 8)
Files: VFXManager.cs · AudioService.cs · SfxClipLibrary.cs · VfxPool.cs
Canon: ONE pool per VFX concern, one owner (the VFX double-stack lesson). Pool by default;
never Instantiate() per use. AudioService is the single audio service (CoreServices.Audio).
DeNelle.Audio assembly only. Do NOT touch gameplay code.
```

### Lane 9 — Read-Only RCA / Diagnosis (no write access)
```
TASK: [specific diagnosis question]
YOU ARE QA / READ-ONLY: You read + diagnose only. You do NOT write code, do NOT gate,
do NOT commit. Your output = the proven root cause + bounded fix spec, handed to CLI.
Classify FIRST: is this a NEW FEATURE (never built) or an EXISTING (regression/bug)?
- NEW FEATURE → do not RCA; route back to PO for a spec/WO
- EXISTING → read FlowTrace data, F8 break-log, Editor.log → cite the LINE that proves the cause
INSTRUMENT-FIRST (§12): the data is the proof, never static reading.
Full spec: docs/TICKET_PIPELINE.md
```

---

## Collision rules (only ways lanes break each other)

1. **VillageSceneBuilder.cs** → only ONE agent at a time (serialization bottleneck). Coordinate via work orders.
2. **SaveSchema / SaveMigrator** → Lane 3 owns the version bump. Every other lane that touches save fields routes through it.
3. **GameState.cs field-adds** → additive only (never remove/rename existing fields). One lane at a time if adding.
4. **OuterWorldBuilder.cs** → Lane 2 owns; coordinate if Lane 3/4 need world references.
5. **CompileGate / git** → CLI only. Agents write + signal. Never `git add -A`; always explicit paths.

---

## Quick-reference: key files and what they own

| File / Path | What it owns |
|---|---|
| `CANON_GROUND_TRUTH_2026-06-26.md` | Single live anchor of current reality |
| `docs/HANDOVER.md` | Operator manual + newest session block |
| `docs/MASTER_CATALOG.md` | Verified-from-code SME catalog |
| `docs/INSTRUMENTATION_STANDARD.md` | How to write observable code (BINDING) |
| `docs/TICKET_PIPELINE.md` | QA→CLI→PO lifecycle (BINDING) |
| `WorkOrders/WORK_ORDER_NNN_*.md` | Unit-of-work specs |
| `CLI_LANES_WO_NUMBERS.md` | WO numbering authority - READ the next free off the banner, never restate it |
| `Assets/_Modules/Core/Diagnostics/` | FlowTrace · Guard · BreakCaptureHarness |
| `region-gates.json` | RuntimeRegionGate warp-gate config |
| `echo-workforce.json` | Echo offline workforce config |
| `dialogues.json` | Custom dialogue content |
| `break-log.jsonl` | F8 flight recorder output |

---

## What NOT to do (the recurring failures — memorize these)

- ❌ **Never guess at a bug** → instrument + capture + read the data, THEN fix
- ❌ **Never use `bash` redirects (`cat >`, `echo >>`) to write .cs files** → mount-sync corrupts
- ❌ **Never `git add -A`** → mass-converts LFS textures
- ❌ **Never touch `Village.unity`** → corruption-cursed and ABANDONED
- ❌ **Never use UXML in a shipped build** → does not render in WebGL
- ❌ **Never write code for DTT, PatriciaLight, or the Blink hero rig** → all removed/junked
- ❌ **Never claim "fixed" without captured data proving it** → headless verify or F8 confirm required
- ❌ **Never commit or push** → CLI is sole committer; agents write + signal
- ❌ **Never start work without reading the mandatory preamble above** → no shortcuts, no exceptions

---

*Maintained by UI. Update when architecture shifts or new lanes are confirmed.*
*Last updated: 2026-06-27 · Anchored to `CANON_GROUND_TRUTH_2026-06-26.md`*
