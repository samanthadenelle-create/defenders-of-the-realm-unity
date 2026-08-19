# RESULT — PROD-011 — the APK-vs-bucket content parity gate

**Verdict:** **LANDED AND PROVEN** (gate built, proven in BOTH directions, ran green on a live build).
⚠ **But it is currently BROKEN IN THE SHIP CHAIN by a change made after it landed — see §5.**
**Commit:** `1eec315c7` — *"feat(prod-011): a gate that proves the APK's content is actually hosted"*, 2026-08-18 21:41.
**Written:** 2026-08-19 by a read-only verification pass (HEAD `399bfb900`). No Unity, no push, no network call was made for this file — the §5 finding is from reading `tools/r2_sync.py` against the actual `ServerData/` tree on disk.

---

## 1. What was wrong

Twice on 2026-08-18 an APK was built whose remote bundle was **not in the bucket** — the new
`structure_art_..._7608a3cb` bundle, and the morning build's `enemy_art_..._2d9daff5`, which had **never
been uploaded at all**. Both were caught **by hand**. `16e22dba3` conceded it in its own body:
*"NO GATE COULD HAVE CAUGHT THIS."*

The failure is the worst kind: build green, upload exits 0, and the player gets **placeholder buildings and
missing enemies** — because `Assets/Resources/Structures` and `/Enemies` no longer exist as a fallback. It
is loud in our harness (`StructureAssetLoader.cs:139` `FlowTrace.Fail`, names the address) and **silent to
the player**.

## 2. What shipped — verified at HEAD

| Acceptance | Where | State |
|---|---|---|
| §4.1 `--verify-catalog` subcommand | `tools/r2_sync.py:259` `cmd_verify_catalog`, argparse at `:423-426`, dispatch `:434-435` | **done** |
| §4.3 docstring no longer teaches the flattening form | `tools/r2_sync.py:21` now reads `--push ServerData` with a `⛔ --push TAKES ServerData, NOT ServerData/Android` block, and `--push` warns at the moment you would make the mistake | **done** |
| §4.4 `--push` no longer skips a same-size-different-content file | `:205` skips only on `size == size AND md5_of(path) == ETag`; multipart ETags (`-N`) are treated as not-comparable and re-pushed (`:92-93`, `:105-107`); the always-32-byte `catalog_*.hash` trap is called out at `:177-182` | **done** |
| §4.5 retry/timeout | **superseded — see §4 below and the WO's rewritten §3** | **ruled** |
| §4.6 wired where a human cannot forget it | `morning-ship-chain.ps1:130-147` — step **2b**, AFTER the APK, BEFORE Firebase distribution, judged by the **marker on a fresh log**, never the exit code | **done** |

The gate's own design is stronger than the ticket asked for: it reads
`Library/com.unity.addressables/aa/<target>/settings.json` — the file Unity bakes into the player — to
decide **which catalog the player will actually request**, rather than trusting the newest file on disk;
requires that catalog to exist in `ServerData` **and byte-match** the Library copy (otherwise: mixed build
artifacts, refuse); and **refuses on a zero-length intersection** rather than printing a green marker over
an empty set.

## 3. THE PROVING EVIDENCE

- **Both directions, per `1eec315c7`:** green on the then-current tree (4 objects), and a **reconstructed
  known-bad state** (unhosted catalog version + a mutated bundle hash) produced `R2_PARITY_FAIL` and exit 1.
  A same-size different-content `.hash` also fails — the exact trap the old size-only skip walked into.
- **On a live build:** `docs/proof/2026-08-18-overnight-gear-structures/README.md:33` —
  `R2_PARITY_OK 4 object(s) verified - first real use of the PROD-011 gate on a live build.`
  (committed `fef3656d8`, Seeker build 331367).

## 4. The retry/timeout half — a DECISION, not unfinished work

Re-verified at HEAD by reading the three `.asset` files:

