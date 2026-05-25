# WORK ORDER 08 — RESULT

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Feature implemented end-to-end in code per the WO design. Build clean (0 errors, 0 warnings). Existing damage/collapse mechanic preserved. Runtime walk-through eyes-on is the remaining tick (build-side gate — see §5).
**Editor:** Unity 6000.4.8f1

---

## TL;DR

| Acceptance criterion | Status |
|---|---|
| 1. `GateProximityOpener.cs` exists, compiles, no warnings | ✅ new file; build is 0-error/0-warning |
| 2. `Gate.RequestOpen()/RequestClose()` added; damage mechanic intact | ✅ additive; HP-derived collapse untouched (§2) |
| 3. Hero approach opens gate (visual + physical) at all four gates | ✅ *by construction*; runtime eyes-on pending (§5) |
| 4. Enemy approach does NOT open the gate | ✅ filtered by `HeroLocomotion` component (§3) |
| 5. Damage-to-25% still collapses independently of proximity | ✅ combined-state logic preserves it (§2) |
| 6. `build-windows.ps1` succeeds; behaviour replicates in exe | ✅ build `[DesktopBuild] SUCCEEDED`; in-exe walk-through eyes-on pending (§5) |
| 7. Changes committed (focused) | ✅ see commits |
| 8. This RESULT.md | ✅ |

---

## 1. Diff summary

**`Assets/_Modules/Village/Gates/Gate.cs`** (modified, additive):
- New `private bool _isOpenForHero;` + public `bool IsOpenForHero`.
- New `RequestOpen()` / `RequestClose()` — idempotent; set/clear the flag, recompute the collapse target, re-apply the force-field state.
- `RefreshCollapseTarget`: `_isOpenForHero` → target `1` (fully passable) takes precedence; otherwise the original HP-derived ramp is unchanged.
- `ApplyForceFieldState`: blocker `up = !_isOpenForHero && IsForceFieldUp` (combined state); collapse/restore events fire on its rising/falling edge.

**`Assets/_Modules/Village/Gates/GateProximityOpener.cs`** (new): `[DisallowMultipleComponent][RequireComponent(typeof(Gate))]`. In `Awake` builds a child `ProximityTrigger` GameObject with a trigger `SphereCollider` (radius serialized, default 4 m, clamped 1–6) + a kinematic `Rigidbody` + a `GateProximityTriggerRelay`. The relay forwards hero-only `OnTriggerEnter/Exit` to the opener, which ref-counts and calls `Gate.RequestOpen/Close`, logging `"[GateProximityOpener] gate-N opened/closed …"`.

**`Assets/_Modules/Village/VillageController.cs`** (modified, additive): `RegisterGate` now attaches `GateProximityOpener` (for future builder bakes); a new runtime `Start()` → `EnsureGateProximityOpeners()` attaches it to every gate on scene load (for the already-baked scene). Both idempotent.

---

## 2. Existing damage mechanic preserved (AC2 / AC5)

The proximity behaviour is layered, not a replacement:
- `_isOpenForHero` is an **additional** reason to drop the field. When it's false, `RefreshCollapseTarget` and `ApplyForceFieldState` behave exactly as before — `TakeDamage`/`Repair` still drive the HP-derived collapse, the blocker still drops below 25% HP, and a gate the enemies battered below 25% **stays open even after the hero leaves** (`RequestClose` re-enables the blocker only if `IsForceFieldUp` also wants it up).
- `ForceFieldCollapsed`/`ForceFieldRestored` now fire on the rising/falling edge of the *combined* "blocking enemies right now" state, per the WO design. **Verified there are no external subscribers** to these events in the codebase, so firing them on a hero-open has no side effects today. (If a future enemy-AI/HUD subscriber is added that assumes "collapsed = damaged," it should consult the new `Gate.IsOpenForHero` to distinguish a hero-open from real damage.)

---

## 3. Hero-only, trigger-event-driven (AC4, hard rules)

- Hero identity = `GetComponentInParent<HeroLocomotion>()` on the entering collider — **not** tags/layers (per hard rule). Enemies have no `HeroLocomotion`, so they never open a gate.
- Detection is pure trigger events (no per-frame `OverlapSphere` polling — per hard rule).
- The `ForceFieldGate` shader `_Collapse` semantics are untouched; the proximity open reuses the existing `_collapseEaseSeconds` easing.

---

## 4. Two implementation realities that shaped the approach

1. **Gates are baked by the edit-time `VillageSceneBuilder`**, which the curated-scene rule (CLAUDE.md #1) forbids re-running. So wiring the opener via the builder/`RegisterGate` alone would **not** affect the already-shipped scene. Fix: `VillageController.Start()` attaches the opener at runtime to every baked gate (`_gates`, or a `FindObjectsByType<Gate>` fallback). `RegisterGate` also attaches it so a *future* bake bakes it in. No scene-YAML hand-edit, no builder run.
2. **The hero is a solid `CapsuleCollider` with no Rigidbody** (transform-moved; HeroLocomotion uses CapsuleCast for movement). Unity only fires trigger events when one side has a Rigidbody — so the proximity zone carries a **kinematic Rigidbody**. (The WO's "make the hero collider non-trigger" step was unnecessary: the hero collider is already solid.) The zone sphere sits on a child GameObject so it doesn't clash with the gate's solid blocker BoxCollider, and a relay routes the child's trigger events to the opener.

---

## 5. Remaining (could not verify autonomously — build-side gate)

The WO's playmode/in-exe walk-throughs (§3.5–§3.7: walk the hero into each gate, watch the violet sheet tear, walk through, watch it reform; confirm an enemy doesn't open it) need either Editor playmode or reaching the Village in the player (Title→HeroSelect→PetSelect→Village — not cleanly automatable headlessly). The logic is verified by static construction + a clean 0-error/0-warning build.

- **Owner ~2-minute confirm (editor):** open `Village.unity`, Play, walk the hero (WASD) toward the north gate. Expect the console `"[GateProximityOpener] gate-0 opened on hero approach"`, the force field eases open (~0.35 s), the hero walks through, then on the far side `"gate-0 closed on hero departure"` and the field reforms. Repeat for gate-1/2/3. Let a wave's Hollow Walkers approach a gate — the field should stay up and they attack it (no proximity open). Screenshots → `docs/wo08-gate-before.png` / `docs/wo08-gate-after.png`.

---

## 6. Suggested follow-up

- With WO-05/06/07/08 done, **WO-10 (MVP smoke test)** is unblocked (depends on WO-06, WO-07, WO-08). WO-10's interactive smoke test is the natural place to get eyes-on confirmation of this gate behaviour, the WO-07 ability HUD, and the WO-07-flagged Heart-HP/Crystals HUD push gap — all in one Village playmode pass.
- If build-side (not just editor) verification of these Village features becomes a recurring need, a dev-only `-bootScene Village` / skip-to-village hook (flagged in WO-06) would unblock it.
