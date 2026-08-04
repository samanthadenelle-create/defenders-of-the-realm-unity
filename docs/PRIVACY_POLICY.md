# Privacy Policy — Echoes of Elarion (Defenders of the Realm)

**App:** Echoes of Elarion (a chapter of Defenders of the Realm)
**Publisher:** DeNelle Studios
**Contact:** SUPPORT_EMAIL_PLACEHOLDER
**Effective date:** 4 August 2026

---

## 1. Summary (plain language)

Echoes of Elarion is a single-player game. We collect the **minimum** needed to run the game, keep your
progress, fix bugs, and improve it. We do **not** sell your data, and we do **not** use it for advertising
profiling. **The game shows no advertisements** — see section 4.

You can play the entire game as a **guest**. As a guest we never receive your name, email address, or phone
number. If you choose to create an account or sign in, we receive your **email address** (or, for Google
sign-in, the basic account details Google returns). If you connect a Solana wallet, we record your **public
wallet address** — never your seed phrase or private keys, which the app never sees.

Your progress is saved on your device **and**, by default, backed up to our server so it can be restored.
This backup runs automatically for signed-in players *and* for guests; a guest backup is stored under an
opaque identifier derived from your device, not under any personal detail.

## 2. What we collect

**a. Account details — only if you sign in.**
The sign-in screen offers email + password, Google sign-in (Android), Connect Wallet, and Play as Guest.
If you create an account or sign in with email, your **email address and password** are handled by Google
Firebase Authentication on our behalf; we can see the email address, never the password. If you use Google
sign-in, we receive the basic account information Google returns for that sign-in. If you use Play as Guest,
none of this is collected.

**b. Public wallet address — only if you connect a wallet.**
When you connect a Solana wallet, the **public address** becomes the key your cloud save, and any bug report
you send, is stored under. Signing happens inside your wallet app; the game never receives, requests, or
stores your seed phrase or private keys.

**c. A device-derived player identifier — if you play as a guest.**
So that a guest still gets a cloud backup, the game generates a stable identifier by hashing your device's
identifier with a fixed salt (SHA-256). The raw device identifier is never sent to us — only the hash. It is
pseudonymous: it identifies a device's save, not a person.

**d. Your game save.**
Your progress — village layout, buildings, resources, roster and Echoes, queues, quests, hero and army state,
your in-game settings, your in-game invite code and any in-game contacts you add — is stored on your device
and, by default, uploaded in full to our server under the identifier in (b) or (c). The upload happens
automatically when the game is paused or closed, and any upload that fails is retried on the next launch.

**e. Gameplay analytics.**
The game sends event records such as `session_start` and progression events, along with app version, Unity
version, and platform. These are **not anonymous**: each event is labelled with the same identifier as your
save — your wallet address if one is connected, otherwise your guest identifier, otherwise `anonymous`.

**f. Diagnostic logs — web version only.**
In the browser/web version, diagnostic tracing is **on by default** and streams the game's log output to our
server: log, warning, error and exception text, stack traces, the active scene name, timestamps, the app
version, and the hostname the game was loaded from, tagged with a random per-session identifier that is not
linked to your account. This remote streaming is **web-only** — the Android build does not send it.

**g. Bug reports — only when you submit one.**
The in-game bug report form sends what the form shows you: your written note, a screenshot of the game (on by
default, and you can turn it off before sending), the recent diagnostic log lines, the scene name, the app
version, the platform, a per-report session id, and your save identifier from (b) or (c) so we can follow up
on repeat reports. Pressing Submit is the consent — nothing is sent unless you press it.

**h. Pi Network sign-in — web version, inside Pi Browser only.**
If you play the web version inside Pi Browser and sign in with Pi, we receive your Pi user id and username
from Pi's authentication service in order to establish that session. This does not apply to the Android /
Solana dApp Store version.

**We do not collect:** your phone number, your precise or GPS location, your device's contacts, your photo
library, your camera or microphone, your biometric data, or any advertising identifier. The Android app
requests only the INTERNET permission.

## 3. How we use it

