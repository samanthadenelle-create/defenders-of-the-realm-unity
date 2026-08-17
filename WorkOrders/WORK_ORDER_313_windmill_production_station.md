<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_313 — Create Windmill in town (production crafter station, like lumbermill/armorer/forge)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 1 (World/Env — VillageSceneBuilder) + Lane 12 (vendor wiring)
**Origin:** owner playtest 2026-06-06 · **Reconcile with:** the four production crafters (DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md), WO-291/294

## Problem
The Windmill (Mother Wren) is a core production crafter in the Forgemasters' saga but doesn't exist in town
yet. Lumbermill, Armorer, and Forge exist; the Windmill needs creating and wired the same way.

## Goal
Add a Windmill structure from the prefab asset catalog, placed in the Artisan district, wired as a stationed
production crafter exactly like lumbermill/armorer/forge (interactable NPC station + upgrade + role).

## Scope
- Builder (VillageSceneBuilder): place a Windmill prefab from the catalog (check `docs/polyperfect-asset-catalog.md`
  / Quaternius/KayKit medieval) in the Artisan/NW district at consistent scale.
- Wire like the existing crafters: stationed NPC (Mother Wren) + `NPCUpgradeStation` + interact → station UI,
  costs/income via `EconomyService`. Hook the Wren Yarn (WO-291) + saga role (the "Last Pressing" quench, WO-294).
- Update the asset/structure catalog + READMEs.

## Acceptance criteria
- [ ] Windmill exists in town (Artisan district), correct scale, from a catalog prefab (no missing-prefab errors).
- [ ] It's a stationed crafter parallel to lumbermill/armorer/forge: NPC + interact + upgrade via EconomyService.
- [ ] Hooks to Wren's dialogue/quest path (WO-291/294) where present.
- [ ] Placed via the builder (no .unity hand-edit); catalog/READMEs updated; brace check; CompileGate OK; build SUCCESS.

## Root cause (triage 2026-06-06)
**Confidence: Confirmed (genuinely absent).** A repo-wide search for `windmill`/`Windmill` finds **no
reference anywhere** in code or `VillageSceneBuilder` (only `Core/TreeOfLifeMaterialFixer.cs` matched "tree").
So the Windmill structure + Mother Wren station simply do not exist yet — pure additive work.
**Suggested minimal fix:** in `VillageSceneBuilder` place a Windmill prefab from the catalog in the Artisan
district, then wire it like the existing crafters — stationed NPC + `NPCUpgradeStation` (that component exists,
`Assets/_Modules/Village/Buildings/NPCUpgradeStation.cs`) + interact → station UI, costs/income via
`EconomyService`. Verify the chosen prefab exists in `docs/polyperfect-asset-catalog.md`; missing prefab →
LogWarning, not error. Lane-1 single-writer — serialize with WO-311/312.

## Do NOT touch
- Never hand-edit `Village.unity`. Lane 1 single-writer (coordinate w/ WO-311/312). Missing prefab → LogWarning, not error.
