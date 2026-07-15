# Grok-01 — VFX Guidance (Hovl combat: towers · sword/shield · spellcasting)

**Status:** LIVING guidance — owner / CLI implementation reference  
**Author:** Grok (SME pass) · **Date:** 2026-07-14  
**Series:** `Grok-01` = first Grok-authored ops/guidance pack for this project  
**Implements via:** `WorkOrders/WORK_ORDER_715_hovl_towers_melee_spell_vfx.md` (READY)  
**Sources (binding + visual gold standard):**
- `docs/HOVL_STUDIO_SME.md` — pack architecture, demos, bloom/tint/soft-stop gaps  
- `docs/vfx/HovlStudio_Inventory.md` — prefab inventory + shortlist  
- `docs/vfx/SkillTree_VFX_Mapping.md` — ability → catalog key table (already wired in data)  
- `docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md` — `vfxKey` on motion-castings rows  
- `docs/SME/SWORD_SHIELD_MOCAP_SME.md` — combo clip map  
- Pack demos under `Assets/Hovl Studio/**/Demo*.unity`  
- Vendor portfolio: [hovl.artstation.com](https://hovl.artstation.com/)  
- Fidelity already landed: `WorkOrders/WORK_ORDER_689_hovl_vfx_fidelity.RESULT.md`

> If this doc and a WO conflict on *current tree state*, **code + newest RESULT win**.  
> If this doc and HOVL SME conflict on *vendor recipe*, **HOVL SME + demos win**.

---

## 0. TL;DR

| Question | Answer |
|---|---|
| What pack is canon for combat VFX? | **Hovl Studio RPG VFX Bundle v6** (`Assets/Hovl Studio/`) — URP Shader Graph, no magenta class |
| How do we play effects? | `VFXManager.PlayKey(key, …)` → `HovlVfxCatalog` (script-generated, pooled) |
| How should projectiles look? | **Flash (muzzle) → flying loop → Hit (impact)** — never a bare bolt |
| Which projectile prefabs? | **`Projectile VFX loop/`** only (script-free); we drive movement |
| Why hero VFX often silent? | **Registry-only motion VFX** (owner 2026-07-12): empty `motion-castings` `vfxKey` = silent by design |
| Biggest tower gap? | Towers fire **cast + impact** only; travel is still mesh `ProjectilePool` — need Hovl **travel** loop |
| Biggest melee gap? | Combo rows lack `vfxKey`; trail exists (`WeaponTrailController`) but no timed slash plate |
| Already fixed (don’t redo)? | Bloom ~4.5 + hue-only tint (WO-689); soft-stop trails already default |

**Implement work:** WO-715 (slices A–F). This file is the **why + which prefab + laws**; the WO is the **file list + acceptance**.

---

## 1. Vendor look language (demos + ArtStation)

Hovl’s demos and portfolio ([ArtStation](https://hovl.artstation.com/)) sell effects with a fixed recipe — not “more particles”:

1. **HDR additive particles + Bloom**  
   Demo `VolumeURP.asset`: Bloom intensity **~5**, threshold **~1.1**. Promo media explicitly depends on post-process bloom.  
   *We ship ~4.5 / 1.1 via WorldFeel (WO-689).* Keep; only retune if owner felt still flat.
2. **Projectile triplet**  
   Muzzle **Flash** detaches at fire point → **loop projectile** flies with trail → **Hit** orients to surface.  
   Never spawn travel without impact; never impact without a readable travel on mid-range shots.
3. **Script-free travel prefabs**  
   v6.0.3 separates `Projectile VFX loop/` (visual only) from `Projectiles with logic/` (`HS_ProjectileMover`).  
   **Gameplay uses loop folder + our mover/follower** (same as Infinity PBR Projectile Factory). Do **not** run `HS_ProjectileMover` on pooled gameplay bolts.
4. **Shape / motion over hue**  
   Owner is red/green colorblind. Slash arcs, shield bubbles, lightning bolts, fire cores must read on daylight terrain.  
   Heal/shield catalog rows stay `recolorable: false` where authored (gold/holy shape).
5. **Portfolio pieces that match our lanes**  
   - Sword: *Unity VFX – Sword slashes*  
   - Defense: *Magic shield effects*  
   - Ranged/fire/lightning: *Fire spells*, *Lightning explosion*, archer / fire-arrow pieces  

**Demo scenes to A/B against (felt-verify):**
- `AAA Projectiles Vol 1/…/Demo projectiles simple spawning.unity`
- `AOE Magic spells Vol.1/…/Demo AOE skills.unity`
- `Magic circles/…/Demo magic circles.unity`
- `RPG VFX Bundle/…/Demo random effects.unity`

---

## 2. Our architecture (reuse — do not greenfield)

| Piece | Role |
|---|---|
| `VFXManager` + `VFXManager.Hovl` | `PlayKey`, pool, soft-stop, hue recolor |
| `HovlVfxCatalog.asset` | Key → prefab (Resources); **script-authored only** |
| `HovlVfxCatalogGenerator` | Menu / batchmode; marker `HOVL_VFX_CATALOG_OK` |
| `VfxManualPicks.json` | Owner overrides (manual wins on key collision) |
| `RangedAttackVFX.PlayHovlTravel` + `HovlVfxFollower` | Proven travel attach pattern |
| `TowerCombat` Cast/Impact keys | Element → key (travel missing) |
| `HeroAbilities` | Registry-only cast; residual/travel paths documented in SkillTree map |
| `motion-castings.json` | **Authority for hero motion VFX** (`vfxKey`, `vfxDelay`, `attachBone`) |
| `WeaponTrailController` | Blade trail layer 0 — keep; Hovl slash = layer 1 |

**One key space.** No parallel enum for Hovl. No `Instantiate` of Hovl prefabs outside the pool.  
**No hand-edits** under `Assets/Hovl Studio/**` — scale/tint at runtime; override paths via generator / manual picks.

---

## 3. Recommended prefabs by lane

Paths under `Assets/Hovl Studio/`. Prefer matching family index across Flash / loop / Hit.

### 3.A Tower projectiles

| Tower / role | Travel (loop) | Muzzle (Flash) | Impact (Hit) | Catalog keys |
|---|---|---|---|---|
| **Arcane Spire / Mage** | `AAA …/Projectile VFX loop/Projectile 17 nova violet.prefab` | `Flash and hits/Flash 17 nova violet.prefab` | `Hit 17 nova violet.prefab` | `Arcane_Projectile` / `_Cast` / `_Impact` |
| **Fire** | `…/Projectile 16 fire.prefab` | `Flash 16 fire` | `Hit 16 fire` | `Fireball_*` |
| **Frost** | `…/Projectile 26 blue diamond.prefab` | `Flash 26 blue crystal` | `Hit 26 blue crystal` | `Frost_*` (+ add `Frost_Cast` if missing) |
| **Lightning** | `…/Projectile 2 electro.prefab` | `Flash 2 electro` | `Hit 2 electro` | `Thunderbolt_*` |
| **Archer / Ballista (bolt)** | `…/Projectile 11 orange arrow.prefab` | `Flash 11 orange arrow` | `Hit 11 orange arrow` | `Spear_*` (+ add `Spear_Cast` if missing) |
| **Siege (optional later)** | heavier family (e.g. 25 / 19) | matching Flash | matching Hit | NEW only if siege ships |
| **Beam (V2 optional)** | `3D Lasers Pack/…/Laser beam 10 fire.prefab` | — | HitEffect child | Requires **`Hovl_Laser` intact** + `DisablePrepare` — separate sub-slice |

**Do not use for towers:** `Projectiles with logic/`, `Projectiles(Particle collision)/`, 2D folder, Meteor shower as continuous fire.

**Element / style → keys (implementer table):**

| Condition | Cast | Travel | Impact |
|---|---|---|---|
| Flame | Fireball_Cast | Fireball_Projectile | Fireball_Impact |
| Aether / spell style | Arcane_Cast | Arcane_Projectile | Arcane_Impact |
| Ice | Frost_Cast | Frost_Projectile | Frost_Impact |
| Lightning | Thunderbolt_Cast | Thunderbolt_Projectile | Thunderbolt_Impact |
| bolt / Physical default | Spear_Cast | Spear_Projectile | Spear_Impact |

### 3.B Sword & shield combos

ArtStation slash language: **curved slash plate + short hit spark**, not a full AOE on every light hit.

| Beat | Prefab | Key | When |
|---|---|---|---|
| Light slash (combo 1–2, heavy) | `AOE Magic spells Vol.1/Prefabs/Flower slash.prefab` | `Melee_Slash` | Hit frame via `vfxDelay` |
| Hit spark | `RPG VFX Bundle/…/Punch Hit.prefab` | `Melee_Impact` | Contact / thrust |
| Cleave / spin / leap land | `AOE …/Energy explosion.prefab` (scale ~1.1–1.3) | `Cleave_Impact` | Finisher / land only |
| Shield bash | Punch Hit or `Chain strike.prefab` | `Melee_Impact` / optional `Shield_Bash_Impact` | Bash skill |
| Block raise (optional) | short holy shield | `Aegis_Cast` / NEW `Parry_Flash` | One-shot on raise |
| Block hold (optional loop) | `Magic circles/…/Magic shield holy loop.prefab` | `Aegis_Shield` | Parent shield bone |

**Keep** `WeaponTrailController`. **Do not** double-fire Melee_Slash from trail code *and* registry on the same frame without delay separation.

**Starting knight registry rows** (owner retunes in Motion Caster; `manual: true`):

| Keyword | Suggested vfxKey | vfxDelay (start) |
|---|---|---|
| attack1 | Melee_Slash | 0.18 |
| attack2 | Melee_Slash | 0.18 |
| attack3 | Melee_Impact | 0.22 |
| heavy | Melee_Slash | 0.22 |
| skill1 (leap) | Cleave_Impact | 0.40 |
| skill2 (shield charge) | Melee_Impact | 0.25 |
| block | Aegis_Cast or Parry_Flash | 0.05 |

### 3.C Spellcasting

Reuse keys from `SkillTree_VFX_Mapping.md` — **do not invent parallel names**.

| Spell family | Cast | Projectile | Impact | Residual |
|---|---|---|---|---|
| Fireball / Emberbrand | Fireball_Cast | Fireball_Projectile | Fireball_Impact | Ember_Burn |
| Thunderbolt | Thunderbolt_Cast | Thunderbolt_Projectile | Thunderbolt_Impact | — |
| Arcane Bolt | Arcane_Cast | Arcane_Projectile | Arcane_Impact | — |
| Frost / snare | Frost cast/flash | Frost_Projectile | Frost_Impact | optional Debuff |
| Heal / Mend / Oathmend | Heal_Cast | — | — | Heal_Aura |
| Eternal Aegis | Aegis_Cast | — | — | Aegis_Shield |
| Warden’s Roar | Taunt_Roar | — | Melee_Impact | Taunt_Aura |
| Dash | Dash_Blink | — | — | — |
| Raid / ultimate AOE | — | — | Raid_Explosion | Cap concurrent ≤ 2–3 |

**Ultimate-only upgrade (optional):** Thunderbolt cast → AOE `Lightning strike.prefab` (new key) — not light spam.

---

## 4. Architecture laws (non-negotiable)

1. **One key space:** `VFXManager.PlayKey` / `HovlVfxCatalog` only.  
2. **Registry-only for hero motion VFX** while that owner law holds: fill `motion-castings.json` `vfxKey`s — do not re-enable abilities.json as sole cast authority.  
3. **Towers are not motion-registry:** element/style → keys in tower code (or catalog props); add **travel** attach on projectile.  
4. **Pool everything hot;** soft-stop travel on impact (default Stop path). Caps: oneshots ~40 / loops ~20.  
5. **Presentation ≠ damage:** travel may be cosmetic if hitscan remains; prefer visual duration ≈ flight time for bolts.  
6. **No dual VFX stacks** for the same beat (VFXManager + AbilityVfxKit both firing).  
7. **ASCII / colorblind:** meaning by shape, motion, timing — never hue alone.  
8. **Do not ship** Mirza Beig / Spells Pack as primary combat path (URP risk / gitignored). Hovl is the combat canon.

---

## 5. Implementation slices (summary — detail in WO-715)

| Slice | Work | Leverage |
|---|---|---|
| **A** | Catalog Flash completeness + regen (`HOVL_VFX_CATALOG_OK`) | Data |
| **B** | Tower Flash → **Travel** → Hit on projectile | **Highest tower feel** |
| **C** | Knight motion-castings `vfxKey` for combos / shield / leap | **Highest melee feel** |
| **D** | Spell cast/travel/impact registry wires + residual verify | Spellcasting |
| **E** | Impact orientation utility (closes WO-689 deferred) | Polish |
| **F** | CompileGate + DataRegression + felt A/B vs demos | Ship |

**Suggested marker when keys resolve + critical paths traced:** `HOVL_COMBAT_VFX_OK`.

---

## 6. Fidelity ledger (don’t thrash)

| Gap | Status | Source |
|---|---|---|
| Bloom off overworld | **Fixed** (~4.5 / thr 1.1) | WO-689 |
| Flat StartColor tint | **Fixed** (hue-only) | WO-689 |
| Trail hard-clear on impact | **Already soft-stop** | WO-689 RESULT |
| Impact identity rotation | **Open** → WO-715 E | WO-689 deferred |
| Tower travel Hovl | **Open** → WO-715 B | This guidance |
| Melee/spell registry keys empty | **Open** → WO-715 C/D | Registry-only law |

---

## 7. Explicit non-goals

- Buying Toon Projectiles 2 / AAA Stylized (not owned).  
- Wiring Mirza / Spells Pack as primary combat VFX.  
- Replacing `WeaponTrailController`.  
- Using `HS_ProjectileMover` as the gameplay mover.  
- Hand-editing Hovl prefab YAML.  
- Laser beam towers unless pulled as optional sub-slice.  
- Build-mode HUD / wall drag (separate WOs).

---

## 8. Verification (PO / CLI)

1. **Towers:** ≥3 types (bolt / spell / fire) show muzzle + travel + impact on device/WebGL.  
2. **Sword/shield:** 3-hit combo + one shield skill show timed Hovl; trail still works.  
3. **Spells:** Fireball / Thunderbolt / Arcane / Mend show cast (+ travel/impact if ranged).  
4. **No regression:** registry-only intact; pool soft-stop; bloom on; no Village↔HUD edge.  
5. **Gates:** `COMPILE_GATE_OK` + DataRegression baseline.  
6. **RESULT:** `WorkOrders/WORK_ORDER_715_hovl_towers_melee_spell_vfx.RESULT.md` with proving `[Flow:*]` lines.

---

## 9. Related files index

| Path | Role |
|---|---|
| `docs/vfx/Grok-01-VFX-guidance.md` | **This file** — recommendations + laws |
| `WorkOrders/WORK_ORDER_715_hovl_towers_melee_spell_vfx.md` | Implementation WO (READY) |
| `docs/HOVL_STUDIO_SME.md` | Full Hovl SME dossier |
| `docs/vfx/HovlStudio_Inventory.md` | Prefab inventory + shortlist §5 |
| `docs/vfx/SkillTree_VFX_Mapping.md` | 16 actives → keys |
| `docs/vfx/weapon_vfx_design.md` | Older weapon-tier flair (pre-Hovl-primary; do not override Hovl canon) |
| `Assets/Editor/HovlVfxCatalogGenerator.cs` | Key → path Map |
| `Assets/Resources/VFX/HovlVfxCatalog.asset` | Runtime catalog |

---

*Grok-01 — update in place when prefab picks or registry law changes; keep WO-715 RESULT as the ship proof.*
