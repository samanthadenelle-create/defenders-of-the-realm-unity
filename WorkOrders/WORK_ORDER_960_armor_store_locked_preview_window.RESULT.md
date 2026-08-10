# WO-960 RESULT — the armor store shows the ladder

**Status:** IMPLEMENTED — owner shelf-depth pin open
**Landed:** 2026-08-10 (wave-3 lane; verified, gated and committed by the CLI seat)

## RCA (§12) — why only 3 rows surfaced

Not missing content. `armor.json` (Resources, the curated side that wins at runtime) holds 24 rows. The
armorer's `vendors.json` contract carried the WO-860 thinning knobs: `onlyEquippable: true` (HIDE
anything not equippable right now) + `perLevelCap: 2`. For a Lv1 knight that leaves exactly the req<=1
band, capped — three cards. The gate was correct; its CONSEQUENCE (hide, rather than lock) was the
defect the owner felt.

Gear unlock level already exists as data: `armor.json` `req.level` (bands at 1 / 3 / 6 / 10). No new
derivation was invented.

## What changed — a fifth DATA knob, inert at its default

- `vendors.json` (BOTH copies, byte-identical): `armorer.lockedPreviewLevels: 5`, schema doc added,
  `version` 1 → 2. Forge / market / jeweler untouched — the weapon shop is deliberately out of scope.
- `VendorRegistry.cs:76` parses it.
- `VendorStockResolver.cs:572` `InPreviewWindow` — `req.level` in `(shopperLevel, shopperLevel + N]`.
  Applied to the weapon and armor bands identically (`:294`, `:326`): under `onlyEquippable` it
  RE-ADMITS the near-future ladder slice as LOCKED rows; beyond the window a level-locked row hides on
  EVERY shelf mode (aspiration, not a wall of lockeds). 0/absent = pre-960 behaviour exactly.
- `PartyShopVM.cs:934` `LockedTapLine` — a tap on a level-locked card reads "<name> unlocks at Lv N."
  `LockReason` stays `"Requires Lv N"` because the card's `Lv N` chip and the disabled buy-button label
  both key on that prefix. A class lock ("Class: Ranger" — a hard never, not a later) keeps its wording.
- Word+shape preserved: the card carries its `Lv N` text alongside the grey tint. Grey alone is never
  the signal.

## Effect

Lv1 knight, armorer: the req-3 and req-6 bands appear greyed with their level, 2 per level, above the
unlocked band. Deeper rows (req 10) stay hidden until Lv 5.

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[armor-store-window]` green)

## Oracle — what it proves

`ArmorStoreLockedWindowRegression` (`ARMOR_WINDOW_OK`): the knob is DATA present in both `vendors.json`
copies (drift-checked) and the loader parses it to the same value; for a knight at Lv 1/3/4/6/8/10 the
armorer's real `Resolve` output EQUALS an independent oracle built straight from `armor.json` (visible ==
unlocked ∪ locked-within-(N, N+5], class-appropriate, non-excluded, defense DESC, id ordinal ASC, capped
per level), every locked ware reads "Requires Lv <req>", nothing deeper than N+5 appears, nothing above
the shopper's level ships eligible; and through the REAL `PartyShopVM` a locked card is Locked and not
Affordable, tapping it spends NOTHING and equips NOTHING, and the status explains the unlock in words.

## Honest limits

It cannot prove the card LOOKS right (the view is untouched and unrendered here), nor whether 5 levels ×
2 rows is the right density on a phone.

## Owner pins

1. Is 5 levels / 2 rows per level the right shelf depth? Both are data, one edit each.
2. Mirror the window onto the forge (weapons)? Deliberately NOT done, per the WO.
3. WO §4's content question is untouched: if the shelf still feels thin after this, that is an
   art/content pass on the curated catalog — proposed, not authored.
