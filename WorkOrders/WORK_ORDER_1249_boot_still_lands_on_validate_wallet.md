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

## ⚠ THE ONE THING TO SETTLE FIRST - DO NOT GUESS IT

**Is landing on wallet validation correct behaviour that should be SKIPPABLE, or is it a defect that
should not happen at all?** Those need different fixes and the difference is not inferable:

- The wallet is **identity / cloud-save**, keyed by `BoundWallet` — so a wallet step on first run may
  be intended product behaviour.
- But the owner is reporting it as something she keeps hitting, which reads as unwanted.

⭐ **Recommended shape, offered so the owner can answer in one word:** wallet validation stays for a
real player, and a `TESTER_BUILD` boot **skips or defers** it, because a tester needs to reach the
game 191 times. `FeatureFlags.IsTesterBuild` already exists for exactly this - owner ruling
2026-08-24: *"if it's a dev build then we can just leave the flag on because it's only going to the
tester. It's not going to the Solana store."*

⛔ **The define is OPT-IN and that direction must never be inverted.** Its ABSENCE means store-safe,
so a store build cannot skip wallet validation by forgetting a flag - only by explicitly adding one.

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
2. A regression pinning the boot route for both cases: a tester build reaches the game, and a
   NON-tester build still validates the wallet. Prove RED first (WO-1138) - the store-safe direction
   is the one that must not silently regress.
3. ⛔ Never render or log a wallet address. Player id is enough.
4. Owner felt-verifies on device by booting straight into the game.

## What NOT to touch

- ⛔ The wallet auth rail itself (`api/auth/*`, signing, `BoundWallet` semantics). This is about the
  BOOT ROUTE, not about how a wallet is verified.
- ⛔ Do not weaken wallet validation for real players to make testing easier.
