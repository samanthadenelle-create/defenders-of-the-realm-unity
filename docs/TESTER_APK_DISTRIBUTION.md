# Tester APK Distribution — how to get the build to testers

How to host `Builds/Android/DefendersOfTheRealm.apk` so others can pull + install it.
App package id: **`com.denellestudios.echoesofelarion`**. The APK is release-signed with
`dotr-release.keystore` (stable signature → testers UPDATE IN PLACE, no reinstall).

---

> ⚠ **Size note (this build): our APK is ~453 MB.** Diawi's FREE tier caps at **50 MB**
> (their paid plan ~€29.99 lifts it) — so **Diawi free will NOT work for our build.** Use
> **Firebase App Distribution** (Option B) instead: free, no practical size cap, plus
> auto-update. Diawi below is kept only for the future case of a <50 MB thin build.

## Option A — Diawi (fastest, one-off — but 50 MB FREE cap; too small for our 453 MB APK)

1. Go to **https://www.diawi.com**.
2. Drag `Builds/Android/DefendersOfTheRealm.apk` onto the page.
3. (Optional) set a **password** + a comment, then **Send**.
4. You get a **URL + QR code**. Send it to the tester.
5. Tester: open the link in their Android browser → download → tap the APK → allow
   "install from unknown sources" if prompted → install.

- **Free tier limits:** the link has a limited lifetime + install count — fine for a quick
  test, not for an ongoing group.
- No account needed. No update notifications (you'd send a new link each build).

---

## Option B — Firebase App Distribution (recurring testers + auto-update notify) — RECOMMENDED

Free, no card, purpose-built for pre-release Android testing. One-time setup, then one
command per build.

### One-time setup
1. Create a project at **https://console.firebase.google.com** (any Google account).
2. **Add an Android app** to the project. Use package name **`com.denellestudios.echoesofelarion`**
   (must match exactly). You can SKIP the `google-services.json` step — App Distribution
   doesn't need it.
3. Left nav → **Release & Monitor → App Distribution → Get started**.
4. **Testers & Groups** tab → create a group named **`testers`** → add your friend's email
   (and yours). Testers get a one-time invite to accept.
5. Note your **Firebase App ID** (Project Settings → your Android app): format
   `1:1234567890:android:abcdef…`.

### Distribute a build — two ways
**Console (no tooling):** App Distribution → **Releases** → drag the APK → pick the
`testers` group → add release notes → **Distribute**. Testers get an email.

**CLI (repeatable — one command per build):**
```
npm install -g firebase-tools
firebase login
firebase appdistribution:distribute Builds/Android/DefendersOfTheRealm.apk \
  --app <YOUR_FIREBASE_APP_ID> \
  --groups "testers" \
  --release-notes "height model + wisdom economy build"
```
Testers: accept the invite once → install the **App Tester** app (or use the emailed link)
→ every new build notifies them automatically; they tap Update.

> Once you have the Firebase App ID, I can add a `distribute-android.ps1` wrapper so a build
> **builds + distributes in one command** (CLI path above). Ask and I'll wire it.

---

## Option C — GitHub Releases (versioned, tied to the repo)

Good if you want a clean versioned download URL.
1. `gh release create v0.x.y Builds/Android/DefendersOfTheRealm.apk -t "Tester build" -n "notes"`
   (or the GitHub web UI → Releases → attach the APK).
2. Share the release page / the asset's download URL.
- **Caveat:** a **private** repo's release asset needs a GitHub login to download. For friends
  to pull freely, use a **public** repo (or a small separate public "builds" repo).
- No update notifications; testers re-download.

---

## Option D — itch.io (private landing page)

1. Create a project → set kind **Android**, upload the APK.
2. Set access to **Restricted** (password or per-user keys) for private testing.
3. Share the page + password. Doubles as a tidy build page you control. Free.

---

## The real launch channel (later) — Solana dApp Store (Seeker, 0% fees)

Not for quick testing (needs the dApp Store CLI + a publisher NFT), but it's the actual
0%-fee release channel for Seeker. For TESTING now, testers just **sideload the APK** on the
Seeker via any option above. See memory `android-seeker-distribution-and-wallet-strategy`.

---

## Notes that apply to all options
- Testers must enable **Install unknown apps** for their browser/file app (Android Settings →
  Apps → special access). Firebase/Diawi smooth this prompt; a raw Drive/GitHub link just
  downloads and Android prompts on open.
- Stable keystore = **update in place**. Do NOT lose `dotr-release.keystore` / `keystore.properties`
  — losing it means testers must uninstall + reinstall (save loss) on the next signed build.
- If testers actually play + the game cloud-syncs, the backend (`/api/game/save`) must be
  deployed/reachable — separate from APK hosting.
