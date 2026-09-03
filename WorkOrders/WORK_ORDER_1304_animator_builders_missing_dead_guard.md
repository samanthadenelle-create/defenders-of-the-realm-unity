# WORK ORDER 1304 — Animator builders missing the `Dead` guard (Knight package + enemy twin)

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — implemented by 34f86ebad `fix(anim): stop the death animation re-entering; guard every action on Dead` — `Dead == false` added to every non-death AnyState transition. Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02; body unchanged.)* *(Prior line:)* **Status:** READY TO IMPLEMENT
**Silo:** Combat / Animation
**Minted:** 2026-09-02 (CLI) — found while fixing the hero death-shake, NOT yet felt-reported.

## Why this exists

While closing the owner's death-animation shake (F8 seq 4647), the fix landed in
`Assets/Editor/HeroAnimatorFactory.cs` — which serves Knight / Mage / Ranger / Cleric plus the
KnightMocap struct-copy through one `Build(HeroSpec)` path. **Two OTHER animator builders carry the
identical hole and were deliberately left alone as out-of-lane.** A found defect with no ticket is a
lost defect, so it gets one.

## The defect class (proven in the sibling file, not inferred here)

Captured proving line, F8 seq 4647, scene `Main_Castle_Overworld`:

```
[Flow:HeroDeath] DEATH ANIMATION IS RE-ENTERING: 3 base-layer state changes in 0.25s on ctrl='Mage'
```

Root cause as established in `HeroAnimatorFactory`: the non-death AnyState transitions
(`Cast`, `Cast_q/w/e/r`, `Attack{N}`, `Hit`, `Victory`) carried **no `Dead` guard**, and each is
standing-gated on `Speed < 2.0` — which **a pinned corpse satisfies permanently at `Speed == 0`**.
So the killing blow's own `Hit` trigger fires on the dead body:

`Death` -> (`Any->Hit` matches) -> `Hit` -> exits -> `Locomotion` -> (`Any->Death` matches again) -> `Death` restarts at frame 0

Three base-layer changes inside a quarter second, clip never advances = the body reads as a SHAKE.

⚠ NOTE THE TRAP THIS TICKET EXISTS TO AVOID REPEATING: the trace's own suggested fix ("the generic
Death fallback must carry a `DeathDir != N` guard") was **already live** in source AND in the built
`Mage.controller`. Death-vs-death was not the collision; death-vs-ACTION was. Do not trust the
instrumentation's stated remedy over the transition graph you read yourself.

## Scope — two files

### 1. `Assets/Editor/KnightPackageControllerBuilder.cs` (builds `KnightPackage.controller`)
Reported unguarded AnyState transitions at approximately lines **303, 336, 350, 363, 374, 404**
(`Attack` / `Cast` / `Cast_*` / `Hit` / `SweepFall` / `Victory`). Its directional-death helper at
approximately **:628-632** applies the `DeathDir` `Equals` only conditionally, so its generic
fallback likely also lacks the `NotEqual` guards. **RE-VERIFY EVERY LINE NUMBER AT SOURCE** — these
were reported from a sibling lane and line numbers rot.

### 2. `Assets/Editor/BuildOrcHumanoidController.cs` (enemy side)
Authors `Dead` / `DeathDir` and is the enemy twin of the same pattern. **Not yet inspected.** Audit
first; only fix what the graph actually shows. If it is clean, say so and close that half — do not
manufacture a change to look busy.

## Acceptance criteria

1. For **every** `(Dead, DeathDir)` combination, at most ONE base-layer AnyState transition is
   satisfiable. Show the mutual-exclusivity argument explicitly, partitioned on `Dead`; do not assert it.
2. `Dead == false` behaviour is **byte-identical** to today for a living actor — this must be a
   zero-delta change for anything alive.
3. Coverage is not reduced: `DeathDir == 0`, and any direction whose optional clip is absent, must
   still reach a death state. Nothing dies without an animation.
4. Follow the established pattern — a single `AddNotDead(AnimatorStateTransition)` helper with the
   RCA in a block comment, mirroring `HeroAnimatorFactory`. **Do not invent a second approach.**

## What NOT to touch

- ⛔ `Assets/Editor/HeroAnimatorFactory.cs` and `Assets/_Modules/Village/Hero/HeroHealth.cs` — already
  fixed 2026-09-02, live in the working tree. Do not re-fix, re-style, or "unify" them.
- ⛔ Do NOT weaken, re-thresholds, or remove the `[Flow:HeroDeath]` re-entry detector. It is what
  caught this. Instrumentation is permanent (CLAUDE.md sec.12).
- ⛔ Do NOT hand-edit any `.controller` asset or `.unity` scene. These are code-built controllers;
  they stay code-built.
- ⛔ Do NOT run batchmode without checking the Unity editor is closed (project lock, CLAUDE.md sec.3).

## Dependency the lead already owes

The hero-side fix is **editor-time code**: `Assets/Resources/Heroes/{Knight,Mage,Ranger,Cleric,KnightMocap}.controller`
on disk are STALE until someone runs `DeNelle.Editor.HeroAnimatorFactory.BuildAll` **and**
`BuildKnightMocapController`. This ticket's own fix will carry the same requirement for
`KnightPackage.controller`. Judge by the marker on a fresh log, never the exit code.
