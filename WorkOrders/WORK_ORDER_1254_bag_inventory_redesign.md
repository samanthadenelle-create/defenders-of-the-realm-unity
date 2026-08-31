**Status:** FIXED — D-LAYOUT/D-SHELF/Trade implemented and regression-gated; D-SEAT cause instrumentation added without changing approved offsets. Awaiting final Seeker town/battle/cave captures and owner felt verification (2026-08-30).
**Jeweler progression follow-up (2026-08-29):** DEVICE-PRESENT in Seeker APK 2026.08.29.346849; owner test pending, including
first real dungeon-return proof. The persistent `EverAcquiredItemIds` ledger is now written only
by `VillageInventory.AddEarned`; the authoritative dungeon payout uses that seam, while ordinary
shop/dev `Add` cannot reveal the Jeweler. `JewelerProgression.IsUnlocked` remains true after the
stone is spent, duplicate exit callbacks cannot grant the guaranteed introduction twice, and the
Jeweler panel refuses direct routes before unlock. The repository has no shared Market/Crafting
navigation model to re-home: Market is reached through `OpenShop`/PartyShop and Weaponsmith/
Armorer vendor contexts; transformation stations independently route through `PanelId.Crafting`,
`PanelId.ConsumableCrafting`, and `PanelId.JewelerCrafting`. Therefore the new Bag navigation must
use those existing destinations, put Weaponsmith + Armorer in Market only, and include Jeweler in
Crafting only when `JewelerProgression.IsUnlocked` (hidden and excluded from counts beforehand).
The owner-approved fallback tuning now authors subsequent eligible completed dungeons at a pinned
15% rough-stone chance; the first dungeon-earned stone still bypasses RNG and is guaranteed once.
Each runtime run atomically claims one reward evaluation, so exit retry/re-entry cannot grant or
roll twice. The return-home Obsidian discovery modal explains that the introduction is guaranteed
but later stones are uncommon and not every dungeon contains one, routes into Jeweler, and persists
completion only after the authoritative polish queue accepts the first rough-stone action. The
Jeweler station and direct panel route stay absent/refused before the monotonic earned-history gate.
There is still no shared Bag/global Market-Crafting navigation bar in the repository to re-home;
Weaponsmith and Armorer remain their existing Market/vendor world routes, while Crafting surfaces
remain transformation-only PanelRouter destinations. Do not create a duplicate menu authority.
Fresh compile evidence: `Builds/compilegate-jeweler-ftue-final.log` (`COMPILE_GATE_OK`). Device
dungeon-return and installed-APK proof remain required, so status stays READY.
**Owner flag follow-up (2026-08-29):** READY pending corrected Seeker APK. Device evidence
`Logs/device/wo-no-shield-current.png` showed the Gear landing state listing the same five
equipped slots twice while the right pane still said `Nothing selected`. The Gear pane now
contains action guidance plus only the open-slot names; each authoritative worn row opens its
matching replacement category. The Off Hand row remains visible and still reports the live
loadout, so this presentation change does not hide or claim to fix the separately flagged
shield-render defect. Focused source regression requires both the non-duplicating guidance path
and the worn-row navigation seam.
**Minted:** 2026-08-27 (UI/UX design seat). Number = CLI main-line next free **1254** (read off `CLI_LANES_WO_NUMBERS.md` banner: `RECONCILED 2026-08-27 (CLI): main line next free = 1254`).
**BANNER:** this seat cannot bump `CLI_LANES_WO_NUMBERS.md` (read-only). **Orchestrator must bump the CLI main-line row 1254 -> 1255 in the SAME edit that records this mint.** A mint on disk without the banner bump is the collision.

**Implementation evidence (2026-08-28):** the runtime Bag now uses six full-width kit tabs, lands
on Gear, separates Off Hand from Weapons, renders horizontal 240px cards with 40% next-card peek,
an explicit remaining count, and a permanent overflow scrollbar. Forge has a true Off Hand category;
its capped shelf independently reserves the strongest main-hand and off-hand. WORN and replacement
comparison use `EquippedOffHand`, and equip routes through `EquipOffHandById`.
`COMPILE_GATE_OK` (`Builds/ready-integrated-compile.log`) and `REGRESSION_OK` 316/316
(`Builds/ready-integrated-regression-retry.log`). The town/battle/cave rendered-shield capture is
still owed, so this is not a felt-device claim.
**Assigned:** CLI implements from this ratified design. UI writes no `.cs` (CLAUDE.md section 2).
**Silo:** HUD/UI presentation (Bag) + two named sibling defects (shield seat, shield shelf). Do not bury either sibling inside the layout work.
**Class:** REDESIGN. The screen works. It does not serve. Successor to **WO-1133** (CLOSED 2026-08-23, Armory Rail shipped). This is not a restyle of that rail.

> **Owner, felt pass 2026-08-27 (verbatim):** she opens Bag and it takes her to an inventory screen that is "not very clean", "very confusing". Almost impossible to know you have to scroll down — no indication on the bag/gear screen that more content is below. She wants a better information architecture. She is open to each category on its own tab with a carousel (gear -> weapons -> armor -> trinkets) **OR** tabs across the top. She does not know which; this spec recommends one.
>
> **Same felt pass, do not ignore:** Knight/Grom — bag says a shield is worn, but it does not render on the character (town, battle, cave). Sword shows. She expected shields to be purchasable as offhand weapons; they are in neither Weapons nor Armor in the store, and there is no default assigned that she can see on the body.

**Evidence already on disk (open these, do not re-derive from vibes):**

