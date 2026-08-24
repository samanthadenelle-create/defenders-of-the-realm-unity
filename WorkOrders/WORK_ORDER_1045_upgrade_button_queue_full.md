# WORK ORDER 1045 — Queue full = a dead upgrade button. Disable it, say why, offer the slot.

**Status:** CLOSED 2026-08-24 — owner felt-tested and closed ("tested closed").
⚠ **STATUS WAS STALE FOR TWO DAYS.** The work SHIPPED in `c1e9636f2` ("feat(upgrade): say WHY the
button is dead, and show what the upgrade actually buys", 2026-08-17 10:28) but that commit did not
flip this line, so `BOARD.html` — which is DERIVED from it — kept rendering WO-1045 as Ready and the
owner re-raised it on 2026-08-19 as an untouched "simple fix". Line corrected 2026-08-19 from a
source verification of the current tree (see §9); **no code was changed to close it.** This is the
CLAUDE.md §2 rule ("the Status line is flipped in the SAME COMMIT as the work") failing in practice.
**Minted:** 2026-08-17 (UI seat) — provenance stack bumped 1045 → 1046 in the same edit
⚠ **Renumbered from 1043 on mint day.** I minted 1043 from a stale banner read while another seat had
already consumed 1043 (dungeon re-bake) and 1044 (biome identity) in the same sweep. Theirs were
first-on-disk-and-referenced, so they win per CLAUDE.md §2 and this moved to 1045. **No 1043 content of
mine survives** — if any doc references "WO-1043 upgrade button", it means this file.
**Lane:** Building upgrade panel + queue state. ⚠ Same surface family as **WO-1027** — see §5.
**Provenance:** owner 2026-08-17, verbatim: *"if a builder has hit the limit of capacity can we
recommend buying a builder or wait for the queue to have a place, right now just will not click, even
maybe grey out till a slot is open in queue"*, with the Archer Tower Enhancements screenshot.

---

## 1. The defect

The player can afford the upgrade — the screenshot shows **49k wood / 49.5k iron** against a cost of
**108 Wood / 48 Iron** — taps **"Upgrade to Level 2"**, and **nothing happens.** No disabled state, no
message, no explanation. The button looks fully enabled and is inert.

A tap that silently does nothing is the worst available outcome: the player cannot tell whether the
game is broken, whether they mis-tapped, or whether they are missing a rule. **Affordable-but-blocked
is invisible.**

## 2. ★ The codebase ALREADY NAMES THIS AS THE BUG — and already solved it one layer down

`BuildTimerService.TryBuySlot` (`:301-315`) carries this comment verbatim:

> `// STEP ONE failed. Say WHAT unlocks it — an unexplained locked button is the bug.`

And it already does the work:

- returns a **player-readable ASCII failure string** — e.g. *"Locked. Awaken a 3rd Echo to unlock extra
  queue slots."* / *"Locked. You have used all N slot(s) your Echoes unlock - awaken another Echo."*
- its own doc says state is **"carried by TEXT (the owner is red/green colourblind)"** — the
  colourblind law is already satisfied at the source
- the broke case is prefixed with **`InsufficientCrystalsPrefix`** *"so the caller can route to the
  crystal store"*

**So the reason text, the Echo gate, the crystal price and the store-routing hook all exist.** The
upgrade button simply never consumes them. ⚠ **Do not write new reason strings** — surface these.

## 3. What blocks the tap (the two axes — do not conflate them)

`BuildTimerConfig` keeps these deliberately separate, with a comment forbidding the merge:

| axis | value | meaning |
|---|---|---|
| `freeBuildSlots` | **2** | how many jobs run **AT ONCE** (concurrency) |
| `queueDepthPerLine` | **5** | how many may be **LINED UP** (depth) |

⚠ *"DO NOT implement the cap of 5 by raising `freeBuildSlots`"* — the config says so explicitly, and
canon §8 repeats it. **The message must name which limit was hit**, because the player's remedy
differs: a full *depth* means wait or finish something; exhausted *concurrency* means buy a slot.

## 4. The change

**Three states, in the owner's own order — grey out, explain, offer.**

1. **Disable the button when the action cannot succeed.** Visibly disabled, not merely inert. ⚠ Must
   read as disabled in **greyscale** — dimming alone on a gold plate is weak; carry it with the label
   too.
2. **Say why, inline.** Surface `TryBuySlot`'s existing reason text, or the depth-cap equivalent. ⚠ The
   message must distinguish *"all builders busy"* from *"queue is full"* (§3) — they are different
   problems with different fixes.
3. **Offer the remedy the player actually has.**
   - Slots buyable → offer it, routed through **`TryBuySlot`** (never `GrantSlot` — it is `[Obsolete]`
     for player-facing use precisely because it skips the Echo gate and the crystal charge)
   - Blocked on crystals → the `InsufficientCrystalsPrefix` hook already exists for store routing.
     ⚠ Anything reaching the store is subject to **WO-1037 §2 / WO-931** — display-only, no purchase
     completes
   - Blocked on Echoes → say which Echo count unlocks the next slot; that is a real goal, not a wall
4. **Re-enable automatically** when a slot frees. ⚠ Do not require the player to close and reopen the
   panel — a stale disabled button is the same bug wearing a different hat.

## 5. ⚠ Sequence with WO-1027 — this is the same surface's other face

**WO-1027** (session shape / builder ache) covers the **idle** side: *"is anything of mine idle?"*
This covers the **full** side: *"why can't I start this?"* Both answer one question — **what is my
queue doing and what can I do about it** — and both touch the queue-state read.

**They should be designed together and land in a deliberate order**, or the player gets two different
visual languages for one system. ⚠ Reuse WO-1027's chosen idle treatment (the Builders chip + peek
rail) as the vocabulary here rather than inventing a second one.

