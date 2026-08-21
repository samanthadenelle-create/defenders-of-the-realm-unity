<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 767 — Tighten texture caps (APK size vs quality lever)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** Build/Art optimization. Scope: **SMALL** (cap-table edit + Apply + rebuild + visual verify).

---

## 0. Why (code-verified 2026-07-24)

The Android APK is **453 MB**; the build report shows **textures = 72.9%** of it (554.8 MB in-build / 612 MB ASTC est). Everything else combined is ~27%. So size = a texture question.

BUT the resolution caps are **ALREADY applied** — `TextureShrinkAudit.Report` (dry run) shows **0 MB** additional saving from re-running at the current caps + ASTC format. So there is **no free win left**; textures are already ASTC-capped. Going smaller = **tightening the caps themselves = a deliberate quality-vs-size tradeoff.** This WO captures that lever for when the owner wants it (not a silent auto-shrink — quality is the owner's call).

## 1. The current cap table (`TextureShrinkAudit.cs:135-139`, per-pack)
| Category | maxSize | ASTC | crunch |
|---|---|---|---|
| Hero | 2048 | 4x4 | none (hero = the star) |
| KayKit | 1024 | 6x6 | q50 |
| Polyperfect | 512 | 6x6 | q50 |
| LanaVfx | 512 | 6x6 | q50 |
| Leohpaz | 1024 | 6x6 | q50 |
| Vfx (Spells Pack — many 8192 sources!) | 1024 | 6x6 | q50 |
| Ui | 1024 | 6x6 | none (legibility) |
| Default | 1024 | 6x6 | none |

## 2. The lever (owner picks aggressiveness)
Each step trades visible fidelity for MB. Options, least-risky first:
- **A (safe):** Hero 2048→1024 (the hero eats the most per-texture; 4× fewer pixels). Likely the single biggest win with the least felt loss on a phone screen.
- **B:** the 1024 packs (KayKit/Leohpaz/Vfx/Ui/Default) → 512 where the art tolerates it. UI stays ≥1024 for legibility.
- **C:** raise crunch quality/aggressiveness on the crunched packs (smaller, more artifacts).
- **D (targeted):** hunt the specific worst offenders in `Builds/texture-shrink-report.txt` (per-category MB) and cap only those.
- Read the detailed per-category report at `Builds/texture-shrink-report.txt` to target the fattest packs first (data-driven, not blanket).

## 3. Process
1. Owner picks the aggressiveness (A / A+B / …).
2. Edit the `CatRule` cap table (`TextureShrinkAudit.cs:135-139`).
3. `TextureShrinkAudit.Apply` (Android ASTC override, idempotent) → reimport.
4. Rebuild APK (`AndroidBuild.BuildSeekerApk`), measure new size.
5. **Visual verify** (memory `headless-screenshot-verify-ui-before-build`): screenshot the hub + hero on-device/headless; owner approves the fidelity at the new caps BEFORE it's the shipping build.
6. `COMPILE_GATE_OK` (no code logic change; .meta churn only).

## 4. Acceptance criteria
- [ ] Owner-approved cap changes applied via `TextureShrinkAudit.Apply`.
- [ ] New APK size measured + reported (before/after).
- [ ] Visual verify: owner confirms fidelity acceptable at the new caps (hero + hub + UI legibility).
- [ ] Reversible: caps live in one table; revert = restore the table + Apply.

## 5. Notes
- Also consider an **AAB** for Play (device-specific splits → each user downloads less than the universal APK) — a separate size win that does NOT touch quality. Pairs with the Google-Play path in `docs/PUBLISHING_STEPS.md`.
- Sounds (5.5%), Shaders (6.2%), Meshes (6.0%) are minor vs textures — not worth the risk until textures are dialed.
- Not urgent for Seeker sideload testing (453 MB is fine there); matters for Play polish + install conversion.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
