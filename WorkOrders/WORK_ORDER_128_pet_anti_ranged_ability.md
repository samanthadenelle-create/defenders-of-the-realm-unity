# WORK ORDER 128 — The Cull: A Pet Ability That Answers the Backline

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** Medium-High — gives pets a role-counter verb (answers the ranged/caster threat), the missing depth between "pet hunts nearest" and the NORTH_STAR's role-weighted combat; pays off in a per-element VFX moment
**Scope:** Medium — one additive ability component in `DeNelle.Village`, one optional additive field on `PetData`, two small additive seams in `DeNelle.Core.Combat` (read enemy role + a displace verb). NO change to the Pet hunt loop, the WO-58 aura, or WO-119 harvest.
**Depends on:** WO-58 (pet combat aura — **must not break**), the existing `Pet` hunt loop + `IDamageable` seam, `EnemyBrain.Role` / `EnemyRole` (already in code). Soft-aligns with the NORTH_STAR "smart targeting" `FindBestTarget` direction.
**Soft-ties (do not block on):** WO-119 (pet auto-harvest — this ability is a **Defend-mode** verb; a Tending pet never uses it, so they cannot conflict). Future DEF-21 tactical `FindBestTarget` (this WO's role-scan is the same weighting idea, scoped to one pet ability).
**Canon source:** `docs/NORTH_STAR.md` "smart targeting" (*"healers first… then ranged/DPS (high threat, squishy)"*), `docs/enemy-codex.md` §2 (Hollow Caster / Tiefling Cultist = the ranged/caster tier that *"hangs back… hits towers / the Heart at distance"*), `docs/elemental-codex.md` §5 (Mirza Beig prefab + tint per element), `docs/narrative-bible.md` §5 (the three pets + tone).

---

## Vision

Right now a defending pet hunts the **nearest** hostile (`Pet.NearestHostile()` — pure proximity).
That is the right floor, but it means the most dangerous enemies in the codex — the **ranged
casters who hang back behind a melee screen and chip the Heart and towers from range** (Hollow
Caster, Tiefling Cultist; enemy-codex §2.1/§2.4) — are the *last* thing a pet reaches, because they
stand *behind* the tanks the pet meets first. The NORTH_STAR names this exact problem and its answer:

> "Smart targeting… focus-fire by role — healers first (deny sustain), then **ranged/DPS (high
> threat, squishy)**, tanks last." — `NORTH_STAR.md`

The owner asked for *"some ability pets have versus ranged, something tying into VFX."* This WO is
that ability: **the Cull** — a pet's signature, cooldowned **dash-to-the-backline** that ignores the
front rank, picks the highest-threat **ranged/caster** enemy on the field, leaps to it, and
**disrupts** it (a short burst + interrupt/displace) in a bright per-element VFX beat. It is the pet
answer to the squishy-but-deadly tier — the role-counter the bible's companions deserve, and the
first taste of the NORTH_STAR's role-weighted combat, scoped to one legible verb instead of a full
`FindBestTarget` rewrite.

This is **additive**. It does not touch the WO-58 aura, the WO-119 harvest, or the existing
nearest-target hunt — the Cull is an *occasional* punctuation on top of normal hunting, fired on a
long cooldown when a backline target exists.

---

## Reconciliation — what already exists (build-up, not rebuild)

I read the pet layer, the enemy role data, and the combat seam before writing this.

