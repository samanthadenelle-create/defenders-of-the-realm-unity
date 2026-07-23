# Privacy Policy — Echoes of Elarion (Defenders of the Realm)

> **DRAFT (2026-07-23) — for owner review before hosting.** First pass by CLI, sourced from the app's
> actual data flows (`api/trace.js`, `api/events/track.js`, the save system). **Not legal advice** — the
> owner (DeNelle Studios) reviews/approves and hosts it at a public URL; that URL goes in the Solana dApp
> Store listing (a privacy policy URL is required when an app collects user data). Fill the `{{...}}` fields.

**App:** Echoes of Elarion (a chapter of Defenders of the Realm)
**Publisher:** DeNelle Studios
**Contact:** {{support email}}
**Effective date:** {{date}}

---

## 1. Summary (plain language)
Echoes of Elarion is a single-player game. We collect the **minimum** needed to run the game, fix bugs, and
improve it. We do **not** sell your data, and we do **not** collect real-world identity information (no name,
email, phone, or precise location) unless you contact us directly. Your game progress is saved **on your
device**. If you connect a Solana wallet (later versions only), we record only your **public wallet address**
to deliver in-game purchases — never your seed phrase or private keys, which the app never sees.

## 2. What we collect
- **Gameplay & diagnostic telemetry** — anonymous events about how the game runs (level reached, features
  used, performance/frame data, error and crash traces). Used to debug and improve the game. Sent to our
  backend only when diagnostics are active.
- **Device & session data** — non-identifying technical info (app version, device/OS type, a random session
  id) attached to telemetry.
- **Public wallet address** *(only if/when you connect a Solana wallet in a version that supports purchases)* —
  used solely to grant purchased items. We never receive or store your seed phrase or private keys; wallet
  signing happens in your wallet app.
- **Local save data** — your progress (city layout, resources, roster, settings) is stored **on your device**
  (local storage). We do not upload your save unless a future opt-in cloud-save feature is enabled.

We do **not** collect: your name, email, phone number, precise/GPS location, contacts, photos, or biometric data.

## 3. How we use it
To operate the game, diagnose and fix bugs, measure and improve performance and features, and (for wallet
versions) deliver purchases. We do not use your data for advertising profiling{{, except aggregate reward-ad
metrics if rewarded ads are enabled — update this line when ads ship}}.

## 4. Who we share it with
- **Infrastructure providers** who host our backend and database (e.g. Vercel, Neon) strictly to run the
  service on our behalf.
- {{Ad network (e.g. Unity LevelPlay/AdMob) — only if/when rewarded ads are enabled; update this section.}}
We do **not** sell your data or share it for others' marketing.

## 5. Data retention
Diagnostic/telemetry records are automatically deleted on a rolling **~7-day** schedule. Local save data lives
on your device until you delete the app or clear its data. Public wallet address records are kept only as long
as needed to support your purchases and account.

## 6. Children
This game is not directed to children under {{13 / the applicable age}}, and we do not knowingly collect data
from them. If you believe a child has provided data, contact us and we will delete it.

## 7. Your choices & rights
You can stop all telemetry by not enabling diagnostics{{/via the in-game Settings toggle — confirm the toggle
exists or add one}}. You can clear local save data from your device settings. To request deletion of any data
associated with your wallet address or session, contact us at {{support email}}.

## 8. Security
We use reasonable technical measures to protect data in transit and at rest. No method is 100% secure, but we
never handle wallet private keys, and payment signing occurs entirely within your wallet app.

## 9. Changes
We may update this policy; the effective date above will change and material updates will be noted in-app or on
this page.

## 10. Contact
{{DeNelle Studios — support email / website}}

---
*Draft — owner to fill the `{{...}}` fields, confirm the telemetry-toggle + children age, adjust the ads/wallet
sections to match the shipped build, then host at a public URL for the dApp Store listing.*
