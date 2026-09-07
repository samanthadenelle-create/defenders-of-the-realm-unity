> # ⚠ SUPERSEDED 2026-08-09 — read `CANON_GROUND_TRUTH_2026-08-09.md` FIRST
>
> **This file is INVERTED on BOTH of its headline sections — not merely stale.**
> - **§0 "THE MACHINE IS BLOCKED / a reboot is the fix" is RESOLVED.** The machine rebooted 2026-08-08
>   08:07:21; commit charge is back to 45.7 GB of 127.8 GB, and the Windows EXE (08-08 14:33) and Android
>   APK (08-08 20:00) both built. Only the WebGL/web-deploy step of that morning order never ran.
> - **§2 "the dungeon stairs — where the hunt actually stands" is CLOSED.** The stairs were SOLVED the
>   same morning (WO-930, `3ab1bfb6` → `cb092b7f`: all 4 content dungeons `PathComplete`). Root cause was
>   **stair YAW** — `GraphDungeonComposer.SolveMate` hardcoded `yaw = 0f` on vertical sockets — never a
>   property of the stair. §2's "nothing cheap remains / next move is to dump navmesh triangles" is dead
>   guidance. Its killed-hypotheses table survives as history: it still stops re-runs.
>
> Per CLAUDE.md §15 dated ledgers are frozen — this is a banner, not a rewrite. Nothing below is changed.

# CANON GROUND TRUTH — 2026-08-08

**Supersedes `CANON_GROUND_TRUTH_2026-08-07.md`.** Per CLAUDE.md §15 this is the single live anchor:
every other doc loses to it on conflict. Written at the end of the 08-07 overnight run.

**Branch:** `wip/village2-and-f8-tickets` · **committed, NOT pushed** · master is stale.

---

## 0. ⛔ THE MACHINE IS BLOCKED — READ THIS BEFORE PLANNING ANY BUILD

**Commit charge is 119.5 GB of a 127.8 GB limit** with **13 GB of physical RAM free and NO Unity
process running.** It is committed memory the OS has not reclaimed after a night of batchmode runs
— not attributable to any live process, so there is nothing to kill.

```
Fatal Error! Could not allocate memory: System out of memory!
```

**A REBOOT IS THE FIX.** Until then, blocked:

| | Status |
|---|---|
| **Windows EXE** | ❌ OOM mid-build. `Builds/Windows/` still holds the **08-07 18:14** build — verify the timestamp before trusting it |
| **Android APK** | ❌ not attempted. IL2CPP/ARM64 needs far more than the 8 GB of headroom left |
| **Firebase release** | ❌ blocked behind the APK |
| **WebGL / web deploy** | ❌ `vercel.json` serves `Builds/WebGL`, which is a Unity build — same wall |

Batchmode *editor* methods (gates, bakes, regression) still run fine; only player builds fail.

**MORNING ORDER: reboot → EXE → APK → Firebase → WebGL.** Everything else below is done and green.

---

## 1. Gate state

```
COMPILE_GATE_OK
REGRESSION_OK 130/130 suites      ← fully green
COMPOSE_ALL_OK 6/6
GRID_OK on all 86 rooms
```

---

## 2. The dungeon stairs — where the hunt actually stands

**Symptom, unchanged:** every multi-level dungeon reports `PathPartial`. Only single-floor
`dg_starter_loop` is `PathComplete`. **Nothing below the starting floor is reachable at all**
(`floor delta start->stop = 0.00m`).

**Not for want of stairs.** Connectors resolve (`connectors=8/10/2, fallbacks=0`), geometry is
placed, mates pass, every ramp carves navmesh.

### ★ FOUR HYPOTHESES TESTED AND KILLED. Do not re-run them. ★

