# WORK ORDER 1334 - The Realm Store needs a PERMANENT face on the action bar

**Status:** READY TO IMPLEMENT
**Silo / Lane:** HUD / action bar + monetization surface
**Type:** EXISTING store, MISSING door
**Minted:** 2026-09-03 (CLI) on a direct owner ruling, with device screenshots as evidence.
**Severity:** P2 - it is the revenue surface, and it is the one surface with no permanent entry.

## The owner's ruling

> *"the realm store is hidden away needs a permanent face on hud"*

## The evidence (device screenshot, Seeker, build 2026.09.03.352921)

The calm-town action bar shows **FIVE faces**: `BUILD · TALK · HERO · JOURNEY · MANAGE`.
**None of them is the store.** The Night Market is reachable only by walking to the building.

⛔ **This is the monetization surface.** Every other verb in the game has a permanent door; the one
that takes money does not. Whatever the retention or conversion argument, a store you have to walk to
is a store most players never open.

## THE SHAPE OF THE FIX - read CLAUDE.md section 7 before touching the bar

The bar's membership is RULED and its ordinals are load-bearing. Do NOT invent an eighth face.

- `HudActionBarModel.ButtonCount` stays **7** (enum identity / array bound).
- `MaxVisibleFaces` is a **MAXIMUM, never a count**. Five faces in open town is the feature working.
- ⭐ **`ActionBarButtonId.Map` is DORMANT at ordinal 4** - feature-flagged OFF (`FeatureFlags.MapTab`)
  because realm travel is a WO-827 stub and the areas do not connect yet. **⛔ NEVER RENUMBER IT: the
  face arrays are indexed by ordinal.**

**The precedent to follow is WO-911 Q10/Q13, and it is exact.** The `Upgrade` face was **RE-POINTED,
NOT ADDED** to the unified Manage screen: it kept its enum value (`= 6`), its widget id
(`upgradeButton`) and its `hud-areas.json` row, which is what dissolved the extra-face problem
entirely. Do the same here rather than growing the bar.

Whether Store re-points the dormant `Map` ordinal or takes another seat is an implementation call -
**state your reasoning in the RESULT** - but the rule is: re-point an existing ordinal, never add an
eighth, never renumber.

## TWO LAYOUT DEFECTS IN THE STORE ITSELF, from the same screenshot

Fix or ticket separately, but do not lose them:

1. **The top-right wallet chip collides with itself.** "Your Wallet", the truncated address,
   "Mainnet", "Your wallet: 3,817 SKR" and "Ready" are all drawn overlapping into an unreadable
   clump. This is the WALLET STATE on the surface that takes money - it must be legible.

   > ### ⭐ OWNER RULING 2026-09-03 - this is a SPEC, not a bug report
   > > *"the white text top right needs moved left and simplified connected they dont need address"*
   >
   > Three instructions, all binding:
   > - **MOVE IT LEFT.** It currently runs to the right edge and collides with the panel frame.
   > - **SIMPLIFY IT.** Collapse the stacked labels into ONE line. Stop drawing "Your Wallet",
   >   the address, the network and the balance as separate overlapping elements.
   > - **⛔ DROP THE ADDRESS ENTIRELY.** *"they dont need address"* - the truncated
   >   `GHKK…sfkC` string is REMOVED, not shrunk, not moved. A player does not verify a base58
   >   address by eye and it is the single biggest contributor to the clutter.
   >
   > **THE FINAL FORM, ruled a moment later - use THIS, it supersedes my suggestion:**
   >
   > > *"or even better SKR: balance"*
   >
   > ```
   > SKR: 3,817
   > ```
   >
   > That is the whole chip. Not `Connected - 3,817 SKR`, which was the CLI's suggestion and which she
   > improved on. **`SKR: <balance>`** and nothing else.
   >
   > ⭐ **Why hers is better, so it does not get "helpfully" expanded again:** a balance that RENDERS
   > AT ALL already proves the wallet is connected - an unconnected wallet has no number to show. So
   > the word "Connected" is redundant with the thing next to it. The disconnected state is then the
   > only case needing its own words (e.g. `SKR: not connected` / a connect prompt) - and that state,
   > not the happy one, is where the words must be explicit, because the owner is red/green
   > colourblind and a greyed-out number is not a message.
   >
   > ⛔ So the CONNECTED state is `SKR: <balance>` - no address, no "Your Wallet", no separate
   > "Ready" pill. The DISCONNECTED state must say so in words.
   >
   > ⛔ **State it in WORDS, never by colour alone** - the owner is red/green colourblind, so the
   > green "Ready" dot must not be the only carrier of connection state. Do not replace the words with
   > an icon-only indicator.
   >
   > ⚠ **Do NOT remove the network indicator without checking first.** Mainnet-vs-Testnet is a
   > MONEY-SAFETY signal: on Devnet/Testnet the tokens are free and a purchase completes for nothing
   > (the matched-pair invariant, `MonetizationActivationRegression`). If it is dropped from the chip,
   > say WHERE a player or the owner can still see it, or keep it as the smallest possible tail on the
   > same line. Do not silently delete a safety signal to reduce clutter.
