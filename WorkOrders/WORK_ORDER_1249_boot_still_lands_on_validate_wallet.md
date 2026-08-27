# WORK ORDER 1249 - Boot still lands on the "validate wallet" screen

**Status:** READY TO IMPLEMENT - ⚠ one owner line needed on intended behaviour (see below)
**Silo:** Wallet / boot flow
**Severity:** P1 for testing throughput. It is the first thing between the owner and 191 validation
items, and it is on the boot path for every player.
**Origin:** Owner, on device, 2026-08-27: *"still lands at validate wallet screen"*.

---

## What was reported

On the tester APK built 2026-08-27 11:47 (`TESTER_BUILD`, commit `fffa4ea9c`), boot lands on a
**validate wallet** screen. The word **"still"** is load-bearing: the owner expected this to have
been addressed already, so **find the prior attempt before writing new code.** A second fix layered
on an unexamined first one is how this comes back a third time.

## ⛔ OWNER RULING 2026-08-27 - NO TESTER BYPASS. THE BUILD MUST BEHAVE LIKE PRODUCTION.

Her words: *"i dont know i need it to act as it would in prod as its the only way to really test
it"*.

**This overrules the recommendation this ticket originally carried**, which was to skip wallet
validation on a `TESTER_BUILD` boot so a tester could reach the game faster. That was the wrong
trade and she named why: **a build that behaves differently from production cannot validate
production.** A bypass would have made 191 felt-tests cheaper and every one of them less meaningful,
and the wallet path is precisely the kind of first-run flow that only ever breaks for real players.

⭐ **THE PRINCIPLE, WHICH IS BIGGER THAN THIS TICKET:** do not fix a testing inconvenience by making
the tester build diverge in BEHAVIOUR. `TESTER_BUILD` is for **tooling the owner deliberately
invokes** - the AdminOverlay, the resource grant, the F8 flag chip. It is **not** a licence to change
what the game does on its own. Apply that distinction to every future "just skip it for testers"
idea.

**So the question this ticket must answer is no longer "how do we skip it" - it is "is this screen
correct?"**
- If landing on wallet validation is **correct** product behaviour, this ticket closes as
  works-as-intended and the owner walks through it like a player would.
- If it is a **defect** (it appears when it should not, or it reappears after being satisfied), fix
  it **for everyone**, in the real flow.

The word **"still"** in her report leans toward defect - she expected it addressed already. Find the
prior attempt before writing anything new.

## Instrument before editing (CLAUDE.md section 12)

This is a boot-order question, and boot-order questions are exactly where static reading misleads.
**Do not theorise about which check fires.** Put `FlowTrace.Step` at each boot decision point, run
it, and let the trace name the step that routes to the wallet screen.

⚠ **`adb logcat -d` after the fact will NOT contain the boot window.** The default 256 KiB ring plus
the `[Flow:Offset]` firehose evicts it, and a post-hoc read makes the feature look like it never ran.
Start the capture BEFORE launching, or raise the buffer.

Device: `SM02G4061955851`. Package: `com.denellestudios.echoesofelarion`.

## Required

1. The boot decision that routes to the wallet screen, named from a captured trace line - not from a
   code read.
2. Whatever the owner rules above, implemented at that seam.
3. Whatever the prior attempt was ("still"), found and reconciled - do not stack a second fix on it.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression pinning the boot route **as production behaves it** - there is no tester variant to
   assert, per the ruling above. Prove RED first (WO-1138).
3. ⛔ Never render or log a wallet address. Player id is enough.
4. Owner felt-verifies on device by booting straight into the game.

## What NOT to touch

- ⛔ The wallet auth rail itself (`api/auth/*`, signing, `BoundWallet` semantics). This is about the
  BOOT ROUTE, not about how a wallet is verified.
- ⛔ Do not weaken wallet validation for real players to make testing easier.