- `docs/ui-evidence/2026-08-21_inventory_weapons_seeker.png` — pre-WO-1133 Seeker capture (Grom, landscape 2670x1200). The tab-row / empty-preview / 5x5 grid the rail was built to kill.
- Live source after WO-1133: `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs`, `InventoryGrid.cs`, `InventorySidebar.cs`, `HeroInventoryController.cs`, `InventoryVM.cs`.
- Store thinning that hides shields: `VendorStockResolver.cs:606-610` (named in-code) + `vendors.json` Forge `categories: ["weapon"]` + `perLevelCap: 2`.
- Shield attach path: `EquipmentController.EquipOffHand` / `AttachOffHandProp`. Starter: `StarterLoadout` knight = `knight_starter` + `knight_shield_starter`.
- Kit scroll that the bag does **not** use: `ElarionUiKit.MakeScrollZone` (`ElarionUiKitObsidian.cs:3225-3342`).
- Kit tabs that the bag does **not** use: `ElarionUiKit.BuildTabRow` (`ElarionUiKitConformance.cs:104-131`).

**Build target:** Seeker landscape **2670x1200 is primary**. Portrait is designed because the kit canvas is `1080x1920` match `0.5` and other panels are captured at both aspects — **runtime autorotate is OFF** (`ProjectSettings.asset` `allowedAutorotateToPortrait: 0`). Portrait layout is for captures, the scaler, and a future unlock, not a live rotate.

---

## 0. One-line truth

**Bag's job is: what am I wearing, what else do I have, is this one better, and where is the rest of it.** The Armory Rail hid the destinations in the scarce axis, hid overflow behind a scrollbar that does not exist, landed on Weapons instead of Gear, mixed shields into the sword list, and left the body/store lies about the shield as if they were a tidy-up.

---

## 1. Three defects. Name them. Do not collapse them.

| Id | Class | What the owner felt | What the code is doing | This WO |
|---|---|---|---|---|
| **D-LAYOUT** | IA / presentation | Bag is unclean, confusing; no tell that more exists below | Left rail of 7 x 112 px entries cannot fit, so the RAIL itself scrolls with no scrollbar. Item grid is a 6-col `ScrollRect` with no scrollbar. Landing tab is Weapons, not Gear. | **Implement here.** |
| **D-SEAT** | Gear visual / attach | Bag says a shield is worn. Town, battle, and cave show the sword and not the shield. | Loadout `EquippedOffHand` is the bag's Off Hand row. The mesh is `EquipmentController.EquipOffHand` -> `AttachOffHandProp`. Those are different systems. A worn word in the bag is not a rendered prop. | **Sibling. Instrument, prove, fix the attach. Do not "fix" it by hiding the Off Hand slot.** |
| **D-SHELF** | Store catalog placement | Shields are purchasable as offhand weapons, but they sit in neither Weapons nor Armor in the store, and there is no default she can see on the body. | Forge `vendors.json` is `weapon` only, `perLevelCap: 2`. `VendorStockResolver.EmitCapped` ranks weapons by `damageMult`, so shields (defense, often 0 dmg) lose the two slots. The in-code comment at `:606-610` already names this. Armorer is `armor` only. There is no Off Hand shelf. | **Sibling. Add an Off Hand bucket to the Forge, not a sort tweak.** |

A layout pass that "makes the Off Hand row prettier" while the mesh is still missing is a failed ticket. A store chip named Shield that still loses `perLevelCap` is a failed ticket. Ship all three, or ship D-LAYOUT with D-SEAT and D-SHELF still RED and say so in the RESULT.

---

## 2. What is wrong — from code, not vibes

### 2.1 Landscape (Seeker 2670x1200) — PRIMARY

The kit modal canvas is `1080x1920` match `0.5`. On 2670x1200 that resolves to **~2148 x 965 reference px** (scale ~1.243). The framed panel is ~907 ref px tall. Height is the scarce axis. Width is plentiful. The live Bag spends the scarce axis on navigation.

**1. The left rail is why she cannot tell there is more.**

`InventoryUIBuilder.cs:214-228` records this in the file, not as a theory:

- WO-1133 D3 authored 7 rail entries at 374 x 132 **device** px.
- At the live scaler, 132 device px is ~106 ref px — **under** `MinTouchPx = 112`.
- The code therefore authors entries **at** the floor (112 ref px) and lets the column scroll.
- Arithmetic: `7 * 112 + 6 * 8 + 2 * 3 = 838` ref px of rail content. The rail viewport is a fraction of a ~907 px panel after header + caption. Roughly **four to five of seven entries are on screen**. Skills and Map sit below the fold. The comment says so: *"roughly five of the seven are visible at once"*.

**There is no scrollbar.** `BuildRail` (`InventoryUIBuilder.cs:240-260`) builds a `ScrollRect` + `RectMask2D` and never assigns `verticalScrollbar`. `BuildItemGrid` (`InventoryGrid.cs:181-189`) does the same for the item stage. The kit already has `ElarionUiKit.MakeScrollZone` with a gilt auto-hiding thumb. The Bag hand-rolls a mute scroller beside it.

Auto-hide would not have saved this. An AutoHide thumb is invisible until the player already knows to drag. That is the defect.

**2. Bag does not open on Gear.**

`HeroInventoryController.cs:84`: `_railIndex = RailWeapons`. Tap Bag -> Weapons grid. The worn set (the answer to "what am I wearing") is rail entry 0, off to the left, and is **not** selected. WO-1133's purpose sentence required that answer on open. The landing tab walks it back.

**3. Gear is five text plates, not a gear view.**

`InventoryGrid.BuildGearSection` (`:83-107`) stacks Main Hand / Off Hand / Armor / Amulet / Ring as label+name plates. No icon. No tap-to-change (the plates are not buttons). No 3D (WO-1133 D1 blocked `HeroPreviewViewer` after F8 seq 3585; that block still stands). The promoted "gear view" is a list of words. Useful, and also the most boring surface in the modal.

**4. The item grid clips row two with no tell.**

Stage band is `BodyY0=0.300` .. `BodyY1=0.875` (`InventoryUIBuilder.cs:77`). On ~907 ref px that is ~522 px of stage. Cells are derived 6-wide at `MinTouchPx` floor (`InventoryGrid.cs:42-44, 202-229`). One row of 112+ cells plus padding fills the stage. A seventh owned weapon is a **clipped second row** behind a mask, with no thumb, no fade, no "N more" word.

