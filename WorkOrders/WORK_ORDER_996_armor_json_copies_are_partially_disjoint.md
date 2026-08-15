# WORK ORDER 996 — `armor.json`'s two canonical copies are PARTIALLY DISJOINT

**Status:** IMPLEMENTED — SA library +15 class ladders; Resources curated; subset regression (2026-08-15).
**Minted:** 2026-08-14 (CLI)
**Silo:** Gear / canonical data
**Found:** four independent times in one day, from four different directions

---

## Measured at source, 2026-08-14

```
Assets/Resources/Data/Canonical/armor.json         version=2   rows=24   md5 0D335C63...
Assets/StreamingAssets/Data/Canonical/armor.json   version=1   rows=30   md5 6D5061BB...

Resources-only ids       : 15
StreamingAssets-only ids : 21
shared                   :  9
```

**Resources-only (15)** — the entire designed class ladder:
```
armor_knight_common  armor_knight_uncommon  armor_knight_rare  armor_knight_epic  armor_knight_legendary
armor_mage_common    armor_mage_uncommon    armor_mage_rare    armor_mage_epic    armor_mage_legendary
armor_ranger_common  armor_ranger_uncommon  armor_ranger_rare  armor_ranger_epic  armor_ranger_legendary
```

**StreamingAssets-only (21)** — all `blink_armor_*` placeholders:
```
blink_armor_basic2..basic10, blink_armor_bear, blink_armor_bird, blink_armor_boar,
blink_armor_demonhunter, blink_armor_dragonhunter, blink_armor_engineer, blink_armor_hydra,
blink_armor_landwarrior, blink_armor_lionguard, blink_armor_minotaur, blink_armor_pantherknight,
blink_armor_savage
```

## ⚠ This is NOT the weapons shape — do not apply the weapons reasoning

`weapons.json` has a **deliberate** asymmetry: Resources (96) is a curated subset of StreamingAssets
(435), produced by `GearCurationExporter`, and **Resources-only ids = 0**. That is a designed projection.

**Armor has no such pipeline and has drifted in BOTH directions.** Neither file is a subset of the
other. The statement *"Resources is a pure subset"* — true for weapons, and now asserted by an oracle —
**is false for armor**, and any check written on that assumption is wrong here.

This already forced a scoping decision: WO-976's assertion (d) (`Resources ids ⊆ StreamingAssets ids`)
was deliberately limited to **weapons only**, because asserting armor would have shipped a check that
was red on arrival. That exemption is logged INFORMATIONAL in-code and should be removed once this
ticket lands.

## The shipping risk

**Resources WINS at runtime** (`CanonicalJson`, Resources-first). So in the editor everything looks
correct — the class ladders are present and the game reads 24 rows.

**Whatever path reads the StreamingAssets fallback gets a roster with NO class armor ladders at all** —
30 rows, none of them `armor_knight_*` / `armor_mage_*` / `armor_ranger_*`. A player on that path has
no class armor progression.

⚠ The **schema versions also differ**: v2 vs v1. Whatever changed between them applies to only one copy.

⚠ Determine which build targets actually take the fallback path before sizing this. An earlier claim
this session that "Android and WebGL read a different roster" was **not verified at source** and should
be treated as UNPROVEN until someone checks `CanonicalJson`'s platform behaviour.

## Four independent sightings, one day

1. **The 300–599 backlog sweep** — flagged `armor.json` Resources v2/24 vs StreamingAssets v1/30 as a
   live dual-copy violation, incidental to WO-544.
2. **WO-544's verification** — `armor_knight` appears 10× in Resources and 0× in StreamingAssets.
3. **WO-976** — scoped its subset assertion to weapons because armor would have failed immediately.
4. **WO-500 step 1's new subset oracle** — fired on armor: *"15 curated armor ids are absent from the
   library"*, deliberately as a `Warn` not a `Fail`, and the agent pointedly did **not** auto-copy rows
   across to make it green.

Four different tools, four different directions, same defect. It has been reported and never owned.

## ⛔ Do NOT fix it by copying rows across

That is the trap, and it is why this is a ticket rather than a five-minute edit. Copying the 15 into
StreamingAssets and the 21 into Resources would make both files 45 rows and both oracles green — while
silently **adding 21 blink placeholders to the runtime roster** and hiding the fact that nobody knows
which copy is authoritative.

**The actual decision:** which copy is AUTHORITATIVE and which is DERIVED?

- If armor should follow the weapons model — StreamingAssets is the **library**, Resources is the
  **curated runtime set** — then the 15 class ladders belong in the library too (they are real content),
  and a curation step should generate Resources from it. That means armor needs the equivalent of
  `GearCurationExporter`, which does not exist today.
- If armor is meant to be hand-authored in Resources with StreamingAssets as a plain mirror, then the
  21 `blink_armor_*` rows are the strays and the mirror should be regenerated from Resources.

**These two answers produce opposite edits.** Pick one before touching either file.

## Acceptance criteria

- One copy is documented as authoritative, in the file header, with the reason.
- The other is generated from it — or, if hand-maintained, a regression asserts the exact intended
  relationship (identity, or subset in a stated direction).
- Schema versions agree.
- WO-976's weapons-only exemption on assertion (d) is removed and armor is asserted under the same rule.
- The generator/exporter that produces the derived copy carries the same guard the weapons tools got in
  WO-500 step 1: **write, re-read from disk, assert the row count, roll back on mismatch.** The file is
  the oracle, not an in-memory variable.

## What NOT to touch

- ⛔ Do not run `Defenders/Catalog/Generate Gear Catalog` or `Render Gear Icons` as part of this without
  reading their WO-500-step-1 headers first — they now behave correctly for weapons, but armor's roles
  are undefined, which is exactly what this ticket decides.
- ⛔ Do not change `weapons.json`. Its asymmetry is correct and deliberate.
- Related but separate: `WORK_ORDER_544_*` (class-specific armor wire-in) is blocked on the same data
  and should be re-scoped after this lands. WO-500 §4 (armor balance) is also a separate pass — the ~30
  `blink_armor_*` rows are still flat placeholders and `armorer` still excludes `blink_` deliberately.
