# WORK ORDER 738 — Echo per-echo agency + specialization (Path B)

> ⚠ SUPERSEDED 2026-08-01 by WO-830 (echo harvest affinity + synergy — all 6 echoes get unique harvest affinities; hidden tri-synergy).

**Status: SPEC — needs owner pins (see "Owner pins"), then READY TO IMPLEMENT.**
**Lane:** Economy/Harvest + HUD (Village/Harvest silo; picker/roster UI). **Type:** design
evolution of BUILT systems (EchoService, EchoAssignments, EchoRosterCatalog/View, EchoCard
picker, WO-709 global multiplier) — reconcile ADDITIVELY, never greenfield a second workforce.
**Minted from `CLI_LANES_WO_NUMBERS.md` banner next-free = 738** (confirmed 2026-07-16c banner).
⚠ Committer action: bump the banner next-free 738 → 739 in the same commit that lands this WO
(this authoring pass is restricted to writing ONLY this file, so the banner is NOT yet bumped).

---

## The ruling (owner, binding — Path B: full per-echo agency + specialization)

- **6 collectible Echo spirits.** Each has: an **element**, a **level** (1..max), and **ONE
  player-assigned lane** (the agency pick).
- **Correct element + lane** = a noticeable bonus; **base contribution to ALL lanes** so no echo
  ever feels wasted; **element cross-bonuses**; a **set bonus** for owning all 6.
