# WORK ORDER 1016 — HIGHEST: hero locomotion is dead in dungeons (slides in idle; vel always 0.00)

**Status:** READY TO IMPLEMENT — **P0 BLOCKER (escalated 2026-08-10: hero cannot move AT ALL, hub + dungeon; blocks WO-1014 walk beat). Was HIGHEST per owner ruling F8 seq=2312.**
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1016 → 1017 in the same edit
**Lane:** Hero locomotion / animation. Gameplay-felt P0.
**Provenance:** owner F8 capture **seq=2312**, 2026-08-10 18:33, scene `Dungeon_HealersCottage`,
verbatim: *"This problem gets marked as Highest on the board. Everything is wrong check locomotion."*
Capture file: `logs/f8-inbox/capture-20260810-183326.md`.

---

## 1. RCA — from the CAPTURED DATA (§12; no code-read theory required to locate this)

The auto-harvested trace proves the defect in two lines that disagree with each other:

**A. The hero IS moving through the world.** `[Flow:Zone]` reports the hero's position advancing across
the capture window:
```
[Flow:Zone] GetZone(x=-28.0,z=-1.8).      <- early, repeated
[Flow:Zone] GetZone(x=-26.9,z=-4.7).      <- later (last line of the capture)
```
…and `[Flow:GaitF]` shows the body rotating: `yaw=270 dYaw=0.0` → `yaw=42 dYaw=12.0`.

**B. Locomotion believes it is standing perfectly still — every single frame.**
```
[Flow:HeroLoco] vel=0.00 m/s | clips=[mixamo.com(w=1.00,len=3.63s)] | baseState hash=-2089788878
                | avatar=MageAvatar | controller=Mage
[Flow:GaitF]    vel=0.00@0deg ... clip=mixamo.com(1.00) speedP=0.00 skate=0.00
```
`vel=0.00` appears on EVERY `HeroLoco` line in the capture. `speedP=0.00` means the animator's speed
parameter is never driven, so the state machine never leaves its base state — and the clip list only
ever contains **ONE clip at weight 1.00**, `mixamo.com` (len 3.63s), for the whole capture.

**⇒ The hero translates through the dungeon while the animator plays a single idle clip: a slide/skate.**
That is the owner's "everything is wrong."

**The defect is the VELOCITY SOURCE, not the animator.** Whatever moves the hero in the dungeon scene is
not the thing `HeroLocomotion` reads velocity from. ⚠ **Do not trust the class header** — canon warns
that `HeroLocomotion`'s "pure transform" comment hides that it is a `NavMeshAgent` (CLAUDE.md mandatory
read). The prime hypothesis to TEST FIRST: in dungeons the hero is driven by direct transform movement
(or a different controller/rig path) while velocity is still read from the NavMeshAgent, which reports
0 because the agent is not the mover here. **Confirm with a capture before editing** (§12).

**Secondary finding (do not conflate):** `clips=[mixamo.com(...)]` — the base clip carries the raw
Mixamo export name. Whether the Mage controller in this scene even HAS a locomotion blend tree needs
verifying: if the controller only holds one state, no velocity fix alone will animate a walk. Check both.

**Out of scope / noise:** the Editor.log `NullReferenceException` during "exit code reload scopes
(post serialization)" is an editor domain-reload/shutdown error, not this defect. Note it, do not chase
it here.

## 1b. COMPANION DEFECT — the dungeon CAMERA is frozen too (F8 seq=2313, same session)

Owner flagged **seq=2313** 22 seconds later, same scene: *"No camera movement."* The captured proof is
already in BOTH captures' `[Flow:GaitF]` lines — the camera yaw never changes, on any line, in either
capture:
```
[Flow:GaitF] ... yaw=270 dYaw=0.0  camYaw=180 dCam=0.0     <- seq 2312
[Flow:GaitF] ... yaw=79  dYaw=0.0  camYaw=180 dCam=0.0     <- seq 2313
[Flow:GaitF] ... yaw=72  dYaw=0.0  camYaw=180 dCam=0.0     <- seq 2313, hero has moved AND turned
```
`camYaw` is pinned at **180** with `dCam=0.0` throughout, while hero yaw swings (270 → 79 → 72 → 42) and
hero position advances. **The camera rig is not following or rotating in dungeons.**

Filed HERE, not separately, because it is the same scene, same session, and almost certainly the same
root class as §1 — *the dungeon's rig wiring does not drive the things it drives elsewhere.* Fix them in
one pass, but **prove them independently** (a camera that follows is not proof that locomotion animates,
and vice versa). Anchor: `DungeonCameraRig`. Same §12 rule — instrument the rig's follow/look path, read
which input is dead, THEN fix.

**Acceptance (camera):** in a dungeon, moving the hero changes `camYaw`/`dCam` in the trace and the view
follows; verified in the same capture as the locomotion proof; swept across the other scenes.

## 1c. ⛔ ESCALATION 2026-08-10 — NOBODY MOVES, INCLUDING THE HERO. THIS IS A P0 BLOCKER.

Owner, verbatim (hub scene `Main_Castle_Overworld`, same session as F8 2316–2318):
**"NPC and Pet Neither move, even hero cannot move"**

