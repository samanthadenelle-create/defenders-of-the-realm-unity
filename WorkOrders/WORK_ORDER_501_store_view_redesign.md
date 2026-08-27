<!-- status-reconcile-2026-08-22 -->
> # STALE 2026-08-22 - RE-SURVEY BEFORE PULLING THIS. EVERY `file:line` IN SECTION 1 IS INVALIDATED.
>
> **This is the GEAR shop, not the Night Market.** The screen this WO redesigns is `PartyShopPanelMvvm` /
> `PartyShopVM`, routed as **`PanelId.PartyShop = 5`** (`Assets/_Modules/Core/UI/PanelRouter.cs:54`). The
> Night Market is a **different** screen (`Assets/_Modules/Wallet/PackStore.cs`). Do not conflate them.
>
> **The header's flag statement is now FALSE.** It reads "ships behind the existing FeatureFlags.PartyShop
> (OFF)". At source today: `Assets/_Modules/Core/FeatureFlags.cs:157` -
> `public static bool PartyShop => Get("partyshop", defaultOn: true);` - **defaultOn: TRUE.** The panel is live.
>
> **All four owner points in section 0 have SHIPPED** (verified at source 2026-08-22 in
> `Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs`):
> 1. hero/type filter - the Buy list is built from `ShopCatalog.Shoppable(ctx, job, level)` in `PartyShopVM`;
> 2. slim name list - the list column was narrowed to pair with the preview pane (`:1004`);
> 3. the larger 3D render preview - `_previewRoot` well at `:1004`, `RawImage _previewImage` at `:86`,
>    live `RenderTexture _rigRt` created at `:1354`, async Addressables model load at `:1264`, price line
>    fitted at `:1096`;
> 4. the bottom action bar - "WO-501 owner point 4: Purchase/Sell toggle + Equip" at `:472-500`, with the
>    toggling label logic at `:1491-1527`.
>
> **The survey is arithmetically stale:** `PartyShopPanelMvvm.cs` is **1727 lines** today (`wc -l`,
> 2026-08-22) against the ~583-line file section 1 cites, so every `:217-319` / `:583-667` / `:520-570`
> style citation below points at unrelated code. **Body preserved for its design intent only** - treat
> section 1 as history, not as a map.

# WORK ORDER 501 - Store/Shop View Redesign (hero-filtered, slim list + 3D preview)

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
>  PRIOR: **Status:** PROPOSAL / READY TO IMPLEMENT (DESIGN pass - read-only survey done)
**Silo:** Monetization/UI (Store) - PartyShop MVVM. File-disjoint from combat/scene lanes.
**Feature flag:** ships behind the existing FeatureFlags.PartyShop (OFF). No new flag.
**Supersedes/extends:** WO-486 (store preview pane + per-item sprites). WO-486 right-side preview + narrowed grid is the FOUNDATION; this WO upgrades the preview from a 2D sprite to a 3D render and adds the hero-type filter + the toggling Purchase/Sell + Equip buttons.
**Lane note:** touches ONLY the PartyShop View + VM (+ optional gear JSON data). Does NOT touch VillageSceneBuilder, any .unity scene, combat, or GearCatalog.cs schema. Safe edit-only lane.

---

## 0. Why - the owner redesign

The store is ~70% BUILT - do NOT greenfield (CLAUDE.md S8). The PartyShop MVVM (PartyShopVM + PartyShopPanelMvvm) already has: a party selector, Buy/Sell tabs, All/Weapons/Armor category chips, per-row stat+delta line, per-row sprite resolution with a glyph fallback, and the full buy/sell/equip transaction surface. The owner wants the VIEW re-laid-out around four points:

1. FILTER by weapon/armor TYPE for the selected HERO - only show gear the current hero can use.
2. SLIM the scroll list to a narrow column of NAMES that stays within the canvas.
3. BESIDE the list, a LARGER 3D RENDER PREVIEW of the selected gear, with the stat DIFF vs the currently-equipped item below it, and a large readable PRICE below that.
4. UNDER that, TWO buttons: a single Purchase/Sell button whose label+action TOGGLES with store mode, and an Equip button.

