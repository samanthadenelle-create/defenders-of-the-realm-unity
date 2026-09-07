# WO-1529: verify the Field Cleric's 205 gold price - intentional, or a typo

**Status:** SPEC - needs an owner decision after the evidence is gathered
**Silo:** canonical `troops.json` (and its twin).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1529 -> 1530 in the same edit). From her review of
`docs/RAID_BALANCE_AUDIT_2026-09-06.md`.

## 1. EVIDENCE

Owner, verbatim:

> "Field Cleric costing 205 gold looks suspiciously low compared with... Spearman at 850, Shieldguard at 1,150
> and Outrider at 1,500... verify whether 205 is intentional or a typo"

The ladder as authored:

```
Field Cleric      205
Spearman          850
Shieldguard     1,150
Outrider        1,500
```

205 is not merely the cheapest - it is a quarter of the next rung, on a HEALER, which is the unit type whose
value rises with army size. If it is a typo the likely intent is 1,050 or 2,050; but that is a guess and this
ticket does not act on guesses.

## 2. FIX SHAPE

- `git blame` the row and read the authoring notes. Report what the history says: deliberate, or a slip.
- Put the finding to the owner with the ladder above. **She decides.**
- If it IS a typo: one number, changed in BOTH twins, edited from HEAD bytes with the LF count proven
  (memory `canonical-json-edits-binary-only-verify-newlines`).

## 3. WHAT NOT TO DO
- Do not "fix" the price on the strength of it looking wrong. A cheap healer may be a deliberate on-ramp for
  the Camp II tank/healer step (WO-1528).
- Do not rewrite `troops.json` in text mode.

## 3B. FINDING — 2026-09-06 (read-only history audit, CLI seat)

**Verdict: NOT a typo, and NOT deliberate either — 205 is a ROW MISSED BY A UNIFORM x5 RESCALE.**
Evidence, all read at source this session (`git log -L 72,92:Assets/Resources/Data/Canonical/troops.json`,
`git show`):

1. **The row was authored 2026-08-21 12:20 by `8f46c9647` "Balance economy and deepen raid combat"** —
   Field Cleric added as a NEW row at `costWood 90 / costIron 70 / costFood 45`. At that moment the whole
   roster was on the same small scale (Spearman `100/50/20`, Shieldguard `120/80/30`, Outrider `160/100/40`),
   so the Cleric was in fact **dearer than the Spearman** — 205 vs 170 basket-sum. **On the day it was
   written the number was sane.**
2. **The same day, 22:49, `4a3423108` "feat(economy): convex Finish-Now pricing + rescale parity (WO-1129)"
   multiplied EIGHT troop rows by exactly x5** (80/20/10→400/100/50; 100/50/20→500/250/100;
   120/80/30→600/400/150; 160/100/40→800/500/200; 320/280/80→1600/1400/400; 60/40/10→300/200/50;
   80/160/50→400/800/250; 200/200/80→1000/1000/400). **The Field Cleric — nine rows in the file, eight in
   the diff — is the ONLY row it did not touch.** It was ten hours old and evidently not in the author's
   working set.
3. **`a11899d58` (2026-08-25) "feat(economy): complete food to stone conversion"** then collapsed each
   basket to a single `costGold` as an exact SUM: 400+100+50=550, 500+250+100=850, 600+400+150=1150,
   800+500+200=1500 … and **90+70+45=205**. The conversion is faithful; it merely **inherited and froze the
   missed rescale**, which is what turned an invisible authoring gap into the visible 205-vs-850 cliff.

**No commit message or authoring note explains 205.** `8f46c9647` and `a11899d58` carry one-line subjects
with empty bodies. `4a3423108`'s body is long and explicitly hunts rescale parity — it records fixing "21
fallback-cost fields in CatalogBootstrap" and "one missed baseline row in the cost-basket oracle
(tower_siege_tower)" — but **names no troop row**; the Cleric slipped through the very sweep that would have
caught it. `troops.json`'s `_comment` says *"Stats/costs are authoritative from WORK_ORDER_PROGRAM_732_736
economy table (do not invent)"*, and **that table has no Field Cleric row at all** (grep: it lists spearman
50/25/10 and shieldguard 60/40/15 only) — the Cleric postdates the table, so canon never priced it.

**Neighbours at each moment** (basket sum → gold):

| | at authoring (08-21 12:20) | after x5 (08-21 22:49) | today |
|---|---|---|---|
| Spearman | 170 | 850 | **850** |
| Field Cleric | **205** | **205 (SKIPPED)** | **205** |
| Shieldguard | 230 | 1150 | **1150** |
| Outrider | 300 | 1500 | **1500** |

**The arithmetically-derived intent is `1025` gold** (90/70/45 x5 = 450/350/225), which restores the
Cleric to its original rung — between Spearman 850 and Shieldguard 1150, exactly where it sat on 08-21.
Note this is NOT the WO's speculative 1,050 or 2,050; those were guesses, 1025 is the rescale factor
applied to the actual authored bytes. **It is still the owner's call** — a 1025 Cleric is a deliberate
choice about the Camp II tank/healer step, not a mechanical correction, and this ticket does not act on it.

Both twins verified byte-identical today (`diff` → IDENTICAL); `costGold: 205` at line 85 in each.

## 4. ACCEPTANCE
- [ ] `git blame` output and any authoring note quoted in the RESULT.
- [ ] The owner's decision recorded in this file.
- [ ] If changed: both twins updated, LF counts proven, and the canonical-JSON oracles green.
- [ ] `REGRESSION_OK n/n` on a fresh log.
