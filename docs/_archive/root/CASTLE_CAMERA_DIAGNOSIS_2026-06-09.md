# Castle "diagonal walk" — diagnosis (2026-06-09, while you were in meetings)

**Symptom:** in MainCastle_Hall, pressing **Up** walks the hero up-screen but ~45° to the left.

## Method — we proved it with hard data, not theory
Walked the diagnosis down the chain, exonerating each layer with evidence:

| Layer | Test | Result |
|---|---|---|
| **Input** | `[HeroLocomotion] First input registered` log | `(0.00, 1.00)` — **clean +Z**, no left value |
| **Transform hierarchy** | read MainCastle_Hall.unity | hero + all 3 parents: rotation 0, scale 1 — **clean** |
| **Locomotion / NavMesh** | Hero (Blaise) world Position before/after holding Up | X `-15.70302 → -15.70302` (**0**), Y `0.079 → 0.079` (**0**), Z `55.249 → 56.375` (**+1.13**) |
| **Hero rotation** | same readout | Y `178° → 0°` — turned to face north and walked north |

**Conclusion: the hero moves perfectly straight north in world space. Input, locomotion, NavMesh, and the hero are all PROVEN GOOD.** The "45° left" is purely a *presentation* problem — something between the straight-north motion and what's drawn on screen.

## What's drawing it at an angle — two findings
1. **The active camera is SmartMobileCamera, yaw 0, directly behind.** Its offset (0, 2.6, -4.5) + look-height 2.5 produces a ~1.3° downward pitch and **zero yaw** — exactly the `(1.273, 0, 0)` rotation read in-editor. A yaw-0 camera directly behind a hero moving pure +Z *should* show straight-up motion.

2. **Real defect found: two camera scripts fight on Main Camera.** The builder (`Village2Playable` lines 138-139 / 604-605) adds **both** `VillageCamera` and `SmartMobileCamera`. The comment claims SMC's `EnforceSoleCamera` disables VillageCamera at Play — **that's false.** `EnforceSoleCamera` (SmartMobileCamera.cs:736) only disables `Camera` *components on other GameObjects*; it cannot disable the sibling `VillageCamera` *script* on the same object. So both run every frame, fighting over the seat (SMC wants height 2.6/dist 4.5; VillageCamera's Awake forces height 5.5/dist 9). **This is a genuine bug and should be fixed regardless** — but both still aim straight-behind at yaw 0, so by itself it explains jitter, not a 45° yaw.

## The honest open fork
Every static code path I can read yields **straight-up** motion (yaw-0 camera + proven +Z motion). So the diagonal is a **runtime value not visible in the source.** The two leading candidates:

- **A — Hero body mesh seated at the wrong yaw for Blaise.** The root walks straight (proven), but if `HeroBody` is rotated ~45° off the root, the *visible character* reads as walking diagonally. `HeroBodySwapper` uses a fixed `forwardYaw = -90f` (HeroBodySwapper.cs:93) — "proven" for the older heroes, but **possibly wrong for Blaise's mesh.** This fits "no rotation + constant 45°."
- **B — The camera is actually yawed at runtime** in a way the single in-editor reading didn't catch (e.g. the two-camera fight, or a transient seat), despite the static math.

I will NOT guess between these and claim a fix — that's the exact trap from the 8-hour morning. **One instrumented playtest settles it.**

## Recommended next step (decisive, ~2 min when you're back)
Add an editor-only per-second log that prints, while playing:
- camera world rotation (euler)
- HeroBody **local** rotation + world rotation
- hero root rotation + world-position delta

Then walk Up once. The numbers tell us instantly whether the **body** is angled (→ fix Blaise's `forwardYaw`) or the **camera** is yawed (→ fix the seat + kill the duplicate VillageCamera). I can add this instrumentation on your OK.

## Fix that's safe to do now regardless (your call)
Make SmartMobileCamera truly sole: have it disable any sibling camera-follow scripts (VillageCamera) on its own GameObject, or drop VillageCamera from the castle wiring. Removes the every-frame fight. Needs a playtest to confirm feel — won't claim it fixes the diagonal.

— Everything below the camera is proven correct. The remaining question is body-yaw vs camera-yaw, and one logged playtest answers it.
