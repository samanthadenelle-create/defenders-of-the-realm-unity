# WO-1487: 20 of 24 Build tiles have no building portrait - an ART DELIVERY blocker on the Manage loop

**Status:** SPEC - needs OWNER ACTION (art delivery), not a code fix
**Silo:** Art. `Portraits/Buildings/` + `ManagePortraitCoverageRegression`.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1487 -> 1488 in the same edit).

## 1. EVIDENCE

```
reg-final2.log   build tile ids=24 (20 on the dated art exemption list)
Builds/ui-capture/ManageFlow_BUILD_gridtop_2670x1200.png   7 of 10 VISIBLE tiles are blank ovals
```

Confirmed on the owner's own device, `Logs/device/screens/owner-screen-20260906-200741.png` (build 358574,
20:07, Manage / Build, DEFENSE tab): **five of the eight DEFENSE tiles are blank** - Ballista, Sky Ballista
(Anti-Air), Catapult, Wooden Palisade, Healing Caravan. Only Archer Tower, Arcane Spire and Barracks carry art.

The exemption list lives in `ManagePortraitCoverageRegression`. It is dated, so the suite is honest about the
gap rather than passing on nothing - but the gap is 83% of the grid, and the Manage Build screen is the
screen the 2000-block program is being captured against. No amount of layout work makes a grid of blank ovals
look finished.

## 2. WHAT IS NEEDED (owner action)

- One portrait per catalog id, delivered to `Portraits/Buildings/<catalog id>.png`.
- **The exemption list in `ManagePortraitCoverageRegression` IS the checklist** - it names exactly the 20 ids
  outstanding. Work it top to bottom; each delivered portrait comes off the list in the same commit.

## 3. WHAT NOT TO DO
- Do not ship a generated placeholder portrait to clear the ovals. A placeholder that passes the coverage
  suite removes the only signal that art is missing.
- Do not remove ids from the exemption list without the art.

## 4. ACCEPTANCE
- [ ] Portraits delivered; the exemption list shrinks by the same count in the same commit.
- [ ] `ManageFlow_BUILD_gridtop` captured fresh and opened - no blank ovals among delivered ids.
- [ ] `REGRESSION_OK n/n` on a fresh log.