Early game (two items) looks like two large tiles in a six-column hole. Late bag looks like a full row plus a secret.

**5. Shields live in the Weapons list and lie about WORN.**

`InventoryVM.BuildWeapons` (`:497-525`) iterates `OwnedWeapons()` including `category: "shield"`. The equipped flag compares **only** `EquippedWeapon` (`:499-504`), never `EquippedOffHand`. A worn heater therefore:

- shows on the Gear Off Hand row (that path reads `_loadout.EquippedOffHand`, `InventoryGrid.cs:89`);
- does **not** get the `WORN` badge in the Weapons grid;
- on Equip from the Weapons tab, `InventoryVM.Equip` (`:405-406`) calls `EquipWeaponById`, which **does** route shields to off-hand (`GearLoadout.cs:1413-1417`). The verb works; the badge does not.

That is the "bag says a shield is worn" half of D-SEAT, and it is a bag bug, not a mesh bug.

**6. Skills is a trap door. Trinkets is a hollow room. Map is a seventh entry that forces the fold.**

- Skills: `SelectRail` (`HeroInventoryController.cs:387`) calls `OpenSkillTree()` and **leaves the bag**. A bag tab that closes the bag is not a bag tab.
- Trinkets: `InventoryVM.BuildOutfits` (`:559-561`) is empty on purpose. A zero-count destination with an authored empty line is honest; giving it equal rank with Weapons in a 7-entry overflowing rail is not.
- Map: `FeatureFlags.MapTab` is OFF. Canon keeps it visible and dormant so the flag is never a surprise. It must not consume a tab that pushes real destinations under a fold.

**7. The right pane still cannot answer "is this better?"**

WO-1133 D8 left the delta column absent on purpose (`InventorySidebar.cs` header). The pane draws a Worn column that says there is nothing to compare. That is still true. This redesign keeps the honest blank until the model exposes worn stats; it does **not** fake a +3.

**8. Close + purse eat the bottom third.**

`BodyY0 = 0.300` exists because the shared kit Close sits in the bottom-centre band and the purse sits above it (`InventoryUIBuilder.cs:70-83`). That constraint is real. The redesign respects the Close (kit-wide invariant, owner F8 x3: every Close is the same pixel size). It does not invent a second Close, and it does not paint content under it.

### 2.2 Portrait (1080x1920 scaler native / 1080x2340 capture)

Runtime autorotate is **off**. Portrait still has to be designed because (a) the canvas **is** 1080x1920, (b) `InventoryArmoryRailRegression` already measures 1920x1080, (c) sibling panels are captured at 1080x2340, (d) a 14% rail of 112 px entries on a 1080-wide frame is ~130 px of label — WO-1133 Case 3 already treats rail labels as a fit hazard.

On portrait the live rail would show **even fewer** entries (the scarce axis is now width **and** the Close still owns the bottom). The 6-col grid would drop columns to keep `MinTouchPx` (`InventoryGrid.cs:216-226`) and overflow faster. A top tab row of 6 labels at 112 px **fits** 1080 (6 x 160-ish). That is why the recommendation below is also the portrait fix, not a landscape-only restyle.

**Do not stretch the landscape rail to portrait.** That is the WO-1192 failure mode.

### 2.3 What WO-1133 got right (keep)

- Empty sections name **what fills them**, never a 5x5 of decorative holes.
- `WORN` is a **word**, not a green tint (owner is red/green colourblind).
- Rarity is a letter **plus** `rarity_n` border weight.
- No empty 3D box. `HeroPreviewViewer` stays behind the evidence gate (WO-1059 / F8 seq 3585). Do not re-add the VIEW GEAR ribbon or the navy rectangle.
- Frame medallion already holds the 2D hero portrait. That is the hero on this screen.
- Pane is always present. Do not go back to "Tap an item to inspect it."
- Shared Obsidian Close. Do not move it.

---

## 3. Recommended IA — TOP TABS. Not a category carousel. Not the left rail.

### 3.1 The recommendation (one)

**Category navigation = tabs across the top, kit `ElarionUiKit.BuildTabRow`.**
**Item browsing inside a tab = a peek-strip of large cards (a carousel of ITEMS, never of categories), with a permanent overflow tell.**

Owner offered two shapes. This is the synthesis: she gets tabs for *where am I*, and a carousel for *what is in here*. She does not get a carousel of destinations.

### 3.2 Why not a category carousel (gear -> weapons -> armor -> trinkets)

A carousel of categories **repeats the reported defect on the other axis**. The complaint is "I cannot tell there is more." A destination that exists only after a swipe is a destination that does not exist. Counts ("Weapons 4", "Off Hand 1") cannot be compared if only one category is on screen. Overshoot between Gear and Weapons is a real thumb error on 2670-wide glass. Carousels are for **content of one kind**. They are the wrong control for **navigation between kinds**.

### 3.3 Why not the left rail (what is live)

Height is the scarce axis on the Seeker's 1200 px. Seven 112 px buttons belong in the plentiful axis (width), not the scarce one. The live code already admits the rail cannot close its own arithmetic and "solved" it by scrolling the navigation. Navigation that must be discovered by scrolling is the worst navigation this screen can have. WO-1133 chose the rail to fill landscape dead-black. It filled the band and created a new fold. That experiment is over.

### 3.4 Why top tabs

- Width is plentiful (2670). Six tabs at >= 112 px sit in one row and are **all visible**. The "more categories exist" problem dies because they are on screen before she taps.
- The kit already has `BuildTab` / `BuildTabRow`: plate + underline selection, luminance contrast (gold plate -> dark ink, dark plate -> gilt text). Selected state sits **under** the word. It cannot eat the label (the 08-21 Weapons chevron defect).
- Each tab carries its **count as a word** (`Weapons 4`), never a colour pip.
- Landing tab can be Gear without hiding Weapons.
- Portrait: same six tabs, same order, same words. Portrait may wrap to two rows of three if a single row fails the label-fit oracle; wrapping is allowed, hiding is not.

