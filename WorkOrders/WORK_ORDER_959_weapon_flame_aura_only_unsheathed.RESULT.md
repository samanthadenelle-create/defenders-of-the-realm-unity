# WO-959 RESULT — element weapon auras render only while drawn

**Status:** IMPLEMENTED — owner felt-verify owed on the "unsheathed" mapping
**Landed:** 2026-08-10 (wave-3 lane; the lane's session expired after writing `GearAura`'s call sites —
its two helper methods were already present; verified, gated and committed by the CLI seat)

## What "unsheathed" means at HEAD (the WO asked us to name the choice)

`EquipmentController._combatActive` — the flag `ApplyHoldPose` PHYSICALLY seats the prop by: hand when
drawn, back socket when sheathed. It is driven per-frame by HeroLocomotion's engagement signal, or by
the auto-mirror (BattleLock / wave Active / imminent Countdown) when nothing drives it. Sword on the
back in town = no flames; drawn for combat = flames. Exposed as
`EquipmentController.cs:1739 public bool IsWeaponDrawn => _combatActive && !(_seatingEditActive && _seatEditSheathed);`
which also respects a live SHEATHED Seating-Editor preview.

**Confirm this is the mapping you meant.** If you expect flames whenever the blade is in hand during a
lull, the mapping moves to the stance flag alone — one line.

## What changed

- `EquipmentController.cs:1748` — new `event Action<bool> OnCarryStateChanged`, raised once per FLIP at
  the end of `ApplyHoldPose` (`:1870-1879`), i.e. AFTER the prop is re-seated, so a subscriber measuring
  the blade sees it at its new parent. Change-only — HeroLocomotion calls `SetCombatActive` every frame,
  and an unguarded invoke would fire at frame rate. Wrapped in `Guard.Try` so a throwing subscriber
  cannot break the equip/pose path.
- `GearAura.cs:352-358` — ONE gate in the want resolution: while not drawn the weapon-seat want resolves
  to `None`, so acquire-on-draw and release-on-sheathe both ride the existing verified
  `StartWeapon`/`StopWeapon` paths. Because every weapon aura resolves through this want, the rule
  covers ALL element auras, not just flame — as the WO required.
- Robustness: the throttled reseat check releases a sheathed aura if the event was missed
  (`GearAura.cs:297`), and `_weaponPending` deliberately tracks the PRE-gate want so the flame reappears
  within the retry window after a draw. With no `EquipmentController` on a rig there IS no sheathe, so
  the aura is treated as DRAWN — fail-visible, never silently withheld.
- The BODY seat (heal relics) is deliberately NOT gated. The ruling is about weapons.

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[gear-aura-carry]` green)

## Oracle — what it proves

`GearAuraCarryGateRegression` (`GEAR_AURA_CARRY_OK`) on a REAL `EquipmentController`: a fresh hero starts
SHEATHED; `SetCombatActive(true)` flips `IsWeaponDrawn` and raises the event exactly once with `true`;
the per-frame no-change re-assert raises NOTHING; `SetCombatActive(false)` raises exactly once with
`false`. Plus a comment-stripped source lint on the three load-bearing lines (the gate clause, the
subscription, the invoke).

## Honest limits

The aura half cannot run headless (`StartWeapon` needs a live `VFXManager` and a measured blade prop),
so "the flames actually go out when she sheathes, and come back cleanly on draw with no flicker and no
stale blade anchor" is felt-verify only.

## Not touched

Gear ownership / auto-upgrade, the aura's look, VFX pool internals (WO-929/955).
