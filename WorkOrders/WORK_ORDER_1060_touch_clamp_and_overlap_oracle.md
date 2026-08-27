**Status:** READY - ⭐ **the owner ruled 2026-08-24 (batch 2, ruling 9): NO WAIVERS** - the allow-list stays at TWO and the four newly-red panels get FIXED. ⚠ The ~21 tap-catcher panels are a LEAD call about the tool, not hers. The oracle landed (`57b2c4595`); the remaining work is fixing real defects. *(Prior line:)* BLOCKED - two owner rulings open. The oracle landed (`57b2c4595`) and works, but its OWN acceptance is unmet: the required four-entry baseline has **two** entries and **43 panels are still red**.  *(Bucket corrected 2026-08-24: the line led with FIXED while its own text said work remained. Prior line preserved below.)*
>  PRIOR: **Status:** FIXED 2026-08-23 (57b2c4595) — rules moved out of the assembly cycle that made them unrunnable; registered as [ui-touch-oracle], 269->270 suites. ⚠ RED ON 43 REAL PANELS — that is the oracle working, and TWO OWNER CALLS are open (see the RESULT): baseline the 4 newly-red panels, and rule on the ~21 full-panel tap-catcher overlaps. AWAITING OWNER RULING.
⚠ Read the 2026-08-23 section: `UI_TOUCH_FAIL` is RED TODAY on 43 real findings across four panels
that were NOT on anyone's list. Those are new tickets, not a reason to grow the allow-list.
*(was: READY TO IMPLEMENT — owner-requested 2026-08-22, "yes do the clamp oracle")*

# WORK ORDER 1060 — The clamp oracle: make layout collisions FAIL THE GATE

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1060 -> 1061 in the SAME edit)
**Assigned:** CLI implements. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** Core / regression tooling
**Class:** ORACLE. Prevents a defect class rather than fixing one instance.
**Cause:** four panels in three days — WO-1051 (Daily Chest), WO-1056 (Armies/Loadouts),
WO-1058 §5 (Manage/Queues), and the equip drawer in the 2026-08-22 screenshots.

---

## 0. One-line truth

**PROD-008 established that no oracle can see orientation, because "looks wrong" is not computable.
Layout is different: the moment a layout is about to break is a discrete, deterministic, observable
event — `ClampMinTouch` growing a control.** We cannot assert that a panel looks good. We can assert
that **nothing had to be rescued**, and that catches the entire class.

---

## 1. Why this class keeps shipping

Every one of the four panels went out compile-green and regression-green. The pattern is identical
each time:

1. A control is authored as a fraction of a small sub-zone.
2. The resolved rect lands **below `MinTouchPx = 112`**.
3. `ClampMinTouch` force-grows it — by 2.5x, 3.9x, **4.5x** in the measured cases.
4. It inflates past its authored band into its neighbours.
5. Nothing anywhere notices. The owner finds it by eye, days later.

**`ClampMinTouch` is a safety net that has become a silent failure mode.** By the time it fires, the
layout it was meant to protect is already destroyed — and it never says so.

---

## 2. The oracle — two asserts, one marker

### Assert A — THE CLAMP MUST NEVER FIRE

Instrument `ElarionUiKit.ClampMinTouch` to record every growth into a static, test-readable list:

```
ClampGrowth { panelName, controlPath, authoredW, authoredH, grownW, grownH }
```

**Any recorded growth on a panel under test is a FAILURE**, reported with the numbers so the fix is
obvious from the log line alone:

```
[touch-oracle] FAIL ArmyMusterPanel/slot-chip-0: authored 197x25 -> grown 197x112 (4.5x on H).
               Author the band above MinTouchPx(112); do not rely on the clamp.
```

⛔ **Do NOT weaken `ClampMinTouch` itself.** It stays exactly as it is — it is the correct runtime
behaviour for a build that ships wrong. The oracle only makes its firing *visible at gate time*.

### Assert B — NO TWO INTERACTIVE RECTS MAY INTERSECT

Assert A cannot catch two correctly-sized controls authored on top of each other — which is exactly
**WO-1058's hazard**, where `Cancel` (0.885-0.98) sits inside where `Upgrade` (0.76-0.98) was, both
comfortably above the floor.

So, after layout, for every pair of interactive rects sharing a canvas: **compute the intersection.
Non-empty = FAIL.**

