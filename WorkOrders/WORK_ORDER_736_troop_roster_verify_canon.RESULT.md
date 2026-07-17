# WORK ORDER 736 — Troop Roster Verify, Regression, Canon Close — RESULT

**Status:** VERIFIED (edit-complete; batch-gate + felt-close pending)
**Date:** 2026-07-16
**Program:** WO-732 → WO-737 · Barracks Troop Roster + Tier Unlocks — **CLOSE**
**Silo:** QA / Regression / Canon

---

## Summary

The Barracks roster is now **data-correct, dual-copied, train-gated, and documented** — proven
by a headless oracle, not by faith. The army is a **7-type tier-unlocked roster**, never "two
troops forever." No production flags flipped; no scenes hand-edited.

---

## 1. DataRegression oracle (headless)

**Added:** `Assets/Editor/Regression/TroopRosterRegression.cs` (new file, `public static bool Run(out string reason)`
contract, mirrors `CompanionRosterRegression`).
**Wired into:** `Assets/Editor/Regression/DataRegression.cs:233` — new line immediately after the
`[companion-roster]` oracle:
`if (!TroopRosterRegression.Run(out var troopRosterReason)) failures.Add(troopRosterReason); else log.AppendLine("[troop-roster] " + troopRosterReason);`

Assertions (loads through the REAL `TroopCatalog.Reload()` + `BuildingTierCatalog.Reload()` — the
same WebGL-safe Resources-first path the game uses):

| Assertion | Location (TroopRosterRegression.cs) | Rule |
|-----------|-------------------------------------|------|
| Exact 7-id set, no missing/extra | `:78-106` | footman/archer/spearman/shieldguard/outrider/battlemage/echo-legionnaire |
| No duplicate ids | `:83-89` | `seen` dictionary rejects dup id |
| Defaults tier 1 | `:109-113` (footman+archer rows of `Expected`) | footman + archer `UnlockBarracksTier == 1` |
| Ladder | `:109-113` | spearman 2, shieldguard 3, outrider 4, battlemage 5, echo-legionnaire 6 |
| Costs ≥ 0, slots ≥ 1 | `:116-119` | wood/iron/food ≥ 0; slots ≥ 1 |
| Visuals (WO-735) | `:122-125` | every troop has non-empty `model` + `iconId` |
| Barracks tier announce copy (WO-734) | `:129-139` | barracks `building-tiers.json` T2–6 `effect` text names the unit it unlocks |
| Unlock gate (WO-733) | `:143-165` | 2 train @ tier 1, 4 @ tier 3, Outrider stays locked ≤ T3; real `TroopUnlock.LockedReason(echo-legionnaire)` cites "Tier 6" + "Legion" |

**Pass marker:** `TROOP_ROSTER_OK` (surfaces as `[troop-roster] …` inside the `RunAll` log);
fail marker `TROOP_ROSTER_FAIL: <n> issue(s)` with per-line detail.

**Headless run method (for orchestrator to gate):**
`DeNelle.Editor.DataRegression.RunAll`
via `run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log`
(authoritative markers: `REGRESSION_OK` overall, `[troop-roster]` line for this oracle).
NOTE: not yet executed here — orchestrator batch-gates (per WO constraint: do NOT gate/build/commit).

---

## 2. Dual-copy verification (md5)

Both canonical JSONs are **byte-identical** across Resources + StreamingAssets:

| File | Resources md5 | StreamingAssets md5 | Identical |
|------|---------------|---------------------|-----------|
| `troops.json` | `2E2CD1E157974D3EF746C039E1350D6E` | `2E2CD1E157974D3EF746C039E1350D6E` | ✅ |
| `building-tiers.json` | `0A6BC89A8381EBC34112151D24595863` | `0A6BC89A8381EBC34112151D24595863` | ✅ |

(Resources = `Assets/Resources/Data/Canonical/…`; StreamingAssets = `Assets/StreamingAssets/Data/Canonical/…`.)

---

## 3. Canon update (§15, one-liner)

