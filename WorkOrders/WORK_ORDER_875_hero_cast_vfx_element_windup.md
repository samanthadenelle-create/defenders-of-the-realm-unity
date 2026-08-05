# WORK ORDER 875 — Hero cast VFX: element-coded flash + windup telegraph (cast-on-magic)

**Status:** READY — child of WO-872. **Lane:** Combat/Hero VFX. **WO#:** UI-seat block; **875**.
**Origin:** owner 2026-08-04 — *"cast on magic."* Audit-backed (WO-872 §2, H1/H3/H7). **Layer:** B/D.
**Ties:** feeds WO-861 (Thrain/Sylas kits) — their cast VFX ride this.

## 1. RCA (audit, `HeroAbilities.cs`)
Hero spell casts are **largely SILENT**:
- `RegistryOnlyMotionVfx = true` (`:1887`) suppresses all `abilities.json` vfx keys and routes every cast through the
  `motion-castings.json` registry against a hardcoded target `"knight"` (`:1992`).
- **14 of ~17 registry `vfxKey` rows are empty** — only `Melee_Slash` + `Fireball_Cast` + `Heal_Cast` populated. So
  most Q/W/E casts show nothing; the ultimate (R) is silent by design (variant-4 keyword `null`, `:1892`).
- **H3 — no hero cast WINDUP:** `SpawnCastWindup` is only called for the enemy caster; the hero plays nothing during
  the 0.35–0.5s wind-up.
- **H7 — the element engine is BUILT but GATED OUT:** `SpellVfxFactory` (full fire/frost/arcane/holy cast+impact+
  projectile) + `AbilityVfxKit.PlayHeroAbility` are reachable only for keyless defs; `HasAuthoredHovlVfx` skip
  disables them for all stock abilities (`:2350`). The fix hook is already imported (`SpellVfxFactory.PlayCast`, `:2363`).

## 2. Fix
- **Un-gate `SpellVfxFactory.PlayCast`** for element-coded cast flashes (keyed on the ability's element/effect-shape),
  so Fire/Frost/Arcane/Holy casts each read distinct — instead of the empty knight-registry keys.
- **Add a hero cast WINDUP** at `CastRoutine` start (`:601`) — a short pre-cast telegraph (reuse the enemy
  `SpawnCastWindup` path), so a spell charges before it fires.
- Keep the working Heal family (H6) as-is. Route via `VFXManager`; owner-tags any element key / CLI maps verbatim.

## 3. Acceptance
- [ ] Hero spell casts show an element-appropriate cast flash (Fire/Frost/Arcane/Holy differ) + a windup telegraph;
      the ultimate is no longer silent. On-device; `CompileGate` green.
- [ ] Thrain (Fireball/Shell/Mend) + Sylas casts read distinctly (WO-861 alignment).

## 4. Do NOT
- Do NOT author new VFX (`SpellVfxFactory`/element library exist). Do NOT leave `RegistryOnlyMotionVfx` masking the
  element engine. WO-872 §4 rules.
