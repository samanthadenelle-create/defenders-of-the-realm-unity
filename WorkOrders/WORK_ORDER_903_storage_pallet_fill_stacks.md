> ## ✅ REVISED 2026-08-21 (UI seat) — READY TO IMPLEMENT. The 2026-08-08 blocker is GONE (bank max is
> readable now, §W1) and the owner re-ruled the granularity to QUARTERS (§W3).
> ## (superseded banner, kept for history) RECONCILED 2026-08-08 - true status was NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: no pallet stack view exists anywhere in the tree; every `storageResource` consumer is caps / build-mode only.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 903 — Storage pallet fill stacks (logs / sacks / ingots, QUARTER intervals)

**Status:** FIXED 2026-08-23 (Codex implemented; CLI reviewed + gated) — AWAITING OWNER FELT-TEST TO CLOSE.

> Five-tier pallet fill for lumberyard / foundry / silo: exact empty and full, quarter / half / three-quarter
> tiers, 2% hysteresis on the internal boundaries, pooled log/ingot/sack props with add-remove transitions,
> a full-tier overflow silhouette, and immediate (unanimated) reduction on upgrade. Works for freshly placed
> AND save-replayed structures — `PlacedStructure.Start` is the one seam both paths share.
>
> ⚠ **DEVICE CAPTURES NOT TAKEN.** Codex had no device session, so this is compile- and gate-verified only.
> The whole ticket is a LOOK, so nothing here is proven until it is seen — this is precisely the class of work
> a green gate cannot speak for.
>
> **CLI addition at review:** the abstract fill-bar fallback degraded SILENTLY. The bar and the pallet look
> nothing alike, so a player who gets the bar is looking at a different feature — and with props served from
> the CDN (§16), a missing or unpushed prop is exactly how that happens. Now a `FlowTrace.Warn` naming WHICH
> of the four conditions fired (parse / catalog null / row miss / prop null), per CLAUDE.md §12.
**Owner ruling 2026-08-21:** quarter intervals, not ~5% steps. *"show the capacity as empty and full
at 1/4 intervals."* The 20-step spec in §Goal is SUPERSEDED by §W3 — read that instead.  
**Minted:** 2026-08-04 (CLI / Grok — owner: pallets show items as bank fills)  
**Silo:** Village presentation / storage  
**Size:** **SMALL** — reuse collector stack pattern; no economy rewrite  
**Depends on:** bank max readable for wood/iron/food (901/857 storage caps — if max is still “uncapped,” use a large soft max or wait for Phase F/cap; prefer wire to real Max when present)  
**Adjacent:** 901 collector loop · 900 collector full tell · `docs/ART_BRIEF_storage_containers.md`

---

## Goal

On **pallets** (storage containers), diegetically show fill:

| Building (catalog id) | Resource | Prop as fill rises |
|----------------------|----------|--------------------|
| `lumberyard` | wood | **logs** (~1 per 5%) |
| `foundry` | iron | **ingots** |
| `silo` | food | **grain sacks** |

```
fill = current(resource) / max(resource)   // village bank
steps = floor(fill * StepCount)            // StepCount = 20 → ~5% each
```

Empty pallet = frame only (0 props). Full = 20 props stacked. Colorblind-safe by **count/height**, not hue.

**Not** collector pending — that stays on farm/lumbermill/forge via `CollectorStackView`.

---

## Reuse (do not rebuild)

| Existing | Use |
|----------|-----|
| `CollectorStackView` + `CollectorStackPropCatalog` | Same prop map (Wood/Iron/Food) and step/pooling idea |
| `RepoProps.storageCapacity` + `storageResource` | Identify pallets |
| Wallet / ResourceLedger + storage max (901) | `current` / `max` |

Prefer: **generalize** stack view to accept a fill provider **or** thin `StorageStackView` that copies Attach/pool pattern and loads the **same** `CollectorStackPropCatalog`.

**Do not** invent a second prop catalog unless the SO cannot be shared.

---

## Scope

1. **Attach** on placed/live lumberyard, foundry, silo (StructureFactory / place commit / scene load — same place collectors get their view once wired).
2. **Fill driver:** poll or subscribe when resources change; recompute steps; toggle props.
3. **Props:** ensure catalog asset at `Resources/Collectors/CollectorStackPropCatalog` has Wood/Iron/Food prefabs (polyperfect log/crate/sack if missing — one-time assign, not new art pipeline).
4. **Fallback:** abstract bar if prop missing (collector pattern).
5. **No** tap-to-collect on pallets (bank only). Optional later: select building shows `current/max` text.

### Out of scope

- Wallet clamp / grant rules (901 Phase F)  
- Collector icons (900/858)  
- Jeweler  
- Crystals pallet (unless a container exists)  
- Full Tripo multi-mesh LODs from art brief (optional follow-up)

---

## Acceptance

