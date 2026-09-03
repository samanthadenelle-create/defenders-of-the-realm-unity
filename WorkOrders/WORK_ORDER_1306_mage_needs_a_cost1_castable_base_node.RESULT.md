# WORK ORDER 1306 — RESULT

**Status:** FIXED (edit-only lane; NOT gated, NOT committed — the lead gates and commits).
**Date:** 2026-09-02
**Silo:** Progression / Hero identity

---

## ⭐ THE NUMBER SHE WILL WANT TO SEE FIRST

> **Drain return rate = `100` percent of the damage ACTUALLY DEALT.**
> Shipping default. Live key `combat.drainReturnPct`. Clamped `0..1000` at the consumer.
> **Awaiting her confirmation** — see "Every number, and where it came from" below.

She can change it with **no rebuild**, and it lands on a running device in about 40 seconds:

```powershell
tools\command-centre.ps1 -Tunables -Key combat.drainReturnPct -Value 50   # half sustain
tools\command-centre.ps1 -Tunables -Key combat.drainReturnPct -Clear      # back to the shipped 100
```

Confirmed true: the knob is read at the moment each drain lands (`HeroAbilities.DrainReturnPct`), so it
is **not** a boot-time knob and needs no relaunch — 10 s edge cache + the 30 s client poll.

---

## The owner's rulings, verbatim — these ARE the spec

1. The retention lens (the reason the WO exists):
   > *"we want them to unlocka few items that can go in the quick swap bar fast, why because our
   > retention number is very low and people are not returning"*
2. The design call this WO was blocked on, 2026-09-02: **MIRROR THE KNIGHT** — apply to the mage the
   same rule the knight got in commit `02f9b8a4f`: the first talent point buys a **castable**, not a
   passive stat.
3. Refinement, same evening:
   > *"the blm needs to get some healing , like drain to stay balanced (early)"*
4. Follow-up, same evening:
   > *"be smart, dont make it need a code change, make it tweakable from a db call"*

**My reading of (3), stated so it can be corrected:** "blm" = black mage = the mage class; "early" =
the early game, which is exactly this cost-1 base node's slot. So the base grant is a **drain** —
damage that returns as healing — and the healing is the *point*, the damage the delivery mechanism.

---

## What shipped

**The mage now reaches a bar-equippable ability in 1 point (was 3).** Final class-tree
points-to-first-bar-ability: knight 1, ranger 1, **mage 1**, shared 1.

### 1. `mage.t1n3` — "Warded Flesh" is re-authored as **"Siphon Ward"**

| field | before | after |
|---|---|---|
| `id` / `tier` / `slot` / `cost` / `iconPath` / `x` / `y` / `prerequisites` | — | **UNCHANGED** |
| `kind` | `stat` | `skill` |
| `abilityId` | (absent) | `mage.siphon` |
| `effect` | `maxHpPct 0.2` | `maxHpPct 0.2` + `ability` rider — **the +20% max HP is KEPT** |
| `name` / `description` | Warded Flesh | Siphon Ward |

Because id/cost/x/y/prerequisites did not move, this is **still a cheapest-cost ROOT on the bottom
row**, so the owner's 2026-08-16 base-row law (`TalentTreeShapeRegression` rule 2 `[base]`) is
satisfied **without being loosened** — which is precisely what the reverted Blink Mastery promotion
could not do. `mage.t2n5` was **not** re-promoted. No id was renamed, no cost changed.

The `kind=skill` + `abilityId` + stat-payload shape is **not invented**: `mage.t2n5` Blink Mastery
already uses exactly it in this same tree, which is why the node could gain a castable and lose
nothing.

### 2. `mage.siphon` — a new pool spell in `classes.mage-skills`

`effect: "drainshot"` — the **shipped** `HeroAbilities.ResolveDrainshot` that `mage.drain` and
`ranger.healing-shot` already run. **No second healing mechanism was authored.**

### 3. `combat.drainReturnPct` — the drain rate on the PROD-022 tunable rail