- **CRITICAL CANON — echoes NEVER fight in real time.** The "Defense" lane is a PASSIVE bonus that
  only resolves when the city is raided **while the player is OFFLINE** (async sim). No echo ever
  appears in an active fight. This preserves the standing "echoes don't fight / don't defend live"
  canon (COMBAT_PIVOT_NORTHSTAR; WO-709 pin: "echoes never fight… unless we add it as a passive
  offline type item").
- **Lanes to support:** Harvest (resource faucet), Crafting (Forge/processing yield+speed),
  Defense (passive OFFLINE city-raid defense bonus), Exploration (passive **dungeon-only** loot
  bonus — owner refinement 2026-07-17: dungeon loot / clearing ONLY, NOT overworld or raid-out).

---

## Context — canon + code, verified from the code (not comments)

**Shipped Echo workforce (all real, all persisted):**

- `EchoService` (`Assets/_Modules/Village/Harvest/EchoService.cs`): the faucet.
  - `MaxEchoes = 6` (EchoService.cs:61 — the full canonical roster) — **note the cap moved to 6**;
    WO-709 was pinned at cap-5, but the shipped code + the 6-spirit roster + this WO's ruling are all
    **6** (owner pin #7 below).
  - `EchoCount` reads `GameState.EchoCount`, floors at 1 (:89).
  - `GlobalHarvestMultiplier => EchoCount` (:108) — WO-709's count-quadratic power curve.
  - `RatePerSecond => EchoCount * (BaseRatePerHour/3600) * GlobalHarvestMultiplier * (1+harvestRateTalent)`
    (:113) — **the total faucet is quadratic in count** (N echoes = N² × one echo's base).
  - `SiloCapacity => SiloCapHours * BaseRatePerHour * EchoCount` (:130) — **LINEAR in count.**
    Rate quadratic ÷ capacity linear ⇒ full-roster fill time SHRINKS as N grows (~40 min at N=6).
    Pre-existing cadence issue (owner pin #6).
  - `DumpSilos()` (:289) banks the pooled silo via `EconomyService.GrantSpendable`, split **even
    thirds Wood/Iron/Food** (:306-310; remainder → Wood). Crystals stays premium.
  - Offline fill reuses `GameState.LastHarvestClaimMs` (OfflineHarvestService's clock) — do NOT touch.
- `EchoAssignments` (`.../Harvest/EchoAssignments.cs`): the per-echo lane STORAGE seam (WO-681/658).
  - Lanes today: `wood` / `iron` / `food` / `idle` (:35-41), stored as a CSV in `GameState.EchoLanes`
    keyed by echo index; `LaneOf(index)` (:51) reads it (index 0 defaults `wood`, later indices `idle`).
  - Scope today (deliberate): STORES + REPORTS only — it does NOT split accrual or the dump mix yet
    (:11-15). **738 is where the rate/dump split finally consumes this field** (the WO-658 "rate-split
    half" this seam was built to host).
- `EchoRosterCatalog` (`.../Harvest/EchoRosterCatalog.cs`): the fixed 6-spirit CODE TABLE
  (Frosthowl/Ice, Verdant Stag/Nature, Voidwing Raven/Void, Stormcoil Serpent/Storm, Stonewarden
  Bear/Earth, Ember Phoenix/Fire; :59-103). Order == echo count. Fields today: Id/Order/DisplayName/
  Element/PortraitName/Flavor/Lore. **Its own documented growth path (:16): "If the roster ever needs
  to grow owner-tunable, promote this to echoes.json under Data/Canonical (dual-copy + md5) — not
  before."** ← the hook for 738's balance data (pin #4).
- `EchoRosterView` (`.../Harvest/EchoRosterView.cs`): the "pet box" grid, reachable via the HUD "Pets"
  button (`EchoUnlockFeedback.cs:157` → `EchoRoster.Open()`). Shows all 6 as portrait cards with
  owned/locked + live gather lane. **Cards are `raycastTarget = false`** (:246) — informational only,
  NOT tappable.
- `EchoCardView` + `EchoCardVM` (`.../Harvest/EchoCard*.cs`): the MVVM lane PICKER (name/portrait +
  WHAT/STATE lines + wood/iron/food chips → `vm.AssignLane`). **This picker is currently UNREACHABLE:**
  its only caller is `EchoInteractable.EchoCard.Open` (EchoWispInjector.cs:311/322), and
  `EchoWispInjector.RebuildIfHub` is **inert** — SCRAPPED per owner felt-test 2026-07-17 ("Echoes are
  portrait-card spirits, NOT 3D models"), returns before spawning any body (EchoWispInjector.cs:131-138).
  So no wisp → no `EchoInteractable` → `EchoCard.Open` has **no live caller**. Reviving reachability is
  a required deliverable (§ Picker/UI).
- Save: `SaveSchema.CurrentVersion = 32` (SaveSchema.cs:32). `echoCount`/`siloResources`/`wavesCompleted`
  (v25), `echoLanes` (v31, additive default-on-read; New Game seeds `"wood"` — GameStateService.cs:866).
  Round-trip test harness: `Assets/_Modules/Core/Tests/SaveLoadRoundTripTest.cs`.

**Real resources (the design's Herb/Essence/Energy/RefinedGoods/Mana DO NOT EXIST — map onto these):**
`ResourceType` (`Assets/_Modules/Core/ResourceType.cs`) = `Iron / Wood / Food / AetherCrystal`, each
mapped 1:1 to a real `GameState` wallet field (`GameState.Iron`, `.Wood`, `.Resources.Food`,
`.Resources.Crystals`). `Magic` also exists (tech-axis currency, not harvestable). **Stone is RETIRED**
(DEF-121). Echo bonuses must target these real fields only.

**Portraits — all 6 ARE committed** (`Assets/Resources/Echoes/Portraits/`): `Frosthowl.png` +
`VerdantStag.jpg` / `VoidwingRaven.jpg` / `StormcoilSerpent.jpg` / `StonewardenBear.jpg` /
`EmberPhoenix.jpg`. The design's **"Ice-Wolf.png" does not exist** — the committed Ice portrait is
`Frosthowl.png`. Default = Frosthowl (owner pin #5, low-risk confirm).

---

## Reconciled data model (the one-paragraph summary)

Keep WO-709's **count-quadratic total** as the untouched spine — the global operation multiplier stays
`GlobalHarvestMultiplier = EchoCount`, so unlocking an echo is still the big power spike. **738 layers
specialization on top as a DISTRIBUTION + a modest bonus, never a second competing global multiplier.**
Each of the 6 catalog spirits gains static balance fields (element→preferred-lane + element→resource,
a per-level `baseWeight`, an affinity multiplier, cross-bonus pairings). Each OWNED echo carries instance
state `{ assignedLane, level }` persisted through the existing `EchoLanes` seam (richer `lane:level`
token grammar, backward-compatible: a bare `wood` token reads level 1). Every echo contributes a **base
weight to ALL four lanes** (so no assignment is wasted) plus a **bonus weight to its assigned lane**,
**doubled (×affinity) when element matches lane**; the per-lane share = each lane's summed weight ÷ total
weight. For **Harvest** this weighting drives the `DumpSilos` split (element→resource) and adds a small
capped total bonus (+X% per correctly-matched echo); for **Crafting / Defense / Exploration** the same
weighting produces a passive multiplier stored on a new per-lane bonus contract that each host system
reads when it lands. **Element cross-bonuses** (adjacent-element pairs both owned) and a **6-of-6 set
bonus** apply as flat all-lane multipliers. Balance numbers live in data (recommended: a new
`Data/Canonical/echoes-balance.json`, dual-copy + md5 — NOT a ScriptableObject `.asset`), identity stays
in the code table.

---

## Data model — where each piece lives (data-only law honored)

Per **CLAUDE.md §4 / ARCHITECTURE_PRINCIPLES §2b** and the catalog's own documented path, this WO does
**NOT** introduce ScriptableObject `.asset` files. Two-part home:

1. **Identity stays in `EchoRosterCatalog` (code table).** Name/element/portrait/flavor/lore are FIXED,
   canonical, compile-safe — no runtime tuning. Add two *derived, non-tunable* fields inline:
   `PreferredLane` (enum) and `PrimaryResource` (`ResourceType`) — these are element identity, not
   balance knobs.
2. **Tunable balance → `Data/Canonical/echoes-balance.json` (NEW, dual-copy Resources +
   StreamingAssets + md5, loaded via `CanonicalJson`).** Holds the numbers the owner will re-tune in
   playtest: per-level `baseWeight` curve, `matchAffinityMult`, `baseLaneContribution`,
   `matchedEchoTotalBonusPct` (capped), `crossBonusPairs` + `crossBonusMult`, `setBonusMult`, `maxLevel`.
   Justification per the §"owner-tunable → JSON" trigger: WO-709 explicitly wants these balance-tuned
   ("tune base rates down to compensate"), so they must be editable without a recompile — the exact case
   the catalog's promotion note calls out. **Owner pin #4:** if you want V1 fastest, ship these constants
   in the code table and defer the JSON; recommended = JSON now (it's the law-aligned home and you'll tune).

**No parallel node/currency types.** All bonuses target existing `GameState` wallet fields via the real
`ResourceType`. Village → Core asmdef only; the bonus contract (below) lives in Core (like `GameModifiers`).

**Proposed element → lane / resource default table (OWNER PIN #1 — needs ruling):**

| Echo (element) | Preferred lane | Primary resource | Rationale (flavor) |
|---|---|---|---|
| Frosthowl (Ice) | Harvest | Food | winter's patience over the fields |
| Verdant Stag (Nature) | Harvest | Wood | the forest gives freely |
| Stonewarden Bear (Earth) | Harvest | Iron | hauls the heaviest loads |
| Ember Phoenix (Fire) | Crafting | (Forge) | sets the forge alight |
| Voidwing Raven (Void) | Exploration | (dungeon loot) | reaches what others cannot |
| Stormcoil Serpent (Storm) | Defense | (offline raid) | restless energy guards the walls |

(This spreads the 6 across all 4 lanes with a Harvest-heavy start so the FELT-NOW slice is rich. Owner
may re-map freely — the code reads the table, nothing is hardcoded per echo.)

---

## Save-schema plan (additive, default-on-read)

- **Reuse the `EchoLanes` seam — do NOT add a parallel `echoLevels` field** (per brief directive).
  Evolve the per-echo token grammar in `GameState.EchoLanes` from `"wood,iron,idle"` to
  `"harvest:3,crafting:1,idle:1,…"` (`lane[:level]`). `EchoAssignments`:
  - `Normalize()` gains the 4 top-level lanes `harvest/crafting/defense/exploration` (+ `idle`);
    **legacy tokens `wood/iron/food` normalize forward to `harvest`** (additive-safe — the shipped
    `"wood"` starter value keeps working, mapped to Harvest with Wood as the element resource).
  - New `LevelOf(index)` parses the `:level` suffix; **a bare token (no `:`) reads level 1** (default-on-read).
  - `SetLevel(index, level)` / the assign path rebuild the CSV with `lane:level` tokens.
- **`SaveSchema.CurrentVersion` bump: `32 → 33`.** The wire field `echoLanes` (string) is UNCHANGED in
  shape, so this is a **no-migrator** bump — the version documents the richer token grammar; old bare-lane
  values parse to Harvest/level-1 on read (same additive-default-on-read pattern as v31's echoLanes seed).
  Add the v33 line to the `CurrentVersion` doc-comment block (SaveSchema.cs:32). New Game seeding
  (GameStateService.cs:866) becomes `"harvest:1"` for the starter echo.
- **Owner pin #8 (impl detail):** richer-token vs a separate `echoLevels` CSV. Default = richer token
  (honors "extend the seam, not a parallel field"). If a clean parallel field is preferred, it's a v33
  additive nullable `echoLevels` (default-on-read → all 1) instead — same version bump.

---

## Math reconciliation — ONE coherent curve

**Spine (untouched):** `GlobalHarvestMultiplier = EchoCount` and `RatePerSecond` stay exactly as shipped
(WO-709 count-quadratic total). 738 does NOT add a second global multiplier.

**Specialization = weighting + a capped bonus:**
- Per echo `e` assigned to lane `L` at level `lv`:
  `weight(e, lane) = baseLaneContribution` for every lane (the "no echo wasted" floor),
  **plus** `baseWeight[lv]` added to lane `L`, **× matchAffinityMult** when `PreferredLane(e) == L`.
- Per-lane share `share(lane) = Σ_e weight(e,lane) / Σ_e Σ_lane weight(e,lane)`.
- **Harvest:** the `DumpSilos` pooled total is split by the Harvest-lane echoes' `PrimaryResource`
  weighting (replacing the even-thirds Wood/Iron/Food), and the TOTAL faucet gains a small
  `1 + matchedEchoTotalBonusPct × (#correctly-matched Harvest echoes)` factor, **capped** so it never
  rivals the count-quadratic spine (e.g. cap +30% total). Conservation invariant: the split still sums to
  the exact pooled silo integer (remainder → the top-share resource) — no resource created or lost.
- **Crafting / Defense / Exploration:** `laneMult(L) = 1 + specializationGain(L)` where
  `specializationGain` is derived from that lane's summed matched weight — stored on the bonus contract
  (below), consumed by the host when it lands.
- **Cross + set:** `crossBonusMult` (flat, applied when an adjacent-element pair is both owned) and
  `setBonusMult` (flat, all-lane, when all 6 owned) multiply in last.

All of the above compiles into **one `EchoLaneBonuses` contract** (Core, pure data, Newtonsoft/CanonicalJson,
mirrors `GameModifiers` shape): `{ harvestResourceShare: Dictionary<ResourceType,float>, harvestTotalMult,
craftYieldMult, craftSpeedMult, defenseBonusMult, dungeonLootMult }`. `EchoService.RatePerSecond` /
`DumpSilos` read the harvest fields; the three stubbed hosts read their field when wired. One read, one
contract — no per-system math scattered (the `GameModifiers` pattern).

**Silo cadence (owner pin #6):** today capacity is linear, rate quadratic → full-roster silo fills in
~40 min. 738 CAN fix this by making `SiloCapacity` consume `GlobalHarvestMultiplier`
(`SiloCapHours * BaseRatePerHour * EchoCount * GlobalHarvestMultiplier`) so fill-time stays ~constant as
the roster grows. This is a balance change — **do it here Y/N is an owner call.** Default = leave capacity
as-is and only note it (out of 738's core scope) unless owner says fix.

---

## Picker / UI plan (revive reachability + show level/bonus; MVVM strict)

**Reachability fix (required):** the built `EchoCardView` picker is dead (no live caller). Since the
wandering wisp is scrapped, wire the picker to the **roster grid** instead:
- In `EchoRosterView.BuildCard`, make **OWNED** cards interactable (`raycastTarget = true` + a tap
  handler) → `EchoCard.Open(index)`. Locked cards stay non-interactive. This gives `EchoCard.Open` a live
  caller through the surface the owner already reaches (HUD "Pets" button), no new proximity system, no
  wisp revival.
- `EchoCardView` / `EchoCardVM` extend (MVVM strict — VM owns all service reads, View is a dumb kit skin):
  - Lane chips become the **4 lanes** (`Harvest / Crafting / Defense / Exploration`) + the current selection
    shown AS TEXT (`(now)`), colorblind-safe (never hue alone). Stubbed lanes (Crafting/Defense/Exploration)
    render with a passive tag — e.g. `"Defense (passive — active when raids land)"` — so the agency is real
    now but the honesty about host state is explicit.
  - Add a **LEVEL** readout + the echo's **specialization bonus** line ("Ember Phoenix — Fire · Lv 3 ·
    Crafting +18% when matched"). Level-up affordance only if the level source is live (pin #2); otherwise
    show level as a read-only stat.
  - Keep the WO-681 first-meeting one-shot beat.
- `EchoRosterView` header/cards additionally surface each owned echo's **level** + **assigned lane** +
  **matched/unmatched** state (TEXT), and the **6-set** progress ("Set 4/6 — +X% all lanes at 6").
- All UI: ASCII-only, ElarionUiKit Obsidian master-frame (NO UXML — PIPELINE_STATE S8), one Close,
  PanelManager-registered, battle-lock respected, large touch targets (mobile).

---

## Per-lane host reality (honest — which have a live host NOW)

| Lane | Real host system (file) | State NOW | 738 plan |
|---|---|---|---|
| **Harvest** | `EchoService.RatePerSecond` + `DumpSilos` split (`.../Harvest/EchoService.cs`) | **LIVE** | Fold specialization into the dump-split weighting (element→resource) + a capped total bonus. **FELT-NOW.** |
| **Crafting** | Forge/processing yield. Nearest live seam = `GameModifiers.ResourceEfficiencyMult` (Forge), compiled from building tiers by `ModifierService` (`.../Core/State/GameModifiers.cs`). **No dedicated craft-speed/yield pipeline reads an echo contribution.** | **PARTIAL / no echo hook** | Compute + persist `craftYieldMult`/`craftSpeedMult` on the `EchoLaneBonuses` contract; **STUB** the consumption until the Forge/crafting loop reads it. **SPECCED-STUBBED.** |
| **Defense** | ArenaDefense placed-defenders, CoC-style (`.../Village/Arena/ArenaDefense*.cs`, `GameState.ArenaDefense`) + WO-729 "Defend & Watch" async sim (**flag-gated OFF by default**) + WO-730 async PvP. **Real "raided while OFFLINE" resolution is post-WO-730, UNBUILT** — no offline city-raid sim runs while away today. | **STUB / FLAG-OFF** | Compute + persist a passive `defenseBonusMult` (base HP/damage buff to placed defenders) that the async offline-raid resolver reads WHEN it lands. **STUB now.** Canon-honored: resolves ONLY in the offline async raid; **no echo ever spawns in a live fight.** **SPECCED-STUBBED.** |
| **Exploration** | **Dungeon system ONLY** (owner refinement 2026-07-17): `DeNelle.Dungeons` / `DungeonController` / dungeon loot (`GameState.ActiveDungeonRun.Loot` / `LootStash` in SaveSchema). Dungeon runs + loot exist; **no loot-multiplier hook.** Overworld / raid-out are OUT of scope. | **PARTIAL / no loot-mult hook** | Compute + persist `dungeonLootMult`, read at the **dungeon-run reward grant** only. **STUB** until wired. **SPECCED-STUBBED.** |

---

## Phased delivery (felt-now vs specced-stubbed)

**Phase A — FELT-NOW (ships + felt-verifiable this WO):**
1. Roster cards show **level + assigned lane + specialization bonus + matched/unmatched** (TEXT).
2. **Revive the picker:** owned roster card tap → `EchoCard.Open(index)`; picker shows the 4 lanes +
   level + bonus; assign persists.
3. **Harvest specialization live:** element+lane match bonus folds into the `DumpSilos` split
   (element→resource) + the capped total bonus; base-contribution-to-all so no echo is wasted.
4. Save: richer `EchoLanes` token grammar + level, `CurrentVersion 32→33`, round-trips.
5. `EchoLaneBonuses` Core contract computed + persisted; element cross-bonuses + 6-set bonus computed and
   shown in the UI (their Harvest portion is live; the non-harvest portion is stored).

**Phase B — SPECCED-STUBBED (defined + persisted + shown, host consumption stubbed until systems land):**
- `craftYieldMult` / `craftSpeedMult` → wired when the Forge/crafting loop consumes the contract.
- `defenseBonusMult` → wired into the offline async-raid resolver (post-WO-729/730).
- `dungeonLootMult` → wired at the dungeon reward grant.
- Each shows in the picker/roster as a passive, honest "active when <host> lands" state.

---

## Gates

- [ ] `COMPILE_GATE_OK` (`DeNelle.Editor.CompileGate.Run`) + NUL-byte guard (WO-434) on every `.cs` touched;
      brace-balance check per file.
- [ ] **EditMode tests** (mirror `SaveLoadRoundTripTest` / `EconomyServiceTests` / catalog tests):
  1. **Specialization curve math** — element+lane match bonus, base-to-all floor, cross-bonus, set-bonus
     locked to the ruled numbers (no dead multiplier types — the G3 pattern).
  2. **Assignment + level round-trip** through the richer `EchoLanes` token, incl. **backward-compat**
     (bare `wood` token → Harvest / level 1).
  3. **Save round-trip v32→v33** default-on-read (an old save with `echoLanes="wood"` loads Harvest/Lv1).
  4. **Dump-split conservation** — the weighted split sums to the exact pooled silo integer (no resource
     created/lost; remainder → top-share resource).
  5. `echoes-balance.json` dual-copy md5 parity (if pin #4 = JSON).
- [ ] `REGRESSION_OK` — `DataRegression` + AutoPilot fleet: simulate roster growth + assignments headless,
      assert per-lane shares + total match the ruled table; offline accrual still honors the spine.
- [ ] `[Flow:Echo]` step-in/out traces on assign / level-up / bonus-recompute (§12).
- [ ] Owner felt-pass on the picker + roster (PO closes; headless can't judge feel).

---

## What NOT to touch

- **`EchoService` count/unlock cadence** (`OnWaveCleared` / `GrantEcho`) and **`GlobalHarvestMultiplier =
  EchoCount`** (the WO-709 count-quadratic spine) — 738 layers on top, never replaces it.
- **The offline clock** (`GameState.LastHarvestClaimMs` / `OfflineHarvestService`) — reuse, don't reinvent.
- **Collect All spine** (WO-663) and the `DumpSilos` banking PATH (`EconomyService.GrantSpendable`) — only
  the SPLIT weighting changes, not the banking route.
- **Building-tier `GameModifiers` compile** (`ModifierService`) — echo bonuses are a **separate**
  `EchoLaneBonuses` contract, NOT folded into building-tier perks.
- **The ArenaDefense / async combat stack** — the Defense lane only READS a passive multiplier; it never
  spawns a fighting echo, never enters a live battle scene.
- **No ScriptableObject `.asset`** for echo data; **no hand-edited `.unity` scenes**; **Village → Core
  asmdef only**; **pool** any repeated UI spawns.

---

## Owner pins (decisions still needed before READY)

1. **Element → lane / resource mapping** (the 6-row table above) — confirm or re-map.
2. **Level-up feed source + max level.** What raises an echo's level? Candidates: waves defended,
   Population XP (WO-587), a new "echo essence" reward, or the WO-709 "echo special food" boost hook.
   Until ruled, level is a read-only stat (Phase A still ships with all levels = current/1).
3. **Bonus sizes:** matched-assignment bonus %/cap, base-to-all contribution weight, per-level `baseWeight`
   curve, element cross-bonus pairings + size, and the 6-of-6 set-bonus size.
4. **Data home:** balance numbers in the code table (fastest V1) vs `Data/Canonical/echoes-balance.json`
   (dual-copy, recompile-free tuning — RECOMMENDED per data-only law). Your call.
5. **Portrait:** all 6 are committed; the design's "Ice-Wolf.png" isn't a thing — default the Ice echo to
   the committed `Frosthowl.png`. Confirm (low-risk).
6. **Silo cadence:** fix the rate-quadratic ÷ capacity-linear ~40-min-fill issue HERE (make capacity
   consume `GlobalHarvestMultiplier`) — Y/N?
7. **Cap reconciliation:** shipped `MaxEchoes=6` + a 6-spirit roster + this ruling all say **6**, but
   WO-709 was pinned cap-5. Confirm **6** (this WO assumes 6, superseding WO-709's 5) and the unlock
   cadence for the 6th (wave 25 at WavesPerEcho=5?).
8. **Save encoding:** richer `EchoLanes` `lane:level` token (default) vs a parallel `echoLevels` CSV — impl
   detail, default = richer token per your "extend the seam" directive.

---

## Source design (owner paste, pre-reconciliation)

> ⚠ The full verbatim AI-authored design text was NOT included in this WO's authoring brief (only the
> distilled ruling + the flagged non-existent references were provided). Committer/owner: paste the
> original design blob here for traceability. Captured from the brief:
>
> - Path B ruling: 6 collectible Echo spirits, each with an element, a level (1..max), and ONE
>   player-assigned lane (agency). Correct element+lane = a noticeable bonus; a base contribution to ALL
>   lanes so no echo feels wasted; element cross-bonuses; a set bonus for all 6.
> - Lanes: Harvest (resource faucet), Crafting (Forge/processing yield+speed), Defense (passive OFFLINE
>   city-raid defense bonus — echoes NEVER fight in real time), Exploration (passive dungeon-only loot
>   bonus — owner refinement 2026-07-17: dungeon loot/clearing ONLY, not overworld/raid-out).
> - **Non-existent references in the original design (corrected in this WO):** resources
>   "Herb / Essence / Energy / RefinedGoods / Mana" (do not exist → mapped to real Wood/Iron/Food/Crystals);
>   portrait "Ice-Wolf.png" (not committed → the committed Ice portrait is Frosthowl.png). The design was
>   written without the repo in front of it; this WO reconciles its intent onto the built EchoService /
>   EchoAssignments / EchoRosterCatalog / EchoCard systems and the real ResourceType/SaveSchema.

---

## Design amendments (owner rulings 2026-07-17, post-implementation)

These refine the Phase-B stub lanes + the onboarding gap. Binding; build to these next.

1. **Defense lane = flat +X% CITY DEFENSE (NOT the offline async-raid resolver).** Drop the
   post-WO-729/730 async-sim plan for Defense. Defense simply applies a flat +X% to the whole
   city's defensive package, CoC-style and BROAD: defensive **structures' damage AND health**
   (towers, walls/gates, the Heart) — not just tower damage. Passive stat buff; echoes still NEVER
   fight live (canon holds). Hook: `EchoLaneBonuses.DefenseMult` → a broad defensive buff via the
   `GameModifiers` TowerDamageMult / structure-HP seams. This is the "easy one" and the next lane to
   wire (it gives the player a real second lane, which the onboarding below depends on).

2. **New-player onboarding: teach the CLAIM LOOP first, defer lane-assignment.** The starter Echo
   auto-assigns Harvest and auto-harvests passively — a new player needn't DO anything for it to
   work, so "assign a lane" is the wrong first lesson (and hollow while only Harvest is live). First
   lesson = "you have a helper gathering resources while you defend; come back and tap to claim the
   silo." Teach specialization/lane-assignment only once 2+ lanes do something (i.e. after Defense
   +X% lands → taught at echo #2's unlock). The existing `EchoTutorialUI` (WO-360) is STALE — it
   teaches the retired combat-pet model; do not reuse it for the workforce Echo.

3. **A teaching CONVERSATION at EVERY Echo unlock (progressive, cumulative).** Each of the 6 unlocks
   (start + every 5 waves) shows a short in-fiction conversation that teaches how the lanes/abilities
   work TOGETHER, deepening as the roster grows so the player learns to get more done: #1 = claim loop
   only; #2 = assign lanes + element match + the Harvest-vs-Defense fork; #3-#5 = synergy / splitting
   the roster / cross-bonuses; #6 = full-roster set bonus. Wire through the existing
   `EchoUnlockDialogue.Show` path — which today fires ONLY on the `EchoUnlocked` event (echo #2+), so
   **echo #1 needs a first-meeting fire added** (no dialogue fires for the starter today). Conversation
   copy is being generated by the creative/narrative seat (ASCII-only, colorblind-safe, honest about
   which lanes are live — Harvest now, Defense landing, Crafting/Exploration foreshadowed-not-taught).
   The creative-final copy is in the next section.

### Build-ready conversation copy (creative-final 2026-07-17 — implement VERBATIM, ASCII-only)

Wire each block to the Echo unlock at that count via `EchoUnlockDialogue.Show(EchoRosterCatalog.ByCount(N), N)`.
Two speakers: **Sylas** (the Steward / mentor) + the awakening **Echo**. TITLE = card title; TELL ME MORE
= the deeper lore line behind the dialogue's "Tell me more" button. Strings are ASCII-only (plain hyphen,
straight quotes) — do NOT re-introduce em-dashes/curly quotes (TMP tofu). **Echo #1 has NO unlock event
today** (`EchoUnlocked` fires only for count 2+), so add a one-shot first-meeting fire for the starter or
Frosthowl's conversation never shows. Honesty rule held: only the two LIVE lanes carry instructions —
Harvest's claim loop (all game) + Defense's city-defense stacking (from #2 on); Crafting/Exploration stay
pure "not yet stirred" flavor until their systems land.

**Voice framing:** Sylas carries the lesson (plain, kind, names the mechanic like pointing at a tool the
player half-understands); the Echo answers with one mythic line of its own (strange, old). Keep every
exchange to a breath — the player reads it standing at the silo.

**UNLOCK 1 — Frosthowl (Ice | prefers Harvest) — starter, needs first-meeting fire**
TITLE: A Gift In The Cold
- Sylas: The glacier sent us a worker. While you hold the wall, Frosthowl fills the silo for you.
- Frosthowl: I hunted these reaches before your kind named them. Cold is patient. Cold gathers.
- Sylas: You do nothing but fight. When the silo is full, tap it to claim what it saved you.
TEACHES: A helper gathers resources into the silo while you defend - tap the silo to claim it into your wallet.
TELL ME MORE: Frosthowl's winter does not freeze the work, it steadies it - every haul comes slow, sure, and never spilled.

**UNLOCK 2 — Verdant Stag (Nature | prefers Harvest) — at 5 waves**
TITLE: Two Spirits, Two Paths
- Sylas: Now you have two. That means you choose where each one works. Open the lanes and assign them.
- Verdant Stag: I remember every seed the forest ever sowed. Set me to Harvest and the land gives freely.
- Sylas: The Stag is strongest at Harvest - its own element. Or send a spirit to Defense to add city defense instead: stronger walls, tougher Heart. Gather more, or fortify. Your call.
TEACHES: Assign each Echo to a lane; matching its element (Stag to Harvest) makes it stronger, and Defense is the second real choice - a flat boost to all your defensive structures' damage and health.
TELL ME MORE: Where the Stag steps, roots wake early and berries swell out of season - Harvest is not its labor, it is its nature.

**UNLOCK 3 — Voidwing Raven (Void | prefers Exploration) — at 10 waves**
TITLE: Do Not Pile Them All
- Sylas: Three spirits now. Do not stack them all in one lane - spread them and you get more done overall.
- Voidwing Raven: I slip between worlds and carry back what no hand can reach. My lane is not yet open to you.
- Sylas: Its true calling, Exploration, still sleeps. For now set it to Harvest or Defense - it helps in any lane, just less than a spirit that loves it.
TEACHES: Splitting your roster across lanes gets more done than piling into one - and any Echo still helps in any lane, even one that isn't its favorite.
TELL ME MORE: The Raven flew the night the first star went out; when its dark road finally opens, it will bring home spoils no dungeon meant to give up.

**UNLOCK 4 — Stormcoil Serpent (Storm | prefers Defense) — at 15 waves**
TITLE: The Wall That Answers Back
- Sylas: Send Stormcoil to Defense - that lane is its element, so the boost runs deeper than any other spirit could give there.
- Stormcoil Serpent: I was born of a sky that would not stop raging. Bind me to the walls and they will rage too.
- Sylas: Two spirits on Defense stack their strength together. The more you fortify, the harder your towers hit and the longer your Heart holds.
TEACHES: Matching the Storm Echo to Defense maxes its buff, and multiple Echoes on Defense stack their city-defense bonus.
TELL ME MORE: The Serpent does not throw a single bolt in the fight - its restless charge simply runs through the stone until the whole wall hums and refuses to fall.

**UNLOCK 5 — Stonewarden Bear (Earth | prefers Harvest/Iron) — at 20 waves**
TITLE: Wood And Iron
- Sylas: The Bear favors Harvest like the Stag - but its element is Earth, so it pulls Iron where the Stag pulls Wood.
- Stonewarden Bear: I slept beneath the roots of the world. Nothing I carry is too heavy.
- Sylas: Run both harvesters and your silo fills with two goods at once. A real economy now - feed it while you hold the line.
TEACHES: An Echo's element sets which resource it favors (Nature to Wood, Earth to Iron), so two harvesters build a two-resource economy.
TELL ME MORE: The Stonewarden hauls what would break a dozen backs and never slows - tireless is not a boast for it, it is simply the shape of stone.

**UNLOCK 6 — Ember Phoenix (Fire | prefers Crafting) — at 25 waves, roster complete**
TITLE: The Last Light Rekindled
- Sylas: The sixth has come. All six spirits awake grants the set bonus - every lane sharper, the whole realm answering as one.
- Ember Phoenix: I have burned and risen a thousand times. My forge-fire, Crafting, is not yet stirred - but it will wake.
- Sylas: When the Forge opens, Ember belongs to it. Until then, place it anywhere and complete the circle. Elarion holds its light again.
TEACHES: Owning all six Echoes grants the set bonus that strengthens the whole roster - the arc is complete.
TELL ME MORE: The Phoenix does not craft with hammer or hand; its fervor simply catches, and every other spirit works a little brighter for the heat.

---

*Cross-refs:* `ECHO_WORKFORCE_SPEC.md` · `WorkOrders/WORK_ORDER_709_echo_workforce_multiplier_hud.md`
(global multiplier + skill-tree→passive direction) · `WORK_ORDER_681_echo_select_intro_and_assign.md`
(the picker) · WO-658 (assignment rate-split half — this WO lands it) · WO-587 (population/slots) ·
WO-729/730 (async defense host) · `docs/ARCHITECTURE_PRINCIPLES.md` §2b/§2c ·
`docs/LORE_FALL_AND_FOUNDING_OF_ELARION.md` (who the Echoes are).
