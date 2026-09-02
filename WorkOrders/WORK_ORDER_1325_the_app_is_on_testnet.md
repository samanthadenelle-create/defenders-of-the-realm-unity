# WORK ORDER 1325 — The app is registered on Pi TESTNET; ship builds must init sandbox

**Status:** DONE
**Silo:** Web / Pi
**Minted:** 2026-09-02 (CLI) on the owner's direct statement.
**Severity:** P0 — a mainnet-inited client cannot complete the Testnet transaction the Developer
Portal checklist requires.

## Owner, verbatim

> **"im on testnet"** — and, on the portal checklist, *"all except last one"*, the last one being
> **make a purchase**.

The Developer Portal badges the app **`Testnet`**. The one outstanding checklist item is
"process a transaction on your app", and it must settle **on Testnet**.

## What was wrong, and why it looked right

`PiEnvironment.Sandbox` was build-driven — sandbox in Editor/dev builds, **mainnet in a ship build**
(WO-1317). That is now WRONG in every build, and the reason it survived scrutiny is worth recording:

| evidence | what it seemed to prove | what it actually proved |
|---|---|---|
| Owner answered "Mainnet" to a direct question (2026-09-02) | the app is mainnet | that was the answer to hand; she later corrected it |
| **Nine** captured Pi Browser sessions: `PiInit(sandbox=False)` -> `Signed in as samanthadenelle`, zero failures | the app is mainnet | **only that AUTH works on mainnet** |

⚠ **AUTHENTICATION IS NETWORK-TOLERANT. PAYMENTS ARE NOT.** A Pioneer is the same Pioneer on either
network, so a mainnet `Pi.init` authenticates fine against a Testnet-registered app. A **payment**
created under `sandbox=false` does not settle where the portal is looking.

**THE LESSON, because it cost two reversals in one day:** a green capture is evidence about the
subsystem it came from. Nine clean sign-ins were treated as settling the environment question for the
whole app. They settled it for auth only. Do not generalise one subsystem's success into a claim about
another.

## The fix

`PiEnvironment.Sandbox` is now a flat `true` with the history recorded in-code. Not build-driven:
the app IS Testnet, so a ship build must be Testnet too. `PiSignInController` and
`PiBrowserPaymentProvider` both read this one constant (WO-1318), so sign-in and payments cannot
disagree.

WO-1321's opposite-environment fallback stays. It is now belt-and-braces — the first attempt wins —
and it is what would have caught this in one session instead of two reversals.

## When the app moves to Mainnet

Pi documents the network as **fixed at registration**: *"once you register the app, this option cannot
be changed"*
(https://github.com/pi-apps/pi-platform-docs/blob/master/developer_portal.md). Moving to mainnet means
a **new portal project and a new API key**, not an edit to this constant alone.

⛔ **Do not flip this line ahead of that migration.** Flip it in the same commit, referencing the new
project. See `docs/reference/PI_AD_NETWORK_APPROVAL.md`.

## Acceptance criteria

1. A ship WebGL build inits `PiInit(sandbox=True)`. Proven by a captured trace, not by reading code.
2. Sign-in still succeeds (it did on 2026-09-01 under sandbox=True, three captured sessions).
3. A Pi payment can be created and completed on Testnet, ticking the portal's last checklist item.
4. Sign-in and payments read the SAME constant — no second copy of the boolean.

## What NOT to touch

- ⛔ Do not restore the `#if UNITY_EDITOR || DEVELOPMENT_BUILD` split. The app is Testnet in every
  build until it is re-registered.
- ⛔ Do not make the environment a runtime toggle or PlayerPrefs value. A device could carry one
  network into a session for the other — WO-1317's defect with extra steps.
- ⛔ Do not remove WO-1321's fallback. It is the mechanism that survives the next time this is wrong.
