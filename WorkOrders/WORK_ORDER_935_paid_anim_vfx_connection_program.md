# WORK ORDER 935 — Paid animation + VFX pack connection program

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 307/307 suites` (Builds/w7-c, Builds/w7-r). AWAITING OWNER FELT-VERIFY to close.

*(Board note 2026-08-24 — Ready-queue audit, `READY_FOR_REVIEW.md`: leading token corrected to a canonical bucket word. Only the status token and this note changed; the ticket body and its claims are untouched.)*

*(Board note 2026-08-24: bucket corrected. The verdict word was non-canonical, so the parser fell through to its substring pass and bucketed this row as **Done** on a word that appeared later in the sentence — the WO-1180 laundering path, and it only ever errs toward "finished". No claim about the work changed; only the leading token, so the row buckets where its own text says it belongs.)*
**Lane:** Art / Combat feel / Catalog  
**Seat:** CLI implements; UI/PO ratifies element picks and feel  
**Banner:** main line next free after mint = **936** (this number = 935)

---

## 0. SME reading list (BINDING — complete before any code)

Do **not** invent a second catalog. These docs already define law; this WO only **connects** paid stock into those pipes.

### Animation / mocap
| Doc | What an SME must internalize |
|-----|------------------------------|
| `docs/ANIMATION_PIPELINE.md` | **CANON:** every body is Humanoid; `Assets/Action/` Shared + type folders; root motion baked in place; factories build controllers |
| `docs/reference/HERO_ANIMATION_DICTIONARY.md` | Live Knight **Grom**: KnightV3 + **KnightMocap**; Attack pill + Q/W/E/R; motion-castings.json; open gaps (stale Cast v0, off-theme kicks, silent R VFX) |
| `docs/SME/SWORD_SHIELD_MOCAP_SME.md` | Studio Mocap S&S: **45 clips, ~9 used**; 2-D strafe unused; 14/15 defensive clips unused; skill1/2 kung-fu kicks off-theme |
| `docs/enemy-codex.md` §5 | Enemy animation strategy: AccuRig → SkeletonHumanoid; KayKit Generic Rig_M/L; **GAP-PRIMARY = quadruped wolf** |
| `docs/SME/CHARACTER_PACKS_SME.md` | Supercyan Fantasy RPG, rigs, URP conversion debt |
| `docs/SME/KAYKIT_SME.md` + `docs/kaykit-asset-catalog.md` | Shared Rig_Medium/Large + Character Animations 1.1 |
| `docs/asset-inventory/README.md` + `01`/`03` | ~21k meshes; three unused shared-rig libraries |
| `docs/port-notes/animation-setup.md` | Port-era setup notes |
| `docs/animations/Knight_Anim_Inventory.md` | Knight clip inventory depth |

### VFX catalogs
| Doc | What an SME must internalize |
|-----|------------------------------|
| `docs/vfx/VFX_PREFAB_HANDBOOK.md` | **CANON pipeline:** never Instantiate pack art from gameplay; Family A continuous vs B burst; CopyAsset → `Resources/VFX/**`; `VFXType` append-only; facade → VFXManager |
| `docs/vfx/VFX_CREATIVE_PICKS_REGISTRY.md` | Element × 6-beat kit (aura→hit); owner-ratified 2026-08-05; loop-cap P0 context in §10 |
| `docs/design/VFX_DIRECTION_2026-08-05.md` | Loop-slot leak (`IsLoop` sticky); HDR off; scale=1 everywhere; phone landscape frame |
| `docs/SME/VFX_PACKS_SME.md` | Mirza / Spells / Lana / magenta class (Mirza pre-URP) |
| `docs/HOVL_STUDIO_SME.md` + `docs/vfx/HovlStudio_Inventory.md` | Hovl RPG Bundle v6; HS_ProjectileMover; loop vs logic projectiles; "not like the demo" gaps |
| `docs/MAGIC_VFX_LIBRARY.md` | Spells Pack 7-element matrix (Casting/Projectile/Explosion/Aura/Shield) |
| `docs/MIRZABEIG_VFX_NOTES.md` / `docs/LANA_RPG_VFX_NOTES.md` / `docs/SPELLS_PACK_NOTES.md` | Pack-specific notes |
| `docs/vfx/weapon_vfx_design.md` | Weapon trails / rarity |
| `docs/vfx/SkillTree_VFX_Mapping.md` | Ability → effect mapping intent |
| `docs/asset-inventory/04_vfx_spells_audio.md` | ~1000 effects available, historically ~38 wired |
| `docs/SME/ASSET_STORE_LEDGER_2026-07-12.md` | **Purchase identities / versions** (what you paid for) |
| `docs/audits/AUDIT_vfx_2026-06-28.md` | Earlier audit snapshot |

### Enemy roster (animation consumers)
| Doc | Role |
|-----|------|
| **`docs/enemy-codex.md`** (RATIFIED 2026-07-26 Hollow Ones) | Full bestiary: who needs Idle/Move/Attack/Hit/Death/**Cast**/Special; which body + which controller path |
| `docs/REGION_ENEMY_ROSTER.md` | Region placement |
| Live data | `enemies.json`, `Defs.cs ENEMY_DEFS`, `EnemyAnimatorFactory`, `WildlandsRoster` |

