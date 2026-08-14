# WORK ORDER 992 — Six classes ship in every build, compile clean, and are NEVER instantiated

**Status:** READY TO IMPLEMENT (per-class; two await research)
**Minted:** 2026-08-14 (CLI)
**Silo:** Codebase hygiene / dead code
**Source:** the 2026-08-14 phantom sweep, plus owner dispositions the same day

---

## What was found

Seven classes exist, compile, and ship in every build with **zero instances and zero callers**:

| Class | From | Owner disposition (2026-08-14) |
|---|---|---|
| `WeatherManager` | WO-52 | **KEEP** — *"weather manager will play into the zones for the map"* |
| `TorchFireController` | WO-55 | **RESEARCH** — *"Im not sure of but it seems like something old, needs researched to see where or when it was created and if anything touches it"* |
| `AuraController` | WO-58 | **RESEARCH** — *"sould be applying auras to towers portals I think, needs research"* |
| `BattlePassManager` | WO-73 | *"ideas not implementations yet i think"* — confirm, then delete or keep as scaffold |
| `CryptoPaymentManager` | WO-73 | same |
| `CosmeticApplier` | WO-73 | same |
| Cinemachine controller | WO-87 | Needs re-target off the **deleted** `Village.unity` |

Each of WO-52/55/58/73/87 has an **honest RESULT file** that flagged *"scene wiring = manual editor
work."* That wiring never happened, and **nobody noticed for ~2.5 months.**

## Why this ticket exists at all

All seven originate in pre-800 tickets, a band the owner considers unreliable (2026-08-14:
*"i wouldnt rely on anything before 800 with much truth"*) — and which is being **verified**, ticket by
ticket, on her instruction. Whatever verdict those old tickets land on:

> ⚠ **A ticket's disposition does not touch the code. Closing one deletes the only record of why the
> code is there.**

Left in their old tickets, these seven become permanently unexplained classes that every future reader
must re-investigate — the same cost this sweep just paid, forever, on repeat. Hoisting them into a
current-era ticket with a per-class decision is what stops that.

## ⚠ THE METHOD — this is the load-bearing part

**Unity serialises script references by GUID, not by class name.** A class-name grep across `.unity`
and `.prefab` finds **nothing** and reads as *"no references, all clear"* — which is exactly how these
sat undetected. To determine whether a MonoBehaviour is actually wired:

1. Read its `.cs.meta` for the `guid`.
2. Search **that GUID** across `Assets/**/*.unity` and `Assets/**/*.prefab`.
3. Separately search the class name across `.cs` for `AddComponent`, `GetComponent`,
   `FindObjectOfType` / `FindFirstObjectByType`, and direct type references.
4. Look for a **commented-out seat line**. WO-87's shape is the giveaway: the controller exists, its
   GUID is in no scene, and the line that would have seated it is commented out at
   `VillageSceneBuilder.Characters.cs:119`. A commented seat is evidence of an intent that was
   reverted — and it dates the decision.

⚠ The composed dungeon scenes (`dg_*.unity`) serialize **binary**, so a text/GUID grep of those proves
nothing either way. Say so rather than concluding absence.

## Per-class scope

**`WeatherManager` — KEEP, do not delete.** The owner has a live use: it feeds the **zones for the
map**. Do not wire it speculatively either — it should be seated by whatever map/zone work claims it,
so the seam is designed once with a real consumer. Until then, annotate the class with a dated note
saying it is intentionally dormant and what it is reserved for, so the next sweep does not re-flag it.
⚠ WO-52 also needs a **ShootingStar prefab** that was never made; record that as part of its cost.

**`TorchFireController` — research before any decision.** Questions to answer: when and why it was
created; what touches it today; and whether it was superseded — the dungeon has torch lighting that
demonstrably works (`DungeonDresser` seats torch props, and there is a documented torch-range lesson
about a literal tuned at `Cell = 6`). If something else lights torches now, name it, and this deletes.

**`AuraController` — research, and expect a divergence.** The owner believes it *"sould be applying
auras to towers portals"*. **WO-58's own title is "pet aura system".** Read the implementation and say
plainly whether it targets pets, towers, portals, or something else. **If the owner's expectation and
the code diverge, that divergence is the finding** — do not smooth it over. Also determine what applies
auras today (`VfxAuraProximityCuller`, the catalog's `Aura_*` keys — `Aura_Dust` is a tracked prefab).

**`BattlePassManager` / `CryptoPaymentManager` / `CosmeticApplier` — confirm the owner's read.** She
believes these are *"ideas not implementations yet"*. Verify: are they substantially complete
implementations that were merely never wired, or thin stubs? That changes the disposition — a complete
implementation is worth wiring; a scaffold is worth deleting. ⚠ Note `CryptoPaymentManager` touches
payments; do not wire anything payment-related without an explicit owner decision.

**WO-87 Cinemachine controller — re-target.** Its builder line is commented out and it points at
`Village.unity`, which is **DELETED**. Either re-target it at `Main_Castle_Overworld` or delete it.

## Acceptance criteria

- Every one of the seven ends in a **stated** end-state: wired, deleted, or **dormant with a dated
  annotation naming what it is reserved for**. No class is left silently unexplained.
- For anything wired: prove it with the GUID search above, not a name grep.
- For anything deleted: confirm no `.unity`/`.prefab` carries its GUID first.
- `COMPILE_GATE_OK` after any deletion — removing a type can break a reference the name grep missed.

## What NOT to do

- ⛔ Do **not** delete `WeatherManager`. The owner has claimed it.
- ⛔ Do **not** wire anything speculatively to "make the sweep clean". An unwired class with a dated
  note explaining why is a *good* state; a wrongly-wired one is a new defect.
- ⛔ Do not strip any `FlowTrace` from these files if they carry it (CLAUDE.md §12, BINDING).
- ⛔ Do not touch payment wiring without an explicit owner decision.
