# COVERAGE FINDINGS LEDGER — the ROI accounting for the full-coverage system

**Purpose:** track EVERY issue the step-in/step-out + DataRegression + fleet coverage system surfaces,
so the token spend on it is judged by DATA (real defects caught) — investment vs. foolhardy — not a vibe.
Updated every catch pass. A "catch pass" = `DataRegression.RunAll` (all oracles, seconds) + one AutoPilot
fleet run. Convergence ("every path clear") = K≥2 consecutive passes with ZERO new findings.

**Classes:** `real-bug` (a genuine defect) · `fail-by-design` (oracle correctly detects a known/accepted
gap until a data/save/art fix lands) · `coverage-artifact` (a harness/instrumentation gap, not a game bug).

**Status:** `proven` (named by captured data) · `to-verify` (oracle/instrument written, awaiting the run
that fires it) · `fleet-pending` (needs a runtime/render drive) · `fixed` · `owner-decision`.

---

## ROI SCORECARD (running)

| Metric | Value (as of 2026-07-08, post-Wave-1 catch pass) |
|---|---|
| Token spend on the coverage system | coverage-map ~1.64M + pilot instrument+oracles ~0.9M + Wave-1 8 teams ~1.66M + enemy oracle ~0.12M ≈ **4.3M** |
| **Real bugs PROVEN by data** | **5 classes** — F8-39 tower-respawn, F8-41 5 untargetable towers, arena 2 untextured surfaces, **Wood/Iron dual-wallet divergence (COV-021, NEW — exposed once the harness was honest)**, version-triple guard armed. *(COV-015 pet-skill-trees RETRACTED — was an oracle-shape bug, not a game bug; content then retired.)* |
| Dead content surfaced + retired (earns-its-place) | pet skill-tree stack (COV-022, deleted) + pet-combat-vs-pivot reconciled (COV-023, gated) — the coverage census made the owner's "pets don't need a skill tree" checkable |
| Last-100-bugs audit (do fixes hold?) | 107 bugs: 22 oracle + 36 fleet-probe + **49 NONE**. **Zero reoccurrences** — every fixed bug with a check passes (fleet confirmed, exe 18:06). Doc: `LAST_100_BUGS_AUDIT_2026-07-08.md`. |
| Open-bug value test (does it catch what's broken?) | 23 open items: **8 DETECTED / 6 COVERED-BUT-GREEN / 9 BLIND** = **40% detection of actionable defects**. Strong on structural/data/logic; blind on runtime-visual/UI/feel. **6 false-green checks** = the key finding. Highest-ROI next: convert those 6 to failing assertions → 40%→~70%, no new instrumentation. Doc: `COVERAGE_VALUE_SCORECARD_2026-07-08.md`. |
| Real bugs instrumented, fleet-pending | 3 (COV-004 arena pole, COV-005 walk-cast, COV-006 death UI) |
| Fail-by-design confirmed (real gap, needs data/save fix) | 1 (COV-013 pet-slot); COV-012 save-drops still to-oracle |
| **False leads REFUTED by real-path oracles** | **2** (COV-010 HeroPortraits, COV-011 Aegis) — saved chasing non-bugs |
| Oracles GREEN (invariants locked + regression-guarded) | 14/21 (incl. enemy rig+color 10/10) |
| Harness false-fails to fix (oracle maturity cost) | 2 (COV-017/018 need throwaway GameState) |
| To-triage | 1 (COV-016 packs 13-vs-5) |
| Durable asset created | a **~30-second 21-oracle full-coverage gate** (amortizes every future session) + step-in/step-out on 5→(70+) flows across the whole codebase |

**HONEST ROI READ (interim — leaning INVESTMENT, not foolhardy):**
- **Pro:** the system caught a genuinely NEW silent bug (COV-015 pet-skill-trees mapping break) nothing
  else was watching; re-proved 3 known roots; **refuted 2 false leads** (real value — the map alone would
  have sent us fixing non-bugs); and produced a permanent ~30s gate that fires on future drift (version-triple,
  catalog mapping, rig/color, scene registration). That gate pays back every session — the spend amortizes.
- **Con / honest:** most Wave-1 oracles (14/21) PASSED — those areas were already healthy, so that coverage
  is *preventive* (guards regressions) not *curative* (caught a live bug). And 2 oracles false-failed on a
  harness gap — a normal maturation cost, but it means the gate isn't fully trustworthy until fixed.
- **Verdict logic:** investment IF (a) real defects surfaced we'd otherwise ship [YES — COV-015 + the tower
  bugs], AND (b) the gate is a durable cheap regression net [YES — 30s, 21 oracles]. **Both hold.** FINAL
  number recorded at loop convergence (Wave 2 + fleet + fixes + re-run until K passes are zero-new).

