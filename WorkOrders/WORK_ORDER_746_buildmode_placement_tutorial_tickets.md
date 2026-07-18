# WORK ORDER 746 — Build Mode felt-fix pass: post-place return · singleton palette state · tutorial spotlight anchor

**Status:** READY TO IMPLEMENT (tickets BM-1 / BM-2 / BM-3, owner playtest 2026-07-18)
**Minted:** 2026-07-18 from the banner (recorded with WO-745; next-free bumps to 747)
**Seat:** UI/QA lane — read-only RCA, no code touched. **Implemented by: Claude (CLI).**
**Owner (PO):** Sam — sighting screenshots 2026-07-18 (Build Mode, tutorial "Place your Lumberyard (0/1)"); felt-verify + close.
**Priority:** P1 — all three sit in the FTUE build flow (founding = the demo).
**Lane:** Build Mode / FTUE — one CLI agent, one pass. Files cluster:
`BuildModeController.cs` · `BuildPaletteUI.cs` · `BuildHudController.cs` · `Core/UI/UiSpotlight.cs` ·
`Core/UI/TutorialHighlightRegistry.cs` · tutorial step defs (`Core/Tutorial/TutorialStepModel.cs` / Tutorial V2).
**Classification (per pipeline):** BM-1 = EXISTING behavior, owner-reversed design decision. BM-2 = EXISTING bug
(enforcement landed WO-707; presentation gap). BM-3 = EXISTING bug in the WO-T2 spotlight flow.

---

## BM-1 — After PLACE, the intent bar stays; should return to the shop

**Sighting:** tap PLACE on a valid ghost → building commits, but the Placing chrome
(`[Rotate Left][Rotate Right][PLACE][Cancel]` + "Placing: Echo Hollow" hint) remains.
**Expected (owner 2026-07-18):** a committed placement returns to the Browse state — palette
expanded (second screenshot), intent bar gone.

**Read-only RCA (verified from code):**
- `BuildModeController.Place()` (~:1655) — the commit path's own doc says: *"The entry **stays
  armed** so the player can place several in a row (CoC behaviour)."* Stay-armed is deliberate;
  the owner is reversing that default for the standard flow.
- `BuildHudController` is a 3-state machine (Browse / Placing / Selected, `:41-45`); the intent
  bar is shown by the Placing state (`:234`).
