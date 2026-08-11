# Overnight Report — 2026-08-10 → 08-11

**Branch:** `wip/village2-and-f8-tickets`
**Ordered by the owner:** *"push that to apk and then to firebase and work other issues overnight.
Get the dungeon fixed. checked with headed proof, and the matching data inside halers cottage"* +
*"update web ui tonight after APK then work the dungeon fixes proven by images and data to verify.
I want those in the proof i want in the overnight report."*

Every claim below is backed by a captured artifact. Where something is **not** proven by a run, it
says so in those words.

---

## 1. Shipped tonight

| Gate | Result |
|---|---|
| `DeNelle.Editor.CompileGate.Run` | **`COMPILE_GATE_OK :: scripts compiled clean`** |
| `DeNelle.Editor.DataRegression.RunAll` | **`REGRESSION_OK 159/159 suites`** (was 158 — `[wall-adjacency]` added) |
| Android | **`[AndroidBuild] SUCCEEDED`** — 616,746,278 bytes, 21:33 |
| Firebase App Distribution | release **`2026.08.11.319827`**, distributed to `testers` |

Commits: `b6645acb` (pushed) — WO-972 walls, WO-968 camera, WO-973 mint + board regen.

---

## 2. The dungeon — headed proof, images **and** matching data

Driver: a real windowed player (`-bootScene Dungeon_HealersCottage`, 1280x720), driven with real
WASD through `SendInput` so `Keyboard.current` sees it — not a scripted move, not a headless stub.
Screenshots and `Player.log` come from **the same run**.

**Proof images:** `docs/proof/2026-08-10-dungeon-headed/01_idle.png` … `08_final.png`
**Matching log:** 47 `[Flow:HeroOwner]`, 46 `[Flow:DungeonCam]`, 44 `[Flow:DungeonMover]`,
45 `[Flow:GaitF]` lines. Zero exceptions, zero `FlowTrace.Fail`.

### PASS — one transform owner, and it never flips

Exactly one ownership line in the entire run:

```
[Flow:HeroOwner] TRANSFORM OWNER = FOREIGN CharacterController (this component writes NOTHING and
publishes the MEASURED root speed to the animator) [scene='Dungeon_HealersCottage'
scriptedMove=ZEROED agent=disabled]
```

WO-968 S1 — *"ownership flips silently, nothing logs which is live"* — is closed.

### PASS — the animator tracks real movement

```
velSelf=0.00 velRoot=4.20 animFeed=velRoot(measured) animSpeed=4.15 ... pos=(-28.04, 0.08, -3.49)
velSelf=0.00 velRoot=4.20 animFeed=velRoot(measured) animSpeed=4.20 ... pos=(-24.13, 0.08, -7.12)
```

A real blend, not one idle clip: `clips=[walk_normal_f(w=0.45), move_run_m(w=0.55)]`, `speedP=4.20`.
No `ANIMATION-VELOCITY STALL` line. **The pixels agree** — the hero translates 15+ m across
`01_idle` → `08_final`.

### PASS — the stick, and the invert trap did NOT fire

`basis=Camera.main(flattened) basisYaw=180.0`, no `NO MOVEMENT BASIS` failure. Against a camera at
yaw 180 (forward = −Z, right = −X):

| Input | Position change | Expected axis | Verdict |
|---|---|---|---|
| W | z 0.00 → −3.49 → −7.12 | −Z | ✅ |
| D | x −28.04 → −35.12 | −X | ✅ |
| A | x −35.12 → −24.13 | +X | ✅ |

**No 180° inversion.** Called out explicitly because WO-968 §8 warned that an over-correction here
would be worse than the original bug.

### PASS — the clock-freeze fix holds in dungeons

`timeScale=1.00` on all 47 samples (`dt` 0.0167–0.0225). No `WORLD CLOCK FROZEN` line. The
ambiguity that made *"i can walk now"* impossible to interpret is off the table for this scene.

### FAIL → FIXED (not yet proven by a run) — the camera

Across 43 heartbeats spanning the hero's full traverse there is **exactly one distinct rig pose**,
byte-identical to the bind seat:

```
Follow='DungeonOTSPivot' pivot=alive vcam=enabled brain=present
hero=pos=(-22.51, 0.08, -7.14) yaw=90.0
rig=pos=(-28.50, 2.25, 3.20) yaw=180.0   mainCam pos=(-28.50, 2.25, 3.20) yaw=180.0
```

`[Flow:GaitF]` independently agrees: `camYaw=180 dCam=0.0` on every line.

**The screenshots are the decisive evidence.** In `01_idle.png` the hero is at frame centre; in
`08_final.png` **he is gone from the frame entirely** and the camera is still staring at the wall he
left, with the compass reading S in all eight shots. You walk your Keeper out of frame and lose him.

