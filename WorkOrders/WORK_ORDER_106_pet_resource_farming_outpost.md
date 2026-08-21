**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> ⚠ **NUMBER COLLISION — this document does not own WO-106; `WORK_ORDER_106_xp_level_progress_hud.md` does.**
> Referred to hereafter as **WO-106-B (pet resource farming outpost)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-106 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK_ORDER_106 — Pet Resource Farming + Outpost System (Economy Integration)

**Status: READY TO IMPLEMENT**

**Owner:** (routed via this session)  
**Branch context:** feat/tower-core-loop (carry forward)  
**Priority:** Next after Village animations + build modal (user directive).

## Vision (pasted / reconstructed from prior + design docs)
Resource nodes (MineNode + variants for Wood/Food/Iron/AetherCrystal) as active faucets. Pets (deployed via PetDeployer, driven by PetHarvester + MineNodeBridge reflection) autonomously detect, path (via HomePost re-anchor), harvest on tick, bank via the node's existing TryAutoExtract (combat priority always wins; soft carry cap). 

Outposts: clear (ClaimableCamp + CampGuards + kill subscribe) → claim (prompt) → build (Watchtower/LumberOutpost/FarmOutpost types via CampBuildMenu) → Outpost auto-trickle harvest (BankTrickle, region danger scaling +25%/tier mirroring nodes) + IDamageableStructure for raze risk. Post-build counterattack (CampDefenseWave) must be defended to SECURE (persisted; outpost razed → rebuildable, camp stays claimed).

Scaling difficulty: more secured outposts / higher threat claimed territory raises stakes (tougher guards, richer yields, feeds into wave/enemy tuning via threat + future DifficultyTuning or WaveManager multiplier). Defensive troops: pets (Defend mode protects while harvesting idle), CampGuards, outpost as fortified point that can host assigned defenders later.

Economy class (EconomyService + GameState wallet) is the single faucet/sink. All pet/outpost/node yields route through it for consistency (in-session Wood/Iron + persisted Food/Crystals, OnChanged for HUD, CanAfford works). Offline accrual (WO-115), Food→Population growth, refine throttle per RESOURCE_ECONOMY_DESIGN.md. No new currencies. Reconcile (no parallel Economy/ old ResourceNode inventory for live path).

See: docs/RESOURCE_ECONOMY_DESIGN.md (faucets: active/pet harvest, passive/outpost trickle, offline cap; hybrid fast-early/slow-late curve; population from Food positive-only), docs/PLAYER_BASE_DESIGN_CATALOG_ROADMAP.md (P4 harvest economy: WO-110/111/115/117/119 nodes + auto-harvest pets/workers + offline + settlement claims), existing MineNode/PetHarvester/Outpost/ClaimableCamp/CampSystem scaffolding.

## Scope — What to implement / polish
- Route **all** harvest yields (MineNode.BankYield, Outpost.BankTrickle, pet-driven extracts) through `EconomyService.Grant(ResourceCost)` so Wood/Iron in-session mirrors + OnChanged listeners (HUD) stay in sync with actual economy. Remove direct state bypass where it would double-count (Grant handles the GameState side for Food/Crystals).
- Extend EconomyService with light "pet/outpost" integration: `SecuredOutpostCount`, `GetTerritoryMultiplier()`, `OnOutpostSecured()` hook (or event), optional `RegisterPassiveSource` stub for future rates. Add `GrantFromHarvest(...)` convenience if useful. Keep public API backward.
- Starter nodes for visible pet farming demo in Village (not just Village2): enhance PetHarvestBootstrap to target "Village" + "Village2", respect the placeholder flag but make nodes usable when enabled (or spawn minimal always in editor/dev for the loop test). Ensure PetDeployer can be wired so a pet with Harvester component actually runs the loop against nodes.
- Outpost / ClaimableCamp: call EconomyService for trickle; on secure/incr secured count in Economy; ensure defensive (existing CampDefenseWave + guards) + yield scaling remain. No behavior change for clear/claim/build.
- Scaling difficulty hook: secured outposts contribute to a simple multiplier or counter readable by future wave/difficulty systems (expose on EconomyService or via GameState extension). ThreatLevel already drives richer/tougher — amplify with count.
- Defensive troops: no new spawners; document + ensure Pet.Mode=Defend + camp guards act as the "assigned" protection for outposts/nodes. (Future: assign specific pets to outpost defense.)
- Cleanup: mark Economy/ module's duplicate ResourceNode/PetHarvester/ResourceInventory as superseded (point to live MineNode + Pets.PetHarvester + Village.EconomyService). Do not delete without owner; update their README.
- No .unity edits. No VillageSceneBuilder touch. Use existing runtime bootstrap pattern. Asmdef rules: Pets stays reflection-only via MineNodeBridge. Use `?.` on EconomyService.Instance and CoreServices. `using DeNelle.Core.Combat;` where IDamageableStructure touched (already in Outpost).
- Acceptance: In Village (with nodes present or bootstrap enabled + pet deployed in Defend), pet moves to node, harvests, resources visibly increase (EconomyService snapshot or GameState + HUD would reflect via Grant). Claim a camp in world (or simulated), build outpost, it trickles via Grant, secure it, Economy.SecuredOutpostCount increments, multiplier >1.0. Build costs using harvested Wood/Iron now see the income. No double-grant, no desync. Braces balanced on all touched .cs. Build succeeds.

