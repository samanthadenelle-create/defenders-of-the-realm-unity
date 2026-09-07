# WORK ORDER 1352 - Structures show wear from the first point of damage

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:03:03, build 2026.09.07.359076). PRIOR STATUS: FIXED 2026-09-03 - shipped in `2026.09.03.353999` and installed on the Seeker. A SCUFF rung FOLLOW-UP ALSO ON HER DEVICE 2026-09-03: the tell was one channel short BY CONSTRUCTION (the gloss ramp interpolated from t=0 at step 1 against a hardcoded 1f endpoint, so smoothness could not move whatever was authored). Now four front-loaded rungs, step 1 dropping albedo 20% AND killing the specular sheen. The 'second renderer' was the tower's lock-on laser - her tower was never half-tinted, but the beam would have been darkened as masonry if first damaged mid-raid. Body-mesh whitelist plus all-or-nothing abstention.
added below smolder inside the EXISTING owner (`StructureDamageVisuals`): albedo x0.88 / x0.77 / x0.66
with smoothness x1.00 / x0.70 / x0.40 across 100%-83.3%-66.7%-50% HP, handing off to the unchanged
smolder -> fire ladder. Gates `COMPILE_GATE_OK` + `REGRESSION_OK 358/358`.
⚠ AWAITING HER EYE ON ONE NUMBER: at 95% HP the tell is a 12% darkening, which may be too subtle to
read at a glance. The band is authored in `damage-states.json`, so strengthening it is a data change,
not a rebuild.
**Silo / Lane:** VFX / structure damage presentation
**Type:** EXISTING owner, new bottom rung
**Minted:** 2026-09-03 - ⚠ minted by an implementing agent while the numbering banner still read 1349,
so 1349-1352 were consumed without a bump. Reconciled in the banner's fortieth pass.
**Severity:** P2 - the game was billing her for repairs on structures that looked pristine.

## The owner's ruling

She was offered three ways to reconcile the mismatch and chose: **show a visible tell from the FIRST
point of damage**. Explicitly NOT chosen: suppressing the repair affordance above the smolder threshold
(that would make a 60%-HP structure unrepairable), and narrowing Repair-All only.

## The mismatch it closes

| | |
|---|---|
| `RepairTarget.cs:150` | `NeedsRepair => DamageFraction > 0.0001f` |
| `StructureDamageVisuals.cs:108-109` | `smolder = 0.5f` - the FIRST VISIBLE tell |

**So 50%-99.99% HP was pristine to the eye and damaged to the code.** Her device toast proved what that
costs: `"Repaired 1 structures for Wood 35, Iron 7"` - Repair-All ran and charged her for a structure
showing nothing at all. After this change, what she sees always matches what she is billed for.

⚠ **WO-1296 (2026-09-02) could not have caught it.** It changed a MESSAGE, not a predicate, and only
covered `DamageFraction == 0` - a tap on a truly pristine structure. This is the other case, which is
why she reported it as *"still"* showing up.

⚠ **And there was no data because the probe was silent:** `RepairAvailabilityProbe.Poll()` returned
early whenever nothing was on fire - and an invisibly damaged structure is by definition not on fire.
It now runs the inverse pass and names every structure in the silent band with its HP, threshold and
the exact price being charged.

## Why it is not hue-carried

The albedo is multiplied by a **scalar**: R, G and B scale by the identical factor, so hue and the
saturation ratio are mathematically unchanged and only **value** moves. Any greyscale conversion is a
weighted sum of R,G,B, so it scales by that same factor - **the tell survives full desaturation
exactly intact.** The second channel is smoothness (matte), a texture read, not colour at all.

## The cost constraint that shaped it

Unlike smolder, which only ever ran on a damaged few, this sees EVERY structure in a town - and the
device already reports `VfxPerfGate` hitches against a 16.7 ms budget.

- **Undamaged structure: one int compare + two dictionary lookups per 0.3 s eval. Zero allocation,
  zero property-block write, zero work.** A first-line guard exists precisely so a pristine town is
  never resolved or written to - an MPB drops a renderer out of the SRP batcher, so "setting it back to
  its own colour" would have cost draw calls town-wide, permanently.
- Damaged structure: one `GetComponentsInChildren` + N writes ONLY on a step change; at most 4
  transitions across a full damage-and-repair cycle.
- **Zero particles, zero GameObjects, zero pooled loop slots.**

## Baked hub structures - it needed both halves

1. Resolve filters on `r.enabled`, which is exactly what `HubStructureVisualInjector.SkinStorefront`
   uses to hide the baked mesh (`r.enabled = false`, not `SetActive`) - so the tell tints the injected
   `LightSkin_` child, never the invisible baked twin.
