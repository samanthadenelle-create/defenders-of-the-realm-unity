# Solana dApp Store — the listing, and how to find it again

**Written:** 2026-08-19 (CLI seat). **Why it exists:** this information was supplied by the owner at least
twice and lost both times, because it lived only in a chat session. It now lives here, in
`publishing/config.yaml`, and in the auto-memory index. **If you learn a new fact about the listing, add
it here in the same breath.**

---

## THE IDENTIFIERS

| what | value | source |
|---|---|---|
| **App NFT address** | `5MG4atMRDSVn9t75oFz1KVxKdUkyz2wPi2MeunT8yFe6` | owner-supplied 2026-08-19; mirrored into `publishing/config.yaml:43` |
| **Package id** | `com.denellestudios.echoesofelarion` | verified at `Assets/Editor/AndroidBuild.cs:46` |
| **Store display name** | "Defenders Of the Realm" | read off the live listing on the Seeker, 2026-08-19 |
| **Tagline on the listing** | "Echoes of a Forgotten Civilization" | same; matches canon-strings |
| **Publisher** | DeNelle Studios | `publishing/config.yaml:32` |
| **Publisher site** | https://echoes-of-elarion.vercel.app/ | `publishing/config.yaml:34` |
| **Support email** | support.EoA@icloud.com | `publishing/config.yaml:35-36` |
| **Release NFT address** | still `""` — filled by the CLI at publish | `publishing/config.yaml:77` |

## ⛔ THERE IS NO WEB URL FOR A dApp STORE LISTING

Verified on the device 2026-08-19 by reading the store app's registered intent filters
(`adb shell dumpsys package com.solanamobile.dappstore`):

```
solanadappstore:
    Action: "android.intent.action.VIEW"
    Scheme: "solanadappstore"
    Authority: "details"
```

**The only registered scheme is `solanadappstore://details…` — there is no `https` listing host**, so there
is nothing to paste into a desktop browser. A listing link resolves ONLY on a device with the dApp Store
installed. Anyone asking for "the store URL" for marketing wants either the publisher site above or the
App NFT address, which is the canonical on-chain identifier.

The store app is `com.solanamobile.dappstore/.activity.MainActivity`. Navigating to a listing in-app
leaves **no URI on the activity** (`act=MAIN cat=LAUNCHER`), so the current page cannot be read back over
adb — a screencap is the only way to see where someone is.

## THE APP IS LIVE. THE NEXT SUBMISSION IS AN UPDATE.

Settled 2026-08-19 from the owner's device, not from a doc: the listing renders an **Open** button (not
Install), and its "What's new in this release" header reads **`2026.08.17.328845`**.

This closes a question that had been open and blocking: canon asserted a live presence but **no artifact in
the repo recorded a publish**, and `docs/SOLANA_STORE_READINESS_2026-08-06.md:22` still listed the NFT mint
as the next action. Both are stale as of today. It matters because `publishing/config.yaml:119-128` warns
that minting a release NFT against the wrong binary is expensive to undo.

**Version drift, as of 2026-08-19:**

| where | version |
|---|---|
| live on the store | `2026.08.17.328845` |
| built and parity-verified locally today | `2026.08.19.332478` |

## HOW TO CHECK THE LIVE STATE AGAIN (no guessing required)

```bash
# adb is NOT on PATH - it ships with the Unity Hub Android SDK platform-tools
ADB="$LOCALAPPDATA/Microsoft/WinGet/Packages/Google.PlatformTools_*/platform-tools/adb.exe"

# 1. what is the device foregrounded on?
"$ADB" shell dumpsys window | grep mCurrentFocus

# 2. what version of OUR app is installed?
"$ADB" shell dumpsys package com.denellestudios.echoesofelarion | grep -E "versionName|versionCode|lastUpdateTime"

# 3. what does the STORE say is live? - screencap the listing; there is no URL to query
"$ADB" shell screencap -p /sdcard/store.png && "$ADB" pull /sdcard/store.png
```

The publisher portal at `publish.solanamobile.com` is the authority for anything not visible on device.

## RELATED

- `publishing/config.yaml` — the submission manifest (media assets, `--whats-new`, the two NFT addresses)
- `docs/MONETIZATION_STATE_2026-08-19.md` — what can and cannot ship, and the owner decisions it waits on
- `WorkOrders/WORK_ORDER_1124_*.md` — why an APK must pass `R2_PARITY_OK` before it is ever submitted:
  a build whose bundles are not hosted installs perfectly and then shows nothing