---

## FINDINGS

| ID | Area | Finding (root) | Proving source | Class | Status | Fix |
|---|---|---|---|---|---|---|
| COV-001 | village-systems | Towers vanish on death: `BaseLayoutLoader` hub-scope guard skips BaseLayout replay in `MainCastle_Hall` → never rebuilt on death→GoCastle() reload | `RunAll` `[tower-respawn]` FAIL | real-bug | proven | remove MainCastle_Hall from `_hubScenesNoBaseLayout` OR rebuild on respawn |
| COV-002 | village-enemies-world | 5 towers untargetable: `DefenseTower`/`ArcaneTower` don't implement `IDamageableStructure` → enemy sweep returns null → waves can't attack any tower | `RunAll` `[def-target]` FAIL (5 named) | real-bug | proven | owner-decision: implement interface OR exclude towers by design |
| COV-003 | resources-art / arena | `ForestClearingArena` `Ground`/`DistantGround` materials bind no `_BaseMap`/`_MainTex` → untextured surfaces | `RunAll` `[arena-prefab]` FAIL (2) | real-bug | proven | bind base texture / fix `new Material()`-serializes-null |
| COV-004 | village-systems / arena | The giant untextured "arena pole" (F8-37) — NOT in the prefab, created at runtime | `BattleArena` `AUDIT` (silo 4) + flag_05/02 | real-bug | fleet-pending | run the arena fleet path; the AUDIT names the object |
| COV-005 | village-enemies-world | Walk-while-cast (F8-38): `RootedCast` sets `isStopped=true` but `DriveNav` un-stops it every frame (no `_casting` guard) | `Enemy.cs` `drivenav-casting` throttle | real-bug | fleet-pending | add `_casting` suppression in DriveNav (deferred) |
| COV-006 | hud / village-hero | Death popups (F8-15) bypass PanelManager (stack) + `GameOverScreen` freezes timeScale with restore only on Retry/scene-load | `DeathTrace` bypass Warn + freeze step-in/out | real-bug | fleet-pending | route end-states through PanelManager; guarantee timeScale restore |
| COV-007 | devtools/autopilot | Overworld probes skipped "no hero": driver cached a one-shot hero handle, fake-nulled on scene stream | fleet skip-lines + `AutoPilotProbes.RefreshHero` contrast | coverage-artifact | fixed | `EnsureHero(phase)` re-resolve (committed `c2aa8337`) |
| COV-008 | economy-meta | Glimmer `TryPurchase/TryAddGlimmer` + Crypto reflection grant can debit-without-grant (player pays, gets nothing) — untraced | map RCA | real-bug | to-verify | Wave-1 `GlimmerEconomyRegression` + instrument (in flight) |
| COV-009 | core | `CanonicalJson.Read` returns null on dual-copy miss with zero log — silent at the data hub every catalog uses | map RCA | real-bug | to-verify | Wave-1 `CoreDataHubRegression` + instrument (in flight) |
| COV-010 | resources-art | HeroPortraits blank (map hypothesis) | `[art-resource]` GREEN | REFUTED | not-a-bug | oracle drove real path — portraits/atlas/icons all resolve. Map located a stale candidate. |
| COV-011 | village-hero | Aegis ward unreachable (map hypothesis) | `[aegis]` GREEN | REFUTED | not-a-bug | oracle proves every aegis weapon has a co-equippable aegis armor → ward reachable. Map's setId read was stale. |
| COV-012 | core / save | `Tribes/Settlements/Wards` not in SaveSchema → dropped on reload | map RCA (MASTER_CATALOG #20) | fail-by-design | to-verify | not yet oracled (core-save asserts version-triple only); Wave-2/save-owner |
| COV-013 | economy-meta | Pet active-slot not persisted (only StarterPetId survives reload) | `[glimmer]` FAIL (fail-by-design) | fail-by-design | proven | add persisted slot field + SaveSchema round-trip; oracle then flips green |
| COV-014 | core | Version-triple drift risk (SaveSchema vs GameState vs Migrator) | `[core-save]` GREEN | real-bug (guarded) | fixed/guarded | aligned at 28/28/28 today; oracle fires the instant a schema bump lands without its migrator step |
| COV-015 | economy-meta | pet-skill-trees "0 trees" (oracle read a keyed object as an array) — then the CONTENT was RETIRED entirely | RCA + owner ruling | REFUTED→retired | resolved | NOT a bug (data parsed fine); the whole skill-tree stack deleted `279c56be` (vestigial, nothing read it) → oracle assertion removed |
| COV-016 | economy-meta | `packs.json` 13 packs — INTENDED growth; oracle's "canon 5" was stale | monetization review 07-02 | stale-oracle | fixed | oracle updated to 13 packs / tiers 1–13. Follow-up: `PackCatalogTest` still asserts 5-tier ladder |
| COV-017 | village-systems | `OfflineHarvest` false-fail (no live GameState in editmode) | `[offline-harvest]` now GREEN | coverage-artifact | fixed `821bac80` | throwaway GameState installed via reflection; now asserts real clock logic + PASSES |
| COV-018 | village-systems | `VillageEconomy` false-fail (same) | `[village-econ]` now RUNS | coverage-artifact | fixed `821bac80` | fixed → exposed the real COV-021 below |
| COV-019 | village-enemies-world | Every enemy rigged + colored (owner ask) | `[enemy-rig-color]` GREEN | not-a-bug | proven-healthy | 10/10 rigged+colored in ~30s; the check the owner requested, now a permanent gate |
| COV-021 | village-systems | **Wood/Iron dual-wallet divergence** — `econ.Grant(wood:25)` moves the shop pool but the upgrade `ResourceLedger` reads `GameState.Wood` (0) → wood income invisible to building-upgrade | `[village-econ]` FAIL (exposed once harness honest) | real-bug | proven | route income through `GrantSpendable` or unify the pools — owner/economy call |
| COV-022 | economy-meta/hud | Pet SKILL TREE was vestigial — renders + stores unlocks but NOTHING reads them; level counter never incremented; contradicts "no pets in battle" | pet audit (cited) | dead-content | retired `279c56be` | owner ruled RETIRE; stack deleted, save-safe |
| COV-023 | pets | Pet COMBAT + leveling wired in code but contradicts the 06-22 "no pets in battle" pivot | pet audit (cited) | canon-vs-code | gated `279c56be` | owner ruled GATE OFF; `ff.petcombat` default OFF; PetHarvester stays the V1 role |

---

## CATCH-PASS LOG (append one row per RunAll+fleet pass)

| Pass | When | RunAll new/known FAILs | Fleet new tickets | Net NEW findings | Convergence? |
|---|---|---|---|---|---|
| Pilot | 2026-07-08 17:02 | 3 (COV-001/002/003) | 0 (pre-instrument exe) | 3 | no |
| Wave-1 | 2026-07-08 17:33 | 7 FAIL / 14 PASS (21 oracles, ~30s): 3 known real (COV-001/002/003) + 1 NEW real (COV-015 pet-skill-trees) + 1 fail-by-design (COV-013) + 2 harness-artifact (COV-017/018) | — | 1 real (COV-015) + 1 to-triage (COV-016) | no |

| Baseline | 2026-07-08 17:48 | **5 FAIL / 16 PASS — ZERO false-fails** (offline-harvest + econ-meta fixed): F8-39, F8-41, arena, **dual-wallet (COV-021, newly exposed)**, pet-slot (by-design) | — | 1 real (COV-021) | trustworthy baseline reached |
| Pet-lane | 2026-07-08 ~18:0x | pet-skill-tree oracle assertion RETIRED (content deleted); pets-combat gated off | (rebuild+fleet in flight) | 0 | — |

### Wave-1 catch-pass detail (2026-07-08 17:33)
- **14 GREEN** = those invariants locked + regression-guarded: core-datahub (48 files + dual-copy), core-catalog, core-world, **core-save (version triple 28/28/28)**, hero-prog, aegis, build-upgrade, arena-cat, companion-roster, townsfolk, **atb-engine (15/15 map, determinism)**, scene-route, art-resource, **enemy-rig-color (10/10 rigged+colored)**.
- **2 map hypotheses REFUTED by real-path oracles** (adversarial-verify value): COV-010 HeroPortraits (art-resource GREEN) + COV-011 Aegis (aegis GREEN). The static map located candidates; the oracles cleared them.
- **2 harness false-fails to fix** (not game bugs): COV-017/018 need a throwaway GameState.
</content>