### Live code authorities (comments lie — verify these)
| System | Path |
|--------|------|
| Anim params | `AnimParams.cs` + `ActorAnimator.cs` |
| Hero bake | `HeroAnimatorFactory`, `motion-castings.json`, `KnightMocap.controller` |
| Enemy bake | `EnemyAnimatorFactory`, `SkeletonHumanoid`, Orc humanoid |
| Troop bake | `TroopFactory` (Knight/Ranger/Mage bind) + `TroopController` (Attack vs Cast) |
| VFX play | `VFXManager`, `VFXCatalog`, `HovlVfxCatalog`, facade / `VfxElementTables` |
| Self-containment gate | `VfxResourceSelfContainmentRegression` |

---

## 1. Investment truth (why this WO is careful)

You paid **real money** for Asset Store + itch packs. That investment is only realized when:

1. Pack art is **reachable in a player build** (gitignored packs do **not** ship unless mirrored into `Resources/` / Addressables).  
2. Gameplay **calls** the catalog at the right beat (cast / trail / hit / death).  
3. Controllers use the **studio clip sets as designed** (combo chains, Cast variants), not a single borrowed slash.  
4. We **do not rebuy or reauthor** what is already on disk.

### Purchase ledger (high-value combat feel)

| Spend class | Products (ledger) | On disk (approx) | Ship path today |
|-------------|-------------------|------------------|-----------------|
| **VFX (premium)** | Hovl RPG Bundle 6.0.4 | ~261 prefabs + HSFiles shaders | Partial via `HovlVfxCatalog` + Resources wrappers |
| **VFX (matrix)** | Zakhan Spells Pack 1.3.14 | ~466 prefabs | Nested in `Resources/VFX/Projectiles/*` |
| **VFX (volume)** | Mirza Ultimate VFX 3.5.2 | ~564 prefabs | Thin; **pre-URP magenta risk** |
| **VFX (casual)** | Lana Casual RPG VFX | tracked subset | Some icons/particles |
| **VFX (utility)** | Unity Particle Pack | ~69 prefabs | Recipes in handbook / WO-884 |
| **Anim (hero mocap)** | Studio Mocap S&S + Magical + Hero Motion | under `Assets/Action/Knight/Motion/…` | KnightMocap **~9 of 45** S&S clips |
| **Anim (library)** | Action Mixamo Humanoid (~401 FBX tracked) | `Assets/Action/` | Hero + enemy retarget source |
| **Anim/body** | Supercyan Fantasy RPG | gitignored; troops mirror SC_* | Bodies + gear; vendor controller **replaced** by AnimParams controllers |
| **Anim/body** | KayKit Adventurers + Char Anim 1.1 | gitignored | Enemy codex shared-rig path; not all silhouettes live |
| **Body bulk** | Blink RPG Ultimate (many SKUs) | huge on disk | Mostly **icons**; purge candidate at polish end |

**Utilization snapshot (2026-08-09 measured + docs):**

| Layer | Owned | Shipped / wired |
|-------|------:|-----------------|
| Resources/VFX game prefabs | — | **~54** under category folders |
| VFXType enum values | — | **~95** named beats |
| Hovl / Mirza / Spells raw prefabs | **~1,200+** | thin mapping + wrappers |
| Studio mocap S&S clips | **45** | **~9** on Knight |
| Troop strike VFX | packs exist | **TroopController = zero VFX calls** |

---

## 2. Architecture law (do not invent a second system)