Almost all the DATA wiring already exists in PartyShopVM. This WO is mostly a VIEW re-layout in PartyShopPanelMvvm.cs plus a small additive VM surface for the hero TYPE filter and a couple of preview/price getters. Reuse-don't-greenfield throughout.

---

## 1. Survey - what exists today (cite file:line)

### Views (code-built uGUI - no UXML; CLAUDE.md S8 "UXML in builds does NOT work - always code-built")
- Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs - the ACTIVE redesign target. A dumb skin that binds a PartyShopVM. BuildChrome() (:217-319) lays out header/wallet, party bar (:256-261), Buy/Sell tabs (:276-294), All/Weapons/Armor category bar (:287-294), and the scroll _contentRoot anchored (0.04,0.12)-(0.96,0.70) at :300 (FULL panel width - the thing to slim). CreateRow (:583-667) currently packs icon+name+statline+price+state-chip+action button ALL inside each row. BuildScrollContent (:520-570) = VerticalLayoutGroup + ContentSizeFitter + per-row LayoutElement. ResolveItemSprite(detail,item) (:671-693) = the never-blank chain: Resources.Load<Sprite>(iconPath) -> ItemIconCatalog.ForWeapon/ForArmor -> pack glyph.
- Assets/_Modules/Village/Hero/ShopPanel.cs - the LEGACY single-hero shop (BUY/EQUIP/SELL tabs, right-side 2D details pane at BuildDetailsPane :428-481, toggling Purchase/Sell button at :341-344). A working reference for a Purchase/Sell button that toggles on _vm.Mode (:343) and a details pane beside a narrowed list (_contentRoot :330-336 is (0.02,0.13)-(0.62,0.71), left ~62%). Bound to ShopVM. KEEP as the proven pattern reference; the redesign lands on PartyShop.
- Assets/_Modules/Village/Hero/PartyShopPanelMvvmBootstrap.cs - spawns the panel only when the flag is ON.

### ViewModels (pure C#, unit-testable, no UnityEngine UI types)
- Assets/_Modules/Village/Hero/PartyShopVM.cs - the complete store VM. REUSE:
  - Hero/type filter ALREADY DONE for BUY: BuildBuy() (:371-455) calls ShopCatalog.Shoppable(ctx, job, level) which folds VendorStockContract kinds + WeaponFitsClass/ArmorFitsClass + level into one list - the list is ALREADY filtered to what the selected member can equip.
  - PartyShopDetail struct (:62-87) carries Stats, Delta, Description, IconPath, IconRole, IconName per row. Selected (:256-257) = the selected row detail. DetailFor(id) (:260-261).
  - Stat/delta math: WeaponStats/ArmorStats (:658-677), DeltaVsEquippedWeapon/DeltaVsEquippedArmor (:679-697) ALREADY compute "+5% dmg vs equipped" / "= equipped" / negative. This IS the stat-diff.
  - Category chips: PartyShopCategory {All,Weapons,Armor} (:55) + SetCategory (:301-308) + CategorySelectorVisible (:214-215).
  - Tab toggle: PartyShopTab {Buy,Sell} (:46) + SetTab (:291-298).
  - Actions: Act(id) (:322-329) fires the row armed buy/equip/sell; transactions BuyWeapon/BuyArmor (:556-588, auto-equip on buy), EquipWeapon/EquipArmor (:590-608), SellGear (:610-620).
  - Selected member: SelectedMember/SelectedJob/SelectedLevel (:342-349).
- Assets/_Modules/Village/Hero/ShopVM.cs + ShopCatalog.cs - the shoppable resolver + the legacy VM.