```
[touch-oracle] FAIL ManageScreenPanel: 'Upgrade'(0.76-0.98) intersects 'Cancel'(0.885-0.98)
               on x by 0.095 of row width.
```

⚠ **Include the shared Close.** Burying it is WO-1051's defect and it must be caught by the same
rule — the Close is an interactive rect like any other.

### The marker

Emit **`UI_TOUCH_OK <n>/<n> panels`** on a clean pass. Judge by the MARKER on a fresh log, never by
the exit code — this repo's runners exit 0 on refusals and FAILs (memory
`gates-report-success-without-proving-it`).

---

## 3. ⚠ The hard part: measuring AFTER the scaler, not during

`ElarionUiKit.cs:1057` records the trap in its own words: **`rect.height` returns RAW SCREEN PIXELS
until the CanvasScaler has applied** — that was F8-5's root cause.

So the oracle must sample **after a layout pass has completed on a canvas configured exactly as the
game's is** (`referenceResolution 1080x1920`, `MatchWidthOrHeight`). A measurement taken during build
will read plausible numbers and prove nothing.

**Test at the landscape aspect the build actually ships** (portrait autorotate is `0`). The whole
defect class exists *because* a portrait reference resolution on a landscape screen makes the vertical
axis far smaller than the fractions suggest.

⚠ **Run at more than one aspect** — at minimum the Seeker's and a 16:9. A band that clears 112 at one
aspect can fall under it at another, and that is a shipping bug the single-aspect version would miss.

---

## 4. Where the panel list comes from

**Do not hand-maintain a list** — a list nobody updates is how the fifth panel ships broken.

`Assets/Editor/UICaptureLaunch.cs` + the headless capture path already enumerate and open panels for
screenshots. **Reuse that enumeration.** A panel that can be captured can be measured, and any panel
added to the capture set is automatically covered by the oracle.

Register the suite with `DataRegression.RunAll` so it runs in the standard gate
(`REGRESSION_OK <n>/<n> suites`).

---

## 5. Rollout — the existing debt must not block the gate

Four panels are known-bad **today**. Turning the oracle on red would block every commit.

1. **Land the oracle reporting-only**, with a **baseline allow-list** of the four known offenders
   (WO-1051, 1056, 1058, and the equip drawer), each entry naming its WO.
2. **Each fix removes its own allow-list entry** in the same commit.
3. **When the list empties, the allow-list mechanism is DELETED** — not left empty. An empty
   suppression list is an invitation to add to it.

⛔ **The allow-list may only ever shrink.** Adding an entry requires an owner ruling, exactly like
re-tagging an impulse SKU `shelfCurated`. Otherwise the oracle becomes a place to record defects
rather than prevent them.

---

## 6. Acceptance

