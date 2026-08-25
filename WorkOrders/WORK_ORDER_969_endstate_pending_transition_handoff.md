# WORK ORDER 969 — Opening Pause over the victory summary destroys the pending home return  — **OWNER CLOSED 2026-08-22** (felt-verified by the owner; PO closes, section 13).

**Status:** FIXED — shipped `9e07db86` ("fix(arena): WO-969"); owner felt-verify owed (PO closes, §13). RESULT file still owed (not fabricated). *(Status corrected 2026-08-14: the line still read READY after the commit landed.)*

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*

> ### VERIFIED AT SOURCE 2026-08-22 - **the oracle IS registered; this ticket's note saying registration is still owed is WRONG**
> `Assets/Editor/Regression/DataRegression.cs:877` registers the `endstate-handoff suite` and logs `[endstate-handoff]`.
> The suite is `Assets/Editor/Regression/EndStateTransitionHandoffRegression.cs` (tag declared at `:2`, failure
> string at `:105`). Nothing about registration is outstanding.
> Owner felt-verify still owed (PO closes, CLAUDE.md 13).

**Silo:** UI / Arena return path
**Source:** Owner F8 seq **2315**, scene `Dungeon_HealersCottage`, 2026-08-10
**Stage:** CLI implemented + edit-only (no Unity run, no gate, no commit in this lane)
**Fence honoured:** did not touch `HeroLocomotion`, `DungeonHero`, `HeroGaitForensics`, `SmartMobileCamera`, `HeroBodySwapper`, `HudModelProducers.cs`, the dialogue JSONs, or `DataRegression.cs`.

---

## 1. The capture (PROVEN-BY-CAPTURE — owner's live Player.log, verbatim, in order)

```
[Flow:UI] PanelManager: 'Pause' opened and verified visible (IsOpen=true).
          DeNelle.Core.UI.PanelManager:NotifyOpened      (PanelManager.cs:181)
          DeNelle.Core.HudModel.PostureSignals:SetEndState(bool)  (PostureSignals.cs:127)
          DeNelle.Village.UI.EndStateView:OnDestroy ()   (EndStateView.cs:1665)
```

```
[BREAK] error: [Flow:BattleArena] STRANDING WATCHDOG FIRED after 45s - the victory panel was destroyed
        without firing its Continue action, so the deferred home return never ran. Returning the hero
        anyway. If you are reading this, find WHAT destroyed the end-state (a wave banner or another
        modal opening over it) - the watchdog is a safety net, NOT the fix.
        (BattleArena.cs:2495)
```

The watchdog's own text is the acceptance bar for this WO: **it is not the fix.**

---

## 2. RCA — the mechanism, every step READ-AT-SOURCE

| # | Fact | Evidence |
|---|---|---|
| 1 | The full (non-compact) end-state joins the single-modal arbiter as a battle-allowed handle whose Close is `CloseFromArbiter`. | READ-AT-SOURCE `EndStateView.cs:175-180` — `PanelManager.RegisterBattleAllowed("EndState", view.CloseFromArbiter, () => view != null)` then `NotifyOpened`. |
| 2 | Pause is ALSO battle-allowed, so the WO-437 battle-lock can never refuse it. | READ-AT-SOURCE `PauseController.cs:182` — `RegisterBattleAllowed("Pause", Resume, () => _paused)`. |
| 3 | `PanelManager` is a strict ONE-panel arbiter: admitting a new handle records it and then **invokes the previous handle's `Close`**. | READ-AT-SOURCE `PanelManager.cs:137-157`. The battle-lock branch (`:122`) is skipped for both handles. |
| 4 | `CloseFromArbiter` deliberately tears down **without** firing `Primary`, then `Destroy(gameObject)`. | READ-AT-SOURCE `EndStateView.cs:1640-1648`. |
| 5 | `OnDestroy` then runs `PostureSignals.SetEndState(false)` — the exact third frame of the captured stack. | READ-AT-SOURCE `EndStateView.cs:1650-1667`, `PostureSignals.cs:123-128`. |
| 6 | The arena's **only** route home, `doMaskedReturn`, was handed to that view as `Primary` and nowhere else. | READ-AT-SOURCE `BattleArena.cs:2345-2372` -> `BattleArenaHud.ShowResult` -> `EndStateVM.FromBattleVictory(..., Primary = onContinue)`. |
| 7 | Therefore the transition died with the GameObject, and only `StrandingWatchdog` (45 s) got the hero home. | PROVEN-BY-CAPTURE (the `[BREAK]` line above) + READ-AT-SOURCE `BattleArena.cs:2481-2502`. |

