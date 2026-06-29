# Weapon VFX Design — escalating per-tier flair on the 5 Knight swords (+ shields)

**Status:** READY TO FOLD IN (creative prep — no live code changed by this doc)
**Author:** SME pass, 2026-06-23
**Pivot fit:** V1 hero armor is **static** (combat-pivot north-star); the visual progression
the player buys/earns lives on the **weapon**. This spec escalates that flair across the 5
Knight swords and adds a light shield cue, **reusing the existing VFXManager + swing-trail
infrastructure** — no new VFX framework.

---

## 0. SME — the infrastructure we REUSE (cite file:line)

This is a wiring + data spec on top of three things that already ship:

| Existing system | What it gives us | File:line |
|---|---|---|
| **VFXManager** (singleton, object-pooled, quality-gated, procedural fallback) | `VFXManager.Play(VFXType, pos, rot)` — static, null-safe, pooled, mobile quality gate, auto audio bridge, **procedural AbilityVfxKit fallback when no prefab wired** | `Assets/_Modules/Village/Vfx/VFXManager.cs:155` (static `Play`), `:345` (`PlayOneshot` pool path), `:616` (`ProceduralFallback`) |
| **VFXType** enum + **VFXCatalog** | named effect vocabulary; designer wires a prefab per type or leaves null → procedural | `Assets/_Modules/Village/Vfx/VFXType.cs:21`; catalog lookup `VFXManager.cs:362` |
| **Swing TrailRenderer** (WO-219) — already code-built on the Knight | a `TrailRenderer` on the right-hand bone, lit each swing, serialized `_trailColor` / `_trailTime` / `_trailStartWidth` / `_trailLinger` | `Assets/_Modules/Village/Enemies/PlayerAttackController.cs:533` (`EnsureSwingTrail`), enabled at `:361`, color/material `:565-577` |
| **Hit resolution** (where the spark fires) | melee damage lands here on the impact frame | `PlayerAttackController.cs:430` (`ResolveAttack`), per-target loop `:457-491` |
| **Block / parry cue** (already fires VFX) | block-raise already plays `VFXType.Impact_ShockwaveRing` as a steel ward flash | `PlayerAttackController.cs:287` (in `UpdateBlock`) |
| **GearLoadout.OnGearChanged** | event fired after EVERY equip (auto + manual shop/equip) — the on-equip hook | `Assets/_Modules/Village/Hero/GearLoadout.cs:53`, raised `:175/411/455/488` |
| **GearVisualApplier** | re-attaches weapon visual on equip; the natural host transform for an on-equip glow | `Assets/_Modules/Village/Hero/GearVisualApplier.cs:41` |
| **SpellVfxFactory / AbilityVfxKit** | element→VFXType router + the procedural particle engine the fallback uses | `Assets/_Modules/Village/Vfx/SpellVfxFactory.cs:55`; `AbilityVfxKit` (procedural shapes) |

**Weapon ids (authoritative — same ids gear-stats + store prep use)** from
`Assets/Resources/Data/Canonical/weapons.json`:

- `knight_starter` — Squire's Blade — common — line 64
- `knight_iron` — Iron Longsword — uncommon — line 92
- `knight_oath` — Oathkeeper — rare — line 106
- `knight_dawn` — Dawnbreaker — epic — line 123
- `aegis_emberbrand` — Emberbrand, the Rekindled — **legendary** (5th) — line 212

Shields (only the starter is authored today; tiered shields are store-prep):
`knight_shield_starter` — Squire's Heater — line 78.

**Design rule we honour:** the prefab fields in the catalog can stay **null** at first —
every VFXType below renders through the **procedural AbilityVfxKit fallback** already in
`VFXManager.ProceduralFallback`, so this ships with **zero new art** and a real prefab can
be dropped into the catalog later with no code change.

> **UPDATE 2026-06-23 (owner directive):** we OWN two real VFX packs — map to ACTUAL
> prefabs, not procedural fallbacks. Section 0.5 below is the authoritative prefab map;
> the procedural fallbacks remain only as the graceful degrade path (catalog entry null →
> AbilityVfxKit). Sections 1–2 keep the design intent; section 0.5 supplies the exact prefab
> per tier + how it registers into VFXCatalog as a **data drop, not new code**.

---

## 0.5 OWNED PACK INVENTORY → real prefab map (authoritative)