Registered end to end, all four sources in one change, per `docs/PROD022_TUNABLE_FLAGS.md`.

---

## Every number, and where it came from — NOTHING was invented quietly

| value | source | status |
|---|---|---|
| **return rate 100%** | **Not a free parameter.** `ResolveDrainshot` heals *the damage actually dealt*, and that identity is PINNED by `Assets/Tests/EditMode/HeroAbilityEffectTests.cs` ("drainshot heal must equal the damage dealt", post-mitigation, HP-clamped). 100 is therefore the value the code already had. | **derived, awaiting confirmation** |
| damage 28 | copied **verbatim** from `mage.drain` | awaiting confirmation |
| range 13 m | copied verbatim from `mage.drain` | awaiting confirmation |
| cooldown 9 s | copied verbatim from `mage.drain` | awaiting confirmation |
| mana 7 | copied verbatim from `mage.drain` | awaiting confirmation |
| castSeconds 0.35 | copied verbatim from `mage.drain` | awaiting confirmation |
| clamp 0..1000 | **authored fresh** (no precedent existed) — mirrors the `Mathf.Max(1, ...)` clamps `StructureContentWarmer` applies to its own knobs. A negative row would make a healing spell damage its caster. | **AUTHORED — flagging it** |
| node name "Siphon Ward" / spell name "Siphon" | **authored fresh.** Names are a creative pick. | **AUTHORED — flagging it** |
| +20% max HP | unchanged from the node's existing `maxHpPct 0.2` | untouched |

**Why `mage.drain`'s numbers verbatim rather than a tuned-down variant:** `mage.drain` is the game's
one authored mage drain, tuned under her own WO-1019 ruling, and its own `_comment` still marks those
numbers `<<DRAFT - owner tuning pass>>`. Copying them means this base grant introduces **no new balance
point at all** — and now the whole family moves from one database row instead of a rebuild. If she
retunes the drain, retune both.

**Existing drain/lifesteal mechanism found and REUSED — I did not author a second one.** Search result:
`drainshot` is the game's one lifesteal shape. `HeroAbilities.HealFromDrain` is its **single owner**;
`ResolveDrainshot` and the public `ApplyDrainshot` both land there, and every drainshot ability
(`mage.siphon`, `mage.drain`, `ranger.healing-shot`) passes through it. The knob was therefore placed
at that one owner.

**Key named `combat.drainReturnPct`, not `mage.drainReturnPct` — a deliberate deviation from the
suggested name, flagged loudly.** The single owner is class-agnostic; scoping the knob to the mage
would require a per-ability branch *inside* that owner, i.e. exactly the second mechanism the "one
owner per concern" rule forbids. `combat.*` is honest about what it moves. **No new `TunableKind` was
added** — it is an integer percent on the existing `Int` kind, as instructed.

**Why a distinct id rather than granting `mage.drain` itself:** `mage.drain` is the mage's *default* E
(`classes.mage.abilities.e`), pressable from minute one with no talent at all. A talent granting it
would satisfy the retention *metric* while giving the player **nothing new to press** — and the knight
precedent grants `knight.thunderbolt`, which is *not* in his stock kit. Mirroring the knight means a
genuinely new castable.

---

## ⛔ The fail-to-default invariant, asserted not assumed

> *"No row, no network, no server, no parse ⇒ TODAY'S BEHAVIOUR, EXACTLY."*

`100 / 100f` is a float identity, so with an empty table the drain heal is **byte-for-byte** what
shipped, and the WO-861 `heal == damage dealt` pin holds unchanged. Driven, not asserted:

| row | meaning | resolved pct | heal on 28 dealt |
|---|---|---|---|
| *(none)* | offline / 404 / empty table / malformed JSON / corrupt cache | **100** | **28.0 — today** |
| `12.5` | a fraction (the obvious mistake on a percent knob; no float kind exists) | **100** | 28.0 — falls to default, does **not** truncate to 12 |
| `-500` | hostile/fat-fingered | 0 (clamped) | 0.0 |
| `999999` | absurd | 1000 (clamped) | 280.0 |
| `50` | legal override | 50 | 14.0 |

