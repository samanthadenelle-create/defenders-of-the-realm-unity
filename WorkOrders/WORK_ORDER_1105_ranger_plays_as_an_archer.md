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

## 4b. ⭐ OWNER RULINGS 2026-08-16 — all four, verbatim-sourced

**R1 — AUTO-TARGET, WITH TAP TO OVERRIDE.** *"I agree that the auto target's a good idea. The user
should have the ability, though, to override — say they automatically select the first one, which
maybe is a tank, and they're trying to take care of a major DPS or a healer. They should have the
ability to override that. So on tap would override."*
=> Auto-acquire the default target; a tap on any valid enemy REBINDS the lock to that enemy and the
marker follows it. The override must survive until that target dies, leaves range, or another tap
moves it — never silently snap back to the auto pick mid-fight (that is the failure her example
describes). This closes §5 below.

**R2 — RANGE MUST BE LEGIBLE.** *"either we add a distance ring for archer range, or only after we
get within range does it auto target."*
=> Two acceptable shapes, owner's choice at implementation; the SECOND is cheaper and needs no new
art: auto-target engages ONLY once the foe is inside the ability's authored range (Quick Shot 15 m),
so "it locked on" IS the range feedback. If the ring is chosen instead, derive its radius from
`AbilityDef.Range` — never a hardcoded metre value (the WO-1035 units bug is the cautionary case).

**R3 — RANGED ATTACKS NEED A COOLDOWN, AND THE RANGER NEEDS AN OFFHAND DAGGER.** *"being that we're
talking about a ranged attack, we need to make sure that there's some kind of a cooldown. I think
the ranger should have an offhand weapon of maybe a dagger because if they do need to spam attacks
and they can't get their ranged archery attacks to go fast enough, they do need something for a
melee attack like a dagger offhand."*
=> The bow primary carries a real cooldown (an archer is not a click-spam weapon). The answer to
"what do I do while it is cooling" is a DAGGER in the offhand — so the melee sweep is not deleted for
the ranger, it becomes the OFFHAND verb. ⚠ This also resolves the §3(a) worry about breaking the
melee path: both verbs stay live for the ranger, bow primary + dagger offhand, and WO-997's Focus
on-hit restore must be decided for each (recommend: the class BASIC is the bow, so Focus rides the
arrow; the dagger is the gap-filler and does not refund).

**R4 — ⭐ THE BOW GRIP RULE (generalises to every weapon, and explains why bows look wrong).**
*"the bow and arrow, as we've discussed many times, when you're looking at the mesh, the longest
piece is gonna be the y axis. You find the straight edge of the y axis, and you go down halfway. And
that's gonna meet on the edge of a curve. The edge of the curve or the ninety degree from the
midpoint is where the hand is gonna hold with the y axis up and down longest piece."*

Derivation, in order:
1. Orient so the **longest measured axis is vertical (+Y)** — `WeaponBoundsOrient` already does this
   (`longest -> +Y, narrowest -> +X`), and already scales by the longest MEASURED axis rather than
   blindly by Y (the 2026-07-06 shield RCA).
2. Take the **straight edge** running along that axis (a bow's string side is straight; the limb/
   riser side curves) and find its **midpoint** — halfway down the Y span.
3. Cast **perpendicular (90 degrees) from that midpoint** toward the curved edge. Where it meets the
   curve is the **grip**, and that point seats in the hand.

⭐ **R4-CORRECTION (owner, 2026-08-16, SUPERSEDES how step 3 was first implemented).** Verbatim:
*"You're seating the bow on the correct axis in the right spot. However, you're doing it on the
perpendicular from the midpoint of the y axis. You wanna follow that perpendicular from the y axis
over to the rounded hilt. The round part of the bow is where the grip is. That's where you handle.
Still, once again, y is up down is the longest percentage or longest piece of the mesh. and the most
narrow is gonna be the x. The z is gonna be the depth, the rounded part of the bow that raises out.
You want the mid of the rounded or the perpendicular from the longest side, the y axis, where it
intersects with the rounded edge."*