### Equip / loadout / type constraints (which gear a hero can use)
- Assets/_Modules/Village/Hero/GearCatalog.cs:
  - WeaponFitsClass(WeaponDef, job) (:304-307) - weapon job is "any" or == class.
  - ArmorFitsClass(ArmorDef, job) (:293-299) + ClassWeight(job) (:278-288) - Ranger/Mage=light, Knight/Cleric=heavy; empty/"any" weight fits all. THIS is the armor TYPE constraint.
  - WeaponDef (:41-133): job, hand (1h/2h, IsTwoHanded :124), category (sword/axe/bow/staff/shield; IsOffHandItem shield :128), damageType (melee/ranged/magic :57), damageMult, reach, rarity, req.level, prefabPath+loadVia+iconPath (:96-105).
  - ArmorDef (:137-193): job, weight (light/heavy :148), defense, hpBonus, rarity, prefabPath.
  - BestWeapon/BestArmor/BestOneHandedWeapon (:220-272), GetBuyCost (gold = GearAppraisal value).
- Assets/_Modules/Village/Hero/GearLoadout.cs - per-wearer equip model: EquipWeaponById/EquipArmorById/EquipOffHandById/Unequip* (:386-526), main/off-hand enforcement EnforceHandSlots (:315-346), per-class persisted equip. Shields auto-route to off-hand.
- Assets/_Modules/Village/Hero/IEquipTarget.cs - the mockable equip seam over a GearLoadout (GearLoadoutEquipTarget adapter :89-165): EquippedWeapon/EquippedArmor defs, WeaponMult/ArmorDefense, TargetClass, Equip*ById/Unequip*, EquipChanged event. The VM holds members as IReadOnlyList<IEquipTarget>.
- Assets/_Modules/Village/Hero/EquipmentController.cs - body-attach layer. LoadsViaAddressable (:540-548) + BeginAddressableEquip (:555+) show the load-path branch (Addressables "gear/" vs the Resources map) reused by the preview (S4).
- Assets/_Modules/Village/Hero/VendorStockContract.cs - GearKind flags + AllowedFor(ctx) (the store-TYPE gate: armorer=Armor, forge=Weapon, etc).

### Icon / render
- Assets/Editor/Catalog/GearIconRenderer.cs - EDITOR pass that renders a real PNG thumbnail per gear entry via AssetPreview.GetAssetPreview (:291-319), resolving the prefab from the Addressables "Gear" group (Blink rows) or Resources.Load (:206-250), stamping iconPath onto weapons.json/armor.json. The per-item still image. For a LIVE rotatable 3D preview we use the runtime RenderTexture rig instead (S4) - GearIconRenderer is the editor/offline analogue, not a runtime path.
- Assets/_Modules/Village/Hero/ItemIconCatalog.cs - keyword->tier-sheet sprite fallback (ForWeapon/ForArmor :57-140). The SECOND fallback after iconPath.
- Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs - THE runtime 3D-preview reference rig (the "Offset Forge" pattern). SetupPreview3D (:191-319): a RenderTexture (:194) on a RawImage, an isolated _previewRoot far below the scene at y=-5000 (:203-204) on dedicated PREVIEW_LAYER 31 (:57,:301-304) with the camera + lights culling-masked to that layer, a URP UniversalAdditionalCameraData Base camera (:292-296), VisualFactory.Skin(prefabPath) to mount the model (:247), FrameCameraOnRig to fit the bounds (:323-344), drag-to-rotate (:346-402), and full teardown/material-free on close (:498-508). REUSE this rig wholesale for the gear preview.

### Scene-wiring / PanelSettings note (the DISABLED gate)
- CLAUDE.md S8: "Store scene-wiring: DISABLED - needs own PanelSettings before re-enabling."
- PIPELINE_STATE.md :67: "WO-22 store re-enable (own PanelSettings + code-built UI)" - parked, needs owner eye. :142 flags "UXML render risk in builds." The PartyShop builds its OWN screen-space Canvas on Open (ElarionUiKit.BuildModalCanvas, PartyShopPanelMvvm :219) so it needs NO PanelSettings - the code-built escape hatch from the disabled UXML store. See S8 below.

---

