# WO-1518 RESULT - research rows now name WHAT they are short and WHY they are locked

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 8 (RESEARCH tree) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06, uncommitted, awaiting the Unity gate.)*
**Lane:** edit-only. Files: `ManageScreenVM.cs`, `ManageRowBenefitRegression.cs`. Renderer unchanged.

## ACCEPTANCE LINE 6 - WHICH ROWS CARRIED A DOOR BEFORE THE CHANGE
`ManageScreenVM.ComposeResearchItem`'s `if (c.Locked)` arm composed a `PrerequisiteBlocked` action
carrying `Route = ManageRoute.ToBuildCard(c.BuildingId, "VIEW BUILDING")` on **every** locked perk, and
`ManageVmProjection.ProjectAction` turns any blocked action with a routable route into a live, ENABLED
door. So **100% of locked research rows already had a working door** - the routing was never the
defect. What no face carried was the WORDS. (The `ResearchChoiceVM.Activate` seam the ticket cites at
`:444` pre-change is the OTHER, legacy path - `LockReason` still declared at `:453`. Untouched.)

## WHAT LANDED (verified at source this session). Line numbers are `ManageScreenVM.cs`.

1. **SHORT names what.** New `ShortBadgeText(IReadOnlyList<CostPart>)` (`:4878`) computes
   `p.Amount - BankOf(p.ConceptId)` over the item's OWN cost basket - the same parts the cost row
   paints and the same bank reader `CostVms` uses - emitting `SHORT 120 IRON` and comma-joining
   multiple shortfalls (`:4889`). Wired into `ComposeResearchItem` (`:4548`) and `ApplyBuildBadge`
   (`:4202`). **No second affordability predicate.** When nothing measures short it keeps the bare word
   and says so via `FlowTrace.Step` (`:4893`) rather than inventing an amount. It stays WORD + NUMBERS,
   not a sentence: the state column is ~a quarter of the row wide with an 18px `FitSingleLine` floor,
   and a sentence there is culled blank (`:4463`).
2. **LOCKED names why, and says what the tap does.** `BadgeText` becomes `"LOCKED - TAP"` (`:4476`) -
   and only when a door can actually be routed; without one it stays `"LOCKED"`, with a `FlowTrace`
   line at `:4485` recording that no door existed. The blocker sentence is JOINED onto `NextRungLine`,
   which the renderer already paints as the row's wide SECOND line, so the row reads
   `name / "Troop damage +8% . Requires Barracks Tier 3" / [padlock] LOCKED - TAP` (`:4468`). The
   `- TAP` half is DERIVED from door routability, never assumed.
3. **The green arrow leaves a SHORT row.** `ProjectAffordanceTile` (`:3967`, from WO-1516) withholds
   the status medallion when a tile's state is the `Available` catch-all and its action is refused;
   applied to research perk tiles at `:4421`.
4. **The `Army is full.` footer.** It was never a footer: it is `ManageScreenVM.Notice`, the single
   band `ManageScreenPanel.BuildNotice` seats beside CLOSE, still holding what `BarracksService` handed
   back on a refused TRAIN tap. Nothing cleared it, so it rode the back stack onto the Armorer research
   screen. New `ClearStaleNotice(destination)` (`:3278`) is called from `EnterTab` (`:3219`) and `GoTo`
   (`:3289`) - a refusal belongs to the screen whose verb was refused. **Fixed in the NAVIGATOR, not
   the band**: suppressing it in the View would need the View to decide which sentences belong on which
   screen, the exact MVVM line the conformance oracle enforces.
5. **Section 3 respected:** no SHORT or LOCKED row is hidden - the deliberate contrast with WO-1516.

## MEASURED CASES
- `ManageRowBenefitRegression.CheckResearchRowsSayWhatAndWhy`
  (`Assets/Editor/Regression/ManageRowBenefitRegression.cs:306`, called `:273`) **empties the fixture's
  purse** - the existing fixture is deliberately rich and could never produce a SHORT row - then walks
  every school's composed perk tiles and asserts: a `SHORT` word carries a digit; a `SHORT` row carries
  no status medallion; a LOCKED row's state word carries `TAP`; a LOCKED row's second line contains
  `ResearchChoiceVM.LockReason` verbatim. FAILS rather than skips if neither row type appeared.
- `ManageTroopsTrainDoorRegression` case 9 (`:440`, shared with WO-1517) proves the `Army is full.`
  sentence does not survive navigation to another Manage screen - exactly
  `Logs/device/screens/owner-screen-20260906-201242.png`.

## GATE HYGIENE / REGISTRATION
`ManageScreenVM.cs` braces 418/418 NUL 0. `ManageRowBenefitRegression.cs` braces 62/62 NUL 0.
No `.cs` written through a shell redirect. No `DataRegression.cs` edit needed -
`ManageRowBenefitRegression` is ALREADY registered in HEAD (`DataRegression.cs:1645` in HEAD; `:1657`
in the working tree after other lanes' edits), inside a `Guard.Try` as `[manage-row-benefit]`.

## OWED
- `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on a fresh log.
- **Acceptance line 4 is NOT covered:** the `PanelDoorRegression`-class check that each locked row's
  door opens a REGISTERED `PanelId` with the return door set. This lane proved the door EXISTS and is
  routable; it did NOT prove its destination is registered.
- Headless research PNGs, opened (line 7). Owner device felt-verify + close.
