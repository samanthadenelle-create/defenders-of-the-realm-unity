# WO-671 RESULT — Action bundle rows + runtime ActionBundlePlayer (DONE)

**Committed:** `8084d8ee` (2026-07-11, lane B) + runtime wiring `17862c51` (07-12 morning:
ActionBundleCatalog wired to runtime for the first time — the ONLY VFX authority is owner Motion
Caster rows per the registry-only directive). Gated in the 07-11/12 arcs. RESULT written
retroactively 2026-07-13 during the sync handoff.

- Bundle rows: targets×keywords → {clip, vfxKey, sfxId, vfxDelay, attachBone, playOneShot} in
  `motion-castings.json` (dual-copy); empty registry = byte-identical bakes (EditMode-gated).
- Runtime `ActionBundlePlayer` consumes rows; abilities.json Vfx* defaults + the hardcoded
  per-swing Melee_Slash burst turned OFF same arc (owner directive 07-12).
- Owner-authored rows (sound drops: Heal/Spell_Impact/Swords_Clash via the impact phase,
  origin-pushed `1ee7b6af`) are live through this path.
