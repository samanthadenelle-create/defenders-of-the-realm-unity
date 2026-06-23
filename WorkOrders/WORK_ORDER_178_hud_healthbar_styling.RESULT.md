# WORK ORDER 178 — RESULT (world-space combat unit health bars)

**Status: IMPLEMENTED (code-only, pending CLI build-verify)**
**Date:** 2026-05-31

> NOTE ON SCOPE: the task brief routed to me asked for **code-built world-space
> health bars over Enemy + HeroHealth** (per-unit floating bars). The on-disk
> WORK_ORDER_178 file is worded as a **screen-space `VillageHudController` USS
> restyle** of the Heart/Hero HUD bars. These are different surfaces. I executed
> the brief (world-space per-unit bars), styled to match the themed HUD palette so
> the two reconcile rather than conflict. The `VillageHudController` screen bars
> were **not** touched. If the owner actually wants the screen-HUD USS restyle,
> that is a separate, additive pass on `VillageHudController` (no overlap with this).

## What was done
Added a self-contained, code-built **world-space** HP bar that floats over combat
units, mirroring the existing `NodeFillIndicator` world-space-bar pattern (uGUI
world-space Canvas + Image, billboarded — no UXML/UIDocument, which don't render
in builds).

### Files changed
- **NEW** `Assets/_Modules/Village/Combat/FloatingHealthBar.cs`
  - Type-agnostic floating HP bar. Reads the unit via two delegates (`Func<float>`
    fraction, `Func<bool>` isDead) so it never references a concrete type — keeps
    DeNelle.Village → Core asmdef rules intact (no asmdef change; folder is under
    the Village assembly tree).
  - Themed to the HUD palette (arcane-violet frame `0.10,0.08,0.16`, gold rim
    `1,0.86,0.45`, green `0.30,0.78,0.40` → amber `1,0.74,0.18` → red
    `0.86,0.12,0.10`) so it reads as part of the same styled set as the quest
    panels / ability bar.
  - States: healthy/warning/critical color, critical adds a gentle pulse; enemy
    bars hide at full HP (declutter) and pop in on first damage; billboards to the
    camera; constant ~1.5m width regardless of host import scale.
  - `destroyOnDead` flag: enemies tear the bar down on death; the hero (which
    respawns) only hides it and auto-recovers on revive.
- **EDIT** `Assets/_Modules/Village/Enemies/Enemy.cs`
  - Added `_healthBar` field + `EnsureHealthBar()` called from `Awake()` (mirrors
    `EnsureHitReaction`). Computes head height from renderer bounds; feeds
    `HpFraction` / `_dead`. No combat-math change.
- **EDIT** `Assets/_Modules/Village/Hero/HeroHealth.cs`
  - `HeroHealthBootstrap` now also attaches a `FloatingHealthBar` to the hero
    (feeds `Fraction` / `!IsAlive`, `hideAtFull:false`, `destroyOnDead:false`).
    The existing top-left IMGUI bar (`OnGUI`) is untouched — it stays as the
    screen readout; this adds the over-the-head bar. No gameplay change.

## Why
Regular enemies had **no** per-unit HP bar (only `BossHealthBar`, a screen-space
boss-only UIDocument bar) and the hero had only an IMGUI screen bar. This adds the
missing over-the-head HP read for both, styled to the themed HUD.

## Risks
- Bars are built at runtime via the self-attach pattern (same as
  `EnemyHitReaction` / `HeroHitReaction`) — no scene/prefab edits, no bake.
- World-space uGUI Canvas per enemy: lightweight (4 Images), but for very large
  swarms consider pooling later (not needed at current wave sizes).
- Height offset is read from renderer bounds in `Awake` (enemy) / bootstrap
  (hero); imported meshes are present at instantiation so bounds are valid.

## Test steps (what the player sees)
1. Enter the Village wave loop and start a wave.
2. Hit an enemy — a slim themed bar (violet frame, gold rim, green fill) pops in
   over its head, draining as it takes damage. Bar turns amber ≤55%, red + pulse
   ≤25%, and disappears when the enemy dies.
3. Full-HP enemies show no bar (declutter) until first damaged.
4. The hero has an always-visible over-the-head bar in the same style; it drains
   on contact damage, hides on death, and reappears full on respawn.

## Done checklist
- [x] World-space themed HP bars over Enemy + Hero (frame/rim/fill/states)
- [x] HP-state coloring (green/amber/red + critical pulse), palette-matched
- [x] Combat math / bindings unchanged (read-only fraction delegates)
- [x] Code-built (no UXML); brace balance verified (14/14, 72/72, 25/25); no bake
- [x] asmdef untouched (Village → Core preserved)