## 2. Target layout (panels / anchors / sizing) - code-built per CLAUDE.md S8

All anchors are fractions of the framed panel (ElarionUiKit.PanelFramed rect, PartyShopPanelMvvm :229). Keep the top band untouched; re-lay only the lower content band.

```
y=0.98  Gear Shop (header)                         Gold: 1234 (wallet)   0.91-0.98
0.80    [Grom][Thrain][Sylas][Elara]  (party member chips)              0.80-0.885
0.78    Grom - Knight (Lv 7)              [ BUY ] [ SELL ]  (tabs)       0.755-0.80
0.72    TYPE chips:  [All][Weapon][Armor]   [1h][2h][shield][light]     0.69-0.748  <- S3 filter
------------------------------+-------------------------------  y=0.70
 NAME LIST (slim, scroll)     |  3D RENDER PREVIEW (RawImage)
 Squire's Blade               |  +---------------------+        preview img 0.40-0.69
 Iron Longsword               |  |   [ rotatable 3D ]  |
 Oathkeeper           <sel>   |  +---------------------+
 Dawnbreaker                  |  Oathkeeper  (name, gilt)       0.34-0.40
 Aegis Edge                   |  +18% dmg  reach 3.4m  1h        0.27-0.34 stats
 (names only, X 0.04-0.40)    |  +6% dmg vs equipped (green)    0.21-0.27 diff   <- S3 diff
                              |  1,250 Gold   (LARGE)           0.13-0.20 price  <- big/readable
------------------------------+-------------------------------  y=0.12
 [Close]    [ PURCHASE / SELL ]      [ EQUIP ]      (status)    0.03-0.105  <- S5 two buttons
y=0
```

### 2a. Slim NAME list (owner point 2)
- Narrow _contentRoot (:300) from anchorMax.x 0.96 to ~0.40: cr.anchorMin = (0.04, 0.12); cr.anchorMax = (0.40, 0.70); (a ~0.26 gutter before the preview at 0.42).
- In CreateRow (:583-667): STRIP everything except the NAME out of the scroll row. Delete the in-row icon host (:607-624), the stat/delta line (:630-635), the price column (:637-642), the state chip (:644-652), and the per-row action button (:654-666). The row becomes one Image plate (kept, for the selected-row hold tint via _rowPlates/HighlightSelectedRow :500-515) + one ElarionUiKit.Label name spanning ~0.06-0.94 of the row. Tap still calls _vm.Select(id) (:604) -> Render -> preview.
- Keep RowHeightPx but reduce (e.g. 74 -> ~44) since a name-only row is shorter; rows stay in the VerticalLayoutGroup so childForceExpandWidth=true (:553) auto-fits the new narrow width - no per-row width math. The list stays within canvas width/height because the content band is bounded by the panel.
- Equipped name still bolds + gilts (reuse item.Equipped at :627) so the player sees what is worn.

### 2b. Larger 3D RENDER PREVIEW pane (owner point 3)
- New panel field _previewRoot (RectTransform) anchored (0.42,0.12)-(0.96,0.70), backed by ElarionUiKit.Well(...) (respect FeatureFlags.BlinkChrome alpha, mirror :526-527).
- Inside, top->bottom:
  - _previewImage (RawImage) - square, top ~0.40-0.69 of the pane, fed by the live RenderTexture (S4). When nothing is selected -> hide the RawImage, show "Select an item to preview." empty state.
  - _previewName (TMP, gilt bold, FontHead) - 0.34-0.40.
  - _previewStats (TMP, FontLabel) - 0.27-0.34 <- detail.Value.Stats.
  - _previewDelta (TMP) - 0.21-0.27 <- detail.Value.Delta, colored by the existing DeltaColor helper (PartyShopPanelMvvm :695-701: green Affordable for "+", Danger red for "-", dim for "=").
  - _previewPrice (TMP, LARGE - FontTitle/FontHead, gilt or affordability-colored) - 0.13-0.20 <- the selected item price (S5). "1,250 Gold". Readable per owner point 3.
