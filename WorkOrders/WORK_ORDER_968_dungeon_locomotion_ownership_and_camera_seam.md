# WORK ORDER 968 — Dungeon locomotion: mover ownership, dead camera basis, frozen camera  — **OWNER CLOSED 2026-08-22** (felt-verified by the owner; PO closes, section 13).

**Status:** DONE — shipped `e3539f5b` (instrumentation) + `5e464578` (camera heal) + `c7761156` (PROVEN by a headed re-run). All four seams verified against `docs/proof/2026-08-10-dungeon-headed-AFTER-camera-fix/`: ONE transform owner, the animator tracks real movement, the stick basis is correct with no 180 inversion, and the camera heal fired once and worked. The **camera FRAMING** question the working camera then exposed is a SEPARATE ticket — **WO-980**, not this one. Owner felt-verify owed; RESULT file still owed (not fabricated). *(Status corrected 2026-08-14: the line still read READY after all three commits landed.)*

> ### VERIFIED AT SOURCE 2026-08-22 (status audit)
> All named fixes are present - `Assets/_Modules/Dungeons/DungeonCameraRig.cs` carries the WO-968 seam work at
> `:167`, `:270-300`, `:599` and `:809`; the mover-ownership half is in `DungeonController.cs:66` and `:199-202`.
> **AND** both symptoms are now structurally unreachable - see the `Scene:` annotation above.
> Owner felt-verify still owed (PO closes, CLAUDE.md 13).

**Priority:** **HIGHEST** (owner ruling, F8 seq 2312)
**Scene:** `Dungeon_HealersCottage` - **LEGACY / SUPERSEDED PIPELINE (verified 2026-08-22).** This is the
**hand-built** dungeon scene. The game now loads **COMPOSED** dungeons, which carry the **town hero** across
(`Assets/_Modules/Dungeons/ComposedDungeonHost.cs:13-23` and `:92-99` -
`GameObject.FindGameObjectWithTag("Player")`, the carried hero winning `HeroControlEnsurer.DedupeHeroes`).
`ComposedDungeonHost.cs` and `ComposedDungeonBootstrap.cs` contain **zero** references to `DungeonHero` or
`DungeonCameraRig` (grep, 2026-08-22), so **both symptoms in this ticket are structurally unreachable on the
composed path.** Do not re-triage this scene - it is not what the game loads.
**Silo:** Dungeons / Hero locomotion / Dungeon camera
**Raised by:** owner F8 seq **2312** + **2313**, 2026-08-10, ~18:33 local
**Related but SEPARATE:** WO-966 (Mage body-yaw constant). See §5 — do not conflate.

---

## 1. The owner's words (verbatim, both captures)

> **F8 seq 2312:** *"This problem  gets marked as Highest on the board. Everything is wrong check locomotion"*

> **F8 seq 2313** (22 seconds later, same scene): *"No camera movement"*

Standing order for this session: **everything proven with DATA, no guesses or hunches.**

---

## 2. Evidence ledger — every claim tagged

`[C]` = **PROVEN BY THE CAPTURE** (a line exists in the harvested log / on-disk data).
`[S]` = **READ AT SOURCE** this session (file:line opened, not inferred).
`[?]` = **NOT PROVEN — unobservable in the current trace.** These are why §6 ships instrumentation.

### 2.1 The hero moves, and two different components can move it

