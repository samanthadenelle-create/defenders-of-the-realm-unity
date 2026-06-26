> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Store + Equip System — owner spec (2026-06-18)

Owner-directed, BINDING. Follows `docs/ITEM_MODEL.md` (data-driven catalog),
`docs/ARCHITECTURE_PRINCIPLES.md` (MVVM presentation, [[ui-mvvm-binding-seam]]),
`docs/STORE_SPEC`-style native panel (Yarn = narrative only, [[yarn-narrative-only-transactions-native]]).
Deliver COMPLETE + verified ([[deliver-complete-verified-not-piecemeal]]).

## The shop window (weapon shop / armor shop) — fully functional

1. **Party-member selector, top-left.** On entering a shop, show **icons top-left, one
   per current party member** (count = party size). **Tap a member to select them.**
2. **Tap → filter.** Selecting a party member **filters the list to only what THAT
   member can wear/equip** — by class (`job`), armor weight (`ArmorFitsClass`:
   Knight/Cleric=heavy, Ranger/Mage=light), and level (`req.level`). You buy/equip FOR
   the selected member.
3. **Only the buttons needed — NO extras.** Exactly **ONE buy button**. **No two buy
   buttons, no duplicate sell bar.** (Kills the "two sell bars" / double-buy issue.)
4. **Unified buy + sell, one screen, single click.** Selling gear to afford new gear
   happens IN the shop — you never leave the screen to sell then return. Buy/sell is a
   single tap on the item.
5. **Real item image.** The one buy button **renders a picture of the ACTUAL item**
   (the Addressable prefab thumbnail / `iconPath`), not a blank/emoji.
6. **Item details + why it's better.** Show the item's **stats + buffs** so the player
   makes an informed decision — e.g. "+better defense", "parry +5% window", "adds burn
   damage". Surface deltas vs the currently-equipped piece where possible.

## Equip-slot rules (hands)

- Slots: **main-hand + off-hand.**
- **1-handed weapon + shield = allowed** (both slots filled).
- **2-handed weapon takes BOTH slots:** equipping a 2H **removes the shield/off-hand**;
  equipping a shield/off-hand while a 2H is held **removes the 2H weapon.** Mutually
  exclusive, enforced at equip.

## Data it reads (no data change — all present in the catalog)
`job`, `weight` (ArmorFitsClass), `req.level`, `hand` (1h/2h), `damageType`, `prefabPath`
(Addressable key for the image), `defense`/`damageMult`/`hpBonus` + any buff fields.
Armor render = the hero→Blink-rig + mesh-swap slice (separate, sequenced after).

## Build standard
Native code-built uGUI MVVM (mirror ShopPanel/ShopVM + ElarionUiKit), NOT Yarn, NOT UXML.
ViewModel owns state (party selection, filter, buy/sell), View binds. Behind a flag until
the owner confirms felt. Ships COMPLETE + regression-gated, not in slices.
