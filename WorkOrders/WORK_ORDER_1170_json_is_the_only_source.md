# WORK ORDER 1170 — JSON is the only source: retire every hand-mirrored C# fallback table

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 303/303 suites` (Builds/w3-c, Builds/w3-r). AWAITING OWNER FELT-VERIFY to close.

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

---

## SITES 4 AND 5 ARE WITHDRAWN - CLI lead, 2026-08-25 (spec-backed, verified at source)

⚠ **This note does not touch the status line above** - the board keeper owns status, and the
partial-landed wording there is deliberate. This records WHY the residual shrank.

**Site 4 (`Assets/_Modules/Onboarding/HeroCatalog.cs`) is WITHDRAWN, like site 6 - mis-specified,
not skipped.**
- ⛔ **`HeroCatalog` is NOT a fallback. It is ALWAYS in force.** Section 2's harm - *"the fallback
  only runs when the JSON is missing/corrupt, i.e. during an incident"* - **cannot occur here**,
  because there is no incident-only path to substitute anything during. The whole argument this
  ticket rests on does not reach this file.
- ⛔ **So the codegen+hash pattern does not transfer.** A generated file would be a **THIRD copy** of
  the data, not a replacement for a second one - the opposite of section 1's rule.
- ⭐ **And the drift is already pinned by a mechanism, not a comment.**
  `HeroKitMirrorRegression` - registered at `Assets/Editor/Regression/DataRegression.cs:944` - pins
  slot + name + signature against `abilities.json` **read at test time**. That is exactly what
  section 5's acceptance asks a hash gate to buy, obtained a different way.

**Site 5 (`Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs`) is WITHDRAWN too.**
- ⛔ **The cited line guards a single float whose own comment declares it deliberately TUNABLE**
  (`PackBountyRewardVariance`, *"TUNABLE opening balance, never a lock"*). Hash-gating it would
  **forbid the very divergence the comment reserves** - a gate that fires on intended behaviour.
- ⚠ **The survey table also mis-names the mirror.** This file's real hand-maintained fallback mirrors
  **`spawn-areas.json`, not `enemies.json`** - and ⭐ **`SpawnAreaEnemyIdRegression`
  (`DataRegression.cs:1159`) already covers the dangerous half** of it.

⚠ **Stale path corrected:** the section-4 table row 5 says `Village/World/OverworldEncounterSpawner.cs`.
The file lives at **`Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs`** (verified at
HEAD; `Village/World/` has no such file). Row 6 already cites the `Enemies/` folder correctly, so the
two rows disagreed with each other.

⭐ **What remains open is therefore section 6 alone:** the standing oracle that flags a NEW C#
collection literal duplicating a canonical JSON file.
