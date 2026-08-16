# WORK ORDER 1110 — The raid's one softlock hatch, its silent catches, and the death-exit loot inversion

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1109 -> 1112 in the SAME edit (1109, 1111 minted alongside)
**Lane:** Raid runtime (`RaidDeployController`, `RaidScoring`, `RaidSelectionScreen`, `RaidDeployVM`,
`RaidDeployScreen`, `HeroHealth`). Disjoint from WO-1109's hero-lifecycle files.
**Provenance:** SME readiness audit of the raid pillar, 2026-08-16.

---

## 1. The softlock — low probability, TOTAL consequence

```
RaidDeployController.cs:150-155
  _camera = Camera.main;
  BuildHud();                            // <-- NOT wrapped in Guard.Try
  StartCoroutine(BindScoringRoutine());  // <-- the 180s clock-expiry subscriber
```

If `BuildHud()` throws, the `StartCoroutine` line never runs. The player then has:
- **no deploy tray**, **no Retreat button**, and
- **no `OnTimeExpired` subscriber** — so `RaidScoring.cs:536`'s `OnTimeExpired?.Invoke()` fires into
  nothing and the 180 s clock does not rescue them.

Remaining exits: die, or kill the application. **This is the only state in the raid with no exit.**

⚠ **Every other risky op in this same file is already `Guard.Try`-wrapped.** This one line is the
exception, which is what makes it a genuine miss rather than a house style.

**Fix:** wrap `BuildHud()` in `Guard.Try` (`CLAUDE.md` §12 — one bad object logs and is skipped, never
blanks a screen), and — more importantly — **subscribe the clock BEFORE building the HUD**, so the
timeout exit cannot be taken out by a presentation failure. The exit hatch must not depend on the UI.

## 2. Silent failures — §12 says a catch that swallows without logging is FORBIDDEN

| Site | What is swallowed | Player-visible consequence |
|---|---|---|
| `RaidDeployVM.cs:356` | bare `catch { return "Army: -"; }` | army readout silently reads as unknown |
| `RaidScoring.cs:634` | bare `catch { }` in `ResolveRewardMultiplier` | ⚠ **a catalog miss silently pays x1 instead of x2.2** on mage_enclave — the player is underpaid with no trace line |
| `RaidSelectionScreen.cs:274` | `if (def == null) return;` | a card tap that resolves nothing is a **dead tap** — no toast, no log |
| `RaidDeployScreen.cs:75` | `Open(null)` logs a warning only | no player feedback at all |

`RaidScoring.cs:634` is the one that costs real money-equivalent: **a 2.2x multiplier silently
becoming 1x is a 55% pay cut the player cannot see and no trace records.** Fix that one first.

Each gets a `FlowTrace.Warn`/`Fail` and, where the player is affected, a toast. Do not convert these
to thrown exceptions — log and continue.

## 3. The death exit forfeits loot that retreating pays — an inverted incentive

- Retreat and timeout both route through `RaidDeployController.cs:463-469` → `RaidScoring.Finalize` +
  `GrantRetreatLoot` → **partial loot paid**.
- Hero death (`HeroHealth.cs:837-872`) reconciles the army at 0 stars and saves, but **never calls
  `RaidScoring.Finalize` / `LootFor`** → **razing credit already earned is forfeited**.

So a player who razes two thirds of a base and then dies gets less than one who razes the same and
taps Retreat. That is the *inverse* of the perverse incentive the retreat-loot block was written to
remove, and it punishes the more committed play.

**Owner decision embedded here — flagging, not presuming:** should death pay the same partial loot as
retreat, or should death carry a deliberate penalty (say, partial loot at a reduced rate)? **Default
if unruled: pay the same as retreat**, on the grounds that the loot is credit for damage already done.

## 4. ⚠ A known exploit, recorded not fixed

The army is only mutated at `ReconcileRaidEnd`, so **quitting mid-raid writes nothing** — the player
can quit to avoid wounding their troops entirely. This is a design hole, not a crash, and closing it
is a separate decision (mid-raid checkpointing vs. an on-entry wound commit). Stated so it is not
rediscovered as a bug later.

## 5. Acceptance

- A forced `BuildHud()` throw still leaves the player able to exit the raid (clock or Retreat) —
  proven by a deliberate fault injection, not by reading the diff.
- Every catch listed in §2 emits a trace line; the reward-multiplier miss is visible in a capture.
- Dying mid-raid pays per the §3 ruling, and a regression pins it so the two exits cannot drift apart
  again.
- No instrumentation removed (§12).
