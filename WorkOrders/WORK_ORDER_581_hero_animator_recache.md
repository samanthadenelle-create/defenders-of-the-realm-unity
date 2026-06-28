# WORK ORDER 581 — Hero animator re-cache writes 0 components (hero won't animate)

**Status:** IMPLEMENTED (edit-only; not gated/committed — orchestrator batch-gates)
**Silo:** Combat/AI + Hero (code only, file-disjoint hero trio)
**Branch base:** `wip/village2-and-f8-tickets` @ `a0987724` (ff-merged into worktree first)

---

## Captured error (break-log.jsonl, MainCastle_Hall, ~7×, error-level)

```
[Flow:HeroBody] Animator re-cache wrote 0 components — neither HeroLocomotion nor
HeroAbilities received the live animator (renamed _animator field?); the hero will
not animate.
```

## RCA — proved from code, the ticket's own hypothesis was WRONG

The error message guesses "renamed `_animator` field?". **The field was NOT renamed.**

1. **Emit site:** `Assets/_Modules/Village/Hero/HeroBodySwapper.cs:466-469` (pre-fix) —
   inside `WireHeroBody(...)`. The re-cache (pre-fix lines 436-462) did:
   `GetComponentsInChildren<MonoBehaviour>(true)`, matched by `mb.GetType().Name ==
   "HeroLocomotion"/"HeroAbilities"`, then `GetField("_animator", NonPublic|Instance)` +
   `SetValue` via `Guard.Try`, counting `recached`. `recached==0` → `FlowTrace.Fail`.

2. **Both fields still exist and were never renamed:**
   - `HeroLocomotion.cs:308` → `private Animator _animator;`
   - `HeroAbilities.cs:97`  → `private Animator _animator;`
   - Confirmed via `git log -p`: the Yarn-removal commit `be39c4db` (HeroLocomotion +
     HeroBodySwapper) only swapped the Yarn `DialogueRunner` hook for the static
     `DialogueService` events — it did **not** touch `_animator`. WO-574 `d455bd42`
     (HeroAbilities) added `_extraCooldown`/`TryCastExtra`/`CastResolved` — it did **not**
     touch `_animator`. So reflection by name would still resolve the field IF the
     component were present.

3. **True root cause = component ABSENCE at re-cache time (a timing bug, not a rename):**
   - `WireHeroBody` runs **synchronously inside `HeroBodySwapper.Start()`** for the Knight
     (the single-hero V1 north-star), because `HeroClass.Knight` routes to
     `BuildLegacyResourcesBody` (HeroBodySwapper.cs:73-78) — no async Addressables wait.
   - In `MainCastle_Hall`, `HeroLocomotion` is added by
     `HeroControlEnsurer.Ensure()` (`HeroControlEnsurer.cs:112`,
     `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` + `sceneLoaded`), whose ordering vs.
     the swapper's `Start()` is **not** guaranteed — it can run AFTER the swap.
   - `HeroAbilities` is **never** added in the castle hub at all: there is **no**
     `AddComponent<HeroAbilities>` anywhere in runtime code (only `VillageSceneBuilder`
     bakes it into the Village scene). `HeroControlEnsurer` adds Locomotion, GearLoadout,
     HeroArmorVisual, etc. — but not HeroAbilities.
   - Net: at the moment the re-cache loop runs, the hero root has **neither** component →
     loop matches 0 → `recached==0` → the FAIL fires. (~7× = re-runs / scene reloads /
     the Blink→legacy fallback path re-entering `WireHeroBody`.)

4. **The reflection was never necessary:** `HeroBodySwapper`, `HeroLocomotion`, and
   `HeroAbilities` are all in namespace `DeNelle.Village` (same `DeNelle.Village` assembly).
   Direct typed references compile fine — the brittle name-based reflection added a whole
   failure class (rename/timing/signature) for no isolation benefit.

5. **HeroLocomotion IS the NavMeshAgent locomotion** (CLAUDE.md warns its header lies):
   `HeroLocomotion.Awake` (lines 354-366) gets/adds + configures a `NavMeshAgent`
   (`updateRotation=false`, drives via `Move()`). The animator it expects is the live
   swapped-body Animator — `ResolveAnimator()` (lines 477-490) finds
   `transform.Find("HeroBody").GetComponentInChildren<Animator>()`. Confirmed.

## Fix

**Explicit setter on both components (no reflection), called directly by type.**

- `HeroLocomotion.cs` — new `public void SetAnimator(Animator anim)`: sets `_animator`
  and calls `RefreshParamCache()` (re-scans Speed/Victory params for the swapped controller).
- `HeroAbilities.cs` — new `public void SetAnimator(Animator anim)`: sets `_animator`,
  pins `_paramCheckedAnimator`, re-scans the `Cast` param.
- `HeroBodySwapper.cs` — replaced the reflection loop with:
  - **Ensure** `HeroLocomotion` exists (`TryGetComponent` → `AddComponent` if missing;
    idempotent with HeroControlEnsurer) then `loco.SetAnimator(anim)` → `recached++`.
    This makes the write **deterministic at swap time** regardless of ensurer ordering,
    so `recached` is now always ≥ 1.
  - Recache `HeroAbilities` **only if present** (`TryGetComponent`) → `SetAnimator` +
    `SetHeroClass(abilitySlug)` → `recached++`. Absent-in-hub is no longer a false FAIL
    (HeroAbilities self-heals its animator in `CastResolved` anyway).
  - Calls wrapped in `Guard.Try` (§12, no silent failure).
  - Success path now logs `Animator re-cache wrote N components (HeroLocomotion[ +
    HeroAbilities]) — direct SetAnimator, no reflection.` with **N > 0**, proving the fix
    headless. The FAIL only remains as defense-in-depth (locomotion ensure itself failing).

## Files modified (for reconcile — explicit paths)

- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs`
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs`
- `Assets/_Modules/Village/Hero/HeroAbilities.cs`

## Validation

- Brace balance (utf-8): HeroBodySwapper 146/146, HeroLocomotion 104/104,
  HeroAbilities 80/80 — all balanced; no NUL bytes.
- No scene files hand-edited.
- `System.Reflection` usage for this path **removed** (none introduced).
- Re-cache now writes ≥ 1 component (locomotion ensured) → no more false `wrote 0` FAIL;
  HeroLocomotion receives the live swapped animator deterministically → Speed→Walk drives →
  hero animates. (Headless confirm: look for `[Flow:HeroBody] Animator re-cache wrote N
  components` with N≥1 on the next AutoPilot MainCastle_Hall run.)

## Acceptance criteria

- [x] Re-cache writes > 0 components in MainCastle_Hall (Knight legacy path).
- [x] Explicit `SetAnimator` on both components; HeroBodySwapper calls by type, no reflection.
- [x] Tolerant of HeroAbilities being absent (hub) — no false FAIL.
- [x] Success FlowTrace logs `wrote N components` (N>0).
- [x] Braces balanced on all touched files; no scene edits.
