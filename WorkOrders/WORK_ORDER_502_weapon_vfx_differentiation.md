# WORK ORDER 502 - Weapon VFX Differentiation (rarity + theme tells)

**Status:** READY TO IMPLEMENT
**WO number check:** 502 is the next free file slot (495-501 used; the numbering authority
caps "next free WO = 430" but 495-501 are already on disk as a contiguous design block, so
502 is the real next file). Slot into Lane 9 (VFX/Audio) in the master backlog.
**Lane:** L9 (VFX/Audio) - no gameplay deps, parallel-safe (CLAUDE.md sec 9).
**Author:** creative/design pass (read-only survey + VFX design).
**Type:** EXISTING-system extension (reuse, do NOT greenfield).

---

## 0. The problem (owner ask)

All swords pull the same / similar mesh (EquipmentController shows a tinted-primitive fallback
because real KayKit weapon meshes aren't in Resources - MASTER_CATALOG flag #10). So the MESH
cannot carry rarity/identity. VFX is the only at-a-glance differentiator. Make a common starter
sword read instantly different from a legendary Elarion blade - by the SWING TRAIL, a blade
AURA/glow, an on-equip shimmer, and the HIT SPARK - all data-driven off the already-equipped
`WeaponDef`, all CHEAP (mobile URP), reusing the VFX spine that is ALREADY built.

---

## 1. REUSE MAP (build on these - do NOT rebuild)

Everything needed already exists. This WO wires a small data map into four existing seams.

| Seam | File | What it already gives us |
|---|---|---|
| Swing trail (code-built) | `Assets/_Modules/Village/Enemies/PlayerAttackController.cs` | `EnsureSwingTrail()` (lines ~540-590) builds a `TrailRenderer` with `_trailColor`, `_trailStartWidth`, `_trailTime`, URP-safe unlit material + alpha-fade gradient. Enabled at swing start, disabled after the hit window. THIS is the primary injection point - it already has color+width fields. |
| Equipped weapon (data) | `Assets/_Modules/Village/Hero/GearLoadout.cs` | `EquippedWeapon` (a `WeaponDef`, MAIN hand) + `OnGearChanged` event fires on EVERY equip change. The clean hook to retint the trail/aura when the weapon changes. |
| Weapon schema | `Assets/_Modules/Village/Hero/GearCatalog.cs` (`WeaponDef`) | Fields already present: `rarity` (common/uncommon/rare/epic/legendary), `damageType` (melee/ranged/magic), `makersMark` (Emberhand/Oathweld/Heartwood/Last-Pressing), `setId`. NO `element` field (see decision below). |
| Hit spark (impact VFX) | `Assets/_Modules/Village/Enemies/Enemy.cs` | line ~1685 `VFXManager.Play(VFXType.Impact_Physical, pos + up*0.3f)` plus element variants (`Impact_Flame/Ice/Aether`, lines ~1699-1701). Recolor/scale point for the on-hit spark. |
| VFX spine | `Assets/_Modules/Village/Vfx/VFXManager.cs` | Pooled, quality-gated (`VFXQuality` Low/Medium/High), procedural fallback, `VFXManager.Play(type,pos)` one-liner. Aura loops via `PlayAura(type, parent)` returning a `VFXHandle` (Stop() on unequip). |
| VFX types | `Assets/_Modules/Village/Vfx/VFXType.cs` + `VFXCatalog.cs` | enum + ScriptableObject prefab map. We add a FEW enum entries (see sec 3). |
| Combat feel | `Assets/_Modules/Village/Vfx/CombatFeedbackManager.cs` | hit-stop / combo / kill slo-mo. DO NOT touch - the spark scaling rides the existing `Enemy` impact call, not this manager. |
| Rarity curve | `WorkOrders/WORK_ORDER_500_weapon_armor_balance.md` sec 2 | The 5-band ladder (common 1.0 -> legendary 2.4, makersMark Elarion x1.25). Our VFX tiers map 1:1 onto these bands so VFX intensity tracks the power curve. |
| Existing trail asset (reuse, optional) | `Assets/Mirza Beig/Particle Systems/_Common/Scripts/TrailRenderers.cs` | Mirza Beig trail helper (already in project) - only if we ever want a particle-emitting prefab trail. NOT required for v1 (the code-built TrailRenderer is enough). |

**Net-new prefabs needed: effectively ZERO for v1.** The trail is procedural (code-built
TrailRenderer, recolored from data). The blade aura + on-equip shimmer reuse `VFXManager.PlayAura`
+ the procedural `ProceduralLoopFallback` already in VFXManager (recolored). The hit spark reuses
the existing `Impact_*` types (recolored/scaled). If art later supplies one shared
`WeaponAura_Tier` particle prefab it slots into the catalog with no code change - flagged as
FINESSE, not a blocker.

---

## 2. The design - four layered tells

A weapon's look is the SUM of two data axes resolved into one VFX profile:

1. **RARITY = the PRIMARY signal** (brightness/width/density escalate; color shifts white->gold).
2. **THEME = the FLAVOR signal** (hue tint + particle treatment from makersMark/element).

Resolved once per equip into a `WeaponVfxProfile { trailColor, trailWidthMul, trailAlpha,
glowColor, auraDensity, sparkColor, sparkScale, shimmerOnEquip }` and pushed to:

- **(A) Swing trail** - color + width + alpha from the profile (the loudest tell; every swing).
- **(B) Blade aura / glow** - a faint persistent loop on the weapon transform (rare+; the
  "this blade is special" idle read). Common/uncommon = none.
- **(C) On-equip shimmer** - a one-shot sparkle burst when the weapon is equipped (rare+),
  so swapping to a better blade FEELS like an upgrade.
- **(D) Hit spark** - the on-hit `Impact_*` VFX recolored to the profile + scaled by rarity.

### 2.1 Per-RARITY table (primary signal - maps 1:1 to WO-500 bands)

| Rarity | Trail color (base, theme tints it) | Trail width x | Trail alpha | Blade aura | On-equip shimmer | Hit-spark scale |
|---|---|---|---|---|---|---|
| common | cool steel `#BFD9FF` (faint) | 1.0 | 0.55 | none | none | 1.0 |
| uncommon | soft green-white `#CFF5D0` | 1.1 | 0.70 | none | none | 1.1 |
| rare | blue `#5AA8FF` | 1.25 | 0.85 | faint blue, density 6 | small sparkle | 1.25 |
| epic | violet `#B061FF` | 1.45 | 0.95 | violet, density 10 | medium sparkle | 1.5 |
| legendary | gold `#FFC83C` (Elarion) | 1.7 | 1.0 | warm gold + slow motes, density 14 | large radiant burst | 1.8 |

(Colors/intensities are STARTING values - owner felt-tunes, see sec 7 BONES vs FINESSE.)

The base trail color above is the rarity "spine." THEME then tints the hue without losing the
rarity read (rarity drives width/alpha/density/aura-presence; theme drives the hue family).

### 2.2 Per-THEME table (flavor signal - from makersMark, until a real element field exists)

makersMark is the existing themed-forge axis (4 marks already in weapons.json). Map each mark
to a hue treatment. For a rare+ weapon the THEME hue overrides the rarity spine color; width/
alpha/density still come from rarity. Common/uncommon (which carry no mark) keep the rarity spine.

| Theme (makersMark) | Element feel | Trail/aura hue | Spark color | Particle treatment |
|---|---|---|---|---|
| Emberhand | fire | warm orange `#FF7A1A` | orange | ember motes drifting up |
| Oathweld | holy/radiant | gold-white `#FFE6A0` | pale gold | fine radiant motes |
| Heartwood | nature/life | green `#6FE06A` | green | soft leaf-green wisps |
| Last-Pressing | aether/arcane | violet-cyan `#9B7BFF` | cyan-violet | arcane sparkle |
| (none) | - | use rarity spine color | rarity spine | minimal |

This keeps theme OPTIONAL and data-derived - no new field strictly required for v1.

### 2.3 Hit-spark scaling (reuse, recolor, scale - DO NOT build a new hit system)

The on-hit spark stays the existing `Enemy.cs` `VFXManager.Play(Impact_*, hitPos)` call. Pass the
equipped weapon's `sparkColor` + `sparkScale` so the impact tints/grows with the blade. Minimal
plumbing: a static `WeaponVfxProfile.Current` (set on equip) that `Enemy` reads when it fires the
impact, OR pass scale through the existing impact call. Prefer reading the profile statically (no
signature churn). Legendary hit = bigger, gold spark; common = small, steel spark.

---

## 3. Data schema addition

### 3.1 Where the map lives
New small data-only class `WeaponVfxProfile` + a static resolver `WeaponVfxMap` in a NEW file:
`Assets/_Modules/Village/Vfx/WeaponVfxMap.cs` (DeNelle.Village assembly - same as the trail +
VFXManager it serves, no cross-assembly issue).

`WeaponVfxMap.Resolve(WeaponDef w) -> WeaponVfxProfile`:
1. Look up the RARITY row (sec 2.1) -> base profile (width/alpha/density/aura/shimmer/sparkScale + spine color).
2. Look up the THEME (makersMark, sec 2.2); if present AND rarity >= rare, override the hue (trail/aura/spark color).
3. Return the merged profile. Pure function, no allocation per swing (cache the last-resolved by weapon id).

Keep the rarity/theme tables as `static readonly` dictionaries IN the .cs (data-as-code) for v1 -
no JSON round-trip needed, and it keeps the map next to the colors the owner tunes.

### 3.2 weapons.json - new field? (DECISION FOR OWNER - see sec 8)
NOT required for v1. The map derives theme from the existing `makersMark`. RECOMMENDED (optional,
later): add an explicit `"vfxElement"` string field to `WeaponDef` (fire/frost/holy/shadow/nature/
arcane/none) so a weapon's VFX theme is authored directly instead of inferred from the forge stamp -
useful once weapons want a theme that diverges from their mark. If added: it's an OPTIONAL field
(default empty -> fall back to makersMark mapping), no migration, no version bump risk beyond adding
the `vfxElement` parse in `WeaponDef`. Do NOT add it unless the owner says yes (sec 8).

### 3.3 VFXType enum additions (a few entries, in VFXType.cs)
- `WeaponAura_Rare`, `WeaponAura_Epic`, `WeaponAura_Legendary` (loop; procedural-fallback OK, recolored).
- `Weapon_EquipShimmer` (one-shot sparkle on equip; procedural-fallback OK).
Add matching arms to VFXManager's `ProceduralFallback` / `ProceduralLoopFallback` (recolor by the
passed profile color) so they render with NO new prefab. Hit spark reuses existing `Impact_*` - no
new type.

