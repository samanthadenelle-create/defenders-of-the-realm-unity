# WO-953 RESULT — Harvest drip feedback + gated-faucet honesty

**Status:** IMPLEMENTED — owner felt-verify owed; §3's instrumented-run citation still owed
**Landed:** 2026-08-10 (wave-3 lane; verified, gated and committed by the CLI seat)

## D1 — "+N <resource>" pops, through the SAME pool (owner ruling honoured)

Owner ruling, verbatim: *"we can use the same item that spawns the damage points."* Verified as REUSE,
not a parallel stack:

- `DamageNumberSpawner.cs:196` — `SpawnResourceGain(int, string, Vector3, Color)`, an additive entry
  point leasing from the existing pool via the existing `SpawnLabel` (`:222`). Combat behaviour above
  it is untouched.
- `ResourceGainPopup.cs:27` — the OLD second stack is GONE. It was a `MonoBehaviour` doing a per-call
  `new GameObject` + TextMeshPro + `Destroy(1.6s)`; it is now a `public static class` forwarder with
  the same `Spawn(pos, msg, tint)` signature, so every income caller compiles verbatim (verified: no
  `AddComponent<ResourceGainPopup>` anywhere in the repo).
- Every income path pops: silo dump (`EchoService.SpawnDumpPops`), pet/node extract
  (`MineNode.cs:631`, `HarvestSite.cs:149`), building tick (`ResourceCollector.cs:413`).
- Burst throttle: `DamageNumberSpawner.cs:168` `GainMergeWindowSeconds = 0.6f`, per-resource merge with
  a `_gainKey` cross-lease guard so a recycled body re-leased to combat can never be merged into.
- Word+shape, ASCII: `MineNode.ResourceDisplayLabel` gives the player word ("Crystals", never
  "AetherCrystal"); the tint is a redundant channel only.

## D2 — picker honesty (the gate surfaced in WORDS)

- `EchoCardVM.cs:361` `TryGetFaucetNeed` — READ-ONLY surfacing of the WO-834 phantom-income gate
  through the pure `ResourceBuildingHarvester.MayHarvest`, `Guard.Try`'d with `fallback:false` so a
  failed read can never stamp a false NEEDS on a paying chip.
- Chip: `EchoCardVM.cs:262` appends `" - NEEDS: <Building>"`, with `" (now)"` still LAST (WO-883). The
  chip stays tappable — assignment remains ALLOWED and starts paying when the building lands.
- Status: `EchoCardVM.cs:171` — `"Gathering Iron - Lv N - waiting on a Forge"`.
- **QR-5.7 name inversion handled:** canon-strings is SKIPPED for the `forge` id (that key names the
  ARMORER storefront); iron resolves the collector card's own catalog displayName instead.

## D4 — pet-node demo rates promoted to owner-tunable data, VALUES UNCHANGED

- New `Assets/_Modules/Village/World/HarvestTuning.cs` — lazy `CanonicalJson` read, `Guard.Try`'d,
  FlowTrace on load AND on the missing-file fallback (never a silent default), `Reload()` for the oracle.
- Defaults ARE the old hardcodes: 5 / 6 s / 5 (`HarvestTuning.cs:43-45`).
- `PetHarvestBootstrap.cs:175,176,193` read them.

### Dual-copy law — verified byte-identical

`Assets/Resources/Data/Canonical/harvest-tuning.json` and
`Assets/StreamingAssets/Data/Canonical/harvest-tuning.json` are byte-identical (md5
`9039bc2720ec60095064d639e8483c6f`), and `HarvestDripRegression.CheckDualCopy` re-asserts it byte-by-byte
in-gate.

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[harvest-drip]` green)

## Oracle

`HarvestDripRegression`, four groups: tuning defaults unchanged; dual-copy byte-identical;
"+N Name" parse accept/reject (multi-word labels, zero, no separator, no plus); merge window positive
and under the 1.6 s label lifetime. Registered in `DataRegression.cs` in the same commit — an oracle
written and never registered is a FAIL by design.

## Still owed

- **§3's RCA gate:** the ONE instrumented run pinning her exact iron path (Echo silo vs pet node vs
  building tick) has not been performed, so no proving line is cited here. No rate was retuned, so the
  gate was not violated — but any future tuning pass must capture that line first.
- `EchoService.SpawnDumpPops` finds the hero by tag per dump and skips the pop (traced at `Once` level)
  if absent. Grants are never affected; only the visual.

## Owner felt-verify

1. Pet/Echo assigned to IRON with no Forge built → chip reads `Iron - NEEDS: Forge` (NOT "Armorer"),
   status reads `waiting on a Forge`, and the assignment still takes.
2. Build the Forge → the cue CLEARS and iron starts arriving.
3. A 3+ resource silo dump → pops stack as a readable column, not a pile.
4. A fast tick burst → merges into one running total instead of wallpapering.
