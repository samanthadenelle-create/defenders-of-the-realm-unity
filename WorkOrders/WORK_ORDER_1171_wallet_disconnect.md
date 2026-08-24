# WORK ORDER 1171 — Wallet disconnect: a finished mechanism with no way in

**Status:** FIXED 2026-08-24 (`d1b6239ca`) — the dev-panel disconnect surface shipped; **awaiting owner felt-verify** (§13: the PO closes, not the CLI). §4 player-facing placement is READY, not yet done. *(Read `DONE (dev-panel surface)` until the 2026-08-24 board reconcile — code landing is not a close.)*

**Minted:** 2026-08-24 (CLI), banner bumped 1171 → 1172 in the same edit.
**Provenance:** owner, 2026-08-24 — *"Offer a disconnect (better), Then reconnect"* — raised while
looking for a way to re-authorise a wallet without uninstalling the app.

---

## 1. It was already ruled, and half-built

⭐ **The owner ruled this on 2026-08-17**, quoted verbatim in `WalletSkinBootstrap.cs`:

> *"yes it should auto connect, there is a menu option to reset"*

**The auto-connect half shipped.** `TryAutoResumeAsync` silently reconnects at boot whenever
`MwaSessionStore.HasStoredSession`. **The reset half never did.**

⛔ **`WalletService.Disconnect()` is COMPLETE** — awaits `_provider.Disconnect()` (which calls
`MwaSessionStore.Clear("explicit disconnect")`), unregisters the signer via
`CoreServices.UnregisterWalletSigner`, sets status, and publishes `PublishWalletDisconnected()` so
every label falls back to "Connect Wallet". **It was called by nothing.** A whole working mechanism
with no door.

⚠ Same shape as the rest of this session: decided, built, never wired. `PublishWalletDisconnected`
existed for the same reason and had no caller either.

## 2. Why it is a real defect, not a test convenience

**Auto-resume without reset is a trap.** A player who connects the wrong wallet is silently
reconnected to it on every cold start, forever, with no way out short of reinstalling the app.
`PiSignInController` makes the corner button **non-interactive once connected** — deliberately
("There is no disconnect surface here and one is NOT invented") — so there was no exit anywhere.

**Reset is what makes auto-connect safe to ship.** The owner's August ruling said both halves in one
breath; only one arrived.

## 3. What shipped

**The Core seam** — `CurrencySkinResolver.WalletDisconnectRequested` + `RequestWalletDisconnect()`,
an exact mirror of the existing `WalletConnectRequested` / `RequestWalletConnect` pair.
⛔ **Required, not stylistic:** `DeNelle.HUD` may not reference `DeNelle.Wallet` (CLAUDE.md §5), so
the button cannot call `WalletService` directly. Never "simplify" it into a direct call.

**The subscriber** — `WalletSkinBootstrap.OnDisconnectRequested` → `DisconnectAsync()`:
- refuses mid-connect (would race the association and could clear a session about to be written)
- no `WalletService` instance = already disconnected; says so plainly and publishes, rather than failing
- does NOT re-publish the disconnected state — `WalletService.Disconnect` already does, in its
  `finally`. One owner per fact.

**The surface** — `AdminOverlay` (Settings → DevTools) "Disconnect Wallet (CHKK…sfkC)", **two-tap
confirm** with a 4-second arm window, matching the full-reset button beside it. Dropping the sealed
session is recoverable but not free — the next launch requires re-authorising in the wallet app — so
a mis-tap must not do it.

**Never silent:** `RequestWalletDisconnect` with no subscriber emits `FlowTrace.Warn`. A dead reset
button that merely does nothing is how a player concludes the wallet cannot be changed at all.

## 4. Still open — the PLAYER-facing placement

The dev panel is reachable but it is a dev surface. A player who wants to switch wallets still has no
route.

⚠ **And the connect side has the same shape of bug**: `PiSignInController.UpdateButtonVisibility`
gates the corner button on `!IsSignedIn && (scene == "Title" || scene == "HeroSelect")`. So the
Night Market can say *"Wallet identity bound — authorize to purchase"* while offering **no way to
authorize from where the player is standing**. Connect and disconnect both need a home on a real
settings screen.

## 5. Acceptance

- [x] `COMPILE_GATE_OK` · `REGRESSION_OK 271/271 suites`
- [x] Disconnect reachable in-session without uninstalling
- [x] Next cold start does NOT auto-resume after a disconnect (the documented other direction of
      `TryAutoResumeAsync`'s gate)
- [ ] Player-facing connect/disconnect surface (§4)
- [ ] Owner felt-test: disconnect → relaunch → asked to Connect → reconnect → store prices
