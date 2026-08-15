# WO-966 — The hero's body faces the wrong way while running (Mage: NW when running N)

**Status:** IMPLEMENTED — 2026-08-15 (Mage/Ranger skin at 0 + shoulder AlignBodyFacingToRoot; Knight +15 untouched; gait uses MeasuredRootSpeed). PO felt-verify Mage run-north.

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Remaining work: THE FIX ITSELF. Only the instrumentation landed; HeroBodySwapper.cs:263 STILL HARDCODES the yaw. Cross-reference WO-985, which reached the same diagnosis independently.
> Everything else in this WO is present in HEAD. The named remainder IS the ticket now - do not
> re-implement the shipped part.
> _Prior status line, preserved: Status: READY TO IMPLEMENT - ⚠ **"no capture needed" is RETRACTED, see the 2026-08-14 banner below**_

> ## ⚠ CORRECTIONS FROM A UNIFIED SME DIAGNOSIS, 2026-08-14 (read before implementing)
> This ticket was diagnosed together with WO-985 because **they are the same subject and an
> uncoordinated fix to one breaks the other** — which is exactly what happened on 2026-08-14, in
> captured data (the dungeon camera/hero yaw pair).
>
> **1. ⛔ `HeroBodySwapper.cs:263` IS SHARED SURFACE — it has no lane.** The dungeon Keeper uses the
> **same `HeroBody`**, swapped by the **same `HeroBodySwapper`** (`DungeonHero.cs:126-127, 188, 215,
> 301`; `DungeonCameraRig.cs:57-58` exists to survive that swap). Changing `:263` for the Mage
> **also changes the dungeon Mage's facing**, against a camera that now compensates for nothing.
> **ONE edit, gated once, verified in BOTH scenes before commit.**
>
> **2. "no capture needed" is FALSE.** The measured 94.5° comes from `HeroFacingAudit` on the **FBX
> asset in the editor** — not from a runtime capture of a **moving, swapped, height-refit, `Rebind`-ed**
> live body. §12: that LOCATES, it does not CONCLUDE. A capture in **both** scenes is required.
>
> **3. THE TITLE'S "45 degrees" IS NOT THE NUMBER.** The audit says **~94.5°**. 45 is the owner's felt
> estimate (this file already says so at `:30`) — which is what a ~90° body error looks like when the
> root is still slewing toward the heading (`HeroLocomotion.cs:1156`) at the instant of judgement.
> **Treat 94.5 as the measured static delta; treat 45 as unproven.**
>
> **4. ⚠ THE AUDIT DOES NOT VALIDATE THE FIX IT RECOMMENDS.** `HeroFacingAudit.cs:142` calls
> `RangerBodyBuilder.MeasureForwardYawNeeded` (`:1123`), which derives forward from the **SHOULDER**
> axis. The self-correct this ticket proposes enabling, `HeroBodySwapper.AlignBodyFacingToRoot`
> (`:2157-2168`), derives it from the **HIP** axis. Different measurements. Deadzone 5° (`:2179`) vs
> the audit's 15° warn band. **Re-point the self-correct at the shared measure, or re-run the audit
> hip-derived and confirm they agree within the deadzone — before enabling anything.**
>
> **5. ⚠ SECTION 3'S PREMISE IS STALE.** It states Mage/Ranger "have no authored FBX — they fall back
> to a tracked KayKit body". **False at HEAD:** `HeroBodySwapper.cs:128-133` probes
> `Resources/Heroes/<slug>` FIRST and routes to `BuildLegacyResourcesBody`; `Assets/Resources/Heroes/
> Mage.fbx` and `Ranger.fbx` both exist. The Mage takes the **`-90` legacy path**, not the identity
> KayKit fallback. **Any fix reasoning from the KayKit premise targets dead code.**
>
> **6. ⚠ THE PROVING INSTRUMENT IS BLIND IN DUNGEONS.** `HeroGaitForensics` gates `bodyErr` on
> `velMag > 0.2` (`:160`) computed from `_loco.Velocity` (`:132-134`) — but `HeroLocomotion.cs:972-975`
> **forces `Velocity = Vector3.zero` every frame in a dungeon** (the CharacterController owns the
> transform). `bodyErr` therefore reports **0 whether or not the body is wrong**. Feed it the measured
> root speed (`HeroLocomotion.cs:27-31`) before trusting any dungeon number.
>
> **7. THE KNIGHT IS STRUCTURALLY IMMUNE — do not read its correctness as a global pass.**
> `ff.knightv3` is ON by default (`:98-102`), routing the Knight to `KnightV3ForwardYaw = 15f` (`:478`)
> and out of the `-90` branch entirely. Measured deltas match exactly: Mage 94.5, Ranger 93.7,
> KnightV3 0. **A Knight regression to ~90 after the fix is the specific alarm** for the latent
> `:652-659` pair ("walking NORTH but FACING EAST"). Pin `|bodyErr| < 15` **per class**, or the next
> class to ship inherits the defect invisibly.
**Renumbered 965 -> 966 on 2026-08-10:** the CLI seat wrote this file WITHOUT bumping the numbering banner - the exact mint-without-bump that caused five collisions on 2026-08-02 - and a parallel lane then legitimately minted 965 for the F8 queue and bumped the banner. That one is referenced in `CLAUDE.md:431`, so under first-on-disk-AND-referenced it keeps 965.
**Date:** 2026-08-10 · **Priority:** Medium-High (it is felt on every step the player takes)
**Block:** main line (CLI) · **Lane:** Hero locomotion / gait
**Owner F8 seq 2309** (2026-08-10 18:15:52, `Main_Castle_Overworld`), verbatim:
*"Mage faces northwest when running north"*