| # | Hypothesis | How it died |
|---|---|---|
| 1 | **Landing width** — 0.80 m eroded below the 1.00 m walkable slot | `TurnRun 4.0 → 3.5` shipped. Landing 1.30 m, slope 40.6°, both measured. **Path unchanged.** |
| 2 | **Slope** — 42.7° too close to the 45° agent max | Switched every descent to the 40.6° turn shape. **Worse:** 0/8 whole vs Vertical's 2/4. The *shallower* ramp fragmented *more*. Reverted. |
| 3 | **Ramp length** — turn legs are 3.5 m vs Vertical's ~6.5 m | Bucketed wholeness by run and slope. **Every Vertical ramp is identical (7 m, 43°) and they disagree: 2/4, 3/5, 1/3, 0/1.** |
| 4 | **NavMesh tiling** — the default 256-voxel tile is ~42.7 m, *smaller* than these dungeons, so each baked as several independently-voxelised tiles stitched at their edges. A flat floor stitches across a seam fine; a **slope** must agree on span heights from both sides. | The only one killed by a **controlled test** rather than a bucket count: set `tileSize` 1024 (~170 m — one tile per dungeon, seams gone entirely) and re-baked all six. Wholeness came back **bit-identical** (2/4, 0/1, 3/5, 0/5, 0/1, 1/3). Override reverted. |

**Also measured 2026-08-08, all negative** (`RAMP CONTEXT` diagnostic): **yaw** — nearly every ramp
is 180° and the bucket splits *within itself* (2/4 vs 0/3 vs 3/5) · **overlapping colliders** —
almost every ramp reports exactly 6 · **voxel phase** — `0/4:3/12 1/4:0/6 2/4:6/13 3/4:3/7`; the
`1/4` bucket never being whole is the only flicker and rests on ~3 underlying ramps.

### What the data actually says

**The variable is PER-INSTANCE, not per-shape.** Identical geometry, different outcomes. Every
hypothesis so far has been a property of *the stair*, and the data keeps answering that the stair
is fine and its *situation* is not.

Also settled: **there is ONE defect, not two.** `top` seam joins track `whole` **exactly** on every
dungeon (2/4↔2/4, 3/5↔3/5, 1/3↔1/3, 0/5↔0/5) — a fragmented ramp has no navmesh at its top to path
*from*, so the "top seam" is a symptom. Bottoms are largely fine (4/4, 4/5, 3/5, 2/3).

And it explains the apparent contradiction in `dg_bonecrypt`: two ramps there **do** work floor to
floor, but the **first** descent out of the entry is a broken one, so the working stairs sit behind
it unreachable. **Reachability is gated by the first failure on the path, not by the average.**

### Where to go next — the cheap moves are spent

The four candidates listed here as "unmeasured" on 08-07 (voxel phase, yaw, neighbouring geometry,
tiling) **have now all been measured, and all came back negative.** Nothing cheap remains.