### Animation
1. **One parameter vocabulary:** `AnimParams` (Speed, InCombat, Attack, Combo, Cast, CastVariant, Hit, Dead…).  
2. **One clip library root:** `Assets/Action/` (Humanoid + ActionClipImporter).  
3. **Hero live body:** KnightV3 + **KnightMocap** (ff.mocaploco); motion-castings.json is owner pick authority for abilities.  
4. **Enemies:** Humanoid AccuRig → SkeletonHumanoid; KayKit Generic → shared Rig controllers; Orc → OrcHumanoid Mixamo.  
5. **Troops:** bind `Resources/Heroes/{Knight|Ranger|Mage}.controller` by role/animator field; **melee Attack / archer Attack / mage Cast** (already fixed 2026-08-09).  
6. **Never** leave vendor Supercyan StrafeMovement as the combat driver (wrong params → slide).

### VFX
1. **Never** `Instantiate` gitignored pack paths from runtime gameplay.  
2. Pipeline: pick recipe → classify Family A/B → **CopyAsset tree** into `Assets/Resources/VFX/**` (+ `_Shared` mats/textures) → catalog row → play via **VFXManager / facade**.  
3. **`VFXType` is append-only** (ordinal serialization). One owner for enum appends.  
4. **IsLoop must be derived from emission**, not a sticky checkbox (loop-cap leak = P0 feel destroyer).  
5. Colourblind law: shape + motion direction, not hue alone.

---

## 3. Connection map (who consumes what)

### 3.1 Animation consumers → packs

| Consumer | Live controller / clips | Pack investment used? | Gap |
|----------|-------------------------|------------------------|-----|
| Hero Grom | KnightMocap + Action/studio mocap + magical-moves | Partial S&S + Magical | Strafe/block/parry/combo chains unused; skills use kung-fu kicks |
| Hero abilities | CastVariant + motion-castings | Partial | Stale generic Cast; R silent VFX; taunt=heal clip |
| Hollow AccuRig | SkeletonHumanoid + Action Mixamo | Yes (Action) | Specials thin; wolf GAP-PRIMARY |
| KayKit fodder/boss | Character Anim 1.1 Generic | Partial | Specials substituted |
| Orc warband | OrcHumanoid Mixamo | Yes (Action) | — |
| **Troops** | Knight/Ranger/Mage controllers | Action clips retargeted | **No strike VFX**; no bow projectile; catapult silent |
| Supercyan bodies | Humanoid retarget of hero controllers | Bodies yes; SC anims **discarded** | Acceptable if AnimParams controllers look good |

### 3.2 VFX consumers → packs

| Beat | Expected pack | Live consumer | Gap |
|------|---------------|---------------|-----|
| Tower muzzle / projectile / impact | Hovl + Particle Pack + Spells | DefenseTower / ArcaneTower | Loop flag leak historically starved all combat VFX |
| Hero spell 6-beat kit | Spells + Particle + Hovl | HeroAbilities + facade | Many beats still proc; Mage showcase incomplete |
| Enemy death / aura | Hovl / Spells / Resources/VFX/Death | Enemy death path | Codex specials under-dressed |
| Harvest / env / portal | Particle / Hovl markers | HarvestAura, Portal | — |
| **Troop melee hit** | Physical impact (Metal/Flesh) | **NONE** | Paid packs unused |
| **Troop archer shot** | Arrow trail + impact | **NONE** (instant damage) | Packs unused |
| **Troop mage cast** | Casting_Fire/Arcane + Projectile + Explosion | **NONE** | Worst ROI miss for Spells Pack |
| **Catapult volley** | EarthShatter / rock + impact | **NONE** | Siege fantasy incomplete |

### 3.3 Enemy codex as the animation test matrix

Use **`docs/enemy-codex.md` roster table** as the checklist of **required motion** (not optional polish):

| Codex role | Required anims | Required VFX (minimum) |
|------------|----------------|-------------------------|
| Fodder / Walker | Idle, Move, Attack, Hit, Death | Death_Generic |
| Standard melee | + Attack×2 | Impact_Physical |
| Skirmisher | + fast Move | Impact light |
| Healer / Caster | + **Cast** | Cast charge + projectile + impact |
| Elite 2H | + sweep | Impact_Shockwave / cleave |
| Heavy / Rig_Large | + slam | Dust / shockwave |
| Boss humanoid | + Special channel | Aura + special cast |
| Wolf quadruped | Own rig clips | Howl telegraph VFX |
| Alduin | Non-combat gestures only | Lighting/staging, not combat VFX |

Any WO phase that claims "enemies done" must tick codex rows, not only `enemies.json` count.

