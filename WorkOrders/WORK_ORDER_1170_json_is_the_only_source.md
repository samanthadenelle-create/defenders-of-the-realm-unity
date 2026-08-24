# WORK ORDER 1170 — JSON is the only source: retire every hand-mirrored C# fallback table

**Status:** READY — owner ruling, 2026-08-24. §4 is the ranked work list.

**Minted:** 2026-08-24 (CLI), banner bumped 1170 → 1171 in the same edit.
**Provenance:** owner, 2026-08-24, verbatim — *"We need to not have anything pulled other than from
json"* · *"Otherwise we always expose risks like that and it's sloppy development"*.

**Trigger:** unlocking the iron node (WO-1168) required editing `build-categories.json` **and** a
hand-written copy of the same data in `BuildCategoryRegistry.cs`. I edited the JSON and nearly
shipped without the C# — which would have worked until the first day the JSON failed to parse, at
which point iron silently closes again with nothing on screen.

---

## 1. The rule

> ## A canonical JSON file is the ONLY place its data may be written.
> A fallback may be **GENERATED** from that JSON and **gated on a content hash**. It may never be
> hand-maintained.

The project already proved the pattern: **WO-1137** codegen'd `CatalogFallbackData.g.cs` from
`structures-catalog.json` behind a `[fallback-parity]` sha256 gate. That gate **caught a stale
fallback during this very session**, within seconds of the catalog changing. It works. It just was
never applied to anything else.

## 2. Why a WRONG fallback is worse than NO fallback

A fallback exists so a parse failure does not brick the game. But a fallback that has drifted does
something worse than crashing: **it silently substitutes different game rules.** Nothing logs, no
marker goes red, and the game keeps running — on last month's numbers.

⛔ **And it fails at exactly the worst moment.** The fallback only runs when the JSON is
missing/corrupt — i.e. during an incident — and that is precisely when nobody can tell whether odd
behaviour is the incident or the fallback.

## 3. ⛔ THE WORST CASE IS COMBAT BALANCE, NOT PALETTES

`Assets/_Modules/Village/Buildings/Tower.cs:1400` — its own doc comment:

> *"The hard-coded fallback table — **identical to the shipped JSON** — so a missing/corrupt
> tower-perks.json can never silently make upgrades a no-op again."*

It hardcodes tier damage multipliers, damage adds, range adds, fire-rate multipliers and signature
abilities. **"Identical to the shipped JSON" is an assertion with nothing enforcing it.** The moment
someone tunes `tower-perks.json`, the two disagree, and a parse failure quietly reverts every tower
in the game to the old balance.

⚠ Note the comment's own history: it was written to fix upgrades *silently doing nothing*. The cure
for one silent failure was a second, quieter one.

## 4. The survey — ranked by blast radius

| # | File | Duplicates | Risk |
|---|---|---|---|
| 1 | `Village/Buildings/Tower.cs:1400` `BuiltInFallback()` | `tower-perks.json` | ⛔ **combat balance** — silent rebalance |
| 2 | `Village/Catalog/BuildCategoryRegistry.cs` (~193, 210, 255, 268) | `build-categories.json` | ⛔ **economy gating** — the WO-1168 defect; 3 separate tables (types, `lockedIds`, `visibleLockedIds`) |
| 3 | `Core/Platform/StakeRewardsResolver.cs:213` `DefaultTiers()` | `stake-rewards.json` | ⚠ **rewards** — pays the wrong amount |
| 4 | `Onboarding/HeroCatalog.cs:143` | hero rows + Q/W/E/R slots | ⚠ self-described "hand-mirror kept in sync" |
| 5 | `Village/Enemies/OverworldEncounterSpawner.cs:925` | `enemies.json` seed | spawn mix |
| 6 | `Village/Enemies/Enemy.cs:1046`, `EnemyTypeVfxLibrary.cs:17` | VFX sets | ⚠ both record that the fallback **was taken forever** unnoticed |

⚠ **Rows 5–6 are the proof this is not theoretical.** Both files already document that their
hardcoded fallback was silently in force for an extended period because the data never loaded. The
failure mode has happened, more than once, and was found by accident.

## 5. Two sanctioned outcomes per site — pick one, never a third

1. **CODEGEN + hash gate** (the WO-1137 pattern) — for data the game genuinely cannot boot without.
   The generated file is `*.g.cs`, never hand-edited, and a regression compares the sha256 of the
   source JSON against the hash recorded at generation time.
2. **DELETE the fallback and fail LOUDLY** — for data whose absence should stop the flow.
   `FlowTrace.Fail` + a visible refusal beats invented values (CLAUDE.md §12: no silent failures).

⛔ **Not sanctioned:** keeping a hand-written table with a "keep the two in sync" comment. That
comment is not a mechanism — it is a hope, and this repo has now lost to it repeatedly (the stale WO
number block, the retired asmdef table, the 1-of-1 treasury in nine files, and this).

## 6. Order of work

1. **`BuildCategoryRegistry`** — the site that triggered the ruling; three tables, clear shape.
2. **`Tower.cs`** — highest blast radius. Codegen; balance must not live in two places.
3. `StakeRewardsResolver`, `HeroCatalog`.
4. Enemy VFX / spawn seeds — decide delete-vs-generate per site; both already failed silently once.
5. **Add a standing oracle:** flag any new C# collection literal that duplicates a canonical JSON
   file, so the pattern cannot come back the way it keeps coming back.

## 7. Acceptance

- [ ] No hand-maintained table mirrors a canonical JSON file
- [ ] Every surviving fallback is `*.g.cs` + a hash-parity suite that FAILS on drift (proven by
      deliberately editing the JSON and watching it go red — a gate nobody has seen fail is not a gate)
- [ ] Every deleted fallback fails loudly with a worded reason, never silently
- [ ] `REGRESSION_OK` green; `CATALOG_FALLBACK_GEN_OK` still emitted
