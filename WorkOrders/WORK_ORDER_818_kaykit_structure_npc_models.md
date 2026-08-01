# WORK ORDER 818 — KayKit NPC body per structure (data-driven, unique)

**Status: SHIPPED 2026-08-01 — all phases (e8bd17b0 phase 1 KAYKIT_STAGE_OK 12/12; 777dd9ff phases 2-3 repo.npcModel); NPC_MODELS oracle green.**
**Owner directive (2026-08-01, verbatim):** "have a team go through the kaykat characters and match a
unique kaykat model for each structure that can be stored in json with the structure ... so they load neatly"
**Silo:** World/NPCs (art-adjacent; model mapping is owner-retaggable data)

## Why
Every structure NPC today wears one of four polyperfect People prefabs (Blacksmith / Merchant /
two Peasants) — doubles read as clones, and the drillmaster is literally the blacksmith body.
KayKit packs ship 40+ distinct characters but are gitignored outside `Resources/` and therefore
NOT runtime-loadable. The fix: stage chosen models into tracked `Resources/NPCs/KayKit/`, and bind
structure -> model in the structures catalog so a swap is a one-word JSON edit.

## The mapping (owner-approved table; retag any row by editing JSON)
| structureId | KayKit model |
|---|---|
| barracks | Paladin_with_Helmet |
| workshop | Engineer |
| forge | Barbarian |
| armorer | BlackKnight |
| jeweler | Tiefling |
| market | Hoarder |
| arcane-tower | Mage |
| pet-house | Druid |
| collector_farm | Farmer_A |
| mill | Farmer_B |
| collector_lumbermill | Ranger |
| fountain_healing | Cleric |

All 12 verified UNUSED (guid-grep zero references) and unique. KayKit `Knight` deliberately avoided:
slug collides with the hero's `Resources/Heroes/Knight.fbx` + the dev anim harness.

## Phases
1. **DONE (this session):** `Assets/Editor/KayKitNpcImporter.cs` — batchmode stager
   (`DeNelle.Editor.KayKitNpcImporter.StageAll`, menu `Defenders/Art/Stage KayKit NPC Bodies`).
   Copies FBX + texture per row into `Assets/Resources/NPCs/KayKit/`, flips copies Humanoid
   (PeopleCharacterImporter idiom), marker `KAYKIT_STAGE_OK <n>/12`. All 12 source paths resolved.
2. **Catalog binding:** add `repo.npcModel` (string, KayKit slug) to `RepoProps` + the 12 rows in
   BOTH `structures-catalog.json` copies (StreamingAssets source + Resources WebGL twin — byte-identical).
   NOTE: sequence AFTER WO-819's `repo.bakedTwins` edits land (same files).
3. **Injector consumption:** `BarracksNpcInjector` + `CastleVendorNpcInjector` resolve the body as
   `Resources.Load("NPCs/KayKit/" + repo.npcModel)` FIRST, falling back to the existing People prefab
   chain, then the capsule placeholder. `NormalizeToHeroHeight` (1.95m) already handles KayKit scale.

## Acceptance criteria
- [ ] Stager run produces `KAYKIT_STAGE_OK 12/12`; staged copies COMMITTED (Resources is tracked).
- [ ] Humanoid avatar verdict logged OK for all 12 (Generic = flag for donor-avatar repair, do not ship silently).
- [ ] Each of the 12 structures' NPC spawns its mapped KayKit body in the hub (screenshot per NPC).
- [ ] A deliberately-bad `npcModel` value degrades to the People fallback + one FlowTrace.Warn — never a blank NPC.
- [ ] Dual-copy parity oracle covers the new field (extend WO-819's CheckSingletons pattern or DataWebRegression).

## Do NOT touch
- The hero rig (`Resources/Heroes/*`), Bryn (`Rogue_Hooded`), skeleton enemies — existing KayKit users stay as-is.
- No creative substitution: the table above is the owner's; any change to it is an owner retag, not a CLI pick.