### 3.5 Tab membership (six, in this order)

| # | Tab | Lands on | Count |
|---|---|---|---|
| 1 | **Gear** | Worn set. **This is the landing tab.** | none (it is a summary, not a pile) |
| 2 | **Weapons** | Main-hand only (`!IsOffHandItem`) | owned main-hands |
| 3 | **Off Hand** | Shields / off-hand weapons (`IsOffHandItem`) | owned off-hands |
| 4 | **Armor** | `ArmorDef` body plate | owned armor |
| 5 | **Trinkets** | Rings + amulets (owned). Empty is still the honest early-game case. | owned accessories |
| 6 | **Potions** | Consumables + materials (existing catch-all) | owned counts |

**Skills is not a bag tab.** The talent tree is its own panel. A bag control that closes the bag is a trap. If a wayfinding chip is wanted, it is a header text button `Talents` that routes through the existing `OpenSkillTree` and is labelled as leaving.

**Map is not a seventh tab.** Canon still requires the dormant Map to be visible so `FeatureFlags.MapTab` is never a surprise. Seat it as a **header chip** `Map` + the word `soon` while the flag is off (same words as `invRailMap` / `invRailMapSoon`). When the flag flips, the chip becomes live and routes. It never consumes a tab slot that would overflow the row.

**Do not add a seventh face to the action bar.** Bag stays one face. This is the screen that face opens.

---

## 4. Scroll affordance — "more below" without a tutorial

The tell must work in greyscale, in words, and at rest (before any drag). Colour-only fade is illegal. AutoHide-only is illegal.

When content overflows the stage, **all four** of these are on:

1. **Peek.** The last fully visible card is never flush with the clip edge. At least **40% of the next card** (or the next row, if stacked) is visible inside the mask. A clipped thing is the oldest "there is more" in games. We already have the items; we are currently masking them flush.
2. **Word.** A single ASCII line from canon, not a tooltip: `3 more` / `3 more below` / `3 more to the side`, with the number from the VM. The word sits in a reserved 36 ref-px band **inside** the stage, never over a tap target, never over Close. If overflow is zero, the band is empty (not a lying "0 more").
3. **Permanent gilt scrollbar.** Use `ElarionUiKit.MakeScrollZone` (do not hand-roll). Override visibility to **Permanent** while `content.size > viewport.size`. AutoHide is the current invisible state and is forbidden on this screen. Thumb position is a shape. Track is a darker well. Meaning is not hue.
4. **Edge fade as reinforcement only.** A 24 px luminance veil on the overflow edge (darker, not a colour). It never appears without (1)+(2)+(3).

**Landscape item tabs (Weapons / Off Hand / Armor / Trinkets / Potions):** one **horizontal** peek-strip of large cards. The plentiful axis carries overflow. Peek is to the **right**. The word is `N more`. Vertical fold of items is a last resort after the strip would go under `MinTouchPx`.

**Portrait item tabs:** vertical list, peek of the next **row**, word `N more below`, permanent scrollbar on the right gutter.

**Gear tab:** five worn slots must all be visible without scrolling on both aspects. If they cannot, the design has failed — shrink chrome, not the slots. Slots are 112 px on the short side.

**Tab row:** must not scroll. If six labels will not fit at FontFloor, shorten the **words in canon-strings** (the rail-label lesson from WO-1133 Case 3), or wrap portrait to 2x3. Do not make the tabs themselves a scroller. That is the rail again.

Empty sections still do not scroll. They show the authored "what fills it" sentence and sit still.

---

## 5. How the four piles map, and where a SHIELD lives

### 5.1 Design ruling (recommendation, ready to implement)

**A shield is an OFF-HAND WEAPON. It is not armor.**

| Layer | Ruling | Why |
|---|---|---|
| Catalog | Keep `weapons.json`, `category: "shield"`, `WeaponDef.IsOffHandItem`. Do not move rows into `armor.json`. | `EquipOffHandById`, the WO-1214 2H-vs-offhand gate, defense stat, and the mesh attach are all weapon-shaped. Relocating the rows is a schema change this ticket does not earn. |
| Bag | **Own tab: Off Hand.** Not mixed into Weapons. Not mixed into Armor. | Owner looked in both and found them in neither. A third tab with the word `Off Hand` (already `invSlotOffHand`) is the IA fix. Weapons tab is main-hand only. |
| Store | **Own category chip: Off Hand**, sibling of Weapons / Armor, on the Forge. | Forge `categories: ["weapon"]` + `perLevelCap: 2` + rank-by-`damageMult` **drops shields on purpose** (`VendorStockResolver.cs:606-610`). Armorer does not sell them. The V1 excuse in that comment ("every class is seeded its starter off-hand") is exactly what the owner cannot see on the body (D-SEAT) and cannot repurchase (D-SHELF). |
| Body | Off-hand mesh on `LeftHand` via `EquipmentController.EquipOffHand`. Knight starter is `knight_shield_starter` (`StarterLoadout`). | Already authored. D-SEAT is "the seed/loadout is not becoming pixels." |
| Player words | `Off Hand` for the slot and the tab. Item names stay (`Squire's Heater`). Never `Offhand` as one word. Never a shield emoji (tofu). | ASCII. Existing keys already use `Off Hand`. |

**Do not put shields in Armor.** Armor is `ArmorDef` (body plate, weight class, `EquipArmorById`). A heater is held, not worn as a chest. Mixing would make Equip from the Armor tab call the wrong seam.

**Do not leave shields in Weapons.** That is the live state, and it is why a player hunting a shield in "Weapons" sees swords, and a player hunting a shield in "Armor" sees plate.

### 5.2 Default assignment the owner can see