- [ ] Place lumberyard; grant wood → logs appear stepwise as bank fill rises  
- [ ] Foundry + iron → ingots; silo + food → sacks  
- [ ] Spend resource → steps drop  
- [ ] 0% = no props; ~100% = full stack  
- [ ] Dual-copy untouched unless only docs; no combat changes  
- [ ] COMPILE_GATE_OK; brace-check any .cs  

---

---

# REVISION 2026-08-21 (UI seat) — quarter intervals, and the blocker is gone

**Owner instruction 2026-08-21:** *"come up with the best solution to show the capacity as empty and
full at 1/4 intervals."*

**Status moves SPEC/NOT-STARTED -> READY TO IMPLEMENT.** Two things changed since the 2026-08-08
reconciliation, and both are verified at source this session.

## W1. The dependency named in the header is RESOLVED

The original WO hedged: *"if max is still 'uncapped,' use a large soft max or wait for Phase F/cap."*
**There is no longer anything to wait for.**

| Needed | Now exists | Verified |
|---|---|---|
| A real per-resource bank max | `TownBankCapacity.BaseCapOf(resource)` | `StorageCapsCatalog.cs:76-80` — callers MUST route through it |
| Container capacity that scales with level | `StorageCapsCatalog.LevelMultiplier(level)`, `[1, 2, 4, 8, 16, 32]` | `:56`, `:89` |
| Container identification | `RepoProps.IsStorageContainer` (`storageCapacity > 0`), `storageResource` | `RepoProps.cs:190-216` |
| The three pallets | `lumberyard` / `foundry` / `silo` rows | `structures-catalog.json` |
| The prop catalogue | `Resources/Collectors/CollectorStackPropCatalog.asset` + a regression pinning it | `CollectorStackPropCatalogRegression.cs` |