**Root cause — read at source in `Library/PackageCache` (Cinemachine 3.1.6), not inferred.**
`ApplyThirdPerson` does `_follow.enabled=false; Destroy(_follow); AddComponent<ThirdPersonFollow>()`.
`Destroy` is **deferred to end of frame**, so the disabled `CinemachineFollow` is still live when the
brain rebuilds its pipeline cache that same frame; `UpdatePipelineCache` takes the **first**
component per stage and the scene-authored body is first — so the doomed component wins the Body
slot. At end of frame it dies, and **Unity does not fire `OnDisable` on an already-disabled
component**, so nothing ever invalidates the cache again. `m_Pipeline[Body]` holds a destroyed object
forever, the Body stage is skipped, and the camera pulls state *from* the transform and pushes it
*back* — a perfect fixed point.

**It is a race.** That explains the contradiction WO-1016 §1d had parked: one capture showed `camYaw`
genuinely changing (308→307→306) while the owner still reported no camera. If `Bind` lands after that
frame's brain update, the destroy has completed and the camera works fine that session.

**Fix:** `HealBodyStage()` polls in `LateUpdate` beside the existing `HealFollowTarget` idiom and
forces a re-cache. Detection uses `ReferenceEquals`, never `==`, because a destroyed Unity object
compares equal to null — a "simplifying" edit would make the squatter read as *"no body configured"*
and silently re-freeze the camera. The comment says exactly that, naming the consequence.

> ⚠ **This fix has NOT executed yet.** The diagnosis is source-derived and the evidence is airtight,
> but no build containing `HealBodyStage` has run. The proof line to look for on the next headed run:
> `body=CinemachineThirdPersonFollow(enabled=True)` with `rig=pos` finally tracking `pivotPos`.

### Also captured

- **`DungeonHero`'s animator re-resolve works:**
  `[Flow:DungeonMover] animator RE-RESOLVED on 'Keeper': animator='HeroBody' controller='KnightMocap'
  hasSpeedParam=True` — WO-968 E13 closed.
- **NEW false-green found and documented (WO-968 §11b):** `[Flow:GaitF] bodyErr` is **vacuous in a
  dungeon.** It derives from `HeroLocomotion.Velocity`, which is 0 by design under foreign
  ownership, so `bodyErr=0.0` prints as a pass while measuring nothing. Left in place with a warning
  rather than quietly retargeted — that needs its own ticket and its own proof.
- **The extract-label rename shipped** — `01_idle.png` shows **"Leave Dungeon"**, not "Extract".
- **NEW defect found in the pixels → WO-973.** Bryn's speech bubble renders as a giant skewed card
  covering ~60% of frame with the text clipped mid-word. The trapezoid shape *is* the diagnosis: a
  screen-space canvas cannot skew, so it is world-space at wrong scale. Its trace said `bubble=ok`
  while the thing was unreadable — a trace asserting construction and silent on legibility.

---

## 3. Walls — WO-972, you can build them beside each other

Proven from the owner's own F8 capture (seq 2327), not inferred:

```
[Flow:Build] REJECT Occupied cell=(17,16) fp=(2x2) gate=CellGrid occupantCell=(17,17) occupant='wall_wood'
[Flow:Structure] 'wall_wood' carries Collider 'MeshCollider' bounds size=(3.03, 3.73, 1.42)
```

A 3.03 m wide, 1.42 m thick palisade on a 3.00 m cell was claiming **36 m²**.

**Two collapses stack:** `MeasureUprightFootprintMetres` reduces the mesh to `Max(x, z)` — discarding
the 1.42 m depth — and `FootprintCells` then **ceils *and squares*** it. A **1% overshoot** doubles
the claim, then re-applies that doubling to the thin axis that was never over a cell.

Same root explains the second symptom she'd been living with: her landed run sits on a **6 m pitch**
(`Occupy 12_17 / 14_17 / 16_17`), so **every wall run had a ~3 m hole between segments**.

Fix is **claim-side only — the mesh is never touched**, so the walls-excluded-from-height-cadence
carve-out holds, the NavMeshObstacle carve is byte-identical at both the old and new claim, and there
is **no save migration** (`CellToWorld` seats on the origin cell centre independent of footprint).

**Words, never colour alone** (owner is red/green colourblind): the refusal now names the occupant —
*"Too close to another building - Wooden Palisade is already on that square"* — in both the toast and
the ghost label. A permanent `FlowTrace.Once` now states authored-vs-measured metres; that number was
logged **nowhere**, which is why this RCA had to bound the width from a collider dump.

Regression `WallAdjacencyRegression [wall-adjacency]` replays her exact cells and is registered.

---

## 4. Web — canon was wrong, and one correction is security-relevant

