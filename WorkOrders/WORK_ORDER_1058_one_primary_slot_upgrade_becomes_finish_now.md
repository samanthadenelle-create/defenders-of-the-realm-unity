**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.

# WORK ORDER 1058 — One primary slot per row: "Upgrade" becomes "Finish Now" in place

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1058 -> 1059 in the SAME edit)
**Assigned:** CLI implements. UI authored the layout; UI writes no `.cs` (CLAUDE.md §2).
**Lane:** UI presentation / queue screen
**Class:** UX IMPROVEMENT — **and it fixes a live hazard that was not the reported problem.**
**Screen:** `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` (`PanelId.Manage`, WO-911).
**Evidence:** owner screenshots 2026-08-22 (Manage / Defense tab, Arcane Spire L2).

**OWNER REQUEST 2026-08-22:** *"instead of adding the button to complete with crystals could we just
reuse the same button and make it finish now so you don't have to move?"*
**OWNER RULING 2026-08-22 (double-tap is a FEATURE):** *"they can double click and be done"* —
*"if in hurry and burn the crystals."*

---

## 0. One-line truth

**Today the second tap in that spot CANCELS the job you just started.** The owner asked for this to
save a finger movement; the arithmetic says it also removes a destructive collision that is live
right now.

---

## 1. ⛔ THE HAZARD — read the two x-ranges

| Row state | Control | x range | Source |
|---|---|---|---|
| Upgrade candidate | **`Upgrade`** (Yellow/Gray) | **0.84 – 0.98** | `ManageScreenPanel.cs:968-973` (`AddBrowseRow`) |

> ### ⚠ CITATIONS CORRECTED 2026-08-22 — READ THIS BEFORE "VERIFYING" §1
> The row above previously cited `ManageScreenPanel.cs:725-727`, "green", and band `0.76–0.98`.
> All three were wrong at HEAD. `:719-729` is `AddActionNoteRow`, whose only live caller is the
> **Repair** offer — not the upgrade CTA. The real upgrade CTA is `AddBrowseRow` at `:968-973`,
> band **0.84–0.98**, coloured Yellow/Gray. `Cancel` is `:856-860`, band `0.885–0.98`, Red.
> An implementer checking the ticket against those old numbers finds a function that has
> nothing to do with the defect and concludes the report is bogus.
>
> **The x-overlap is real but is NOT the mechanism.** `Upgrade` and `Cancel` live in DIFFERENT
> SECTIONS — `Cancel` in "IN QUEUE", `Upgrade` in "UPGRADES" below it — and the list re-renders
> wholesale on `QueueChanged`. So they are never the same row. The plausible mechanism is that
> starting a job INSERTS a queue row in the section above, shifting every browse row DOWN by
> `RowHeightPx`, so the second tap lands on whatever slid under the finger — most often the NEXT
> structure's `Upgrade` (an unintended second purchase), and only `Cancel` when scroll position
> happens to put a queue row there.
>
> ⛔ **THE FORBIDDEN FIX.** The tempting way to make a "Finish Now" face exist is to guarantee the
> job is RUNNING rather than queued, i.e. raise `BuildTimerConfig.freeBuildSlots`. Do not.
> `queueDepthPerLine = 5` and `freeBuildSlots = 2` are DIFFERENT AXES and the config says so in
> its own comment block: "DO NOT implement the cap of 5 by raising freeBuildSlots." Also do not
> add a confirm or lockout on the second tap — §2.2 forbids it.
>
> This is a PRESENTATION ticket in ONE file (`AddQueueRow` `:808-871` + `AddBrowseRow`
> `:952-974`). The VM is read-only; slot accounting is not involved at all.
>
> ✅ **EVIDENCE SUPPLIED BY THE OWNER 2026-08-22 and now in-repo:**
> `docs/ui-evidence/wo1058/01_before_two_browse_rows.png`
> `docs/ui-evidence/wo1058/02_after_queue_row_inserted.png`
>
> They CONFIRM the shift mechanism above, and they are the reason §1's x-overlap story
> should not be implemented literally:
> * **BEFORE** — two browse rows, `Arcane Spire -> L2` and `-> L3`, each `Ready | Upgrade`.
> * **AFTER one tap** — a QUEUE row is INSERTED AT THE TOP (`Tower Arcane Spire -> L2 ·
>   Building · 23s left · 60% done · Refund 480 iron, 1500 crystals`) carrying `Finish
>   10 crystals` and `Cancel`. The browse list is pushed DOWN by that row's height. The
>   second tap therefore lands on whatever slid under the finger.
>
> ⚠ **TWO FURTHER DEFECTS VISIBLE IN THE SAME FRAME, NOT IN THE ORIGINAL TICKET — fix them
> in this pass or they will be re-reported as new bugs:**
> 1. **Text is CLIPPED.** The queue row's title `Tower Arcane Spire -> L2` is cut off along
>    its top edge, and the bottom-right CTA renders as `Upgrad...`. Measure against the real
>    box; do not shrink the control to compensate (`MinTouchPx = 112`).
> 2. **Content OVERFLOWS its band and runs under the `Close` button** — `Ready` and the last
>    row are overlapped by the footer. The list needs its own clipped viewport with the
>    footer reserved, not a taller stack (vertical is the scarce axis).
| Running / queued job | **`Cancel`** (red) | **0.885 – 0.98** | `:856-858` |
| Running / queued job | `Finish` (yellow) | 0.455 – 0.655 | `:822-824` |