```
Schemas/Structure_Art_BundledAssetGroupSchema.asset:36   m_RetryCount: 2   :33 m_Timeout: 0
Schemas/Enemy_Art_BundledAssetGroupSchema.asset:36       m_RetryCount: 2   :33 m_Timeout: 0
Schemas/Default Local Group_BundledAssetGroupSchema.asset:36 m_RetryCount: 0  :33 m_Timeout: 0
```

The LOCAL group is untouched **on purpose** — it never touches the network. `m_Timeout` stays **0** on all
three because the remote payload measured on disk today is `enemy 67,582,523 B + structure 20,670,596 B =
88,253,119 B (84.2 MiB)` → **~141 s at 5 Mbps, ~471 s at 1.5 Mbps** for a cold first run. Any timeout short
enough to rescue a dead socket **aborts a healthy download on mobile data** — it would cause the failure it
was added to prevent, preferentially for the players the retry was for. **The WO's §3 has been rewritten to
record this ruling**, because as originally written the ticket read as half-done and the next reader would
have "finished" it into an outage. Acceptance §4.5 is struck through and superseded in the same edit.

*(Footnote for accuracy: `1eec315c7`'s body attributes the 141 s / 471 s figures to "the enemy bundle". They
are the **combined** remote set. The enemy bundle alone is ~108 s / ~360 s. The conclusion is unchanged.)*

## 5. ⚠ WHAT IS NOT PROVEN — AND ONE LIVE DEFECT FOUND TODAY

**A. The gate cannot run in the ship chain as currently invoked (found 2026-08-19, store-push relevant).**
`morning-ship-chain.ps1:133` calls `--verify-catalog` **with no argument**, which defaults to `ServerData`
(`tools/r2_sync.py:423`, `const="ServerData"`). That path calls `_detect_target`, which
**`sys.exit`s unless `ServerData/` holds exactly ONE subdirectory** (`tools/r2_sync.py:250-256`). As of today
it holds **two**:

```
ServerData/Android/                 newest catalog_2026.08.19.332478.bin
ServerData/StandaloneWindows64/     newest catalog_2026.08.19.332462.bin
```

So the chain now dies at step 2b with `FAIL: cannot pick a build target` **before it can print any marker**.
This fails SAFE (the chain refuses to distribute) rather than false-green, which is the right direction —
but the gate is inoperative until the call passes the target explicitly: **`--verify-catalog ServerData/Android`**.
The Windows folder appeared because of the WO-1124 defect (Addressables built for the wrong active target),
so fixing WO-1124 does not by itself remove the second folder.
**UNPROVEN:** I did not execute `r2_sync.py` (network + credentials, and the CLI seat holds the build lock).
This is a code-read plus a directory listing, not an observed run. *What would settle it:* one
`python tools/r2_sync.py --verify-catalog` in the current tree — expected output is the `cannot pick a build
target` exit, not `R2_PARITY_OK`.

**B. What the gate structurally cannot prove** (stated in `cmd_verify_catalog`'s own docstring, and it should
stay stated): it does **not** prove the APK on disk was built from this Addressables state — nothing stamps
that; it does **not** prove the bundles are loadable, only that objects of the right name and bytes are
hosted; it does **not** prove anonymous public read (that is `--check`, at that instant only); and it says
nothing about a bundle a *future* catalog will want. **The only defence is ordering: content → player →
verify → ship.**

**C. Not actioned, recorded by `1eec315c7` while proving the gate:** ~19.7 MB of **flattened objects at the
bucket root** surviving the `16e22dba3` bad push (unreachable by the game), and ~80 MB of stale bundles in
`ServerData/Android/` from earlier builds. Cost and clutter, not correctness.

**D.** `R2_PARITY_OK` has been observed on **one** build (331367). It has never run in the
`overnight-apk-build.ps1` path — only `morning-ship-chain.ps1` carries it (see WO-1124 §4).