---

## 4. Wiring (the four hooks)

1. **Trail recolor (A):** in `PlayerAttackController`, resolve `WeaponVfxMap.Resolve(GearLoadout.
   EquippedWeapon)` and apply `trailColor`/`trailWidthMul`/`trailAlpha` to `_swingTrail` whenever
   `GearLoadout.OnGearChanged` fires (and once on first build). Update the existing
   `EnsureSwingTrail()` to read from a current profile instead of the fixed `_trailColor`. Keep the
   serialized field as the common/default fallback.
2. **Blade aura (B):** on `OnGearChanged`, if profile has an aura, `VFXManager.PlayAura(WeaponAura_*,
   weaponTransform)` and keep the `VFXHandle`; Stop() the old handle on change/unequip. Gate to
   `VFXQuality >= Medium` (the manager already quality-gates loops).
3. **On-equip shimmer (C):** on `OnGearChanged` to a rare+ weapon, `VFXManager.Play(Weapon_EquipShimmer,
   weaponTransform.position)` once.
4. **Hit spark (D):** set `WeaponVfxProfile.Current` on equip; `Enemy.cs` reads it where it already
   calls `VFXManager.Play(Impact_*, hitPos)` and applies `sparkColor`/`sparkScale`. (If recoloring
   the pooled impact is awkward, scale-only for v1 + recolor via the procedural-fallback color - flag
   as finesse.)

