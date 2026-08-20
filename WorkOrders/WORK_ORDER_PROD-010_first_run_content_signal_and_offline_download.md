# PROD-010 — First-run content signal ("registering build / creating profile") + opt-in OFFLINE download

**Status:** RE-IMPLEMENTED, GATED, COMMITTED and SHIPPED 2026-08-20 (`fa411367d`) — **AWAITING THE ONE
THING ONLY THE OWNER CAN DO: AN AIRPLANE-MODE RUN ON DEVICE.**

> The "UNGATED, UNCOMMITTED" line that used to sit here was written by the authoring lane BEFORE the
> committer gated it, and it went stale the moment the batch landed. Current, verified state:
> `COMPILE_GATE_OK`; `REGRESSION_OK 225/225 suites — 225 green, 0 red` with `[offline-pull]`
> registered and green; working tree clean for `OfflineContentService.cs` / `OfflineOptInPanel.cs`;
> shipped in build **2026.08.20.333831** on the Seeker.
>
> ⛔ WHAT REMAINS IS NOT CODE. Every acceptance criterion is met EXCEPT the airplane-mode felt-test,
> and no amount of further engineering can close it: the whole claim of this feature is "the game
> opens without a connection", and only a device with the radio off can witness that. Two minutes:
> Settings → Offline → Download → wait for 100% → turn Wi-Fi OFF → cold-start the app.

> ## ⛔ THE 2026-08-19 "IMPLEMENTED" WAS FALSE. RECORDED HERE SO IT CANNOT BE RE-LITIGATED.
>
> The status line that used to sit here read *"IMPLEMENTED 2026-08-20 (`345a7b464`) — AWAITING OWNER
> FELT-VERIFY"*. **The feature did not work at all.** `OfflineContentService.ContentKeys` was
> `{ "Structure_Art", "Enemy_Art" }` — Addressable **GROUP names**. A group name is not an Addressables
> key; only **addresses** and **labels** are, and the only labels this project authors are `default`,
> `Locale`, `Locale-en` (verified in `AddressableAssetSettings.asset` → `m_LabelTable`). So every
> `GetDownloadSizeAsync` matched nothing and answered **0**, the prompt told the player *"Everything is
> already downloaded"*, `SetOptedIn` stamped them offline-ready, and **not one byte was ever fetched**.
>
> Owner, verbatim: *"i would not be asking that of the villiage if you had just completed prod 10"* — and
> that is a fair reading. The eager content-warming strain the project is now unwinding exists partly
> because this pull never pulled.
>
> **Why nothing caught it:** compile gate green, regression suite green, ticket marked IMPLEMENTED.
> Nothing in the build ever compared the CLAIM ("download complete") against an OUTCOME ("are the bytes
> actually cached?"). A success report that is never checked against an outcome is indistinguishable from
> a no-op, and that is the general lesson, not a detail about Addressables keys.

**What landed 2026-08-20 (this pass), and what is still owed:**

| # | Obligation | State |
|---|---|---|
| 1 | Content set is COMPLETE, not a naming guess | DONE — see §A below |
| 2 | The pull PROVES it pulled (post-pull re-measure = 0) | DONE — `PullVerified`, the missing assertion |
| 3 | Per-build keying survives the re-pack | DONE — version stamp **plus** an online re-verify |
| 4 | Progress + size MEASURED and smooth | DONE — byte-weighted `GetDownloadStatus`, MB shown |
| 5 | Useful subset instead of all-or-nothing | **NOT BUILT — written up in §D, owner's call** |
| — | `COMPILE_GATE_OK` | GREEN (coordinator gated the combined tree); DataRegression 217/222, 4 known-red baseline |
| — | `OFFLINE_PULL_OK` regression | WRITTEN + **registered** in `DataRegression.RunAll` by the coordinator; 7 groups incl. a meta group that proves the suite can fail |
| — | On-device airplane-mode felt-verify | ⛔ STILL NOT PROVEN — same gap as before, and it is the one that matters |

⚠ **NOT PROVEN, stated plainly:** the compile gate and `DataRegression` are green (coordinator, 217/222
with the 4 known-red baseline), and the set-completeness claim is backed by the catalog/group audit in
§A. But **no device run and no airplane-mode run** — the actual promise this feature makes to the player
has still never been felt, before or after. Do not mark this DONE on the strength of this table.
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** MEDIUM-HIGH — it is the first thing a new player sees on the LIVE build.
**Silo:** Core UI / content delivery. **Lane:** `Assets/_Modules/Core/UI/LoadingOverlay.cs` + Addressables API. No scenes.
**Provenance:** owner rulings 2026-08-18 — *"lets keep the cdn"* / *"put a small signal that states
registering build to user and creating profile"* / *"gives us the 10 seconds (one time) to load. Have
them watching the left hand while the right hand does the work."*
**Cross-refs:** **PROD-009** (per-family on-demand). If PROD-009 lands first, this signal covers a much
smaller window — its copy and duration assumptions change, so re-read them before implementing.

---

## 0. ⛔ OWNER RULING 2026-08-19 — THIS TICKET SUPERSEDES PROD-009. LANDING IT CLOSES BOTH.

> Owner, verbatim: **"PROD 10 kills 10 and 09"**.

**Why it is the right call and not merely the cheaper one:** PROD-009 (per-family on-demand enemy
content) exists to shrink the first-run download by streaming families as they are needed. This ticket
takes the same bytes and moves them to ONE honest, opt-in, up-front download behind a signal that tells
the player what is happening. Once the player has consciously chosen to pull the content, there is
nothing left for per-family streaming to solve - it would spread the identical bytes across the session
and buy a worse experience for a large amount of Addressables complexity.

It also removes the ordering trap this ticket used to carry. The old cross-ref warned that PROD-009
landing first would change this ticket's copy and duration assumptions ("gives us the 10 seconds (one
time) to load"). With 009 retired, the duration is simply the real download - and the copy has to be
honest about it. Measured 2026-08-19: the remote set is 88,253,119 bytes (enemy 67,582,523 + structure
20,670,596), i.e. roughly **141 s at 5 Mbps and 471 s at 1.5 Mbps**. **DO NOT ship copy promising ten
seconds in front of a two-to-eight minute download.** The signal's job is to be true, not brief.

