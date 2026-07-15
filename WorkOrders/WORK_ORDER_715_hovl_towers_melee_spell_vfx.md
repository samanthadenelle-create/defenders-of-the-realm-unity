# WORK ORDER 715 — Hovl VFX proper wire: towers + sword/shield combos + spellcasting

**Status:** READY TO IMPLEMENT  
**Priority:** P0 (feel / demo look) — Pi mobile-web demo  
**Lane:** VFX / Combat Feel  
**Effort:** Medium–Large (data-first; small code seams)  
**Owner ask:** recommend best Hovl effects (SME + demos + [hovl.artstation.com](https://hovl.artstation.com/)) and properly implement for **tower projectiles**, **sword & shield combos**, **spellcasting**.  
**Guidance doc (canon for picks + laws):** `docs/vfx/Grok-01-VFX-guidance.md`  
**Program slot:** Grok-03 Wave C (combat juice) — `docs/UI/Grok-03-here-to-there-WO-program.md`  
**Depends on (already shipped):** WO-VFX-001 inventory · WO-VFX-002 catalog · WO-VFX-003 skill-tree keys · WO-689 fidelity (bloom 4.5 + hue-only tint) · Action Keyword Registry / Motion Caster · `HovlVfxCatalogGenerator`  
**Does NOT depend on:** new Asset Store packs · HS_ProjectileMover · Mirza Beig / Spells Pack (gitignored / URP-fragile)

---

## 0. North star (vendor + ArtStation language)

Hovl’s demos and portfolio sell effects with a fixed recipe (see `docs/HOVL_STUDIO_SME.md` §3 + ArtStation pieces e.g. *Sword slashes*, *Magic shields*, *Fire spells*, *Lightning explosion*):

1. **HDR additive particles + bloom** (demo Volume bloom ~5; we run ~4.5 via WorldFeel — keep).  
2. **Triplet composition for projectiles:** **Flash (muzzle) → flying loop projectile → Hit (impact)** never a bare bolt.  
3. **Dark enough contrast** so additive reads; we can’t full demo-black outdoors — bloom + bright cores carry it.  
4. **Shape/motion over hue** (owner red/green colorblind) — slash arc, shield bubble, lightning bolt, fireball core are the read.  
5. **Script-free `Projectile VFX loop/` + our mover** (vendor v6.0.3 pattern; Infinity PBR Projectile Factory same) — **do not** drive `HS_ProjectileMover` on gameplay paths.

**Already true in code:** `VFXManager.PlayKey` + `HovlVfxCatalog` (30+ keys) + pooling + hue recolor (WO-689).  
**Not true yet for the player’s feel:**

| Lane | Gap |
|---|---|
| **Towers** | Only **cast + impact** Hovl keys; travel body is still `ProjectilePool` mesh/art — no Hovl **travel** loop on the bolt. No true Flash→fly→Hit triplet. |
| **Sword/shield** | Basic swings = trail only (`WeaponTrailController`). Combo / shield bash / leap lack **registry `vfxKey` rows**. `Melee_Slash` / `Melee_Impact` / `Cleave_Impact` exist but **motion-castings `vfxKey` is empty** on knight attack rows → **registry-only mode silences** abilities.json VFX. |
| **Spellcasting** | Ability keys exist in catalog + abilities.json, but **registry-only** means cast/impact only fire when Motion Caster rows carry `vfxKey` / phase fields. Most rows empty → silent casts. |

---

## 1. Recommended prefabs (best of pack for our three lanes)

Paths under `Assets/Hovl Studio/`. Prefer **`Projectile VFX loop/`** + matching **`Flash and hits/`**. Catalog keys map 1:1 into `HovlVfxCatalogGenerator` Map (+ `VfxManualPicks.json` for owner overrides).

### 1.A Tower projectiles (family index = AAA Vol 1)

| Tower / role | Travel (loop) | Muzzle (Flash) | Impact (Hit) | Catalog keys (use / add) | Why (SME + demo) |
|---|---|---|---|---|---|
| **Arcane Spire / Mage** (spell) | `Projectile VFX loop/Projectile 17 nova violet.prefab` | `Flash and hits/Flash 17 nova violet.prefab` | `Flash and hits/Hit 17 nova violet.prefab` | `Arcane_Projectile` / `_Cast` / `_Impact` | Violet nova = readable “magic bolt” on daylight terrain; already catalogued. |
| **Flame / Fire tower** | `…/Projectile 16 fire.prefab` | `…/Flash 16 fire.prefab` | `…/Hit 16 fire.prefab` | `Fireball_*` | Vendor fire family; hot core feeds bloom. |
| **Frost tower** | `…/Projectile 26 blue diamond.prefab` | `…/Flash 26 blue crystal.prefab` *(add if missing)* | `…/Hit 26 blue crystal.prefab` | `Frost_*` | Crystal shard = ice without hue-only read. |
| **Electro / lightning tower** | `…/Projectile 2 electro.prefab` | `…/Flash 2 electro.prefab` | `…/Hit 2 electro.prefab` | `Thunderbolt_*` | Stylized bolt; ArtStation “Lightning explosion” language. |
| **Archer / Ballista (physical bolt)** | `…/Projectile 11 orange arrow.prefab` | `…/Flash 11 orange arrow.prefab` *(add)* | `…/Hit 11 orange arrow.prefab` | `Spear_*` | Arrow family; matches `projectileStyle: "bolt"`. |
| **Siege / heavy (optional later)** | `…/Projectile 25 orange explosion.prefab` *(or 19 circle bomb)* | matching Flash | matching Hit | `Siege_*` NEW only if siege towers ship | Heavier payload; keep pool small. |
| **Beam tower (V2 / optional)** | `3D Lasers Pack/Prefabs/Laser beam 10 fire.prefab` | — | laser HitEffect child | NEW `Laser_*` | **Requires `Hovl_Laser` intact** + `DisablePrepare` before pool return — separate mini-slice. |

**Do not use for towers:** `Projectiles with logic/` (HS_ProjectileMover fights our pool), `Projectiles(Particle collision)/`, 2D projectile folder, Meteor shower as continuous fire (cap/perf).

### 1.B Sword & shield combos (melee read)

ArtStation *Sword slashes* / *Demon slash* language: **bright curved slash plate + short hit spark**, not a full AOE explosion on every swing.

| Beat | Prefab | Key | Attach / when |
|---|---|---|---|
| **Light slash (attack1/2/3, heavy)** | `AOE Magic spells Vol.1/Prefabs/Flower slash.prefab` | `Melee_Slash` | Weapon bone or chest+forward; `vfxDelay` = hit frame (~0.15–0.35s from Motion Caster) |
| **Hit spark on connect** | `RPG VFX Bundle/Random effect prefabs/Punch Hit.prefab` | `Melee_Impact` | At contact point; scale 0.7–1.0 |
| **Cleave / spin / combo finisher** | `AOE …/Energy explosion.prefab` (scale ~1.1–1.3) | `Cleave_Impact` | Feet / blast centre; **not** every light hit |
| **Shield bash (block skill / atk_shieldswipe)** | Cast: `Melee_Slash` or `Flash 11` small; Impact: `Punch Hit` or `Chain strike.prefab` | `Melee_Impact` / NEW `Shield_Bash_Impact` → `RPG …/Chain strike.prefab` | Forward cone centre |
| **Heroic Leap land** | Impact: `Cleave_Impact` or `Front spikes attack.prefab` | `Cleave_Impact` / NEW `Leap_Impact` | Landing point |
| **Block hold (loop, optional)** | `Magic circles/…/Loop version/Magic shield holy loop.prefab` | `Aegis_Shield` (reuse) | Parent to shield bone; soft-stop on release |
| **Parry one-shot** | `RPG …/Yellow Flash.prefab` or `Punch Hit` | NEW `Parry_Flash` | Shield bone |

**Keep:** `WeaponTrailController` trail as layer 0 (steel read). Hovl slash = layer 1 on **registry keywords only** (owner Motion Caster canon).  
**Avoid:** `Meteor*` on light swings; `Dragon punch` except ultimate; dual-stack VFXManager + AbilityVfxKit for same beat.

### 1.C Spellcasting (hero + enemy)

| Spell family | Cast | Projectile | Impact | Residual | Notes |
|---|---|---|---|---|---|
| **Fireball / Emberbrand** | `Fireball_Cast` | `Fireball_Projectile` | `Fireball_Impact` | `Ember_Burn` | Triplet — demo recipe |
| **Thunderbolt** | `Thunderbolt_Cast` | `Thunderbolt_Projectile` | `Thunderbolt_Impact` | — | Cast can upgrade to AOE `Lightning strike.prefab` for ultimate only |
| **Arcane Bolt** | `Arcane_Cast` | `Arcane_Projectile` | `Arcane_Impact` | — | Universal W |
| **Frost / snare** | Flash frost | `Frost_Projectile` | `Frost_Impact` | optional Debuff loop | Pinning Spear already uses Frost impact |
| **Heal / Mend** | `Heal_Cast` (sun circle) | — | — | `Heal_Aura` | Shape/motion; recolorable:false |
| **Aegis / shield** | `Aegis_Cast` | — | — | `Aegis_Shield` | Matches ArtStation magic-shield language |
| **Taunt roar** | `Taunt_Roar` | — | `Melee_Impact` | `Taunt_Aura` | Outward shock + ground ring |
| **Ground channel / cast circle** | `Magic circle fire call` / electro loop | — | — | — | NEW keys only if cast time ≥ 0.5s |
| **Raid / ultimate AOE** | — | — | `Raid_Explosion` (`Meteor hit`) | — | Cap concurrent ≤ 2–3 |

Full ability→key table already documented in `docs/vfx/SkillTree_VFX_Mapping.md` — **reuse keys; do not invent parallel names**.

---

## 2. Architecture law (do not violate)

1. **One key space:** `VFXManager.PlayKey` / `HovlVfxCatalog` only. No new enum greenfield for Hovl.  
2. **Registry-only motion VFX (owner 2026-07-12):** hero cast/impact/travel for **motion-driven** actions = `motion-castings.json` `vfxKey` (+ phase fields when present). Empty key = silent by design. **Filling empty rows IS the hero work.**  
3. **Towers are not motion-registry:** keep element→key tables in `TowerCombat` (or catalog `RepoProps`), but **add travel keys** and attach Hovl loop to the existing `ProjectilePool` bolt / follower (same pattern as `RangedAttackVFX.PlayHovlTravel` / `HovlVfxFollower`).  
4. **Pool:** Hovl keys only through VFXManager; soft-stop on travel end (already default). Caps: oneshots 40 / loops 20.  
5. **Presentation ≠ damage:** Hovl travel can be cosmetic if hitscan remains; prefer **visual travel duration ≈ flight time** for bolt-style towers.  
6. **No hand-edit of Hovl prefabs** under `Assets/Hovl Studio/` — recolor/scale at runtime; own overrides in `VfxManualPicks.json`.  
7. **ASCII / colorblind:** meaning by shape/motion/timing, not hue alone.  
8. **Do not reintroduce abilities.json as sole cast authority** while registry-only flag is ON — dual-write registry rows instead.

---

## 3. Implementation plan (ordered slices)

### Slice A — Catalog completeness (data + generator) — S

**Files:**
- `Assets/Editor/HovlVfxCatalogGenerator.cs` — ensure Map has full triplets including missing Flash rows (`Frost_Cast`, `Spear_Cast` if not present).  
- Run `Defenders/VFX/Generate Hovl VFX Catalog` → `HOVL_VFX_CATALOG_OK`.  
- Dual-copy any JSON only if new data files are added (not required for Map-only).

**Acceptance:**
- [ ] Every key in §1 has a catalog row with a loadable prefab ref.  
- [ ] Generator idempotent; marker printed.

### Slice B — Tower Flash → Travel → Hit — M ⭐ highest tower leverage

**Files (expected):**
- `Assets/_Modules/Village/Buildings/TowerCombat.cs` — extend `CastKeyFor` / add `TravelKeyFor` / keep `ImpactKeyFor`; on fire: PlayKey cast at muzzle; attach travel to projectile; on hit: PlayKey impact (orient if normal known — close WO-689 deferred item for towers).  
- `Assets/_Modules/Village/Buildings/DefenseTower.cs` / projectile init path if style-specific.  
- `Assets/_Modules/Village/Buildings/ProjectilePool.cs` / projectile component — optional `HovlVfxFollower` or `PlayKey(..., follow: projectileTransform)` on Get.  
- Mirror pattern: `RangedAttackVFX.PlayHovlTravel` (`Hero/RangedAttackVFX.cs`).

**Mapping (element / style → keys):**

| Condition | Cast | Travel | Impact |
|---|---|---|---|
| Flame | Fireball_Cast | Fireball_Projectile | Fireball_Impact |
| Aether / spell style | Arcane_Cast | Arcane_Projectile | Arcane_Impact |
| Ice | Frost_Cast (add) | Frost_Projectile | Frost_Impact |
| Lightning / electro | Thunderbolt_Cast | Thunderbolt_Projectile | Thunderbolt_Impact |
| bolt style / Physical | Spear_Cast (add) | Spear_Projectile | Spear_Impact |
| default | Spear_Cast | Spear_Projectile | Spear_Impact |

**Acceptance:**
- [ ] Archer/Mage/Fire towers show Hovl muzzle flash + travelling loop + impact (not mesh-only).  
- [ ] Soft-stop trail on impact (no mid-air pop).  
- [ ] FlowTrace: `[Flow:TowerVfx] cast=… travel=… impact=…` once per shot (throttle ok).  
- [ ] Fleet/headless: no NRE; pool caps not spammed; `COMPILE_GATE_OK`.  
- [ ] Mobile: concurrent tower fire does not exceed loop cap (tune pool sizes 8–12 for travel keys).

### Slice C — Sword & shield combo VFX via Motion Registry — M

**Files:**
- `Assets/StreamingAssets/Data/Canonical/motion-castings.json` **and** Resources dual-copy if runtime reads Resources.  
- Knight (and orc if shared) rows for: `attack0/1/2/3`, `heavy`, `skill1`, `skill2`, `block`, `parry` (if used).  
- Set `vfxKey`, `vfxDelay`, `attachBone` (e.g. `Weapon` / empty = chest), `playOneShot: true`, `manual: true`.  
- Optional: `ActionBundlePlayer` already fires `row.vfxKey` — verify path still live.  
- `PlayerAttackController` / `WeaponTrailController` — **keep trail**; do **not** double-fire Melee_Slash from both trail and registry unless delay-separated.

**Recommended knight rows (starting point — owner can retune in Motion Caster):**

| Keyword | Clip (existing SME) | vfxKey | vfxDelay (start) | Notes |
|---|---|---|---|---|
| attack1 | atk_slashright / spin | Melee_Slash | 0.18 | Light |
| attack2 | atk_slashleft | Melee_Slash | 0.18 | |
| attack3 | atk_stab | Melee_Impact | 0.22 | Thrust = impact spark |
| heavy | atk_slashdown | Melee_Slash | 0.22 | |
| skill1 | atk_jump (leap) | Cleave_Impact | 0.40 | On land frame |
| skill2 | atk_shieldcharge | Melee_Impact | 0.25 | Shield rush |
| block | shieldswipe / blockup | Aegis_Cast (short) or Parry_Flash | 0.05 | One-shot on raise |
| champions-combo / sweeping via abilities | — | Melee_Slash + Cleave_Impact via ability residual path | registry phase if available | |

**On hit connect (optional polish):** when `ResolveAttack` lands damage, `PlayKey(Melee_Impact, hitPos)` once per target (cap). Prefer this over every swing if slash alone is enough.

**Acceptance:**
- [ ] Basic combo chain shows Flower slash (or successor) timed to swing, not feet-only oneshot.  
- [ ] Shield bash / leap land has distinct heavier FX.  
- [ ] Empty `vfxKey` rows no longer silent for wired keywords.  
- [ ] Registry-only log line becomes “fired” not “silent by design” for those keywords.  
- [ ] Colorblind: slash shape visible on daylight ground.

### Slice D — Spellcasting (registry + residual) — M

**Files:**
- `motion-castings.json` knight/mage rows: `cast`, `castHeal`, skill keywords used by QWER.  
- Ensure `ActionBundleCatalog` phase fields (if present) for `vfxProjectile` / `vfxImpact` **or** keep ability residual path for HoT/DoT when registry has cast only.  
- `HeroAbilities` — no architecture rewrite; only data + verify `PlayResidualLoop` still runs for `VfxResidual` when allowed (if residual still reads abilities.json, keep that path — document which beat is registry vs ability).  
- Enemy ranged already uses EnemyTypeVfxSet Hovl keys — align sets to same Fireball/Arcane/Thunderbolt triplets.

**Minimum knight spell wires:**

| Ability / keyword | Cast vfxKey | Projectile | Impact | Residual |
|---|---|---|---|---|
| thunderbolt | Thunderbolt_Cast | Thunderbolt_Projectile | Thunderbolt_Impact | — |
| emberbrand-throw | Fireball_Cast | Fireball_Projectile | Fireball_Impact | Ember_Burn |
| arcane-bolt | Arcane_Cast | Arcane_Projectile | Arcane_Impact | — |
| mend / oathmend / second-wind | Heal_Cast | — | — | Heal_Aura |
| eternal-aegis | Aegis_Cast | — | — | Aegis_Shield |
| wardens-roar | Taunt_Roar | — | Melee_Impact | Taunt_Aura |
| dash | Dash_Blink | — | — | — |

**Acceptance:**
- [ ] Casting any wired ability shows Hovl cast (and travel+impact for ranged).  
- [ ] Heal/shield read by ring/bubble motion with bloom, not flat green/red.  
- [ ] No double-fire (registry + abilities.json cast both) — one authority per beat.  
- [ ] FlowTrace proves path: `owner bundle vfx '…' fired` or ability residual path once.

### Slice E — Impact orientation (close WO-689 deferred) — S

**Files:** tower hit, hero impact helper, enemy RootedCast.  
Utility: `Quaternion.FromToRotation(Vector3.up, normal)` when normal known; identity for pure ground.

**Acceptance:** wall/ground hits sit on surface in a quick editor/play probe.

### Slice F — Verification gates — S

1. `CompileGate` → `COMPILE_GATE_OK`  
2. Brace/NUL on every touched `.cs`  
3. DataRegression: no new reds beyond known baseline  
4. Optional: AutoPilot probe `AssertHovlPlayKey` — fire one tower + one melee keyword + one spell; expect FlowTrace PlayKey hits  
5. Owner felt: side-by-side with Hovl demo scenes  
   - `AAA Projectiles …/Demo projectiles simple spawning.unity`  
   - `AOE …/Demo AOE skills.unity`  
   - `Magic circles/Demo magic circles.unity`  
6. Marker: `HOVL_COMBAT_VFX_OK` printed by a tiny editor or headless check that critical keys resolve non-null prefabs

---

## 4. Files to touch (checklist)

| Path | Action |
|---|---|
| `Assets/Editor/HovlVfxCatalogGenerator.cs` | Add missing Flash keys; optional Siege/Parry |
| `Assets/Resources/VFX/HovlVfxCatalog.asset` | Regen only (script-authored) |
| `Assets/Editor/VfxManualPicks.json` | Owner overrides only |
| `Assets/_Modules/Village/Buildings/TowerCombat.cs` | Travel keys + attach |
| Projectile path (`ProjectilePool` / projectile component) | Follower / follow transform |
| `Assets/StreamingAssets/Data/Canonical/motion-castings.json` | vfxKey rows (manual:true) |
| Resources dual-copy of motion-castings if required by loader | Sync |
| `docs/vfx/SkillTree_VFX_Mapping.md` | Note registry-only + tower travel |
| `docs/HOVL_STUDIO_SME.md` or short RESULT | Same-breath canon if behavior changes |
| **Do NOT touch** | `Assets/Hovl Studio/**` prefab YAML · Village.unity · unrelated HUD |

---

## 5. Explicit non-goals

- Buying Toon Projectiles 2 / AAA Stylized (not owned).  
- Wiring Mirza Beig / Spells Pack as primary (URP risk; Hovl is canon).  
- Replacing WeaponTrailController entirely.  
- HS_ProjectileMover as gameplay mover.  
- Bloom retune (WO-689 done — only if felt still flat).  
- Wall builder / build HUD (separate).  
- Laser beam towers unless explicitly pulled into this WO as optional sub-slice.

---

## 6. Reference index (for implementer + felt A/B)

| Source | Use |
|---|---|
| `docs/HOVL_STUDIO_SME.md` | Architecture, bloom, tint, soft-stop, demo wiring |
| `docs/vfx/HovlStudio_Inventory.md` | Prefab shortlist §5 |
| `docs/vfx/SkillTree_VFX_Mapping.md` | Ability key table |
| `docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md` | vfxKey vocabulary |
| `docs/SME/SWORD_SHIELD_MOCAP_SME.md` | Combo clip map |
| Pack demos under `Assets/Hovl Studio/**/Demo*.unity` | Visual gold standard |
| [hovl.artstation.com](https://hovl.artstation.com/) | Portfolio language: sword slashes, magic shields, fire spells, lightning, archer arrows |
| YouTube demos (Hovl Studio Store): AAA projectiles, AOE spells | Motion timing / triplet |
| WO-689 RESULT | Bloom + hue tint already fixed; impact orient deferred → this WO |

---

## 7. Acceptance criteria (PO close)

1. **Towers:** Fire at least three tower types (bolt / spell / fire); each shows **muzzle + travel + impact** Hovl FX, readable on phone WebGL preview.  
2. **Sword/shield:** 3-hit combo + one shield skill show timed slash/impact; trail still works.  
3. **Spells:** Fireball/Thunderbolt/Arcane/Mend each show cast (+ travel/impact where ranged).  
4. **No regression:** registry-only law intact; no UITK; no Village↔HUD edge; pool soft-stop; bloom still on.  
5. **Gates green:** COMPILE_GATE_OK + DataRegression baseline + optional HOVL_COMBAT_VFX_OK.  
6. **Canon:** RESULT file `WorkOrders/WORK_ORDER_715_hovl_towers_melee_spell_vfx.RESULT.md` with proving FlowTrace lines.

---

## 8. Suggested commit slices (orchestrator)

1. `vfx(715A): catalog flash keys + regen HovlVfxCatalog`  
2. `vfx(715B): tower Hovl travel triplet`  
3. `vfx(715C): knight motion-castings melee vfxKeys`  
4. `vfx(715D): spell cast registry wires + residual verify`  
5. `vfx(715E): impact orientation utility`  
6. `docs(715): RESULT + mapping note`

Push only on owner felt OK.

---

## 9. Status / handoff

**READY TO IMPLEMENT** for CLI.  
**Number authority:** mint from `CLI_LANES_WO_NUMBERS.md` — this WO is **715**; bump next-free to **716** when claiming.  
**UI seat:** do not mint parallel “VFX-1” numbers; use this file.
