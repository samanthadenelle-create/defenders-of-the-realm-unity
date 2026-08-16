# WORK ORDER 994 - The shield's authored seat is stranded against a base WO-970 moved

**Status:** REOPENED 2026-08-16 — diagnostic spec ready (Section: DIAGNOSTIC SPEC below); AWAITING OWNER GO. Prior 2026-08-15 re-seat did not hold on device.
**Minted:** 2026-08-14 (CLI)
**Silo:** Gear / equip seating
**Source:** OWNER REPORT - *"still same problem when porting from dungeon with Shield position"*

---

## OWNER PIN 2026-08-15 (re-scope the remaining bug)

> **Shield is perfect until porting from dungeon to town. Only then does it break.**

### What this means

| Context | Owner feel |
|---------|------------|
| Town / steady play | **Seat is good** — do **not** re-dial `shield_A` as a global fix |
| Dungeon | **OK** while inside |
| **Exit dungeon → town** | **Breaks** — only this transition |

### Remaining work (port seam, not Seating Editor A)

1. Trace **dungeon exit / scene load / equip re-apply** path: what re-parents or re-`NormalizeInto`s the off-hand after `SceneRouter` to Castle.
2. Suspects (instrument, don't guess): height/scale change town vs dungeon (WO-994 height amp), `ApplyHoldPose` / sheathe on scene load, second `EquipmentController` attach, `fullOverride` + compensate asymmetry on re-equip.
3. Fix so **town post-port matches pre-port / in-dungeon good seat** — preserve the dial that already feels perfect.
4. ⛔ Do **not** invent new `offsets.json` eulers “to fix town” if that ruins the good dungeon/town steady pose.

---

## Root cause, proven from captured data (still useful for the port path)

WO-970 (`af5e2e7d8`, 2026-08-10 19:27) fixed `AlignAxesYLongXNarrowZWide` so a weapon's long axis
finally reaches +Y. Same mesh, same authored delta, before and after:

```
PRE-FIX  (WO-970 SS2)   NormalizeInto 'EquipmentProp_OffHand': aligned b1=(0.01, 0.002, 0.008)   X-long
POST-FIX (2026-08-14)   NormalizeInto 'EquipmentProp_OffHand': aligned b1=(0.002, 0.01, 0.008)   Y-long
```

The inner prop rotation moved ~90 degrees. `shield_A`'s delta - `rot=(-160,-180,-84)`, dialled
**2026-07-07** in `Assets/Resources/OffsetForge/offsets.json` - was authored on top of the OLD align
and has never been re-dialled.

Owner F8, **1h36m after** that commit: seq2325 *"the shield is **now** mid body"*, seq2326 *"broken
shield carried back on exit"*.

## Why the prior fix did not hold - one sentence

WO-970 named the pin but excused this exact file:

> *"`shield_A` is `fullOverride: true` = absolute in the socket frame, so it is immune either way."*

**That is FALSE at source.** `fullOverride` writes the delta onto `gripRoot`
(`EquipmentController.cs:1615-1617`); `NormalizeInto` rotates `prop`, which is gripRoot's CHILD
(`WeaponBoundsOrient.cs:135`). Final orientation = `gripRoot(authored) . prop(AlignAxes)`.
Absolute on the OUTER frame is not immunity to an INNER rotation. That sentence is why this survived.

## The matched pair

- **Half A (moved):** `WeaponBoundsOrient.AlignAxesYLongXNarrowZWide` - `WeaponBoundsOrient.cs:116-143`
- **Half B (stranded):** `shield_A` `rot=(-160,-180,-84)` + `shield_A@sheathed` `rot=(2,180,-78)`

## The trace is hollow - this is why 4 days of captures show nothing

`EquipmentController.cs:1612` echoes `offsets.json` verbatim. It prints **byte-identical text
whether the shield lands on the arm or 90 degrees through her chest**, and has printed the same string
since 2026-07-07 - unchanged straight through the regression.

The line claiming to be landed proof (`:1691`) is worse: it prints position but **no rotation** (the
only thing that changed), **no world bounds**, and runs **before `ApplyHoldPose()` at `:1701`** - so
for a sheathed hero it logs a pose the prop does not keep.

## Two real port-specific amplifiers (proven; which one the owner sees is UNPROVEN)

**(a) Different heroes, different heights.** Dungeon Keeper `height=2m -> propScale=51.025`; town hero
`height=1.8m -> propScale=45.924`. The shield is **11% larger in the dungeon** against a FIXED
position delta.

**(b) Drawn-vs-sheathed size asymmetry, flagged by WO-970 and never ticketed.** Back path
`:1847` calls `CompensateParentScale` **unconditionally**; hand path `:1860` guards it on
`_offHandParentCompensate`, which `:1619` sets **false** for `fullOverride`. Captured:
`parent='CC_Base_L_Hand' -> 0 lines` vs `parent='SheatheSocket_Back' -> 41 lines`. **One prop
renders at two sizes** - 1.666x between in-hand and on-back.

## Fix

**A - DATA (owner's hands, not an agent's).** Re-dial `shield_A` + `shield_A@sheathed` in the
Seating Editor against the corrected align. These are manual/CANON values. **An agent must NOT compute
a compensating euler** - that recreates the same stranded pair one layer up.

**B - CODE.** `:1847` must take the same `_offHandParentCompensate` guard as `:1860` (mirror for
the main weapon at `:1819` vs `:1834`). A prop must not render two sizes.

**C - TRACE (mandatory).** Replace `:1691` with a MEASURED world pose emitted **after**
`ApplyHoldPose()`: parent name, `propLocalEuler` AND `gripLocalEuler` **separately** (so a moved
base is distinguishable from a bad dial), world euler, encapsulated world bounds, bone lossyScale,
compensated bool, DRAWN|SHEATHED. Without C the next capture is blind again.

**D - REGRESSION.** Assert `aligned b1` is Y-longest after every `AlignAxes` (WO-970 stated the
invariant and shipped no test), and that drawn vs sheathed world bounds match for a `fullOverride` prop.

**E - CANON.** Banner the false "immune either way" line in WO-970. It is load-bearing.

## What NOT to do

- Do NOT hand-compute a corrective rotation for `shield_A`. Re-dial it.
- Do NOT delete the hollow `:1612` line - retoken it (CLAUDE.md SS12: never strip FlowTrace).
- Do NOT touch `AlignAxesYLongXNarrowZWide`. Half A is correct now.

---

## DIAGNOSTIC SPEC 2026-08-16 (post-IMPLEMENTED regression; verified at source; AWAITING OWNER GO)

**Symptom (owner felt-test, Seeker, 2026-08-16):** shield renders as a flat oval plate jutting
horizontally off the hero's LOWER BACK - not on the arm, not flush to the back - and persists
across every scene after the dungeon->town port. Device screenshot on file (scratchpad s1.png).
The 2026-08-15 scene-load re-seat (RESULT above) did NOT hold on device.

**Owner rulings still binding** (docs/DESIGN_SUGGESTIONS_OPEN_TICKETS_2026-08-15.md, WO-994 section):
breaks only on dungeon->town port - fix the SEAM, not the dial. Do NOT re-dial `shield_A`.
Do NOT touch AlignAxes. INSTRUMENT FIRST (CLAUDE.md sec 12): no seat numbers change until one
captured run names the mechanism.

### Owner-tuned constants - record only, NEVER guess at or recompute
(all cites = `Assets/_Modules/Village/Hero/EquipmentController.cs` at HEAD 2026-08-16)

- Drawn shield preset `Shield()` at `:162-167`: `gripPos=(-0.05, 0, 0)`,
  `gripEuler=(-58, 16, -90)`, `leftHand=true` (Offset Forge + hand-bone nudge 2026-06-23).
- Sheathed-on-back `_sheatheOffHandLocalEuler` at `:378` = `(0, 90, 192)` - base `(0,90,12)`
  with owner `Z+=180` (comment `:374-377`, owner live felt-tune 2026-07-04, manual=true).
- `shield_A` registry delta `rot=(-160,-180,-84)` in `Assets/Resources/OffsetForge/offsets.json`
  (dialled 2026-07-07) plus any persistentDataPath `attachment-offsets.json` user row.

### Candidate mechanisms - verified at source

**CANDIDATE A - Reload() asymmetry between first-equip and the WO-994 re-seat. VERIFIED AS
ASYMMETRY; consequence UNPROVEN.**
- `OnEnable()` calls `AttachmentOffsetRegistry.Reload()` at `:483` immediately before
  `EquipBestForHero()` at `:484`.
- `CoReapplyGearAfterSceneLoad` (`:498-511`, subscribed `:487-488`) calls
  `InvalidateHeroHeightCache` `:503`, `CacheRig` `:504`, `EquipBestForHero` `:506`,
  `ApplyHoldPose` `:507` - and does NOT call `Reload()`.
- HOWEVER, source shows the naive reading ("re-seat runs on shipped defaults") is NOT
  automatic: `AttachmentOffsetRegistry` statics (`s_map`/`s_loaded`,
  `AttachmentOffsetRegistry.cs:73-74`) persist across scene loads in a player (no domain
  reload), and `TryGetOffset` (`:212`) goes through `EnsureLoaded` (`:76-80`) which lazily
  re-reads BOTH the base json and the persistentDataPath user json (`LoadFromDisk` `:90-100`).
  Defaults win only if the map was somehow cleared AND the user-file read fails on device.
  The trace, not a theory, decides whether A fires.

**CANDIDATE B - PackageBakedGear skip racing the 2-frame re-seat wait. VERIFIED AS A REAL
SKIP PATH; timing on device UNPROVEN.**
- `PackageBakedGear` property `:227` (marker class `:3047`); weapon attach skips at `:645`,
  off-hand/shield attach skips at `:1449-1455` inside `EquipOffHand` (comment block `:222-227`).
- `HeroBodySwapper.cs:735-741` adds `PackageBakedGearMarker` BEFORE adding
  `EquipmentController` (`:742-743`) when `usePackage` - by design, synchronously.
- The re-seat waits exactly 2 frames (`:501-502` `yield return null` x2) "for HeroBody swap /
  height retarget". If the town body swap/bake completes AFTER that window, the re-equip runs
  against the pre-swap rig or with a marker state that does not match the final body -
  leaving the package pose or a prop parented to a stale bone.

**CANDIDATE C - found during this verification: the idempotent early-outs make the WO-994
re-seat a NO-OP for a surviving prop. VERIFIED AT SOURCE.**
- Weapon: `:653-659` - same `_currentWeaponId` + live `_currentWeaponProp` -> `return`.
- Off-hand: `EquipOffHand` `:1457-1460` - same `_currentOffHandId` + live
  `_currentOffHandProp` -> `return`, BEFORE any NormalizeInto / registry lookup / height use.
- Consequence: if the hero root (and its shield prop) survives the port, the `:506`
  `EquipBestForHero` re-seat early-outs for the shield - `InvalidateHeroHeightCache` is
  consumed by nothing, no re-NormalizeInto happens, and only `ApplyHoldPose` `:507` runs on
  the STALE prop transform. The shipped fix can be structurally inert for the exact case it
  targets. This is consistent with "IMPLEMENTED but the owner still sees the bug".

### Instrumentation plan (sec 12 compliant - ONE run discriminates A vs B vs C)

CLI implements ONLY these trace lines first. No seat number, no offset, no guard-flag, no
frame count changes until the captured run names the mechanism. All lines `[Flow:Equip]` /
`[Flow:Offset]` tagged so the F8 harness + logcat carry them; keep them permanent per
CLAUDE.md sec 12 (flag off later, never strip). Mind the logcat ring - pull the capture
immediately after the port, or via break-log.jsonl.

1. **Registry state, both paths (discriminates A).** In `LoadFromDisk` the row-count line
   already exists (`AttachmentOffsetRegistry.cs:97-99`). Add: at `OnEnable:484` (pre-equip)
   and inside `CoReapplyGearAfterSceneLoad` before `:506`, one line each:
   `registryProbe path=<START|SCENELOAD> rows=<s_map.Count> shieldA=<pos/rot/scale/fullOverride
   or MISSING> shieldA@sheathed=<same>` (via `TryGetOffset("shield_A", ...)`). If the
   scene-load probe shows shipped values where the start probe showed user values, A fires.
2. **Bake-marker + body identity vs the re-equip frame (discriminates B).** In
   `CoReapplyGearAfterSceneLoad` immediately before `:506`:
   `reapplyCtx scene=<name> frame=<Time.frameCount> baked=<PackageBakedGear>
   body=<HeroBody child name> animator=<isHuman/avatar name>`.
   In `HeroBodySwapper` at the marker-add (`:737`) and at swap completion, log
   `frame=<Time.frameCount>` so the two frame numbers ORDER the race directly. Also: the
   existing SKIP lines at `:647-650` / `:1451-1454` already prove B's branch if they appear
   between scene load and the reapply line.
3. **Idempotent early-out visibility (discriminates C).** The weapon early-out logs
   (`:657`); the off-hand early-out at `:1458-1460` logs NOTHING - add one line there:
   `offhand idempotent skip id=<id> prop=<name> parent=<parent path> frame=<n>`.
   If the scene-load re-equip emits this skip, C fires and the shipped re-seat is proven
   inert for the shield.
4. **Final measured pose, both paths (the ground truth both sides diff).** After
   `ApplyHoldPose` in BOTH the OnEnable path and `:507`: one MEASURED line for the shield
   prop (this is Fix C above, still unshipped):
   `shieldPose path=<START|SCENELOAD> parent=<bone path> gripLocalEuler=<v> propLocalEuler=<v>
   worldEuler=<v> worldBounds=<encapsulated size> lossyScale=<v> height=<_cachedHeroHeightM>
   compensated=<bool> state=<DRAWN|SHEATHED>`.
   Diff pre-port vs first-town-frame: identical numbers = presentation/other; different
   numbers = the differing field names the mechanism.
5. One run = play to dungeon with shield correct -> exit to town -> pull capture. The four
   probes decide: A (registry rows/values differ), B (baked/skip lines or frame order wrong),
   C (offhand idempotent skip on the scene-load path), or none (escalate with the measured
   pose diff in hand).

### Acceptance standard (OWNER DIRECTIVE 2026-08-16 - BINDING)

The proof for the eventual fix is a DEVICE screenshot taken AFTER a dungeon->town transition
on the Seeker - the seat is perfect until that transition, so a hub-only screenshot does not
test it. Batchmode/headless captures are INVALID for this class. True before/after = same
device, same transition path (dungeon with shield correct -> port -> town screenshot).

### Status

Spec complete; instrumentation is the FIRST and ONLY implementation step.
**AWAITING OWNER GO** before the CLI lands the trace lines.