**Four rounds of correlation have each cost one bake and each returned nothing. That pattern is
itself the signal** — a 15-ramp sample bucketed against scalars cannot resolve this. The next move
is to stop correlating and *look*: dump the actual navmesh triangles over one known-bad ramp
(`NavMesh.CalculateTriangulation`, filtered to the flight's bounds) and read **where** the strip
breaks. One picture of a real failure beats a fifth scalar.

**★ That move is now a ticket: `WorkOrders/WORK_ORDER_927_pathpartial_seam_revalidation.md`** (owner-authored
2026-08-08). It captures ground truth on **one** failing seam — both attachment points' world coords, the
residual delta vector, connector local/lossy scale, mesh bounds, world span, a connector-disabled check, and
the triangulation dump — rather than bucketing a population against a fifth scalar. **Measure the FIRST
failing descent out of the entry**, not a convenient one (see the `dg_bonecrypt` note above).

⚠ **Fallout the WO also records:** `WorkOrders/DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md` §5.5.2 justified the
whole stitch/connector architecture on hypothesis #1 — *"today's `PathPartial` is an erosion problem, the top
landing is 0.80 m against a 1.00 m slot."* That premise died here. §5.5.2 is now **struck** and the file
carries a hypothesis-killed banner. The architecture may still be right; it needs a new reason.

### The diagnostics are permanent — start from them, not from source

Every bake now prints, unasked:
- `PATH DIES` — last reachable corner, distance short, **nearest room by id**, floor deltas
- `RAMP CARVE` — how many ramps carve, how many are whole end to end
- `RAMP SEAMS` — bottom→own floor and top→floor above, separately
- `RAMP SHAPE vs WHOLE` — wholeness bucketed by run and by slope
- `RAMP CONTEXT vs WHOLE` — by yaw, voxel phase, overlapping colliders
- `RAMP TILE` — by tile straddle and distance to the nearest tile edge

⚠ **The instruments have been wrong twice, and both times looked confident.** `ReportRampCarve`
first sampled the ramp's extreme tips (which overhang ~0.35 m past each nose for the landing seam)
and reported a uniform `0/N` everywhere; probing inboard gave the mixed truth. Probe radii are
deliberately opposite — **tight (0.35 m)** on the ramp so a hit cannot be the floor underneath,
**generous (6 m)** when finding a room's floor. Do not unify them.

---

## 3. New guards shipped (both proven RED before being trusted)

**Grid + floor-plane guard** — `GraphDungeonComposer`, asserted **before** the `RoundToInt` emit,
which is where the evidence is destroyed. Every solved room must already be an integer; every
non-vertical connector must sit at local Y 0. This file stated the invariant *in prose* since it was
written and nothing checked it; the 2026-08 drift fix was applied to the instance, not the class.
Vertical sockets are exempt and **the exemption is the point** — the 8 stair sockets at
±`FloorSeparationY/2` are the only connectors off the floor plane, and the connector-model redesign
deletes them.

**`[room-shell]` rework** — `RoomPrefabMeta` gains `floorShafts`/`ceilingShafts`; the oracle **samples**
the footprint at 0.25 m and asserts coverage outside a declared shaft **and** that the shaft is
genuinely open. Union-bounds was the cheap alternative and **would have passed the ring-ceiling bug
found the same day** — a perimeter `Ceil_N/S/E/W` whose union covered the room while its centre was
open to sky. Union bounds cannot see a hole.

---

## 4. Also landed 08-07

- **WO-853 §7 ruled + shipped** — raid destruction **50 spire / 30 structures / 20 garrison**. §1's
  targetability seam was already fixed; scoring was the last piece. Nothing pinned the split before.
- **WO-912 D2 SETTLED = Unity LevelPlay** — by *eligibility*, not preference: AppLovin will not
  onboard without a published store listing. Q2a/Q3a moot. **D3 still blocks the SDK** until Unity's
  Regulated Activities pre-approval returns **in writing**. Draft ready in
  `WorkOrders/WORK_ORDER_912_UNITY_PREAPPROVAL_REQUEST.md`; org `samanthadenelle`, project
  `435f5e1e-b8bf-4f9f-8143-7d5eca669c67`. Three ad units created and recorded.
- **Ad placements purged** — `place.store.crystals` paid **150 crystals** for an ad view and was
  ENABLED. Crystals are the SKR on-ramp; that is exactly the convertibility AdMob and Unity prohibit,
  and it was live while the pre-approval was being drafted. Guarded by `AD_COVENANT_OK`.
  Owner then re-enabled harvest doubler + daily chest (D4 reversed) — both legal, crystals stayed out.
- **Wave 1 step 2/3** — composed dungeons and the hand-coded outpost relit and enclosed; outpost
  floor z-fight fixed and `NAV_OK 5/5` (cause was `BuildChoke` splitting on the wrong axis, giving two
  parallel barriers with no door — pre-existing, revealed by a check that did not exist before).
- **WO-920 camera** — the WO described the pipeline the owner is *not* looking at. Composed dungeons
  and the outpost bake **no camera at all**; they inherit the village `SmartMobileCamera`.

---

## 5. Open, needing the owner

- **#51** stairs (above) · **#42** WO-874 `EliteVFXController` — 13 references, **never attached**
- **Send the Unity pre-approval** — the one action that unblocks D3
- **#49** pin `AndroidTargetSdkVersion` (currently `0`, device is API 36) **before** any ad SDK
- Known-visible and unfixed: the green legendary gate / EXIT beacons render **Unlit** and read as neon
  against the new dark ambient (**WO-924** minted for it) · a green cast on upper wall surfaces,
  undiagnosed · the composed ceiling hole, unconfirmed since the overview capture is now dark-on-dark