All hooks null-safe (`?.`) per CLAUDE.md sec 10 - no equipped weapon -> common defaults, never throws.

---

## 5. Files to edit

- `Assets/_Modules/Village/Vfx/WeaponVfxMap.cs` - **NEW** (profile struct + rarity/theme tables + Resolve + static Current).
- `Assets/_Modules/Village/Vfx/VFXType.cs` - add `WeaponAura_Rare/Epic/Legendary`, `Weapon_EquipShimmer`.
- `Assets/_Modules/Village/Vfx/VFXManager.cs` - add procedural-fallback arms for the new types (recolor by profile color); optional accept a color/scale on the impact path.
- `Assets/_Modules/Village/Enemies/PlayerAttackController.cs` - resolve + apply profile to `_swingTrail` on `OnGearChanged`; drive aura + shimmer.
- `Assets/_Modules/Village/Enemies/Enemy.cs` - apply `WeaponVfxProfile.Current` color/scale to the existing impact-spark Play call.
- (ONLY IF owner approves sec 8) `GearCatalog.cs` (`WeaponDef.vfxElement`) + `weapons.json` x3 copies (Data/Resources/StreamingAssets - triple-copy sync rule, MASTER_CATALOG data section).

**What NOT to touch:**
- `CombatFeedbackManager.cs` (hit-stop/combo/slo-mo - untouched).
- No `.unity` scene files (CLAUDE.md sec 3). No hand-edit of Village.unity.
- No EquipmentController mesh work (separate problem - the mesh fallback is MASTER_CATALOG flag #10, out of scope here).
- Do NOT greenfield a new VFX manager / hit system - reuse VFXManager + the Enemy impact call.
- Do NOT add per-weapon unique prefabs - one shared recolorable path.
- weapons.json `version` / pricing fields - untouched (that's WO-500's lane).

---

## 6. Acceptance criteria

- [ ] Equipping common -> uncommon -> rare -> epic -> legendary visibly escalates the SWING TRAIL (color shifts steel->green->blue->violet->gold; width + alpha grow) at a glance.
- [ ] rare+ weapons show a faint persistent BLADE AURA on the weapon; common/uncommon show none.
- [ ] Equipping a rare+ weapon fires a one-shot EQUIP SHIMMER.
- [ ] A makersMark themed weapon (Emberhand/Oathweld/Heartwood/Last-Pressing) tints the trail/aura/spark to its theme hue while keeping its rarity width/alpha.
- [ ] The HIT SPARK recolors + scales with the equipped weapon (legendary = bigger/gold; common = small/steel) using the EXISTING `Impact_*` path - no new hit system.
- [ ] All net-new VFX render via procedural fallback with ZERO new prefabs wired (catalog-less build still shows them).
- [ ] `VFXQuality.Low` suppresses auras/shimmer (manager quality gate) but keeps the cheap trail recolor - no perf regression on mobile.
- [ ] Null-safe: no equipped weapon -> common defaults, no exceptions in the F8 break-log.
- [ ] Brace-balance gate passes on every edited .cs (CLAUDE.md sec 1); `COMPILE_GATE_OK`.

---

## 7. BONES vs FINESSE (headless-gateable vs owner-felt)

**BONES - CLI can build + headless-verify (no felt judgment needed):**
- `WeaponVfxMap` resolver + rarity/theme tables (data + pure function - DataRegression can assert Resolve(rarity) returns the right band).
- The four wiring hooks compile + fire (FlowTrace.Step on each equip-resolve + trail-apply + aura-spawn + spark-tint -> headless play confirms the path runs, no nulls).
- Quality-gate behavior (Low suppresses loops) - assertable.
- VFXType enum + procedural fallback arms exist + render a non-null object (VFXManager's VerifyHasParticles already self-reports invisible effects).

**FINESSE - OWNER felt-tunes live (colors/intensity are FEEL, flag them - CLAUDE.md sec 12):**
- The exact trail COLORS per rarity + per theme (sec 2.1/2.2 are STARTING values).
- Trail width multipliers, alpha, aura particle density, shimmer scale, spark scale numbers.
- Whether the theme hue should fully override or blend with the rarity spine.
- Whether aura starts at rare or only epic+ (readability vs clutter call).
Expose ALL of these as serialized/inspector or a small tunables block so the owner adjusts without a recompile.

---

## 8. DECISION NEEDED FROM OWNER

1. **Add a `vfxElement` field to weapons.json?** Default = NO (derive theme from existing
   `makersMark`, ship v1 with zero data churn). Say YES only if you want to author a weapon's VFX
   theme independently of its forge stamp (e.g. a Heartwood-marked blade that should read as frost).
   It's an additive optional field, no migration - but it touches all 3 weapons.json copies, so it's
   your call.
2. **Aura threshold:** auras start at RARE (sec 2.1) - confirm, or push to EPIC+ if rare auras feel
   too busy with many weapons on screen.
3. **Theme override vs blend:** sec 2.2 has theme HUE override the rarity spine color for rare+.
   Confirm override (cleaner, more legible) vs a blend (subtler).