| Need | Exists? | Where / note |
|---|---|---|
| Pet hunt loop + nearest-target scan | **BUILT** | `Pet.Update()` (Defend only), `Pet.NearestHostile()` — `Physics.OverlapSphereNonAlloc` on `_enemyMask` → `IDamageable`. **The Cull reuses this same overlap + mask; it does not add a second scanner.** |
| Pet element + per-element hit colour | **BUILT** | `Pet._element` (`DamageElement`), `Pet.ElementColor(e)` already maps Flame `#ff7043` / Ice `#7dd3fc` / Aether `#b388ff`. **The Cull's VFX tint reuses this.** |
| Pet → enemy status verb | **BUILT** | `IDamageable.ApplyStatus(StatusEffect, seconds)` — `{Slow, Freeze, Burn}`. Pet already calls `ApplyStatus(Slow)` for the Ice Wolf. **The Cull's "disrupt" leans on this (Slow/Freeze) + one new verb (§3c).** |
| Enemy **role** data (the targeting key) | **BUILT — but Village-only** | `EnemyRole { Tank, Healer, DPS, Ranged, MiniBoss }` in `WaveEnemyGroup.cs` (DeNelle.Village); set on `EnemyBrain.Role`. **`IDamageable` (Core) does NOT expose role today** — a pet (DeNelle.Pets) cannot read it across the asmdef boundary. **This WO adds the minimal Core seam to read it (§3a). This is the one new cross-module seam.** |
| Pet kinematic movement (for the dash) | **BUILT** | `Pet.MoveToward` (eased accel/arrival). **The Cull reuses it for the leap — no `NavMeshAgent`.** |
| Pet hit VFX bridge | **BUILT** | `PetAttackVfxBridge.Strike(color, pos)`. The Cull adds a bigger one-shot burst keyed to the codex prefab table (§4). |
| WO-58 aura | **BUILT — DO NOT BREAK** | `AuraController` / `PetData.level{1,3,5}EmissionRate`. Unrelated to this WO; not touched. |
| WO-119 harvest (Tend mode) | **spec'd** | A Tending pet is out of Defend, so it never fires the Cull — **no conflict**; the Cull is gated to `PetMode.Defend` exactly like the hunt loop. |

**So the new work is: one `PetCullAbility` behaviour (Village) that role-scans for a backline target,
dashes via `MoveToward`, and disrupts via `ApplyStatus` + one new displace verb; ONE Core seam to read
enemy role across the asmdef line; ONE optional additive `PetData` field; and a VFX hook.** It is NOT a
new movement system, a new scanner, a `FindBestTarget` rewrite, or any aura/harvest change.

> **Why a Core seam is unavoidable:** `EnemyRole` lives in `DeNelle.Village`; `Pet`/abilities that
> reference it must do so through `DeNelle.Core` (CLAUDE.md §5 — Pets→Core only, never Village). The
> cleanest, smallest seam is a `Role` read on `IDamageable` (Core), populated by the Village `Enemy`
> from its `EnemyBrain.Role`. See §3a. **No `System.Reflection`** (CLAUDE.md §10).

---

## 1. The ability — the Cull (dash-to-the-backline + disrupt)

A defending pet, in addition to its normal nearest-target hunting, periodically performs **one
high-impact strike on the most dangerous backline enemy.** The whole point is that it **skips the
front rank** the player's other defenders are already chewing on and punishes the squishy ranged tier.

**Trigger (all must be true):**
- The pet is in `PetMode.Defend` (same gate as the hunt loop — a Tending/Idle/Fortify pet never Culls).
- The Cull cooldown is ready (default **9s**, tunable).
- A valid **ranged/caster-role** hostile exists within the pet's `_huntScanRadius` (the existing 60u scan).

**Effect (the strike):**
1. **Leap to the backline.** The pet dashes to the chosen target using the existing `Pet.MoveToward`
   (briefly boosted speed, ~1.4× for the leap, capped to arrival) — it bypasses nearer melee because
   it is *targeting by role, not distance.* No new movement system.
2. **Burst hit.** On arrival (within `_attackRange`) it lands a single amplified strike — default
   **2.0× the pet's normal `_attackDamage`** (tunable) — through the existing
   `IDamageable.TakeDamage(amount, element)` path, source-tinted green like all pet hits.
