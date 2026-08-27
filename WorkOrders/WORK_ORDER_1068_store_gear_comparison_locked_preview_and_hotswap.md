# WORK ORDER 1068 — Store comparison, future preview, and hot-swap value

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Parent:** WO-1063 · **Requires:** WO-1064 through WO-1067

> ### ⛔ TWO OF THE THREE THINGS IN THIS TITLE ALREADY SHIP. DO NOT GREENFIELD.
> Added by CLI review 2026-08-22, before assignment, because the title reads like
> three new features and only ONE of them is.
>
> | Concern | State | Where it lives |
> |---|---|---|
> | **Locked preview** | **DONE** — `WO-960 armor store locked preview window`, implemented + gated 2026-08-10, RESULT filed | `PartyShopPanelMvvm` / `ArmorStoreLockedWindowRegression` |
> | **Store preview pane** | **DONE** — `WO-486`, audit-verified as shipped 2026-08-21 | `PartyShopPanelMvvm` preview well + live `RenderTexture` rig |
> | **Comparison + hot-swap value** | **GENUINELY NEW** — this is the ticket | to build |
>
> ⚠ **THE STORE THIS TICKET MEANS IS THE GEAR SHOP, NOT THE NIGHT MARKET.** Two
> different panels answer to the word "store": `PartyShop` (PanelId 5 — weapons,
> armor, gold, and the target here) and `RealmStore` (PanelId 13 — packs, real
> money, the monetization lane). Pointing this work at `PackStore.cs` would land it
> in the wrong panel AND collide with the live UI-001 rebuild. `WO-501` is also the
> gear shop and still reads READY although all four of its owner points shipped —
> read it before starting, do not re-implement it.
>
> Scope this ticket to **comparison and hot-swap guidance ONLY**, extending
> `PartyShopPanelMvvm`. Everything else here is verification that the existing
> surfaces still hold.

## Outcome

Selecting gear shows exact, live before/after value and tactical identity before spending Gold.
Build one presentation-neutral comparison from the same effective-stat authority combat uses; do not
use nominal base-20 damage when selected-hero output can be resolved.

## Weapon preview

- Effective damage and signed delta.
- Reach and signed delta.
- One/two-hand and shield consequence.
- Damage type and affinity.
- Strong/resisted matchups with exact percentages.
- Effect with proc, duration, stacks/cooldown.
- Current/candidate item level and next Improve preview.
- Hot-swap eligibility, attack-style label and slot consequence.
- Resolved price and affordability.

Example:

```text
THE RIMEBOUND VIGIL — Epic Ice Greatsword
Damage 42 (+11) · Reach 4.4m (+1.0m)
Two-handed — shield removed
Strong vs Flame (+25%) · Resisted by Ice (-25%)
25% Slow for 2.5 sec · HOT-SWAP READY: Heavy Control
Purchase — 14,000 Gold
```

Armor shows effective defense/HP offsets, weight/class fit, levels, price and a large normalized 2D
image—never an empty 3D cavity. Color supplements signs/words; it is never the only signal.

## Shelf and hot swap

- Starter gear is granted.
- Curated primary shelf: two current-tier primary choices where certified content exists.
- Preserve a class-primary family slot; shields/any-class rows cannot evict all swords/bows/staves.
- Separate off-hands/ammunition where comparison semantics differ.
- Add the approved near-future locked window to Forge: greyed, `Unlocks at Lv N`, no deep-future wall.
- Provide `All Wares` for unlocked certified gear rather than placing all 96 cards on the main shelf.
- Dungeon/quest discovery may unlock later vendor replacements when explicitly authored.
- Mark `HOT-SWAP READY` and explain style (Reliable, Heavy, Control, Burn, Anti-Hollow).
- Buying never silently overwrites a hot-swap assignment; existing BattleLock/persistence remains the
  authority.

## Gates and captures

- Comparison deltas equal actual before/after runtime results.
- Displayed price equals debit.
- Every shown effect has a consumer.
- Locked rows cannot buy/equip and state exact unlock.
- Non-ready visuals never appear.
- Hot-swap marker equals real eligibility; purchase does not mutate assignment.
- Capture sword, element, two-hand tradeoff, Ranger ammo, Mage staff and armor cases on target device.

## Do not

- Do not calculate combat/pricing in the View.
- Do not show raw ids, VFX verbs or prefab names.
- Do not use a blank armor model cavity.
- Do not auto-replace player attack style on purchase.
- Do not expose the full catalog on the primary shelf.
