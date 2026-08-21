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
| `AuraController` | WO-58 | ⚠ **STALE ROW — THE FILE WAS ALREADY DELETED** (`b63bc7190`, WO-993, 2026-08-16, five days before this WO was minted). Research answered in the 2026-08-21 lane pass below: it was **pet-only**, never towers/portals. |
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

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `7 classes never instantiated` — no disposition applied. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

---

## LANE PASS 2026-08-21 (edit-only agent) — dispositions applied, ZERO deletions

Every class was verified by the §"THE METHOD" GUID route, **plus a raw-byte scan of the 12 binary
scenes in both plain and nibble-swapped order** — so the "a text grep of `dg_*.unity` proves nothing"
caveat is now closed with real evidence rather than a shrug. **No file was deleted; no class was
wired.** Six files received a dated dormancy annotation in their own header, which is the acceptance
criterion "no class is left silently unexplained".

| Class | Disposition | Annotated |
|---|---|---|
| `WeatherManager` | **KEEP — dormant, reserved for map zones** (owner claim) | ✅ `Village/Vfx/WeatherManager.cs` |
| `TorchFireController` | **PROPOSE DELETION — superseded**, but BLOCKED (see below) | ✅ `Environment/TorchFireController.cs` |
| `AuraController` | **ALREADY GONE — this ticket row is STALE**, see below | n/a (file does not exist) |
| `BattlePassManager` | **owner disposition owed** — complete impl, NOT an "idea" | ✅ `Cosmetics/BattlePassManager.cs` |
| `CryptoPaymentManager` | **UNTOUCHABLE — owner only** | ✅ `Wallet/CryptoPaymentManager.cs` |
| `CosmeticApplier` | **owner disposition owed** — complete impl, NOT an "idea" | ✅ `Cosmetics/CosmeticApplier.cs` |
| `HeroCinemachineRig` (WO-87) | **deliberately disabled 2026-05-20; re-target is owner's call** | ✅ `Village/Hero/HeroCinemachineRig.cs` |

### ⚠ THREE CORRECTIONS TO THIS TICKET'S OWN PREMISE

**1. `AuraController` DOES NOT EXIST. It was deleted five days before this WO was written.**
Commit `b63bc7190`, 2026-08-16, *"refactor(pets): WO-993 — retire the physical pet stack (aura,
progression, spirit layer)"*, on the owner ruling *"we dont use the pet aura anymore since we descoped
them to simply helpers"*. Its former path was `Village/Pets/AuraController.cs` (not `Pets/`).
**And the divergence this WO asked us to name, named:** the owner expected it to apply auras to
"towers portals"; the code was **pet-only and unambiguous** — its own header read *"drives a persistent
aura ParticleSystem scaled with pet level… Attach to any pet prefab alongside Animator and PetBrain"*,
with per-pet colour themes. WO-58's title "pet aura system" was **accurate**; the tower/portal aura the
owner has in mind was **never this class and has never existed**. Today: `VfxAuraProximityCuller`
structurally cannot touch towers/Heart/boss; `Aura_EmpowerTower` **has no consumer at all**
(`ParticlePackVfxBatchBuilder.cs:1024` refuses to wire one); `Aura_TalentNode` is live but belongs to
the talent tree. ⚠ Do not confuse it with `Village/Heart/HeartAuraController.cs`, which is live,
self-attaching and regression-pinned.

**2. `TorchFireController` is NOT reference-free — deleting it breaks the compile.**
`NightTorchLightSystem.cs:191` holds a live `Object.FindObjectsByType<TorchFireController>()`. Nothing
ever creates one, so it always returns empty, but the **type reference is real**. The deletion is a
**two-file change** touching a live lighting system, which is an owner call. Left in place.

**3. "Ideas not implementations yet" is wrong for all three WO-73/74 classes.**
`BattlePassManager` (311 L, 16 FlowTrace sites), `CryptoPaymentManager` (379 L, 22 sites) and
`CosmeticApplier` (334 L, 19 sites) are **finished implementations that were never seated**. By this
ticket's own rule — *"a complete implementation is worth wiring; a scaffold is worth deleting"* — that
**flips the disposition**, so none of them should be swept out as scaffolds. Details per file.

### 🔴 THE ONE FINDING THAT IS NOT ABOUT DEAD CODE
**Equipping a cosmetic today changes a state flag and nothing else.** `GlimmerCurrencyService.Equip`
(:154) sets ownership/equip state, but `CosmeticApplier.ApplyCosmetic` is **defined only there and
called nowhere**, and no applier is attached to any prefab. That is a player-facing gap that was
hiding inside a dead-code ticket.

### ⛔ ESCALATED, UNTOUCHED: `CryptoPaymentManager`
Three facts forbid an agent acting: the game is **LIVE on the Solana dApp Store** (next submission is
an update); this is **the payment path** (`PayWithSOL/SKR/USDC` → `SendFlatPayment` → `GrantGlimmer`
at `:235`, called from `:209`); and a **Glimmer purge is already pending an owner migration ruling
(WO-1126)** whose blast radius includes exactly that method. Also recorded in-file: the shipped store
path (`PackStoreVM`) **reimplemented the same reflection bridge** instead of calling this class
(`PackStoreVM.cs:190`), so the two are duplicate grant implementations — deduplicating them is a
ticket with real money attached, not a cleanup.

### Left for the owner (one word each unblocks a lane)
1. `TorchFireController` — approve the two-file retirement (class + `AttachToExistingTorches`)?
2. `BattlePassManager` — seat it (needs a `BattlePassData` .asset + a season) or retire it?
3. `CosmeticApplier` — seat it (closes the equip-does-nothing gap) or retire it?
4. `CryptoPaymentManager` — hold until the WO-1126 Glimmer ruling; then wire-or-retire, with
   `PackStoreVM`'s duplicate bridge in the same decision.
5. `HeroCinemachineRig` + `CinemachineCameraController` — one decision covering **both**; re-enabling
   swaps the live camera (it disables `VillageCamera` at `:128-133`) and was disabled for a named,
   reproduced defect, so it needs a felt test, not a compile.
6. `WeatherManager` — no action; note its unpaid cost: **the ShootingStar prefab does not exist
   anywhere in the tree**, and `VFXType.ShootingStar` falls back to a procedural effect with no SFX.

> **OWNER RULING 2026-08-21 (verbal, this session) — BattlePassManager disposition:**
> *"battlepassmanager isnt used cause we didnt ever use battle pass yet."*
>
> **KEEP, DORMANT — not dead code.** It is unwired because the FEATURE has not shipped, not
> because the class was abandoned. That matches what the audit found at source: 311 lines,
> 16 FlowTrace sites, a finished debit->grant discipline, and a live `SpendGlimmer` call at
> `BattlePassManager.cs:175`. This is a built system waiting for its door, which is the exact
> case WO-992 says to KEEP.
>
> ⛔ Do not propose deleting it again. It is the implementation half of
> `WORK_ORDER_battle_and_monthly_packs`, which the owner set to TOP PRIORITY the same day —
> deleting it would delete the work that ticket depends on.

