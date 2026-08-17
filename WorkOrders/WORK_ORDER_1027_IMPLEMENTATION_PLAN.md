# WO-1027 IMPLEMENTATION PLAN — the ache, carried by shape and number

**Written:** 2026-08-17 (planning agent, read-only — an APK build held the tree; nothing was edited)
**Plans:** `WorkOrders/WORK_ORDER_1027_session_shape_and_builder_ache.md`
**Design is RULED and CLOSED (2026-08-17):** **(b) empty-slot silhouette** as the resting state
**+ (a) the "2 of 3 lines idle" numeral**. **(c) the toast is REJECTED.** This plan does not reopen §4.
**⛔ NO HUE. EVER.** If shape + numeral do not read, the fix is a clearer SHAPE. Not a tint, not an
accent, not "just a subtle warm edge". Every acceptance below is greyscale-first.

---

## 0. ⚠ READ THIS BEFORE PLANNING ANY UI — TWO TREE FACTS THAT CHANGE THE TICKET

The WO was minted 2026-08-15 against a HUD that has since moved. Verified at source this session:

### 0a. ⛔ THE BUILDERS CHIP DOES NOT EXIST. It was retired by the owner on 2026-08-07.

`Assets/_Modules/HUD/Kit/HudKitController.cs:505`:

```csharp
// BuildQueueStatusChip(pool);   // retired 2026-08-07 (owner)
```

with the reason at `:497-504`:

> *"This SUPERSEDES WO-911 ruling Q10/Q13, which kept the chip as a status glance after retiring its
> double-tap door. That ruling's real intent — exactly ONE Queues entry — is unchanged and now simply
> lands on the bar's Manage face alone. BuildQueueStatusChip and its QueueRailView are LEFT INTACT
> below, unreferenced."*

**Therefore WO-1027 §3.1 — *"The right-column Builders chip already exists as a status glance with an
inline peek rail (canon §7); it is the natural home"* — is FALSE against the tree.** The named home is
dead code. `BuildQueueStatusChip` (`:832`), `_queueChipLabel` (`:176`), `_queueRail` (`:177`),
`FormatQueueChip` (`:978`) and `OnBuildersChipTapped` (`:880`) are all still compiled and all
unreachable from `BuildHud`.

**⚠ `CLAUDE.md` §7 is STALE on exactly this point** — it still says *"The right-column Builders chip
SURVIVES as a STATUS GLANCE ONLY (count/timer + the inline peek rail)"*. Per §15 this must be corrected
in the same commit as the work.

> **⛔ DO NOT UN-RETIRE THE CHIP.** The owner removed it deliberately and the removal comment states
> the intent. Re-adding it to host this feature would quietly overturn an owner ruling to satisfy a
> stale sentence in an older WO. **Route the glance to where the owner already put the door.**

### 0b. ★ THE EMPTY-SLOT SILHOUETTE IS ALREADY BUILT — and it is nearly invisible

`Assets/_Modules/Core/UI/QueueRailView.cs:288-296` already synthesises an empty-slot card:

```csharp
for (int i = 0; i < free; i++)
    outp[w++] = new ObsidianQueueGate.QueueEntry
    { Free = true, Verb = "FREE", Label = "Open slot", RemainingSec = -1, StackCount = 1, };
```

and the data contract at `Assets/_Modules/Core/UI/ObsidianQueueGate.cs:83-85` states the law outright:

> *"TRUE => this is an EMPTY SLOT placeholder, not a job. A free slot must render as a visible
> empty-slot CARD, never as blank space (WO-864 bug 2)."*

**So option (b) is not new work — it is work that shipped and then failed to read.** The defect is at
`QueueRailView.cs:417-431`:

| element | busy card | free card | separation |
|---|---|---|---|
| plate colour | `(0.09, 0.085, 0.08, 0.95)` | `(0.10, 0.10, 0.12, 0.55)` | **Rec.709 luma ≈ 0.086 vs 0.101 — a gap of 0.015** |
| plate sprite | `RpgUiCatalog` slot frame, `Image.Type.Sliced` | **no sprite**, `ApplyRounded` only (`:431`) | the only real shape difference |
| centre mark | portrait art, else the name's initial | `"+"` (`:461`) | ✅ genuinely shape-carried |
| text | `Gilt` | `ParchmentDim` | dim-vs-dim |

⚠ **A 0.015 luma gap is a hue-free signal that is *still* invisible** — which is the colourblind law's
sibling failure and just as disqualifying. `TalentFocusSingletonRegression.cs:67` sets this project's
own bar at **`MinGreyscaleLumaGap = 0.45f`**, thirty times what the free card manages. **The plate is
the problem; the `+` is the part that already works.**

> **This reframes the whole ticket and makes it cheaper than the WO estimated.** Deliverable (b) is
> *"make the existing free card actually read as an empty socket"*, not *"build an empty-slot
> silhouette"*. Deliverable (a) is one new derived numeral. Nothing else is new.

