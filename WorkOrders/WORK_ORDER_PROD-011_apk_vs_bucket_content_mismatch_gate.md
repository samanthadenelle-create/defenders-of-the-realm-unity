# PROD-011 — NOTHING gates an APK-vs-bucket content mismatch (plus the retry/timeout resilience fix)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-18 (docs seat) — PROD series.
**Priority:** HIGH — a mismatch ships invisible content to every player and is currently caught only by hand.
**Silo:** Release tooling. **Lane:** `tools/r2_sync.py` + the Addressables group schemas. No gameplay code, no scenes.
**Provenance:** 2026-08-18 release. Prior art: commit `16e22dba3`.
**Cross-ref:** **PROD-009** depends on the retry/timeout half of this ticket (§3) — more bundles means
more requests, and a single dropped request is currently a permanent miss.

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

## 3. The cheap resilience fix — bundle it here

`m_RetryCount: 0` and `m_Timeout: 0` (schema lines `:36` and `:33`) on all three groups:

- `Assets/AddressableAssetsData/AssetGroups/Schemas/Structure_Art_BundledAssetGroupSchema.asset`
- `Assets/AddressableAssetsData/AssetGroups/Schemas/Enemy_Art_BundledAssetGroupSchema.asset`
- `Assets/AddressableAssetsData/AssetGroups/Schemas/Default Local Group_BundledAssetGroupSchema.asset`

**One dropped request is currently a permanent miss for that session** — no retry, no timeout. On a
phone on mobile data that is not an edge case.

Set a small non-zero retry count and a real timeout. **PROD-009 makes this mandatory**: splitting into
per-family bundles multiplies the number of requests, so it multiplies the exposure.

---

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
5. `m_RetryCount` / `m_Timeout` non-zero on all three schemas.
6. The verify step is wired where a human cannot forget it — the release path, not a doc.

## 5. What NOT to touch

- Do not change what is remote vs local (PROD-010 §1).
- Do not change bundle grouping here — that is PROD-009, and it must land as one deliberate change.
- Do not treat `--check` as the gate (§2.3).