An architect verified the deploy path against the **live** Vercel deployment record and live HTTP
responses. Four load-bearing canon claims were refuted:

1. **Production is not the 07-16 build.** `KEY_FACTS.md` said `q2v5vj86g`; there have been three
   production deploys since — Aug 3, Aug 4, and **Aug 5** (`dpl_9vGadbKyPrQ55HR3PaUT53i9CNUh`).
2. **`Builds/PROD_ROLLBACK.txt` was referenced for weeks and never existed.** There was no recorded
   way back from a bad promotion. Now written, with the outgoing prod id, *before* promoting.
3. **⚠ `api/` is NOT preview-only.** `HANDOVER.md` (in two places, including the block a new session
   actually reads), `CANON_GROUND_TRUTH_2026-08-09.md`, and `AUDIT_2026-08-09.md` all said prod runs
   old `api/` code with no CORS. A live request returns the **new** structured shape
   `{"ok":false,"code":"AUTH_WALLET_MALFORMED","ref":"…"}` **with** `access-control-allow-origin: *`.
   **`AUDIT_2026-08-09.md` §5 argued against promoting on the grounds that old prod code was
   protecting us from findings F5/F6/F7 — that premise is false. Those endpoints have been live in
   production since Aug 3.** F5/F6/F7 remain real and open; the mitigation everyone believed was in
   place never existed. Tonight's promotion does not make it worse, and does not fix it.
4. **The 100 MB Vercel per-file limit is not a blocker.** `webgl-hosting-notes.md` called it a
   confirmed blocker and the reason we moved to itch; prod is *currently serving* a **165,005,813
   byte** `.data`. (The size is still a real load-time problem — WO-545/WO-282, moving heroes out of
   `Resources/`, never landed. Corrected as "should not be this big", not "size is fine".)

**Method used, chosen from the record rather than convenience:** build → deploy preview → verify the
preview actually serves the new build → `vercel promote <that exact url>`. Promoting a verified
preview ships the artifact that was inspected; `--prod` re-uploads one that wasn't. No repo script
supports promotion, so that step stays a deliberate manual command — no `--prod` was added to any
script in a release window.

---

## 5. Addressables — audit verdict: **CORRECT-BUT-FRAGILE**

Safe to ship from this machine tonight. Two defects would bite a fresh clone or CI, and both were
**minted rather than fixed mid-release**:

- **WO-974 — the content build has no seam.** `m_BuildAddressablesWithPlayerBuild: 0` does not mean
  "don't build"; at package source it means *"use the global settings stored in preferences"*. No
  build entry point calls `BuildPlayerContent`. **It works here by luck of an uncommitted per-machine
  preference**, and when it doesn't, the build still goes green and ships unresolvable assets.
- **WO-975 — the `Gear` group points at a gitignored pack.** `Gear.asset` is git-**tracked** with
  **426 entries** resolving into `Assets/Blink/`, which `.gitignore:350` excludes. A tracked group
  *asserts* content a clone doesn't have → hollow bundle → no weapons, no armour, no hero body, with
  no warning naming the cause. The existing `AddressableKeyExists` probe only `LogWarning`s, so it is
  a soft signal, not a fence.

Correct and worth keeping: nothing remote (`Remote.LoadPath` is `<undefined>`, `m_BuildRemoteCatalog:
0`), the WebGL load path goes over HTTP via `UnityWebRequest` so it does **not** repeat the
`File.ReadAllText`-throws-in-WebGL trap, and Addressables/`Resources/` are cleanly disjoint.

---

## 6. Open pins — need the owner, not more investigation

1. **Dungeon stick: camera-relative or Keeper-relative?** The basis is now correct and
   camera-relative. Once the camera actually rotates, this becomes a feel choice.
2. **Should `DungeonHero` exist at all?** The data answers the factual half: its CharacterController
   was the sole owner for the entire run, cleanly, and `HeroLocomotion` correctly published measured
   root speed under it. Two movers coexisting is **no longer producing a defect** — so this is now a
   structural preference, not a bug.
3. **WO-966 hero facing** — the −90 root yaw in dungeons is deliberately untouched and must not be
   tuned against the body offset until this is ruled.
4. **Mage kit DRAFT numbers** and skill-tree node placement (the mage tree is a full 5×4 grid).
5. **`Posion_Cast` spelling** — three variants exist in the VFX set.

---

## 7. What to check on the Firebase build (`2026.08.11.319827`)

- **Walk a dungeon and watch the camera.** This is the one fix with no run behind it.
- **Build two walls side by side**, then build a run of them and look for gaps.
- **Refuse a placement on purpose** and read the message — it should name the building in words.
- Bryn's bubble in Healer's Cottage will still be wrong. It is ticketed (WO-973) — not a surprise.