⚠ Two things PROD-009 was ALSO carrying that do not die with it - re-home them or they are lost:
  1. The **main-thread freeze**: `StructureAssetLoader` (and five sibling loaders) call
     `handle.WaitForCompletion()`, a SYNCHRONOUS stall, not async pop-in. An up-front download does not
     fix a blocking resolve; it only moves when it hurts.
  2. **Zero labels** on all 78 enemy + 35 structure entries (`m_SerializedLabels: []`), which is what
     made partial fetch impossible in the first place. An opt-in "download everything" needs no labels -
     but anything that ever wants a subset does.

**Action:** mark PROD-009 SUPERSEDED BY PROD-010, do not implement it, and fold the two survivors above
into this ticket or mint them separately. Recorded here in the same breath as the ruling (CLAUDE.md
section 15) so the retirement cannot be re-litigated from a stale doc.

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

---

# 2026-08-20 — THE ACTUAL FIX (re-open pass)

## §A. Set completeness — what the audit found, and why prefixes were the wrong shape

The interim fix (`dd6c9732a`) replaced the group names with an enumeration of catalog **addresses**
under the prefixes `Structures/` and `Enemies/`. That was a floor, not the finish. Audited today against
`Assets/AddressableAssetsData/`:

| group | addresses | LoadPath profile var | remote? | address shape |
|---|---:|---|---|---|
| `Structure_Art` | 35 | `Remote.LoadPath` | **YES** | `Structures/…` |
| `Enemy_Art` | 78 | `Remote.LoadPath` | **YES** | `Enemies/…` |
| `Gear` | 427 | `Local.LoadPath` | no | `gear/…` |
| `Dungeon` | 1 | `Local.LoadPath` | no | `dungeon/…` |
| `Localization-Locales` / `-String-Tables-English` / `-Assets-Shared` | 3 | `Local.LoadPath` | no | — |
| `Default Local Group` | 0 | `Local.LoadPath` | no | — |

