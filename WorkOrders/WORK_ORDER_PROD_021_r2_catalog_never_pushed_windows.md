# PROD-021 — The R2 catalog for the shipped build was never pushed (occurrence FOUR)

**Status:** READY TO IMPLEMENT — EMERGENCY / LIVE DEFECT. ⚠ **CANDIDATE CLOSE — 2026-09-02 verification below says the gate defect this ticket was minted against is FIXED. NOT closed here: PO closes (CLAUDE.md §13).** *(Prior line:)* **Status:** READY TO IMPLEMENT — EMERGENCY / LIVE DEFECT
**Minted:** 2026-09-01 (CLI, PROD banner bumped 021 -> 022 in the same edit)
**Silo:** Content ship chain (CLAUDE.md §16). Disjoint from the WO-1289..1292 art lane.
**Covers 93 of the 148 un-acked F8 captures** (seq 4081–4224 clusters A + B).

---

## PROVING DATA — from the capture, before any code-read (CLAUDE.md §12/§14)

`logs/f8-inbox/capture-20260831-164333-seq4158.md`:

```
OperationException : CheckCatalogsOperation failed with the following errors:
RemoteProviderException : TextDataProvider : unable to load from url :
  https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/StandaloneWindows64/catalog_2026.08.31.349579.hash
UnityWebRequest result : ProtocolError
HTTP/1.1 404 Not Found
```

**That exact file EXISTS on disk**: `ServerData/StandaloneWindows64/catalog_2026.08.31.349579.hash`.
Built, never pushed. This is the §16 failure class for the **fourth** time (2026-08-18, -19, -20).

## THE 93-CAPTURE CASCADE — one cause, many faces

| n | symptom | seqs |
|---|---|---|
| 7 | `CheckCatalogsOperation failed` / `ChainOperation failed` | 4095-4098, 4158-4160 |
| 19 | `Addressables.InvalidKeyException` | 4081, 4085, 4088, 4092, 4145-4148, 4155, 4164-4167, 4174-4175, 4181-4185 |
| 1 | `[Flow:StructureAssets] warm pass discovered 38 structure address(es) but NOT ONE is resident` | 4189 |
| 20 | `[Flow:VisualFactory] model not found`: `Structures/store`, `jeweler`, `Forge`, `lumbermill`, `farm`, `armorer`, `PetHouse2`, `arcane tower`, `ShopAndCrafting`, `IronMine`, `GenericContainer`, `Tower_Wooden_Watchtower` | 4190-4214 |
| 6 | `[Flow:Structure] '<id>': visual not resident — pending-art proxy` (`workshop`, `collector_forge`, `lumberyard`, `foundry`, `silo`, `tower_ground_archer`) | 4204-4215 |
| 39 | `[Flow:MagentaGuard] hid stray MAGENTA placeholder` (WardStones ×36, StructureArtPending ×6) | 4112-4144, 4216-4221 |
| 1 | `[Flow:EnemyAssets] 'Enemies/Orc_Warrior' NOT-YET-DOWNLOADED for 96.4s` | 4222 |

**Owner-visible face**, her own flags: `4099 [Title] an internet connection error` ·
`4161 [Title] still this screen on exe`. The game cannot fetch its catalog, so it sits on Title.

## ⛔ WHY EVERY GATE STAYED GREEN — the real defect to fix

`Builds/r2-parity.log` (2026-08-31 16:46) **does** carry `R2_PARITY_OK 54 object(s) verified`.
(⚠ The log is **UTF-16LE** — a plain `grep R2_PARITY_OK` finds NOTHING and reads as a failure.
Decode before judging it.)

`tools/r2-ship.ps1:115` runs `--verify-catalog "ServerData/$catalogTarget"` — **ONE explicit target**.
§16 mandates the explicit form because the bucket holds both `Android` and `StandaloneWindows64`.
But that makes the verify **single-target while the push is parent-wide**: a run that pushes
`ServerData` and verifies `Android` emits `R2_PARITY_OK` **while the Windows catalog 404s**.
The marker is true and the build is still broken.

**Second, independent violation:** `ServerData/Android/catalog_2026.09.01.350657.hash` was built
**2026-09-01**, AFTER the 08-31 16:46 parity proof. §16's invariant is *the proof must postdate the
bytes it claims to prove*. It does not. The `.githooks/pre-push` guard exists and
`core.hooksPath=.githooks` is set, so a push of `ServerData/` should be refused — verify it bites.

## THE WORK

1. **Rebuild content for BOTH targets and push.** Requires the Unity editor CLOSED (project lock).
   Use the ONE sanctioned path: `tools\r2-ship.ps1`. Never re-inline the push or the verify.
   Never `adb install` a hand-built APK — that bypasses the gate entirely.