## Files to edit / create (reconcile, minimal delta)
- `WORK_ORDER_106_pet_resource_farming_outpost.md` (this) + later `.RESULT.md`
- `Assets/_Modules/Village/EconomyService.cs` — extend with outpost count / multiplier / grant helpers; keep all existing.
- `Assets/_Modules/Village/World/MineNode.cs` — route BankYield (and callers Extract/Drain) through EconomyService.Grant(new ResourceCost(...)) + state (or let Grant own the write).
- `Assets/_Modules/Village/World/Camps/Outpost.cs` — replace direct state mutate + ResourcesChanged in BankTrickle with `EconomyService.Instance?.Grant(...)`.
- `Assets/_Modules/Village/World/Camps/ClaimableCamp.cs` — on HandleDefended, call `EconomyService.Instance?.OnOutpostSecured()` (or equiv) to incr count.
- `Assets/_Modules/Village/World/PetHarvestBootstrap.cs` — broaden TargetScene support ("Village", "Village2"), improve comments for pet farming enablement, ensure 4 resource types spawn for demo loop.
- `Assets/_Modules/Economy/README.md` + optionally touch the 3-4 dupe files with "SUPERSEDED — see Village MineNode + Pets.PetHarvester + EconomyService" header (no functional change).
- `Assets/_Modules/Pets/README.md` + `Assets/_Modules/Village/README.md` (or World sub) — add cross-ref to the integrated pet/outpost economy loop.
- (Optional small) `Assets/_Modules/Core/State/DifficultyTuning.cs` or just document the multiplier on EconomyService for wave systems to consume later.
- Update `PROJECT_INDEX.md` / `docs/README.md` if new design notes added (no new docs required for this WO).

## What NOT to touch
- No hand edits to `Village.unity` or any .unity.
- Do not touch `VillageSceneBuilder.cs`.
- Do not greenfield new node types or parallel inventory; reconcile to existing MineNode + EconomyService + GameState.
- Do not change PetHarvester state machine or MineNodeBridge (already complete per WO-229).
- No new asmdef coupling (Pets stays isolated).
- No UXML; code-built only if any UI.

## Acceptance criteria (line-by-line verifiable)
- [ ] Pet (with PetHarvester) in Village finds a MineNode (via bootstrap or world), transitions MovingToNode → Harvesting, calls TryAutoExtract, yield lands (visible in logs or via Economy snapshot).
- [ ] EconomyService.Grant is the path taken by node + outpost harvests; Wood/Iron _fields and OnChanged fire for harvest income; no double-add to persisted resources.
- [ ] ClaimableCamp → build Outpost → trickle calls Grant; after defense secure, Economy secured count >=1 and TerritoryMultiplier > 1.0.
- [ ] CampSystem / PetHarvestBootstrap remain dark-by-default or flag-controlled; enabling shows the loops without crashing or requiring scene changes.
- [ ] All cross-service calls use `?.`; no new System.Reflection except the established MineNodeBridge pattern.
- [ ] Brace balance passes (`python3 -c "..."`) on every .cs touched.
- [ ] Module READMEs updated with the integration story.
- [ ] (Build) `build-windows.ps1` or equivalent reports SUCCESS; exe launch with Village + pet deploy exercises no fatal harvest errors.
- [ ] Matches RESOURCE_ECONOMY_DESIGN (faucets via pet/outpost, no new wallets) and PLAYER_BASE P4 roadmap.

## Notes for implementer
- The "Economy class" = `EconomyService` (the multi-resource Grant/CanAfford facade over GameState + in-session). Use it as the choke point.
- Existing scaffolding (PetHarvester full state machine, Outpost/ClaimableCamp full lifecycle, MineNode single banking) is already 80-90% of the vision — this WO is the "using the Economy class" + visibility + scaling polish pass.
- Test primarily via runtime (bootstrap + deploy) or editor play; batchmode for VillageSceneBuilder only if a builder change is forced (it isn't).
- After edits: run brace gate on each file. Save RESULT.md on complete+verified.

Increment WO on next. Owner makes final creative calls on numbers (yields, multipliers, camp count).

---
(Generated per Claude.md §2 protocol at continuation of Village test + pet priority request.)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
