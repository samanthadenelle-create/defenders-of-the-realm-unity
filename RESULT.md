# RESULT — WO-30 (Camera Follow) + WO-31 (Hero Animation Pipeline)
Date: 2026-05-25 · Branch: samantha-village-progress-2025-05-23

---

## WO-30 — Camera Follow Fix

### Diagnosis (from runtime log, no-pet isolation build)
The camera was **never** following a pet. Runtime proof:
- `[VillageCamera DIAG] Target = Hero (Blaise) | Position = (6,0,4)` — target correct.
- Children dump = `Hero (Blaise) → HeroBody + HeroIndicatorRing + bone rig` — as expected.
- `[HeroLocomotion] First input registered: (0,1)` — the hero genuinely moved.

The defect was the **offset**: the old `(0, 20, -10)` was a near-top-down 63° perch ~20 m up. From that height a hero walking 6 m barely changes the camera angle, so the world read as a *static overhead map* with the hero sliding to the screen edge. The follow math was fine; the framing was wrong.

### Fix — `Assets/_Modules/Village/Hero/VillageCamera.cs` (rewritten)
- Closer/lower fixed **world-space** offset `(0, 8.5, -6)` — hero prominent, camera visibly tracks.
- Looks at hero chest (`+1.2` up) each frame → hero stays centred.
- `Vector3.SmoothDamp` chase at `0.10 s` — responsive, not jittery.
- Offset is **world-fixed** (never orbits with hero yaw) → with world-relative WASD this keeps screen axes == world axes (no circling feedback loop).
- `Awake()` forces the WO-30 tuning at runtime because the **baked Village scene stores a stale `_followOffset`** and re-baking Village risks the `level3 / Position out of bounds` corruption.
- All diagnostic logging removed.

---

## WO-31 — Hero Animation Pipeline End-to-End

### Root cause (confirmed)
1. The hero FBXs are Generic-rigged but `HeroAnimatorSetup` had **never been run** — no `.controller` files, `clipAnimations: []`, and `avatarSetup: 0` (no avatar → rig un-driveable).
2. The baked placeholder body (`Wizard.fbx`) is **missing** → loads as a capsule with no Animator.
3. `HeroBodySwapper` tried to *snapshot* the placeholder's controller — always null → the entire "assign Animator + controller + re-cache" block was skipped.
4. `HeroLocomotion` (and `HeroAbilities`) cache `_animator` in `Awake()`, before the swap → stale/null forever → `SetFloat("Speed")` / `SetTrigger("Cast")` no-op → **sliding statue**.

### Fix
- **`HeroAnimatorSetup.cs`**: added `SetupAll` batch + `-executeMethod` target; set `avatarSetup = CreateFromThisModel`; added take-diagnostic logging; tolerant of single-take FBXs (Walk-only).
- **Ran it** (`-executeMethod DeNelle.Editor.HeroAnimatorSetup.SetupAll`):
  - **Mage** — 8 takes → `Mage.controller` (Idle/Walk/Cast). ✅
  - **Knight** — 8 takes → `Knight.controller` (Idle/Walk/Cast). ✅
  - **Ranger** — original FBX had **0 takes**; owner re-exported an animated 61 MB FBX (2026-05-25 19:00). Re-imported in place (kept `.meta`/GUID) → `Ranger.controller`. ⚠️ Tripo **merged its 3 animations into one 368-frame take**, so Walk = the whole merged clip looping (animates, but cycles through all 3 motions). Polish later by splitting frame ranges or re-exporting with separate clips like Mage/Knight.
- **`HeroBodySwapper.cs`**: removed fragile snapshot dependency — loads `Resources/Heroes/<slug>.controller` directly (snapshot only as fallback); always ensures an Animator + assigns controller; **always** re-caches `_animator` on **both** `HeroLocomotion` *and* `HeroAbilities`; defensive log (`controller`, `avatar`, `clips`, components re-cached).
- **`HeroLocomotion.cs` / `HeroAbilities.cs`**: self-heal — re-resolve `_animator` from the `HeroBody` child if null (backstop against swap-order changes).

### Verification log to look for
`[HeroBodySwapper] Animator wired: controller=Mage, avatar=…, clips=2, re-cached 2 component(s)`
(Note: `[HeroLocomotion] Start … animator=null` is **expected** — that snapshot is taken pre-swap; the swapper + self-heal wire it afterward.)

---

## Remaining animation tasks
- **Ranger** clip is a single merged 368-frame take (3 animations concatenated) — split into Walk/Cast/Idle by frame range, or re-export from Tripo with **separate** clips (as Mage/Knight have), then re-run `Defenders → Animation → Setup Ranger Animator`.
- Walk-cycle polish (foot sliding / speed-matched playback) — `Speed` currently drives a binary Idle↔Walk; could blend playback speed to `Velocity.magnitude` for a tighter gait.
- Pets are temporarily disabled (`PetDeployer.DIAG_SKIP_ALL_PETS = true`) for camera/anim isolation — **flip to `false`** to restore the three starter pets once camera + walk are confirmed.