Provenance is traceable: the knob is in `RemoteTunables.Registry`, so it appears in the per-session
`[Flow:Tunables] CONFIG` line and gets its own `KNOB combat.drainReturnPct = <n> provenance=<default |
remote | remote-cached | local-playerprefs>` line. A `pct` of 0 emits a `FlowTrace.Warn` rather than
silently not healing (CLAUDE.md §12).

---

## Oracles — the mutations I proved

### NEW: `TalentTreeShapeRegression` rule 7 `[first-point]`

Deliberately **general, not a per-class pin**: *every CLASS tree's bottom row must hold at least one
node that grants a castable whose ability id resolves in `AbilityCatalog`*. Pinning the knight and the
mage by id would have made each fix its own special case and left the next class free to regress
silently — which is how the mage became the only outlier in the first place. The shared pool is exempt
(universal strip, not a class identity).

I cannot run a Unity gate in this lane, so the rule's logic was replicated exactly and driven against
mutated data. **Proven RED three ways, GREEN on HEAD:**

| run | result |
|---|---|
| current tree | **GREEN** — knight `knight.t1n2 -> knight.thunderbolt`; ranger `ranger.t1n3 -> ranger.tumble-step`; mage `mage.t1n3 -> mage.siphon` |
| **MUT 1** — revert `mage.t1n3` to the pre-WO-1306 stat node | **RED**: `[first-point] 'mage' has NO castable on its bottom row (mage.t1n1,mage.t1n2,mage.t1n3)` |
| **MUT 2** — point it at `mage.siphon-typo` | **RED**: `[first-point] 'mage' base-row node mage.t1n3 -> 'mage.siphon-typo' (no such ability in AbilityCatalog)` |
| **MUT 3** — regress the **knight** (Thunderbolt back to a stat) | **RED**: `[first-point] 'knight' has NO castable on its bottom row` — proving the rule generalises rather than pinning the mage |

Rule 2 `[base]` re-checked on all three trees after the edit: knight/ranger/mage each 3 base nodes, all
roots, all at the tree's minimum cost 1. Unchanged.

### EXTENDED: `RemoteTunablesDefaultsRegression`

`ExpectedKnobCount` 8 → 9; `ExpectedDefaults` gains `("combat.drainReturnPct", 100)`; the bad-values
failure path now also carries `"12.5"`; `Case3_Consumers` gains the new consumer's default, its
negative clamp, its ceiling clamp, **its success path** (a legal `50` must actually resolve to 50 — a
refusal-only proof certifies nothing), its return-to-default on clear, and a source lint that
`HeroAbilities.cs` still reads `KeyCombatDrainReturnPct` and has not re-hardcoded a const.

Four-source parity replicated and **proven RED on every single-source mutation:**

| mutation | result |
|---|---|
| current | **GREEN** (Registry 100 · oracle literal 100 · doc 100 · js allowlist present) |
| Registry default 100 → 80 | **RED** ×2 — `[defaults]` and `[doc-parity]` both name the disagreement |
| key removed from `api/_lib/tunables.js` | **RED** — `[key-domain] ... is MISSING key 'combat.drainReturnPct'` |
| doc table default 100 → 60 | **RED** — `[doc-parity] doc says 60; Registry says 100` |
| oracle literal 100 → 25 | **RED** — `[defaults] Registry says 100; oracle literal says 25` |

`MageSpellKitAuthoringRegression` Case 7 unlock-ledger re-computed by hand: all 10 mage-pool ids
resolve — `mage.siphon` **has** a node and is correctly **absent** from `PendingUnlockNode`; the five
pending entries are untouched and none went stale. `MageAbilityIconRegression` uses an explicit id
list, so `mage.siphon` adds no art requirement — and **no `concept-icons.json` row was authored for
it**, deliberately: `mage.drain` is itself left unauthored there pending an owner art tag, and picking
one would be the creative pick the owner-tags-the-art rule forbids the CLI. Its VFX fields are likewise
**held empty**, exactly as `mage.drain`'s are.