3. **Disrupt the cast.** It applies a short **interrupt/displace** so the ranged enemy stops shooting
   for a beat:
   - **Default (all pets):** `ApplyStatus(StatusEffect.Freeze, 0.8s)` — a brief hard interrupt (the
     caster can't fire while frozen). Tunable to `Slow` if Freeze reads too strong in playtest.
   - **Plus a small knockback/displace** away from the Heart (the new `Displace` verb, §3c) — shoves
     the ranged unit back out of its firing position, reinforcing "you got pulled out of the line."
     Displace is gentle (~1.5–2.5u) and never pushes an enemy through a wall (clamp; see §3c).
4. **VFX payoff.** A bright, element-tinted one-shot fires at the impact point (§4) — the visible
   reward the owner asked for. This is *the* moment the ability sells.

**The trade-off (state it, this is the design's spine):**
- **The Cull spends the pet's attention.** While leaping to the backline the pet is *not* defending
  its post/lane — for ~1s it commits to the dive. A player who relies on a single pet to hold a lane
  will see that lane briefly open while the pet culls. The reward (a silenced caster) must be worth
  the gap. This is the same "answer the threat or hold the line" tension the NORTH_STAR wants.
- **Long cooldown.** The Cull is a *punctuation*, not a spam. 9s default keeps it a decisive beat, not
  the pet's main rhythm — normal nearest-target hunting still does the bulk of the work between Culls.
- **It only matters when a backline exists.** No ranged/caster on the field → no Cull → the pet just
  hunts nearest as today. The ability is dormant against pure-melee waves (e.g. "The Grey March"),
  and lights up exactly when the codex's ranged tier shows ("Withering Whisper," "Cultist Strike").

---

## 2. Targeting — how it finds the backline (role-aware)

The Cull reuses `Pet`'s existing overlap scan, then **scores by role** instead of pure distance —
the NORTH_STAR `FindBestTarget` weighting, scoped to this one ability.

**Selection (within the existing `_huntScanRadius` overlap):**
1. Collect living `Hostile` `IDamageable`s (same as `NearestHostile()`).
2. Keep only those whose **role is `Ranged` or a caster-type** — read via the new
   `IDamageable.Role` seam (§3a). (Codex maps Hollow Caster = `Ranged`/caster, Tiefling Cultist =
   caster; treat `MiniBoss` casters as eligible too if their role reads caster — owner tunes the set.)
3. **Score** each candidate and dash to the best. Suggested weighting (mirrors NORTH_STAR
   *"healer > ranged > tank, weighted by distance + HP"* but scoped to ranged-counter):

   ```
   score = roleWeight(role)            // Ranged/Caster = high; everything else excluded for the Cull
         + (1 - hpFraction) * hpBias   // finish wounded casters first (deny the squishy)
         - distance * distancePenalty  // mild — the WHOLE point is it WILL reach a far backline target
   ```
   `distancePenalty` is **small** on purpose: unlike the hunt loop, the Cull is *allowed* to ignore
   nearer melee and reach the backline. Defaults: `roleWeight(Ranged)=10`, `hpBias=3`,
   `distancePenalty=0.05/u`. Tunable.
4. If no ranged/caster candidate exists, the Cull does **not** fire (cooldown does not start) — the
   pet keeps hunting nearest as normal.

> **Reuse, don't fork:** this scan calls the same `Physics.OverlapSphereNonAlloc` + `_enemyMask`
> pattern `Pet.NearestHostile()` uses (share the buffer or use a local one of equal size). Do **not**
> add a second always-on scanner; evaluate only when the cooldown is ready (so it's cheap).

---

## 3. Data + code model — DESIGN ONLY (illustrative; CLI writes the real code)

Assembly discipline (CLAUDE.md §5): the ability **behaviour lives in `DeNelle.Village`** (it must read
the Village enemy's role; same reason `PetContextualBehaviour` and WO-119's `PetHarvestAssignment` sit
in Village). The **role-read seam lives in `DeNelle.Core.Combat`** (so Pets/Village both see it). The
optional per-pet **data field lives on `PetData` (`DeNelle.Data`)**. **Village → Core/Pets only; Pets
never references Village.**

### 3a. `IDamageable.Role` — the minimal Core seam (the one new cross-module surface)

Add a read-only `EnemyRole` accessor to the Core combat contract so an ability can target by role
without referencing `DeNelle.Village`. The `EnemyRole` enum should **move to (or be mirrored in)
`DeNelle.Core.Combat`** so Core can name it (today it lives in `WaveEnemyGroup.cs` / Village). Prefer
**moving** the enum to Core and having `WaveEnemyGroup` reference it (one canonical enum), to avoid a
duplicate — CLI's call on move-vs-mirror, but **one source of truth** is the requirement.

```csharp
// DeNelle.Core.Combat — add to IDamageable (additive; non-combatants return a default):
namespace DeNelle.Core.Combat
{
    public enum EnemyRole { Tank = 0, Healer = 1, DPS = 2, Ranged = 3, MiniBoss = 4 } // moved from Village

    public partial interface IDamageable
    {
        /// <summary>Combat role of this target (Tank/Healer/DPS/Ranged/MiniBoss).
        /// Lets role-aware abilities (the Cull) target the backline without
        /// referencing DeNelle.Village. Buildings/Heart return DPS-equivalent or a
        /// neutral default — only Enemy carries a meaningful role.</summary>
        EnemyRole Role { get; }
    }
}
```

The Village `Enemy` implements it by returning its `EnemyBrain.Role` (it already holds a brain
reference, or can fetch it). **This is the only structural change to the combat seam.**

### 3b. `PetCullAbility` — the additive behaviour (Village) — the heart of this WO

One component on (or added at runtime to) a defending pet. It is the **only** new gameplay loop.

```csharp
using DeNelle.Core.Combat;   // IDamageable, EnemyRole, StatusEffect, DamageElement
using DeNelle.Pets;          // Pet, PetMode
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The Cull — a defending pet's role-aware anti-ranged ability. On a long
    /// cooldown, picks the highest-threat RANGED/caster hostile (NORTH_STAR smart
    /// targeting), dashes to it via Pet.MoveToward, lands an amplified strike, and
    /// disrupts it (Freeze interrupt + small Displace) with an element-tinted VFX.
    /// Fires ONLY in PetMode.Defend — a Tending (WO-119) / Idle / Fortify pet never
    /// Culls. Does NOT touch the WO-58 aura or the normal nearest-target hunt loop.
    /// Lives in Village because it reads the Village enemy's role; Pets never refs Village.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetCullAbility : MonoBehaviour
    {
        [Tooltip("Seconds between Culls. Long — this is a punctuation, not the pet's main rhythm.")]
        [SerializeField] private float _cooldown = 9f;

        [Tooltip("Damage multiplier on the Cull strike vs the pet's normal hit (2.0 = double).")]
        [SerializeField] private float _damageMultiplier = 2.0f;

        [Tooltip("Hard-interrupt duration applied to the culled caster (Freeze).")]
        [SerializeField] private float _interruptSeconds = 0.8f;

        [Tooltip("How far the strike shoves the ranged enemy back, away from the Heart (units).")]
        [SerializeField] private float _displaceDistance = 2.0f;

        [Tooltip("Speed multiplier on the leap to the backline target.")]
        [SerializeField] private float _leapSpeedMultiplier = 1.4f;

        [Header("Targeting weights (NORTH_STAR FindBestTarget, scoped to anti-ranged)")]
        [SerializeField] private float _hpBias = 3f;            // finish wounded casters first
        [SerializeField] private float _distancePenalty = 0.05f; // SMALL — the Cull is allowed to reach far

        private Pet _pet;
        private float _cdRemaining;
        private readonly Collider[] _overlap = new Collider[48];

        private void Awake() => _pet = GetComponent<Pet>();

        private void Update()
        {
            if (_pet == null || !_pet.IsAlive) return;
            if (_pet.Mode != PetMode.Defend) return;            // gate — same as the hunt loop
            _cdRemaining = Mathf.Max(0f, _cdRemaining - Time.deltaTime);
            if (_cdRemaining > 0f) return;

            IDamageable target = FindBacklineTarget();          // role-scored; null if no ranged/caster
            if (target == null) return;                         // dormant vs pure-melee waves

            // CLI: drive the leap + strike. Either a short coroutine (leap via Pet.MoveToward at
            // _leapSpeedMultiplier until within _pet attack range, then strike) or an immediate
            // strike-on-select if the dash reads better as a telegraphed pounce. Design accepts either;
            // the visible "it crossed the field to the caster" read is the requirement.
            CullStrike(target);
            _cdRemaining = _cooldown;
        }

        // Reuses Pet's overlap/mask pattern; scores by role (NORTH_STAR), not pure distance.
        private IDamageable FindBacklineTarget() { /* OverlapSphereNonAlloc; keep Ranged/caster role;
                                                      score = roleWeight + (1-hpFrac)*_hpBias
                                                              - dist*_distancePenalty; return best */ return null; }

        private void CullStrike(IDamageable foe)
        {
            // amplified hit through the existing seam, tinted green like all pet hits
            // foe.TakeDamage(_pet.NormalDamage * _damageMultiplier, _pet.Element);
            foe.ApplyStatus(StatusEffect.Freeze, _interruptSeconds);          // interrupt the cast
            (foe as IDisplaceable)?.Displace(/* away-from-Heart dir */ Vector3.zero, _displaceDistance); // §3c
            // VFX payoff — element-tinted burst at foe.WorldPosition (see §4)
        }
    }
}
```

> **Pet accessors:** the Cull needs the pet's normal damage + element to amplify the strike. `Pet`
> exposes `Element`-equivalent via its hit path today but the fields (`_attackDamage`, `_element`) are
> private. Add **read-only properties** (`public float NormalDamage`, `public DamageElement Element`)
> to `Pet.cs` — these are trivial additive getters, the only Pets-assembly edit, and they touch
> **none** of the hunt/aura logic. (If CLI prefers zero Pets edits, the Cull can carry its own
> serialized damage/element mirror — design-acceptable, but the getter is cleaner.)

### 3c. `IDisplaceable` — the new "shove" verb (Core, optional but recommended)

`IDamageable` has no knockback/displace verb today (only `TakeDamage` + `ApplyStatus`). The Cull's
"pull the caster out of position" needs one. Add a tiny optional companion interface in Core (mirrors
the existing `IDamageTintable` optional-companion pattern), so non-displaceable targets simply don't
implement it:

```csharp
namespace DeNelle.Core.Combat
{
    /// <summary>Optional companion to IDamageable: lets an ability shove a target a
    /// short distance (the Cull's anti-ranged knockback). Implemented by Enemy;
    /// the implementor clamps so it never pushes through a wall / out of the world.</summary>
    public interface IDisplaceable
    {
        void Displace(Vector3 direction, float distance);
    }
}
```

If the owner prefers **no new verb in v1**, drop `Displace` and ship the Cull as **burst + Freeze
interrupt only** — that already delivers the anti-ranged fantasy. Displace is the *nice-to-have* that
makes the backline visibly scatter. **Owner's call; the Freeze-interrupt is the non-negotiable core.**

### 3d. Optional per-pet flavor — ONE additive field on `PetData` (keep v1 uniform)

Per the brief, v1 stays simple/uniform (every pet Culls the same way — same cooldown, same disrupt).
The element already differentiates the **VFX tint** for free (§4: Aether violet / Flame ember / Ice
frost). If the owner later wants a *tiny* mechanical differentiator, add **one optional additive
field** to the existing `PetData` SO — do **not** add a new SO, and do **not** touch the WO-58 aura
fields or WO-119's `harvestBoostMultiplier`:

```csharp
// Assets/Data/PetData.cs — DeNelle.Data — ADD (optional, v1 can leave at default):
[Header("Cull (WO-128) — optional per-pet anti-ranged flavor")]
[Tooltip("Per-pet override of the Cull's disrupt. 0 = use the component default (uniform v1). " +
         "Later flavor idea: Ice Wolf Freeze, Flame Pup Burn, Aether Sprite longer interrupt.")]
public StatusEffect cullDisruptOverride;       // honored only if a per-pet flag opts in
public bool         useCullDisruptOverride = false;
```

Suggested later (owner's call, NOT v1): **Ice Wolf** = Freeze (its frost identity, codex Ice), **Flame
Pup** = Freeze→swap to Burn-on-arrival (ember), **Aether Sprite** = longer interrupt (the Light pins
the caster). Purely flavor — never enough to break the uniform trade-off. **Shipping default: uniform,
field left at its default, element drives only the VFX tint.**

---

## 4. VFX — the payoff (DESIGN-only; per element, from the elemental codex)

The Cull's strike fires a bright one-shot at the impact point, tinted to the pet's element. Reuse the
existing `PetAttackVfxBridge.Strike(color, pos)` for the small core spark, and add a larger
element-keyed burst from the codex's Mirza Beig table (`elemental-codex.md` §5). All paths relative to
`Assets/Mirza Beig/Particle Systems/Ultimate VFX/`.

| Pet | Element | Tint (codex §1) | Cull-impact one-shot (Mirza Beig) | Read |
|---|---|---|---|---|
| **Aether Sprite** | Aether | Violet `#9B6FFF` (start `#C8A8FF`) | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Rings/pf_vfx-ult_xp-ckit_psys_oneshot_hitRing2-solid.prefab` (violet) + `…/pf_vfx-ult_xp-ckit_psys_oneshot_distortedShockwave-light.prefab` | An arcane ring snaps shut on the caster — "the Light pins it" |
| **Flame Pup** | Flame | Ember `#FF4400` → `#FF9900` | `Expansions/XP - ACTION/Prefabs/Oneshot/pf_vfx-ult_xp-action_psys_oneshot_explosion2.prefab` (deep-red tint, small scale) | A burst of hearth-fire on the backline — "the Pup bites the cold" |
| **Ice Wolf** | Ice | Frost `#80CCFF` / white | `Expansions/XP - STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` (0.4s burst, Stop=Destroy) + `…/distortedShockwave-light` (frost tint) | A frost-snap freezes the caster mid-cast — pairs with the Freeze interrupt |

- **Reuse the existing tint map.** `Pet.ElementColor()` already returns the right per-element colour;
  feed it to `PetAttackVfxBridge.Strike()` for the small spark, and tint the chosen one-shot's Start
  Color / Color-over-Lifetime to the codex values above. **Do NOT author new particle systems** — tint
  existing Mirza Beig prefabs (codex §1 "Color intent for particle artists").
- **One-shot lifetime ~0.5–0.8s**, Stop Action: Destroy (mobile-safe, pool-friendly; matches the
  codex's other Oneshot usages). Play through `VFXManager.Instance?.Play(...)` if a `VFXType` entry is
  added, or via the existing bridge — CLI's call, **null-guarded** (`?.`) either way.
- **Audio (optional):** a short element-keyed sfx on the Cull via `CoreServices.Audio?.PlaySfx(...)`
  with `?.` — only if a fitting `SfxId` exists; do not invent a mixer route here.

---

## 5. Ties to neighbouring systems (do NOT duplicate or break their state)

- **WO-58 (pet combat aura) — DO NOT TOUCH.** The Cull is a strike ability, not an aura. No edit to
  `AuraController`, `PetData.level{1,3,5}EmissionRate`, `enableOrbitSparksAtL5`, or `PetAuraVFX`.
- **WO-119 (pet auto-harvest) — additive, non-conflicting.** The Cull is gated to `PetMode.Defend`
  exactly like the hunt loop; a **Tending** pet (WO-119) is out of Defend, so it never Culls. The
  optional `PetData` field added here (`cullDisruptOverride`) is a **separate block** from WO-119's
  `harvestBoostMultiplier` — neither touches the other.
- **The Pet hunt loop — unchanged.** `Pet.Update()` / `Pet.NearestHostile()` / `Pet.MoveToward` are
  **reused, not rewritten.** The Cull is a *second, occasional* behaviour layered beside the hunt; it
  reads the same mask/scan and the same movement helper. No change to nearest-target hunting.
- **`EnemyBrain.Role` / `EnemyRole`** — **read** the role via the new Core `IDamageable.Role` seam.
  Do not re-derive role from stats. The enum should have **one canonical home** (move to Core, §3a) —
  do not leave a duplicated enum in two assemblies.
- **NORTH_STAR `FindBestTarget` (future DEF-21)** — the Cull's role-scored selection is the *same
  weighting idea* scoped to one ability. When the full `FindBestTarget` lands, the Cull's scorer can
  fold into it; for now it is self-contained. Flag this so they don't get duplicated long-term.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Pets/PetCullAbility.cs` | **Create** — the anti-ranged ability: cooldown, role-scored backline scan, leap via `Pet.MoveToward`, amplified strike + Freeze interrupt + optional Displace, VFX hook (lives in Village beside `PetContextualBehaviour`) |
| `Assets/_Modules/Core/Combat/IDamageable.cs` | **Edit (additive)** — add `EnemyRole Role { get; }` to `IDamageable`; move the `EnemyRole` enum here as the canonical source; add optional `IDisplaceable` companion (§3a, §3c) |
| `Assets/_Modules/Village/Waves/WaveEnemyGroup.cs` | **Edit (if enum moved)** — reference the Core `EnemyRole` instead of declaring its own (one source of truth) |
| `Assets/_Modules/Village/Enemies/EnemyDamageable.cs` (the `Enemy` `IDamageable` impl) | **Edit (additive)** — implement `Role` (return `EnemyBrain.Role`) and, if shipping Displace, `IDisplaceable.Displace` (clamp so it never shoves through a wall / out of world) |
| `Assets/_Modules/Pets/Pet.cs` | **Edit (additive getters only)** — add `public float NormalDamage` + `public DamageElement Element` read-only properties so the Cull can amplify/tint; **do NOT touch the hunt loop, aura, or movement** |
| `Assets/Data/PetData.cs` | **Edit (optional)** — add the additive `cullDisruptOverride` block (default off / uniform v1); **do NOT touch WO-58 aura fields or WO-119 `harvestBoostMultiplier`** |
| `Assets/_Modules/Village/Vfx/VFXType.cs` / `VFXManager.cs` | **Edit (optional)** — add a `Pet_Cull` VFXType + catalog entry if routing the burst through VFXManager (else use `PetAttackVfxBridge`); tint per codex §4 |

**Assembly discipline (CLAUDE.md §5):** `PetCullAbility` lives in `DeNelle.Village` (it reads the
Village enemy's role — same reason `PetContextualBehaviour` / WO-119's `PetHarvestAssignment` are in
Village). The role-read seam + `IDisplaceable` live in `DeNelle.Core.Combat`. The optional data field
is on `PetData` (`DeNelle.Data`). **Village → Core/Pets only; Pets never references Village.** All
HUD/Audio/VFX cross-module calls use `?.`. **No new `System.Reflection`** (CLAUDE.md §10). UI (if any
telegraph) is **code-built** (no UXML — PIPELINE_STATE.md §8). Run the brace-balance gate on every
`.cs` touched. Combat/AI is the code-only lane (CLAUDE.md §9) — no scene files, no bake.

---

## Acceptance Criteria

- [ ] A defending pet periodically performs **the Cull**: on a long cooldown (default ~9s), it selects the highest-threat **ranged/caster-role** hostile within its scan radius and commits a single amplified strike — **bypassing nearer melee** because it targets by role, not distance
- [ ] Targeting reads enemy **role** via the new `IDamageable.Role` Core seam (populated from `EnemyBrain.Role`) — the pet (DeNelle.Pets/Village ability) never references the concrete Village enemy across the asmdef line, and `EnemyRole` has **one canonical home** (no duplicate enum)
- [ ] The selection is **role-scored** (NORTH_STAR weighting: ranged/caster prioritized, wounded-first via HP bias, only a small distance penalty so the Cull reaches the backline) — not pure-nearest
- [ ] The strike **disrupts** the caster: amplified damage (default 2×) through `TakeDamage` + a short **Freeze interrupt** (default 0.8s) so it stops casting; optional **Displace** shoves it back (clamped — never through a wall) if shipped
- [ ] The Cull fires **only in `PetMode.Defend`** — a Tending (WO-119) / Idle / Fortify pet never Culls (verified: same gate as the hunt loop), so it cannot conflict with WO-119
- [ ] Against a **pure-melee wave** (no ranged/caster present) the Cull stays **dormant** — cooldown does not start, the pet hunts nearest exactly as today
- [ ] A bright, **element-tinted VFX one-shot** fires on the Cull impact, per the codex table: Aether Sprite = violet, Flame Pup = ember, Ice Wolf = frost — reusing `Pet.ElementColor()` + tinted Mirza Beig prefabs (no new particle systems authored)
- [ ] **v1 is uniform** across the three pets (same cooldown/disrupt); per-pet mechanical flavor is the optional additive `PetData.cullDisruptOverride`, left off by default — element drives only the VFX tint
- [ ] WO-58 pet combat **aura is unchanged** — no aura field, `AuraController`, or `PetAuraVFX` edited
- [ ] The Pet **hunt loop / nearest-target scan / movement are reused, not rewritten** — `Pet.cs` gains only additive read-only getters; no `NavMeshAgent` added; the leap uses `Pet.MoveToward`
- [ ] Brace balance passes on every `.cs` touched; cross-module HUD/Audio/VFX calls use `?.`; no `System.Reflection`; Village → Core/Pets only; Pets does not reference Village

---

## Do NOT touch

- **Do NOT break or modify the WO-58 pet combat aura** — no edits to `AuraController`,
  `PetData.level{1,3,5}EmissionRate`, `enableOrbitSparksAtL5`, or `PetAuraVFX`. The Cull is a strike,
  not an aura.
- **Do NOT conflict with WO-119 pet auto-harvest** — the Cull is `PetMode.Defend`-only; a Tending pet
  never fires it. The `PetData` field added here is a **separate block** from WO-119's
  `harvestBoostMultiplier`; do not touch WO-119's field, registry, or Tend mode.
- **Do NOT rewrite the Pet hunt loop or add a second always-on scanner** — reuse
  `Pet.NearestHostile()`'s overlap/mask pattern, evaluate the Cull only when the cooldown is ready, and
  reuse `Pet.MoveToward` for the leap. **No `NavMeshAgent` on the pet.**
- **Do NOT duplicate the `EnemyRole` enum** — give it one canonical home (move to `DeNelle.Core.Combat`)
  and have Village reference it. Read role through the `IDamageable.Role` seam, never by re-deriving
  from stats.
- **Do NOT reference `DeNelle.Village` from `DeNelle.Pets`** — the ability lives in Village (it reads
  the enemy's role); the only Pets-assembly edits are the additive read-only `NormalDamage` / `Element`
  getters, and even those are avoidable (§3b).
- **Do NOT author new particle systems** — tint existing Mirza Beig prefabs per the elemental codex
  (§4 / codex §1). VFX is DESIGN-only here; CLI wires the prefab swap/tint.
- **Do NOT make the Cull punishing or spammy** — it is a long-cooldown punctuation with a real
  trade-off (the pet leaves its lane for the dive), telegraphed and recoverable. No instakill, no
  permanent CC, no chaining.
- **Do NOT introduce `System.Reflection`** in these scripts (CLAUDE.md §10 — even though the legacy
  `PetAttackVfxBridge.Strike` uses reflection internally; do not add new reflection).
- **Do NOT hand-edit any `.unity` scene file, and do NOT fire any bake/batchmode** — this is the
  code-only Combat/AI lane (CLAUDE.md §9).
- Do not touch ATB, WalletService, monetization, or clan code.

---

🤖 Spec'd by the design lane (UI). Grounded against the live code (`Pet.cs` hunt loop +
`NearestHostile` + `ElementColor` + `MoveToward`; `IDamageable` carries Faction/Hp/`ApplyStatus`
{Slow,Freeze,Burn} but **no role and no displace** today; `EnemyRole {Tank,Healer,DPS,Ranged,MiniBoss}`
lives in `WaveEnemyGroup.cs` and is set on `EnemyBrain.Role` — Village-only, hence the minimal Core
seam), `NORTH_STAR.md` smart-targeting (*healers > ranged/DPS, weighted FindBestTarget*),
`enemy-codex.md` §2 (Hollow Caster / Tiefling Cultist = the backline tier this answers),
`elemental-codex.md` §1/§5 (per-element tint + Mirza Beig prefab table), `narrative-bible.md` §5 (the
three pets), and reconciled non-conflicting with WO-58 (aura — untouched) and WO-119 (harvest —
Defend-gated, separate `PetData` block). Markdown work order only — no `.cs` touched, no bake fired.