**`Cancel` (0.885–0.98) sits entirely INSIDE where `Upgrade` (0.76–0.98) just was.**

So the exact gesture the owner is asking for — tap, tap again — currently does this:

1. Tap `Upgrade` at the right edge. The job queues.
2. The row re-renders. That same right edge is now `Cancel`.
3. Tap again → **the job you just started is cancelled.**

Meanwhile `Finish` is nowhere near the finger — it is off at 0.455, a third of the row away.

**The current layout puts a destructive control exactly where the positive one was, and puts the
positive follow-up as far from the finger as the row allows.** The owner's request fixes both with
one change.

---

## 2. The design — ONE primary slot, fixed position, never destructive

### 2.1 The rule

**Every row has exactly one PRIMARY slot, right-aligned at `x 0.76 – 0.98`, and it always holds the
action the player wants.** Its label changes with row state; its position never does.

| Row state | Primary slot face | Sub-line | Colour |
|---|---|---|---|
| Upgrade candidate, affordable | **`Upgrade`** | the resource cost | Green |
| Upgrade candidate, unaffordable | `Upgrade` | the cost | Gray |
| Running / queued job | **`Finish Now`** | `10 crystals` (`r.FinishCostText`) | Yellow |
| Running / queued, unaffordable | `Finish Now` | the cost | Gray |

**The slot is never `Cancel`.** That is the whole invariant, and it is what makes a fast double-tap
safe to sanction.

Reuse `BuildTwoLineCta` exactly as it stands (`:822`) — verb on top, cost underneath in a smaller
font. That two-line shape was itself an owner felt-test outcome and it is why the second tap is
never blind: **the price is on the face before the finger arrives.** No confirm dialog is needed
because the cost was already read.

### 2.2 Double-tap is SANCTIONED — no lockout, no confirmation

Per the owner's ruling. Tap to start, tap again to finish, crystals spent, done.

⛔ **Do NOT add an input lockout, a cooldown, a confirm dialog, or an "are you sure" on the second
tap.** The fast path is the feature. A seat that adds friction here has undone the ticket.

The one thing that stays is the **cost printed on the face** (§2.1) — that is not friction, it costs
zero taps, and it is what makes "burn the crystals" a choice rather than a surprise.

⚠ **One honest note, stated once and not re-litigated:** `FinishPrice` scales with time remaining, so
the same gesture costs 10 crystals on a 23-second job and a great deal more on a 55-minute one. The
face carries the number in both cases, which is the mitigation. **No further guard is wanted.**

