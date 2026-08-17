<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-06
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-06) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 613 — VFX moments: tower tier-up + construction complete

**Status:** READY TO IMPLEMENT — overnight creative lane (owner-requested 2026-07-06,
"have creative for overnight add vfx on tower upgrades and on timer cooldown finish").
**WO number 613 PROVISIONAL** (authority = MASTER_PIPELINES_BACKLOG; confirm on mint).
**Lane:** VFX/Audio (§9 — no gameplay dependencies, safe to parallelize).

## The two moments (hooks already exist, landed 2026-07-06)
1. **Tower tier-up** — `StructureFactory.ReskinForLevel(...)` returns true at the exact frame
   the new tier model replaces the old (called from `BuildModeController` upgrade + the
   legacy `StructureTierVisual.Apply` path for non-model entries). The moment deserves a
   burst: the upgrade should feel EARNED.
2. **Construction complete** — `UnderConstructionVisual.Reveal()` (WO-612): scaffold dims
   drop, tower pops to full color + starts firing. Deserves a small "built!" flourish.

## Creative direction (owner taste: earns-its-place, logical, readable)
- Tier-up: a rising golden ring/burst at the structure's base + brief upward shimmer —
  reads as "empowered", scaled to the structure's bounds. Distinct tint per tier is a
  nice-to-have (bronze/silver/gold — matches StructureTierVisual accents), NOT color-only
  (colorblind rule: the shape/motion carries the meaning).
- Timer finish: smaller — dust-settle puff + a soft gold flash on the fresh model. Should
  read at town-camera distance without being a firework.

## Reuse law (BINDING — the two-VFX-stack scar)
- Use the EXISTING pooled VFX stack (VFXManager / the pooled hosts visible in
  `[Flow:VFX] pool hosts` traces). NO new particle systems per-event at runtime — pool,
  bounded, one owner per concern. Check `docs/MASTER_CATALOG` VFX area + the Mirza Beig /
  Spells pack inventory (~1000 effects owned, ~38 wired) BEFORE authoring anything new.
- Audio: a small SFX on each moment via `CoreServices.Audio` (SfxId registry) if a fitting
  clip exists — do not add new audio assets without checking SfxClipLibrary first.

## Wiring points (exact)
- `Assets/_Modules/Village/Catalog/StructureFactory.cs` → end of `ReskinForLevel` success path.
- `Assets/_Modules/Village/BuildMode/UnderConstructionVisual.cs` → `Reveal()`.
- Keep Village → Core direction; VFX side stays presentation-only.

## Acceptance
- [ ] Upgrade a tower → burst fires once, scaled to the model, pooled (no allocation spike
      in `[Flow:VFX]`/`[Flow:Perf]` traces).
- [ ] Build-timer completion → flourish fires once at reveal; nothing fires when the flag
      degrades to instant (no job = no scaffold = no reveal moment... confirm: instant
      placement should get the SMALL flourish too so placement always has feedback — CLI call).
- [ ] `COMPILE_GATE_OK` + fleet pass; `FlowTrace.Step("VFX", ...)` on each fire.
- [ ] Push held for owner felt-pass (ten-year-old test — it should feel GOOD, not busy).