| # | Claim | Tag | Proof |
|---|---|---|---|
| E1 | The hero's world position changed during the seq-2312 window: `(-28.0, -1.8)` -> `(-26.9, -4.7)` | `[C]` | `[Flow:Zone] GetZone(...)` x2 in the harvest |
| E2 | Those x/z ARE the hero root. The only unguarded per-second `GetZone` caller alive in a dungeon reads `HeroLocomotion.transform.position` | `[S]` | `ZoneManager.cs:56-62` (the throttled emitter); caller `TutorialFlow.cs:274-281`. The other candidate (`TutorialFlow.cs:1638`) returns before `GetZone` on `!OuterWorldLoaded()`, and `OverworldEncounterSpawner`'s loop is gated — `[C]` `[Flow:Encounter] MaintainLoop gated: overworld-not-loaded` |
| E3 | In the seq-2312 window `HeroLocomotion.Velocity` was **0.00** every sample, while E1 was happening | `[C]` | `[Flow:HeroLoco] vel=0.00 m/s` x6, emitted at `HeroLocomotion.cs:1315-1318` `[S]` |
| E4 | In the same scene, at other moments, `HeroLocomotion.Velocity` was **5.00 m/s with live player input** | `[C]` | `[Flow:HeroDrift] input=(0.00,1.00) vel=(0.000,5.000)` |
| E5 | E4 **proves the dungeon neutralize was OFF at that moment.** `DungeonController.EnsureSingleDungeonMover` calls `HeroLocomotion.SetScriptedMove(Vector2.zero)` every Update; `ReadMoveInput` returns that override verbatim. A zeroed override makes `input.y > 0.5f` impossible, and that comparison is the **entire gate** on the `[Flow:HeroDrift]` block | `[S]` | `DungeonController.cs:824`; `HeroLocomotion.cs:1517`; gate at `HeroLocomotion.cs:1065` |
| E6 | E3 is consistent with the neutralize being **ON** at that moment (input forced to zero -> `Velocity` decays to 0) | `[S]` | same lines |
| E7 | Therefore the hero's mover **changes during a single session** and nothing in the log says which is live | `[C]`+`[S]` | E3 vs E4 |
| E8 | `DungeonHero` (CharacterController) IS present on this scene's Keeper — not an assumption | `[S]` | `Dungeon_HealersCottage.unity`, `DungeonController` instance: `_hero: {fileID: 547342961}`, `_heroController: {fileID: 547342958}`, `_cameraRig: {fileID: 1877637690}` |
| E9 | A `[Flow:GaitF]` sample shows `dYaw=12.0` at `fps=60`. `720 deg/s * (1/60) = 12.0` — exactly `DungeonHero._turnSpeed`'s per-frame cap, i.e. **DungeonHero was the rotation writer on that frame** | `[C]`+`[S]` | `[Flow:GaitF] ... dYaw=12.0`, `[Flow:Perf] fps=60`; `DungeonHero.cs:66` (`_turnSpeed = 720`), `FaceHeading` at `:503-514`. Caveat: `HeroLocomotion.ApplyLockFaceYaw`'s cap is also `12 * 60 * dt = 12.0` (`:197`), but it requires `FeatureFlags.LockOn` **and** a live locked enemy, and the capture reads `enemies=0` `[C]` — so lock-face cannot be engaged and `FaceHeading` is the writer |
| E10 | `FaceHeading` only writes yaw when `_planarVelocity.sqrMagnitude >= 0.0025` — so E9 additionally proves **DungeonHero's own planar velocity was non-zero** on that frame, while `HeroLocomotion.Velocity` read 0.00 | `[S]` | `DungeonHero.cs:505-507` |

**E9 + E10 + E3 together are the decisive pair:** on those frames DungeonHero was moving and turning the hero, and the component that feeds the animator reported zero.

### 2.2 The animator is fed from the wrong component

