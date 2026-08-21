<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 768 — Thin-client streaming migration (finish the intended architecture)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** Addressables / Build / Content pipeline. Scope: **LARGE** (relocate ~600 MB-1 GB, migrate ~439 load sites, stand up CDN). Sequence AFTER current polish + WO-766.
**Full architecture + numbers:** `docs/THIN_CLIENT_STREAMING_ARCHITECTURE.md` (read first).

---

## Reality (verified)
FAT client today: every Addressables group is LOCAL, no CDN/remote catalog exists, and 768 MB
of art lives in `Resources/` (always baked) + big scene-referenced packs. APK = 453 MB,
textures = 73%. "Streaming" is scaffolding (`HeroAssetLoader`, `AddressablesGroupConfig`) that
always falls back to `Resources.Load`.

## Biggest lever
`Resources/Enemies` = **504 MB** (half the Resources tree), loaded per-wave → ideal to stream.
Then Heroes 81 MB (=WO-282, on HOLD), Structures 32 MB, then VFX/environment packs.

## Phases (each independently shippable + measurable)
- **Phase 0** — stand up remote host (Cloudflare R2 = free egress, recommended) + `BuildRemoteCatalog=1` + Remote.LoadPath URL. (= WO-281 build blocker.)
- **Phase 1** — Enemies → Remote group + async spawn-load. The 504 MB win. DO THIS FIRST + MEASURE.
- **Phase 2** — Heroes → Remote per-selection (= WO-282, un-HOLD; seam built).
- **Phase 3** — Structures/Towers art → Remote (stream on browse/place).
- **Phase 4** — VFX (Hovl/Lana) + Environment (Polyperfect/Art) → Remote (scene refs → AssetReference).
- **Phase 5** — migrate ~439 `Resources.Load` → async loaders; first-run download screen; offline cache/fallback; handle lifecycle.

## Projected savings (validate by doing Phase 1)
- Phase 1 only: APK ~250-320 MB.  Phase 1-3: ~180-260 MB.  All phases: ~120-180 MB thin boot.
- Bytes move to CDN (first-run/on-demand), not deleted.

## Bonus (beyond size)
Remote catalog = push new enemies/events/art/balance **without a store resubmit** — live-ops
superpower for a crypto live game.

## Caveats
CDN egress cost (R2 free egress mitigates); first-run download UX; offline fallback; async
load-timing bug risk; multi-week effort. Not needed for Seeker sideload testing; worth it before
Google-at-scale. Cheaper Play-only interim: AAB + Play Asset Delivery.

## Acceptance (Phase 1 gate — prove the model before committing to the rest)
- [ ] Remote host live; a remote catalog builds + loads.
- [ ] `Resources/Enemies` relocated to a Remote group; enemy spawn loads async + releases on clear.
- [ ] Rebuilt APK size measured before/after (the REAL number).
- [ ] No enemy-spawn regressions (load-before-visible; offline fallback if remote unreachable).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`.

## Reconciliation
WO-282 (Phase 2), WO-281 (Phase 0), WO-191 (WebGL parallel), WO-767 (orthogonal cap tradeoff).
Data source: read-only RCA 2026-07-24 (cited in the architecture doc), per §12.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