---

## 4. Phased program (implement in order — protects spend)

### Phase 0 — Inventory freeze (read-only, 1 session)
- [ ] Diff ledger vs disk (versions, missing URP import for Spells/Mirza).  
- [ ] Export one table: **pack prefab → Resources path → catalog key → call site** (or EMPTY).  
- [ ] Confirm self-containment gate green after any prior mirror work.  
- [ ] List top **20 unused Hovl/Spells recipes** that match VFX_CREATIVE_PICKS already ratified.  
**Done when:** a single `docs/vfx/UTILIZATION_SCORECARD_YYYY-MM-DD.md` exists (dated, not eternal canon).

### Phase 1 — Stop burning slots (P0 VFX health)
- [ ] Prove `IsLoop` is emission-derived on catalog generators (handbook + VFX_DIRECTION).  
- [ ] Re-run tower fire under combat load; zero `active loops 20/20 SKIPPED` in break-log.  
- [ ] Owner A/B on HDR if still off (VFX_DIRECTION §2).  
**Done when:** Hovl/Spells you already mapped actually *show* in a full wave.

### Phase 2 — Hero mocap ROI (use what you paid ActorCore for)
- [ ] Re-bake KnightMocap: fix **generic Cast** away from `atk_slashright` (HERO_ANIMATION_DICTIONARY gap 1).  
- [ ] Remap skill1/skill2 to **`atk_jump` / `atk_shieldcharge`** (SWORD_SHIELD_MOCAP_SME §4) — drop kung-fu kicks.  
- [ ] Wire one **parry** + keep block; leave full 2-D strafe as optional Phase 2b.  
- [ ] R ultimate: non-null cast VFX row (dictionary gap 5).  
**Done when:** Motion Caster / playtest reads "demo-adjacent," not kick-fu knight.

### Phase 3 — Troop combat feedback (raid ROI)
Connect **existing** VFXType / Hovl keys — no new enum unless missing after audit:

| Troop class | Anim (already) | VFX to attach on strike |
|-------------|----------------|-------------------------|
| Melee | Knight Attack | `Impact_Physical` or Hovl flesh/metal impact at hit point |
| Archer | Ranger Attack | optional muzzle + **Projectile_Arrow** or streak; impact on hit |
| Battlemage | Mage **Cast** | `Cast_MageCharge` / Casting_Fire → projectile → explosion (Spells matrix) |
| Catapult | static machine | EarthShatter / rock burst at target (siege) |

Rules:
- Call only through VFXManager; respect caps.  
- Prefer cataloged types already in VFX_CREATIVE_PICKS.  
- Mirror any new Resources prefabs via handbook builder (not hand-copy half GUIDs).

**Done when:** raid deploy of F/A/Mage/Catapult each produces distinct anim + VFX; no magenta on fresh clone path.

### Phase 4 — Enemy codex motion + VFX parity
- [ ] For every **live** `enemies.json` / raid garrison id: codex anim set present on controller.  
- [x] KayKit Rig_Medium casters now bake a real Cast state; AccuRig casters already had Cast + WindUp. Runtime `Enemy.RootedCast` drives both. Cast VFX was already live.
- [ ] Necromancer summon substitute + aura.  
- [ ] **Do not block** on Wildlands wolf (GAP-PRIMARY) unless owner prioritizes cold dungeons.  
**Done when:** wave + raid garrison reads as "codex animated," not capsule-with-slide.

### Phase 5 — Mage showcase + tower kit completion
- [ ] Finish Fire + Arcane + Ice full 6-beat kits for Mage abilities (registry §2–§3).  
- [ ] Tower elemental kits share same wheel (facade).  
- [ ] Document intentional procedural leftovers (Holy/Wind).

