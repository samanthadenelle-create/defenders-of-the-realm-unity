# WORK ORDER 1298 — Owner flag: the hero slides through the castle gate with a dead animator

**Status:** READY TO IMPLEMENT
**Source:** F8 capture seq **4362** + **4363** (the owner's own flag). Ledger: `docs/qa/F8_TRIAGE_2026-09-02.md` §2.
**Silo:** Hero locomotion / castle gate seam
**Severity:** P0 — this is the owner's own flag, the highest-priority item in the 2026-09-01 backlog.

> ## ⚠ READ THIS BEFORE WRITING ANY CODE — THIS IS A *VERIFICATION* TICKET FIRST
> `WORK_ORDER_1295_continuous_castle_gate_traversal.md` retired `GateWarp` and the runtime
> `NavMeshLink`, and the owner **felt-verified it on 2026-09-02** — verbatim *"i can now go through
> gates normally"*. **The flagged session predates that fix** (2026-09-01 20:12 UTC) and its log still
> shows `[Flow:Seam] WarpTo`, which WO-1295 deleted. **It is entirely possible this defect is already
> closed.** Step 1 is to prove it either way with a *moving* hero. Do not open a second fix on the gate
> seam before that proof exists.

---

## Owner-facing symptom

The owner flagged the game 520 seconds into a `Main_Castle_Overworld` session while the founding
tutorial was pointing her at the west gate. At that moment her hero was travelling **west at 12–14.5
metres per second in an idle pose, with her input taken away** — a glide, not a walk. She wrote:

> *"Something is wrong here. PLease trace it"*

## Captured proving lines (§12 evidence — quoted verbatim)

**The flag itself** (`logs/f8-inbox/capture-20260902-013506-seq4362.md`):
```
{"kind":"flagged","message":"[Main_Castle_Overworld] Something is wrong here. PLease trace it",
 "stack":"","scene":"Main_Castle_Overworld","t":520.8158569335938,"utc":"2026-09-01T20:12:15.09…Z"}
```
Seq **4363** is the same instant (`t=520.8201293945313`) from the on-screen mobile FLAG button.

**What the log says was happening** — from the Session-A `Player.log` tail carried in the harvest of
seq 4355–4364 (see the drain caveat in the ledger §1: this tail covers roughly the final ~90 s of the
session, which brackets `t=520.8`):

```
[Flow:Seam] WarpTo sample HIT @ (-34.00, 0.08, 0.00) dist=0.00 (req (-34.00, 0.08, 0.00)) scene='Main_Castle_Overworld'
[Flow:Seam] WarpTo post-warp: agent.isOnNavMesh=True @ (-34.00, 0.08, 0.00)
```
```
[Flow:HeroOwner] ANIMATION-VELOCITY STALL: root travelled 14.53 m/s but Animator Speed=0.00 (velSelf=0.00).
  The animator is being fed a DEAD value — a mover other than this component wrote the transform and
  nothing re-published its speed.
```
```
[Flow:HeroOwner] scene='Main_Castle_Overworld' owner=HeroLocomotion ownerCC=none ownerAgent=on-mesh
  scriptedMove=off velSelf=0.00 velRoot=14.49 animFeed=velSelf animSpeed=0.00 rootYaw=270.0
  basis=SmartMobileCamera.CameraYaw basisYaw=238.3 timeScale=1.00 dt=0.0380 inputSuppressed=True
  autoWalk=False mainCamYaw=222.3 pos=(-39.50, 0.08, 0.00)
```
```
[Flow:Tutorial] FocusMask resolved highlightId=world.gate_direction target=WaveSpawnPoint-W style=Glow rect=(962,254,120,120)
```

Positions in that window run x = **-34.00 → -35.10 → -39.50 → -43.35** at z=0, `rootYaw=270.0` — due
west, straight out of the west gate, which is exactly where the tutorial FocusMask was pointing her.

**Secondary, same tail, unowned:**
```
[Flow:UI] TextFitGuard [DialogueViewUI/ObsidianPanel/PanelContent/Zone_Header/Label]: armed but text
  still EMPTY after 600 frames — standing down (a blank plate here is a TEXT-NEVER-SET bug, not a fit bug)
```

## Suspected seam

- `Assets/_Modules/Village/Hero/HeroLocomotion.cs:1690` — where the stall is *reported*. The report is
  correct; the defect is upstream of it.
- The report's own diagnosis names the shape: *"a mover other than this component wrote the transform
  and nothing re-published its speed."* With `scriptedMove=off`, `autoWalk=False`, `velSelf=0.00` and
  `velRoot=14.49`, **no component is claiming the movement**, yet the transform moved. The `WarpTo`
  lines immediately upstream identify the mover as the (now-retired) gate seam warp.
- `HeroLocomotion.cs:1694` onward — the "Manual NavMeshLink traversal (WO-468)" block — is the
  surviving code that slides the hero across a seam in-world. If any path there still writes the
  transform without republishing `Velocity`, the stall survives WO-1295.

## Acceptance criteria

1. **Proof first, fix second (§12).** Produce a captured run in which the hero **walks (moving, not
   stationary) out of each of the four castle gates** on current HEAD, and show either:
   - **(a)** zero `ANIMATION-VELOCITY STALL` lines and zero `[Flow:Seam] WarpTo` lines across all four
     crossings — in which case this WO closes as *already fixed by WO-1295*, with the capture attached
     as the proof; **or**
   - **(b)** the stall reproducing, with the `[Flow:HeroOwner]` state line naming which owner field is
     lying (`velSelf` vs `velRoot`, `scriptedMove`, `autoWalk`).
   `Assets/_Modules/DevTools/GateTraversalProof.cs` already drives the hero across the gates and is the
   obvious host for this assertion — **but note WO-1295's own warning that it moves the hero with
   `agent.Move(outward * 0.55f)`, not the player-input path.** The stall is only meaningful for a hero
   moving at run speed, so the proof must drive it fast enough that `IsAnimationStalled` can trip.
2. If (b): whatever writes the transform re-publishes speed into the animator feed in the same frame,
   so `animSpeed` tracks `velRoot`. Add a regression under `Assets/Editor/Regression/` that fails when
   `velRoot > 1 m/s` while `animSpeed == 0` for more than 0.5 s of a scripted crossing.
3. The `ANIMATION-VELOCITY STALL` FlowTrace at `HeroLocomotion.cs:1690` **stays in the code** either
   way (CLAUDE.md §12 — instrumentation is permanent).
4. **Secondary:** identify which zone the empty `DialogueViewUI/…/Zone_Header/Label` belongs to and
   either set its text or stop arming the plate. One line; do not let it grow into a UI refactor.
5. The owner felt-verifies and closes (§13). Do not mark this DONE from a headless run.

## What NOT to touch

- ⛔ **`Assets/_Modules/Village/World/GateTraversalInjector.cs`** — WO-1295 is live in the working tree
  and another seat owns that file. Do not edit it, do not revert it, do not re-add `GateWarp` or the
  runtime `NavMeshLink`.
- ⛔ **Do not re-open the gate geometry, the navmesh bake, or `SyntyCastlePerimeterBuilder.cs`.** The
  owner has already felt-verified gate traversal. This ticket is about the *animator*, not the *nav*.
- ⛔ Do not touch the enemy-pathing half of WO-1295 (`NavMesh.CalculatePath == PathComplete`); that is
  still open under WO-1295 and stays there.
- ⛔ Do not strip or disable any `[Flow:HeroOwner]` / `[Flow:Seam]` trace (§12).
- ⛔ Do not re-scope this into a hero-locomotion refactor. If step 1 returns (a), close it.
