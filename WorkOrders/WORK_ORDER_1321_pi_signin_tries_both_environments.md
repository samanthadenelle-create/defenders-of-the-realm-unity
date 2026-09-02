# WORK ORDER 1321 — Pi sign-in tries the declared environment, then the other one

**Status:** DONE (implemented; the capture that ends the ambiguity is still owed)
**Silo:** Web / Pi
**Minted:** 2026-09-02 (CLI) on the owner's explicit ruling: *"Build it to try both"*.

## The problem this solves — contradictory evidence about ONE boolean

Sandbox and mainnet are DIFFERENT Pi environments. An app is registered in exactly one, and
initialising against the wrong one fails authentication **with no message naming the cause**. The
evidence about which one this app uses does not agree with itself:

| source | says | weight |
|---|---|---|
| captured `web_trace`, 2026-09-01, x3 sessions | `PiInit(sandbox=True)` -> `Signed in as samanthadenelle` | **a TESTNET init that AUTHENTICATED** |
| the owner, asked directly 2026-09-02 | **Mainnet / production** | WO-1317 shipped `sandbox=false` on this |
| the owner, later the same day | *"i read something about this is on testnet"* | reopened it |

⚠ **I should have weighted the captured trace against the answer at the time and flagged the
conflict. I did not** — I took the answer and shipped. If the app is testnet, WO-1317's flip made
authentication WORSE, not better. That is recorded here rather than quietly corrected.

## What was built

`PiSignInController.SignInAsync` now runs `TryInitAndAuthenticate(sandbox)`; if that round fails for
any reason, it **retries once on `!sandbox`**. Both attempts trace their environment by name. On a
successful fallback it emits, loudly:

```
ENVIRONMENT MISMATCH PROVEN: sign-in succeeded on <X> after failing on <Y>.
Set PiEnvironment.Sandbox to <value>.
```

So **one real Pi Browser session ends the question for good**, and the player signs in either way
instead of hitting a dead button while we guess.

Every SDK await stays bounded exactly as before (the 2026-07-01 root cause: the Pi SDK resolves only
through a JS promise callback, so an unbounded await on a dismissed consent popup hangs FOREVER and
leaves the button dead). A timeout is now a FAILED ATTEMPT rather than a dead screen — which is also
what lets the caller try the other environment.

The `username`-only scope and its WO-1318 rationale moved into the helper **unchanged**. Do not widen
it here; `payments` is still requested lazily at purchase time.

## ⛔ This is a diagnostic, NOT a licence to stop knowing

The fallback makes sign-in resilient, which also makes it easy to never find out the truth. Once a
capture proves the environment, **set `PiEnvironment.Sandbox` accordingly**. The fallback then costs
nothing, because the first attempt always wins.

Do NOT let this become the permanent answer to "which environment are we on".

## Acceptance criteria

1. A Pi Browser session signs in successfully regardless of which environment the app is registered in.
2. The trace names the environment on every attempt, and a fallback success prints the MISMATCH line.
3. When the declared environment is correct, exactly ONE init+auth round runs (no wasted second call).
4. `PiEnvironment.Sandbox` is updated to the proven value once a capture exists, and this WO is
   referenced in that commit.

## What NOT to touch

- ⛔ Do not add `payments` to the sign-in scope (WO-1318 AC 6 — it turns a dismissed consent into a
  failed sign-in for an existing player).
- ⛔ Do not remove the timeouts. They are the 2026-07-01 dead-button root cause.
- ⛔ Do not make the environment a runtime toggle or PlayerPrefs value — a device could carry testnet
  into a mainnet session, which is WO-1317's defect with extra steps.