1. `UI_TOUCH_OK <n>/<n> panels` on a fresh log for a clean tree; **marker absent = failure**.
2. **Proven RED before green:** run it against `ArmyMusterPanel` as it stands today and capture the
   FAIL naming the 4.5x growth. An oracle never seen red is not evidence (PROD-008's rule).
3. Assert A catches sub-floor authoring; Assert B catches same-size overlap. **Prove each with its
   own case** — a deliberately shrunk control, and two deliberately overlapping ones.
4. Measurement happens post-scaler at **>= 2 aspect ratios**, landscape.
5. The panel set is **derived** from the capture enumeration, not hand-listed.
6. The baseline allow-list has exactly four entries, each naming its WO.
7. `COMPILE_GATE_OK`; brace-check every `.cs`.

---

## 7. What this does NOT do

- **It does not assert that a panel looks good.** No oracle can (PROD-008). It asserts that nothing
  had to be rescued and that nothing overlaps — a proxy, and the proxy that catches this class.
- **It does not replace screenshots.** `UI_CAPTURE_OK` and opening the PNGs still bind; the oracle
  catches geometry, the eye catches everything else.
- **It does not touch any panel's layout.** Those are WO-1051 / 1056 / 1058 / 1061.

## 8. Files

**Create:** `Assets/Editor/Regression/UiTouchClampRegression.cs` (or the project's suite convention).

**Edit:** `Assets/_Modules/Core/UI/ElarionUiKit.cs` — **record-only** instrumentation inside
`ClampMinTouch`; behaviour unchanged (§2 Assert A). Register the suite in `DataRegression`.

**Read:** `Assets/Editor/UICaptureLaunch.cs` (the enumeration to reuse) · `ElarionUiKit.cs:1057`
(the post-scaler trap) · WO-1051 §3.5, WO-1056 §1, WO-1058 §1 (the measured cases).

---

## PROGRESS — 2026-08-22 (UI-001 implementation seat, edit-only; NOT gated, NOT committed)

**Status stays READY TO IMPLEMENT.** A substantial part landed as a side-effect of UI-001's §R7 step
1 ("the oracle first"), but §6 acceptance is NOT met — read the gap list before closing this.

### The finding that changed the shape of the work

**§8 said "Create `Assets/Editor/Regression/UiTouchClampRegression.cs`". That file was not created,
deliberately: MOST OF THIS ORACLE ALREADY EXISTED** as `UICaptureLaunch.AuditGeometry` (~:3660), and
building a second one would have left two oracles disagreeing about the same canvases.

Measured against §2: rule 4 already IS Assert A (it measures the AUTHORED band against `MinTouchPx`
post-scaler, which catches the defect *before* the clamp would rescue it), rule 2 was Assert B but
narrowed to SIBLINGS, and rule 3 was Assert B's occlusion half. It already runs at three aspects
(§3), already derives its panel set from the capture enumeration (§4/§5), and already measures in
root-canvas reference px. **The oracle did not lack the asserts. It lacked two things:** the store
was not in the enumeration, and cross-parent overlap walked free.

### Landed

- `ElarionUiKit.cs` — `ClampGrowth` + `ClampGrowths` + `ClearClampGrowths`, recorded inside
  `UiKitMinTouchGuard.LateUpdate`. **Clamp behaviour is byte-for-byte unchanged** (§2's ⛔).
- `UICaptureLaunch.cs` — Assert B widened past the sibling test; ancestor/descendant pairs excluded
  as composition. **Cross-parent hits are routed to the NEW marker only**, so the pre-existing
  `UI_GEOMETRY_OK` gate keeps its exact behaviour and this cannot redden unrelated commits.
- `UICaptureLaunch.cs` — `UI_TOUCH_OK <clean>/<checked> panels` / `UI_TOUCH_FAIL`, reported from
  `ReportTouchOracle()` beside the other three distinct markers.
- `UICaptureLaunch.cs` — the §5 baseline allow-list, exactly four entries, each naming its WO,
  with the shrink-only and delete-when-empty rules written at the site.
- `UICaptureLaunch.cs` — `CaptureNightMarketStore`, so the money screen is measured at all three
  aspects. It is deliberately NOT baselined: it must be able to go red.

### NOT done — what still stands between this and §6 acceptance

1. **§6.2, the RED PROOF.** Nothing here has been run. `UI_TOUCH_FAIL` has never been seen red, and
   PROD-008's rule is that an oracle never seen red is not evidence. Run the capture and capture the
   FAIL naming ArmyMusterPanel's 4.5x growth **before** trusting any green.
2. **§6.3, the two synthetic cases** — a deliberately shrunk control and two deliberately overlapping
   ones — are not written.
3. **§4, `DataRegression.RunAll` registration.** The marker rides `RunCaptureHeadless`, not the
   standard `REGRESSION_OK` gate.
4. **The runtime half of Assert A is inert in batchmode.** `LateUpdate` never fires in an edit-mode
   capture, so `ClampGrowths` is expected EMPTY on a headless run; the gate-time assert there is
   rule 4. The recorder earns its keep on a device/play session. **Do not read an empty ring as a
   pass** — that misreading is the exact shape of the blindness this WO exists to end.

---

## RESULT — 2026-08-23 (edit-only implementation seat; NOT gated, NOT committed)

### What closed the three gaps the 08-22 seat left open

**1. The rules moved to ONE home so they could be PROVEN: `Assets/_Modules/Core/UI/LayoutOracle.cs`.**
The 08-22 seat was right that a second oracle would have been worse than none — but the reason
`UI_TOUCH_FAIL` had never been seen red was structural, not lazy: the rules lived inside
`UICaptureLaunch` (assembly `DeNelle.Editor`), and `DeNelle.Editor` **references**
`DeNelle.EditorRegression`, so no suite could ever reach them without an assembly cycle. Rules 2, 3
and 4 are now `LayoutOracle.Audit`, in `DeNelle.Core`, called by **both**:

- `Assets/Editor/UICaptureLaunch.cs:3711` — `AuditGeometry` delegates; the sibling/cross-parent
  routing into `fails` / `crossFails` is byte-for-byte what it was, so `UI_GEOMETRY_OK` is unchanged.
  Rule 1 (text-off-plate) stays in the harness — it is kit-zone specific and not a touch rule.
- `Assets/Editor/Regression/UiTouchClampRegression.cs` — the new suite.

The duplicated helper predicates were **deleted** from `UICaptureLaunch`, not left behind: two copies
of `Overlaps`/`ButtonUsable`/`ClippedOut` are two oracles waiting to disagree about one canvas.

**2. §6.3 — the synthetic cases exist and the suite FAILS if the oracle stays quiet.**
Four cases x two landscape aspects (1920x1080, 2340x1080): an authored 21.6 px band (Assert A),
two stacked SIBLINGS (Assert B), two stacked controls in DIFFERENT parents (Assert B's widening —
pinned separately so a future re-narrowing to siblings reddens THIS suite instead of quietly
restoring the blind spot), and the same controls laid apart (must be silent). The suite asserts the
**wording**, not just the count: a finding that has lost either widget's path or the px numbers is a
failure, because the owner is red/green colourblind and an unnamed collision is unactionable.

**3. §4 — registered.** `DataRegression.RunAll` → `[ui-touch-oracle]`.
**Registered oracle suites: 269 → 270** (`Builds/red3fix-datareg.log` 09:02 vs
`Builds/wo1060-regression.log` 12:37).

### The RED proof — on REAL panels, not only synthetic ones

`UI_CAPTURE_OK 89` / **`UI_TOUCH_FAIL x43 over 89 panels (76 clean)`** (`Builds/wo1060-capture.log`).

**The headline is an Assert A hit that only exists at SOME aspects — exactly the case §3 argued for
and a single-aspect oracle would have shipped:**

```
SUB-TOUCH-FLOOR BAND [RaidDeploy_2340x1080 @2340x1080]
  'ObsidianPanel/PanelContent/Zone_Footer/ObsBtn_BEGIN ASSAULT' resolves 1003.6x103 ref px
  -- shortest side 103 is 9 px UNDER ElarionUiKit.MinTouchPx (112).
SUB-TOUCH-FLOOR BAND [RaidDeploy_2670x1200 @2670x1200]  ... 1017x101.7 ... 10.3 px UNDER ...
```

RaidDeploy's two footer CTAs clear the floor at 1920x1080 and fall under it at both taller-landscape
aspects. Nothing has ever said so.

**And the buried Close (§2's "⚠ Include the shared Close" — WO-1051's defect class, on a live panel):**

```
BUTTONS OVERLAP [RumorBoard_1080x2340 @1080x2340]
  'ObsidianPanel/PanelContent/CloseButton' (x -180..180, y -763.1..-631.1) and
  'ObsidianPanel/PanelContent/DetailPane/DetailCta/ObsBtn_Accept' (x -340.2..13.6, y -757.1..-645.1)
  share 193.6x112 ref px -- two tap targets in one place; only one can win the raycast.
```

Full inventory of the 43: **DialogueOptions 18** (BUTTONS OVERLAP), **RumorBoard 18** (14 BUTTON OVER
TEXT + 4 BUTTONS OVERLAP), **RaidDeploy 4** (SUB-TOUCH-FLOOR), **EndStateWaveClear 3**.

### ⛔ FOUR NEW PANELS ARE RED AND NONE OF THEM IS ON THE ALLOW-LIST. THAT IS THE POINT.

§5 predicted four known-bad panels. The oracle found four **different** ones. Per §5 the list may only
ever shrink and an addition needs an owner ruling, so **these were not baselined** — they need tickets.

**One judgement call is deliberately left to the PO rather than silently coded around:** most of the
DialogueOptions/EndState hits are a full-panel *tap-catcher* (`TapAdvance`, `TapDismiss`) overlapping
the CTAs it sits behind. That layering may well be intentional — the CTA wins the raycast by hierarchy
order. Rule 3 already excludes graphic-less buttons as "hit areas ... cannot collide visually", and
extending that same exclusion to Assert B would drop ~21 of the 43. **It was NOT done here**, because
narrowing a rule to make a gate green is how a gate stops meaning anything, and the call belongs to
the owner. Decide it as a ruling; do not let it become an allow-list entry.

### The GREEN proof

```
[ui-touch-oracle] UI_TOUCH_ORACLE_OK 8/8 cases -- LayoutOracle went RED on an authored
sub-MinTouchPx(112) band and on stacked controls (sibling AND cross-parent), named both widgets and
the overlap in px, and stayed silent on the same controls laid apart -- at 2 landscape aspects.
  [red-A @1920x1080] SUB-TOUCH-FLOOR BAND [SyntheticSubFloor @1920x1080] 'SubFloorHost/slot-chip-0'
      resolves 576x21.6 ref px -- shortest side 21.6 is 90.4 px UNDER ElarionUiKit.MinTouchPx (112).
  [red-B @1920x1080] BUTTONS OVERLAP 'Row/Upgrade' and 'Row/Cancel' share 182.4x648 ref px
  [red-B2 @1920x1080] BUTTONS OVERLAP 'ShelfRowA/card-120-SKR' and 'ShelfRowB/card-overlapping'
      share 691.2x178.2 ref px
  [green @1920x1080] 2 controls, both >= 112 px, disjoint -- oracle silent.
```

`REGRESSION_FAIL` on that run is **2 pre-existing reds that are not this WO's**:
`STRUCTURE_ORIENTATION_FAIL` and `NIGHT_MARKET_UI_FAIL`. 268/270 green.

### What the ticket got wrong

- **§8 "Create `Assets/Editor/Regression/UiTouchClampRegression.cs`"** — correct file, wrong reason.
  The suite could not have called the rules where they lived; the assembly cycle had to be resolved
  first. That is why this took a Core move rather than a new file.
- **§6.6 "the baseline allow-list has exactly four entries"** — it has **two** (`ArmyMuster`,
  `EquipDrawer`). `ManageScreen` was deleted by WO-1058 per the shrink-only rule; a Daily Chest entry
  was never added. The list is honest as it stands — the count in §6.6 is what is stale.
- **§2's ClampGrowth ring stays inert headless**, as the 08-22 seat warned. It is not asserted by the
  new suite and an empty ring is still NOT a pass. Assert A (the authored-band measurement) is what
  carries the gate, and it is the assert that just caught RaidDeploy.

### Files
- **New** `Assets/_Modules/Core/UI/LayoutOracle.cs` (braces 26/26)
- **New** `Assets/Editor/Regression/UiTouchClampRegression.cs` (braces 33/33)
- **Edit** `Assets/Editor/UICaptureLaunch.cs` (braces 491/491) — rules 2/3/4 delegated, dupes deleted
- **Edit** `Assets/Editor/Regression/DataRegression.cs` (braces 928/928) — registration
- **Edit** `Assets/Editor/Regression/DeNelle.EditorRegression.asmdef` — `UnityEngine.UI` +
  `Unity.TextMeshPro` made explicit (the suite builds real uGUI controls)

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 9: **NO WAIVERS. The allow-list stays at TWO.**

⭐ **Owner, verbatim:**
> *"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."*

- ⛔ **The four newly-red panels are NOT added to the allow-list.** They get **fixed**. The list may
  only ever **shrink**; adding to it takes her word, and her word is no.
- These are real defects — including the buried Close button on the live Rumor Board sharing
  194x112 px with an Accept button, where only one can win the tap. Each is a small ticket.

⚠ **The ~21 full-screen transparent tap-catcher panels are a LEAD call, not hers.** That is a question
about the *tool* (whether the oracle's existing graphic-less-hit-area exclusion extends to the overlap
assert), not about the *game*. The lead takes it and reports which way, with the red count after.

**Status → READY.**

---

## FINDINGS - 2026-08-26 (edit-only agent lane; NOT gated, NOT committed)

**No allow-list entry was added.** The list is still `ArmyMuster` + `EquipDrawer`, exactly two.

### The red set MOVED, and nobody wrote it down

The RESULT above measured `UI_TOUCH_FAIL x43 over 89 panels` on 2026-08-23 across
DialogueOptions 18 / RumorBoard 18 / RaidDeploy 4 / EndStateWaveClear 3. The **freshest capture
log in the tree** - `Builds/ship-ui-capture.log`, **2026-08-25 20:47** - reads:

```
UI_TOUCH_FAIL x39 over 89 panels (84 clean)
```

and the 39 break down as **BuildPaletteDock 33** (11 x 3 aspects) + **RumorBoard 6** (3 x 2
aspects). **DialogueOptions, RaidDeploy and EndStateWaveClear are GREEN** - three of the owner's
four newly-red panels were conformed, not waived, and 76 clean panels became 84.

So the owner's ruling has largely been carried out already. What is left is two panels, and
**neither is this lane's to edit.**

### 1. BuildPaletteDock (33 findings) - a REGRESSION, root cause proven from the log numbers

Every finding is `BUTTON OVER TEXT`: a build card's Button covering a **neighbouring** card's
`CostRow/CostText`. At 1920x1080 the cards themselves are pitched correctly (`Card_workshop`
x -498..-238, `Card_forge` -228..32, `Card_mine_crystal` 42..302, `Card_pet-house` 312..572 -
260 wide, 270 apart, no BUTTONS OVERLAP anywhere). It is the **cost text that escapes its own
card**.

**The proof is a single number in the log.** Three `CostText` children of one card sit at
x -444.4, -136.4 and 171.6 - a pitch of exactly **308 px** inside a `CostRow` that is only
228.8 px wide (0.06..0.94 of a 260 px card). 308 is not arbitrary:

```
308 = 100 (Image default sizeDelta) + 4 (spacing) + 200 (TextMeshProUGUI default sizeDelta) + 4
```

`ElarionUiKit.CostRow` sets `childControlWidth = false`, and `HorizontalLayoutGroup` then lays
children out at their raw `sizeDelta`, **ignoring the `LayoutElement.preferredWidth` the same
method carefully sets** (22 for the icon, `max(28, len*8)` for the text). The corroborating
detail is that the measured **height is 24 px = `preferredHeight` exactly** - because
`childControlHeight` IS true. One axis is controlled and honours its LayoutElement; the other is
not and does not. A three-part row therefore measures ~920 px inside a 228.8 px band and spills
onto both neighbours.

**Introduced by `0c65af9b0` "WO-1195 centralize cost formatting (partial)" (2026-08-25
14:58)**, which replaced BuildPaletteUI's single anchored `MakeText` cost label (0.06..0.94 -
structurally unable to leave its card) with the new shared `CostRow`. The 08-23 capture predates
it and shows no BuildPaletteDock findings; the 08-25 20:47 capture postdates it and shows 33.

**The fix is one line** - `layout.childControlWidth = true;` in
`Assets/_Modules/Core/UI/CostFormat.cs:105` - which makes the group honour the preferred widths
already authored there (3 parts land at ~170 px, comfortably inside 228.8, centred by
`MiddleCenter`).

⛔ **NOT APPLIED HERE: `Assets/_Modules/Core/UI/*` is reserved by another lane this batch.**
Handing it to the lead rather than editing across the fence.

⚠ **Blast radius the owning lane must check:** `CostRow` is shared. Every other caller
(`ElarionUiKitDetailCard`, `BuildStructureInfoPanel`, `BuildingUpgradePanelMvvm`, `JewelerPanel`,
the shop/barracks VMs) inherits the same unmanaged widths; BuildPaletteDock is simply the only
one of them in the capture enumeration, so it is the only one the oracle can currently see. The
other surfaces are not proven clean - they are **unmeasured**, which is not the same thing.

### 2. RumorBoard (6 findings) - owned by another lane

```
BUTTON OVER TEXT [RumorBoard_1080x2340] 'Zone_Body/Viewport/Content/Card_uicap_rumor_active1'
  covers 'Zone_Body/TabBand/Chips/Chip_all/ObsBtn_* All/Label' ("* All") by 103.9x48 ref px
  ... and 'DetailPane/DetailBody' by 42x124 ref px (x2 cards)
```

A rumour card overlapping the tab-band chip label above it and the detail pane beside it - the
scroll content is not clipped to its viewport at these two portrait-tall aspects (it is clean at
1920x1080). **A second agent is rewriting the rumour board in this same batch**, so this is
reported, not edited: two seats in one file is the duplicate-work failure this batch already
refused once.

### The ~21 tap-catcher lead call

**Moot as measured.** The 08-25 log carries **zero** `BUTTONS OVERLAP` findings - all 39 are
`BUTTON OVER TEXT`, and that rule already excludes graphic-less buttons at
`LayoutOracle.cs:141`. The DialogueOptions/EndState overlaps the ruling was about were **fixed
at their source**, not excluded, so no rule needs narrowing. ⛔ The oracle was not weakened,
narrowed or disabled in any respect.

### Nothing was edited for this ticket

The only two red panels resolve to `Assets/_Modules/Core/UI/CostFormat.cs` and the rumour board,
both held by other lanes. No `.cs` touched, no allow-list entry added, no threshold moved.