---

## 1. THE SHAPE OF THE FIX — three surfaces, one authority, zero new systems

| # | what | where | new? |
|---|---|---|---|
| **A** | **The numeral** — the `Manage` action-bar face carries `"Manage"` → `"Manage 2/3 idle"` | `HudActionBarModel` (Core) | new derivation, **exact existing precedent** |
| **B** | **The silhouette** — the free card becomes a real empty SOCKET: recessed plate at a legible luma gap, a dashed/notched frame, and the `+` kept | `QueueRailView` (Core) | **repair of shipped code** |
| **C** | **The one-tap door + the "you're set" state** | the `Manage` face (already the single Queues entry) + a rail end-state | routing already exists |

**No new panel. No new action-bar face. No new spawner, service, poller or state.** Exactly what §5 of
the WO demands.

### 1a. ★ WHY THE BAR FACE IS THE RIGHT HOME (and the only one left)

`Assets/_Modules/Core/HudModel/HudActionBarModel.cs` **already does precisely this, for Raids**:

- `:198` `public const string RaidsBaseLabel = "Raids";`
- `:195` `private string _raidsFaceLabel = RaidsBaseLabel;` — the face word is **model-owned state**
- `:201-206`: *"The owner is red/green colourblind — a grey tint carries NO meaning for her, so every
  dim state must ship a **WORD/NUMBER tell** as well (`RaidsFaceLabel` on the face, `RaidsDimMessage`
  on the tap)."*
- `:26-29`: WO-1008 fed slot counts in *"only to … build the face's WORD/NUMBER tell"*
- `:36-41`: the model already **polls `ObsidianQueueGate`** every `Tick()` and fires
  `ActiveButtonsChanged` **only on a real transition** — the poll cost for this feature is zero
- `:12-14`: *"the View (HudKitController) subscribes … and just renders. **Zero predicates remain in
  the View.**"*

`UICaptureLaunch.cs:535` even shoots it: `CaptureRaidsFaceStates(); // WO-1008: the bar face live /
0-of-cap / partial`.

**A word-and-number tell on a bar face is this repo's established, owner-approved, screenshot-gated
answer to exactly this problem.** Copy it verbatim for Manage. It also satisfies §6 of the WO by
construction: the ache lives ON the button that fills it, so *"one tap from the thing that fills it"*
costs zero new code.

---

## 2. ⭐ THE SINGLE AUTHORITY FOR IDLE-LINE STATE — `ObsidianQueueGate.Status`

**This project's dominant bug is duplicate authority. So: nothing below computes idleness. One place
already knows, and it is already presentation-ready.**

`Assets/_Modules/Core/UI/ObsidianQueueGate.cs` (assembly `DeNelle.Core`, namespace `DeNelle.Core.UI`):

| member | line | gives us |
|---|---|---|
| `WorkQueueStatus Status { get; private set; }` | `:151` | the published snapshot |
| `bool Available` | `:91` | **false until the service publishes — the "don't lie yet" guard** |
| `int BusyOf(ChannelId)` | `:126-134` | active workers on a line |
| `int SlotsOf(ChannelId)` | `:115-123` | total worker slots on a line |
| `QueueEntry[] EntriesOf(ChannelId)` | `:102-112` | the cards, never null |
| `int Version` | `:99` | bumps per publish — the change-detect the model already uses |
| `PublishStatus(...)` | `:154-158` | **Village writes; nobody else ever does** |

Its own header states the contract (`:46-49`):

> *"BuildTimerService (DeNelle.Village) owns queue + clock; it pushes a presentation-ready snapshot
> here … The HUD chip polls Status … **no cross-assembly read**."*

**The definition of idle, stated once and nowhere else:**

```
IsLineIdle(c)   ==  Status.Available && Status.BusyOf(c) == 0
IdleLineCount() ==  count of { Builder, Train, Research } where IsLineIdle
LineCount       ==  3   (the three ChannelId values)
```

> ⚠ **IDLE MEANS `Busy == 0`, NOT `Busy < Slots`.** A line running 1 of 2 builders is *working*, not
> aching. `Busy < Slots` is a different fact — it is what makes the FREE CARD appear in the rail
> (surface B), and it is deliberately a **finer** signal than the numeral. Conflating them would make
> the bar say "idle" while a building is visibly under construction, which reads as a bug.

> ⚠ **`queueDepthPerLine` (5) IS NOT AN INPUT TO EITHER SIGNAL.** `BuildTimerConfig.cs:159` is the
> LINE LENGTH; `freeBuildSlots` (`:136`, =2) is CONCURRENCY. `ObsidianQueueEngine.cs:50-52` warns:
> *"Depth is NOT concurrency … Conflating them (by raising freeBuildSlots to 5) would delete the
> waiting pain the crystal sink monetizes."* **Idleness is a concurrency fact only.** Reading
> `queueDepthPerLine` anywhere in this feature is a bug — if it appears in a diff, reject the diff.