2. **Make the verify cover EVERY target the bucket holds, not one.** This is the gate defect.
   `r2-ship.ps1` must verify `ServerData/Android` **and** `ServerData/StandaloneWindows64` (enumerate
   the subdirectories of `ServerData/` rather than naming them, so a third target cannot be forgotten),
   and withhold `R2_PARITY_OK` unless ALL of them verify. Keep `--push ServerData` (the PARENT) —
   pushing a child flattens the keys to the bucket root and still reports `R2_PUSH_OK`.
3. **Prove the pre-push hook bites** — touch something under `ServerData/`, attempt a push, confirm
   the refusal, then clear it the sanctioned way. Do NOT add an override flag; §16 removed that
   deliberately.

## ACCEPTANCE CRITERIA

- [ ] `R2_PUSH_OK` **and** `R2_PARITY_OK` on a FRESH log, naming **both** targets. Judge by the
      MARKER, never the exit code — this repo's runners exit 0 on refusals and FAILs.
- [ ] `curl -I` (or the tool's own check) returns **200**, not 404, for the catalog hash the running
      build requests: `StandaloneWindows64/catalog_2026.08.31.349579.hash` (or its successor).
- [ ] Prove the widened verify FAILS when one target is missing — delete/rename one target's catalog
      in a scratch run and watch `R2_PARITY_OK` be withheld. A pass-only proof is not acceptance
      (memory `prove-the-success-path-not-just-the-refusal`).
- [ ] Fresh device/exe run shows **zero** `VisualFactory model not found` and **zero**
      `StructureArtPending` MagentaGuard lines.
- [ ] Owner felt-verifies the Title screen loads and the town shows real buildings (PO closes, §13).

## DO NOT

- Do not "fix" this by re-inlining a push into a chain, adding an override flag, or raising a timeout.
- Do not conclude the parity log is empty because grep found nothing — it is UTF-16LE.
- Do not treat a green `R2_PARITY_OK` as proof until it names every target.

---

## ⚠ VERIFICATION 2026-09-02 (board status audit — read before working this ticket)

Verified against the tree and `Builds/r2-parity.log` today. **The gate defect in "THE WORK" item 2 is
fixed, and the Windows catalog is present on R2.** Recorded here, not closed — the owner closes (§13).

**PROVEN:**
- **Item 2 (the widened verify) is implemented.** `tools/r2-ship.ps1` no longer names one target:
  it enumerates the subdirectories of `ServerData/` that hold catalogs (`:177 foreach ($t in $targets)`,
  verifying `ServerData/$name` at `:182`), rewrites each per-target pass to `R2_PARITY_TARGET_OK`, and
  withholds the aggregate marker unless all pass. It also fails (`R2_PARITY_FAIL`, exit 16) when
  `$targets.Count -eq 0` — "nothing to verify" is a FAILURE, not a pass.
- **Acceptance line 1 is met on a fresh log.** `Builds/r2-parity.log` (2026-09-02 16:30:50, UTF-16LE —
  decode before judging) ends with:
  `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=261`.
  Three targets named, so the "green marker while the Windows catalog 404s" shape can no longer occur.
- **The StandaloneWindows64 catalog exists and is current on disk:**
  `ServerData/StandaloneWindows64/catalog_2026.09.02.352005.hash` (06:04), i.e. the 08-31 hash the
  capture 404'd on has been superseded and its successor is covered by today's parity run.

**NOT PROVEN by this audit — do not read the above as full acceptance:**
- **Acceptance line 3 (the falsification run) is unproven.** Nobody has shown `R2_PARITY_OK` being
  *withheld* when one target's catalog is removed. Per memory `prove-the-success-path-not-just-the-refusal`,
  a pass-only proof is not acceptance — the widened loop is read at source, not exercised against a miss.
- **Acceptance lines 4 and 5 (fresh device/exe run with zero `VisualFactory model not found` /
  `StructureArtPending`, and the owner's felt-verify) are untouched.**
- ⛔ **A §16 freshness violation is live RIGHT NOW.** `ServerData/WebGL/catalog_2026.09.02.352005.bin`
  and `.hash` are stamped **16:31:40**, i.e. **50 seconds AFTER** the 16:30:50 parity proof. The §16
  invariant is *the proof must postdate the bytes it claims to prove*, so `.githooks/pre-push` should
  refuse a push that carries `ServerData/`. Clear it the one sanctioned way (`tools2-ship.ps1`) —
  never with an override.

**Recommendation to the owner:** this looks closeable on the gate defect, but close it only after the
falsification run (line 3) and a fresh device/exe run (line 4). The WebGL freshness gap above is a
separate, live item.