- `BuildPaletteUI.Expand()` (~:340) is documented as *"called from CancelArmed, i.e. every
  return-to-carousel: after a placement OR a cancel"* and already clears the armed glow + re-renders
  live costs — but on the stay-armed success path `CancelArmed()` is never invoked, so the HUD
  never leaves Placing. (The Expand doc's "after a placement" describes intent, not the wired path.)

**Fix sketch (CLI):** at the END of a successful `Place()` commit (after charge + BaseLayout
append + toasts/signals), run the existing return-to-carousel path (`CancelArmed()` →
`Expand()` → `SetState(Browse)`). Do NOT bypass the tutorial signal that the placement step
listens to (TutorialStepModel: the Echo Hollow grant rides placement). If multi-place-in-a-row
matters later (walls), gate stay-armed per catalog row (e.g. `repo.multiPlace`) — default OFF.

**Capture to prove (§12, before coding):** one placement with `[Flow:Build]` — expect the
`Place() — tower spawn` step line with NO subsequent HUD state-transition line; add a
`[Flow:BuildHud] state -> Browse` trace as part of the fix so the RESULT can paste the pair.

**Acceptance:** place any building → intent bar hidden, palette expanded, no armed card, costs
re-rendered live; tutorial placement step still advances; `AssertBuildMoveChain` regression green.

---

## BM-2 — Placed singleton (Echo Hollow) is still an armable option in the palette

**Sighting:** Echo Hollow was just placed, yet its card still shows in the Town tray as a
normal buyable (80W 30I). It is a singleton — it "should not be an option."

**Read-only RCA (verified from code):**
- Singleton ENFORCEMENT exists — WO-707: `BuildModeController.SingletonAlreadyBuilt()` (`:1830`,
  reads `entry.repo.singleton` + a standing BaseLayout record) is checked at arm (`:1817`) and
  re-checked at commit (`:1670-1675`), rejecting with `BuildFeedbackToast` "Already built - your
  town has one" (`BuildFeedbackToast.cs:138`).
- The PALETTE has no such gate: `BuildPaletteUI.Render()`/card build renders every entry of the
  active category with cost chips regardless of built state — the player is offered a card that
  can only fail at arm time. `SingletonAlreadyBuilt` is `private` to `BuildModeController`, so
  the palette cannot query it today.

**Fix sketch (CLI):** hoist the singleton-built check to a shared internal helper (controller or
a small static over CatalogRegistry + BaseLayoutLoader), then in the card build render
singleton-built entries as a NON-armable "Built" state: desaturated art + a "Built" chip
(text + state, never color alone), no cost chips, tap → the existing Singleton toast. (Owner may
prefer the card fully removed — pin at felt-verify; disabled-Built is the recommended default so
the catalog stays discoverable.) Re-render on placement commit (BM-1's Expand re-render covers it).

**Capture to prove:** arm the placed Echo Hollow from the palette → `[Flow:Build]` shows the
arm attempt + Singleton toast line — that pair proves the offer-then-reject gap.

**Acceptance:** after placing Echo Hollow: its card is visibly Built and not armable; same for
any `repo.singleton` row; Lumberyard/Foundry/Silo (deliberately non-singleton, WO-707 note)
unaffected; state survives palette re-open and save reload.

---

## BM-3 — Tutorial glow anchored to the wrong card, and orphaned when the palette collapses

**Sighting:** objective reads "Place your Lumberyard (0/1)" but the round glow sits on the
**Forge** card; after arming (palette collapsed to the intent bar) the same glow floats
mid-screen over "Rotate Right" — anchored to nothing. Expected: the tutorial highlights exactly
the card it is asking the player to build (Lumberyard), only while that card is on screen.

**Read-only RCA (suspects named, capture required):** two glow systems overlap here —
1. The ARMED-card gilt halo: `BuildPaletteUI` `ArmedIconGlow` + `IconGlowPulse` (`:504-510`),
   cleared on `Expand()` (`:344-347` — the "glow must not stay on" fix, owner felt-test 07-17).
   It is parented to the card, so it should hide with the tray on `Collapse()`.
2. The TUTORIAL spotlight: `UiSpotlight` (dim + circular cutout, own always-on canvas ~sort 4300,
   `Core/UI/UiSpotlight.cs`) resolving targets through `TutorialHighlightRegistry` (WO-T2);
   `BuildPaletteUI:113/261` "owns tutorial spotlight registration", `BuildTabRow.cs:101`
   registers tab targets ("highlight town tab to start").
   A free-floating circle that survives palette collapse matches the UiSpotlight cutout holding a
   STALE screen rect of a hidden/wrong target — the palette re-renders its cards (Render()
   destroys/rebuilds), so a spotlight bound to a dead card RectTransform keeps the old screen
   position; and the glow sitting on Forge instead of Lumberyard suggests the step's highlightId
   resolves to the wrong card (id-vs-index mismatch in card registration, or a
   `lumbermill`-vs-`lumberyard` id skew — compare `StrategicPlacementMigration.cs:90` mapping
   `EchoHollow… → itemId "pet-house"` for how display names and item ids diverge).

**Capture to prove (§12 — do FIRST, the two suspects need splitting):** log at spotlight Show:
`highlightId`, the resolved target's name/path, and its screen rect; log card registration ids
during `Render()`. One run of the Lumberyard step tells us wrong-target vs stale-rect (or both).

**Fix sketch (CLI):** register palette cards under stable ids (`build.card.<entryId>`) at every
`Render()` (registry re-arm on late build is already supported — `TutorialHighlightRegistry:45`);
point the step at `build.card.lumberyard`'s ACTUAL entry id (verify against the catalog row —
capture may show `lumbermill`); UiSpotlight must follow its target's liveness — hide while the
target is inactive/destroyed (palette collapsed, card rebuilt) and re-acquire on re-register;
dismiss on the step's completion signal ("the signal advances, not the spotlight" —
UiSpotlight.cs:9). The armed-card halo stays as-is (BM-1 returns to Browse, which clears it).

**Acceptance:** during "Place your Lumberyard": spotlight sits on the Lumberyard card only;
arming (palette collapse) hides it; placement completes → spotlight gone; no floating glow in
any Placing-state screenshot; tab spotlight ("Town") behavior unchanged.

---

## Do NOT touch
- No UXML; code-built uGUI only; ASCII-only TMP; panels stay near-black (WO-562).
- No `.unity` hand-edits; no changes to WO-707 arm/commit enforcement semantics (presentation only).
- Barracks CoC / Room Forge lanes untouched (separate programs in flight).

## Result protocol
`WorkOrders/WORK_ORDER_746.RESULT.md` — paste the §12 capture lines per ticket (BM-1 state pair,
BM-2 offer/reject pair, BM-3 spotlight resolve line), CompileGate green, owner felt-verify closes
each BM ticket; Notion row WO-746 → Done when all three verify.