- Optional emoji never-blank fallback (WO-486 S4): when the resolved model/sprite is null, draw the def emoji icon as a large TMP glyph so the preview never blanks.

### 2c. Bottom action bar (owner point 4) - see S5.

The top band (header/wallet/party/tabs) is UNCHANGED. Only the TYPE-chip row (S3), the slim list, the preview pane, and the bottom button bar change.

---

## 3. Data wiring A - hero weapon/armor TYPE filter (owner point 1)

The hero-FIT filter is ALREADY built: BuildBuy (:371-455) lists only gear the selected member can equip via ShopCatalog.Shoppable(ctx, job, level) (folds WeaponFitsClass/ArmorFitsClass/level). The existing All/Weapons/Armor chips (PartyShopCategory, SetCategory :301) already narrow weapon-vs-armor. The owner asks for a finer TYPE narrow ON TOP of the hero-fit list.

REUSE the category-chip mechanism; ADD a TYPE sub-filter (additive VM surface, pure):
- Add enum PartyShopType { Any, OneHand, TwoHand, Shield, Light, Heavy } to PartyShopVM.cs, plus PartyShopType Type getter + void SetType(PartyShopType) mirroring SetCategory (:301-308) exactly (set -> clear selection -> Rebuild -> Raise).
- In BuildBuy/BuildSell, after the existing class/level/category filter, drop rows that fail the TYPE predicate, read from data ALREADY on the def (no schema change):
  - OneHand -> w.IsOneHandedMain (:132); TwoHand -> w.IsTwoHanded (:124); Shield -> w.IsOffHandItem (:128). damageType (melee/ranged/magic) optional via w.damageType (:57).
  - Light/Heavy (armor) -> a.weight vs GearCatalog.ClassWeight - but since the list is already hero-fit, the meaningful armor TYPE narrow is by slot/keyword once armor gets slots; for v1 Light/Heavy chips can be hidden for a single-class member (their armor is already one weight).
- View: add a TYPE chip row in BuildChrome at (0.04,0.69)-(0.96,0.748) (just under the category bar), building chips with the existing CreateCategory-style helper (:329-336) but calling _vm.SetType(...). Highlight the active chip with the existing TabSelectedTint/TabRestTint pattern (:340-356). Show weapon-type chips (1h/2h/shield) when the active category includes weapons; hide irrelevant chips (mirror CategorySelectorVisible :214 logic). Compute the present types from the built _items and only show chips with >0 rows; never show a dead chip - the chip set adapts to the selected hero.

Net: the filter point reuses ShopCatalog.Shoppable + the fit predicates; the only NEW logic is the small PartyShopType enum + SetType + one predicate pass in the two builders + the chip row in the View.

## 3b. Data wiring B - stat DIFF vs equipped (owner point 3, "+5 dmg green / -2 armor red")

ALREADY built - no new math. DeltaVsEquippedWeapon (:679-687) and DeltaVsEquippedArmor (:689-697) compute the signed delta vs the SELECTED member EquippedWeapon/EquippedArmor (read through the IEquipTarget seam), and DeltaColor (PartyShopPanelMvvm :695-701) maps the sign to green/red/dim. The preview _previewDelta binds detail.Value.Delta and colors via DeltaColor. To match the owner "+5 dmg / -2 armor" wording exactly, OPTIONALLY also surface a raw-number delta (e.g. damageMult points or defense points) by extending the existing delta strings - additive to the same methods.

## 3c. Data wiring C - PRICE (owner point 3, large + readable)

AddBuyWeaponRow/AddBuyArmorRow already put the cost on ItemVM.Price (:471, :487) from GearCatalog.GetBuyCost (gold = GearAppraisal.Appraise). For SELL the refund is ScaleCost(...,0.50) (:523, :539). Expose the selected row price for the preview: add a PartyShopVM.SelectedPriceText getter (or reuse Items/DetailFor to find the selected ItemVM.Price) and bind it to _previewPrice in big font. The buy-vs-sell sign + label come from _tab (Buy shows cost, Sell shows "+refund").