**Was the battle lock already released?** Yes, twice over. `BattleInProgress = false` is set at `BattleArena.cs:2451`, *after* `ShowResult` — so seconds before the owner ever touched Pause. And it would not have mattered: Pause is registered battle-allowed precisely so it can never be gated. **The end-state was not protected by the battle lock at the moment of the capture, and could not have been.**

**Does the end-state deserve the battle's protection?** No — it deserves something different, and that distinction is the whole fix. The end-state is **a pending state transition wearing a screen**. Protecting the *screen* is the wrong axis; the screen is genuinely displaceable (the player must always be able to pause). What must be protected is the **transition**, and the way to protect a transition is to stop storing it in a view.

---

## 3. The fix — shape (c), and why (a) and (b) are worse

The owner's read is **CONFIRMED**: (c) is the robust one.

**(c) — the deferred return is made independent of the panel's lifetime. CHOSEN.**
`EndStateVM` gains `Abandoned` plus `HandBackPendingTransition(reason)` — the latch lives on the **model**, not the view, because the entire point is that the transition outlives the view. The view calls it from **both** abandon choke points. The arena passes `doMaskedReturn` a second time as `onAbandon`, so the instant the screen dies the arena re-claims the transition and completes it. `returnStarted` already latches, so exactly one of Continue / hand-back / watchdog ever does anything.

The evidence that this is the general fix and the others are not: `BattleArena.cs:2381-2383` already enumerates **three** paths that destroy the end-state without firing, and all three funnel through `AbandonedPrimaryWarn` (`EndStateView.cs:98`, `:1612`, `:1645`). Hooking that choke point plus `OnDestroy` covers every path that exists **and every path nobody has written yet**.

**(a) — the end-state becomes a panel Pause cannot displace. REJECTED.**
`PanelManager` has no priority/undisplaceable concept at all (READ-AT-SOURCE, whole file, 240 lines — the only refusal axis is `BattleAllowed`). Adding one would mean *refusing Pause*, which contradicts the standing rule that the player must always be able to pause. It also leaves the other two destroy paths (a replacing `Show`, a scene load) untouched.

**(b) — Pause layers over the end-state without destroying it. REJECTED.**
`PanelManager` is architecturally one-panel-at-a-time (DEF-212, file header) and `AnyOpen` / `OpenPanelName` are single-valued; layering is a structural change to the arbiter with game-wide blast radius on every registered panel. And it fixes exactly **one** of the three destroy paths — the wave-banner `Show` replacement (`EndStateView.cs:96-101`) and `OnSceneLoaded` would still strand.

**Presentation nicety that ships with it:** the hand-back fires while the game is PAUSED (`PauseController` zeroes `Time.timeScale`) while every fade in `ReturnHomeWithFade` is UNSCALED — so without a gate the black fade and the 7 km warp would play out *under* the pause menu. `ReturnHomeWithFade` now holds while `Time.timeScale <= 0.0001f`, unscaled, hard-capped at `PausedReturnHoldCapSeconds = 300f` so a stuck timeScale can never become a new way to strand the hero. Gated on `timeScale`, not on `PauseController`, because `DeNelle.Village` must not reference `DeNelle.Settings`.

**The watchdog is untouched.** `StrandWatchdogSeconds` is still `45f`, the `FlowTrace.Fail` and its message are byte-identical, and the regression **pins all three** so a future seat cannot "fix" this by lengthening or quietening it.

---

## 4. Files changed

| File | Change |
|---|---|
| `Assets/_Modules/Village/UI/EndState/EndStateVM.cs` | New `Abandoned` action + `HandBackPendingTransition(reason)` (latch, trace, `Guard`) + `HandedBack`; `FromBattleVictory` takes `onAbandon`. |
| `Assets/_Modules/Village/UI/EndState/EndStateView.cs` | `SignalAbandon(reason)` delegating to the VM; called from `AbandonedPrimaryWarn` (all three known paths) and from `OnDestroy` (catch-all). |
| `Assets/_Modules/Village/Arena/BattleArenaHud.cs` | `ShowResult` plumbs `onAbandon` through to the factory. |
| `Assets/_Modules/Village/Arena/BattleArena.cs` | Passes `onAbandon: doMaskedReturn`; adds the paused-hold gate + `PausedReturnHoldCapSeconds`. Watchdog untouched. |
| `Assets/Editor/Regression/EndStateTransitionHandoffRegression.cs` | NEW suite (below). |

Brace-balanced, NUL-free, ASCII-only in player-visible strings (verified on all five).