Knight starter is already `Squire's Blade` + `Squire's Heater` (`GearLoadout.cs:125-130`). Ranger/Mage starter off-hand is null (WO-1240 armor-only kits). The bag Gear tab on a fresh Knight **must** show:

```
MAIN HAND     Squire's Blade
OFF HAND      Squire's Heater
ARMOR         <authored starter armor name, or empty>
AMULET        empty
RING          empty
```

If Off Hand reads `empty` on a fresh Knight, that is a **grant/seed** bug (body seed skipped, or WO-1214 refused). Log it. Do not paper over it by writing "Squire's Heater" into the view.

If Off Hand reads `Squire's Heater` and the world body has no shield mesh, that is **D-SEAT**. The bag is telling the truth. The attach is lying. Keep the words.

### 5.3 D-SEAT — separate defect, required instrumentation before any attach edit

**No seating math change until a captured `[Flow:Equip]` line names the dead step** (CLAUDE.md section 12). Static candidates (do not pick one):

- `PackageBakedGear` skip (`EquipmentController.cs:2048-2054`) — Paladin bake suppresses the prop. Live KnightV3 path is `usePackage:false` (`HeroBodySwapper.cs:727-775`), so this should NOT fire on Grom. Prove it with the line.
- Addressable `gear/weapon/ShieldWithItemLogic` 404 / failed handle (`BeginAddressableOffHand`) falling through a fallback that then also fails.
- Attach succeeds, then a later scene-load re-seat no-ops (`idempotent skip`, WO-994) or `OnDisable` destroys the prop and LateAttachRetry never restores off-hand.
- Prop attached at scale 0 / wrong layer / parented to a hidden sheathe socket (`AttachOffHandProp MEASURED` already prints parent + scale).
- Preview/bag honesty vs world: bag reads loadout; world reads the prop. They already disagree.

**Required proving line (add if missing, then run, then fix THAT):**

```
[Flow:Equip] seat-proof class='knight' offId='knight_shield_starter' loadoutOff='<id|null>'
             prop='<name|null>' parent='<bone|null>' baked=<0|1> addr=<ok|fail|n/a>
             renderers=<n> bounds=<v> scene='<name>'
             CAUSE=<loadout-null | baked-skip | addr-fail | attach-fail | attached-invisible | ok>
```

Town, a battle scene, and a cave/dungeon must each emit this. D-SEAT is not closed until `CAUSE=ok` in all three with a visible mesh, matching the bag Off Hand word.

A headless oracle that only checks `EquippedOffHand != null` will green-pass the reported bug. Pin the **prop + renderer**, not the loadout.

### 5.4 D-SHELF — separate defect, the fix the resolver already named

`VendorStockResolver.cs:606-610`: *"if the owner wants a shield always purchasable, the fix is a per-slot bucket key here, not a sort tweak."*

Do that:

- Cap **main-hand** and **off-hand** independently under `perLevelCap` (two swords does not evict the heater; two heaters does not evict the sword).
- Forge keeps `categories: ["weapon"]` (shields **are** weapons). Do not add a fake `armor` row for heaters.
- PartyShop category bar becomes `All / Weapons / Off Hand / Armor` (Armor still hidden on a weapons-only vendor via `CategorySelectorVisible`). Off Hand is not a type-chip buried under Weapons (`PartyShopType.Shield` already exists and is the wrong altitude — a chip the player has to know to press is how the owner missed them).
- Default Forge view for a Knight still leads with main-hands, with Off Hand one tap away **and visible as a word**.
- `onlyEquippable: true` still applies. A Mage must not be sold a Knight heater that WO-1214 will refuse. `job:"any"` shields remain gated by the armed-hero invariant (WO-1214). Do not loosen that gate to fill the shelf.

---

## 6. Mock-level layout

Authoritative numbers. CLI does not invent spacing. Fractions are of the **framed panel content** from `ElarionUiKit.BuildObsidianPanel` (same parent the live Bag uses). Device px are at Seeker 2670x1200 (panel ~2460 x 1128 device, ~2148 x 965 canvas ref -> panel ~1976 x 907 ref). Touch floor is **112 ref px**.

Keep the existing Close. Do not paint stage/pane under `y = 0.300` of the panel. Purse stays above Close.

### 6.1 Landscape 2670 x 1200 (primary)

```
Device px inside the framed panel (~2460 x 1128)

0                                                                2460
+------------------------------------------------------------------+
|  [oval portrait]     INVENTORY                    Map  soon      |  header chrome (kit)
+------------------------------------------------------------------+  y 0.885-0.985  (~90-100 ref px)
|  Grom Ironhand   KNIGHT  LV 4    HP 123/175  MP 12/12  XP 40%    |
+------------------------------------------------------------------+
|                                                                  |  TAB ROW  y 0.760-0.875
|  [ GEAR ] [WEAPONS 2] [OFF HAND 1] [ARMOR 1] [TRINKETS] [POTIONS] |  height >= 112 ref px
|     ^^^ selected = underline + plate, never a hue-only fill      |
+-------------------------------------+----------------------------+
| STAGE                               | PANE                       |  y 0.300-0.750
|                                     |                            |
|  (see tab contents below)           |  name / WORN or empty      |
|                                     |  stats as words            |
|                                     |  Equip / WORN status       |
|                                     |                            |
+-------------------------------------+----------------------------+
|  Off Hand  empty. The Forge sells heaters.     1230 G   291 C    |  purse  y 0.222-0.288
+------------------------------------------------------------------+
|                         [ Close ]                                |  kit CTA, do not move
+------------------------------------------------------------------+
```

**Tab row geometry (landscape):**

