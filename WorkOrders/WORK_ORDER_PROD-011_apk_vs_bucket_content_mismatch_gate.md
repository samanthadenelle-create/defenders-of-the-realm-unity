# PROD-011 — NOTHING gates an APK-vs-bucket content mismatch (plus the RETRY resilience fix — a TIMEOUT was ruled out on evidence, §3)

**Status:** IMPLEMENTED 2026-08-18 (`1eec315c7`) — `--verify-catalog` + content-aware `--push` + the corrected docstring + `m_RetryCount: 0 → 2` on **both remote groups**; wired into the ship chain at `morning-ship-chain.ps1:130-147`; `R2_PARITY_OK 4 object(s) verified` on a live build (`docs/proof/2026-08-18-overnight-gear-structures/README.md:33`). **`m_Timeout` stays 0 BY DECISION — that is the finished answer, not missing work; see §3.** ⚠ ONE NEW DEFECT FOUND 2026-08-19 WHILE VERIFYING (see the RESULT, and WO-1124): `ServerData/` now holds TWO platform folders, so the chain's argument-less `--verify-catalog` hits `_detect_target`'s `sys.exit` (`tools/r2_sync.py:250-256`) and the ship chain dies before it can ever print a marker. — AWAITING OWNER FELT-VERIFY / PO CLOSE (§13).
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** HIGH — a mismatch ships invisible content to every player and is currently caught only by hand.
**Silo:** Release tooling. **Lane:** `tools/r2_sync.py` + the Addressables group schemas. No gameplay code, no scenes.
**Provenance:** 2026-08-18 release. Prior art: commit `16e22dba3`.
**Cross-ref:** **PROD-009** depends on the RETRY half of this ticket (§3) — more bundles means more
requests, and a single dropped request was a permanent miss. **The TIMEOUT half is a closed decision
(NO timeout), not outstanding work — read §3 before touching a schema.**

---

## 1. What happened tonight

The APK's new `structure_art_..._7608a3cb` bundle **did not exist in R2**, and the enemy bundle from
the morning build **had never been uploaded at all**. Both were caught **only by hand**.

A repo-wide search confirms **no gate exists** for this. Commit `16e22dba3` already conceded it:
*"NO GATE COULD HAVE CAUGHT THIS"*.

The failure mode is the worst kind: the build is green, the upload command exits 0, and the player
gets **invisible buildings and missing enemies**. It is loud in our harness
(`StructureAssetLoader.cs:139` `FlowTrace.Fail`, error severity, names the address) and **silent to the
player**.

---

## 2. The gate that would work

Parse the **built catalog's remote `m_InternalId`s**, `list_objects_v2` the bucket, **diff**. Fail on
any bundle the catalog references that the bucket does not hold.

**Every input already exists in `tools/r2_sync.py`** — credentials from `.env.r2`, the client, the
listing. This is a new subcommand, not new infrastructure.

### Three sharp edges in the existing tool, to fix or document in the same change

1. ⛔ **`--push ServerData/Android` FLATTENS to the bucket root and 404s in game** — that is the
   `16e22dba3` bug, and **`tools/r2_sync.py`'s docstring at `:21` STILL documents the wrong form**:
   ```
   python tools/r2_sync.py --push ServerData/Android
   ```
   Fix the docstring in the same commit. A tool whose own usage text teaches the failure will keep
   producing it.
2. **`--push` skips by SIZE, not hash**, and `catalog_*.hash` is **always exactly 32 bytes** — so a
   reused `bundleVersion` **silently skips** the very file that says which content is current.
3. **`--check` proves credentials only.** It never proves that *your catalog's* bundles are present.
   It is not, and must not be treated as, a content gate.

---

## 3. The resilience fix — RESOLVED 2026-08-18 (`1eec315c7`). RETRY: YES. TIMEOUT: NO, DELIBERATELY.

> ### ⛔ DO NOT "FINISH" THIS SECTION BY ADDING `m_Timeout`. THE ZERO IS THE ANSWER.
> This section originally asked for *"a small non-zero retry count **and a real timeout**"*. **The retry
> half shipped. The timeout half was CONSIDERED AND REJECTED ON EVIDENCE**, and the reasoning was left in
> the commit body instead of here — which made the ticket read as half-done. It is not. A seat that
> "completes" it by setting a timeout would cause an outage on exactly the connections this ticket set out
> to protect.

