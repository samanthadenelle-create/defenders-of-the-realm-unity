# WORK ORDER 1213 - The rewarded-ad claim grants in SILENCE, and its CTA is jammed against the panel edge

**Status:** IMPLEMENTED + gate-green - DEVICE/FELT-VERIFY OWED (not FIXED/DONE)
**Silo:** UI / Monetization presentation
**Origin:** Owner felt-test, 2026-08-26, on Seeker build `2026.08.26.341419`. Owner verbatim:
*"get someone to style the watch ad for double reward and give it proper spacing"* and *"after the
ad is completed instead of cancelling to the home screen toast you received x reward"*.

**File (single, this is one seat's ticket):**
`Assets/_Modules/Village/Monetization/DailyChestController.cs`

---

## Slice A (P0) - the grant is real but SILENT, so it is indistinguishable from a failure

`Claim(int gold, string path)` credits the player and then vanishes the panel:

```
EconomyService.Instance.AddCoins(gold);
service.Save();
FlowTrace.Step("DailyChest", $"claimed +{gold} Gold path={path} day={...}");
Close();
```

The reward LANDS. The player is told nothing - the modal simply closes and they are back on the
home screen. From the player's seat a successful double-reward and a cancelled ad look **identical**,
which is why this was reported as "cancelling to the home screen."

The `FlowTrace.Step` proves it to US and says nothing to THEM. That asymmetry is the defect.

**Required:** on a successful claim, present a toast naming the amount BEFORE/as the panel closes.

- **Reuse the existing seam - do NOT invent a second toast path.** `ElarionUiKit.ShowToast` is the
  sanctioned one; `Assets/_Modules/Core/UI/BankOverflowToastPresenter.cs:249` is the working
  precedent for tone/life/width arguments. A second toast owner is the "one fact written twice"
  bug this repo keeps paying for.
- The toast fires for **both** claim paths, not just the ad path - `Claim` is shared by the plain
  claim and `rewarded_double`. A player who takes the base reward is owed the same acknowledgement.
- **The message names the amount and the currency in WORDS**, ASCII only (device tofu law), and
  never carries meaning by colour alone (owner is red/green colourblind).
- Route the string through `VillageStrings.Canon` beside the existing `chest*` keys - do not inline
  a literal. Add the key next to `KeyClaimDouble` / `KeyAdNoReward`.
- ⛔ **Do not move the grant.** `AddCoins` + `Save` are correct and ordered; this ticket adds an
  acknowledgement, it does not touch the economy path.

## Slice B (P2) - the two CTAs are cramped edge-to-edge

Current rects (`:66-69`):

| | min.x | max.x |
|---|---|---|
| Claim | 0.015 | 0.485 |
| Ad    | 0.515 | 0.985 |

Outer margins are **0.015** per side while the inter-button gutter is **0.030** - so each button is
half as far from the panel wall as it is from its neighbour, and both read as jammed into the
corners. Give the row real, symmetric breathing room.

⛔ **CONSTRAINTS THAT ARE NOT NEGOTIABLE - read `:53-65` before editing:**
- The vertical band **0.025 - 0.280** is load-bearing. It resolves to ~135 ref px at 16:9 and
  ~117 px at 20:9, both of which clear `MinTouchPx` (112) - which makes `ClampMinTouch` a
  deliberate NO-OP here. **Do not shrink the height to buy width.** An inflating clamp is exactly
  what pushed the FrameRaid Deploy row into the shared Close.
- Nothing here may geometrically reach the shared Close band (`DefaultCloseZone` y 0.050-0.125).
  The panel is parented into `layout.body` precisely so the kit reserves that band.
- `ClampMinTouch` **has already been checked and ruled out** at three sites in this codebase.
  Do not name it as a cause; check the band arithmetic first.
- If the row genuinely cannot be spaced within the well, **make the panel taller** - that is the
  recorded precedent in this same file, not shorter buttons.

## Acceptance

1. `COMPILE_GATE_OK` on a fresh log, 0 `error CS`.
2. `REGRESSION_OK <n>/<n> suites` on a fresh log - read the count off the marker.
3. ⭐ **A CAPTURED SCREENSHOT of the panel at 2670x1200** (the Seeker's real surface), opened and
   looked at. Headless gates cannot see spacing. `UI_CAPTURE_OK` proves a panel rendered, never
   that it looks right - this slice is not done on a marker.
4. A falsifiable assertion for Slice A: the oracle asserts the toast COUNT/message after a claim,
   not that `Claim` was called. `ElarionUiKit.ShowToast` is a no-op outside play, which is exactly
   the seam `BankOverflowToastPresenter` exposes (`ToastCount`, `LastToastMessage`) - mirror it.
   ⛔ A test that would still pass with the toast deleted is decoration (see WO-1138).
5. Owner felt-verifies and CLOSES. Not the CLI.

## What NOT to touch

- The economy grant, `DailyChestDayKey`, or the once-per-UTC-day gate.
- `SetAdFace` / the three-state CTA relabel (WO-1051 defect 5) - it is correct.
- `FeatureFlags.RewardedAdSkip` and the preload path.
- Any other panel's geometry. This is one file.


---

## UI SEAT DELIVERABLE (2026-08-26) - SLICE B SPACING SPEC

Symmetric gutters: outer margins and the inter-button gutter all read 0.015. New rects:
- Claim: min.x **0.015**, max.x **0.4925**
- Watch-ad: min.x **0.5075**, max.x **0.985**
Vertical band 0.025-0.280 UNCHANGED (load-bearing; ~135/~117 px clears MinTouchPx 112 at both
aspects, keeping ClampMinTouch a deliberate NO-OP - do not shrink the band, make the panel
taller if anything ever needs room, per this WO's own precedent). Slice A (the claim toast) is
fully specced in the WO body already; no design addition needed.
## LANDED-WORK AUDIT (2026-08-26)

The daily-chest acknowledgement path landed in `b303c4fbf`. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83800` proves both the WO-1213 chest toast and WO-1225 celebration
raise above authored modals, use measured values, and communicate with words/numerals; `:83814` is
`REGRESSION_OK 291/291`. The suite explicitly partial-skips rendered-surface measurement in
batchmode. Still owed: the required 2670x1200 panel/acknowledgement screenshot opened and inspected,
its spacing and measured-value checks, and owner felt-close.