### Phase 6 — Catalog hygiene + scorecard
- [ ] Prune dead Hovl catalog rows that leak loops.  
- [ ] Utilization scorecard: target **meaningful** use of paid packs (not 100% of 1000 prefabs — that's vanity). Success = every **combat beat in the creative registry** has a pack-backed prefab and a live call site.  
- [ ] Update `docs/asset-inventory` utilization lines with dated measurements.

---

## 5. Explicit non-goals (protect budget and focus)

- Do **not** import Blink armor bulk as combat bodies (icons yes; purge later).  
- Do **not** rebuy "another VFX pack" until Phase 0 proves a hole in Spells/Hovl/Particle.  
- Do **not** flatten multi-layer Particle Pack trees.  
- Do **not** hand-edit `Village.unity`.  
- Do **not** renumber `VFXType` existing ordinals.  
- Do **not** treat enemy-codex Wildlands as live until owner un-defers.  
- Do **not** claim 100% of Mirza 564 prefabs must ship — only coherent combat language.

---

## 6. Acceptance criteria

### Machine
- [ ] `COMPILE_GATE_OK`  
- [ ] `VfxResourceSelfContainmentRegression` green (0 gitignored deps under Resources/VFX)  
- [ ] No new sticky `IsLoop=true` on pure-burst recipes in generators  
- [ ] TroopRoster still asserts Knight/Ranger/Mage strike mapping  

### Felt (PO / Claude)
- [ ] Hero: Attack combo reads sword; skills not kick-fu; cast not sword-slash  
- [ ] Troop melee/archer/mage/catapult: distinct anim + VFX  
- [ ] Full wave: tower VFX still present at shot 50+ (no permanent cap starve)  
- [ ] Raid: battlemage cast VFX visible; catapult impact reads siege  
- [ ] Enemy casters cast; melee swing; deaths pop  

### Investment
- [ ] Scorecard shows **call sites** for Spells matrix + Hovl combat keys + Studio mocap offensive clips  
- [ ] No new paid asset required to close Phases 1–4  

---

## 7. Suggested file touch list (when implementing)

| Phase | Likely files |
|-------|----------------|
| 1 | VFX catalog generators, HovlVfxCatalog rows, VFXManager loop reclaim |
| 2 | HeroAnimatorFactory, motion-castings.json (dual if any), KnightMocap bake |
| 3 | TroopController, optional TroopVfx helper, VFXCatalog rows, Resources/VFX mirrors |
| 4 | EnemyAnimatorFactory, Enemy death/cast hooks, codex-aligned specials |
| 5 | HeroAbilities, VfxElementTables, tower elemental keys |
| 6 | Dated utilization scorecard doc only |

---

## 8. Handoff note to implementers

You are not designing VFX from scratch. You are a **plumber**:

```
Paid pack (gitignored)
  → editor mirror into Resources/VFX (+ _Shared)
  → VFXCatalog / HovlVfxCatalog row (correct IsLoop)
  → gameplay beat calls VFXManager
  → AnimParams trigger already playing the matching motion
```

If anim and VFX disagree (e.g. Cast motion + no cast VFX, or Attack motion + fireball), the product still wastes the packs. **Pair them.**

Enemy codex is the **checklist of who needs which pair**, not a suggestion list.

---

## 9. Owner questions (block only Phase 2b / wolf)

1. Prioritize **hero mocap completeness** (strafe/parry matrix) vs **troop/raid VFX** first? (Recommend: Phase 1 → 3 → 2 → 4.)  
2. Un-defer Wildlands / wolf GAP-PRIMARY this quarter?  
3. HDR on for bloom language (cost OK on target phones)?  

---

_End WO-935. SME sources listed in §0; measured disk counts 2026-08-09; utilization narrative aligned with asset-inventory + creative registry + enemy codex._

---

## SLICE PLAN 2026-08-26

**Why this section exists:** §4 already phases this program, but a phase is not a slice — Phase 3 alone
is four unrelated troop classes in three different files. The board's warning applies literally here:
**one slice landing (the 08-21 KayKit caster Cast state) is not Phase 4 landing, and Phase 4 landing is
not the ticket landing.** Below, each slice is one implementable unit with its own acceptance.

**State verified at HEAD 2026-08-26 (opened at source):**
- `Assets/_Modules/Village/Combat/CombatCast.cs` EXISTS; `TroopController.cs:643` routes the **mage**
  strike through `CombatCast.Play(CastSpellIdFor(_element), …)` — Phase 3's battlemage row is DONE.
- `TroopController.Attack`'s **non-cast branch** (`:650-659`) does `foe.TakeDamage(...)` + an anim
  trigger and **plays no VFX at all** — Phase 3's melee and archer rows are UNSTARTED.
- `VFXType.Impact_Physical` is cataloged (`Editor/VFXCatalogGenerator.cs:108` → Lana
  `Slash_stone_once`, a ONESHOT) and the exact call pattern to copy is `Enemy.cs:1871-1886`
  (`Impact_Physical` + `HitSurfaceVfx.ResolveAndPlay`, both `Guard.Try`-wrapped, `playSound:false`).
- `VFXType.Projectile_Arrow` EXISTS in the enum. `Cast_MuzzleFlash` exists.
- **No `docs/vfx/UTILIZATION_SCORECARD_*.md` exists** — Phase 0 has never been done.

### Slice 935-1 — Troop MELEE strike VFX (Phase 3, melee row)
- **File:** `Assets/_Modules/Village/Troops/TroopController.cs` — the `else` branch of `Attack` only.
- **Do:** mirror `Enemy.cs:1871-1886` exactly: `Impact_Physical` at the target's chest +
  `HitSurfaceVfx.ResolveAndPlay`, both inside `Guard.Try`, `playSound:false` (the anim/audio beat is
  already played). No new `VFXType`, no new prefab, no catalog row.
- **Acceptance:** a source-lint suite asserts the melee branch calls `VFXManager.Play` and
  `HitSurfaceVfx`; the cast branch is untouched; felt-check that a raid melee hit now marks contact.
- **Blocked:** no. **Smallest slice in this program — start here.**

### Slice 935-2 — Troop ARCHER shot (Phase 3, archer row)
- **Files:** `TroopController.cs`, and whichever projectile helper the hero ranger already uses — find
  it, do NOT write a second projectile mover (`HS_ProjectileMover` / `SpellVfxFactory` are the
  incumbents).
- **Do:** the archer's damage is instant today. Either (a) muzzle + `Projectile_Arrow` streak +
  impact as pure presentation over the existing instant damage, or (b) a real travel time. **(a) only**
  — (b) changes combat math and is a different ticket.
- **Acceptance:** an archer strike shows a travelling arrow read and an impact; DPS is byte-identical
  (assert the damage call site is unmoved).
- **Blocked:** on 935-1 (same method, same file — serialize them, do not fan out).

### Slice 935-3 — Catapult volley (Phase 3, siege row)
- **Do:** rock/EarthShatter burst at the impact point.
- **Acceptance:** distinct from 935-1's slash arc at a glance, in greyscale.
- ⚠ **Blocked — needs an owner VFX tag.** Memory `vfx-map-owner-tags-no-creative-pick`: the owner tags
  the VFX key, the CLI maps key → hook verbatim. There is no `Impact_Earth*` / `EarthShatter` value in
  `VFXType` today, so this slice needs both a pick and an append-only enum value. **Do not pick one.**

### Slice 935-4 — Phase 0, the utilization scorecard (read-only)
- **File:** ONE new dated doc `docs/vfx/UTILIZATION_SCORECARD_2026-08-XX.md`. Dated, not eternal canon.
- **Do:** the §4 Phase 0 table — pack prefab → Resources path → catalog key → call site (or EMPTY).
- **Acceptance:** the doc exists and every row cites a file:line or the word EMPTY.
- **Blocked:** no. Read-only, perfectly parallel with 935-1. **This is the slice that tells the owner
  what her money bought**, and it has never been run.

### Slice 935-5 — Phase 2, hero mocap ROI (bake, needs Unity)
- Three separable sub-slices, each its own acceptance: (a) generic `Cast` off `atk_slashright`;
  (b) skill1/skill2 → `atk_jump` / `atk_shieldcharge`; (c) a non-null R-ultimate cast VFX row.
- **Blocked:** needs a Unity bake seat. (b) is a felt/creative change — ⚠ it re-points what the owner
  sees when she presses a button; recommend she is shown it, not told about it.

### Slice 935-6 — Phase 4 remainder: enemy codex parity
- The 08-21 KayKit caster slice is one row of the §3.3 matrix. The rest — Necromancer summon
  substitute + aura, per-role Attack×2 / sweep / slam — is unstarted.
- **Acceptance:** §3.3's table ticked ROW BY ROW against `docs/enemy-codex.md`, not an
  `enemies.json` count. ⛔ Wildlands / wolf stays deferred per §5 unless the owner un-defers.

### Slice 935-7 — Phase 5 + 6
- **Blocked** on 935-4 (Phase 6's scorecard target is meaningless before Phase 0's baseline) and on
  the owner's §9 question 3 (HDR).

**Ordering:** 935-4 ‖ (935-1 → 935-2). Then 935-6. 935-3 and 935-5 park on their pins.
**Owner pins outstanding, named precisely:** §9 Q1 (mocap-first vs troop-VFX-first — the WO's own
recommendation is 1→3→2→4), §9 Q2 (un-defer Wildlands wolf), §9 Q3 (HDR on), and the 935-3 VFX key tag.
