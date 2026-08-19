# PROD-010 — First-run content signal ("registering build / creating profile") + opt-in OFFLINE download

**Status:** READY TO IMPLEMENT (owner-designed; the CDN ruling below is DECIDED, the copy rules are binding)
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** MEDIUM-HIGH — it is the first thing a new player sees on the LIVE build.
**Silo:** Core UI / content delivery. **Lane:** `Assets/_Modules/Core/UI/LoadingOverlay.cs` + Addressables API. No scenes.
**Provenance:** owner rulings 2026-08-18 — *"lets keep the cdn"* / *"put a small signal that states
registering build to user and creating profile"* / *"gives us the 10 seconds (one time) to load. Have
them watching the left hand while the right hand does the work."*
**Cross-refs:** **PROD-009** (per-family on-demand). If PROD-009 lands first, this signal covers a much
smaller window — its copy and duration assumptions change, so re-read them before implementing.

---

## 1. The owner's ruling: KEEP THE CDN. Record WHY it was right.

> *"lets keep the cdn"* — no duplication, no local/remote moves, no group split for this ticket.

Two verified reasons that make this the correct call, not merely the cheap one:

1. `AddressableAssetSettings.asset:22` reads `m_DisableCatalogUpdateOnStart: 0` — catalog update on
   start is **ENABLED**. **Already-installed APKs adopt the new remote catalog at launch.** So
   re-pointing an asset to a local path would make every shipped build resolve a path that **does not
   exist inside it** — **invisible buildings for every existing player**.
2. Re-grouping rehashes bundles (content-hashed names) and **forces a full re-download for everyone**.

Caching is confirmed ON — `m_UseAssetBundleCache: 1` on both remote group schemas
(`Structure_Art_BundledAssetGroupSchema.asset:29`, `Enemy_Art_BundledAssetGroupSchema.asset:29`). So
the cost is genuinely **one-time PER BUILD**; content-hashed names mean each new APK re-downloads.

---

## 2. Part A — the first-run signal

### Surface

Extend `Assets/_Modules/Core/UI/LoadingOverlay.cs` with a **non-blocking quiet mode**:

- no full-screen background,
- `blocksRaycasts = false` (today it is hard-set `true` at `:120`),
- a bottom strip rather than a cover,
- a `SetMessage(...)` entry point so beats can advance (**does not exist yet** — it is new).

Trigger from `LoadingOverlay.OnSceneLoaded` (`:152-159`). Fire-and-forget
`DownloadDependenciesAsync`, and use **`GetDownloadStatus()` as the ONLY honest progress source**.
Dismiss on handle-done **OR** the existing `MaxShowSeconds = 30f` failsafe (`:47`, enforced at `:184`).

### Rejected alternatives, with reasons (do not re-propose)

- **`BuildFeedbackToast`** — wrong assembly and wrong concern; it speaks about build actions.
- **`ObjectiveBannerUi`** — retired, zero callers.

### ⛔ THE COPY MUST BE TRUE ON EVERY LAUNCH

The beat is **selected from measured inputs**, never from a fixed string:

| Measured input | Beat |
|---|---|
| bundles genuinely downloading (`GetDownloadStatus()` shows bytes outstanding) | the content beat |
| profile genuinely being created (first save write) | the profile beat |
| wallet genuinely binding | the registration beat |
| **nothing is true** — cached, wallet already bound, already onboarded | **SHOW NOTHING** |

A fixed string claiming "creating your profile" on the fifth launch is the **`[[missing:market]]`
defect class**: a UI asserting something the data does not support. **ASCII-only, no colour-carried
meaning** (the owner is red/green colourblind).

Duration honesty: at 5 Mbps a first run is ~33 s of structures and ~141 s of enemies (PROD-009 §1c), so
**no beat may be driven by a timer**. Every advance comes from `GetDownloadStatus()`.

---

## 3. Part B — the opt-in "play offline" download (same API, same hook)

Owner proposal, folded in here deliberately because it is the **same Addressables API and the same
hook** as Part A:

- `GetDownloadSizeAsync` → show the **real MB** for what is not yet cached.
- `DownloadDependenciesAsync` → fetch it, with progress from `GetDownloadStatus()`.
- Explicitly player-initiated. Never automatic, never on a metered-connection assumption.

**Caveats to record in the UI's own copy and in the code header:**

1. **Android can evict the cache under storage pressure.** The screen must **re-check the real size on
   entry**, every time — a "you already have everything" state that was true last week is a lie today.
2. **Every app update invalidates it** (content-hashed names). Say so where the player can see it.
3. ⛔ **Never hardcode the MB figure.** It comes from `GetDownloadSizeAsync` or it is not shown.

---

## 4. Acceptance criteria

1. Quiet mode does not block input: a player can tap through the HUD while the strip is showing
   (`blocksRaycasts == false` proved in the capture, not just in the diff).
2. A **cached** launch (second run, same build) shows **NOTHING** — proved by a screenshot of a second
   boot.
3. A **cold** first run shows beats that advance with real `GetDownloadStatus()` byte movement, and it
   dismisses when the handle completes — not on a timer.
4. `MaxShowSeconds = 30f` still force-dismisses; a stuck download never leaves the strip up forever.
5. Offline screen shows a size that changes when the cache is cleared, and shows 0/"nothing to
   download" when the cache is warm.
6. UI_CAPTURE screenshots opened and reviewed (compile-green never proves a panel reads right).

## 5. What NOT to touch

- Do not move any asset local/remote, do not split groups here (§1) — that is PROD-009's decision and
  its own forced re-download.
- Do not add a fixed-duration beat or a hardcoded MB figure (§2, §3).
- Do not make the offline download automatic.