### 2a. ⛔ WHY NOT `BuildTimerService` — the assembly wall

`BuildTimerService` lives at `Assets/_Modules/Village/Buildings/BuildTimerService.cs`, assembly
**`DeNelle.Village`**. It has the richer API — `SlotCount(ChannelId)` `:198`, `ActiveJobsOf(ChannelId)`
`:351`, `PendingJobsOf(ChannelId)` `:358`, `IsLineFull(ChannelId)` `:666`, `HasFreeSlot` `:429` — and
**it is unreachable from the HUD**. `Assets/_Modules/HUD/DeNelle.HUD.asmdef` references
`DeNelle.Core` + `DeNelle.Data` **only** (CLAUDE.md §5, the one enforced invariant). `HudActionBarModel`
is in `DeNelle.Core`, which cannot see `DeNelle.Village` either.

**So the gate is not a convenience — it is the only legal seam, and it is already the one in use.**

⚠ **Confirmed: there is NO existing `IsLineIdle` / `IdleLineCount` / `FreeSlotsOf` anywhere in the
tree** (the only `Idle*` hit is `WandererDialogue.PickIdleLine`, unrelated). The closest existing
readers each derive their own local answer, and there are **already three of them**:

| # | site | what it derives | axis |
|---|---|---|---|
| 1 | `BuildTimerService.PublishStatus` → local `Fill(...)`, `BuildTimerService.cs:1425-1465` (esp. `:1434-1447`) | `busy = ActiveJobsOf(id).Count; slots = SlotCount(id); queued = PendingJobsOf(id).Count;` | **THE root computation** — everything else is downstream of this |
| 2 | `QueueRailView.BuildCardModel`, `QueueRailView.cs:276-277` | `int free = Mathf.Max(0, slots - st.BusyOf(channel));` | busy/free |
| 3 | `ManageScreenVM.ChannelSummary` + `AddSummary`, `ManageScreenVM.cs:66-89`, `:324-335` | `Busy/Slots/Depth/DepthCap` read from the **service directly** (Village-side) | busy/free |

**All three are the busy/FREE-SLOT axis; none is the idle-LINE axis.** So the derivation is genuinely
new — and #2 and #3 are exactly why it must be minted **once**, on the struct, rather than as a fourth
local expression. ⚠ **Also re-point `QueueRailView.cs:276-277` at the new helper** rather than leaving
it as a parallel computation.

⚠ **There is no `ChannelId.All` array.** `enum ChannelId { Builder=0, Train=1, Research=2 }` lives at
`Assets/_Modules/Core/Jobs/JobKind.cs:96-104`, and **every caller hand-lists the three**
(`BuildTimerService.cs:1445-1447`, `ManageScreenVM.cs:320-322`). `IdleLineCount()` hand-listing them a
third time is consistent with the house style; do **not** greenfield an `All` array as a side quest.

### 2b. Where the derivation lives: **on `ObsidianQueueGate.WorkQueueStatus` itself**

Add two members to the struct that already owns `BusyOf`/`SlotsOf`/`EntriesOf`/`LabelOf`, so the fact
is inseparable from the data:

```csharp
// ── WO-1027: the session-shape derivation. IDLE == zero active workers on the line.
// Deliberately NOT (Busy < Slots): a line running 1 of 2 is WORKING. The finer
// has-a-free-slot fact is the FREE CARD's business (QueueRailView), never the numeral's.
// ⚠ queueDepthPerLine is NOT an input — depth is not concurrency (ObsidianQueueEngine:50).
public bool IsLineIdle(DeNelle.Core.Jobs.ChannelId c) => Available && BusyOf(c) == 0;

/// <summary>How many of the three channels have NOTHING running. 0 before first publish.</summary>
public int IdleLineCount()
{
    if (!Available) return 0;          // never claim idleness we have not been told about
    int n = 0;
    if (IsLineIdle(ChannelId.Builder))  n++;
    if (IsLineIdle(ChannelId.Train))    n++;
    if (IsLineIdle(ChannelId.Research)) n++;
    return n;
}

/// <summary>The three queue channels. The denominator of the "N of 3 idle" glance.</summary>
public const int LineCount = 3;
```

**Every one of the three surfaces reads these two members and computes nothing.** That is the whole
duplicate-authority defence: A, B and C cannot disagree because there is only one sentence.

---

## 3. FILES — every create/modify, with its assembly

⚠ **`.asmdef` read, not assumed** — `Assets/_Modules/HUD/DeNelle.HUD.asmdef` (references
`DeNelle.Core` + `DeNelle.Data` only), `Assets/_Modules/Core/DeNelle.Core.asmdef`,
`Assets/_Modules/Village/DeNelle.Village.asmdef`.