### What shipped

| Schema | `m_RetryCount` | `m_Timeout` | Why |
|---|---|---|---|
| `Schemas/Structure_Art_BundledAssetGroupSchema.asset:36` | **2** (was 0) | `:33` **0** — deliberate | REMOTE group; a dropped request now retries twice |
| `Schemas/Enemy_Art_BundledAssetGroupSchema.asset:36` | **2** (was 0) | `:33` **0** — deliberate | REMOTE group; same |
| `Schemas/Default Local Group_BundledAssetGroupSchema.asset:36` | **0**, unchanged **on purpose** | `:33` 0 | **LOCAL group — it never touches the network.** A retry count on it would be noise pretending to be safety. This corrects §4.5 below, which asked for non-zero on "all three schemas". |

*(Values above re-verified at HEAD on 2026-08-19 by reading the three `.asset` files directly.)*

### ⛔ WHY `m_Timeout` IS AND STAYS 0 — the numbers, measured on disk

`m_Timeout` in a `BundledAssetGroupSchema` is a **whole-request** ceiling on a `UnityWebRequest`, not an
idle/connect timeout. The remote payload is enormous, so any value small enough to rescue a genuinely
dead socket is far smaller than a healthy download on a slow phone:

| Measured, `ServerData/Android/`, 2026-08-19 | bytes | at 5 Mbps | at 1.5 Mbps |
|---|---|---|---|
| `enemy_art_assets_all_2d9daff5….bundle` | 67,582,523 (64.5 MiB) | ~108 s | ~360 s |
| `structure_art_assets_all_7608a3cb….bundle` | 20,670,596 (19.7 MiB) | ~33 s | ~110 s |
| **both, i.e. a first-run cold fetch** | **88,253,119 (84.2 MiB)** | **~141 s** | **~471 s** |

*(Derivation, so it can be re-checked rather than believed: bytes × 8 ÷ bitrate. The **141 s / 471 s**
figures quoted in `1eec315c7`'s body are the **combined** remote set, not the enemy bundle alone — recorded
here because the mis-attribution is easy to repeat.)*

So a "sane-looking" 60 s or 120 s timeout **aborts a perfectly healthy download** on 5 Mbps, and anything
above ~471 s is long enough that the OS/socket layer has given up first — i.e. it buys nothing a dead
connection would not already surface. **A timeout here would CREATE the failure it was added to prevent,
and it would do so preferentially for players on poor mobile data — the exact population the retry was
for.** `m_RedirectLimit: -1` and the retry are the resilience; the clock is not.

**If this ever needs revisiting, the right change is NOT a timeout — it is smaller bundles** (that is
**PROD-009**, per-family splits). Shrink the unit of work first; only then is a per-request ceiling even
discussable. Do not re-open this as a schema edit.

## 4. Acceptance criteria

1. A new `r2_sync.py` subcommand (e.g. `--verify-catalog <ServerData path>`) that parses the built
   catalog's remote `m_InternalId`s, lists the bucket, and **exits non-zero** naming every missing
   object.
2. **Proved in both directions:** run it against tonight's known-bad state (or a reconstructed one) and
   it FAILS naming `structure_art_..._7608a3cb` and the enemy bundle; run it against a good state and
   it passes.
3. The `:21` docstring is corrected to the form that does **not** flatten.
4. `--push` no longer skips a same-size-different-content file (hash compare, or always re-push the
   `catalog_*.hash`), with the 32-byte case called out in a comment.
5. ~~`m_RetryCount` / `m_Timeout` non-zero on all three schemas.~~ **SUPERSEDED BY §3 (2026-08-18):**
   `m_RetryCount: 2` on the **two REMOTE** groups; **`m_Timeout` stays 0 on all three**, and the LOCAL
   group keeps `m_RetryCount: 0`. Meeting this criterion as originally written would be a REGRESSION.
6. The verify step is wired where a human cannot forget it — the release path, not a doc.

## 5. What NOT to touch

- Do not change what is remote vs local (PROD-010 §1).
- Do not change bundle grouping here — that is PROD-009, and it must land as one deliberate change.
- Do not treat `--check` as the gate (§2.3).