| # | Claim | Tag | Proof |
|---|---|---|---|
| E11 | `ActorAnimator` is the **SOLE** writer of the Animator `Speed` param from `HeroLocomotion`; the legacy raw `SetFloat` was deliberately retired | `[S]` | `HeroLocomotion.cs:1107` (`_actor?.SetLocomotion(...)`), retirement note `:1263-1269`, write at `ActorAnimator.cs:113-118` |
| E12 | It is fed `HeroLocomotion.Velocity.magnitude` — i.e. **this component's own idea of speed**, not the root's measured speed | `[S]` | `HeroLocomotion.cs:1107` |
| E13 | `DungeonHero`'s competing `SetFloat(Speed, ...)` can be a permanent no-op: `_animator` and `_hasSpeedParam` are resolved **once, in Awake**, and never re-resolved — while `HeroBodySwapper` rebuilds the rig asynchronously *after* that | `[S]` | `DungeonHero.cs:138-149` (Awake-only resolve), guarded write at `:239-240`; swap timing `DungeonController.cs:732-733, 753` ("HeroBodySwapper injects ~160 ms later") |
| E14 | The capture's animator state is a single idle clip at full weight throughout the standing window | `[C]` | `clips=[mixamo.com(w=1.00,len=3.63s)]`, `speedP=0.00` |
| E15 | Contrast: `HeroLocomotion` DOES self-heal its animator across a body swap (`ResolveAnimator` + `SetAnimator` re-scan params). `DungeonHero` has no equivalent. Same bug shape, one component patched, the other not | `[S]` | `HeroLocomotion.cs:658-716` vs `DungeonHero.cs:138-149` |

### 2.3 The camera-relative movement basis is dead in a dungeon

| # | Claim | Tag | Proof |
|---|---|---|---|
| E16 | `[Flow:HeroDrift]` reports **`camYaw=0.0` on EVERY line** in the dungeon | `[C]` | owner's Player.log, all sampled lines |
| E17 | That `camYaw` is **not** the camera's transform yaw — it is `SmartMobileCamera.CameraYaw`, and it degrades to a literal `0f` when no `SmartMobileCamera` exists | `[S]` | `HeroLocomotion.cs:861-864` (`_smartCamera != null ? .CameraYaw : 0f`), printed at `:1093` |
| E18 | **There is no `SmartMobileCamera` in `Dungeon_HealersCottage`** — zero references to its script GUID `fbe5788c0485400459d4d3c3808798ba` in the scene file, and nothing runtime-adds one | `[S]` | scene grep (count 0); no `AddComponent<SmartMobileCamera>()` anywhere in `Assets/` |
| E19 | Therefore, whenever `HeroLocomotion` is the live mover in a dungeon, **the player's stick/WASD is interpreted world-absolute**: "forward" means world +Z regardless of where the camera is pointing | `[C]`+`[S]` | E16 + E17 + E18 |
| E20 | `DungeonHero` uses a **different** basis for the same stick — it projects `_moveCamera` (`Camera.main`) onto the floor plane | `[S]` | `DungeonHero.cs:318-329, 343-357, 376-390` |

**E19 + E20 is the single best explanation of "everything is wrong":** the two possible movers interpret the identical input in two different frames of reference, and one of them has no frame of reference at all.

⚠ Note the **naming collision that has already misled this investigation**: `[Flow:GaitF] camYaw` is `Camera.main.transform.eulerAngles.y` (`HeroGaitForensics.cs:137`) `[S]`, while `[Flow:HeroDrift] camYaw` is `SmartMobileCamera.CameraYaw` (`HeroLocomotion.cs:862`) `[S]`. **Two different quantities printed under one name.** GaitF read 180, HeroDrift read 0; both are correct readings of different things. Renaming the HeroDrift field to `basisYaw` is part of this WO.

### 2.4 The frozen camera (F8 seq 2313)