### Packs (read their docs first)
- **Spells Pack** — `Assets/Spells Pack/` (doc: `Documentation/Documentation.txt`). URP-ready
  (import `Spells Pack/Packages/URP (2020.3.33+)` once; the project is already URP). Relevant
  prefab families, all pooled-friendly ParticleSystems:
  - `Particles/Prefabs/Buffs/Buff_<Element>.prefab` — **looping** under-character aura ring
    (Fire/Ice/Arcane/Light/Dark/Nature/Storm). → on-equip aura + legendary held loop.
  - `Particles/Prefabs/Shields/Shield_<Element>.prefab` — **looping** bubble/ward shimmer. →
    shield guard shimmer / block-hold.
  - `Particles/Prefabs/Projectiles/Explosion/Explosion_<Element>[_n].prefab` — **oneshot**
    burst. → on-hit spark + legendary equip burst.
  - `Particles/Prefabs/Projectiles/Casting/Casting_<Element>[_n].prefab` — **oneshot** charge
    flash. → faint on-equip edge glow (cheap).
  - `Particles/Prefabs/Spells/Spell_<Element>[_n].prefab` — **oneshot** AoE (heavier). → only
    the legendary combo-finisher shock.
- **Mirza Beig — Ultimate VFX** — `Assets/Mirza Beig/Particle Systems/Ultimate VFX/`
  (doc: `_DOCS/README - Ultimate VFX.txt`). NOTE: many demo prefabs ship DISABLED — the
  catalog must point at the prefab and `VFXManager` already calls `SetActive(true)` on
  acquire (`VFXManager.cs:386/439`), so that's handled. Relevant prefabs:
  - `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_weaponEffectElectricSwordTutorial.prefab` — a
    purpose-built **electric blade loop** (sword weapon-effect). → the legendary held blade
    loop (the standout for `aegis_emberbrand`). (`...SwordOriginal` / `...SwordPreTutorial`
    are denser variants.)
  - `Expansions/XP - SHOCKWAVES/Prefabs/Oneshot/pf_vfx-ult_xp-shockwaves_psys_oneshot_shockwaves.prefab`
    — flat expanding ring. → upgraded shield parry flash + legendary combo shockwave.
  - `Expansions/XP - ACTION/Prefabs/Oneshot/pf_vfx-ult_xp-action_psys_oneshot_explosion*.prefab`
    + `..._flashbang*.prefab` — punchy oneshot bursts/flashes. → heavier on-hit / equip bursts.

### How it registers (DATA DROP, not new code)
`VFXManager` already pools by `VFXType` and reads a `VFXCatalog` ScriptableObject that maps
`VFXType → {Prefab, PoolSize, MinQuality, LifetimeOverride}` (`VFXManager.cs:476-484`,
`InitialisePools`). **The entire mapping below is achieved by populating VFXCatalog entries
in the Inspector** — drag the prefab onto the matching `VFXType`, set `PoolSize` + `MinQuality`.
No VFXType enum change is required for tiers 1–4 (existing values already exist). Two NEW enum
values are recommended ONLY for the legendary so it reads distinctly; both fall back to an
existing procedural if left unwired:
- `Aura_BladeEmber` (legendary held loop) — until added, reuse existing `Aura_Flame`.
- `Equip_LegendaryBurst` (legendary equip flare) — until added, reuse `Impact_ExplosionAether`.

Adding those two enum values is the ONLY code touch, and it's optional; everything else is a
catalog wiring drop. Mobile budget set per-entry via `MinQuality` (see §4).

### Sword tier → exact prefab

| Tier / id | On-equip glow | On-swing trail/slash | On-hit spark | Legendary held loop |
|---|---|---|---|---|
| 1 `knight_starter` (common) | — none — | existing code TrailRenderer only (cool steel) | — none — | — |
| 2 `knight_iron` (uncommon) | `Casting/Casting_Light.prefab` (brief cool flash) via `Cast_KnightSlam` entry | code trail (brighter/longer) | — none — | — |
| 3 `knight_oath` (rare) | `Casting/Casting_Fire.prefab` (warm Emberhand glow) via `Cast_KnightSlam` | code trail (gold, wider) | `Explosion/Explosion_Light.prefab` via `Impact_ShardsBurst` | — |
| 4 `knight_dawn` (epic) | `Casting/Casting_Fire_2.prefab` | code trail (gold + faint bloom) | `Explosion/Explosion_Fire.prefab` via `Impact_Flame` | — |
| 5 `aegis_emberbrand` (legendary) | `Explosion/Explosion_Fire_3.prefab` via `Equip_LegendaryBurst` (or `Impact_ExplosionAether`) | code trail (steel→ember gradient) **+** held loop below | `Explosion/Explosion_Fire_2.prefab` via `Impact_Flame`; combo finisher → `XP - SHOCKWAVES/...oneshot_shockwaves.prefab` via `Impact_ShockwaveRing` | **`Mirza .../Prefabs/Loop/pf_vfx-ult_demo_psys_loop_weaponEffectElectricSwordTutorial.prefab`** via `Aura_BladeEmber` (fallback `Aura_Flame`), attached to blade, `PlayAura` handle, `Stop()` on unequip |