2. The renderer list re-resolves when the host's `childCount` moves, so a **late-arriving** skin picks
   it up on the next eval.

Without both, this would have worked everywhere except the town she is looking at.

## Oracle

Appended to the existing `StructureBurnRegression`. It binds to the LIVE `RepairTarget.NeedsRepair`
rather than a copy of the `0.0001` literal, sweeps 201 samples for silence and monotonicity, and pins
that `scuffOnset` may never be authored below the repair predicate.

**Mutation:** setting `scuffOnset` back to `0.5` (the pre-change world) fails three ways - 100/201
silent samples, wrong ordinal above the handoff, onset below threshold.

⭐ The agent found a real bug in its own first draft doing that: the monotonicity test was inverted and
passed everything vacuously. Caught only by running the arithmetic, not by reading it.

## Acceptance

- [x] No HP band is visually silent while repair-eligible.
- [x] Not hue-carried; survives greyscale.
- [x] Reaches baked hub structures, including a late-arriving skin.
- [x] Zero cost on an undamaged town.
- [x] Oracle proven RED; mutation reported.
- [ ] ⛔ **Owner felt-verifies the tell is STRONG ENOUGH to read at a glance**, and closes. If not, the
      lever is `scuffMinDarken` / `scuffMaxDarken` in `damage-states.json` - data, not a rebuild.

---

# FOLLOW-UP 2026-09-03 - the tell shipped, and it was too quiet to see

**Owner ruling, verbatim:** *"just do whats best. I dont know that answer so use SME to do recommended
option"* - the strength judgement was explicitly delegated. Decided and implemented below.

## The measurement that reopened it

Her device, same night the first cut shipped:

```
[Flow:RepairProbe] INVISIBLY-DAMAGED 'Archer Tower' hp=0.960 damageFraction=0.040
    firstVisibleTellAtHp=0.50  inRepairAllSet=True  price=wood=15 iron=7
[Flow:DamageVis] scuff step 1/3: hp=0.960 band=SCUFF
    applied albedo x0.88 (VALUE only, no hue shift) + smoothness x1.00 to 1 renderer(s)
[Flow:DamageVis] scuff renderers resolved for 'Archer Tower': 1 eligible of 2
```

Two separate problems on those three lines, and only one of them was a judgement call.

## 1. Strength - the delegated judgement

**The structural bug first, because no authored number could have fixed it.** The ramp interpolated on
`t = (step - 1) / (steps - 1)`, which is **0 at step 1 by construction**, and the gloss ramp's step-1
endpoint was the hardcoded literal `1f`. So `smoothness x1.00` on her trace was not a tuning miss - the
second channel was **mathematically incapable of moving at step 1**, whatever anyone authored. Step 1 was
a one-channel 12% value drop on a sunlit building. That is the whole of what she could not see.

**Chosen values** (`damage-states.json` v3 -> **v4**, both twins, verified byte-identical by md5
`035dfd341d6f093d48c3dfa98938ce16`):

| knob | was | now | why |
|---|---|---|---|
| `scuffSteps` | 3 | **4** | 3 steps make step 1 cover a 17-point HP range alone, so one rung must be both "something happened" AND the top third's resolution. 4 rungs (~12.5 points each) let step 1 be loud while 2..4 stay gentle. |
| `scuffMinDarken` | 0.12 | **0.20** | The noticeability floor. 0.12 was measured invisible. |
| `scuffMaxDarken` | 0.34 | **0.38** | Keeps a little more travel under the front-loaded step 1 without approaching "ruined". |
| `scuffGlossStep1` | *(new)* | **0.55** | Gives the gloss ramp a step-1 endpoint of its own. This is the field that makes step 1 a **two-channel** tell. |
| `scuffGlossFloor` | 0.40 | **0.25** | So the gloss channel still has somewhere to go across four rungs. |

Resulting ladder:

| step | HP band | albedo | gloss |
|---|---|---|---|
| 1 | (0.875 .. 0.9999] | x0.80 | x0.55 |
| 2 | (0.750 .. 0.875] | x0.74 | x0.45 |
| 3 | (0.625 .. 0.750] | x0.68 | x0.35 |
| 4 | (0.500 .. 0.625] | x0.62 | x0.25 |

### Front-loaded, and the argument for it

The brief asked this to be argued either way. **Front-loaded, decisively.** Jumps are `0.200 / 0.060 /
0.060 / 0.060` on albedo and `0.450 / 0.100 / 0.100 / 0.100` on gloss - step 1 is **3.3x** the albedo jump
and **4.5x** the gloss jump of any rung after it.

- The only distinction a player actually reads off a building is **binary**: touched or not. A linear ramp
  spends its budget discriminating 83% HP from 67%, which nobody reads, and starves the transition that
  carries the meaning. The old 0.12/0.34-over-3 gave jumps of `0.12 / 0.11 / 0.11` - almost perfectly even.