---

## 4. Data wiring D - the 3D RENDER PREVIEW (reuse the BuildPreviewModal rig)

The preview is a runtime RenderTexture rig modeled on BuildPreviewModal.SetupPreview3D (:191-319) - the "Offset Forge" / PreviewRenderUtility pattern the owner referenced.

- Build a small reusable GearPreviewRig (new helper class in the Hero module, or inline private fields on PartyShopPanelMvvm) mirroring BuildPreviewModal:
  - One RenderTexture (e.g. 384^2, ARGB32) assigned to _previewImage.texture (BuildPreviewModal :194).
  - An isolated _previewRoot at y=-5000 on PREVIEW_LAYER 31, with two directional lights + a Camera (UniversalAdditionalCameraData Base, targetTexture = rt), all culling-masked to the layer (:203-304). Nothing renders into the live village.
  - Mount the selected gear MODEL into the rig from the def prefabPath:
    - Resolve the prefab the SAME way the equip path does (DRY with EquipmentController): if LoadsViaAddressable(def) (loadVia=="addressable" or prefabPath starts "gear/", EquipmentController :540-548) -> Addressables.LoadAssetAsync<GameObject>(prefabPath) (mirror BeginAddressableEquip :555+, async, guarded, released on swap/close); else Resources.Load<GameObject>(prefabPath). Instantiate under the rig (guarded, like EquipmentController :606-614).
    - If prefabPath is null/empty (most gear today - GearCatalog :96 "NULL for now") OR the load fails (Blink pack gitignored), FALL BACK gracefully: show the 2D iconPath/ItemIconCatalog sprite via the existing ResolveItemSprite chain (:671-693) on an Image, or the emoji glyph. The preview NEVER blanks (same never-blank law as the rows). Log the branch with FlowTrace.
  - FrameCameraOnRig to fit bounds (:323-344). OPTIONAL drag-to-rotate (:346-402) - a nice-to-have "wow factor"; v1 may auto-spin or sit at a 3/4 angle.
  - TEARDOWN on row-change AND on panel Close: release the Addressables handle, destroy the rig, RenderTexture.Release(), free runtime materials (:498-508). Hook into the existing Close() (:765-781) and into RenderPreview (rebuild the model when _vm.SelectedId changes).
- RenderPreview() - new private method called from Render() (:199-213), reads ONLY _vm.Selected (a PartyShopDetail?) for name/stats/delta/price/iconPath + resolves the def for prefabPath via the same role/id keys the View already uses (GearCatalog.FindWeapon/FindArmor by detail.IconName, as ResolveItemSprite already does at :689/:684). No game-state pull beyond the catalog read the View already performs for sprites.

NOTE on Addressables/Blink: the live 3D model only appears for rows whose prefabPath resolves (Blink "Gear" group imported, or a Resources prefab). For all other rows the 2D sprite/emoji fallback shows - identical to how the equipped body model behaves today. Acceptable for v1; degrades cleanly.

---

## 5. Data wiring E - the two action buttons (owner point 4)

REUSE the existing transaction surface; this is a VIEW button bar, not new logic.

- ONE Purchase/Sell toggle button at (0.36,0.03)-(0.62,0.105). Its LABEL + ACTION toggle on _vm.Tab (mirror the proven ShopPanel pattern at :341-344): Buy tab -> label "Purchase <price>", action _vm.Act(selectedId) which (when not owned) routes to BuyWeapon/BuyArmor (:556-588, auto-equips on buy); Sell tab -> label "Sell <refund>", action _vm.Act(selectedId) -> SellGear (:610-620). The price is prominent in the label AND in the preview _previewPrice (S3c). Greyed/disabled when nothing selected or unaffordable (ItemVM.Affordable).
- An EQUIP button at (0.66,0.03)-(0.88,0.105): action equips the selected OWNED item to the selected member via _vm -> EquipWeapon/EquipArmor (:590-608) -> IEquipTarget.Equip*ById -> GearLoadout.Equip*ById (shields auto-route to off-hand, hand-slot rules enforced). Add a thin PartyShopVM.EquipSelected() command (selects + equips the held id) OR reuse Act when the item is owned (today Act already does EQUIP-if-owned at :467/:484). Disable Equip when the selected item is not owned (must buy first) or already equipped (ItemVM.Equipped).
- Keep the existing Close button (:304-307) and Status line (:309-318). Raise the buttons above the scroll content (SetAsLastSibling) so a row can never eat the tap (the ShopPanel close-button trap :365).

