# WORK ORDER 672 — Unified Structure Damage Lifecycle + Presentation (F8-50)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
"is there a way to visually tell what is damaged? health bar or any notification, damaged maybe on fire?
Inoperable till repaired?" + "do we have a condition if isdamagable is true and health = 0 then destroy?")
**Census (verified from code, 2026-07-11):** see the per-type table in the session ledger — summarized below.
**Unifies:** F8-39 (towers vanish on death) · F8-42 (repair costs) · F8-50 (this) · the F8-45 report's
tower blind spot.
**Law:** One Model (lifecycle = capability data, systems are readers) · presentation never touches
objects · POOL by default · colorblind rule (never color-only).

## The problem (census verdict)
`IDamageableStructure` exposes only `IsAlive` + `ApplyContactDamage`. At hp==0 the types split three ways
in code: Tower/DefenseTower/ArcaneTower/HarvestSite hard-`Destroy(gameObject)` (vanish, no ruin, not
repairable — the F8-39 class); WallSegment/Gate/Building/ResourceCollector/Heart persist as
shells (repairable). Damaged visuals: Gate force-field collapse shader + the Heart's 7-state crystal are
the ONLY tells; no structure has a health bar, no fire/smoke, no mesh damage stages. Functional
degradation exists only on ResourceCollector (accrual × HpFraction; broken = zero).

## The design (one lifecycle, everywhere)
State machine as DATA (thresholds in catalog data — "data only always"):
```
Intact (hp=max)
  → Damaged (hp < max): world tells ON (bar + burn VFX at thresholds), function scales where modeled
  → Broken (hp == 0): INOPERABLE SHELL — stays in the world ("either they exist or do not"),
      stops functioning (tower stops firing, collector stops accruing), costed Repair restores (F8-42);
      destroyed-forever only via player demolish / full-rebuild flow.
```

### Slice A — lifecycle unification (the Q2 answer becomes a rule)
1. Kill the `Destroy(gameObject)` at 0 HP in Tower.cs:735-746, DefenseTower.cs:128-134,
   ArcaneTower.cs:122-128, HarvestSite.cs:57-61 → adopt the ResourceCollector Broken model
   (`_broken`, `IsAlive=>hp>0 && !broken`, `Repair()`); keep the `Destroyed` events firing (renamed
   semantics: "broke") so existing listeners (wave targeting etc.) still release targets.
2. Surface `HpFraction` uniformly (interface addition or a capability component) so the F8-45
   WaveDamageReport covers towers with zero special-casing (its header documents this exact gap).
3. Broken tower/harvest persistence: HP/broken ride the same persistence their layout does
   (BaseLayout save path for placed structures — additive schema note, follow the v29 precedent).

### Slice B — damage presentation (ONE observer, pooled)
New single presentation system (e.g. `StructureDamageVisuals`, Village presentation area) that OBSERVES
`HpFraction` and drives, per thresholds from data:
- **Health bar:** `FloatingHealthBar.Attach` (Combat/FloatingHealthBar.cs:119 — type-agnostic
  delegates, hideAtFull=true) — bar appears ONLY when damaged. Reuses the proven enemy chip.
- **≤50% ("smoldering"):** `Ember_Burn` Hovl loop at reduced scale (no smoke key exists in the
  30-key catalog — reuse Ember_Burn scaled, or import a smoke prefab as its own mini-task).
- **≤25% ("on fire"):** `Ember_Burn` full scale + the existing critical pulse on the bar.
- **Broken:** burst `Raid_Explosion` (catalog key exists, currently ZERO runtime callers) + persistent
  ember + desaturated/darkened material state + bar pinned empty. Shape/motion carries the meaning
  (colorblind-safe by construction).
- All VFX via `VFXManager.PlayKey` (pooled); one owner; caps on simultaneous burn loops.

### Slice C — functional gating
Tower/DefenseTower/ArcaneTower: firing gated on `!broken` (binary at broken; no partial-HP nerf V1 —
keep combat readable). HarvestSite: harvest gated on `!broken` (already gates on hp>0). Collector:
already correct (WO-671 wave).

### Slice E — REPAIR FROM THE REPORT (owner 2026-07-11: "i saw a damage report but could not repair")
The report is display-only today; repair = only the worst-repair tap prompt. V1 shape (one action, one
button — the owner UI law):
- **"Repair All — N◈" button on the damage report panel** (the wave-clear EndState CTA seat): sums
  `WallRepairController.CostFor` across listed rows, spends crystals via the existing repair spend path,
  repairs worst-first until the wallet runs dry (partial repair = honest; rows update). Disabled-with-cost
  when unaffordable (informative, not dead).
- Granular path stays the in-world flow: walk to a damaged structure → the existing repair tap prompt
  (WallRepairController.HandleTap) — now discoverable because Slice B makes damage visible.
- Broken structures (hp==0) repair at the full F8-42 rebuild cost through the same button.

### Slice D — data
`damage-states.json` (or extend structures-catalog rows): thresholds {smolder:0.5, fire:0.25},
vfx keys, bar offset — per-type overridable, defaults global. Dual-copy rule applies.

## Acceptance
- [ ] No structure `Destroy(gameObject)`s itself from damage anywhere (grep gate).
- [ ] Owner can SEE at a glance: damaged (bar + smolder), burning (fire), broken (dark + ember + empty bar).
- [ ] Broken tower stops firing; repair restores it; wave damage report lists towers.
- [ ] F8-39 unreproducible (nothing vanishes/reappears — the class is gone).
- [ ] `COMPILE_GATE_OK` + DataRegression + fleet; owner felt-verify per the ten-year-old test.

## Do NOT
- Touch the Gate force-field collapse or the Heart crystal states (already-good bespoke tells — the
  new system must skip types that opt out via data).
- Instantiate VFX directly; two systems owning burn effects; color-only signaling.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