- Step 1 is **the only rung with nothing above it**. It has no neighbour to be compared against and must
  carry a category change on its own. Steps 2-4 are read against their predecessor *and* are converging on
  the smolder, which arrives with an entirely new channel (motion + smoke) - so they need less per-step
  delta to still read as escalation.
- Weber: the same ratio reads smaller against an already-darkened surface, so a linear-in-darken ramp is
  already sub-linear perceptually at the dark end. Front-loading corrects toward, not away from, even
  perceptual spacing.

**The counter-argument is real and it set the ceiling.** Most structures sit near full HP most of the time,
so step 1 *is* what a healthy town looks like after a graze. That caps step 1's absolute magnitude at
"noticeable on inspection, invisible at a glance". The way to have both was **not** a bigger colour hit - it
was **adding the second channel at step 1**. Killing the specular sheen removes the sun's highlight off the
roof and timber: a strong, local, free cue (same float, same property block that was already being written)
that is a texture read rather than a colour read, so it sits entirely outside the colourblind risk surface.

**Escalation is untouched.** Total scuff travel is 1.00 -> 0.62 on a *static surface*. The smolder still
adds motion and smoke, fire a flame, critical a rhythm and a glyph, broken a silhouette change. A matte,
darker wall cannot read as a mild upgrade to "on fire" - the channels are categorically different, not
scalar. Oracle section E now pins the last rung under a `0.45` ceiling for exactly this reason.

### Desaturation proof

Unchanged property, now **proven in code rather than asserted in a comment** (oracle E6). The tint is
`(r*mul, g*mul, b*mul, a)`. On a reference albedo with three *different* channel values (a flat grey would
pass no matter how badly the code tinted) the oracle asserts, at every step:

- `Color.RGBToHSV` hue unchanged within 1e-4 (the scalar cancels in the hue sector maths),
- saturation unchanged within 1e-4 (`S = (max-min)/max`; the multiplier cancels top and bottom),
- Rec.709 greyscale luma equals `base_luma * mul` exactly - i.e. **the tell is fully intact after complete
  desaturation, at the same 20%/38% strength it has in colour**,
- alpha bit-preserved.

The gloss channel is not a colour at all and so is trivially safe.

## 2. The 1-of-2 renderer split - a defect, and it was mis-read

**The second renderer is `DefenseTower`'s `AimBeam`** - the lock-on laser, a `LineRenderer` on a child
GameObject built by `EnsureAimBeam` (`Assets/_Modules/Village/Buildings/DefenseTower.cs:1282-1319`) and
created with `_aimBeam.enabled = false` until a target is locked. It is **not the building**, and dropping
it was correct. It is *not* a baked twin - the Archer Tower is an owner-authored Tripo watchtower skinned
through `StructureFactory`, not a `HubStructureVisualInjector` swap. So **her tower was never half-tinted**;
the one eligible renderer was the entire visible tower. The trace line simply could not say so, because
every skip reason shared one counter and only the multi-albedo one had a name.

**But there was a live bug underneath it.** The old rule dropped the beam via `!r.enabled`, which is true
only *while the beam is off*. The first resolve happens the first time the structure is damaged - and a
tower gets damaged **during a raid, while locked on**. In that ordering the laser would have been captured
as building surface and darkened, then kept a property block permanently.

**Fix - a whitelist plus all-or-nothing.** `ResolveScuffRenderers` now asks two separate questions:

1. **Is this the building's body?** Only a `MeshRenderer` / `SkinnedMeshRenderer` *with a mesh*. Line,
   trail, sprite and particle renderers are effects the structure owns, never its surface - ignored
   outright, and **never a coverage hole**. A whitelist, not a blacklist: a blacklist needs extending every
   time a new renderer subclass appears under a structure, and the cost of missing one is tinting an effect
   as though it were stone. It subsumes the old explicit `ParticleSystemRenderer` skip.
2. **Can we drive every visible body renderer?** If not, **the whole structure abstains** and says so.
   A half-tinted building reads as a *rendering fault*; an untinted one merely reads as undamaged, and only
   one of those costs trust in the whole surface. The forfeit routes through the existing
   `scuff UNREACHABLE` path plus a new named `scuff ABSTAINS` warning that states which renderer and why.

**`enabled` stays the visibility test and is explicitly NOT a coverage hole.**
`HubStructureVisualInjector.SkinStorefront` hides the baked twin with `r.enabled = false` (never
`SetActive`), so every hub structure legitimately carries a disabled body mesh. Counting that as a hole
would make her entire town abstain. The `childCount` re-resolve in `ApplyScuff` is likewise untouched, so a
late-arriving `LightSkin_` child still picks the tell up.

