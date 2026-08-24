> ⚠ **NUMBER COLLISION — this document does not own WO-911; `WORK_ORDER_911_unified_queue_screen.md` does.**
> Referred to hereafter as **WO-911-B (timer speed-up, crystals, all channels)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the CRYSTAL channel is DONE - `BuildTimerService.cs:732/818/856` carry a channel-aware `InstantFinishPrice` / `TryInstantFinish`. The AD channel is NOT done - there is no `TryAdSkip` in `BuildTimerService`.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

> ## !! WO NUMBER COLLISION - "911" IS TWO UNRELATED WORK ORDERS !!
> This file is **WO-911 timer speed-ups** (crystals + ads). A SECOND, entirely unrelated WO also numbered
> 911 exists: `WorkOrders/WORK_ORDER_911_unified_queue_screen.md` (the unified Manage/Queues screen),
> and THAT one is DONE. **Commits and board rows crediting "WO-911" refer to the SCREEN, not to this
> timer work.** A RESULT file or a board row keyed to the bare number "911" cannot be correct for both -
> always disambiguate by file suffix (`_timer_speedup_crystals_all_channels` vs `_unified_queue_screen`).

# WORK ORDER 911 — Timer speed-ups actually available (crystals + ads, all channels)

**Status:** FIXED - awaiting owner felt-verify. ⛔ **This ticket's banner was 16 DAYS STALE and would have sent a seat to rebuild working code.** It claimed *"there is no `TryAdSkip` in `BuildTimerService`"* (reconciled 2026-08-08). Verified at HEAD 2026-08-24: a **channel-aware pair exists** - `BuildTimerService.cs:880` `CanWatchAdToSkip(string)` and `:888` `CanWatchAdToSkip(ChannelId, string)`, with `WatchAdToSkip` beside it - plus `Village/Monetization/AdGateService.cs` and a covenant regression over the placements. ⚠ Read the tree, not the banner.
>  PRIOR: **Status: PARTIAL — crystal channel DONE, ad channel NOT** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-05 (CLI / Grok — owner: “nothing is available as a speed up”)  
**Silo:** Queue / Economy / Store (HUD presentation + BuildTimerService)  
**Roles:** CLI implement  
**Depends on:** existing `BuildTimerConfig` InstantFinish + AdSkip; PackStore crystals grants  

---

## Owner problem (proven in code)

Player waits on build / train / research and **never sees a usable speed-up**.

### What already exists (half-built)

| Piece | State |
|-------|--------|
| `BuildTimerConfig` | `instantFinishCrystalsPerMinute`, `instantFinishMinCrystals`, `adSkipSeconds`, daily ad cap |
| `BuildTimerService.TryInstantFinish(structureId)` | Spends **GameState crystals**, completes job |
| `BuildTimerService.WatchAdToSkip(structureId)` | Rewarded ad → skip chunk |
| `ObsidianQueueHud` Instant / Ad buttons | Only on **ACTIVE Builder** jobs |
| Packs | Many grant **crystals** + convenience kinds like `instant-build` (tokens **not** consumed for timers yet) |

### Why nothing shows / works

1. **Channel lock — Builder only**  
   `InstantFinishPrice` / `TryInstantFinish` / `CanWatchAdToSkip` / `WatchAdToSkip` resolve jobs via **Builder channel + structureId only**.  
   Comment in `ObsidianQueueHud`: *Train/Research jobs never resolve InstantFinish by structureId*.

2. **UI hides the row when both fail**  
   ```text
   if (price <= 0 && !adOk) return;  // no Instant button, no Ad button
   ```

3. **Queued jobs** (`StartMs <= 0`) → price 0, ad false — no CTA while waiting for a free slot.

4. **Ads** need `RewardedAdManager.Instance` + `IsAdReady` — often **false** in Editor / non-store builds → Ad button never appears.

5. **Crystal packs** can grant crystals, but if Instant never shows, crystals feel useless for waits. Convenience `instant-build` tokens are **not** applied as timer finishes (ApplyPackContents records pack ownership only).

**Not a missing currency type.** Use **crystals** (already HUD + pack economy). Do **not** invent a second premium currency.

---

## Product north star (CoC / mobile)

| Free path | Paid path |
|-----------|-----------|
| Wait | **Crystals** → finish now (price scales with remaining time) |
| Optional **Ad** → shave a fixed chunk (store builds only) | Buy **crystal packs** in Realm Store to fund finishes |