## §1 What the capture actually contains — and what it does NOT

⚠ **This WO deliberately does not name a root cause.** Per CLAUDE.md §12 a fix needs a captured line
that PROVES the cause, and the harvested window does not contain one. Recorded honestly rather than
guessed, because an inference-fix here would be a 45-degree constant somebody has to un-guess later.

Every `[Flow:GaitF]` line in the harvest reads **`vel=0.00`** — the capture landed while the hero was
STANDING STILL, not running. What the standing sample does show:

```
[Flow:GaitF] vel=0.00@170deg dHead=0.0 yaw=171 dYaw=0.0 camYaw=207 dCam=0.7  ... clip=mixamo.com(1.00)
[Flow:GaitF] vel=0.00@170deg dHead=0.0 yaw=171 dYaw=0.0 camYaw=180 dCam=-0.7 ... clip=mixamo.com(1.00)
[Flow:GaitF] vel=0.00@170deg dHead=0.0 yaw=171 dYaw=0.0 camYaw=171 dCam=0.0  ... clip=mixamo.com(1.00)
```

- Body `yaw` is pinned at **171** across the whole window while `camYaw` sweeps **207 → 171**. The body
  does not track the camera while idle — which may be correct behaviour, or may be the same defect seen
  at zero speed. **Unresolved either way.**
- `vel=0.00@170deg` — the heading field reads 170 with zero magnitude, so it is a stale/last-heading
  value, not a movement direction. It cannot be used to compute an offset.
- ~45 degrees is the owner's felt estimate (N vs NW), not a measured number. Do not hard-code it.

## §2 The ONE capture that settles it

**Run north, and press F8 WHILE STILL MOVING.** That puts `vel > 0` lines in the harvest with a live
heading, and the answer falls straight out of three fields on one line:

| Field | What it tells us |
|---|---|
| `vel=<speed>@<deg>` | the direction the hero is actually TRAVELLING |
| `yaw` | the direction the body is FACING |
| `camYaw` / `dCam` | where the camera is, and whether the body is chasing it |

`yaw − vel_deg` at a steady run **is** the offset, measured rather than estimated. If that difference is
a constant, it is a seating/authoring offset; if it varies with `camYaw`, the body is being driven by
camera-relative input that is not being converted back to world space; if `dHead` lags `dYaw`, it is a
smoothing/order-of-operations problem.

Secondary, from the same capture: `skate` and `speedP` say whether the CLIP matches the travel — a
facing offset and a foot-skate usually have the same root.

## §3 Scope, once the capture exists

1. RCA from the captured line; quote it in the RESULT (§12 — the proving line is the deliverable).
2. Fix at the seam the data names — **not** by adding a compensating rotation offset unless the data
   proves the authoring is what is wrong.
3. ⚠ Check whether this is CLASS-SPECIFIC. The flag names the **Mage**, and Ranger/Mage were unlocked
   recently with **no authored FBX** — they fall back to a tracked KayKit body. If the Knight is
   correct and the Mage is not, the defect is in the fallback body's authored forward axis, and that is
   the QR-5.2 export-convention class (a **-90 YAW ONLY** fix, never a pitch — a pitch lays the model on
   its back, proven by captured data on the trolls).
4. A regression pinning the invariant: at a steady run, body yaw tracks travel heading within a small
   band, for EVERY playable class.

## §4 What NOT to do

Do not apply a 45-degree constant because "NW vs N is 45". The owner's report is a felt bearing, the
capture has no moving sample, and a magic constant would mask whatever the real seam is on every other
heading.

## §MEASUREMENT (2026-08-10) — the cause is now a NUMBER, not a hypothesis

Run headless, no playtest, no F8: `-executeMethod DeNelle.Editor.HeroFacingAudit.MeasureAll`
(log `Builds/facing-audit.log`, marker `HERO_FACING_AUDIT_OK 3/3 models measured, 2 disagree`):

```
Mage     measured forward (-0.079, 0, 0.997) via humanoid shoulder axis -> needs   4.524 deg; swapper applies -90 -> DELTA 94.524 DISAGREES
Ranger   measured forward (-0.064, 0, 0.998) via humanoid shoulder axis -> needs   3.658 deg; swapper applies -90 -> DELTA 93.658 DISAGREES
KnightV3 measured forward ( 0,     0, 1    ) via humanoid shoulder axis -> needs   0.000 deg; swapper applies -90 -> DELTA  0      agrees
```

**The Mage's model already faces +Z within 4.5 degrees, and HeroBodySwapper.cs:263 rotates it -90 anyway.**
That is a ~94 degree error on the BODY only, which is why the root steers north while the mesh points off,
and why Knight (delta 0) never showed it. Candidate (b) in §3 is CONFIRMED; (a) camera-space and (c)
smoothing are not needed to explain it.

### The fix is a one-line owner choice
1. **DERIVE (recommended).** HeroBodySwapper.cs:660 if (isBlink) -> if (isBlink || cls != HeroClass.Knight),
   which runs the existing skeleton-derived AlignBodyFacingToRoot self-correct. A future model cannot
   re-break it, because nothing is hard-coded. Yaw-only.
2. **CONSTANT.** HeroBodySwapper.cs:263 : -90f -> the measured value. Faster, but the next model drop
   re-opens this ticket - which is exactly how a -90 authored for the Tripo era survived onto CC/AccuRIG
   models and shipped this bug.

⚠ Yaw only, never a pitch (QR-5.2: a pitch laid the trolls on their backs, proven by captured data).