---

## Files changed

| file | change |
|---|---|
| `Assets/Resources/Data/Canonical/hero-talents.json` | `mage.t1n3` re-authored; `_ownerRuling` item (7); mage tree `_comment` |
| `Assets/StreamingAssets/Data/Canonical/hero-talents.json` | twin, byte-equal |
| `Assets/Resources/Data/Canonical/abilities.json` | new `classes.mage-skills.siphon` |
| `Assets/StreamingAssets/Data/Canonical/abilities.json` | twin, byte-equal |
| `Assets/_Modules/Core/Ops/RemoteTunables.cs` | `KeyCombatDrainReturnPct`, `DrainReturnPctDefault = 100`, Registry entry |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | `DrainReturnPct` (clamped, public static for the oracle); `HealFromDrain` applies it |
| `api/_lib/tunables.js` | `TUNABLE_KEYS` allowlist row |
| `docs/PROD022_TUNABLE_FLAGS.md` | flag #9, independence note, CONFIG line, worked example, file map, 8 → 9 counts |
| `Assets/Editor/Regression/TalentTreeShapeRegression.cs` | **new rule 7 `[first-point]`** |
| `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` | extended to 9 knobs + the new consumer |
| `WorkOrders/WORK_ORDER_1306_...md` | Status → FIXED |

`tools/client-tunables.mjs` needed no edit — it **imports** `TUNABLE_KEYS` from `api/_lib/tunables.js`
rather than keeping its own copy (the doc's "duplicated in the operator CLI" line is stale).

### Hashes — both canonical copies byte-equal

```
hero-talents.json  14c51424c522e1b5540fe3bdc4e5c67eb39d3f15d8a045b9b4eb761eff6a1c4c  (Resources == StreamingAssets)
abilities.json     2f22a7501466263fb938544424b4d8b9395f7dd8a4ce7b6675a3cc1efcdf9f2b  (Resources == StreamingAssets)
```

### Brace / NUL check — every `.cs` touched (CLAUDE.md §1)

```
Assets/_Modules/Core/Ops/RemoteTunables.cs                        BALANCED  clean
Assets/_Modules/Village/Hero/HeroAbilities.cs                     BALANCED  clean
Assets/Editor/Regression/TalentTreeShapeRegression.cs             BALANCED  clean
Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs      BALANCED  clean
```
Both JSON files re-parsed with a strict parser: **PARSE OK, no NUL**. `api/_lib/tunables.js` loaded
under node: 9 keys, correct order.

---

## Coordination — the two live adjacent lanes

- **WO-1310 / `HeroSkillTreePanelMvvm.cs`: NOT TOUCHED.** No edit to the layout solver, axis rotation,
  lattice/pitch maths, content extents or node-plate label sizing. `git status` confirms the file is
  unmodified. Rule 7 reads only `kind` / `abilityId` / `effect` — no geometry — so it cannot collide
  with the felt-verification in flight.
- **WO-1294 / hot-swap bar + troop portraits: NOT TOUCHED.** No edit to `HeroLoadout*.cs`,
  `AssignableSkillBar*.cs` or any portrait code. The new spell reaches the bar through the **existing**
  `HeroLoadoutVM` path (unlocked skill-kind node → `AbilityCatalog.FindById` → choices), which needed
  no change.
- Q's locked-basic rule untouched: nothing here writes a `q` slot or renumbers a slot ordinal. The
  spell's `slot: "e"` is only the chooser's *suggested* slot, per the pool contract.

## Open for the owner

1. **Confirm the drain return rate (100%)** and the copied `mage.drain` numbers — or just move the knob.
2. **Confirm the names** "Siphon Ward" (node) and "Siphon" (spell) — the one genuinely creative pick here.
3. **Tag the art** for `mage.siphon` (and `mage.drain`) when convenient — both are deliberately
   unauthored in `concept-icons.json`, and both hold their VFX keys empty.
4. Not gated and not committed — hand to the lead for `COMPILE_GATE_OK` + the regression run.