`Remote.LoadPath` = `https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]`.

**Finding:** the two prefixes cover exactly the remote set **today, and only by coincidence of naming.**
They encode a guess about how content is named, and the owner ruled TODAY that enemies re-pack **per
family** and structures **per asset** — a re-pack renames and multiplies groups freely. The next remote
group whose addresses do not start with `Structures/` or `Enemies/` would have been dropped **silently**:
the identical failure mode as the group-name bug, wearing a different hat. `gear/` (427 entries) also
sits one profile-variable flip away from being remote, and nothing in the prefix list would have noticed.

**Change:** the runtime set is now **every key in the loaded catalog**, minus 32-hex asset-GUID duplicates
and an explicit (currently empty) `ExcludedKeyPrefixes`. This is safe and costs nothing because
Addressables answers the remote/local question itself — a **local bundle contributes 0 bytes** to
`GetDownloadSizeAsync` and `DownloadDependenciesAsync` on it is a no-op — and `MergeMode.Union`
deduplicates shared bundles, so the number shown to the player is the true remote set whatever the groups
end up being called. Completeness **by construction**, not by naming.

**Second net (Editor):** `OfflinePullRegression` group 5 walks the real `AddressableAssetSettings`, finds
every group whose `LoadPath` resolves to an `http…` URL, and requires `IsOfflineContentKey` to accept
every address in it. A re-pack that produces a remote group the offline set would drop fails there,
loudly, at build time — instead of on a player's plane.

## §B. How the pull now proves it pulled

`DownloadAllForOffline` no longer trusts handle status:

1. `EnsureInitialized()` (coroutine yield on `InitializeAsync(false)` — **never** `WaitForCompletion`,
   which Addressables 2.9.1 implements as `while (!InvokeWaitForCompletion()) { }`, no timeout, no exit).
2. `CollectContentKeys()` — 0 keys is an **abort**, never a success.
3. `MeasureDownloadSize(keys)` — ONE batched `GetDownloadSizeAsync(IEnumerable)` (not a per-key sum,
   which would double-count shared bundles and overstate the size to the player). Unmeasurable = abort.
4. Download in chunks of 24 with `MergeMode.Union`, each handle released after its chunk so peak bundle
   memory stays bounded.
5. **⭐ `MeasureDownloadSize(keys)` AGAIN, and `PullVerified(keyCount, allHandlesOk, remaining)` must be
   true — keys > 0, every handle Succeeded, and `remaining == 0`.** Only then is the offline-ready stamp
   written, by `StampOfflineReady`, which is **private**: the panel can no longer stamp from its own
   reading, which is the door the original defect walked through.

## §C. Per-build keying after the re-pack

`PulledForThisBuild` still requires `offline.pulledbuild == Application.version`, and that still holds.
But the version stamp **alone is not sufficient and is no longer relied on alone**: a remote catalog
update — or today's re-pack — re-hashes every bundle while `Application.version` never moves. So
`ResolveContentSource` now runs `VerifyCachedSetStillComplete()` on every **online** launch when the
stamp is set: it re-measures and **clears the stamp** if bytes have reappeared, so the player is offered
the download again. It never clears on an *unmeasurable* answer — an unknown must not cost a player a
download they already paid for, exactly as it must not earn them a promise they have not.

## §D. Useful subset (obligation 5) — NOT BUILT, owner's call

Now that enemies pack per family and structures per asset, a partial pull is technically possible: fetch
`Structures/` only (20.7 MB of the 88.3 MB measured on 08-19) and stream enemy families on demand.