| # | file | assembly | change |
|---|---|---|---|
| 1 | `Assets/_Modules/Core/UI/ObsidianQueueGate.cs` | **DeNelle.Core** | **+`IsLineIdle`, +`IdleLineCount`, +`LineCount`** on `WorkQueueStatus` (§2b). ⛔ Pure derivation — **do not touch `PublishStatus`, `Version` or any field**; the publisher stays the sole writer |
| 2 | `Assets/_Modules/Core/HudModel/HudActionBarModel.cs` | **DeNelle.Core** | **+`ManageBaseLabel`, +`ManageFaceLabel`, +`ManageFaceChanged`**, +the `Tick()` recompute. Cloned line-for-line from the Raids tell (`:26-29`, `:193-206`) |
| 3 | `Assets/_Modules/HUD/Kit/HudKitController.cs` | **DeNelle.HUD** | View-only: subscribe `ManageFaceChanged`, set the Manage face's label text. ⛔ **ZERO predicates** (`HudActionBarModel.cs:14`). Do NOT read `ObsidianQueueGate` here |
| 4 | `Assets/_Modules/Core/UI/QueueRailView.cs` | **DeNelle.Core** | **Surface B**: raise the free card's plate to a legible greyscale gap + give it a socket outline. Touch `BuildCard` `:415-431` and `:458-464` only |
| 5 | `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` | **DeNelle.Village** | **Surface C**: the "you're set" end-state when `IdleLineCount() == 0` and no line has a free slot. Read-only use of #1 |
| 6 | `Assets/Editor/Regression/SessionShapeRegression.cs` | **DeNelle.EditorRegression** | **NEW** oracle (§5) |
| 7 | `Assets/Editor/Regression/DataRegression.cs` | DeNelle.EditorRegression | one registration line (§5b) |
| 8 | `Assets/Editor/UICaptureLaunch.cs` | DeNelle.Editor | **+`CaptureSessionShapeStates()`** — idle / partial / fully-loaded (§6) |
| 9 | `CLAUDE.md` §7 | — | **§15, same commit**: correct the stale "Builders chip SURVIVES" line (§0a) |

**⚠ Files 1–5 are file-disjoint from WO-1038's lane** (`TagManager`, `PetHarvestBootstrap`,
`OutpostDefender`, the `SafeFindWithTag` sweep). The two tickets can run as parallel edit-only agents
per CLAUDE.md §11; **file 7 (`DataRegression.cs`) is the one collision** — both add a registration
line, so **one agent owns that file** and the other hands over its line as text.

### 3a. Surface A — the numeral, in `HudActionBarModel`

Mirror `_raidsFaceLabel` exactly:

```csharp
/// <summary>The base (unadorned) Manage face word. The View builds with this exact string.</summary>
public const string ManageBaseLabel = "Manage";

/// <summary>
/// WO-1027 — the SESSION-SHAPE tell on the Manage face. CoC's ache is a RED BADGE; the owner is
/// red/green colourblind so a badge is banned outright (WO-1027 sec 4). The message is carried by a
/// NUMERAL, which has no hue to get wrong and needs no mitigation. Same WORD/NUMBER-tell contract
/// the Raids face already ships (WO-1008, RaidsFaceLabel above).
///   3 idle -> "Manage - 3 idle"     (nothing cooking at all)
///   1-2    -> "Manage - 2 of 3 idle"
///   0      -> "Manage"              (the calm state IS the bare word; silence is the reward)
/// ⛔ NEVER append a colour, glyph badge or tint. If it does not read, make the WORD clearer.
/// </summary>
public string ManageFaceLabel => _manageFaceLabel;
```

recomputed inside the existing `Tick()` (the poll at `:36-41` already runs), guarded on
`ObsidianQueueGate.Status.Version` so it is a **transition**, not a per-frame string build, and
raising `ManageFaceChanged` only on a real change.

> ⚠ **"N of 3 idle" not "N idle" when N<3.** The denominator is what makes the numeral legible without
> the player knowing the system — WO-1027 §4 option (a) is quoted as *"2 of 3 lines idle"*. At N=3 the
> denominator is noise, so the full-idle case drops it. At N=0 there is **no tell at all** — the WO's
> §3.3 "session-complete signal" is the ABSENCE of the numeral, which costs nothing and cannot nag.

**⛔ Do NOT add an eighth face.** `ActionBarButtonId.Upgrade = 6` keeps its value, `Map` stays dormant
at ordinal 4, `ButtonCount` stays 7, `MaxVisibleFaces` stays 6 (`HudActionBarModel.cs:121`). This
changes a face's **label string** and nothing else about the bar.

### 3b. Surface B — make the socket read (the actual visual work)

In `QueueRailView.BuildCard` (`:415-431`). Four shape moves, **zero hue**:

1. **Luma gap.** The free plate must clear `MinGreyscaleLumaGap` against the busy plate — the same
   `0.45f` bar `TalentFocusSingletonRegression.cs:67` already enforces, using the same Rec.709 `Luma`
   helper (`:227-231`). Today's gap is **0.015**. A recessed socket that reads is *darker* and the
   busy card is *lighter* — the busy card already carries the `RpgUiCatalog` slot frame at
   `Color.white` (`:429`), so most of the gap is available by **removing the free plate's competing
   near-black rather than by tinting anything**.
