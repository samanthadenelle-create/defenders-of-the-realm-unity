# RESULT — WO-818 KayKit NPC body per structure (all phases)

**Shipped:** 2026-08-01 — Phase 1 commit `e8bd17b0`, Phases 2-3 commit `777dd9ff`
(oracle registration `1371e70a`).
**Gates:** COMPILE_GATE_OK + REGRESSION_OK incl. new `NPC_MODELS_OK` oracle — verified headless.

## What shipped
- **Phase 1 (`e8bd17b0`):** `DeNelle.Editor.KayKitNpcImporter.StageAll` ran headless —
  `KAYKIT_STAGE_OK 12/12`, all 12 Humanoid avatars retarget-ready. Staged assets tracked at
  `Assets/Resources/NPCs/KayKit/` (12 FBX + textures + materials, 66 files).
- **Phases 2-3 (`777dd9ff`):** `repo.npcModel` field on RepoProps; `structures-catalog.json` v6,
  both copies hash-identical, with exactly 12 owner rows:
  barracks=Paladin_with_Helmet, workshop=Engineer, forge=Barbarian, armorer=BlackKnight,
  jeweler=Tiefling, market=Hoarder, arcane-tower=Mage, pet-house=Druid, collector_farm=Farmer_A,
  mill=Farmer_B, collector_lumbermill=Ranger, fountain_healing=Cleric.
- Shared `KayKitNpcBody` resolver (KayKit-first -> People chain -> capsule; one `FlowTrace.Warn`
  on an authored-but-broken slug). `BarracksNpcInjector` + `CastleVendorNpcInjector` consume it.
  `NormalizeToHeroHeight` kept.
- **Oracle (`1371e70a`):** `CheckNpcModels` — `NPC_MODELS_OK` asserts dual-copy parity,
  staged-FBX existence, and a verbatim 12-row pin (a 13th row must update the oracle in the
  same commit).

## PO felt-verify still open
- [ ] KayKit bodies visible in the hub (unique body per structure; no more four-prefab clones).

## Known limitation (tracked)
- KayKit bodies stand statically (no AmbientNPC/Animator) — animated idles = follow-up WO.
