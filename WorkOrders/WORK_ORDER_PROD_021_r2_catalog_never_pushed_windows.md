# PROD-021 — The R2 catalog for the shipped build was never pushed (occurrence FOUR)

**Status:** READY TO IMPLEMENT — EMERGENCY / LIVE DEFECT
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