2. **A socket OUTLINE.** The busy card is a filled sliced frame; the free card should be an **open
   outline / dashed rule** — present, obviously a container, obviously containing nothing. Silhouette
   in the literal sense. Built through `ElarionUiKit`, never a raw `Image`.
3. **Keep the `+`** at `:461` — it already works and it is the only part of the current free card that
   survives greyscale.
4. **Inset the plate** a few px versus a busy card so the row reads as *socket, socket, card* by
   rhythm even at a glance, from silhouette alone.

> ⚠ **This is a Core file that the Manage screen and the (retired) chip both host** —
> `HudKitController.cs:851-853` calls it *"The SHARED component … so the two surfaces can never show a
> different queue visual."* Fixing it here fixes it everywhere, which is the point. It also means a
> careless change here is visible on every queue surface at once — screenshot all of them (§6).

### 3c. Surface C — "you're set", and the one tap

- **One tap:** free, and the door already exists — `HudKitController.OnManageAction()` at
  **`HudKitController.cs:1895-1906`**:
  `if (ObsidianQueueGate.HasSubscriber) { ObsidianQueueGate.RequestToggle(); return; }` (`:1898-1902`),
  falling back to `PanelRouter.Open(PanelId.Manage)` (`:1903`) on a boot race. The face is built and
  bound at `HudKitController.cs:613-616` (`RegisterBarButton(ActionBarButtonId.Upgrade, "upgradeButton",
  manage)`), the panel registers itself at `ManageScreenPanel.cs:130-136`, `PanelId.Manage = 16`
  (`PanelRouter.cs:100`). ⛔ **Reuse `OnManageAction` verbatim — do not add a second door.**
  ⚠ `ObsidianQueueGate.RequestToggle()` takes **no channel argument**, so "open straight to the idle
  line" is NOT available today. **Do not build that** — the WO asks for one tap to the screen that
  fills it, and the Manage screen's own tabs (`ManageScreenVM.SelectTab` `:268`, `ChannelOf` `:254`)
  take it from there. Deep-linking would need a `PanelRouter.Open(PanelId, context)` registration
  (`PanelRouter.cs:174`) and is a separate ticket.
- **"You're set":** in `ManageScreenPanel`, when `Status.IdleLineCount() == 0` **and** every channel has
  `BusyOf(c) == SlotsOf(c)`, state it in words. WO §3.3 calls this a genuine improvement over CoC;
  it must be **a sentence**, not a colour, not a checkmark glyph.
- ⛔ **No toast, no popup, no entering-town trigger, anywhere.** Ruling (c) is REJECTED. A reviewer
  seeing any `Toast`/`Nudge`/`OnTownEntered` call in this diff should red it on sight.

---

## 4. `FlowTrace` / `Guard` POINTS (§12 — instrumentation is permanent)

**⛔ USE THE TAG THE FILE ALREADY USES. Do not mint a new system name for one feature.** Verified at
source, per file:

| file | its existing `FlowTrace` system tag | evidence |
|---|---|---|
| `ObsidianQueueGate.cs` | **`"HUD"`** | `:42` |
| `HudActionBarModel.cs` / `HudKitController.cs` | **`"HudKit"`** | `HudKitController.cs:886, 904, 967, 1897, 1905` |
| `QueueRailView.cs` | **`"QueueUi"`** | `:194, 220, 222, 233, 637` (also `QueueIconResolver.cs:71`) |
| `ManageScreenPanel.cs` / `ManageScreenVM.cs` | **`"Manage"`** | `ManageScreenBootstrap.cs:36` + 27 calls across the trio |
| `BuildTimerService.cs` | `"BuildTimer"` / `"Obsidian"` | `:496, 500, 537, 546, 1134` / `:315, 330, 337` |

| where | call | why |
|---|---|---|
| `HudActionBarModel` — on the label **transition only** | `FlowTrace.Step("HudKit", $"manage face tell: '{old}' -> '{new}' (idle {n}/3, statusVer={v})")` | ⚠ **Step on the EDGE, never in `Tick()`.** WO-1038 §4b and its three-strike table: a permanent/recurring condition logged per frame buries the owner's real signals. The model is already edge-triggered — log where it already transitions |
| `HudActionBarModel` — status never published | `FlowTrace.Once("HudKit", "manage-tell-unavailable", "ObsidianQueueGate.Status.Available is false — Manage face shows the bare word; BuildTimerService has not published yet")` | ⚠ `Once`, not `Warn`. This is **normal at boot**. Without it, "the numeral never appears" is indistinguishable from "the numeral is broken" — the exact ambiguity §12.3 says to split *before* touching code |
| `HudKitController` — applying the label | `Guard.Try("HudKit", "apply manage face tell", () => …)` | a null face ref must not take the bar down with it |
| `QueueRailView.BuildCard` | `Guard.TryEach("QueueUi", …)` over the entry array | one bad card logs and is skipped; it must never blank the rail (§12.2) |
| `ManageScreenPanel` — the "you're set" state | `FlowTrace.Step("Manage", "session complete: all 3 lines loaded, no free slots")` | fires on a real transition, at most a handful per session |

