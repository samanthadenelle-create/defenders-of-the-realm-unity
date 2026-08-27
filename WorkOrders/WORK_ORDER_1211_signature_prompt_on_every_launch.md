# WORK ORDER 1211 - The game asks the player to sign on EVERY launch

**Status:** READY - IMPLEMENTED 2026-08-26, FULL GATE + DEVICE PROOF OWED. Boot/connect no longer mints or signs: cloud load attaches only an already-usable in-memory session (or guest header) and otherwise keeps the local save. Save writes route unconditionally through shared `BackendRequestSigner.TryAttachAsync` and remain fail-closed. The preserved oracle was restored/registered; fresh deliberate RED `BACKEND_SAVE_AUTH_FAIL`, focused green `BACKEND_SAVE_AUTH_OK`, and fresh `COMPILE_GATE_OK`. Claude must run the full regression after resolving/acknowledging the unrelated pre-existing WO-1239 structure-cadence red, then device-prove two cold launches with zero wallet sheets and no boot-window `sign_messages`.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1211 -> 1212 in the same edit)
**Silo:** Core / backend auth + Wallet
**Reported:** the owner, 2026-08-25, on build `2026.08.26.341323`: *"check why it asks for
authentication everytime i load"* / *"ive never had a game do that"*.

⭐ **That second sentence is the acceptance bar.** No game makes you sign a cryptographic message to
see your own town. A returning player meets a wallet bottom sheet before they meet the game.

---

## The boot, in order, from the device log (`tmp/felt2/logcat-auth.txt`)

```
19:49:53.654  [Flow:Wallet] <- Connect (provider=Solana Wallet, Mainnet) (2782.4ms)
19:49:53.656  [Flow:Wallet] Login connect bound save identity to wallet CHKK...sfkC (cloud-attested=True)
19:49:53.869  [Flow:Wallet] SignMessage via targeted MWA association.
19:49:53.882  [Flow:Wallet] MWA reauthorize+sign_messages identity -> name='Echoes of Elarion' ...
              -> com.solanamobile.wallet/MWABottomSheetActivity takes window focus
```

⭐ **The CONNECT is silent and works.** Auto-resume seals its session and reconnects with no player
action - `6e9f86cc3` is doing its job and is NOT the defect (do not re-debug MWA from this symptom;
the 2026-08-18 anchor already warns about exactly that mis-read).

**What raises the sheet is the line 200 ms later.** `reauthorize` on its own is silent; pairing it
with `sign_messages` is what forces the wallet UI. So the question is only: *who asks for a signature
during boot?*

## Root cause - two auth authorities, and the save path is on the wrong one

`Assets/_Modules/Core/State/GameStateService.cs:1637-1653` runs its **own** nonce-and-sign rail on
every sync, including the LOAD at boot:

```csharp
var nonce = await FetchNonce(wallet);                                   // :1638
var message = $"dotr-save:v1:{wallet}:{nonce}:{payloadHashOrLoadTag}";  // :1648
signature = await signer.SignMessageBase58(message);                    // :1653
```

⛔ **`GameStateService` contains ZERO references to `BackendRequestSigner`** (verified by grep at
HEAD). WO-1157 built a cached session rail - `POST /api/auth/session`, one signature, a bearer token
that speaks for the wallet until it expires - precisely so the player is not asked repeatedly. **The
save path never adopted it.** It mints a fresh nonce and demands a fresh signature every single sync,
and a boot always syncs.

⚠ This is the repo's dominant failure shape in a new place: **one job, two implementations**, the
newer one carrying the guarantee and the older one still wired to the player. Same class as the WO
number block (CLAUDE.md sec.2), the retired assembly table (sec.5), the duplicated R2 push (sec.16)
and today's Food surfaces.

⚠ Note also what canon already recorded about the OTHER direction: WO-1157's session is minted
**lazily on the first authed call**, so a first purchase in a session shows two prompts. Both facts
have the same cure - mint once, early, and reuse - which is why they should be designed together.

## What to build

1. **Route the save sync through the WO-1157 session rail.** `GameStateService`'s load/save attaches
   the cached bearer token; it signs **only** when no usable session exists.
2. ⛔ **SUPERSEDED - DO NOT PERSIST THE SESSION TOKEN.** This item originally said to persist it
   across launches. That is WRONG and the batch ruling overrides it: the in-memory-only design is a
   deliberate security decision with its reason at `BackendRequestSigner.cs:61-66` - it is a bearer
   credential, and PlayerPrefs on Android is readable by a backup. The dev seat raised the conflict
   rather than silently following the older paragraph, which was the correct move.
   **The prompt is removed by not signing for a READ at boot (item 3), not by writing the credential
   down.**
3. **A boot must never sign for a READ.** Loading the player's own town is not a value-granting
   action. If the server requires proof for a load, that proof comes from the session; if no session
   exists, **the game opens on the local save and mints on the first action that actually needs it** -
   never in front of a player who just tapped the icon.
4. **Keep every write fail-closed.** `api/game/save.js` stays authenticated; `authenticateGranting`
   stays on every route that grants value. ⛔ This ticket must not loosen a single grant path - it
   moves WHEN the proof is obtained, never WHETHER.

## ⭐ THE ORACLE FROM THE BOUNCED ATTEMPT IS PRESERVED - START THE REWORK FROM IT

`WorkOrders/preserved/WO-1211_BackendSaveAuthRegression.preserved.cs.txt`

The bounced attempt shipped a genuinely good oracle. It is kept OUT of `Assets/` (and with a `.txt`
extension) for two reasons: an unregistered suite under `Assets/Editor/Regression/` fails
`RegressionMarkerRegression` RULE 2, and registering this one against the current tree would be
deliberately RED - backing the attempt out restored the second signing authority it asserts is gone.

**Owner ruled 2026-08-25: HOLD it, do not register it red**, because `REGRESSION_OK` is a pre-ship
gate and a store submission was in flight. It is preserved rather than deleted per the WO-1053
precedent: when reconciling parallel seats, PRESERVE before you delete.

It pins cached-only boot load, guest routing at BOTH load and save call sites even when enforcement is
off, no connect-time mint or sign, shared-signer write routing with the auth-failure branch
structurally bound to `return false`, and zero remaining auth/nonce/sign authority in
`GameStateService`.

⛔ The rework moves it back and the COMMITTER registers it - never the lane that wrote it. ⛔ Do not
weaken it to make a rework pass; it is the pin that would have caught the bounce.

## Acceptance criteria

- **Cold launch, twice in a row, with a bound wallet: ZERO wallet sheets.** Judged on device, by the
  owner's eyes, and by the absence of `sign_messages` in the boot window of a fresh log.
- The town still loads from the cloud save, proven by a row read - not by the absence of an error.
- A save WRITE still refuses without valid auth; prove the refusal AND the success path (WO-1199's
  lesson: a refusal test is not acceptance).
- A registered oracle asserting `GameStateService`'s sync path resolves its credential through the
  shared session rail, so a future edit cannot quietly re-introduce a second signing authority.
- Gates by marker on fresh logs.

## What NOT to touch

- ⛔ MWA auto-resume / `TryAutoResumeAsync` / `ConnectForLoginAsync`. They are working; the log proves
  a silent connect. Changing them chases the wrong system.
- ⛔ `authenticateGranting` and the purchase/promo/referral rails. Value-granting proof is unchanged.
- ⛔ The `dotr-save:v1:` canonical message format itself while any client in the wild still sends it -
  the server reconstructs it exactly; changing the shape is a separate, coordinated break.
