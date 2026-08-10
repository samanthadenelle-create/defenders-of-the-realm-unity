# WORK ORDER 960 — Armor store: show the ladder — locked items greyed with their level, next-5-levels window

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 960 → 961 in the same edit)
**Silo:** Village shop/gear UI + gear unlock derivation
**Origin:** owner RULING 2026-08-10, verbatim: *"we need more armor, only 3 options in store, if the
issue is they are locked to a tier — could we display as greyed out with lvl and only show ones in
the next 5 levels."*

## 1. Verified state (2026-08-10)

`Assets/Resources/Data/Canonical/armor.json` (the curated Resources side WINS — owner gear ruling)
holds **24 rows**: armor_cloth/leather/chain/plate · aegis_plate · full per-class rarity ladders
(`armor_<class>_common|uncommon|rare|epic|legendary` for knight/ranger/+). **The store surfaces 3.**
The defect is store VISIBILITY/FILTERING, not missing content. Surveyed keys carry no obvious
level/tier field — the gating derivation must be found at source.

## 2. RCA first (§12)

Find the armorer/store stock pipeline (the shop the Armorer vendor opens — DialogueCommandBridge
OpenShop → the gear shop VM) and cite WHY only 3 rows surface: class filter? a hardcoded stock list?
ownership/price filter? rarity gate? Then find how gear unlock LEVEL is (or is not) derived — if no
level gating exists, derive the display level from the existing progression axis (hero level vs
rarity mapping) as DATA, not code constants, and propose the mapping in the RESULT for her tune.

## 3. The ruling to implement

1. The store lists the class-appropriate ladder, not just the unlocked slice.
2. **Locked items render GREYED with their unlock level in words/numbers** (`Lv 7` on the card —
   word+shape, never grey-tint alone; owner is colourblind, grey+text is the pair).
3. **Window: only lockeds within the NEXT 5 LEVELS of the hero's current level show**; deeper
   future items stay hidden (aspiration without wall-of-lockeds).
4. Locked cards are non-purchasable, tap explains ("Unlocks at Lv 7"). No FREE/sale chrome.
5. Regression: with a level-N fixture, the visible set == unlocked ∪ locked-within-(N,N+5]; a
   locked card can never be purchased.

## 4. Content note (hers, later)

If after the visibility fix she still wants MORE armor rows, that is a content/art pass on the
curated Resources catalog (the small-set ruling was art-driven: "nothing decent to use yet" —
re-evaluate against the KayKit/AccuRig art now in tree). Propose, don't author.

## 5. What NOT to touch

The dual-copy gear ruling (Resources = truth; StreamingAssets stale side exempt) · aegis setId
(WO-audit item) · weapon shop scope (mirror later if she likes the armor result).
