# WORK ORDER 1063 — Gear identity, economy, affinity, VFX, and store program

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: IMPLEMENTED — AWAITING COMBINED OWNER GATE
**Owner:** Samantha · **Implementation:** Codex, uncommitted batch 2026-08-22  
**Children:** WO-1064 through WO-1068

## Goal

Create one coherent chain:

`real daily Gold -> price -> stats/effect -> enemy matchup -> Elarion name -> VFX verb -> visual certification -> store comparison -> hot swap`

No link may lie about another. VFX is not damage, flavor is not a functioning status effect, and an
Addressable key is not proof that a weapon sits correctly in a hand.

## Owner requirements

1. The rewarded Daily Chest pays **1,000 Gold once per UTC day** and is a primary price denominator.
2. Starter gear is granted; stores sell meaningful alternatives and progression.
3. Elemental gear has strengths and weaknesses. Matching affinity resists; no immunity.
4. Curated weapons may add bounded DoT, burn, poison, slow, cleave, pierce, armor break or execute.
5. Mechanical identity is decided before names; final names and lore use Elarion vocabulary.
6. Gear authors semantic VFX verbs; the owner-approved registry chooses the prefab.
7. Store selection shows exact signed offsets and truthful effects/matchups before purchase.
8. Only visually certified weapons enter the Forge. Armor requires a correct 2D image and live stats,
   not a 3D equip model.

## Sequence

1. **1067 foundation:** install inventory, geometry evidence and readiness gates before curation.
2. **1064:** measure all daily Gold and establish prices.
3. **1065:** add the single affinity/resistance combat seam.
4. **1066:** curate effects, narrative names and VFX verbs after vocabulary and certification exist.
5. **1068:** extend PartyShop with comparison and loadout guidance only.

1064 and the inventory/capture setup of 1067 may overlap. 1067 and 1065 must precede 1066. 1068 integrates
only rows proven by 1064–1067.

## Shared laws

- Catalogs own facts; combat owns arithmetic; pricing owns valuation; VFX registry owns prefab choice;
  presentation owns wording and layout.
- One affinity resolver and one effective-stat comparison authority.
- Enemy affinities are authored, never inferred permanently from name, model, biome or region.
- Missing VFX suppresses presentation only; gameplay still resolves and logs the missing mapping.
- A nonfunctional effect adds zero price and is never advertised.
- Board is generated with `python tools/board_build.py`; never hand-edit `BOARD.html`.

## Program acceptance

- [ ] Complete source-proven daily Gold ledger.
- [ ] Prices expressed as chest-only days and active-player days.
- [ ] Every damage path applies affinity exactly once.
- [ ] Every advertised effect has a consumer and regression.
- [ ] Every elemental weapon has an Elarion name, functional subtitle and semantic VFX verbs.
- [ ] Every sellable weapon has owner-viewed visual evidence.
- [ ] Store offsets equal live applied values and hot-swap eligibility is explicit.
- [ ] Compile/data/economy/combat/VFX/UI gates green plus owner device verification.

## Do not

- Do not tune price before measuring faucets.
- Do not expose uncertified art to fill shelf quantity.
- Do not create per-id combat switches or raw prefab keys in weapon data.
- Do not close visual/UI work from static gates alone.