**⛔ Never `FlowTrace.Warn`/`Fail` for an idle line.** Idle is a *normal player state*. Logging it at
error severity would put a routine condition into the F8 capture queue and bury real defects — the
standing lesson from WO-1022 / WO-1025 / WO-1038.

---

## 5. THE ORACLE — `SessionShapeRegression`

**Assembly `DeNelle.EditorRegression`; markers `SESSION_SHAPE_OK` / `SESSION_SHAPE_FAIL`** — distinct
per canon §8 (a shared `REGRESSION_OK` is how a 22-case pass once read as a full-suite pass).

> ### ⚠ THE ASSEMBLY CONSTRAINT THAT SHAPES EVERY CASE
> `Assets/Editor/Regression/DeNelle.EditorRegression.asmdef` references `DeNelle.Core`,
> `DeNelle.Village`, `DeNelle.Pets`, `DeNelle.Data`, `DeNelle.BattleATB`, `DeNelle.Dungeons`,
> `DeNelle.Wallet` — **NOT `DeNelle.HUD`.**
> So cases touching `ObsidianQueueGate`, `HudActionBarModel` and `QueueRailView` (all **Core**) can be
> **behavioural** — call the real code. Anything about `HudKitController` (**HUD**) must be a
> **source-text lint**, which is exactly why `HudActionBarRegression.cs:17-21` says so in its own
> header. Plan each case on the right side of that line before writing it.
>
> **Also already guarding this lane:** `UiObsidianConformanceRegression`
> (`Assets/Editor/Regression/UiObsidianConformanceRegression.cs`, `HardFailOnNew = true` `:74`,
> `KitToken = "ElarionUiKit"` `:78`, strong-smell regexes `:82-90`), registered at
> `DataRegression.cs:387`. **Every widget added in surfaces A–C must route through `ElarionUiKit` or
> this gate reds on its own** — no new oracle needed for kit conformance. Relevant factories:
> `ElarionUiKit.Label` (`ElarionUiKit.cs:1708`), `AddImage` (`:2304`), `ApplyRounded` (`:2319`),
> `BuildObsidianButton` (`ElarionUiKitObsidian.cs:617`), `FitSingleLine` (`:2857`).

**Skeleton copied from `Assets/Editor/Regression/TalentFocusSingletonRegression.cs`** — the closest
neighbour by subject (it is *the* greyscale oracle in this repo) and by shape: `RunAll()` `:69-73`
emitting the marker, `Run(out string reason)` `:76-103` as the DataRegression-shaped contract that
**never throws**, the per-case `Case(failures, name, body)` wrapper `:105-109`, a `notes` list, and the
Rec.709 `Luma` helper `:227-231` with `MinGreyscaleLumaGap = 0.45f` `:64-67`.

### 5a. Cases

| # | case | asserts | the defect it catches |
|---|---|---|---|
| 1 | `[idle-authority]` | `IsLineIdle`/`IdleLineCount` over a built `WorkQueueStatus` fixture: 3 lines all empty → 3; Builder busy 1/2 → **2** (⚠ *not* 3 — a partly-worked line is NOT idle); all busy → 0; `Available=false` → **0**, never 3 | the boot lie ("3 idle" on a screen that has heard nothing) and the `Busy<Slots` conflation |
| 2 | `[no-depth-input]` | source-lint: neither `ObsidianQueueGate.cs` nor `HudActionBarModel.cs` mentions `queueDepthPerLine`; and `IdleLineCount` is unchanged when only the pending queue length changes | depth/concurrency conflation (`ObsidianQueueEngine.cs:50-52`) |
| 3 | `[greyscale]` ⭐ | Rec.709 luma of the free plate vs the busy plate `>= MinGreyscaleLumaGap (0.45)`; free-card ink alpha `>= 0.5`; **the free card and the busy card differ in at least one non-colour channel** (sprite / outline / inset) | **the ruled design failing silently.** Today's tree scores **0.015** — this case is RED before the fix and is the proof the fix landed |
| 4 | `[no-hue-signal]` | source-lint of the three touched UI files: no new `Color` literal is introduced whose signal survives ONLY in hue — i.e. every state pair separated by colour is also separated by luma ≥ the bar. Precedent: the source-lint style `StructureTargetableRegression.cs:28-29` names as the house pattern (`UiMvvmConformanceRegression`) | the "let me just add a small warm accent to help" regression — the exact thing the ruling bans |
| 5 | ~~`[mechanics-frozen]`~~ | ⛔ **DO NOT WRITE THIS CASE — it already exists.** `ObsidianQueueRegression.CheckWo911DepthCap` (`Assets/Editor/Regression/ObsidianQueueRegression.cs:391-429`) already asserts `queueDepthPerLine == 5` and `freeBuildSlots == 2` **and drives the real `ObsidianQueueEngine`** to prove the axes stay separate. Registered at `DataRegression.cs:357`. **Cite it in the RESULT; adding a second copy would itself be the duplicate-authority bug this ticket is trying to avoid** | WO §6, already covered |
| 6 | `[bar-shape]` | `HudActionBarModel.ButtonCount == 7`, `MaxVisibleFaces == 6`, `(int)ActionBarButtonId.Map == 4`, `(int)ActionBarButtonId.Upgrade == 6` | an eighth face / an ordinal renumber silently re-pointing the View's face arrays |
| 7 | `[tell-is-a-transition]` | driving the model across N publishes with an unchanged status raises `ManageFaceChanged` **at most once** | a per-frame string build and a per-frame `FlowTrace` line (the §4b flood) |
| 8 | `[calm-is-bare]` | `IdleLineCount()==0` ⇒ `ManageFaceLabel == ManageBaseLabel` exactly | a permanent adornment on a calm bar — the nag ruling (c) rejected, sneaking in by another door |

