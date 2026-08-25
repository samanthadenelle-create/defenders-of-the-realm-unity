> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: HeroAbilities.cs:1887 still has `RegistryOnlyMotionVfx = true` (VERIFIED at source) - the exact mask sec.4 of this WO forbids, still gating 4 call sites.
> The previous Status line read "READY - child of WO-872." and was wrong.

# WORK ORDER 875 — Hero cast VFX: element-coded flash + windup telegraph (cast-on-magic)

**Status:** FIXED 2026-08-25 - source acceptance landed at `1772be8af`; awaiting owner device visual/capture approval.
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

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 3: **UNBLOCKED. This is a MAP, not a creative pick.**

The VFX authority split is now **canon in `FOUNDATIONAL_RULINGS.md` §4** — read it there; ⛔ it is
deliberately **not restated here**, per that file's own no-paraphrase rule.

What it means for this ticket: `SpellVfxFactory` already contains a full fire / frost / arcane / holy
cast library and `RegistryOnlyMotionVfx = true` suppresses it, so most hero casts are silent. Mapping
*fire ability → the prefab named fire* falls in the **lead's** column. Owner, verbatim:

> *"If a prefab literally says Fire Cast, mapping a fire ability to it isn't creative direction.
> It's plugging the toaster into the toaster outlet."*

⚠ The standing rule (*owner tags the key, CLI maps verbatim, CLI never picks*) is **scoped, not
retired** — where an existing label answers the question, the lead proceeds and **sends her a capture**.

**Status → READY.**