Covenant: **time convenience only** — never combat power.

---

## Scope

### Phase A — Engine: all channels (required)

Extend BuildTimerService (names can match house style):

```text
InstantFinishPrice(ChannelId ch, string jobId)
TryInstantFinish(ChannelId ch, string jobId)
CanWatchAdToSkip(ChannelId ch, string jobId)
WatchAdToSkip(ChannelId ch, string jobId)
```

- Find **active** job on that channel by `StructureId` (job key already used for train/research ids).  
- Price = `Config.InstantFinishPrice(RemainingSeconds)`.  
- Spend crystals via existing `GameStateService.AddCrystals(-price)` (or single wallet seam used today).  
- Complete via `CompleteChannelJob(ch, jobId)` (already exists).  
- Keep structureId-only Builder overloads as wrappers for back-compat.

**Queued jobs:** either  
- **V1:** no Instant until active (document), **or**  
- **V1.1:** allow Instant on queued at price of full duration (preferred for “I don’t want to wait in line”).

### Phase B — HUD: always show sell-time CTAs when a job is active

`ObsidianQueueHud.AddJobActionRow`:

- Pass **channel** of the job (Builder / Train / Research).  
- Show **Instant `Nc`** whenever `price > 0` for that channel+id.  
- Show **Ad** when `CanWatchAdToSkip` (store/editor stub — see Phase C).  
- If crystals &lt; price: still show button dimmed or toast “Need more crystals — open Store”.

Optional: same Instant/Ad on building focus chip when structure is mid-upgrade (if a timer chip exists).

### Phase C — Ad path honesty

| Build | Behavior |
|-------|----------|
| Store + ads ready | Real rewarded ad → skip |
| Editor / no ad SDK | **Dev stub:** treat “Ad” as free skip chunk once per day **or** hide Ad and only show crystals (owner pick — default **hide Ad when !IsAdReady**, show crystals always) |

Never show a dead Ad button with no feedback.

### Phase D — Crystal packs (store shelf, data-first)

**Do not invent a new currency.** Crystals already grant from packs.

1. Audit `packs.json`: ensure at least **2–3 crystal-forward packs** readable in UI (name/tagline say crystals for timers).  
2. Optional **pure crystal SKUs** (if tier slots free): e.g. small/med/large crystal only packs ($0.99 / $4.99 / $9.99) — data only + dual-copy.  
3. After Instant fails for unaffordable: toast + optional **“Get crystals”** → `PackStoreBootstrap.OpenRealmStore` if available.  
4. Convenience tokens `instant-build` / `instant-repair`: **either**  
   - **Wire** as free instant finishes (consume count from state inventory), **or**  
   - **Stop advertising** until wired — do not leave dead pack copy. Prefer wire simple consumable counter on GameState if cheap.

### Phase E — Proof

- EditMode or headless: active Train job → InstantFinishPrice &gt; 0; TryInstantFinish completes and crystals decrease.  
- Builder upgrade same.  
- Research same if research jobs exist.  
- FlowTrace on refuse (no crystals / not active / ad not ready).

---

## Acceptance

- [ ] Active **Builder**, **Train**, and **Research** jobs show **crystal Instant** in Work / queue panel when price &gt; 0  
- [ ] Tapping Instant with enough crystals finishes that job immediately  
- [ ] Insufficient crystals → clear toast; Store open path if wired  
- [ ] Ad button only when actually available (or honest editor stub)  
- [ ] No new currency type  
- [ ] Packs still grant crystals into live wallet used by Instant  
- [ ] COMPILE_GATE_OK + brace-check every .cs  
- [ ] PO felt: “I can always pay crystals to skip a wait I care about”

---

## Do NOT

- Combat power for cash  
- Forced ads  
- Second premium currency  
- Rewrite queue engine — only extend price/complete APIs + HUD  

---

## Paste for Claude / CLI

```text
Implement WORK_ORDER_911_timer_speedup_crystals_all_channels.md.
Owner: nothing available as speed-up. Root cause: Instant/Ad only resolve Builder jobs;
Train/Research and unavailable ads hide all CTAs. Extend InstantFinish/AdSkip to all
channels; show crystal Instant on active jobs; hide dead Ad buttons; use existing
crystals + pack grants (optional pure crystal packs). No new currency. COMPILE_GATE_OK.
```

---

## One-line truth

**Speed-ups already exist for Builder crystals — wire them to every channel and always show Instant when a job is running; sell crystals in the store you already have.**