- Band: `x 0.035-0.965`, `y 0.760-0.875` (content fractions).
- 6 tabs via `ElarionUiKit.BuildTabRow`, `gapFrac = 0.012`.
- Each tab >= 112 ref px tall. Width ~ (0.930 - 5*0.012)/6 = **0.145 of panel** ~ 286 device px / ~198 ref px — enough for `OFF HAND 1` at FontFloor.
- Selected = kit plate + underline. Unselected = dark plate + parchment label. Count is a second line or a suffix ` 1`, never a coloured pip.
- Gear has no count.

**Gear tab stage (landing):**

```
+---------------------------+------------------------------+
| MAIN HAND                 |  (pane, always on)           |
| Squire's Blade            |                              |
+---------------------------+  Squire's Blade              |
| OFF HAND                  |  WORN                        |
| Squire's Heater           |  +3% def                     |
+---------------------------+                              |
| ARMOR                     |  This is what you are        |
| empty                     |  holding.                    |
+---------------------------+                              |
| AMULET     empty          |  [ WORN ]   (status, not a   |
| RING       empty          |    tappable no-op button)    |
+---------------------------+------------------------------+
```

- Five slots, stacked, **full stage height**, each short side >= 112 ref px. `gap 8` ref px.
- Stage x `0.035-0.62`, pane x `0.63-0.965` (pane ~30%, same job as WO-1133, without the left rail).
- Each slot is a kit `Slot` (`slot_armor`) and **is a button**. Tap selects that slot, fills the pane, and does **not** leave Gear. A second control on the pane, `Show items`, switches to the matching pile tab (Weapons / Off Hand / Armor / Trinkets) with that slot pre-selected. One tap to inspect, one tap to browse replacements. Never a nested EquipDrawer modal on top of the bag (`EquipDrawer` stays off this screen; it remains the EquipmentPanel path).
- Vacant = the word `empty` in italic dim, never a blank plate.
- Icon 72 ref px square on the left of the slot when an item is worn; glyph fallback if art is null.
- No 3D niche. The frame medallion is the hero. Re-adding a RawImage box is a regression of WO-1133 defect 3.

**Item tab stage (Weapons / Off Hand / Armor / Trinkets / Potions):**

```
+--------------------------------------------------+-----------+
|  [CARD] [CARD] [CARD] [peek 40%]                 | pane      |
|   WORN    U      C                               |           |
|  Blade   Iron   Oak                              |           |
|                                                  |           |
|  2 more                                          |           |
|  [gilt thumb on a dark track, always if overflow]|           |
+--------------------------------------------------+-----------+
```

- Card size: **240 x 240 ref px** (derived: stage width minus pane minus peek minus gaps, 3 full cards + 0.40 peek). Short side stays >= 112. If the stage cannot fit 3+peek at 240, drop to 2+peek, never below 112.
- Card = kit `BuildRaritySlot`. Name under the icon, one line, fit-or-ellipsize at FontFloor. Rarity letter + rim. `WORN` word-badge when `ItemVM.Equipped` (now correctly set for off-hand).
- Horizontal `ScrollRect` through `MakeScrollZone` (kit). `horizontal = true`, `vertical = false` on this aspect. Permanent scrollbar along the **bottom** of the stage when overflow.
- Peek: content padding-right = `0.40 * cardWidth` while `count > visible`, else 0.
- Word band: 36 ref px under the cards, left-aligned: `invMoreCount` formatted as `{0} more`. Hidden when overflow is 0.

### 6.2 Portrait (1080 x 1920 ref / 1080 x 2340 capture)

```
+--------------------------------------+
| INVENTORY                 Map  soon  |
+--------------------------------------+
| Grom Ironhand  KNIGHT LV 4           |
| HP 123/175   MP 12/12                |
+--------------------------------------+
| [GEAR] [WEAPONS 2] [OFF HAND 1]      |  row A  >= 112
| [ARMOR 1] [TRINKETS] [POTIONS]       |  row B  >= 112  (ONLY if row A fails label-fit)
+--------------------------------------+
| MAIN HAND      Squire's Blade        |
| OFF HAND       Squire's Heater       |
| ARMOR          empty                 |
| AMULET         empty                 |
| RING           empty                 |
+--------------------------------------+
| Squire's Heater                      |
| WORN                                 |
| +3% def                              |
| This is what you are holding.        |
+--------------------------------------+
| 1230 G    291 C                      |
| Off Hand empty. The Forge sells      |
| heaters.                             |
+--------------------------------------+
|              [ Close ]               |
+--------------------------------------+
```

- Prefer **one** tab row of six if label-fit passes at FontFloor. If it fails, wrap to 2x3. Never scroll the tabs.
- Gear: slots stacked full-width, pane **below** the slots (portrait stacks; landscape splits). All five slots still visible without scroll.
- Item tabs: **vertical** peek-list, one card per row, 112+ tall, 40% of the next card visible, word `N more below`, permanent scrollbar on the right 10 px gutter.
- Same Close, same purse, same ASCII.

### 6.3 Band budget (print once per build)

A `FlowTrace.Step("Inventory", ...)` line on every `BuildRoot` must print the resolved ref-px of: header, tabRow, stage, pane, purse, close, tabCount, tabWrap (0|1), overflowWord, scrollbarMode. The next "it still clips" capture names the band from data.

---

## 7. Model / VM changes this design needs (in-scope because the view cannot lie without them)

These are not a new equip system. They are the projection the new tabs require.

1. `InventoryTabKind` gains `OffHand`. `BuildWeapons` skips `IsOffHandItem`. New `BuildOffHands` uses `EquippedOffHand` for the `Equipped` flag.
2. `BuildWeapons` equipped flag stays `EquippedWeapon` only (now correct, because shields left the list).
3. `InventoryVM.Equip` for an off-hand id calls `EquipOffHandById` (today it reaches that via `EquipWeaponById` routing; call the off-hand seam directly so a future routing change cannot silently put a heater in the main hand).
4. `ReplacedBySelection` (`InventorySidebar.cs:270-278`) for an off-hand returns `EquippedOffHand`, not `EquippedWeapon`. Today a heater would claim it replaces the sword.
5. Overflow count is a VM field: `max(0, Slots.Count - visibleCount)`. Visible count is measured by the view after layout and written back, or computed from authored card size. Do not hardcode "if count > 6".
6. Worn-stat compare stays **absent** until a follow-up exposes it on `InventoryDetail`. Pane keeps `invPaneNothingToCompare`. Do not fake deltas.