This is **larger than the dungeon animation defect this WO was opened for**:
- Not just the dungeon — **the HUB scene too**.
- Not just the animator — **the hero does not TRANSLATE AT ALL**. §1's original finding was "position
  advances while `vel=0.00`" (a *feed* bug). This is worse: **no movement happens.**
- The guide and the NPC are frozen as well, so it is **not hero-specific** — it is whatever drives
  agent/character movement in that scene, for every actor.

**⇒ This is the root cause of the tutorial STEP-STUCK** logged in WO-1014 §1d: `founding_walk` waits
120s for `hero.reached:guide_gate` and times out **because nothing in the scene can walk**. Fixing the
walk beat is pointless until this is fixed. **WO-1014 §2d is BLOCKED ON THIS WO.**

**Priority: HIGHEST/P0 — a game where the hero cannot move is unplayable.** This outranks every other
open item in the current queue (WO-1010/1012/1014/1015/1019 all assume a movable hero).

**Diagnosis order (§12 — instrument, do not guess):**
1. Capture the hub scene and read whether input reaches the mover at all: input → locomotion → agent.
   Log `agent.enabled`, `agent.isOnNavMesh`, `agent.isStopped`, `Time.timeScale`, and the input vector.
2. **Prime suspects to TEST, cheapest first:**
   - **`Time.timeScale == 0`** — a modal/tutorial pause left set (the tutorial's `pausePressure`,
     a panel, or the coach beat). This would freeze hero AND pet AND NPC simultaneously — exactly the
     reported symptom, and the single most likely cause of "nothing at all moves".
   - **Input suppressed by tutorial chrome** — `FocusMask` raycast-blocking (WO-1012 spec'd
     input-blocking outside the cutout!) swallowing joystick input. ⚠ HIGH suspicion: the mask shipped
     this session, and `style=Gesture` was active in the seq-2316 trace.
   - **NavMesh missing/unbaked** in the hub → every agent immobile.
   - `agent.isStopped` left true by a tutorial/leash step.
3. The tutorial FocusMask suspicion is testable in seconds: does movement work with
   `ff.tutorialv2` off / after the tutorial completes? If yes, the mask/input-gate is the culprit and
   the fix belongs to the WO-1012 kit, not the locomotion layer.

**Acceptance (this escalation):** hero moves in the hub with the tutorial ACTIVE and inactive; pet and
NPC path normally; `Time.timeScale == 1` during coach beats; a capture proves input → velocity → motion.
Add a regression that fails if input is non-zero while position is unchanged for N frames.

## 2. What to do

1. **Instrument the velocity path FIRST** (§12 — the hard gate). Add/extend `[Flow:HeroLoco]` to print,
   per frame: the mover in use (agent vs transform vs controller), `agent.enabled`, `agent.isOnNavMesh`,
   `agent.velocity`, the frame delta-position derived velocity, and which one feeds the animator's speed
   parameter. Run the dungeon headless and READ IT. The line that shows a non-zero delta-position beside
   a zero fed-velocity is the fix site.
2. **Fix at that site.** Whatever actually moves the hero in dungeons must be the velocity source that
   drives `speedP`. Prefer feeding the animator from a **measured world delta-position / deltaTime**
   (mover-agnostic) rather than binding to one mover — the same code then works in village AND dungeon,
   which is the whole class of bug here.
3. **Verify the controller has a locomotion blend tree** for the Mage rig in this scene; if the dungeon
   loads a reduced/other controller, that is a second fix.
4. **Sweep the other scenes** — village, overworld, Village2, raids. A mover/velocity mismatch is
   scene-shaped; prove locomotion animates in EVERY scene, not just the one that was flagged.

## 3. Acceptance criteria

- [ ] In `Dungeon_HealersCottage`, walking produces `[Flow:HeroLoco] vel > 0` and `[Flow:GaitF] speedP > 0`
      with a walk/run clip weighted in — captured, not eyeballed.
- [ ] No slide: the hero's animation matches its world movement (`skate` stays near 0 while moving).
- [ ] Idle → walk → run → idle transitions play in dungeons.
- [ ] The same capture proof for village + overworld + one raid scene (no regression, no other scene
      silently on the broken path).
- [ ] The velocity feed is mover-agnostic (documented in the file header — and the misleading
      "pure transform" header on `HeroLocomotion` is CORRECTED in the same commit, canon §15).
- [ ] `[Flow:HeroLoco]` retains the new diagnostic fields (§12: instrumentation is PERMANENT, never
      stripped).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`; add a regression that fails if fed-velocity is
      0 while delta-position is non-zero for N consecutive frames — this bug can never return silently.
- [ ] Owner felt-test in the dungeon: target verdict "it walks."

## 4. What NOT to touch

- The gait-forensics harness itself (`HeroGaitForensics` is the instrument that caught this — extend,
  never remove).
- Combat/BattleLock (the capture's "MELEE swing SUPPRESSED — no active battle" line is expected
  behaviour in an unstaged dungeon room, not a defect).
- Dungeon composition/scene bake, equipment socket scaling (`[Flow:Equip]` parent-scale lines are a
  separate concern).
