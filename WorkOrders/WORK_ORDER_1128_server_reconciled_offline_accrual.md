# WORK ORDER 1128 — Server-reconciled offline accrual: stop trying to verify the client, make it not matter

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-20 (CLI seat) — banner bumped 1128 → 1129 in the SAME edit
**Lane:** Backend (`api/game/*`) + `GameStateService` sync + the offline opt-in panel copy.
No gameplay balance, no economy retuning, no scenes.
**Provenance:** owner, 2026-08-20, verbatim: *"the real question would be, would we be able to verify
that their offline data was valid? Uh, how would we do things like getting resources, uh, from their
pets harvesting while they're offline."* and *"but then the save would only persist locally right?"*

---

## 1. THE ANSWER THIS TICKET IMPLEMENTS

**You cannot verify a client's offline data, and you should not try. You make it not matter.**

Anti-cheat that inspects client state is a race you lose on a rooted device. The winning shape is
different: **the server never accepts a client number as authority for anything it can derive
itself.** Offline play stays fully playable; the economy just reconciles when the player reconnects.

Split what offline play produces — the two halves need opposite treatment:

| kind | examples | treatment |
|---|---|---|
| **TIME-DERIVED** | collector accrual, build/train/research timers, Echo harvest | **Server recomputes** from its own clock. The client's number is a DISPLAY ESTIMATE and is never the value that lands. |
| **ACTION OUTCOME** | battles won, loot rolled, XP earned | Cannot be recomputed without re-simulating the game. **Out of scope** — see §5, and do not pretend otherwise. |

## 2. WHAT ALREADY EXISTS — verify before you build (§12)

`ResourceCollector.CatchUpAway` (WO-859) is already hardened on one axis, and the code says so:

```csharp
double awaySec = (nowMs - _lastAccrualMs) / 1000.0;
if (awaySec < 0.0)   // clock set BACKWARDS
{
    // "clamped to 0, no accrual, no re-claim"
    awaySec = 0.0;
}
if (awaySec > MaxAwaySeconds) awaySec = MaxAwaySeconds;  // int-overflow guard, NOT the design cap
```

A fresh collector seeds `_lastAccrualMs` to now and back-fills nothing. A backwards clock earns zero
and cannot re-claim. `OfflineHarvestService` mirrors the same arms.

**⚠ AND NOTE WHAT DOES *NOT* NEED FIXING:** `PetHarvester` runs on `Time.time` — **session time**. Pets
do not harvest while the app is closed at all. The owner's question implies they might; they do not.
Confirm this at source before designing anything for it, and do NOT add offline pet accrual as part
of this ticket — that is a design change, not a security fix.

**THE OPEN HOLE IS A FORWARDS CLOCK.** `TimeSource.NowUnixMs()` is device time. Set the clock ahead,
relaunch, collect. What bounds the damage today is **container capacity** — a collector fills to its
limit and stops, so the ceiling is one full container rather than unbounded resources.

**That is a design property silently doing security work.** It holds only while capacity stays
finite and small. Nobody chose it as a defence; it is load-bearing by accident, and that is exactly
the kind of thing this project has been burned by all week. Write it down as a deliberate property
or replace it — do not leave it implicit.

## 3. WHAT TO BUILD

**3.1 — Server stamps `last_seen` per player.** `api/game/save.js` records the authoritative UTC at
which the server last accepted a save. Note `last_seen` already exists as a column concept elsewhere
(`api/admin/cleanup.js`, `api/admin/db.js`) — reuse the convention, do not invent a second one.

**3.2 — Server recomputes time-derived accrual on load/sync.** On `api/game/load.js` (or a new
reconcile step in save), the server computes `awaySec` from **its own** `NOW()` minus the stored
`last_seen`, derives what the time-derived systems should have produced, and **caps the incoming
client claim at that value**. Client claim higher → clamp to the server figure and log it. Lower →
accept the client's (never pay a player more than they claim; that is a bug, not generosity).

**3.3 — The client stops treating its own accrual as final.** `GameStateService` sync applies the
server's reconciled figure. The offline number stays for display so the game feels responsive; it is
provisional until sync.

**3.4 — Say the local-save truth in the panel.** `OfflineOptInPanel` must state that while offline the
save lives **only on this device** until reconnect, so a lost or wiped device loses that progress.
The owner asked *"but then the save would only persist locally right?"* — a player deserves the same
answer she did, in the same screen that offers the mode. ASCII only, no meaning by colour.

**3.5 — Make the capacity dependency explicit.** Wherever the forwards-clock exposure is bounded by
container capacity, say so in a comment at that site, naming that the cap is what bounds it. If a
future ticket raises storage caps without knowing this, the hole widens silently.

## 4. DO NOT

- Do NOT add offline PET harvesting. Pets are session-time today (§2); adding away-accrual is a
  design change and needs an owner ruling, not a security ticket.
- Do NOT weaken or remove `CatchUpAway`'s backwards-clock clamp. It is correct and it is the
  precedent this builds on.
- Do NOT block offline play on a server round-trip. The whole point is that the game runs without a
  connection; reconciliation happens when one returns.
- Do NOT invent an anti-tamper scheme that inspects the device (root detection, clock attestation).
  That is the race this ticket exists to avoid entering.
- Do NOT re-open PROD-009 (per-family on-demand shrink). The owner killed it; PROD-010 supersedes.

## 5. HONEST SCOPE — what this does NOT solve

**Action outcomes remain client-authoritative.** A player who edits a local save to add a won battle,
loot roll or XP cannot be caught by this, and catching them would require server-side simulation of
combat — a different and far larger project.

Why that is acceptable *today*, stated so the next reader can re-judge it when it stops being true:
this is a PvE game with **no trading, no PvP ladder and no player-to-player economy**, so the
blast radius of a cheater is their own save. It stops being acceptable the moment any of those three
things change — most likely via the monetization program (WO-1117), where inflated resources
devalue real purchases.

## 6. ACCEPTANCE CRITERIA

1. Server stamps `last_seen` on every accepted save.
2. A client claiming MORE time-derived accrual than the server's own clock allows is **clamped to the
   server figure**, and the clamp is logged with both numbers.
3. A client claiming less is accepted unchanged.
4. A player who genuinely plays offline for N hours and reconnects receives the correct accrual — the
   reconciliation must not punish honest offline play. Prove this case explicitly; it is the one that
   makes the feature worth having.
5. Offline play still works with no connection at all — no server round-trip is on the critical path.
6. `OfflineOptInPanel` states the local-only save consequence.
7. A regression asserts the clamp both directions: an over-claim FAILS to land in full, and an honest
   claim lands untouched. A gate that does not fail the known-bad state is not a gate.
8. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` (read the count off the marker — it is 222/222 as of
   2026-08-20, the first fully-green run; do not let this ticket be the one that re-reds it).
9. Owner felt-verifies: play offline, reconnect, confirm resources are right (PO closes, §13).

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `api/game/ only load.js,save.js` — server accrual endpoint unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