Trinkets: if owned rings/amulets already exist on `IInventoryStore`, list them. If not, the tab stays the authored empty line. Do not invent a cosmetics catalog.

---

## 8. Player-facing strings (ASCII, both canon copies, byte-identical)

Keys follow the live flat camelCase. Additions go in `InventoryStrings.AllKeys` so the parity suite catches a missing copy.

`Assets/Resources/Data/Canonical/canon-strings.json` **and** `Assets/StreamingAssets/Data/Canonical/canon-strings.json`.

Reuse existing keys where the word is already right: `invSlotOffHand` = `Off Hand`, `invSlotEmpty` = `empty`, `invPaneWornBadge` = `WORN`, `invActionEquip` = `Equip`, `invRailMap` / `invRailMapSoon`.

| Key | Text |
|---|---|
| `invTabGear` | `Gear` |
| `invTabWeapons` | `Weapons` |
| `invTabOffHand` | `Off Hand` |
| `invTabArmor` | `Armor` |
| `invTabTrinkets` | `Trinkets` |
| `invTabPotions` | `Potions` |
| `invMoreCount` | `{0} more` |
| `invMoreBelow` | `{0} more below` |
| `invEmptyOffHand` | `Nothing here yet. The Forge sells heaters and bucklers.` |
| `invGoToItems` | `Show items` |
| `invHeaderTalents` | `Talents` |
| `invNextTabsHint` | `Every pile is a tab on this row.` |

Retire from the painted surface (keep the keys so old saves/docs do not 404, or delete from `AllKeys` if unused): `invNextRailHint` (`the rail keeps every section one tap away`) — that sentence becomes a lie the moment the rail dies. Do not paint it.

Skills empty-line may remain in the file; it is no longer shown in the bag.

No emoji. No en-dash. No curly quotes. No colour names in the copy (`green`/`red`).

---

## 9. Acceptance

ASCII. No meaning by hue. Open the PNGs. Greyscale pass included.

### D-LAYOUT

- [ ] Opening Bag on a Knight lands on **Gear**, with all five worn slots readable without scrolling, on 2670x1200 and on 1080x1920.
- [ ] Six tabs are visible at once on 2670x1200: Gear, Weapons, Off Hand, Armor, Trinkets, Potions. None are below a fold. Tabs themselves do not scroll.
- [ ] Selected tab is a plate + underline + the stage changing. A greyscale screenshot still identifies the selected tab.
- [ ] Weapons tab lists **no** `IsOffHandItem` rows. Off Hand tab lists **only** those rows.
- [ ] A worn heater shows `WORN` on the Off Hand tab **and** as the Gear Off Hand value. The two cannot disagree (same `ItemVM.Equipped` / loadout field).
- [ ] When an item tab has more cards than fit: (a) >= 40% of the next card is visible, (b) the word `{n} more` (landscape) or `{n} more below` (portrait) is visible at rest, (c) a gilt scrollbar thumb is visible at rest. All three. A tab with no overflow shows none of them.
- [ ] Empty Off Hand shows `Nothing here yet. The Forge sells heaters and bucklers.`
- [ ] Every tap target >= `ElarionUiKit.MinTouchPx` (112) on its short side. `ClampMinTouch` is a no-op. Do not add this panel to `TouchBaseline`.
- [ ] Skills is not a bag tab. Map is a header chip with the word `soon` while `FeatureFlags.MapTab` is off; the flag stays off.
- [ ] No empty 3D box. No VIEW GEAR ribbon. No 78x72 cells. No top-tab chevron eating its label.
- [ ] All new strings ASCII, both canon copies byte-identical, present in `InventoryStrings.AllKeys`.
- [ ] `COMPILE_GATE_OK`. `InventoryArmoryRailRegression` is rewritten against **this** design's independent numbers (tab row, six tabs, no rail, peek, overflow word). Do not keep asserting 374/1496/800.
- [ ] Captures opened: landing Gear (Knight with sword+heater), Weapons, Off Hand, Armor, empty Trinkets, Potions; overflow case (>= 7 weapons); greyscale of the overflow case; 2670x1200 and 1080x1920.

### D-SEAT (sibling; may ship in the same commit if proven, else stays RED in the RESULT)

- [ ] The proving `[Flow:Equip] seat-proof` line is in the ticket / RESULT **before** any seating math edit.
- [ ] Knight with `EquippedOffHand = knight_shield_starter`: shield mesh is visible in **town**, **battle**, and **cave**. Sword remains visible. If a scene fails, that scene's `CAUSE=` is cited; no inference-fix.
- [ ] Oracle fails when loadout has an off-hand and the live prop has zero visible renderers.

### D-SHELF (sibling)

- [ ] Forge shows an **Off Hand** category as a word, sibling of Weapons.
- [ ] A level-1 Knight can **see and buy** at least one heater/buckler at the Forge without pressing a hidden type chip. `perLevelCap` on main-hands does not delete that row.
- [ ] Mage is not sold a Knight-only heater that `CanEquipWeaponNow` will refuse.
- [ ] Armorer still does not sell shields.

Owner felt-test (not CLI-closeable): she opens Bag and can answer, without being told, **what she is wearing, that Off Hand is a place, that more items exist when they do, and that a heater is a thing she can buy and see on Grom.**

---

## 10. What NOT to touch

