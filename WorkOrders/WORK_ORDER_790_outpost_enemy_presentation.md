# WORK ORDER 790 — Outpost/garrison enemy presentation: flat green/orange + weapon not seated

**Status:** READY TO IMPLEMENT
**Lane:** Lane 9 (VFX/Art) — enemy visual/material path
**Type:** EXISTING (the spawn path is built; presentation degrades to fallbacks)
**Minted:** 2026-07-30 (owner felt-reports + screenshots from an EnemyOutpost garrison fight)
**Author:** UI/RCA seat (agent-sourced RCA). CLI implements + gates. PO felt-verifies + closes.

---

## Symptom (owner)

Playing an **EnemyOutpost garrison** (screenshots: "Garrison Troll (Lv 8)" boss bar, LOCKING):
- Enemies render as **flat solid-color silhouettes** — one bright **GREEN** (troll/orc), one solid
  **ORANGE/RED** — untextured / unlit look.
- "**enemy floating in air, weapon not seated properly**" (weapon-seating half of this WO; the
  floating half is WO-791's off-NavMesh placement).

## RCA — proven from code (read-only agent, file:line)

### Green / orange = EnemyFactory's DESIGNED fallback firing (no albedo on Orc-family meshes)
- The garrison spawns an **Orc-family** set (boss `orc-warlord`→`Orc_Necromancer`, guards
  `orc-raider`) — `EnemyOutpost.cs:855-873,514-517`; mapped `EnemyFactory.cs:421`.
- Orc/Warband rigs force-attach `TripoMaterialFixer` because their `.fbx.meta` remaps point at
  DELETED `tripo_mat_*.mat` (raw Phong → magenta otherwise) — `EnemyFactory.cs:132-143`.
- With **no committed basecolor** for the Warband/Troll family, EnemyFactory binds a **solid flat
  species TINT** instead of a texture — `EnemyFactory.cs:216-253`:
  - grunt orc → `Color(0.30f,0.42f,0.22f)` = **flat green** (`:248`)
  - boss → dark slate (`:247`); troll → grey-green (`:245`); ogre → grey (`:246`)
- `TripoMaterialFixer` applies that tint unconditionally with NO texture → solid unlit fill —
  `TripoMaterialFixer.cs:331`.
- **Orange/red** = the **capsule fallback** when a model has no renderable mesh:
  `TintCapsule → Color(0.55f,0.30f,0.35f)` — `EnemyFactory.cs:285-300,669-677` (reached when
  `VisualFactory.Skin`/render-verify fails for that model).
- **Why village enemies look fine:** village waves are mostly Hollow **Skeleton** ids that ship real
  KayKit/AccuRig materials (no tint path); the outpost's boss + orc guards resolve to the
  **textureless Orc family** that hits the tint/capsule fallback. It's the id→model mapping of this
  spawn path, NOT a per-scene skip.

### Weapon not seated
- Enemy held-weapons are **gated OFF** by owner (F8 2026-07-04: *"enemies spamming weapons in all
  sorts of odd ways — maybe we not add a weapon unless we perfect one"*): only attaches when
  `ff.enemyweapons==1` AND `model=="Orc_Berserker"` — `EnemyFactory.cs:190-194`.
- When enabled, `AttachEnemyWeapon` seats on the RightHand bone with a **data-driven grip offset from
  `AttachmentOffsetRegistry`** (Offset Forge, "eyeball default") — `EnemyFactory.cs:552-576`. An
  untuned offset = weapon floats/mis-seats. So "weapon not seated" is EITHER the untuned grip (if the
  gate is on) OR a stray attach that shouldn't happen with the gate off. **CLI: confirm from the
  screenshot/trace whether the mis-seated weapon is on an enemy or the hero, and whether
  `ff.enemyweapons` is on, before editing.**

## Candidate fix locations
- Provide committed albedo for the Orc Warband family (the `Enemies/OrcTex/*_basecolor` atlases the
  `OrcHumanoid` branch already expects — `EnemyFactory.cs:202-214`), so the tint fallback is not the
  final look; OR promote a proper material per the owner-tagged art (never substitute — hold for owner
  art direction on the orc skin).
- Capsule fallback (`:669-677`) is downstream of a missing/failed `Orc_Necromancer` mesh load — verify
  the mesh resolves; if it's a gitignored-pack dangle, tie to WO-785 (VFX/art survivability) pattern.
- Weapon grip: tune the `AttachmentOffsetRegistry` entry in the Offset Forge for the ONE weapon before
  re-enabling `ff.enemyweapons`, per the owner's standing ruling.

## Proving steps (§12 — run headless before/after)
- `[Flow:Enemy] garrison fallback TINT ... bound to '<model>'` (`EnemyFactory.cs:250`) and
  `[Flow:TripoMatFix] ... tintActive=True` (`TripoMaterialFixer.cs:278`) → confirms the green is the tint path.
- `[Flow:Enemy] model '<x>' had NO renderable mesh ... FALLBACK to tinted capsule` (`EnemyFactory.cs:291`)
  → confirms the orange capsule.

## Acceptance
- [ ] Outpost orc/troll garrison enemies render with proper materials (no flat green/orange), verified
      via `RunCaptureHeadless` screenshot (memory `headless-screenshot-verify-ui-before-build`).
- [ ] No mis-seated/floating weapon on garrison enemies (either gate stays off cleanly, or the tuned
      grip seats correctly).
- [ ] Brace/NUL gate green on any `.cs` edited; `COMPILE_GATE_OK`.
- [ ] Screenshot to owner; **PO closes**.

## What NOT to touch
- Owner-tags-art rule: do not invent an orc skin — hold for owner art direction if a texture must be chosen.
- Do not flip `ff.enemyweapons` on without the perfected grip (owner ruling).
- Non-movement/floating placement = WO-791 (off-NavMesh) — don't fix navmesh here.

*Notion row pending.*