- **CONFIRMED CORRECT and not to be changed:** the axis assignment (Y = longest/vertical, X =
  narrowest, Z = depth where the bow bulges out) and the **mid-Y start point**.
- **WRONG, and now fixed:** the TERMINATION. Step 3's "where it meets the curve" was implemented as
  the FIRST surface the perpendicular encounters behind the string. It must instead run all the way
  to the **ROUNDED EDGE — the Z-EXTREME of the mid-Y cross-section on the curved side**, i.e. the
  apex of the riser's bulge. Implemented as a MAX over the whole mid-Y band (not a ray sample), so a
  sparse mesh cannot miss it. `WeaponBoundsOrient.TryDeriveBowGrip`; pinned by
  `RangedPrimaryRegression` case `bow-grip-apex` against a synthetic bow whose apex is known in
  closed form (the two answers are 0.30 m apart on a 1 m bow, so the old rule cannot pass it).

⚠ **WHY THIS IS A FIX, NOT A RESTATEMENT:** `WeaponBoundsOrient`'s existing seat mode is documented
as *"Bounds centre at the parent origin (bow centre-grip, shield strap)"* — but the bounding-box
centre of a bow lies in the **HOLLOW between the string and the belly**, i.e. in empty air beside the
wood. Seating the hand at bounds-centre therefore floats the grip off the mesh. R4 keeps the same
midpoint on the long axis and projects it **out to the actual surface**, which is where a hand can
close. Implement as a surface projection from the measured bounds (raycast/nearest-point against the
mesh along the perpendicular), never as a per-weapon dialed offset.
⚠ Owner-tuned `manual=true` offsets in `attachment-offsets.json` remain canon and are NEVER
overwritten by the derived pass (ARCHITECTURE_PRINCIPLES section 4).

**R4a — CROSSBOWS ARE THE ONE EXCEPTION, AND ARE EXCLUDED FOR NOW.** Owner verbatim: *"that rule
will apply to every bow. The only one where that would be incorrect would be a crossbow. If the word
crossbow is in it, the widest part is on the x axis. The narrowest part would be the y axis, and the
other one would be depth or the z axis. But for simplicity, let's not include any crossbows until we
have verified that we can do that one successfully."*

- The bow rule (R4) applies to **every bow**. A crossbow inverts it: **widest -> X, narrowest -> Y,
  medium -> Z (depth)** — it is held across the body, not upright, so the "longest -> +Y" default is
  wrong for it by construction. Keyed on the NAME token `crossbow` (case-insensitive), which is the
  sanctioned discriminator (canon derives from bounds **+ asset name**).
- **Until the plain-bow path is proven on device, crossbows stay OUT.** Do not author, grant, or
  shelve one; do not implement the inverted mapping speculatively.

⚠ **MEASURED STATE 2026-08-16 — this is already true at runtime, and there is exactly one way to
break it:**
- `Assets/Resources/Data/Canonical/weapons.json` (the copy that **WINS** at runtime): **0 crossbows**.
- `Assets/StreamingAssets/Data/Canonical/weapons.json` (the stale 431-row side, kept by the
  deliberate owner gear ruling): **125 crossbow hits**.
- **68 crossbow meshes/prefabs exist on disk.**

So crossbows are already unreachable in the shipped game — the exclusion costs nothing today. **The
hazard is the editor menu `Defenders/Catalog/Generate Gear Catalog`**, which the weapons deep-dive
records as re-inflating Resources from 96 rows to 431 and writing BOTH copies. Running it would pull
all 125 crossbow rows into the live catalog at once, every one of them seating wrong under R4.
**Acceptance for this WO therefore includes a guard**: a regression asserting the runtime weapons
catalog contains no `crossbow` id while the exclusion stands, so the re-inflation cannot ship the
failure silently.

## 5. ~~ONE OWNER RULING NEEDED~~ — ✅ ANSWERED BY R1 ABOVE (auto-target + tap override)

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
