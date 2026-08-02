# WORK ORDER 837 — Stockpiles cap resource capacity (not founding freebies)

**Status:** READY TO IMPLEMENT (owner-ruled 2026-08-02, verbal during felt-test)
**Silo:** Economy / BuildMode / Tutorial (single lane; no scene files)
**Depends on:** WO-834 (blank town — shipped). Touches the same FoundingKit/tutorial surface as WO-780/822.

## 1. Owner ruling (verbatim, 2026-08-02)
> "Lumberyard Foundry and Quarry are the stockpiles, we dont start with those in the array.
> Those are needed as the stone caps on capacity"

Plain reading: the stockpile buildings are PROGRESSION buildings — not founding freebies, not
pre-seeded. Their gameplay role is CAPPING resource capacity (CoC-storage model: build/upgrade
stockpiles to raise how much you can hold).

## 2. Code truth (verified 2026-08-02)
- `BuildModeController.FoundingKit` (~:2565-2576) = the FTUE free-once id array — **contains
  `lumberyard` today** (KEY_FACTS: {pet-house, lumberyard, tower_ground_archer}). That is "the array".
- structures-catalog (WO-707 taxonomy, ~:1083): **the three containers are `lumberyard` / `foundry` /
  `silo`** — "hold the stock and are the ONLY enemy-raid targets"; each carries `storageCapacity: 500`
  as **stubbed data with no reader** (was awaiting the WO-672 damage-to-stores loop).
  ⚠ NAMING (owner-confirm): the owner said "Quarry" for the third container; the catalog id is `silo`
  (grain). Keep catalog ids; a rename (silo -> quarry, or display-name only) is an OWNER edit.
- Tutorial `founding_stores` step completes on `build.structure_placed:lumberyard` — authored when
  the lumberyard was a founding freebie. Needs re-spec under this ruling.
- No wallet-cap mechanic exists anywhere (EconomyService grants unbounded; silo caps are Echo-silo
  only). This WO introduces it.

## 3. Build
1. **FoundingKit**: remove `lumberyard` (array + its per-id freebie doc). Founding kit = pet-house +
   tower_ground_archer (unless the owner re-rules).
2. **Capacity model (data-driven)**: per-resource wallet cap = base cap (new tunables, e.g.
   `base wood/iron/food cap` in a balance catalog) + SUM of owned stockpiles' `storageCapacity`
   (lumberyard->Wood, foundry->Iron, silo->Food), tier-scaled if the row carries tiers. One reader
   (EconomyService or a small CapacityService in Core) — One Model: capability on the entry
   (`storageCapacity`), never per-type code.
3. **Enforcement**: grants clamp at cap (overflow LOST with a FlowTrace.Warn + a UI hint), spends
   unchanged. Harvest Dump + wave loot + quest rewards all route through the same clamp (single seam).
4. **UI**: resource chips show `current/cap` when a cap is active (ASCII, text-encoded; no color-only).
   Full chip state gets a "Storage full - build/upgrade a stockpile" hint (teach seam, not a toast spam).
5. **Tutorial**: re-spec `founding_stores` — either it teaches BUYING the first lumberyard (charged,
   affordable from starting income; verify against WO-780 affordability) or it moves later in the flow;
   placement watchdog rules per the 2026-08-02 pacing tune (300s + pause-in-Build).
6. **Raid hook note**: containers stay the only enemy-raid targets (WO-707) — capacity work must not
   break the WO-672 damage-to-stores intent; the stub field becomes live data shared by both.
7. **Oracles**: extend regression — caps parse per container row; clamp math (unit-testable pure
   static); FoundingKit no longer contains a container id; dual-copy parity if new tunables land in a
   catalog. EditMode tests per the ArmyReadinessTests pattern.

## 4. OWNER CONFIRM (defaults; veto any)
1. Third container stays id `silo` (display "Silo") vs rename to Quarry — default: keep silo, flag only.
2. Base caps before any stockpile exists — default: generous enough to never bite before the first
   raid loop (exact numbers = balance pass at implement time, headless-verified).
3. Overflow behavior at cap — default: clamp+lose with warn (CoC model), not bank-and-hold.

## 5. Do NOT
- Do NOT pre-seed any container on a blank town (WO-834 stands). Do NOT touch .unity scenes.
- Do NOT rename catalog ids without the owner's explicit word (save/data references).
- Do NOT couple with WO-830 Echo affinity (separate lane; the Dump clamp seam is the only contact).