- **`LayoutOracle.cs`** and **`UICaptureLaunch.TouchBaseline`**. The allow-list stays `ArmyMuster` + `EquipDrawer`. Adding Inventory/Bag/Armory to it is not a fix (owner 2026-08-24: no waivers).
- **`UICaptureLaunch.cs` layout.** The harness photographs. It does not re-author.
- **HUD assemblies referencing Village types.** Bag stays in `DeNelle.Village`. HUD opens it through `PanelRouter.Open(PanelId.Inventory)` only.
- **UXML / UI Toolkit.** Code-built uGUI via `ElarionUiKit` only.
- **Save schema.** No bump. Off-hand persistence keys already exist.
- **`EquipOffHandById` class/level/armed-hero gates (WO-1214).** Do not loosen them to fill a shelf or a tab.
- **`HeroPreviewViewer` / WO-1059.** Do not re-mount a blank RT. The medallion portrait is the hero.
- **EquipmentPanel / EquipDrawer** as a stacked modal from this screen. Do not bring the drawer back on top of the bag.
- **Night Market / `PackStore` / Realm Store.** Wrong store. D-SHELF is PartyShop + VendorStockResolver + `vendors.json`.
- **Action bar face count / ordinals.** Map stays dormant at ordinal 4. Bag stays one face.
- **`FeatureFlags.MapTab`.** Stays off.
- **Scene files.** No hand-edit of `.unity`.
- **Polyperfect / Addressable packing / R2 push**, unless D-SEAT's proving line names a missing `ShieldWithItemLogic` bundle. If it does, the fix is the ship chain (`tools/r2-ship.ps1`), not a bag layout change.
- **A new `ObsidianUiHelper`.** Kit ids only.
- **Raw hex in the view.** `UiStyle` / `ElarionUi` tokens.

---

## 11. Files (likely)

**D-LAYOUT (presentation)**

- `Assets/_Modules/Village/Hero/HeroInventoryController.cs` — landing tab = Gear; kill rail ordinals as navigation.
- `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs` — tab row, zone constants, overflow tell, delete `BuildRail`.
- `Assets/_Modules/Village/Hero/InventoryGrid.cs` — Gear slots tappable; item peek-strip; no mute `ScrollRect`.
- `Assets/_Modules/Village/Hero/InventorySidebar.cs` — Off Hand replace-line; `Show items` CTA.
- `Assets/_Modules/Village/Hero/InventoryPaperDoll.cs` — header chip for Map / Talents. Still no preview box.
- `Assets/_Modules/Village/Hero/InventoryVM.cs` — `OffHand` tab, equipped flags, overflow count.
- `Assets/_Modules/Village/Hero/InventoryStrings.cs` + **both** `canon-strings.json` copies.
- `Assets/Editor/Regression/InventoryArmoryRailRegression.cs` — rewrite authority to this spec.
- Tests: `Assets/Tests/EditMode/InventoryVMTests.cs` (off-hand not in Weapons; WORN uses `EquippedOffHand`).

**D-SEAT (attach)**

- `Assets/_Modules/Village/Hero/EquipmentController.cs` — proving line first; fix only the named `CAUSE`.
- Existing `KnightGearProofCapture` / attach regressions: assert prop + renderer, not loadout alone.

**D-SHELF (store)**

- `Assets/_Modules/Village/Hero/VendorStockResolver.cs` — per-slot bucket (main vs off-hand) under `perLevelCap`.
- `Assets/_Modules/Village/Hero/PartyShopVM.cs` / `PartyShopPanelMvvm.cs` — Off Hand category, not a buried type chip.
- `Assets/Resources/Data/Canonical/vendors.json` **and** the StreamingAssets copy — notes only if a new category token is required; prefer keeping `weapon` and bucketing in the resolver so data stays honest.
- `ForgeShelfClassKindRegression` (or sibling): a level-1 Knight shelf contains >= 1 `IsOffHandItem` **and** >= 1 main-hand.

**Do not edit** `Assets/_Modules/HUD/**` except if a string-only HUD chrome currently says "rail". It should not.

---

## 12. Build order

1. **Instrument D-SEAT** (`seat-proof` line). Run town / battle / cave. Paste the three lines into the RESULT **before** any attach edit.
2. **VM split** (Off Hand tab, equipped flags, replace-line). EditMode tests red then green.
3. **Kill the rail. Ship the tab row + Gear landing + peek-strip + overflow word + permanent scrollbar.** This is D-LAYOUT. Captures.
4. **D-SHELF bucket** in the resolver + Off Hand store category. EditMode on the Forge shelf.
5. **D-SEAT fix** only if step 1 named a cause. Never a speculative offset dial.
6. Rewrite `InventoryArmoryRailRegression`. Brace-check every `.cs`. `COMPILE_GATE_OK`. Open the PNGs, including greyscale overflow.

Half of this ticket is still removal: the rail, the mute ScrollRects, Skills-as-a-bag-tab, the Weapons-contains-shields lie, the unused 6th/7th fold.

---

## 13. Easy vs right (named, because they diverge)

- **Easy:** keep the rail, add a scrollbar, ship. **Right:** stop putting navigation in the scarce axis. The owner already told us the rail is confusing; polishing it repeats WO-1133's failure mode with a thumb.
- **Easy:** put shields in Armor because "they defend." **Right:** Off Hand is a weapon slot with a defense stat. The seams, the mesh, and the 2H gate are all weapon-shaped. Armor is a body.
- **Easy:** treat "bag says worn, body shows nothing" as a missing icon on the Gear plate. **Right:** that is D-SEAT, a different system, and hiding the slot would destroy the evidence.

---

## Related (read, do not reopen as this ticket)

WO-1133 (closed; rail shipped; preview carve-out still live) · WO-1059 (blank RT; still do not promote) · WO-1015 (equipment panel layout) · WO-1061 (drawer list; query must stay non-empty) · WO-1068 (store compare; Forge is the store, not Night Market) · WO-1214 (any-job shield disarming a Mage; gates stay) · WO-994 / PROD-005 (shield seat history) · WO-1060 (TouchBaseline stays at two entries) · WO-1192 (both orientations, oracle cannot see emptiness).
