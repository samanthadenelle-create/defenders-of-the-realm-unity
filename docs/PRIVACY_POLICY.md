# Privacy Policy — Echoes of Elarion (Defenders of the Realm)

**App:** Echoes of Elarion (a chapter of Defenders of the Realm)
**Publisher:** DeNelle Studios
**Contact:** support.EoA@icloud.com
**Effective date:** 22 August 2026

---

## 1. Summary (plain language)

Echoes of Elarion is a single-player game. We collect the **minimum** needed to run the game, keep your
progress, fix bugs, and improve it. We do **not** sell your data, and we do **not** use it for advertising
profiling. The monetized Android update offers **optional rewarded advertisements** — see section 4.

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

**We do not collect directly:** your phone number, your precise or GPS location, your device's contacts,
your photo library, your camera or microphone, or your biometric data. When the advertising SDK is active,
Unity LevelPlay and its mediated advertising partners may process device and advertising identifiers,
IP address, coarse location inferred from IP, app/device information, consent choices, and ad interaction
and performance data under their own privacy notices. We do not receive your seed phrase or private keys.

## 3. How we use it

To run the game, save and restore your progress, authenticate you, diagnose and fix bugs, measure and improve
performance and features, and — if you connect a wallet — to deliver anything you purchase. We do **not**
build advertising profiles ourselves. Advertising partners may select or personalize ads where permitted by
law and by your consent choices, as described in section 4.

## 4. Advertising

The monetized Android update offers **optional, user-initiated rewarded advertisements** through Unity LevelPlay. There are no
forced interstitial advertisements or persistent banner advertisements. The reward and the choice to watch
are shown before an ad starts. If an ad is unavailable, fails, or is dismissed before completion, no reward
is granted and ordinary play remains available.

LevelPlay is an advertising mediation service: the ad shown may be supplied by Unity or another advertising
network configured through LevelPlay. Those providers may process the technical and advertising data listed
in section 2 to deliver, secure, measure, and report an ad. Where a consent choice is presented, that choice
governs whether personalized advertising may be delivered. Declining consent does not prevent ordinary play;
it may make an advertisement unavailable or limit it to non-personalized delivery.

## 5. Who we share it with

- **Infrastructure providers who run the service on our behalf** — Vercel (application hosting and server
  functions) and Neon (the Postgres database that stores cloud saves, analytics, diagnostics, and bug reports).
- **Google Firebase Authentication** — if you create an account or sign in with email or Google, Firebase
  handles that sign-in and holds your credentials. Google's own privacy policy governs its handling.
- **Pi Network** — only in the web version inside Pi Browser, and only if you sign in with Pi, in order to
  verify that sign-in.
- **Your Solana wallet app** — when you connect it or choose to buy, the Game sends the public transaction
  details needed for your review and approval, such as the token, amount, network, and recipient. Signing stays
  inside the wallet app; we never send it your seed phrase or private key.
- **Unity LevelPlay and its mediated advertising partners** — only when the rewarded-ad service is active,
  to deliver and measure an advertisement and prevent fraud, as described in sections 2 and 4.
- **No data brokers.**

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
data, contact us at support.EoA@icloud.com and we will delete it.

## 8. Your choices & rights

- **Play as a guest.** You can play the whole game without giving us an email address, a Google account, or a
  wallet address.
- **Turn off the bug-report screenshot.** The screenshot toggle is on the report form, and nothing is sent
  until you press Submit.
- **Clear your local save** through your device's app settings.
- **Ask us to delete your data.** Contact support.EoA@icloud.com with your wallet address, the email you
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
in-app or on this page. Any material change that widens what we collect will be published here before the
version that makes the change ships.

## 11. Contact

DeNelle Studios — support.EoA@icloud.com