Because the VM already exposes SelectedId, Act, the transactions, and Coins/Affordable, the button bar binds existing members - the toggle is pure View wiring on _vm.Tab + _vm.Selected.

---

## 6. REUSE vs BUILD

| Concern | REUSE (cite) | BUILD (new) |
|---|---|---|
| Hero weapon/armor FIT filter | ShopCatalog.Shoppable + WeaponFitsClass/ArmorFitsClass/ClassWeight (GearCatalog :293-307); BuildBuy (:371) | - |
| Finer TYPE chips (1h/2h/shield) | WeaponDef.IsTwoHanded/IsOffHandItem/IsOneHandedMain (:124-132); SetCategory chip pattern (:301,:329) | PartyShopType enum + SetType + 1 predicate pass + chip row in BuildChrome |
| Slim NAME list | BuildScrollContent VLG (:520-570); _rowPlates/HighlightSelectedRow (:500); narrow _contentRoot anchor (:300) | strip icon/stat/price/chip/button from CreateRow (name-only) |
| Stat DIFF vs equipped | DeltaVsEquippedWeapon/Armor (:679-697); DeltaColor (PanelMvvm :695) | (optional) raw +/- number in delta string |
| Price (large) | ItemVM.Price / GetBuyCost / ScaleCost (:471,:523) | SelectedPriceText getter + big _previewPrice TMP |
| 3D render preview | BuildPreviewModal.SetupPreview3D rig (:191-319); EquipmentController.LoadsViaAddressable+BeginAddressableEquip (:540-555); ResolveItemSprite 2D fallback (:671) | GearPreviewRig + RenderPreview() + _previewRoot chrome |
| Purchase/Sell toggle + Equip | _vm.Tab/Act/Buy*/Sell*/Equip* (:322-620); ShopPanel toggle pattern (:341-344) | bottom button bar in BuildChrome + thin EquipSelected() (or reuse Act) |
| Modal canvas (no PanelSettings) | ElarionUiKit.BuildModalCanvas (PanelMvvm :219) | - |
| Never-blank guarantee | ResolveItemSprite chain + emoji icon (:671) | preview emoji/sprite fallback when prefab missing |

Net NEW code: ~1 enum + SetType/Type + 1 predicate pass + (optional) SelectedPriceText/EquipSelected in PartyShopVM.cs; the CreateRow slim + BuildPreviewPane/RenderPreview + GearPreviewRig + TYPE chip row + button bar in PartyShopPanelMvvm.cs. No new files required (rig may be a private nested helper). No new assembly, no new flag, no schema change.

---

## 7. Files to edit
- Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs - slim CreateRow to name-only; narrow _contentRoot anchor (:300); add TYPE chip row; add _previewRoot + BuildPreviewPane + RenderPreview + the GearPreviewRig (RenderTexture rig); add the Purchase/Sell + Equip button bar; teardown the rig in Close (:765).
- Assets/_Modules/Village/Hero/PartyShopVM.cs - add PartyShopType enum + Type/SetType + the TYPE predicate in BuildBuy/BuildSell; add SelectedPriceText (and optional EquipSelected). All additive, pure (System.Math; no UnityEngine UI types) - keeps the VM unit-testable.
- (OPTIONAL data) Assets/Resources/Data/Canonical/weapons.json / armor.json - author prefabPath (and iconPath) on the showcase gear so the 3D model actually resolves; otherwise the 2D fallback shows. Edit the Resources copy; the pipeline re-syncs StreamingAssets/Builds (do NOT hand-sync).