So `fill = current / max` is fully computable today. **Scope item 3 ("ensure catalog has
Wood/Iron/Food prefabs") is likely already satisfied** — check the regression's assertions before
doing any prefab assignment work.

## W2. ⛔ THE ORIGINAL 20-STEP SPEC DEFEATS ITS OWN ACCESSIBILITY CLAIM — replace it

The WO specifies `StepCount = 20`, one prop per ~5%, and then claims the result is *"colorblind-safe
by count/height, not hue."* **At 20 steps that claim is false in practice**, and the owner's quarter
ruling fixes it rather than merely simplifying it:

- **A 5% change is not perceptible on a pallet at town-camera distance.** One extra log out of twenty
  is a sub-pixel silhouette change. Feedback the player cannot perceive is not feedback.
- **20 adjacent heights are not separable — 5 are.** "Count/height" only carries information when the
  steps are far enough apart to *name*. A player can say *"that silo is about half"*; nobody has ever
  looked at a pile and said *"that's 45%."*
- **It is cheaper.** Fewer instanced props per pallet across every container in town, on a phone.

**Quarters are not a downgrade of the 20-step design. They are the version that works.**

## W3. THE MAPPING — five display tiers, with BOTH ENDS EXACT

The naive `floor(fill * 4)` is wrong at both ends and would make the feature lie:
at 99% it shows three-quarters, and at 1% it shows empty — so a player who just deposited sees
nothing, and a player who is one log from cap sees room that is not there.

**Empty and full are therefore EXACT states, not bands.** The owner asked for *"empty and full at
1/4 intervals"*; making the two ends exact is what stops the intervals from lying.

| Tier | Condition | Cumulative props | Reads as |
|---:|---|---:|---|
| **0 — Empty** | `current == 0` | 0 | bare frame |
| **1 — Quarter** | `0 < fill < 0.375` | 2 | a low scatter — *"something is in there"* |
| **2 — Half** | `0.375 <= fill < 0.625` | 5 | half the frame height |
| **3 — Three-quarters** | `0.625 <= fill < 1.0` | 9 | most of the way up |
| **4 — Full** | `current >= max` | 14 **+ spill** | topped out and overflowing |

- **Tier 1 triggers on any non-zero holding.** Deposit one log and the pallet acknowledges it.
- **Tier 4 triggers only at cap.** "Full" means full.
- Props are **instanced once at Attach and SetActive-toggled**, exactly as `CollectorStackView` does
  (`:200`, `:219-222`) — no per-change instantiation.

### W3.1 The FULL tier is the most valuable thing this feature ships

A container at cap means **incoming resources are being thrown away.** That is the one piece of
actionable information a fill display can carry, and it deserves its own read rather than just
"more props":

**Tier 4 breaks the frame line** — props spill past the pallet edge, a sack tips, logs sit askew on
top. The silhouette stops being a tidy stack and becomes a mess.

Why this is the right tell: it is **diegetic** (no icon, no bar, no HUD element), it is
**greyscale-legible** (the frame's straight edge is broken — a shape change, not a colour change),
and it reads at distance as *"that one needs attention"* without teaching the player any vocabulary.
It also gives the player the upgrade prompt for free: a spilling silo argues for its own next level.

⚠ Keep this **distinct from WO-900's collector full tell** — that is the harvest node's *pending*
state on farm/lumbermill/forge. This is the *bank* at cap. Two different facts; do not share one
visual language between them or the player learns the wrong lesson.

### W3.2 Hysteresis — required, or the pallets flicker

Resources tick continuously. A bank hovering at a tier boundary would toggle props on and off every
tick, which reads as a bug.

**Require a deadband: a tier change only commits when fill crosses the threshold by >= 0.02 (2% of
max) in the direction of travel.** Applies to the interior boundaries only — **the exact ends never
get a deadband**, because `current == 0` and `current >= max` are not approximations.

### W3.3 Transition — props arrive, they do not pop

A newly-active prop scales in from ~0.6 over ~0.15 s with a slight drop. It reads as *goods
delivered* rather than *geometry appearing*, it costs nothing, and it draws the eye to the container
that just changed. Removal is the same in reverse. **No particle effect** — this is not a VFX
ticket and must not take a Family-A loop slot.

### W3.4 ⚠ Container upgrades make the pile SHRINK — and that must not read as theft

`LevelMultiplier` doubles capacity per level (`[1, 2, 4, 8, 16, 32]`, ceiling
`RepoProps.MaxStructureLevel = 6`). Upgrading a full silo halves its fill fraction, so the pile drops
from tier 4 to tier 2 **while the player's stored amount has not changed at all.**

Handle it deliberately: on an upgrade-driven recompute, **suppress the shrink transition** and let
the new tier appear with the rebuilt structure. A player who watches their grain visibly vanish
after paying for an upgrade will file it as a bug, and they will be right to.

## W4. Build shape — a sibling view, not a generalisation

The WO offers two routes. **Take the second.**

`CollectorStackView.Attach(ResourceCollector collector)` (`:96`) is hard-bound to `ResourceCollector`
and reads `ResourceCollector.StepCount` throughout (`:215-222`, `:312`, `:352`). Generalising it to
accept a fill provider means touching a live collector surface to serve a new one — a refactor
smuggled into a SMALL presentation ticket, which is exactly what CLAUDE.md's architecture law says
not to do.

**So: a thin `StorageStackView` that copies the Attach/pool/toggle pattern and loads the SAME
`CollectorStackPropCatalog`.** The WO's own rule — *"do not invent a second prop catalogue"* — is
preserved, which is the part that actually mattered.

- `TierCount = 5` (0..4) lives on the new view. **Do not** reuse `ResourceCollector.StepCount`.
- Attach on placed/live `lumberyard` / `foundry` / `silo` at the same seam collectors get theirs.
- Fill driver subscribes to resource change; recompute tier; toggle. Poll only if no event exists.
- Fallback to the abstract bar when a prop is missing (the collector pattern).
- **No tap-to-collect on pallets** — bank only, unchanged from the original scope.

## W5. Acceptance (replaces the original checklist)

- [ ] Empty pallet = frame only, at `current == 0` and **only** there.
- [ ] One unit deposited into an empty bank -> **tier 1 appears**.
- [ ] Fill through 40% / 70% -> tier 2, tier 3, each visibly distinct **at town-camera distance**.
- [ ] At cap -> **tier 4 with the spill**; the frame silhouette is broken.
- [ ] Spend below cap -> the spill clears and the tier steps down.
- [ ] **Greyscale capture:** all five tiers nameable with hue removed.
- [ ] **Flicker test:** park the bank on a tier boundary and tick resources — no toggling (W3.2).
- [ ] **Upgrade test:** upgrade a full container — the pile drops a tier **without** a shrink
      animation, and no resources were actually lost (W3.4).
- [ ] All three pallets: `lumberyard`/wood, `foundry`/iron, `silo`/food.
- [ ] `COMPILE_GATE_OK`; brace-check every `.cs`; dual-copy untouched (no data change is needed).
- [ ] Captures opened, not just taken.

## W6. Still out of scope (unchanged)

Wallet clamp / grant rules · collector icons (900/858) · jeweler · crystals pallet · Tripo multi-mesh
LODs. **Also newly out of scope:** any change to `CollectorStackView` or `ResourceCollector` (W4).

## Paste for Claude / CLI

```text
Implement WORK_ORDER_903_storage_pallet_fill_stacks.md (SMALL).
Reuse CollectorStackView / CollectorStackPropCatalog pattern on lumberyard/foundry/silo.
Drive fill from bank current/max at QUARTER intervals - FIVE tiers (empty / 1-4 / 1-2 / 3-4 / full),
both ends EXACT, per SECTION W3. NOT 20 steps - that spec is superseded. logs / ingots / sacks.
No collect-on-pallet; no economy rewrite. COMPILE_GATE_OK; brace-check .cs.
```