| # | Claim | Tag | Proof |
|---|---|---|---|
| E21 | `Camera.main`'s yaw was **constant 180.0, dCam=0.0**, across a window in which the hero both translated (E1) and turned (yaw 270 -> 42) | `[C]` | `[Flow:GaitF] ... yaw=270 ... camYaw=180 dCam=0.0` and `... yaw=42 dYaw=12.0 camYaw=180 dCam=0.0` |
| E22 | 180 is **exactly the bind-time seat**: the layout spawns the Keeper at `facingY: 90`, and `EnsurePivot` stamps `_headingYawOffset` (default **+90**) as the pivot's local yaw. 90 + 90 = 180 | `[S]` | `Assets/Resources/Data/Canonical/dungeons/healers-cottage.json` -> `spawn.facingY: 90`; `DungeonCameraRig.cs:445` + `:142` |
| E23 | So the camera is **parked at the pose `SeatThirdPersonImmediate` gave it on Bind** and has not tracked since | `[C]`+`[S]` | E21 + E22 + `DungeonCameraRig.cs:452-474` |
| E24 | It is **not** merely sitting at its authored scene pose — the authored `Main Camera` yaw is **0**, not 180 | `[S]` | `Dungeon_HealersCottage.unity`, Transform `&476083669`: `m_LocalRotation {x:0.438, y:0, z:0, w:0.899}` (pitch 52, yaw 0) |
| E25 | A `CinemachineBrain` and the `DungeonCameraRig` vcam both exist in the scene; `_cameraRig` is wired on `DungeonController`, so `Bind(_hero)` has a live path | `[S]` | scene: brain on `Main Camera` (`&476083666`), `DungeonCameraRig` on `FollowCameraRig` (`&1877637690`), `DungeonController._cameraRig: {fileID: 1877637690}`; call site `DungeonController.cs:895-899` |
| E26 | **WHY it stopped following is NOT PROVEN.** This rig logs only at Bind (t~0); the harvest is a 60-line tail at t~1698 s, and it contains **zero** `[Flow:DungeonCam]` lines | `[?]` | absence in the harvest + `DungeonCameraRig` has no per-frame trace (before §6) |

**Ranked candidates for E26** (each separated by one read of the new heartbeat, §6):
1. **The pivot was destroyed.** `EnsurePivot` creates `DungeonOTSPivot` and parents it **under the hero**; the async `HeroBodySwapper` rebuilds the hero's children *after* the dungeon binds. A destroyed Follow target makes the body stage a no-op and the rig holds its last pose. **This is the exact same failure shape as E13** — a dungeon component wires itself to the pre-swap rig and never re-resolves.
2. `_camera.Follow` was never set (Bind not reached on this path).
3. The vcam is solving but is not the brain's live camera / is disabled.
4. The rig transform tracks but nothing copies it to the rendering camera.

---

## 3. Root-cause statement

**The dungeon has no single, observable owner of the hero transform, and every downstream consumer is wired to the wrong source.**

Concretely, three seams, one shape:

- **S1 — Ownership flips silently.** `EnsureSingleDungeonMover` neutralizes `HeroLocomotion` by a **static** side-channel (`SetScriptedMove(zero)` + `GroundSnapEnabled=false`) that logs **once** on apply and once on restore. The capture proves both states occurred in one session (E3 vs E4) and no line names which is live. While un-neutralized, `HeroLocomotion` translates the root directly (`transform.position += step` when the agent is disabled/off-mesh, `HeroLocomotion.cs:978-982` `[S]`) **and** writes root yaw by slerp, while `DungeonHero` writes root yaw with a `-90` model offset (`DungeonHero.cs:510-511` `[S]`) — two movers, two rotation writers, one transform.
- **S2 — The animator is fed a component, not the world.** `Speed` comes from `HeroLocomotion.Velocity` (E11/E12), which is dead by design whenever the dungeon neutralize is on; `DungeonHero`'s competing write can be a permanent no-op (E13). Neither path publishes what the root ACTUALLY did.
- **S3 — The movement basis is dead.** With no `SmartMobileCamera` in a dungeon, `HeroLocomotion`'s camera-relative conversion silently becomes identity (E16-E19), while `DungeonHero` uses `Camera.main` (E20). And the camera itself is frozen at its bind seat (E21-E23), so even the good basis would be stale.

---

## 4. Blast radius — is the town affected?

**Dungeon-only, on every seam in this ticket.** Stated per seam so a fix is not over-scoped:

