**Status:** READY TO IMPLEMENT

# WORK ORDER 1137 — The hardcoded fallback catalog is a silent wrong-game, not a safety net

**Minted:** 2026-08-21 (CLI, banner bumped 1137 -> 1139 in the SAME edit alongside WO-1138)
**Lane:** Core / catalog. **Class:** ARCHITECTURE — duplicated state that has drifted four times.
**Found by:** the economy-drift fix lane during the 2026-08-21 gate sweep, answering the question
*"why does a hardcoded fallback cost table exist next to an authored catalog at all?"*

## THE FINDING

`CatalogBootstrap.RegisterFallback` (`Assets/_Modules/Village/Catalog/CatalogBootstrap.cs:159`)
holds hardcoded structure rows, registered ONLY when `LoadFromJson` returns 0 rows.

**Three facts that together make it worse than nothing:**

1. **It covers 3 of 28 catalog rows.** If it ever fires, the player does not get "a palette that is
   not empty" — they get a **different, silent, 3-row game**. Nothing on screen says the content
   failed to load.
2. **Its trigger is close to unreachable.** `CanonicalJson.Read` resolves `Resources` first, and
   `structures-catalog.json` is a Resources-embedded TextAsset compiled into the build. It cannot go
   missing the way a StreamingAssets file can.
3. **It has drifted at least FOUR times**, each caught only because a gate polices it:
   - `footprint` 2.5 vs 1.75
   - a `visualPrefabPath` pointing at PatriciaLight art **deleted 2026-06-09**
   - a missing `upgradeTexturePath` slot (LATENT — fallback-path L3 towers rendered untextured)
   - 21 cost fields, after the 2026-08-21 rescale (x4 / x10 / x14)

**This is structurally the §16 trap.** §16 exists because remote art with no local fallback
"installs perfectly, launches perfectly, and plays" with capsule enemies and no error on screen.
This is the same shape one layer in: **a failure path that presents as success.** A gate is
currently the only thing standing between us and shipping it.

## THE TWO HONEST OPTIONS (owner picks — do not assume)

### (a) DELETE IT AND FAIL LOUD — recommended
Remove `RegisterFallback`. On a catalog that will not load, `FlowTrace.Fail` + a visible
"content failed to load" state. **A catalog that cannot load is not a survivable condition**, and
pretending otherwise is exactly what hides it. This also deletes the duplicated state permanently,
which is the recurring failure class behind the stale WO number block (§2), the retired dependency
table (§5), the hardcoded repo root (§0) and the drifted R2 push (§16).

### (b) IF A FALLBACK MUST SURVIVE: GENERATE IT
An editor codegen step emits `RegisterFallback` **from** `structures-catalog.json` — all 28 rows —
as a build artifact. Drift stops being a gate failure and becomes **impossible**, and the existing
parity gate collapses to a cheap "the generated file is current" check.

⛔ **What is NOT an option: leaving it hand-maintained.** It has drifted four times in three months;
the fifth is a matter of when.

## ACCEPTANCE

- [ ] Owner rules (a) or (b)
- [ ] If (a): a catalog load failure is LOUD and visible, never a quiet 3-row game
- [ ] If (b): the generated table covers all 28 rows and regenerates from the catalog
- [ ] `BuildEconomyRegression.CheckFallbackParity` either becomes unnecessary (a) or becomes a
      freshness check (b) — ⛔ never weakened while a hand-maintained table survives

## NOT IN SCOPE

The 2026-08-21 cost rescale itself (owner-ruled, applied, correct), and the parity fix already
landed that same day.
