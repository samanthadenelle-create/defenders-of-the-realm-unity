# PROD-012 — Is an internet connection REQUIRED on first run? An OWNER DECISION, not a defect

**Status:** FIXED 2026-08-25 - source acceptance landed at `c3b40829b`; awaiting owner Seeker reconnect felt-test and OPS dApp Store listing update.
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** MEDIUM — but it gates a **store-listing** claim on a LIVE published app, so it cannot sit indefinitely.
**Silo:** Product / content delivery policy. **Lane:** decision first; implementation scope depends entirely on the ruling.
**Provenance:** 2026-08-18 CDN migration review.
**Cross-refs:** PROD-009 (per-family on-demand), PROD-010 (first-run signal + opt-in offline download).

---

## 1. Why this is a decision and not a bug

Moving structures and enemies to the CDN **deleted `Assets/Resources/Structures` and
`Assets/Resources/Enemies`** (verified: neither directory exists). The loaders' design is
**Addressables-first with a Resources fallback** — with those folders gone, **the fallback chain has no
second tier**.

Consequence: **a first run with no connection has no buildings and no enemy models.**

That is not, by itself, wrong. It is a **product decision the owner has never been asked to make**, and
this ticket exists so she makes it deliberately rather than discovering it from a review.

## 2. The nuance the owner supplied, and it is CORRECT

**Bundles cache.** `m_UseAssetBundleCache: 1` on both remote group schemas (`:29`). So this is a
**FIRST-RUN (per build) requirement, not a per-launch one**. A player who completes one connected
session plays offline afterwards, until the next app update re-hashes the bundles.

## 3. The miss is LOUD to us and SILENT to the player

`Assets/_Modules/Core/Addressables/StructureAssetLoader.cs:139`:

```
structure asset '<address>' (<Type>) not found via Addressables OR Resources —
the structure will render NOTHING.
```

Error severity, names the address, once per key, with the deliberate note above it that
**no synth fallback exists for a building** — an unresolved structure is *"an INVISIBLE BUILDING the
player has paid resources for"*.

So we will always know. **The player will just see nothing.** That asymmetry is the reason a decision
is owed.

---

## 4. THE DECISIONS OWED (owner only — do not answer these in code)

1. **Does the game declare "internet required" in the Solana dApp Store listing?** It is a published
   app; the listing is a claim we are currently making by omission.
2. **Does a no-connection first run get an honest screen?** i.e. *"you need a connection to finish
   setting up"* with a **retry**, instead of a town of invisible buildings. (If yes, this is small and
   pairs naturally with PROD-010's surface.)
3. **Is a minimal offline floor wanted?** A tiny local fallback set so a disconnected first run shows
   *something* rather than nothing. ⛔ **This is the ONLY one of the three that would justify
   duplication** — and duplication carries the PROD-010 §1 hazard (already-installed APKs adopt the new
   remote catalog, so a local path that does not exist in shipped builds = invisible buildings for
   existing players). If she wants a floor, it must be designed against that hazard, not bolted on.

## 5. What happens after the ruling

- Ruling (1) → a listing edit, no code.
- Ruling (2) → a small UI change on PROD-010's surface.
- Ruling (3) → a real content change, its own work order, and a forced re-download to plan for.

**Do not pre-build any of the three.** Park this ticket with the questions surfaced so unblocking is
one owner word.

## 6. What NOT to do

- Do not restore `Assets/Resources/Structures` / `Assets/Resources/Enemies` speculatively.
- Do not add a placeholder/synth building fallback — the loader's header rules that out deliberately
  for structures, and quietly showing a wrong model is worse than showing none.
- Do not answer §4 on the owner's behalf in a RESULT file.

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 1: **YES, internet IS required.** All three answered.

**Recorded by the UI seat from `OWNER_RULINGS_OWED_2.md` §1. Do not re-decide.**

1. ✅ **Declare it on the dApp Store listing.** We currently make the opposite claim by omission.
2. ✅ **An honest setup screen with a RETRY, never a broken town.**
   ⭐ **Her exact wording — ship this string, do not paraphrase it:**
   > **"An internet connection is required to finish setting up Elarion."**
   Her reason: *"better than scary error-code soup."*
3. ⛔ **NO duplicated offline asset floor.** Ruled out — it means duplicating content, and duplication
   carries the PROD-010 hazard (already-installed APKs adopt the new remote catalog, so a local path
   absent from shipped builds gives *existing* players invisible buildings).

⚠ Scope reminder from the ticket body, unchanged by this ruling: bundles cache, so this is a
**first-run-per-build** requirement, not a per-launch one.

**Status → READY.** Nothing further is owed by the owner on this ticket.