| Seam | Town / `Main_Castle_Overworld` | Why |
|---|---|---|
| S1 ownership | **NOT affected** | `DungeonHero` + `EnsureSingleDungeonMover` exist only in dungeon scenes; in town `HeroLocomotion` is the sole mover, on a baked navmesh |
| S2 animator feed | **NOT affected in practice** | In town `HeroLocomotion.Velocity` *is* the truth, so feeding it to `Speed` is correct. The latent hole (feed = component, not world) is the same everywhere, but only a foreign mover exposes it |
| S3 camera basis | **NOT affected** | Town has a `SmartMobileCamera`, so `CameraYaw` is real |
| Frozen camera | **NOT affected** | `DungeonCameraRig` exists only in dungeon scenes |
| WO-966 body yaw | **AFFECTED EVERYWHERE** | It is a mesh-level constant — see §5 |

Also in scope for a re-check, **not** proven here: the **composed `dg_*` dungeons** bake the hero with `HeroLocomotion` + `HeroBodySwapper` **only** and no `DungeonHero` (per WO-967's source read of `DungeonBaker.cs:1168-1187`). If that is still true, then in composed dungeons `HeroLocomotion` is *always* the mover with an *always* dead basis (S3 permanently on), and `DungeonCameraRig.Start`'s fallback bind — `FindAnyObjectByType<DungeonHero>()`, `DungeonCameraRig.cs:262-267` `[S]` — finds **nothing**, so no bind happens at all. Verify with the §6 heartbeat before acting.

---

## 5. Relationship to WO-966 — INDEPENDENT, and they stack

**They are two different facing defects. Fixing one will not fix the other, and adjusting one to compensate for the other is forbidden.**

- **WO-966** is a **constant, mesh-level** fault, measured not guessed: `HeroFacingAudit.MeasureAll` says the Mage FBX needs **+4.5 deg** to face +Z while `HeroBodySwapper.cs:263` applies **-90** to every non-Knight body — a fixed **94.5 deg** error, present in **every scene including town**, on a **child** transform (`HeroBody`).
- **WO-968** (this) is about **which component writes the ROOT transform** and **what frame of reference the input is converted in**. It is **dungeon-only** and **variable**.

They stack: in a dungeon, `DungeonHero.FaceHeading` applies a **further** `-90` to the **root** (`DungeonHero.cs:510-511`), on top of the swapper's `-90` on the body. A dungeon Keeper moved by `DungeonHero` therefore carries the WO-966 mesh error **plus** an extra root offset that the town hero does not have.

**On the `dYaw` swings** (`-58.6, -48.8, -23.3, -56.4, +29.8, +16.4, +32.9`): these are **explained and are NOT a third defect.** `[Flow:HeroDrift]`'s `dYaw` is `Mathf.DeltaAngle(camYaw, transform.eulerAngles.y)` (`HeroLocomotion.cs:1094` `[S]`) — and `camYaw` is **0** here (E16-E18). So `dYaw` is simply the root yaw wrapped to +/-180, and it checks out exactly against the logged root yaw on every line (`heroYaw=337.6 -> dYaw=-22.4`; `303.6 -> -56.4`; `311.2 -> -48.8`). It swings with input because the root is slewing toward a **world-absolute** heading — i.e. `dYaw` is a **symptom of S3**, not evidence of a body-vs-travel error. It is not a body-yaw measurement at all and must not be used as one.

---

## 6. Instrumentation — ALREADY LANDED (§12: earn the edit)

Three permanent heartbeats, all gated on `FlowTrace.Enabled` (zero cost off), none replacing or removing any existing trace:

1. **`[Flow:HeroOwner]`** — `HeroLocomotion.LateUpdate` (new; `LateUpdate` deliberately, because `Update` has several early-returns and those frames are exactly the ones worth reporting). Prints per second: `scene`, `ownerCC` (the existing `ForeignMoverOwnsTransform` capability check, printed), `ownerAgent`, `scriptedMove` (the dungeon neutralize gate), `velSelf` (this component's `Velocity` — what feeds `Speed`), **`velRoot` (MEASURED delta-position speed — what actually happened)**, `animSpeed`, `rootYaw`, `basis` + `basisYaw` (and **where the basis came from**), `mainCamYaw`, `pos`.
   Plus a **named failure line**: `ANIMATION-VELOCITY STALL` fires when `velRoot > 0.5` while `animSpeed < 0.1`.
2. **`[Flow:DungeonMover]`** — `DungeonHero.Update`. Prints `planarVel`, `inputEnabled`, `cc` state, `tapTarget`, `yaw`, `pos`, **`animator` (name or `<null/destroyed>`)**, `hasSpeedParam`, `animSpeed`, `moveCam`, `camYaw`. Plus a named failure line when it is moving with a dead Animator handle (E13).
3. **`[Flow:DungeonCam]`** — `DungeonCameraRig.LateUpdate`, before the FPV early-return. Prints `mode`, `fpv`, `combatFraming`, `vcam` enabled state, **`Follow` target name or `<null>`**, **`pivot` alive/destroyed + its world yaw**, bound hero pos/yaw, rig pos/yaw, `mainCam` name/pos/yaw, `brain present/ABSENT`, `headingYawOffset`.

Also added: `HeroLocomotion.ScriptedMoveActive` (read-only static) — the neutralize gate was previously unobservable from outside.

**One capture of a dungeon walk now answers, in three lines, every `[?]` in §2.** Take that capture before any behavioural edit.

---

## 7. The fix — one line per candidate, in this order

**Ordering is load-bearing. See §8 — a partial fix here actively masks the rest.**

- **F0 (do first, no code):** run one dungeon walk with §6's heartbeats and read them. This resolves E26 and tells you whether S1 is "neutralize off" or "arena flag stuck".
- **F1 — Fix the FEED, not the readout.** When `ForeignMoverOwnsTransform()` is true, `HeroLocomotion` must publish the **measured root speed** (delta position / dt, the `velRoot` the heartbeat already computes) to `ActorAnimator.SetLocomotion` instead of its own dead `Velocity` (`HeroLocomotion.cs:1107`). Mover-agnostic; no scene-name check. **Explicitly forbidden:** making `HeroGaitForensics` or any trace read a different velocity to make the number look right.
- **F2 — One Speed owner.** `DungeonHero` stops calling `SetFloat(Speed, ...)` directly (`DungeonHero.cs:239-240`); `ActorAnimator` remains the single writer (it already re-resolves across body swaps, E11/E15). If F1 is not taken, then `DungeonHero` must at minimum re-resolve `_animator`/`_hasSpeedParam` on a null/destroyed handle instead of only in `Awake` — but F1+F2 is the correct shape.
- **F3 — Give the dungeon a real movement basis.** `HeroLocomotion.cs:861-862`: when `_smartCamera == null`, fall back to `Camera.main`'s **flattened** yaw rather than a hard `0f`. Note the trap: in town top-down, `CameraYaw` legitimately **returns** 0, so the fallback must trigger on the component being **absent**, never on the value being zero.
- **F4 — Rename the colliding trace field.** `[Flow:HeroDrift]`'s `camYaw` -> `basisYaw` (`HeroLocomotion.cs:1093`), matching what `HeroGaitForensics` already calls it. Two quantities must not share a name (§2.3).
- **F5 — Re-resolve the camera's follow target.** Whatever F0 names: if the pivot is destroyed by the body swap, `DungeonCameraRig` must re-bind when `_camera.Follow == null || _pivot == null` (a cheap null-poll, same self-heal idiom as `HeroLocomotion.ResolveAnimator`), rather than assuming a one-shot `Bind` holds for the run.
- **F6 — Make ownership explicit, not a static side-channel.** S1's neutralize is three shared statics with no live state. Replace the *observability* gap with the §6 heartbeat (done) and, if F0 shows the neutralize genuinely lapsing, give `HeroLocomotion` a proper per-instance `SetMovementSuspended(bool, string reason)` so ownership is a first-class, inspectable, per-hero fact instead of a global that any other system can clear.
- **F7 (verify only, no edit yet):** confirm §4's composed-`dg_*` note. If those dungeons have no `DungeonHero`, `DungeonCameraRig.Start`'s fallback bind never fires there and they need F5 regardless.

**Out of scope for this WO:** the WO-966 body-yaw constant; the DEF-7 `-90` in `DungeonHero.FaceHeading` (it is only reachable once F0 confirms `DungeonHero` is the sanctioned mover — fix it in the same pass as WO-966's ruling, not before, or the two offsets will be tuned against each other).

---

## 8. ⚠ Masking warning (read before shipping any single piece)

**F3 alone, shipped while the camera is frozen, makes the game feel WORSE, not better.** The camera is parked at yaw **180** (E21-E23). Wiring the movement basis to `Camera.main` while it sits at 180 gives the player a **constant 180-degree inverted** stick — "forward walks backward" — which reads as a brand-new bug and would cost the owner another felt-test session.

Equally: **F5 alone** (unfreezing the camera) makes S3 far more visible — the view will now rotate while the stick keeps meaning world +Z.

**Therefore: F0 -> F5 -> F3, together, in one deploy.** F1/F2 (the animation feed) are independent of the camera pair and may ship on their own.

**And do not accept "the animation looks right now" as proof of S1.** F1 makes the walk cycle play whichever component moved the hero — that is correct, and it is *also* exactly what would hide two movers still fighting over one transform. S1 is closed only by the `[Flow:HeroOwner]` line showing a single owner.

---

## 9. Regression — "animation velocity is non-zero when world position changes"

Three layers; the first is the one the owner asked for.

**R1 — headless play probe `DUNGEON_MOVER_PROBE`** (AutoPilot fleet, joins the `REGRESSION_OK` set):
enter `Dungeon_HealersCottage`, `HeroLocomotion.SetScriptedMove((0,1))` — no wait, use the **real** input seam the probe already owns — drive forward ~2 s, and each frame sample `(a)` root `delta position / dt` and `(b)` `Animator.GetFloat("Speed")`.
**FAIL** if, for **>= 15 consecutive frames**, `rootSpeed > 0.5 m/s` while `animSpeed < 0.1`. This is precisely the defect and it would have failed on 2026-08-10.
Second assert in the same probe: **exactly one component wrote the root this frame** — fail if a live `CharacterController` and a non-suspended `HeroLocomotion` both claim the transform (`ForeignMoverOwnsTransform() == true` while `HeroLocomotion.ScriptedMoveActive == false`). That single boolean pair is the S1 defect and is now readable (§6).
Third assert: **basis is real** — fail if `HeroLocomotion` is the live mover in a dungeon while no camera basis source exists.

**R2 — edit-mode unit test** (runs in `DataRegression`, no scene): extract the stall predicate as a pure static, e.g. `LocomotionTelemetry.IsAnimationStalled(float rootSpeed, float animSpeed, int consecutiveFrames)`, and test the boundary cases (moving+idle = stall; standing+idle = fine; moving+walking = fine; NaN animSpeed = not a stall). Keeps the rule itself covered even when no play session runs.

**R3 — scene/data guard** (edit-mode): assert that every dungeon scene carrying a `DungeonCameraRig` also carries a `CinemachineBrain` on the `MainCamera`-tagged camera, and that `DungeonController._cameraRig`/`_heroController` are wired. Cheap, and it catches the whole class of "the rig exists but nothing drives the view".

---

## 10. Files

**Touched by the instrumentation already landed:**
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` — `ScriptedMoveActive` accessor + `[Flow:HeroOwner]` `LateUpdate` heartbeat + stall callout
- `Assets/_Modules/Dungeons/DungeonHero.cs` — `[Flow:DungeonMover]` heartbeat + dead-animator callout
- `Assets/_Modules/Dungeons/DungeonCameraRig.cs` — `[Flow:DungeonCam]` rig heartbeat

**Expected for the fix (F1-F7):** the three above, plus `Assets/_Modules/Core/Combat/ActorAnimator.cs` (only if the feed signature changes), plus the new regression files.

**Do NOT touch:** `HeroGaitForensics.cs` velocity source (F1 forbids fixing the readout); `HeroBodySwapper.cs:263` (that is WO-966); any `.unity` scene by hand (§3 of `CLAUDE.md`).

---

## 11. Acceptance criteria

1. A dungeon capture contains `[Flow:HeroOwner]`, `[Flow:DungeonMover]` and `[Flow:DungeonCam]` lines, and a reader can name **who moved the hero** and **why the camera is/is not following** without opening a single source file.
2. No `ANIMATION-VELOCITY STALL` line in a normal dungeon walk.
3. `[Flow:HeroOwner]` shows exactly one transform owner for the whole walk.
4. The camera yaw tracks the hero yaw (F8 2313 closed) **and** the movement basis matches the camera (F8 2312's "everything is wrong" closed) — verified **together**, per §8.
5. `R1` fails on a revert of F1, and passes after. `R2` + `R3` green.
6. Town locomotion is **byte-unchanged in behaviour** — `[Flow:HeroOwner]` in `Main_Castle_Overworld` shows `ownerCC=none`, `scriptedMove=off`, `basis=SmartMobileCamera.CameraYaw`.
7. **PO (owner) felt-verifies and closes** — headless cannot judge this one (§13).

---

## 11b. ⚠ FALSE-GREEN WARNING — `[Flow:GaitF] bodyErr` IS VACUOUS IN A DUNGEON (headed run, 2026-08-10)

**Do not read `bodyErr=0.0` in a dungeon capture as "the body is aligned." It measures nothing there.**

`HeroGaitForensics` derives `bodyErr` from `HeroLocomotion.Velocity` — and under a foreign
`CharacterController` owner that value is **0.00 by design** (that is the whole point of F1: the
animator is fed the MEASURED root speed instead). With a zero travel vector there is no heading to
compare the body against, so the error term collapses to `0.0` and *prints as a pass*.

Proven in the 2026-08-10 headed proof run of `Dungeon_HealersCottage`: every `[Flow:GaitF]` line reads
`vel=0.00@0deg ... bodyErr=0.0` while `[Flow:HeroOwner]` on the same frames reads
`velRoot=4.20 animSpeed=4.20` and the hero visibly crosses 15 m of floor. The two instruments are not
in conflict — GaitF is honestly reporting a quantity that is undefined in this ownership mode.

**Consequence if ignored:** a future session reads `bodyErr=0.0`, marks the facing seams green, and
burns a whole cycle before discovering it never had a measurement. (Note this is the same instrument
that WO-1016 §1d already caught disagreeing with `HeroLoco` — same root, one velocity source.)

**Fix direction (NOT taken here — it is F1's shape, not a readout patch):** point `bodyErr`'s heading
at the same mover-agnostic measured root velocity the animator now uses, so the forensic is defined in
every ownership mode. ⚠ §7 F1 explicitly FORBIDS changing a trace's velocity source to make a number
look right — this is the opposite case (the readout is undefined, not inconvenient), so it needs its
own ticket and its own proof, not a quiet edit.

## 12. Open owner pins

- **P1:** With the basis fixed, should the dungeon stick be relative to the **camera** (F3) or to the **Keeper's own facing**? The two dungeon movers currently disagree with each other; only the owner picks the feel.
- **P2:** Once F0 names the mover, is `DungeonHero` still the sanctioned dungeon mover at all — or does the dungeon simply use `HeroLocomotion` with a proper basis and drop the second mover entirely? That is the structural answer to S1 and it is a design call, not a bug fix.