2. **"CLOSE THE GAP" overlaps the "MONTHLY LEDGER" button**, hiding roughly half its label.
3. Minor: the top price row (`993 SKR ~$19.99` / `2481 SKR ~$49.99`) is clipped by the scroll region.

---

## IMPLEMENTED 2026-09-03 - DEFECT #1 (the wallet chip) ONLY. The HUD face is NOT built.

**Status of this WO stays READY TO IMPLEMENT** - the headline ask (a permanent store face on the
action bar) is untouched. What landed is the owner's chip ruling from the "TWO LAYOUT DEFECTS"
section, plus a measurement (not a fix) for defect #2 and a hand-back on defect #3.

### What the chip renders now

| state | exact string |
|---|---|
| connected, mainnet | `SKR: 3,817` |
| connected, devnet | `SKR: 3,817  DEVNET` |
| no wallet | `Connect a wallet to see SKR` |
| account attached, not authorized | `SKR: wallet bound - authorize` |
| durable identity only | `SKR: identity bound - authorize` |
| reading | `SKR: reading wallet...` |
| unreadable / unprovisioned mint | `SKR: unavailable in this build` |
| Pi skin (unchanged) | `Prices are set in Pi at checkout. Nothing is charged until you confirm in Pi.` |

One line in every state. No address, no "Your Wallet", no "Ready" pill, no colour-only signal.

### Where the network indicator lives now

**On the same line, and ONLY when it is NOT mainnet** - read live from `WalletService.NetworkLabel`.
Silence means mainnet; the WORD `DEVNET` means the tokens are free and a purchase settles for
nothing. The loud case is the dangerous case.

⛔ **This is a net GAIN in money safety, not a trim.** The old carrier was
`Resources/UI/NightMarket/network-frame.png` - authored ART with "Mainnet", a green dot and a
"[READY] Ready" pill **baked into the texture**. It printed "Mainnet" over a **Devnet** session: a
confident lie on the surface that takes real money, carried partly by a hue the owner cannot see.
That plate is removed from the header. The separate `DEVNET - TEST TOKEN` marker above the Buy
control (`BuildSpotlightCta`) is untouched, so the pre-purchase warning is unaffected.
The PNG itself is left on disk; nothing references it.

### Also removed, and it is worth flagging

The `~$12.40` fiat tail and its live Jupiter quote (`RefreshFiatApproximation`). Its only reader was
this chip, and the owner ruled `SKR: <balance>` is the whole chip. **Display only** - no price, SKU,
quote-for-purchase, entitlement or grant was touched. `storeBalanceFiat` is kept in canon so the
sentence survives for a surface with room for it.

### Defect #2 - "CLOSE THE GAP" over "MONTHLY LEDGER": MEASURED, NOT FIXED

⛔ **It is not an overlap.** Both rails are `RectMask2D` scroll columns and their regions are
disjoint (0.64-1.00 and 0.02-0.63). The ACTIONS rail is **shorter than its own content**, so its
last row is cut by its own mask, and what the eye reads under a cut-off row is the next rail's
heading.