All `Casting_*` / `Explosion_*` are **oneshots** (auto-returned to pool by lifetime); the only
**loop** is the legendary blade effect (one at a time, handle-stopped). The swing arc stays the
existing code TrailRenderer for tiers 1–4 (zero prefab cost); for the legendary the Mirza loop
is parented to the same right-hand/blade transform so it travels WITH the swing.

### Shield tier → exact prefab

Shields are the off-hand (`GearLoadout.EquippedOffHand`). Only `knight_shield_starter` is
authored; tiers 2–5 are store-prep reusing the same prefabs at higher `PoolSize`/intensity.

| Shield tier | Block-raise flash (parry window opens) | Guard shimmer (block held) | Perfect-parry burst |
|---|---|---|---|
| 1 starter (`knight_shield_starter`) | existing `Impact_ShockwaveRing` (already firing at `PlayerAttackController.cs:287`) — wire its catalog entry to `Spells Pack/.../Shields/Shield_Arcane.prefab` (cool steel-blue ring) | — none — | `Shields/Shield_Light.prefab` (oneshot-style, brief) via the parry cue |
| 2 (store-prep) | `Shield_Light.prefab` | — | `Shield_Light.prefab` |
| 3 | `Shield_Light.prefab` | `Shields/Shield_Arcane.prefab` (faint held shimmer, loop) | `Shield_Arcane.prefab` |
| 4 | `Shield_Storm.prefab` | `Shield_Arcane.prefab` | `XP - SHOCKWAVES/...oneshot_shockwaves.prefab` |
| 5 | `Shield_Fire.prefab` (ember ward, matches Emberhand set) | `Shield_Fire.prefab` (loop, quality-gated) | `XP - SHOCKWAVES/...oneshot_shockwaves.prefab` |

The block flash uses the **already-wired** `VFXType.Impact_ShockwaveRing` trigger — the only
change is pointing that catalog entry at a `Shield_*` prefab and (for held shimmer tiers 3+)
adding a small `VFXType.Aura_ShieldGuard` loop entry (new optional enum; fallback procedural).

---

## 1. Per-tier sword VFX (escalating)

The escalation maps to the rarity ladder. Each tier ADDS to the one below it (cumulative),
so the player feels each upgrade. A small `WeaponVfxTier` lookup (keyed by weapon id) drives
three already-existing trigger points: **on-equip**, **on-swing (trail)**, **on-hit (spark)**.