⚠ **Prove the red on cases 1, 3 and 8**, then restore — WO-1038's acceptance criterion, applied here.
**Case 3 is red on the tree as it stands today**, which is the cheapest possible proof: run it before
the fix, capture the failure line, fix, run again.

### 5b. Registration — one line in `Assets/Editor/Regression/DataRegression.cs`

Copying the neighbour idiom verbatim from `:547` (and `:535`, `:541`, `:557`):

```csharp
// --- WO-1027 session shape: the ache is carried by SHAPE + NUMBER and never by hue (the owner is
// red/green colourblind; CoC's red badge is banned outright). Pins the ONE idle-line authority
// (ObsidianQueueGate.WorkQueueStatus.IdleLineCount -- idle means zero ACTIVE, never Busy<Slots),
// that the empty-slot socket clears the 0.45 Rec.709 luma bar the talent oracle already sets, that
// queue MECHANICS are untouched (freeBuildSlots 2 / depth 5, different axes), and that a calm bar
// shows the BARE word -- the rejected nudge cannot return as a permanent adornment. ---
DeNelle.Core.Diagnostics.Guard.Try("Regression", "session-shape suite", () => { if (!DeNelle.Editor.Regression.SessionShapeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[session-shape] " + r); });
```

---

## 6. ACCEPTANCE — headlessly checkable

| # | criterion | how it is checked headlessly |
|---|---|---|
| 1 | `COMPILE_GATE_OK` (incl. the NUL-byte scan) | `DeNelle.Editor.CompileGate.Run` |
| 2 | `REGRESSION_OK <n>/<n> suites` **and `SESSION_SHAPE_OK` present in the same log** | ⚠ grep for **both**; a green aggregate proves nothing about which suite ran (canon §8) |
| 3 | **The greyscale bar is met** — free plate vs busy plate ≥ 0.45 Rec.709 luma | **case 3**, numerically, in the log line. ⭐ This is the colourblind law as a NUMBER, not an eyeball |
| 4 | Screenshots in **three** states: all-idle / partial / fully-loaded | **new `CaptureSessionShapeStates()`** in `UICaptureLaunch.RunCaptureHeadless` (`:512-535`), via the existing `ForEachTarget("…", …)` idiom used by `CaptureQueueRail` (`:2236-2241`). ⚠ WO §7 says two states; **three**, because "partial" is the state the *of 3* denominator exists for and the only one where the numeral can be wrong |
| 5 | **Greyscale pass on each PNG** | desaturate each capture and confirm the socket and the numeral both survive. ⚠ Also **open the colour PNGs** (memory `headless-screenshot-verify-ui-before-build`; `RumorBoardPanel.cs:42` and `DataRegression.cs:393` both record panels that shipped broken behind a green capture marker) |
| 6 | `UI_CAPTURE_OK <n>` with n increased, plus `ReportFidelity`/`ReportGeometry` clean | `UICaptureLaunch.cs:544-551` |
| 7 | Mechanics frozen: `freeBuildSlots == 2`, `queueDepthPerLine == 5` | case 5 |
| 8 | Bar shape frozen: 6 visible, `ButtonCount` 7, `Map` dormant at 4 | case 6 |
| 9 | Zero `Toast`/`Nudge`/`OnTownEntered` in the diff | ruling (c) — grep the diff |
| 10 | `CLAUDE.md` §7 corrected re: the retired chip (§0a) | §15, same commit |
| 11 | Owner felt-verify: *"after ten seconds in town, do I know what to do next?"* | PO closes (§13). ⚠ **CLI never closes this** |