⛔ **The column is oversubscribed, so no re-split fixes it.** Body = 978 - 100 - 132 = **746 px**,
of which 0.02..1.00 = **731 px** is available.

```
ACTIONS       = 64 + 2*120 + 2*6 + 16 = 332 px   (rail has 268 -> 64 px SHORT)
CLOSE THE GAP = 64 + 3*120 + 3*6 + 16 = 458 px   (rail has 455 ->  3 px short)
total 790 px into 731 px = 59 px OVERSUBSCRIBED
```

The three catch-up rows are real - `packs.json` carries three `"band": "gap"` rows. A derived
re-split was written, measured, and **reverted**: it only moves the clipping onto the third
catch-up offer, which is the exact regression the `utilityFloor` comment records as already
having been fixed once. The arithmetic is now written into the source at the `LandscapeActions`
region so the next seat starts from the measurement.

**Two candidate fixes, both needing an owner/design call:**
- **(a)** Drop the "ACTIONS" heading - it labels two buttons that already read REDEEM and MONTHLY
  LEDGER. Frees 64+6 = **70 px**, closing the 59 px gap exactly with nothing else moved. Cheapest,
  but it deletes a word the owner did not ask to lose.
- **(b)** One scroll column for both rails, headings inline. The only mask edge becomes the bottom
  of the column; nothing is cut mid-rail. Structurally right; touches `_utilityContent`,
  `_gapUtilityContent`, `_persistentUtilityChildren` and Render()'s clear rule.

### Defect #3 - clipped top price row: NOT TOUCHED

Different region (the shelf scroll column / card row heights), and it cannot be sized honestly
without a fresh capture. Left open.

### Oracle

`NightMarketUiRegression.CheckWalletChip` + `CheckDisconnectedWords`, wired into `Run()`.
Bounds: `[one-line]`, `[no-address]`, `[words]` (checked against **both** shipped
canon-strings.json copies), `[network]`, `[left]`. Proven RED against **eight** mutations
(restore the address; re-stack onto two lines; restore the baked Mainnet/Ready plate; delete the
live network test; push the chip back to x=1.0; carry the disconnected state as `--`; revert
`storeBalanceValue` to `Your wallet: {0} SKR`; let a `{0}` back into `storeBalanceBoundAddress`) -
every one goes red, baseline green.

### Files

- `Assets/_Modules/Wallet/PackStore.cs`
- `Assets/_Modules/Wallet/StoreStrings.cs`
- `Assets/Resources/Data/Canonical/canon-strings.json`
- `Assets/StreamingAssets/Data/Canonical/canon-strings.json`
- `Assets/Editor/Regression/NightMarketUiRegression.cs`

Not gated, not committed - lead gates, commits and distributes.

---

## Constraints

- ⛔ The owner is **red/green colourblind**. The face must be identifiable by ICON SHAPE and its
  WORD, never by colour. Do not ask her to choose a hue.
- Phone-first landscape; touch targets **>= 112px** (`MinTouchPx`).
- ASCII-only in the label. A non-ASCII glyph renders as a tofu box and FAILS the regression.
- UI is code-built uGUI via `ElarionUiKit`. **UXML does not work in builds** - never introduce it.
- ⛔ **Do NOT touch prices, SKUs, entitlements or grants.** Those are server-authoritative
  (`api/_lib/purchase-catalog.js`), `/verify` runs AFTER settlement, and the game takes real money on
  mainnet. This ticket adds a DOOR, nothing behind it.
- The store itself is `PackStore` and already exists end to end - **do not greenfield a second store
  surface.** Canon (WO-1164) rules ONE tabbed Store, "one destination two doorways": the walk-up
  building and the HUD entry open the SAME screen.

## Acceptance

- [ ] A permanent, always-visible store face in calm(town), reachable in one tap.
- [ ] `ButtonCount` still 7; no ordinal renumbered; the dormant `Map` ordinal not broken.
- [ ] The walk-up building and the HUD face open the SAME `PackStore` screen.
- [ ] An oracle pins the face's presence and its ordinal stability. Prove it RED first and report the
      mutation.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] A device screenshot a human opened - headless cannot see a bar.
- [ ] PO felt-verifies and closes.