---

## 5. Blast radius (owner asked)

- **Town: YES, it reproduces identically.** `PanelManager` is a `DeNelle.Core` static with no scene awareness, and town/overworld rep battles resolve through the same `BattleArena.Resolve` -> `ShowResult` path. Pause over a town arena victory summary was the same 45-second strand. Fixed by the same change (the fix is at the model/arena layer, not the scene).
- **Wave-clear banner + Pause: NOT a strand, verified.** `EndStateVM.FromWaveClear` and `FromOutpostVictory` are `Compact = true`, are deliberately **not registered** with the arbiter (`EndStateView.cs:175`), and carry **no `Primary`** — `PrimaryRoute = "dismiss"`. Nothing load-bearing to lose. The *reverse* direction is real and is fixed: a compact banner's `Show()` destroys an open full victory modal (`EndStateView.cs:96-101`), which is the originally-reported device case, and that path now hands back.
- **`WO-952` compact/EndStateView geometry: NOT DISTURBED.** No geometry constant, no `Bind`, no layout code was touched.
- **Follow-up, NOT in this WO's scope (named so it is not lost):** two other factories delegate a load-bearing action the same way — `FromGameOver(onRetry)` (`EndStateVM.cs:321`) and `FromRaidVictory(onReturn)` (`:358`). The hand-back mechanism is now available to both; wiring their owners is a separate ticket. (`Village2RaidController.cs:332` already mitigates its own case differently — it registers with `Close = ReturnHome`.) `FromHeroDeath` carries no `Primary` (respawn is `HeroHealth`'s own coroutine) and is not exposed.

---

## 6. Regression

`Assets/Editor/Regression/EndStateTransitionHandoffRegression.cs`
Entry: `public static bool Run(out string reason)` in `DeNelle.Editor.Regression`, shaped like `GearAuraCarryGateRegression`.
Markers: `ENDSTATE_HANDOFF_OK` / `ENDSTATE_HANDOFF_FAIL`.
Standalone: `run-unity-method DeNelle.Editor.Regression.EndStateTransitionHandoffRegression.RunAll`

Three cases:
1. **`handback-contract` (LIVE model)** — a real `FromBattleVictory` VM: hand-back runs exactly once, **never** invokes `Primary`, is a permanent no-op once Continue has fired, and is a no-op when nothing was delegated.
2. **`arbiter-displace` (LIVE arbiter)** — a real `PanelManager` sequence reproducing the captured step: an `EndState` handle open, a battle-allowed `Pause` admitted over it. Asserts Pause is **still admitted** (shape (a) stays rejected), the previous handle **is still closed** (DEF-212 intact), the pending transition **completes exactly once**, and `Primary` is **never** fired.
3. **`wiring-lint` (source, comment-stripped)** — both abandon choke points call the hand-back (method-body scanned, so it names *which* one is missing), `BattleArena` passes `onAbandon: doMaskedReturn`, and the watchdog is still 45 s with its `FlowTrace.Fail` and its `STRANDING WATCHDOG FIRED` message intact.

Not provable headlessly (owner felt-verify, PO closes per `docs/TICKET_PIPELINE.md`): that the hero visibly arrives home after pausing over a victory summary. The view itself cannot be exercised in edit mode — `Show` builds a canvas and the abandon paths call `Destroy`, which errors outside play mode — which is exactly why the latch was put on the model.

**Registration line for `DataRegression.RunAll` (lane-fenced — committer applies):**

```csharp
if (!EndStateTransitionHandoffRegression.Run(out var rEndStateHandoff)) failures.Add(rEndStateHandoff); else log.AppendLine("[endstate-handoff] " + rEndStateHandoff);
```

Tag: `[endstate-handoff]`

---

## 7. Acceptance criteria

- [ ] Compile gate green (`COMPILE_GATE_OK`).
- [ ] `ENDSTATE_HANDOFF_OK` on the standalone entry, and green once registered in `DataRegression.RunAll`.
- [ ] Owner felt-verify: win an arena fight, open Pause over the victory summary, close Pause — the hero returns home under a fade, and **`STRANDING WATCHDOG FIRED` never appears** in the break-log.
- [ ] The watchdog constant, its `FlowTrace.Fail` and its message are unchanged in the diff.

## 8. What NOT to touch

`HeroLocomotion`, `DungeonHero`, `HeroGaitForensics`, `SmartMobileCamera`, `HeroBodySwapper`, `HudModelProducers.cs`, the dialogue JSONs, `DataRegression.cs`, and any WO-952 EndStateView geometry.