⛔ **A green gate is not a green feature.** Criteria 4 + 5 are the ones that can actually fail here —
this is a *legibility* ticket, and memory `screenshots-are-primary-evidence-for-visual-defects` applies
in full: FlowTrace shows what the code believes, the screenshot shows what the player sees.

---

## 7. TOP RISKS

| # | risk | mitigation |
|---|---|---|
| 1 | ⛔ **An implementer reads WO §3.1, goes looking for the Builders chip, finds it dead, and UN-RETIRES it** — silently overturning an owner ruling | §0a is the first thing in this plan. The home is the **Manage bar face** |
| 2 | ⛔ **Someone "helps" the socket with a colour accent** | Ruled out twice, plus **case 4** lints for it and **case 3** measures it. If the socket does not read, the answer is a heavier outline or a deeper inset |
| 3 | **`IdleLineCount` gets recomputed at a second site** (the View, the Manage panel, a future widget) | §2b puts it on the struct every reader already holds. **Case 1 pins the semantics; a code review should red any `BusyOf(...) == 0` written outside `ObsidianQueueGate.cs`** |
| 4 | **`Busy < Slots` used as "idle"** | The single most likely wrong turn. Case 1's `1/2 → 2 not 3` assertion exists solely for it |
| 5 | **`Available == false` at boot ⇒ the bar claims "3 of 3 idle"** before the service has ever published | `IdleLineCount()` returns 0 when `!Available`, asserted by case 1, traced by `FlowTrace.Once` |
| 6 | **The numeral becomes a per-frame string build + a per-frame trace line** | Case 7, plus the model is already edge-triggered (`HudActionBarModel.cs:36-41`) |
| 7 | `QueueRailView` is **shared** — a change lands on the Manage screen and the (dormant) chip at once | Deliberate and desirable; capture **every** rail host (§6.4) |
| 7b | **An oracle case is written on the wrong side of the assembly wall** — `DeNelle.EditorRegression` cannot see `DeNelle.HUD`, so a "behavioural" `HudKitController` case will not compile | §5's assembly box: Core = behavioural, HUD = source-lint (`HudActionBarRegression.cs:17-21`) |
| 7c | **A fourth local busy/free computation is added** beside `BuildTimerService.cs:1434-1447`, `QueueRailView.cs:276-277` and `ManageScreenVM.cs:324-335` | §2 lists all three by line; the plan re-points #2 at the new helper rather than adding to the pile |
| 8 | **`DataRegression.cs` collides with WO-1038's registration line** | One agent owns that file; the other hands over its line as text (CLAUDE.md §11) |
| 9 | A "session-complete" state that is **wrong** is worse than none — telling a player they are done while a line is free | Its predicate is stricter than the numeral's: `IdleLineCount()==0` **AND** every `BusyOf(c)==SlotsOf(c)` |

---

## 8. WHAT COULD NOT BE VERIFIED

- **Nothing was executed.** The Unity lock was held by a running APK build, so there is no
  `COMPILE_GATE_OK`, no regression run and no capture behind this plan. ⚠ §12: this is a **static
  read** — it LOCATES the seams; the greyscale numbers in §0b are computed from the literals at
  `QueueRailView.cs:421-423`, **not measured off a PNG**. **The first implementation step should be to
  run case 3 against the untouched tree and confirm it goes red** — that converts §0b from an
  inference into captured data before a line is edited.
- **`ManageScreenPanel` was not read in full** (`Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs`)
  — its registration (`:130-136`), `Open` (`:157-181`), `Close` (`:184-203`), `OnQueueChanged`
  (`:205-210`) and `BuildTabs` (`:479-505`) are located, but the exact insertion point for the "you're
  set" state (§3c) needs one read before editing.
- **Whether `HudActionBarModel.Tick()` already reads `ObsidianQueueGate.Status.Version`** or only
  mentions the gate in its header (`:37`) — if it already does, surface A is a few lines; if not, add
  the version compare alongside the existing polls.
- **The `Manage` face's label TMP element** — the face is built at `HudKitController.cs:613-616` via
  `ElarionUiKit.BuildObsidianButton(pool, "Manage", …, OnManageAction)`, but the handle the View would
  use to *re-set* that label after construction was not identified. ⚠ **If `BuildObsidianButton`
  returns no label handle, surface A needs a small kit-side accessor — plan for that, do not reach in
  with `GetComponentInChildren<TMP_Text>()`**, which the conformance gate's strong-smell regexes
  (`UiObsidianConformanceRegression.cs:82-90`) exist to catch. Canon §7 pins the widget id
  `upgradeButton` and its `hud-areas.json` row: **both must stay untouched.**
- **No PNG was measured.** §0b's luma numbers are computed from source literals only (§8, first bullet).
- **`git` was off-limits**, so whether these files are clean at HEAD or carry another seat's uncommitted
  edits is unknown (memory `other-seats-commit-ungated`). Re-check before editing.
