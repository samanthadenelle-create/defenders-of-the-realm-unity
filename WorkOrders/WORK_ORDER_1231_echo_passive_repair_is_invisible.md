# WORK ORDER 1231 - Echo passive repair is completely invisible, and it SPENDS the player's materials

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Silo:** UI / Echoes
**Severity:** P1. A system that debits the player's wallet with no notification is worse than a
missing feature - the player experiences it as resources vanishing.
**Origin:** Owner felt-test, 2026-08-26. Owner verbatim: ***"where do i change them or is that
something passive"*** -> ***"if its passive we should somehow let them know"***.

---

## The state, read at source

**Repair is passive and there is nowhere to change it.** Owner ruling WO-1108 (2026-08-16):
*"the number of pets that we have just passively takes towards healing."* The WO-811 "Repair
structures" picker chip is RETIRED - `EchoAssignments.cs:232` refuses the call in as many words:

```
AssignRepair(echo=N) -- RETIRED (WO-1108): repair is passive across every owned Echo
```

`EchoBonusCalculator.RepairFractionsPerSecond()` sums **every OWNED Echo** (count x level).
`echoes-balance.json` -> `repairFractionPerHour: 0.35`. Adding an Echo raises the mend rate with no
assignment change. **This is all correct and is NOT the defect.**

## The defect: none of it reaches the player

A sweep of `Assets/_Modules` for player-facing strings mentioning Echo repair / passive mending
returns **ZERO**. Every hit is a `FlowTrace` line, an editor-only `[Tooltip]`, or a log message. The
player is never told:

- that structures mend on their own at all,
- that **owning more Echoes makes it faster** - which is a monetisation-relevant fact and currently
  a total secret,
- what the current rate is,
- or that it is happening right now.

## THE ONE THAT MATTERS MOST - it spends materials silently

`EchoRepairService.cs:258-259`:

```
{reason}: cannot afford {cost} for '{name}' -- waiting for materials
(repair SPENDS; never free hitpoints).
```

**Passive repair debits wood and iron from the player's wallet**, and when the wallet is short it
**stalls silently**. So today the player sees materials leave with no cause shown, and sees repair
not happening with no reason shown. Both halves are invisible.

Offline accrual too: `ClaimOffline` banks an `'echo-repair'` share of the away window
(`EchoRepairService.cs:202-224`) and applies it on return - so a returning player can find materials
already spent before they touched anything.

## Required

1. **Tell the player the system exists**, where they already look at Echoes (the Echo card / roster).
   State the effect in the player's terms, not the internal fraction: what it does, and that more
   Echoes = faster.
2. **Attribute the spend.** When passive repair consumes materials, the player must be able to find
   out that it did. Design the surface - a resource-log line, a claim-summary row, an entry in the
   offline-return summary - **but it must not be a toast per repair**; that would spam.
3. **Surface the stall.** "Waiting for materials" is an actionable state and is currently only in a
   FlowTrace. If Echoes have stopped mending because the player is broke, say so where they will see
   it.
4. **The offline-return summary should account for it** alongside the other offline consumers, since
   that is the moment the player is already reading a "while you were away" report.

## Constraints

- **Do NOT re-introduce a repair ASSIGNMENT or picker chip.** Owner ruling WO-1108, binding. Repair is
  passive and count-driven. This ticket is about COMMUNICATION only.
- **Do NOT change `repairFractionPerHour` (0.35)**, the count x level math, or the spend behaviour.
  Whether passive repair should spend at all is a separate OWNER RULING - flag it, do not act on it.
- **The owner is red/green colourblind.** No meaning by hue alone; greyscale check is the gate.
- **ASCII-only TMP strings. Code-built uGUI via `ElarionUiKit`. NO UXML** - project law.
- `MinTouchPx = 112` on anything tappable, without creating a new overlap.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. **A DEVICE SCREENSHOT at 2670x1200, opened and looked at**, showing the Echo surface with the
   passive-repair explanation present, plus one showing the spend attribution after a repair has
   actually fired. `UI_CAPTURE_OK` alone is NOT acceptance.
3. A greyscale check of both captures.
4. A regression that FAILS on today's tree: assert a player-facing string exists for the passive
   mend and for the spend attribution. Prove it RED first (WO-1138).
5. Owner felt-verifies and CLOSES.

## OWNER RULING NEEDED (flag, do not act)

Should passive Echo repair **spend materials at all**? It currently does. The alternative - free
mending, with the Echo count as the only pace knob - is simpler to communicate and arguably what
"passive" implies to a player. This is an economy decision and it is the owner's.


---

## UI SEAT DELIVERABLE (2026-08-26) - APPROVED SURFACES + OWNER ECONOMY RULING

**Owner approved the design this session (explicit choice via the UI seat's question).**
**Mockup:** `WorkOrders/WORK_ORDER_1231_mockup_2670x1200.png`.

The two surfaces (both EXISTING surfaces, no new HUD element, per this WO's fence):

1. **Echo card - PASSIVE MENDING block** (under the existing harvest line):
   - Explainer: "Your Echoes mend the town's walls on their own. Every Echo you wake mends
     faster - no assignment needed." (ASCII; final copy may route via canon-strings.)
   - Live rate line: "Mend rate now: +X% wall health / hour" - bound to
     `EchoBonusCalculator.RepairFractionsPerSecond()`, never hardcoded.
   - Spend line: "Mending uses Wood + Iron as it works."
   - **Stall chip**: framed word-chip "PAUSED - waiting for materials (<resource>)" - shown ONLY
     while stalled, hidden otherwise. Word + frame, never hue alone; greyscale-safe.
2. **While-you-were-away summary** - the spend-attribution home (option chosen: offline-return
   summary entry; NEVER a per-repair toast):
   - "Echoes mended the walls  +X% wall health"
   - "  spent while mending  -X Wood, -X Iron"
   - "Mending paused Xh - ran out of <resource>" (only when it happened)
   All values from the live claim math (`EchoRepairService.ClaimOffline`), zero invented numbers.

**OWNER RULED (2026-08-26): the material spend STAYS.** Mending remains a Wood+Iron sink; the
defect was invisibility, not the economy. The flagged question in this WO is CLOSED.

CLI scope unchanged: wire strings/values through the two surfaces, RED-first regression, device
screenshot at 2670x1200 opened, greyscale check, owner felt-verify closes.