Do NOT touch: any .unity scene, VillageSceneBuilder*.cs, combat/ATB/BattleArena, GearCatalog.cs schema (fields exist), ItemIconCatalog.cs, EquipmentController.cs (call its public LoadsViaAddressable pattern; don't fork it), the StreamingAssets/Builds JSON mirrors.

---

## 8. PanelSettings / scene-wiring note (the DISABLED gate)

- The store scene-wiring is DISABLED pending its own PanelSettings (CLAUDE.md S8; PIPELINE_STATE.md :67 WO-22, :181) BECAUSE the legacy store risked UXML in builds (which does NOT work - CLAUDE.md S8).
- This redesign side-steps that entirely: PartyShopPanelMvvm is code-built uGUI and builds its OWN screen-space Canvas on Open via ElarionUiKit.BuildModalCanvas("PartyShopPanelMvvmUI", 31000) with overrideSorting (:219-221). It needs NO PanelSettings and ships NO UXML - the code-built escape hatch the disabled UXML store cannot use. So this WO does NOT require the WO-22 PanelSettings re-enable.
- The 3D preview adds a runtime Camera + RenderTexture under URP. Mirror BuildPreviewModal UniversalAdditionalCameraData Base setup (:292-296) so URP actually renders the rig to the RT; isolate on PREVIEW_LAYER 31 with culling masks so it never touches the live scene or any PanelSettings UI.
- The panel is gated OFF behind FeatureFlags.PartyShop; the bootstrap only spawns it when ON, and the legacy ShopPanel is suppressed when the flag is ON (so the two never double-open). No scene change.

---

## 9. Headless verify (no owner playtest required)
Extend DataRegression / the AutoPilot store oracle (per WO-486 S9, CLAUDE.md S3 headless gate):
- STORE-HERO-FILTER: for each class, the built BUY CurrentStock contains ONLY ids that pass WeaponFitsClass/ArmorFitsClass for that class (the hero-type filter holds).
- STORE-TYPE-CHIP: with a TYPE chip active, every built row satisfies the chip predicate (1h->IsOneHandedMain, etc.) and no dead (zero-row) chip is shown.
- STORE-PREVIEW-RESOLVES: for the selected id, the preview yields a 3D model OR a 2D sprite OR an emoji - never neither (never-blank, like WO-486 STORE-ICON-RESOLVES).
- STORE-DIFF: DeltaVsEquipped* returns the correct signed delta vs a fake IEquipTarget equipped def (pure VM test, no scene).
- STORE-TOGGLE: the Purchase/Sell button label+action follows _vm.Tab; Equip equips the held id to the selected member IEquipTarget.
Run under the existing headless CompileGate + AutoPilot fleet (no editor open). The VM tests are pure - drive the VM with a fake IEconomy/IInventoryStore/IEquipTarget (the seams already exist).

---

## 10. Notes for the implementer
- Presentation-only View: every datum comes from _vm.* getters (Selected, Items, Tab, Type, SelectedPriceText). Never state-pull a def in the View except the existing catalog sprite/prefab resolution the rows already do (GearCatalog.Find* by the VM-supplied id key).
- DRY the prefab load: have the preview rig call the SAME Addressable-vs-Resources branch as EquipmentController.LoadsViaAddressable; do not duplicate the load heuristic.
- Reuse ONE sprite resolver: keep ResolveItemSprite (:671) as the single 2D fallback for rows AND the preview no-model case.
- Instrument per S12: FlowTrace.Step("Store", "preview model id=.. branch=addressable|resources|sprite|emoji") so headless capture proves which branch took.
- ASCII-only; code-built UI only (no UXML); pool/one-owner for the preview rig (one rig, rebuilt on select, torn down on close) - never leak a RenderTexture or material (BuildPreviewModal :498-508).
