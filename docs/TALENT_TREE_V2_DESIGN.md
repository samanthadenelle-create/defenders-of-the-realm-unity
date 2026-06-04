# Talent Tree v2 — depth, real impact, Witcher-style ranks + respec

> Owner ask (2026-05-31): the Hero Talent tree *"needs better levels and actual impact in game"* —
> *"deeper veins, better rewards deeper down"* — and *"allow tiers like in Witcher: de-level a talent then
> go deeper into it."* Today the tree is flat: 6 nodes/hero (3 tiers × 2 cols), each a **one-time binary
> unlock** bought with Wisdom, summing flat `damageBonus`/`cdReduction`. This designs v2. Creative/design;
> grounded in `HeroTalentCatalog`/`HeroTalentModifiers`/`TalentTreePanel`. No code/bake.

## Current state (verified) + the gaps
- `HeroTalentCatalog`: per-hero `nodes[]` = `{id, name, tier(1-3), column(a/b), cost, prereqs,
  damageBonus, cdReduction}`. **6 nodes/hero, binary Learned/not.** Costs 1/2/3 Wisdom by tier.
- `HeroTalentModifiers` sums unlocked nodes' `damageBonus`/`cdReduction` into HeroAbilities' math.
- **Gaps owner named:** (1) **shallow** — only 6 binary nodes, no ranks; (2) **weak impact** — most nodes
  are +10/15% damage or −10% cd, numbers you barely feel; the *flavorful* effects (chain, AOE, zone) are
  great but locked to single unlocks; (3) **no respec** — can't pull points back; (4) **no "deeper = more"
  payoff** — tier 3 isn't dramatically more rewarding than tier 1.

## v2 — three changes

### 1. RANKED talents (Witcher-style depth): pour multiple points into one node
Each talent becomes **multi-rank** instead of binary. You invest **rank by rank**, and **deeper ranks pay
off bigger** (super-linear — the owner's "better rewards deeper down"):
- A node has `maxRank` (e.g. 3–5). Each rank costs Wisdom (rising per rank) and scales its effect.
- **Ranks unlock qualitative breakpoints, not just bigger numbers** — e.g. *Honed Edge*: R1 +10% dmg →
  R3 the double-hit chance → R5 it always double-hits + cleaves. So digging deep into ONE talent
  transforms how it plays (the depth + real impact owner wants), vs. spreading thin for small % bumps.
- **"Deeper veins" gating:** higher ranks of a node may require hero level / tier prereqs, so the best
  payoff is a real investment, not turn-1 affordable.

### 2. Witcher-style RESPEC: de-level a talent, refund, redistribute deeper
The Witcher mechanic the owner named — **pull ranks back out and re-spend**:
- **De-level a talent** → refunds its Wisdom (full or a tunable %, e.g. 100% in town / a small loss in
  field — owner's call), freeing points to **pour deeper into another node** or **respec a build**.
- Lets the player **experiment**: try spreading, then de-level and **go deep** into one vein for the big
  breakpoint. Encourages mastering builds, not fearing a wrong click.
- Reuse the **existing refund infrastructure** (`ServerConfig.RefundRate`/`CrystalRefundRate` already
  models a refund fraction for empowerment — mirror that for Wisdom/talent refunds). Wisdom is the
  currency; refund returns Wisdom.
- A **"Respec" button** (free in town, or a small Wisdom/crystal fee) for a full reset, plus per-node
  de-level for surgical changes.

### 3. ACTUAL in-game impact (make talents matter)
The owner's core complaint — talents don't feel impactful. Fix by:
- **Bigger, qualitative effects at depth** (above) — a maxed talent should visibly change combat, not just
  nudge a %.
- **Wire EVERY talent's effect through to gameplay** (audit `HeroTalentModifiers` → HeroAbilities): the
  flavorful nodes (chain, AOE, zone, taunt, mark) must actually fire in combat, not just sum a number.
  Confirm each node's described effect is *implemented*, not cosmetic text. (Several read as real effects —
  verify they're hooked; flat % nodes should be the minority, the signature effects the majority.)
- **Feed the bigger systems:** talents should interact with the party/targeting (WO-169) — e.g. a tank's
  taunt talent, a healer's bigger heal — so ranks deepen the party combat, not just solo stats.

## Data shape (for the eventual WO — reconcile, don't rebuild)
- Extend `HeroTalentNodeDef` with `maxRank` + **per-rank effect arrays** (`damageBonus[]`/`cdReduction[]`/
  a `behaviorUnlockAtRank` for the qualitative breakpoint). Track **current rank per node** in GameState
  (persisted), not a binary set.
- `HeroTalentModifiers` sums the **current-rank** effect of each node.
- Respec/de-level: a method that decrements a node's rank + refunds Wisdom (reuse the RefundRate pattern).
- All ranks/costs/effects in the **catalog JSON/SO** — tunable, not hard-coded (it's already JSON-driven —
  extend the schema). Same applies to the **Pet skill tree** (`PetSkillTreeCatalog`) — give it the same
  ranked+respec treatment for consistency.
- UI (`TalentTreePanel`/`HeroTalentPanel`): show **rank pips** (●●○○○), a de-level/respec control, and the
  next-rank preview — and restyle to the themed HUD (ties WO-178/175 UI-polish pass).

## Acceptance criteria
1. Talents are **multi-rank** (pour multiple Wisdom into one node); deeper ranks scale **super-linearly** + unlock **qualitative breakpoints**, not just bigger %.
2. **Respec works** — de-level a node refunds Wisdom (reuse RefundRate pattern); a full-respec control exists; points can be redistributed deeper.
3. Every talent's effect is **actually wired to combat** (audit confirms — no cosmetic-only nodes); signature effects (chain/AOE/taunt/etc.) fire.
4. Ranks/costs/effects are **data-driven** (catalog JSON/SO, tunable); current rank persisted in GameState.
5. Same ranked+respec model applied to the **Pet skill tree**; UI shows rank pips + de-level + next-rank preview (themed).
6. Brace balance; data-driven; no bake.

## Open questions for owner / creative
- **Max rank per node** (3? 5?) and **refund rate** (100% always, or a field penalty)?
- **Respec cost** — free in town, or a Wisdom/crystal fee per full respec?
- **Tree size** — keep 6 nodes/hero but make them ranked-deep, or also add more nodes/tiers (deeper tree)? (Owner "deeper veins" could mean either — recommend ranks first, more nodes later.)
- Creative pass on the **qualitative breakpoints** per node (which rank unlocks the transform) — the fun lives here.

🤖 Creative/design doc (UI lane). Grounded in HeroTalentCatalog/HeroTalentModifiers/TalentTreePanel,
PetSkillTreeCatalog, ServerConfig.RefundRate, WisdomCurrencyService. No code/scene/bake.