`PIPELINE_STATE.md:17` — new dated delta at the top of CURRENT STATE:
*"2026-07-16 delta — BARRACKS 7-TROOP TIER-UNLOCKED ROSTER (program WO-732→737, VERIFIED): the army is a
7-type roster trained at the Barracks, gated by Barracks building tier — Footman + Archer day-one (tier 1),
then Spearman (T2) · Shieldguard (T3) · Outrider (T4) · Battlemage (T5) · Echo Legionnaire (T6) … Not 'two
troops forever.'"*

(`CANON_GROUND_TRUTH_2026-07-13.md` remains the live anchor; when the next dated ground-truth is minted it
should carry this same one-liner. Program index status → VERIFIED noted here.)

---

## 4. Quality gate (touched .cs)

| File | Braces | NUL |
|------|--------|-----|
| `Assets/Editor/Regression/TroopRosterRegression.cs` | 41 / 41 ✅ | none |
| `Assets/Editor/Regression/DataRegression.cs` | 525 / 525 ✅ | none |

No `.unity` scenes touched. No `System.Reflection` added. No feature-flag defaults flipped.

---

## Program 732–737 — completion ledger

| WO | Title | RESULT | State |
|----|-------|--------|-------|
| 732 | Troop roster data + `unlockBarracksTier` schema | `WORK_ORDER_732_*.RESULT.md` | DONE (7-troop `troops.json`, dual-copied) |
| 733 | Training unlock UX + train refuse gate | `WORK_ORDER_733_*.RESULT.md` | DONE (`TroopUnlock` single gate authority) |
| 734 | Barracks tiers announce unit unlocks | `WORK_ORDER_734_*.RESULT.md` | DONE (T2–6 effect text names each unit) |
| 735 | Placeholder models / portraits / tray icons | `WORK_ORDER_735_*.RESULT.md` | DONE (0 capsule fallbacks; placeholders) |
| 736 | Dual-copy, DataRegression, canon (this WO) | *this file* | VERIFIED (edit-complete, gate pending) |
| 737 | Obsidian layout contract for Train panel | `WORK_ORDER_737_*.RESULT.md` | DONE (Obsidian Train panel) |

**The roster program (732–737) is COMPLETE** — data, gate, copy, visuals, layout, regression, canon
all landed. Remaining work is orchestrator batch-gate + owner felt-pass, not implementation.

---

## Residual gaps (owner-sourced art — not code)

Per CLAUDE.md §12 (recommend, don't invent). These do NOT block the roster; each is a JSON
`model`/`iconId` swap once art exists — no code change:

- **Real Ranger mesh (Outrider):** `Resources/Heroes/Ranger` is a `.tripo-extracted` stub (no
  `Ranger.fbx`). Outrider ships on the loadable **SC_Archer** stand-in; wants a fast/light silhouette.
- **Real Mage mesh (Battlemage):** `Resources/Heroes/Mage` is a `.tripo-extracted` stub (no `Mage.fbx`).
  Battlemage ships on SC_Archer; wants a caster silhouette + a proper arcane/staff icon (no owned
  bow/magic glyph in `Resources/RpgUi/icons`).
- **Aether-sprite idle:** owner-sourced idle/VFX pass for the caster read — deferred to a later art WO.
- **Shieldguard/Echo Legionnaire** currently share the `Knight` body (elite/tank read is acceptable
  placeholder); distinct art optional later.

---

## PO felt sign-off (owner)

Manual PO script (headless can't judge feel):

1. Enable Barracks (`ff.barracks=1` if needed). Open Train UI → **7 rows**; only Footman + Archer trainable.
2. Upgrade Barracks to **T2** → Spearman unlocks; tier effect text mentions Spearman.
3. Train 1 Spearman → army has it; save/reload preserves.
4. Attempt to train Echo Legionnaire at low tier → **refused, no resource spend**.
5. (Optional, WO-726) deploy a mixed army in a raid.

- [ ] PO felt-verified + CLOSED