| Tier | Weapon id | On-equip | On-swing trail | On-hit spark | "Aura" while held |
|---|---|---|---|---|---|
| 1 — common | `knight_starter` | none | **plain steel arc** — existing trail, cool-steel color, short `_trailTime` (current default `0.14`) | none (TakeDamage's central hit feedback only) | none |
| 2 — uncommon | `knight_iron` | **faint edge glow** — brief one-shot `Impact_Physical` flash at the blade on equip | steel arc, **slightly brighter/longer** (color alpha ↑, `_trailTime` ~0.18) | none | none |
| 3 — rare | `knight_oath` | edge glow (brighter) | **visible swing trail** — wider `_trailStartWidth` (~0.26), warm-gold tint (`makersMark` "Emberhand" → gold), `_trailTime` ~0.22 | **light hit-spark** — `VFXType.Impact_ShardsBurst` one-shot at hit point | none |
| 4 — epic | `knight_dawn` | edge glow + a soft pulse | gold trail, longest `_trailTime` (~0.26), faint additive bloom | **elemental hit-spark** — `VFXType.Impact_Flame` (Dawnbreaker reads dawn/fire) | none |
| 5 — legendary | `aegis_emberbrand` | **legendary equip burst** — `VFXType.Impact_ExplosionAether` (one-shot stored-aether flare) | gold-into-ember trail (two-stop gradient steel→ember), max width/time | **ember hit-spark** `Impact_Flame` + on a combo finisher `VFXType.Impact_ShockwaveRing` (the saga's "stored aether shock") | **legendary aura** — persistent low-emission ember loop on the blade via `VFXManager.PlayAura(VFXType.Aura_Flame, bladeTransform)`, handle stored, `Stop()` on unequip |

Notes:
- Tiers 1–4 reuse the **same TrailRenderer** that already exists — they only change its
  serialized fields (`_trailColor`, `_trailTime`, `_trailStartWidth`). No second trail.
- The hit-spark one-shots route through `VFXManager.Play(...)` which is **already pooled +
  quality-gated** — on `VFXQuality.Low` the manager skips loops/auras automatically, so the
  legendary aura self-disables on low-end mobile (see `VFXManager.cs:409` loop cap + quality
  gate `:419`).
- `Impact_ShardsBurst`, `Impact_Flame`, `Impact_ExplosionAether`, `Impact_ShockwaveRing`,
  `Aura_Flame` **all already exist** in `VFXType.cs`. Per §0.5 these now resolve to REAL pack
  prefabs via VFXCatalog (Spells Pack Explosion/Casting/Shield + the Mirza electric-sword
  loop); the procedural AbilityVfxKit stays only as the graceful degrade when an entry is null.

### Tint per maker's mark (free flavor reuse)
`weapons.json` already carries `makersMark` ("Emberhand"=fire/gold). The tier lookup can read
it to pick the trail/spark color so the flair matches the lore with no extra data:
Emberhand → warm gold/ember; default → cool steel.

---

## 2. Shield VFX (light — optional)

Shields are the off-hand (`GearLoadout.EquippedOffHand`). Only `knight_shield_starter` is
authored; treat tiers 2–5 as store-prep that reuse the same cues at rising intensity.

| Shield tier | On block-raise (parry window opens) | On block-impact (hit absorbed) |
|---|---|---|
| 1 — starter (`knight_shield_starter`) | the **existing** cool-steel ward flash already fires — `VFXType.Impact_ShockwaveRing` at `PlayerAttackController.cs:287`. Keep as-is. | brief `VFXType.Impact_Physical` spark at the shield face |
| 2–5 (store-prep) | same ShockwaveRing, **brighter + a faint guard shimmer** — a short additive ring scaled by tier | ShockwaveRing (bigger) on a **perfect parry** (`OnParrySuccess`, `PlayerAttackController.cs:310`) |

The block flash is **already wired and shipping**; the only addition is scaling its
intensity/color by shield tier. No new trigger point.

---

## 3. How each hooks into the EXISTING system (no new framework)

Three trigger points, all already present:

1. **On-equip glow / burst / aura** — subscribe to `GearLoadout.OnGearChanged`
   (`GearLoadout.cs:53`). On change, read `EquippedWeapon.id`, look up its tier, and:
   - one-shot: `VFXManager.Play(tier.EquipFlash, bladeWorldPos)` (tiers 2+).
   - legendary aura: `var h = VFXManager.Instance.PlayAura(tier.HeldAura, bladeTransform)`;
     store `h`; on the next `OnGearChanged` that isn't legendary, `h?.Stop()`.
   The blade transform is whatever `GearVisualApplier` attached to the right hand
   (`GearVisualApplier.cs:41`); when no mesh is attached, fall back to the right-hand bone
   used by the swing trail (`PlayerAttackController.EnsureSwingTrail`, `:537-548`).

2. **On-swing trail** — already built in `PlayerAttackController.EnsureSwingTrail`
   (`:533`) and lit at `StartAttack` (`:361`). Add a tiny `ApplyWeaponTrailTier(WeaponDef)`
   that sets `_trailColor` / `_trailTime` / `_trailStartWidth` from the tier when gear
   changes (call it from the same `OnGearChanged` subscriber). The trail rebuild already
   handles material/URP-safety (`:575`).

3. **On-hit spark** — in `ResolveAttack`'s per-target loop, after
   `damageable.TakeDamage(...)` (`PlayerAttackController.cs:486`), add one line:
   `VFXManager.Play(tier.HitSpark, hitPos);` (tiers 3+ only; tiers 1–2 stay clean). `hitPos`
   is already computed at `:477`. The legendary combo-finisher shock checks the existing
   `_comboIndex == ComboLength-1` and adds `VFXManager.Play(VFXType.Impact_ShockwaveRing, hitPos)`.

**Where the small new code goes (for CLI, when scheduled):**
- New tiny static lookup `WeaponVfxTier` (id → {EquipFlash, HeldAura, HitSpark, trailColor,
  trailTime, trailWidth}) — pure data, ~40 lines, in `Assets/_Modules/Village/Vfx/`.
- One subscriber method + the 3 call-site additions in `PlayerAttackController` (already the
  trail + hit owner). No change to VFXManager, VFXType (unless `Aura_BladeEmber` is wanted),
  or the gear model.

---

## 4. Reuse-vs-new table (keep it mobile-cheap)

| Element | EXISTS (reuse) | REAL PACK PREFAB (data drop) | NEW CODE |
|---|---|---|---|
| Pooling / quality gate / audio bridge | VFXManager (all of it) | — | — none — |
| Graceful degrade render | AbilityVfxKit via `ProceduralFallback` | — | — none — |
| Swing trail (T1–4) | TrailRenderer in PlayerAttackController (WO-219) | — (code trail, per-tier color/time/width values) | — none — |
| Edge glow (T2+ equip) | `Cast_KnightSlam` entry | Spells `Casting/Casting_Light` / `Casting_Fire[_2]` | catalog wire only |
| Hit-spark (T3+) | `Impact_ShardsBurst` / `Impact_Flame` | Spells `Explosion/Explosion_Light` / `Explosion_Fire[_2]` | catalog wire only |
| Legendary equip burst | `Impact_ExplosionAether` | Spells `Explosion/Explosion_Fire_3` | optional enum `Equip_LegendaryBurst` |
| Legendary held loop | `PlayAura` loop path | **Mirza** `Loop/...weaponEffectElectricSwordTutorial` | optional enum `Aura_BladeEmber` |
| Legendary combo shock | `Impact_ShockwaveRing` | **Mirza** `XP - SHOCKWAVES/...oneshot_shockwaves` | catalog wire only |
| Shield ward flash | `Impact_ShockwaveRing` (already firing) | Spells `Shields/Shield_Arcane` / `Shield_Light` / `Shield_Fire` | catalog wire only |
| Shield guard shimmer (T3+) | `PlayAura` loop path | Spells `Shields/Shield_Arcane` / `Shield_Fire` (loop) | optional enum `Aura_ShieldGuard` |

**Mobile-cheap guarantees (all inherited, nothing new to enforce):**
- Everything is **pooled** (oneshot cap 40, loop cap 20 — `VFXManager.cs:101-104`); set a
  small `PoolSize` (2–4) per catalog entry so the pack prefabs warm cheaply.
- **Quality-gated per entry**: set the loop/aura entries (legendary blade loop, shield guard
  shimmer) to `MinQuality = High` so they auto-skip on `VFXQuality.Low/Medium` (`:419`); set
  the Mirza explosion oneshots to `Medium`. Oneshot sparks/flashes stay cheap at any tier.
- Mirza demo prefabs may ship DISABLED — handled, VFXManager `SetActive(true)`s on acquire.
- Pick the **lighter pack variants** for mobile: the smaller `Casting_*`/`Explosion_*` and the
  `...SwordTutorial` loop (not the denser `...SwordOriginal`); strip any attached point-light
  on the prefab copy if overdraw/light-count is a concern.
- Low **overdraw**: prefer additive unlit, ≤12 particles per effect, no soft particles, no
  shadows — same budget the existing trail uses (2-vert corners, view-aligned, `:558-563`).
- **One** persistent loop max at a time (the legendary aura), handle-stopped on unequip — no
  loop leak (the manager's `_loopObjects` accounting at `:553` covers it).

---

## 5-line summary

1. V1 hero armor is static, so weapon flair carries the progression — escalate it across the
   5 swords (`knight_starter`→`knight_iron`→`knight_oath`→`knight_dawn`→`aegis_emberbrand`).
2. Reuse the shipping stack end-to-end: VFXManager (pooled, quality-gated, VFXCatalog) + the
   WO-219 swing TrailRenderer + the block ShockwaveRing — **no new framework**.
3. Real owned-pack prefabs are wired by VFXCatalog as a DATA DROP (§0.5): Spells Pack
   `Casting_*` (equip glow), `Explosion_*` (hit/equip spark), `Shields/Shield_*` (block flash +
   guard shimmer); Mirza `weaponEffectElectricSwordTutorial` loop (legendary held) + Shockwaves
   oneshot (combo/parry). Procedural AbilityVfxKit stays only as the null-entry degrade.
4. Three existing hooks: on-equip (`GearLoadout.OnGearChanged`), on-swing (the code trail in
   `PlayerAttackController`), on-hit (one `VFXManager.Play` after `TakeDamage`).
5. Mobile-cheap by inheritance — pooled, small per-entry `PoolSize`, `MinQuality=High` on the
   one legendary loop + shield shimmer (only loops, auto-skip on low). ONLY code touch is 3
   optional new enum values (`Equip_LegendaryBurst`, `Aura_BladeEmber`, `Aura_ShieldGuard`),
   each with an existing fallback; everything else is Inspector catalog wiring.