**Recommendation: do not build it yet.** Two reasons, both concrete:

1. **It re-opens PROD-009, which the owner explicitly killed** (*"PROD 10 kills 10 and 09"*). Per-family
   streaming is exactly what 009 was, and the ruling was that one honest up-front download beats spreading
   the same bytes across the session.
2. **It would break the promise the screen makes.** The whole value of this feature is the sentence
   *"this game now works without a connection."* A subset pull cannot say that — it has to say "the town
   works offline but raids may not", and the player discovers the caveat mid-raid on a plane. Today's
   design has ONE claim and ONE proof of that claim; a subset needs a claim, a proof, and a per-family
   fallback story for every uncached family.

If the owner does want it, the clean shape is: **labels, not prefixes.** All 78 enemy + 35 structure
entries currently have `m_SerializedLabels: []` (noted in §0 of this ticket as a PROD-009 survivor). Author
a `core` label on the always-needed set and a per-family label on the rest; then `IsOfflineContentKey`
becomes a label filter, `GetDownloadSizeAsync` sizes each tier separately, and the prompt can offer
"Town only / Everything" with two measured numbers. That is a real ticket, not a tweak — mint it rather
than smuggling it in here.

## §E. Follow-ups this pass deliberately did not do

- **On-device airplane-mode felt-verify.** Still the one thing that has never been proven, before or after.

## §F. 2026-08-20 follow-up — the regression was a HOLLOW PASS, and that mattered

The coordinator's gate caught it, via the project's own regression meta-oracle:

> `REGRESSION MARKER FAIL (1): hollow pass: OfflinePullRegression.cs has no failing path at all
> (no 'return false', no 'return <list>.Count == 0') - it can only ever report OK`

The first cut computed `bool ok = failures.Count == 0; return ok;`. That *behaves* correctly — but
acting on the flag was right for a reason bigger than the grep. **PROD-010 shipped broken because a
success report was structurally incapable of being a failure report.** Fixing that with a test carrying
the same shape would be the identical mistake at one remove, and a rule that cannot bite is worse than
no rule because it reads as coverage.

Two changes:

1. **`Run` exits through an explicit, reachable `return false`** when any group records a failure,
   naming which assertion and the observed values.
2. **New group 0 proves the failure path is reachable AT RUNTIME**, not by shape. It feeds a
   deliberately-wrong expectation — `ClassifySize(0, 0) == AlreadyCached`, i.e. exactly what the
   2026-08-19 build effectively believed — through the same `Expect` helper the real groups use, and
   FAILS if nothing gets recorded. It earns its place twice: if the service ever regresses to calling a
   zero-key set "already cached", the probe stops recording and group 0 fails, while group 1 fails from
   the opposite direction on the same regression. A second probe does the same for `PullVerified`
   against the no-op shape (handles OK, 5 MB still outstanding).

**Re-pack re-check (it landed while this was in flight).** Group 5 was re-verified against the tree
rather than assumed: `Enemy_Art` is now `m_BundleMode: 2` (PackTogetherByLabel, 5 `enemyfam-*` bundles)
and `Structure_Art` is `m_BundleMode: 1` (PackSeparately, 35 per-asset bundles). **Both stayed on
`Remote.LoadPath`; addresses unchanged.** Bundle mode re-shapes how bytes are packed, never what a key
is — which is precisely why the set is enumerated from catalog keys and not group names. Group 5 now
also walks entry **labels**, because after the re-pack `enemyfam-*` names actual bundles.

One canon correction the re-pack forced, fixed in the same breath (CLAUDE.md §15): the chunk-size note
in `OfflineContentService` said all enemy + structure entries carry `m_SerializedLabels: []`. True when
written that morning, false by that afternoon. Enemy entries now carry exactly one `enemyfam-*` label
each; no entry carries a blanket `default` label, so the widest label expands to ~16 entries and the
per-chunk memory bound still holds.