## 6. Do NOT

- Do not raise `freeBuildSlots` to "fix" a depth block (§3 — explicitly forbidden)
- Do not change queue mechanics at all; this is communication only
- Do not call `GrantSlot` from any player-facing path (§4.3)
- Do not write new failure copy where `TryBuySlot` already provides it (§2)
- Do not encode the disabled state in hue alone (colourblind law)

## 7. Acceptance criteria

- [ ] With the queue at capacity, the upgrade button is **visibly disabled** — never enabled-and-inert
- [ ] The reason is **stated on screen**, and names *which* limit was hit (concurrency vs depth)
- [ ] Where a slot is purchasable, the offer is reachable and routed via `TryBuySlot`
- [ ] Where it is Echo-gated, the message names the unlock condition
- [ ] The button **re-enables on its own** when a slot frees, with no panel reopen
- [ ] Disabled state and reason legible in **greyscale**; ASCII-only
- [ ] `freeBuildSlots` == 2 and `queueDepthPerLine` == 5 verified unchanged
- [ ] Verified at **2670x1200**, the Seeker's real surface

## 8. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Fill the queue, open the panel, capture: **disabled + reason + offer**. Then free a slot and confirm
   live re-enable
3. `UI_CAPTURE_OK` — **open the PNGs**, plus a greyscale pass
4. Owner felt-verifies: *"do I understand why I can't build, and do I know what to do about it?"*

---

## 9. Source verification of the shipped fix (2026-08-19, current tree — read at source, not from the commit message)

Every §7 criterion re-checked against the working tree at HEAD. Line numbers are as-read today.

| §7 criterion | verified at | verdict |
|---|---|---|
| Visibly disabled, never enabled-and-inert | `BuildingUpgradePanelMvvm.cs:1188` routes `QueueFull` to `BuildQueueFullBand`; that band calls `BuildLockButton(..., onClick: null)` — the dim lock plate, `Button.interactable` false | PASS |
| Reason on screen, names WHICH limit | `:1262-1320` prints the depth line (`"Queue full - {0} of {1} lined up"`) AND a separate crew line (`"{0} of {1} crews working"`) — the two axes are different numbers on one screen | PASS |
| Offer routed via `TryBuySlot` | `OnBuyQueueSlotTapped` → `_vm.TryBuyQueueSlot()`; `GrantSlot` has **no** player-facing caller (only `BuildTimerService.cs:223` decl, `:237` the `[Obsolete]` alias, `:334` inside `TryBuySlot` itself) | PASS |
| Echo-gate names the unlock condition | `_vm.QueueSlotLockReason` is appended to the dead half whenever no purchase is offered — surfaces `TryBuySlot`'s own sentence, no new copy written (§2/§6 honoured) | PASS |
| Re-enables on its own, no reopen | `Update()` (`:290`) polls `ObsidianQueueGate.Status.Version` and re-`Render()`s on publish; `ContentSignature()` (`:461`) now hashes `BuilderQueueDepth/Limit`, `BuilderCrewsBusy/CrewSlots`, `CanBuyQueueSlot` + `QueueSlotPrice`, so the band repaints as the line drains | PASS (code); needs a live run to prove felt |
| ASCII-only | asserted by the suite's COPY section, which walks the label char-by-char | PASS |
| `freeBuildSlots == 2`, `queueDepthPerLine == 5` unchanged | `BuildTimerConfig.cs:136` and `:159` | PASS — unchanged |
| Verified at 2670x1200 + greyscale | — | **NOT PROVEN — still owed** (see below) |

**Regression coverage:** `Assets/Editor/Regression/UpgradeQueueFullSurfaceRegression.cs`
(`[queue-full-surface]`, marker `QUEUE_FULL_SURFACE_OK`), registered at
`Assets/Editor/Regression/DataRegression.cs:429`, so it runs under `REGRESSION_OK`. Its assertions are
real oracles, not decoration — each names a broken state that would print differently: removing the
`QueueFull` enum member fails §1; a blank or non-ASCII face fails §4; a `QueueFull` label containing
"busy"/"crew" fails the axis-conflation check; making `LineFullMessage` non-public fails §5; and a
re-added `GrantSlot(` in the player path fails the source scan. A live section fills the Builder line
through `ObsidianQueueEngine` and asserts the refusal actually fires.

**STILL OWED before this can be CLOSED** (both need the Unity seat, which was busy on an APK build):
1. `COMPILE_GATE_OK` + `REGRESSION_OK` at HEAD — the suite has not been re-run since the tree moved on.
2. `UI_CAPTURE_OK` at **2670x1200**: queue filled, panel open, showing disabled + reason + offer; then
   free a slot and capture the live re-enable. Plus the greyscale pass. **Code review cannot discharge
   these** — the disabled read and the greyscale contrast are pixel facts, not source facts.