The resolve trace now breaks the count out by reason - body/effect/hidden/undrivable - so this class of
mis-read cannot recur.

## Cost - preserved, and slightly improved

The zero-cost steady state is **unchanged and was never touched**: `ApplyScuff`'s first line is still
`if (step <= 0 && rec.ScuffStep <= 0) return;`, so an undamaged structure costs one int compare plus the
two dictionary lookups in `ScuffStepFor` per 0.3 s eval - **zero allocation, zero property-block write, no
renderer dropped from the SRP batcher**. Every change above lives behind that guard.

**Worst case in a full town: strictly lower than before.** The change can only ever *reduce* the eligible
set - the whitelist removes line/trail/sprite renderers, and the all-or-nothing gate can only empty a set.
A firing tower now contributes 1 MPB'd renderer instead of the 2 it could have taken mid-raid. The upper
bound is unchanged in shape: `(structures at or below onset) x (visible body meshes each)` - for a
fully-drained ~60-structure town at 1-3 body meshes apiece, on the order of 60-180 renderers outside the
SRP batcher, and only while they are actually damaged. Four steps instead of three means at most 4
property-block writes per structure across a full drain instead of 3; the write still happens only on a
step *change*.

## Oracle - RED first, mutation reported

Extended `StructureBurnRegression`'s `ScuffOracle` with **section E, `ScuffStrengthOracle`** - no new suite,
no `DataRegression.cs` edit. It binds to `StructureDamageVisuals.ScuffDarkenFor` / `ScuffGlossMulFor`, which
were **extracted out of** `ApplyScuff` (not copied) precisely so the pin is on the shipping ramp.

Pins: E0 step 0 is a no-op (the SRP guard) - E1 step-1 darkening >= 0.18 - E2 step-1 <= 0.25 and last step
<= 0.45 - E3 step-1 gloss < 0.95 (two channels at first blood) - E4 the pristine->step-1 jump is the
largest in the ladder, both channels - E5 strict monotonicity, both channels - E6 the desaturation proof
above.

**RED-first mutation, run against the pre-follow-up values** (`scuffSteps 3`, `scuffMinDarken 0.12`,
`scuffMaxDarken 0.34`, `scuffGlossFloor 0.40`, plus `scuffGlossStep1 1.0` to reproduce the old hardcoded
endpoint exactly). Three independent failures:

- **E1**: step-1 darkening `0.120` < the `0.18` floor.
- **E3**: step-1 gloss `1.000` is not below `0.95` - the second channel is inert.
- **E4**: the gloss ladder's first jump is `0.000` while later jumps are `0.300` - back-loaded.

Restoring `4 / 0.20 / 0.38 / 0.55 / 0.25` turns all three green. The mutation instructions are recorded in
the oracle's own header so the next seat does not have to re-derive them.

**Sections A-D re-checked against `scuffSteps = 4` and all still hold:** no silent sample (the top sample
clamps to step 1), monotonic sweep, handoff `ordJustAbove == 4 == steps` at `hp 0.501` and
`ordAtSmolder == 5 == steps + 1`, and `scuffOnset` unchanged at `0.9999`.

## Files touched

- `Assets/Resources/Data/Canonical/damage-states.json` (v4) + the `Assets/StreamingAssets/...` twin - md5
  identical, ASCII-only, valid JSON.
- `Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs` - new `scuffGlossStep1` knob + accessor, ramp
  extracted to `ScuffDarkenFor` / `ScuffGlossMulFor`, body-renderer whitelist, all-or-nothing gate,
  reason-broken-out resolve trace. Braces BALANCED, no NUL.
- `Assets/Editor/Regression/StructureBurnRegression.cs` - section E. Braces BALANCED, no NUL.

Nothing in `NeedsRepair`, the repair predicate, repair pricing or Repair-All membership was touched; no
material or texture asset on disk was modified.

## Acceptance (follow-up)

- [x] Step 1 is a two-channel tell and clears the noticeability floor.
- [x] Ceilings pinned so a healthy town cannot be authored into a slum.
- [x] Escalation into smolder/fire/critical/broken preserved and pinned.
- [x] Scalar-only tint proven under full desaturation, in code.
- [x] Second renderer identified; all-or-nothing shipped; baked-twin and late-skin behaviour intact.
- [x] Zero-cost undamaged path preserved; worst case strictly lower.
- [x] Oracle proven RED against the old values; mutation recorded.
- [ ] Lead runs the compile gate + `DataRegression.RunAll`, then commits.
- [ ] **Owner felt-verifies at ~4% damage**: noticeable on the building she is looking at, town not grim
      at a glance. Levers remain pure data - `scuffMinDarken` (up = louder first hit) and
      `scuffGlossStep1` (down = deader sheen) in `damage-states.json`.