### 2.3 Everything else moves LEFT, away from the finger

The action cluster re-lays out so nothing destructive is adjacent to the primary slot:

| Control | New x range | Note |
|---|---|---|
| (`Ad`) | 0.24 – 0.38 | **Never built today** — `FeatureFlags.RewardedAdSkip` is OFF and the control is *absent*, not disabled (`:833`). Reserve the band; do not construct it. |
| **`Cancel`** | **0.40 – 0.55** | Moves out of the primary slot entirely. Keeps its Red face and its refund line. |
| `Move up` | 0.57 – 0.72 | Was 0.765–0.875, which **collides with the primary slot** — it must move. |
| — gap — | 0.72 – 0.76 | Deliberate separation so no mis-tap crosses into the primary. |
| **PRIMARY** | **0.76 – 0.98** | `Upgrade` / `Finish Now`. Fixed for every row state. |

⚠ **Verify the RESOLVED width of each control, do not assume it.** `ClampMinTouch` must be a
**no-op** — if it fires, the band was authored too small and it will inflate into its neighbour.
That is WO-1056's entire root cause, on the panel next door.

### 2.4 Cancel keeps its refund promise

`Cancel` still states what comes back on its own row (`:865-868`, the third text line). WO-911's
ruling Q1 stands: the refund is **100% of what was paid, flat**, and a pre-v37 job refunds **zero and
says so**. Moving the button does not touch any of that.

---

## 3. What NOT to touch

- **`_vm.FinishNow` / `_vm.Cancel` / `_vm.BumpUp`** and the crystal spend path. Presentation only.
- **`FinishPrice` / `FinishCostText`** — the VM owns both; the View only renders them (`:820`).
- **The `Ad` control's absence.** It is deliberately never constructed while the flag is off. Reserve
  its band; do **not** build it "disabled".
- **The two-line CTA shape.** An owner felt-test outcome — verb over cost. Keep it.
- **The refund text and the 100%-flat rule** (§2.4).
- **`RowCtrlY0` / `RowCtrlY1`.** Only x-ranges change here.

---

## 4. Acceptance

1. The primary slot is at `x 0.76–0.98` in **every** row state, and is **never** `Cancel`.
2. Tapping `Upgrade` then tapping the same spot again **finishes the job** — it does not cancel it.
3. The crystal cost is legible on the primary face **before** the second tap.
4. **No lockout, no cooldown, no confirm dialog** exists on the second tap (§2.2).
5. `Cancel` is reachable, still Red, still states its refund, and is **not adjacent** to the primary.
6. `ClampMinTouch` is a **no-op** on every control in the cluster.
7. Unaffordable states render Gray with the cost still readable.
8. **Greyscale pass** — primary vs Cancel distinguishable without hue (they differ by position,
   label and now separation, not only Red vs Green).
9. `COMPILE_GATE_OK`; brace-check every `.cs`; before/after screenshots opened, against the owner's
   2026-08-22 pair.

---

## 5. Noted, not folded — the same layout class as WO-1056

The owner's second screenshot shows this panel clipping: *"Tower Arcane Spire -> L2"* cut off at the
top of its row, *"Ready"* and *"Upgrade"* sheared at the bottom of the visible area, and
*"Upgrade queued."* sitting over the frame's bottom-left ornament.

That is the **WO-1056 class** (controls and text authored below the touch floor / outside their
band), not this ticket's subject. **Do not fix it here** — it wants the same treatment as the
Armies/Loadouts panel and should be handled with it, so the fix is one pattern rather than three
patches.

## 6. Files

**Edit:** `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` — the row action cluster
(`:806-870`) and the upgrade-candidate row CTA (`:725-727`).

**Read, do not edit:** `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` (`FinishPrice`,
`FinishCostText`, `CanAffordFinish`, `CanCancel`, `RefundText`) ·
`WorkOrders/WORK_ORDER_1056_armies_loadouts_panel_stacks_in_the_scarce_axis.md` (§5).
