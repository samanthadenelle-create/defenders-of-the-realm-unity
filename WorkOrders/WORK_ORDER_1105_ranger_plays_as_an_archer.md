# WORK ORDER 1105 — Sylas the Ranger plays like a swordsman: give the archer his bow, his target, and his icons

**Status:** READY TO IMPLEMENT (needs one owner ruling — see §5)
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1105 -> 1106 in the same edit
**Lane:** Hero combat input + targeting + action-bar presentation. ⚠ Touches `PlayerAttackController`
(shared with the WO-997 Focus hook and WO-1103 rewards) — coordinate, do not fork it.
**Provenance:** owner felt-test 2026-08-16, verbatim: *"I selected Sylas, the ranger... I would expect
the ranger to have ranged abilities. Instead, his skill set still only focused on short attacks that
would be comparable to a sword or a dagger. We have a skill, and we have an animation, which is for
him casting bows. So we need to make sure that his character or any ranged attack allows you to
select, maybe tap to select the enemy that you wanna target, give it a way to know that it's
targeted, kinda using that targeted spell VFX... and then it needs to allow us to use the bow. The
action bars seem to reflect something more generic, not really something that shows that it was a
bow and arrow attack."*

---

## 1. ⭐ THE KIT IS ALREADY RANGED — the defect is the PRIMARY ATTACK, not the abilities

Measured from `Assets/Resources/Data/Canonical/abilities.json`, `classes.ranger.abilities`:

| slot | id | name | range | effect |
|---|---|---|---|---|
| Q | `ranger.q` | Quick Shot | **15 m** | strike |
| W | `ranger.w` | Snare Trap | **12 m** | snare |
| E | `ranger.healing-shot` | Healing Shot | **15 m** | drainshot |
| R | `ranger.r` | Storm of Arrows | 6.5 m | aoe |

**Nothing in the authored kit is melee.** So the "short attacks comparable to a sword or dagger" she
felt are NOT the abilities — they are the **primary attack input**, which is class-agnostic:

`Assets/_Modules/Village/Enemies/PlayerAttackController.cs:47` —
`_attackRange = 3.2f`, an `OverlapSphere` sweep around the hero, used by **every class**.

⚠ **And the file documents the assumption that is false for her** (`:49-55`):
> *"Ranged classes (mage/ranger) never set reach, so they keep the fixed range — **their real attacks
> route through AbilityDef.Range, unchanged**."*

That assumption holds only if the player's main, spammable attack is an ABILITY. It is not: the
primary input drives the 3.2 m melee sweep, and the ranged kit sits behind Q/W/E/R as "skills". So
the archer's default verb is a sword swing. **This is the whole defect in one line.**

## 2. What already exists — build ON these, do not greenfield

- **Bow visual + orientation:** `HeroBowAttachment` (referenced by `EquipmentController`,
  `GearVisualApplier`); canon `ARCHITECTURE_PRINCIPLES §4` records the bow as the worked example of
  deriving grip/orientation from mesh bounds + asset name (`NormalizeInto`) — do not hand-dial it.
- **The bow CAST ANIMATION exists** (owner: *"we have an animation... for him casting bows"*). Find
  it and drive it from the ranged primary; do not author a new clip.
- **Target acquisition:** `HeroTargetIndicator` runs a 45 m acquire ring and publishes `LockedTarget`,
  which `HeroAbilities.ResolveStrikeLike` already consumes (gated by each ability's own reach).
- **The "what am I targeting" VFX she is describing SHIPPED TODAY:**
  `CastingTelegraphVfx.TryBeginTargetMarker` puts the owner-picked Hovl *Marker 2 Pointer Loop* on the
  cast target (unit-parented, or the blast-centre ground point for AoE), with an auto-destroy safety
  net. **Reuse that seam verbatim** for the ranged primary's target — same marker, same rules.
- ⚠ **`ff.lockon` is `defaultOn: false`** (`FeatureFlags.cs`, WO-512) — parked *"until felt-proven
  (mobile-nausea is the top risk)"*. That flag is about a lock-on CAMERA, not about target selection.
  Do not flip it as a side effect of this work; if the ranged primary needs it, that is a separate
  owner ruling.

## 3. What to build

**(a) A ranged PRIMARY attack for ranged classes.** The primary input must resolve through the
class's ranged verb (Quick Shot's 15 m strike path) instead of the 3.2 m sweep, driving the bow
animation and the existing projectile path (`LaunchProjectile` — damage lands on ARRIVAL, which is
already how ability strikes work, and is what makes an arrow read as an arrow).
⚠ **Do not delete or re-range the melee sweep** — the Knight depends on it, and WO-997's Focus
on-hit restore + WO-1103's reward path both hang off `ResolveAttack`. Branch by class capability,
keep both paths alive, and make sure the Focus restore still fires on a landed ARROW (per WO-997 the
restore is armed for the class BASIC and paid on hit-confirm — a ranged basic must satisfy it).

**(b) Tap-to-select a target, with visible confirmation.** Owner: *"tap to select the enemy that you
wanna target, give it a way to know that it's targeted."* Tap an enemy -> it becomes `LockedTarget`
-> the Marker 2 Pointer Loop rides it until it dies, leaves reach, or the player selects another.
Never colour-only. Must work with touch on the Seeker surface, not just mouse.

**(c) The action bar must read as a BOW.** Owner: *"the action bars seem to reflect something more
generic, not really something that shows that it was a bow and arrow attack."* The naming/iconography
is currently knight-flavoured at the source — `HeroCatalog.cs:142-153` authors *Shield Charge* etc.,
and `HudModelProducers.cs:65` already records the exact prior incident: **"The owner, playing a MAGE,
got Sword Heroic / Shield Charge"**. Give the ranger's faces his own names + bow/arrow icons, read
from the class's authored kit — never a hardcoded per-class table (that is the same hand-authored-vs-
derived defect class as `IsLoop`, `Hidden`, and the town that laid itself on its side).

## 4. Acceptance

- Playing Sylas, the primary attack **fires an arrow at range** with the bow animation; no sword-like
  swing is reachable as his default verb.
- Tapping an enemy marks it visibly with the owner-picked marker, and the arrow goes to THAT enemy.
- The action bar shows bow/arrow iconography and the ranger's own ability names — verified in a
  **device screenshot at 2670x1200**, not a batchmode capture (batchmode has no GameView; the
  resolution in a headless PNG filename is a label, not a layout).
- Knight is unaffected: melee reach, Emberbrand/on-hit procs and reward crediting unchanged.
- Ranger Focus still refunds on a landed basic (WO-997 rule: armed for the class basic, paid on hit).

## 5. ⚠ ONE OWNER RULING NEEDED

Is the ranged primary **auto-targeting** (fires at the nearest/locked foe in reach, mobile-friendly,
matches how the abilities already resolve) or **strictly tap-to-select first** (no target, no shot)?
Her words support tap-to-select; auto-target with tap to OVERRIDE is the mobile-standard middle and
is what the existing `LockedTarget`-else-`NearestHostile` chain already does. **Do not guess** — the
answer changes whether a mistap costs the player a shot in a live fight.

## 6. What NOT to touch

- `ff.lockon` (camera lock-on, parked on a nausea risk — separate ruling).
- The melee sweep's existence, reach math, or `EffectiveRange()` for the Knight.
- `VfxManualPicks.json` / owner VFX tags — reuse the marker that is already tagged, never substitute.
- `abilities.json` ranger ranges — the data is already correct; this is an input/presentation defect.