To run the game, save and restore your progress, authenticate you, diagnose and fix bugs, measure and improve
performance and features, and — if you connect a wallet — to deliver anything you purchase. We do **not**
build advertising profiles, and we do **not** use your data to target advertising.

## 4. Advertising

**The game does not currently show advertisements.** No advertising SDK is integrated, no advertising network
is contacted, and no data whatsoever is shared with any advertising network.

The game does contain an optional "Ad" button that shortens a build timer. In the current version it grants
that time saving immediately without presenting any advertisement, because no ad provider is connected. It is
always optional; nothing in the game requires it.

If rewarded advertising is introduced in a future version, this policy will be updated **before** that version
ships, and the advertising provider will be named in this section and in section 5.

## 5. Who we share it with

- **Infrastructure providers who run the service on our behalf** — Vercel (application hosting and server
  functions) and Neon (the Postgres database that stores cloud saves, analytics, diagnostics, and bug reports).
- **Google Firebase Authentication** — if you create an account or sign in with email or Google, Firebase
  handles that sign-in and holds your credentials. Google's own privacy policy governs its handling.
- **Pi Network** — only in the web version inside Pi Browser, and only if you sign in with Pi, in order to
  verify that sign-in.
- **Your Solana wallet app** — only in the sense that signing happens there; we send it nothing about you.
- **No advertising networks and no data brokers.**

We do **not** sell your data or share it for anyone else's marketing.

## 6. Data retention

- **Web diagnostic logs** and authentication-rejection records are deleted automatically on a daily sweep once
  they are more than **7 days** old.
- **Cloud saves** are kept for as long as the identifier they are stored under is in use, so that your
  progress can be restored. Ask us and we will delete yours.
- **Gameplay analytics events** and **bug reports** are retained until they are no longer useful for
  diagnosing and improving the game, and are deleted on request.
- **Local save data** lives on your device until you delete the app or clear its data.

## 7. Children

This game is not directed to children under **13**, and we do not knowingly collect personal data from them.
Thirteen is the threshold set by the U.S. Children's Online Privacy Protection Act (COPPA), and this section
follows that standard.

Where local law sets a **higher** minimum age for consenting to data processing — in particular the EU/UK
GDPR, under which member states set a digital-consent age between 13 and 16 — that higher local age applies
instead, and the game is not directed to anyone under it.

The game does not ask for your age and has no age gate. If you believe a child has provided us with personal
data, contact us at SUPPORT_EMAIL_PLACEHOLDER and we will delete it.

## 8. Your choices & rights

- **Play as a guest.** You can play the whole game without giving us an email address, a Google account, or a
  wallet address.
- **Turn off the bug-report screenshot.** The screenshot toggle is on the report form, and nothing is sent
  until you press Submit.
- **Clear your local save** through your device's app settings.
- **Ask us to delete your data.** Contact SUPPORT_EMAIL_PLACEHOLDER with your wallet address, the email you
  signed up with, or your report/session id, and we will delete the cloud save, analytics, diagnostic and
  bug-report records associated with it.

**Please note:** the current version has **no in-game setting that turns analytics or diagnostics off**, and
the game's Settings screen does not offer one. Analytics are sent on all platforms and web diagnostic tracing
is on by default in the browser version. Until an opt-out exists in the game, the way to stop collection is to
stop playing and uninstall, or to write to us and ask for deletion. We would rather tell you that plainly than
describe a switch that is not there.

## 9. Security

Everything the game sends travels over HTTPS. Cloud saves keyed to a wallet require a signature from that
wallet over a single-use challenge, so another player cannot write to your save. Guest cloud saves use a
weaker, rate-limited path and are marked as untrusted on our side. No database credentials or secrets are
embedded in the game client. We never handle wallet private keys, and transaction signing happens entirely
inside your wallet app. No method of transmission or storage is 100% secure.

## 10. Changes

We may update this policy. The effective date at the top will change, and material updates will be noted
in-app or on this page. Any change that widens what we collect — including the introduction of advertising —
will be published here before the version that makes the change ships.

## 11. Contact

DeNelle Studios — SUPPORT_EMAIL_PLACEHOLDER
