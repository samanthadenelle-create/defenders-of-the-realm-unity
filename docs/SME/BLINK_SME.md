# BLINK SME Dossier — the Blink (RPG Art) pack family

**Date:** 2026-07-11 (overnight SME research session)
**Pack root:** `Assets/Blink/` (repo-root-relative; the root is machine-dependent) (**gitignored** — never referenced directly at runtime; see §2.1)
**Publisher:** **Blink** — Unity Asset Store publisher id **49855**
(https://assetstore.unity.com/publishers/49855), company site blinkstudios.dev,
docs hub https://blink.developerhub.io/ (they are the makers of **RPG Builder**).
Support runs through their Discord (discord.gg/fYzpuYwPwJ).

**Products installed on disk** (identities per the owner's purchase ledger,
`docs/SME/ASSET_STORE_LEDGER_2026-07-12.md`, all purchased 2026-06-15 — mapped to
on-disk folders below):

| Product (store id) | Version | On-disk folder |
|---|---|---|
| **OBSIDIAN UI — RPG/MMORPG/ARPG** (206302) | 1.0 | `Art/UI/Obsidian_UI` + `Tools/UIReSkinner` |
| **400 Low Poly RPG Weapons** (207720) | 1.0.1 | `Art/Weapons/LowPoly/MegaWeaponPack1` |
| **500 RPG Spell Icons — Fantasy** (200510) | 1.0 | `Art/Icons` (+ `Art/UI/Free_Blink_Icons`) |
| **Stylized Orcs Bundle — RPG NPC** (220636) | 1.0 | `Art/NPCs/Stylized/Orcs` |
| **Stylized Female** (201307) v3.0 + **Modular Male** v3.0 + **Character Customization M/F** v1.0 | — | `Art/Characters/Stylized` |
| **Stylized Armor Sets 1** (205939) v2.0 / **2** (219757) / **3** | — | `Art/Characters/Stylized/Humans/ArmorPack1..3` |
| **Stylized RPG Armor Sets Bundle** (227641) + **Bundle 2** (stubs) | 1.0 | `StylizedArmorBundle2/README.txt` (claim-links only) |
| **Low Poly Human — RPG Characters** v1.2 + **Low Poly Armor Sets 1/2/4/5/6** (set 3 NOT owned) | — | `Art/Characters/LowPoly` |
| **70+ Stylized Textures Bundles 1+2** (3.8+3.5 GB) + **Realistic Ice Textures** (1.7 GB) | 1.0 | `Art/Textures` |
| **RPG Art ULTIMATE Bundle** (338650) | 1.0 stub | `UltimateBundle/README.txt` (claim-links only) |

The two "bundle" folders are **stubs**: `UltimateBundle/README.txt` and
`StylizedArmorBundle2/README.txt` contain only Asset Store claim links (Serf / Hydra /
Warrior / Lion Guard / Mystic / Panther / Peasant / Phoenix / Wizard / Pirate / Chaos
Servant / Priest / Dwarven / Engineer / Guard armor sets, ids 338224–338258). **Those 15
armor sets are claimable for free but are NOT yet imported into the project.**

---

## Table of contents

1. [Inventory — what is on disk](#1-inventory)
2. [How WE consume it](#2-how-we-consume-it) — CANON-live (UI frame, weapons) vs JUNKED (armor rigs) vs UNUSED
3. [Intended usage — how Blink means it to be assembled](#3-intended-usage)
4. [Web research — publisher, products, official workflow](#4-web-research)
5. [Opportunities + gaps + DO-NOT-REUSE](#5-opportunities--gaps)
6. [Executive summary](#6-executive-summary)

---

## 1. Inventory

### 1.1 Weapons — `Art/Weapons/LowPoly/MegaWeaponPack1` (400 Low Poly RPG Weapons)

**400 prefabs = 16 families × 25** in `_Prefabs_MWP1/` (verified count), naming
`<Family><Hands>_<NN>` e.g. `Swords_1H/Sword1h_01.prefab`:

Axes_1H, Axes_2H, Bows, Claws_1H, Crossbows, Daggers_1H, Hammers_2H, Maces_1H,
Polearms_2H, Scythes_2H, Shields, Spellbooks, Staves, Swords_1H, Swords_2H, Wands
— 25 each. Source FBX in `Meshes_MWP1/` (~405), one shared atlas material set in
`Materials_MWP1/`, demo scene `_DEMO_MWP1/DEMO_LowPolyMegaBundle1.unity`.

**Bundled script:** `Scripts_MWP1/MaterialTilingOffset.cs` (+ an Editor copy) — the
pack's entire color-variant system. It is a trivial MonoBehaviour: on Awake it takes the
renderer's material and sets `mainTextureOffset = (xOffset, yOffset)` plus a material
tint. The weapons share one small palette-atlas texture; shifting the UV offset selects
a different palette cell → the advertised **64 color variations per weapon (25,600
combos)** without extra textures. A small editor tool in the pack drives batch variant
creation. Weapons are static meshes (~300 tris average) **modeled with the grip at the
origin, identity rotation** — the fact our WO-478 "native" seating relies on.

### 1.2 Characters / armor — `Art/Characters` (292 prefabs total)

Two parallel tech tiers, both centered on **one base human body per gender** with armor
as full-body skinned outfits sharing that armature:

- **Stylized tier** (`Stylized/Humans/`, the Modular Male / Stylized Female products):
  - Base bodies: `Prefabs_Humans/HumanMale_Character.prefab` + `HumanFemale_Character.prefab`,
    imported **Humanoid** (fbx meta `animationType: 3`).
  - `Prefabs_Humans/Customization/` — **88 attachment prefabs** (Beard5–19, Eyebrows, Hair,
    Faces, Earrings…; the Earring/attachment FBX are Generic `animationType: 2` static
    attachments). This is the Character Customization M/F product content;
    `FemaleCustomizationDemo.unity` demos it.
  - Armor: `ArmorPack1` **90 prefabs**, `ArmorPack2` **30**, `ArmorPack3` **30**
    (Stylized Armor Sets 1/2/3). Naming: `Cloth1_1_1_HumanMale.prefab` etc. — each set
    ships a HumanMale + HumanFemale variant. Themed sets include Wolf, Stag, Centurion,
    LionGuard, PantherKnight, DragonHunter, DemonHunter, Minotaur, Hydra, Dragonic, Bear,
    Boar, Savage, Engineer (see `docs/BLINK_NOTES.md`).
  - **Import gotcha (proven 2026-06-20):** the ~70 armor-body FBX shipped **Generic**
    while the base bodies are Humanoid → Humanoid clips have nothing to retarget onto →
    T-pose. Our `Assets/Editor/BlinkArmorHumanoidFixer.cs` flips them to Humanoid and
    copies the base body's avatar.
- **LowPoly tier** (`LowPoly/Humans_LowPoly/`, Low Poly Human v1.2 + Low Poly Armor Sets):
  base `HumanMale_Character` / `HumanFemale_Character` prefabs + **50 armor prefabs**
  under `ArmorPacks/Prefabs`, demo scenes `ArmorPacks/Demos/ArmorPack1/2/4/5/6_LowPoly.unity`
  (set 3 not owned — matches the ledger). This LowPoly male base is the body
  `BlinkAddressableMarker` publishes as `hero/base/HumanMale`.
- **Shared animation library** `Art/Animations/{Combat,Gathering,Movement}` + per-tier
  `Animations_Starter_Pack` — humanoid clips (BlockingLoop, BowShot, Buff, CastingLoop,
  Death, GetHit, IdleCombat, MeeleeAttack_OneHanded/TwoHanded, Punches, SpellCast,
  SpinAttack variants… plus Gathering and Movement sets).

### 1.3 NPCs — `Art/NPCs/Stylized/Orcs` (Stylized Orcs Bundle, 27 prefabs)

**Four archetypes × three skin variants = 12 character prefabs**: `Orc_Warrior1..3`,
`Orc_Hunter1..3`, `Orc_Warlock1..3`, `Orc_Boss1..3` — plus **15 weapon/prop prefabs**
(`Orc_Axe1..3`, `Orc_Bow1..3`, `Orc_Staff1..3`, `Orc_Arrow1..3`, `Orc_Boss_Weapon1..3`).

- **Rig: Unity Humanoid** (`Orc_Warrior.fbx.meta animationType: 3` — verified), i.e.
  professionally rigged and retargetable with ANY humanoid clip we own.
- **Two complete animation sets** (all FBX, also Humanoid): `Animations_Orcs` (22 clips:
  BowShot, Buff, CastLoop, Death, FallingLoop, GetHit, Idle, IdleCombat, Jumps, MagicAoE,
  MeleeAttack_One/TwoHanded, 6 run directions, SpellCast, 2 strafes, StunnedLoop) and a
  parallel `Animations_OrcBoss` set (22 OrcBoss_* clips).
- **Two ready animator controllers**: `OrcAnimator.controller`, `OrcBossAnimator.controller`.
- Demo scenes per archetype under `Demo_NPCs/Demo_Orcs/`.

### 1.4 UI art — `Art/UI/Obsidian_UI` (OBSIDIAN UI product) — THE canon source

PNG counts per folder (verified): **Panels 22** (Inventory, Stats, Crafting,
Talent_Tree, Merchant, Dialogue ×2, Quest ×2, Core ×2, Loot, Options, Settings, Pet,
Panel_Element, Text_Background, and Male/Female/Pet silhouettes for paper-dolls),
**Slots 18** (Inventory_Slot, Armor_Slot ×2, Character_Slot, Action_Bar_Slot,
Socketing_Slot…), **Buttons 42**, **HUD 50** (bars, cast bars, chat, minimap art),
**Icons 71**, **Elements 31**, **Decoration 38**, **Shapes 9**, **Cursors 10**.

- **Fonts:** four families with licenses — **Acme, Alata, Merriweather, Titillium**
  (`Fonts_Obsidian/`).
- **Assembled uGUI prefabs** (`Prefabs_Obsidian/`): **31 widget prefabs** (Bar1–7,
  CastBar1–3, DiabloHealth/DiabloMana orbs, Close/Collapse/Expand buttons, Toggle1–3,
  Rectangle/Rounded button families in Gray/Green/Red/Yellow variants) **+ 27 full-screen
  prefabs**: HUDCore, HUDCore_Diablo, Inventory, Loot, MerchantPanel, Crafting,
  Enchanting, Socketing, TalentTree, QuestLog, QuestPanel, QuestTracker, Chat, Minimap,
  CharacterCreation, CharacterSelection, Characters, Dialogues, GameMenu, LoginScreen,
  LoadingScreen, PetPanel, PartyNameplate, TargetNameplate… These are complete,
  functioning screens — the "fully functioning car" of the owner's 2026-07-03 ruling.
- **Demo scene:** `_DEMO_UIPacks/OBSIDIAN_DEMO.unity` — every screen laid out live.
- **Bundled runtime script:** `UI_Scripts/StatBar.cs` (`BLINK.UI.StatBar`) — a demo-only
  ping-pong fill animator: after `delay`, every FixedUpdate it adds/subtracts `speed`
  to `Image.fillAmount`, bouncing between 0 and 1. Decorative demo logic, not a real
  health bar — our kit binds fills itself.
- **Integrations** (`Integrations_Obsidian/`): `BlinkUIReSkinner.unitypackage` +
  `Obsidian_UIToolbox_Templates_RPGB.unitypackage` + RPGBuilder UIToolbox template
  `.asset` files (Bars, HUD_Main, HUD_Main_Diablo, Minimap, Pets…) — for reskinning
  their RPG Builder product; not applicable to us directly.

### 1.5 Reskin tool — `Tools/UIReSkinner` (Blink UI ToolBox)

`Editor/Blink_UI_ReSkinner.cs` + `Scripts/{UIElementTemplate,UIPanelTemplate,UIToolboxData}.cs`
(namespace `BLINK.UIToolbox`). Logic: **ScriptableObject-driven UI theming**. A
`UIPanelTemplate` is a named list of `UIElementTemplate`s; each element template carries
typed `EntryField`s for every skinnable property of a uGUI widget (sprite, highlight/
pressed/selected/disabled sprites and colors, fill/background/handle sprites, font +
TMP font, preserveAspect, raycastTarget, floats…). The editor window walks a selected
UI hierarchy and stamps the template values onto matching widgets — i.e. swap an entire
UI skin (Obsidian → another Blink theme) by applying a different template set. Not
web-indexed as a standalone product; it ships inside their UI packs.

### 1.6 Icons — `Art/Icons` (500 RPG Spell Icons) — **608 PNGs**

Organization: `Classes/<ArchetypeGroup>/<Class>/<Class><N>.png` — 5 archetype groups
(**Assassin, Elementalist, HolyDarkness, Symbiose, Warrior**) × 5 classes each = **25
classes** (Brawler, DemonHunter, Hunter, Ranger, Rogue; Arcanist, Cryomancer,
Electromancer, Geomancer, Pyromancer; Cultist, Medium, Necromancer, Paladin, Priest;
Beastmaster, Druid, Enchanter, Shaman, Shapeshifter; Barbarian, Berserker, Deathknight,
Dragonknight, Guardian) × **20 spell icons each** = 500. Plus `Emblems/` — **25 class
emblems** (one per class); `Extra/` — 5 per-archetype background sheets + 1 illustration
per archetype; `Extra/Slots/` — Slot1–3 generic action-bar slots + **25 per-class themed
`Slot_<Class>.png` action-bar slots**; `SourceFiles/` + `Demo/Demo_SpellIconBundle.unity`.
`Art/UI/Free_Blink_Icons` = the 17-icon free sampler.

### 1.7 Textures — `Art/Textures` (~9 GB, 9 biome families)

`StylizedForestTextures` (24 sets), `StylizedDesertTextures` (20), `StylizedIceTextures`
(16), `StylizedLavaTextures` (15), `StylizedEgyptTextures` (14), `StylizedVikingTextures`
(15), `StylizedNecromancerTextures` (17), `StylizedDungeonTextures` (15),
`RealisticIceTextures` (15) — each texture a folder with tiling PBR maps; one demo scene
per family under `Demo/`.

---

## 2. How WE consume it

### 2.1 The gating fact — gitignored, never referenced directly

`Assets/Blink` is **gitignored** (owner-purchased, not redistributable; absent on fresh
clone / CI / WebGL). Two sanctioned consumption paths, both fresh-clone safe:

1. **UI: mirror-to-Resources** — editor importers `CopyAsset` the used slice into
   **committed** `Assets/Resources/RpgUi/` (fresh GUIDs, import settings preserved).
   Pack absent ⇒ LogWarning + no-op; the committed mirrors keep working.
2. **Gear/bodies: Addressables** — `BlinkAddressableMarker` files pack prefabs into a
   "Gear" Addressables group under a stable address scheme; runtime loads by address and
   releases handles.

### 2.2 CANON-LIVE — the Obsidian UI master-frame system (BINDING canon)

Canon doc: `docs/UI_BLINK_TEMPLATE_CANON.md` — "The Blink frame IS the chrome. Screens
NEVER restyle — they drop chrome-less content into the frame's pre-styled drop-zones."

**Import side (editor, regenerate-only):**
- `Assets/Editor/RpgUiImporter.cs:50` — `BlinkRoot = "Assets/Blink/Art/UI/Obsidian_UI"`;
  entry table lines 184–213 mirror the **frames** (Inventory/Crafting/Stats/Core/Core_2/
  Talent_Tree/Merchant/Dialogue/Quest_Log/Settings/Options/Loot/Pet → `frame_*`),
  **silhouettes** (Male/Female/Pet → `sil_*`, lines 203–205) and **slots**
  (Inventory/Armor/Armor_2/Character/Action_Bar/Socketing → `slot_*` 9-sliced,
  lines 208–213) into `Resources/RpgUi/`. Menu: `Defenders/Art/Import RPG UI Pack`.
- `Assets/Editor/BlinkUiImporter.cs:30` — second mirror pass (`PackRoot` same folder):
  panels-as-9-slice (`panel_vendor/window/grid/quest/portrait/bar/tab`, lines 130–138),
  HUD roles, plus the sprite atlases. Menu: `Defenders/Art/Import Blink UI Pack`.
- `Assets/Editor/BlinkPrefabMirror.cs:1-33` — the **P0 centerpiece** (owner 2026-07-03
  "why recreate the wheel"): mirrors the pack's **assembled uGUI prefabs** into
  `Resources/RpgUi/prefabs/` with transitive GUID remapping of sprite/font/nested-prefab
  deps and a zero-pack-GUID validation. Scope v1 = HUD-critical set (HUDCore, nameplates,
  CastBar1–3, QuestTracker, Chat, Minimap + all button/bar widgets). **40 mirrored
  prefabs** exist in `Assets/Resources/RpgUi/prefabs/` today; full screens are an
  explicit second pass, not yet done.
- `Assets/Editor/BlinkFontImporter.cs:1-20` — bakes 3 TMP SDF fonts from the pack fonts:
  Merriweather-Bold → `font_title`, Alata-Regular → `font_body`, Acme-Regular →
  `font_stamp` (Titillium reserved), committed under `Resources/RpgUi/font/`.

**Runtime side:**
- `Assets/_Modules/Core/UI/RpgUiCatalog.cs:60-76` — role-based loader over the mirrored
  art (`RoleFrame/RoleSilhouette/RoleSlot/…`, `PrefabFolder`, `FontFolder`); every `Get`
  returns null when art is absent so callers keep a procedural fallback.
- `Assets/_Modules/Core/UI/ElarionUiKit.cs` — `BuildObsidianPanel(..., frameName)` = the
  ONE master factory; `ZonesFor(frameName)` = the per-frame drop-zone table (header /
  body / medallion / footer). `ElarionUiKitObsidian.cs:122-146` — **prefab-first mode**:
  every builder first tries the mirrored Blink prefab by candidate name
  (`InstantiateBlinkPrefab`), procedural build as fallback; e.g. bars at :362-373,
  action slots at :899, cast bars at :1065. One chrome tint for all Blink art at :60.
- `Assets/_Modules/Core/FeatureFlags.cs:104-108` — `BlinkChrome` debug flag ("hide our
  UI dressing" to A/B the pure Blink panel look, default OFF; menu at :623).
- **Hero-select carousel (WO-559):** `Assets/_Modules/Onboarding/HeroSelectController.cs:5-7,91,192`
  — the owner-pinned Blink character-creation design rebuilt in code on the Blink
  Obsidian master frame (`FrameCharacter` chrome via `ElarionUiKit.PanelChrome`).
- Downstream screen consumers (all via the factory, not via Blink paths):
  `InventoryUIBuilder.cs`, `EquipmentPanel.cs`, `ShopPanel.cs`, `PartyShopPanelMvvm.cs`,
  `BuildingUpgradePanelMvvm.cs`, `SettingsController.cs`, `TitleController.cs`,
  `GameGuidePanel.cs`, `EndStateView.cs`, `RaidSelectionScreen.cs`, `HelpMenu.cs`,
  `BugReportView.cs`, `HudKitController.cs`, `DialogueView` (the §8 reference impl)…
- Regression: `Assets/Editor/Regression/UiObsidianConformanceRegression.cs` enforces the
  formula; showcase/gallery builders `HudObsidianShowcaseSceneBuilder.cs`,
  `ObsidianComponentGalleryBuilder.cs`, `ObsidianDemoCapture.cs`.

### 2.3 CANON-LIVE — weapons via Addressables (WO-478 native seating)

- `Assets/Editor/Catalog/BlinkAddressableMarker.cs:44-71` — marks the pack Addressable:
  weapon root `Assets/Blink/Art/Weapons/LowPoly/MegaWeaponPack1/_Prefabs_MWP1` (:49),
  armor root (:51), LowPoly base body (:60). **Address scheme (shared contract):**
  `gear/weapon/<PrefabName>` (e.g. `gear/weapon/Sword1h_01`), `gear/armor/<Name>`,
  `hero/base/HumanMale`, single group "Gear". Menu: `Defenders/Catalog/Mark Blink Gear
  Addressable`.
- `Assets/Editor/Catalog/GearCatalogGenerator.cs:105-112` — `BlinkGearSource` is **the
  PRIMARY gear source** (~805 weapon+armor assets scanned); stamps `loadVia="addressable"`
  on every Blink row, derives category from the filename encoding (`Sword1h_01` →
  1-hand sword, :90-94), writes the committed catalog JSON.
- `Assets/_Modules/Village/Hero/GearCatalog.cs:99-103,176-178` — `WeaponDef.loadVia` /
  armor `loadVia` fields: `"addressable"` ⇒ prefabPath is an Addressables address.
- `Assets/_Modules/Village/Hero/EquipmentController.cs` — the runtime consumer:
  - `:101` — `native` flag: **Blink props are authored grip-at-origin + oriented, so
    seating trusts them and skips grip normalization** (WO-478); `:197` — the Knight's
    starter sword is the Blink `Sword1h_01`-class prefab (`Native(Sword("sword_A"))`).
  - `:697-734` — data-driven equip: `LoadsViaAddressable(def)` (reads loadVia or a
    `gear/` prefix) → async `Addressables.LoadAssetAsync`, attach on completion,
    hero never left unarmed if the prefab doesn't resolve (WO-425 invariant).
  - `:292-303,529` — handle ownership: ONE owner, released on every swap / off-hand
    change / detach / OnDisable so a Blink prefab never leaks (shields load via
    `gear/weapon/Shield1h_XX`).
- `Assets/Editor/Catalog/GearIconRenderer.cs:1-20` — renders real item thumbnails from
  the Addressable prefabs into committed `Resources/ItemIcons/` for the store UI.

### 2.4 JUNKED — the Blink armor/hero-body path (do not resurrect)

Owner pivot 2026-06-22: **Blink armor is JUNKED as the hero art foundation** (hero =
dedicated Knight rig via the hero Addressables package; memory `blink-canonical-art-foundation`).
The machinery still exists but is flag-gated OFF:

- `Assets/_Modules/Core/FeatureFlags.cs:47-52` — `BlinkArmor` default **false**
  (`ff.blinkarmor`); when OFF, armor equips never touch the Blink swap.
- `Assets/_Modules/Village/Hero/HeroArmorVisual.cs:102-105,128` — the full-body armored
  skinned-mesh body swap (humanoid-retargeted over the hidden base body); both entry
  points early-return on the flag.
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs:43-46,74-75` — the Blink LowPoly base
  body Addressable (`hero/base/HumanMale`); **the Knight explicitly skips the Blink base
  load** and builds the armored Tripo body directly.
- `Assets/Editor/BlinkArmorHumanoidFixer.cs` — the historical Generic→Humanoid armor FBX
  fix (T-pose root cause, 2026-06-20). Keep for archaeology; only relevant if armor swaps
  ever return.
- `Assets/_Modules/Village/BlinkWardrobe.cs` — the **Dressable capability** (TKT-2):
  toggles the Starter_/Cloth_ skinned pieces already present on a modular Blink body
  (prefix-match by mesh name, :103-106). Still invoked by
  `VisualFactory.cs:185-192` (`DressInStarter` on any dressable body) and referenced for
  companions (`StoryCompanionInjector.cs:666`) — i.e. **live for NPC bodies that happen
  to be Blink-modular, junked for the hero**.

### 2.5 UNUSED — owned but not consumed anywhere (verified by grep)

- **Stylized Orcs Bundle** — ZERO code references to `Assets/Blink/Art/NPCs`. Every
  in-game orc is **Tripo** (`Assets/Art/Incoming_Tripo/Enemies/Orcs/` promoted by
  `Assets/Editor/PromoteOrcsToResources.cs:24`; `EnemyFactory.cs` orc handling is all
  Tripo rig/material fixing). We fought the Tripo orc rig war while owning
  professionally Humanoid-rigged orcs with two full 22-clip animation sets and ready
  animator controllers. See §5.1.
- **500 RPG Spell Icons** — ZERO references to `Art/Icons`/Emblems/Slots. Ability-bar
  icons are owner-drawn (`Resources/RpgUi/abilities/attack_sword.png` etc.), and
  `ElarionUiKitObsidian.cs:673` explicitly scopes Blink icons out of the wallet widget.
  608 professional icons sit unused. See §5.2.
- **Texture bundles (~9 GB, 9 biomes)** — ZERO code references (scene/terrain GUID usage
  not audited, but no importer/builder touches them).
- **Obsidian full-screen prefabs** (Inventory/Merchant/TalentTree/CharacterCreation/…)
  — explicitly deferred as BlinkPrefabMirror's "second pass".
- **UI ReSkinner tool, Decoration (38) / Shapes (9) / Cursors (10) sprites, the free
  armor-set claim links (338224–338258), Titillium font.**

---

## 3. Intended usage — how Blink means it to be assembled

From the pack's own demos/scripts and Blink's published RPG Builder documentation
(their reference consumer — https://blink.developerhub.io/rpg-builder/items):

- **Modular characters:** one base body prefab per gender (Humanoid avatar) carrying the
  bare-skin mannequin meshes; **armor = pre-skinned SkinnedMeshRenderer objects bound to
  the same armature, toggled enabled/disabled by GameObject name on equip/unequip**, with
  optional per-slot material assignment and **body culling** (hide the body parts under
  the armor to stop poke-through). Customization pieces (hair/beard/eyebrows/faces/
  earrings) are additional named attachments. Our `BlinkWardrobe` prefix-toggle is
  exactly this model; our `HeroArmorVisual` full-body swap-in was a heavier variant.
  Blink's sanctioned retarget tool is their free **Skinned Mesh Transfer**
  (assetstore id 219764) for re-skinning armor onto a different armature.
- **Weapons:** separate static prefabs **spawned into a named weapon-slot transform** on
  the character, with per-state (combat/rest) position/rotation offsets configured in
  data — which works because every weapon is authored grip-at-origin. Color variants via
  `MaterialTilingOffset` UV-offset into the shared palette atlas.
- **Orcs/NPCs:** drop-in character prefabs with the supplied animator controller; the
  archetype anim sets are Humanoid FBX so any humanoid library retargets onto them.
  Weapon props parented to hand bones like the human weapons.
- **Obsidian UI:** assembled **uGUI screen prefabs are the product** — you drop
  HUDCore/Inventory/TalentTree into a Canvas and rebind; widget prefabs (bars, cast bars,
  toggles, buttons) compose smaller surfaces; the raw PNGs (panels Simple-imported,
  slots/buttons 9-sliced) + 4 licensed fonts support custom builds. `OBSIDIAN_DEMO.unity`
  is the showroom. `StatBar.cs` only animates demo fills. The **UI ToolBox / ReSkinner**
  (ScriptableObject templates per widget type, applied over a hierarchy) is their theming
  path, chiefly for reskinning RPG Builder UIs via the bundled UIToolbox template assets.
- **Icons:** per-class folders of 20 numbered spell icons + 1 emblem + 1 themed action-bar
  slot each, plus generic slots and per-archetype background/illustration sheets — i.e. a
  ready visual identity kit for a 25-class talent/ability system.
- **Textures:** per-biome tiling terrain/wall sets, one demo scene per biome.

---

## 4. Web research

- **Publisher:** "Blink", Asset Store publisher **49855**
  (https://assetstore.unity.com/publishers/49855); company blinkstudios.dev; docs hub
  https://blink.developerhub.io/; support via Discord. Best known for **RPG Builder**
  (their no-code RPG framework — which explains why every art pack ships RPG-Builder
  integration folders).
- **Product pages** (matched to our folders):
  - OBSIDIAN UI — https://assetstore.unity.com/packages/2d/gui/obsidian-ui-rpg-mmorpg-arpg-206302
    (2D GUI, v1.0 2021, all pipelines; uGUI-era sprite pack — no UI Toolkit claim anywhere,
    consistent with our §1 framework decision).
  - 400 Low Poly RPG Weapons — https://assetstore.unity.com/packages/3d/props/weapons/400-low-poly-rpg-weapons-207720
    (16 families × 25, ~300 tris, 64 color variants per weapon via the editor tool; UE
    mirror confirms family list).
  - 500 RPG Spell Icons — https://assetstore.unity.com/packages/2d/gui/icons/500-rpg-spell-icons-fantasy-200510
    (25 classes × 20 icons + emblem + themed slot each).
  - Stylized Orcs Bundle — https://assetstore.unity.com/packages/3d/characters/humanoids/fantasy/stylized-orcs-bundle-rpg-npc-220636
    (Fab mirror confirms **Unity Humanoid rig**, 3 color variants, ~29 animations,
    warrior/hunter/warlock/boss).
  - Stylized Female — …/stylized-female-human-rpg-character-201307; Armor Sets 1
    (205939), 2 (219757 — sets composed of Helmet/Shoulders/Chest/Belt/Gloves/Pants/
    Boots), Bundle (227641); RPG Art ULTIMATE Bundle (338650). The 338224–338258 armor
    set ids in our stub README are a mid-2026 listing wave adjacent to the Ultimate
    Bundle id — too new to be web-indexed yet.
  - 60+/70+ Stylized Textures Bundle — …/60-stylized-textures-bundle-rpg-environment-206183.
  - Free samplers: Free Low Poly Swords (198166), Free RPG Fantasy Spell Icons (200511),
    FREE UI Utility (206817).
- **Official workflow guidance found:** armor = shared-rig skinned meshes toggled by name
  with body culling; weapons = slot-attached prefabs with per-state offsets; retargeting
  via their free **Skinned Mesh Transfer** tool (219764, tutorial
  https://www.youtube.com/watch?v=vu8kd4wrfG4). **No publisher guidance on Addressables
  exists** — our Addressables + mirror-to-Resources pipelines are our own architecture
  (and the correct one for a gitignored pack).
- Caveat: the Asset Store renders descriptions client-side; some per-package details are
  inferred from mirrors (Unreal/Fab listings, gameassetdeals) as flagged.

---

## 5. Opportunities + gaps

### 5.1 Stylized Orcs Bundle — the biggest sleeping asset

We own drop-in, **professionally Humanoid-rigged** orc NPCs (4 archetypes × 3 skins,
two complete 22-clip combat/locomotion sets incl. strafes/casting/stun/death, ready
animator controllers, matching weapon props) — and never used them, while spending
significant effort rigging Tripo orcs. Options (owner decision — the Tripo
Knight/Orcs-first roster is settled canon, so this is an *offer*, not a directive):
per-family Addressables group (the WO-545 pattern) as additional enemy families or
variants; the boss set as a dungeon boss; `Orc_Axe/Bow/Staff` props for the
`EnemyWeapons` flag; or their anim clips retargeted onto our existing humanoid enemies
(all Humanoid FBX). Zero rig war required.

### 5.2 500 RPG Spell Icons — a free win for talent tree / abilities / hero identity

The 68-node talent tree v2, ability hot-swap HUD, and class kits currently run on a
handful of owner-drawn icons. We own 500 class-themed spell icons + 25 class emblems +
25 themed action-bar slots, organized per class. Consumption is a one-afternoon importer:
add entries to `RpgUiImporter.BuildEntryTable()` (the §5 pipeline already exists) or a
sibling `BlinkIconImporter` mirroring only the classes we ship. Emblems fit the
hero-select carousel and talent-tree headers; themed slots fit the action bar. When
picking icons remember the owner is red/green colorblind — choose by silhouette/shape
distinctiveness, never by hue.

### 5.3 Obsidian second pass — the full-screen prefabs

`BlinkPrefabMirror` scope v1 mirrored only the HUD-critical set. The pack's assembled
Inventory / MerchantPanel / TalentTree / Crafting / Socketing / CharacterCreation /
LoginScreen / LoadingScreen / GameMenu screens are unmined references (or direct
mirrors) for screens we still build procedurally. Also unmined: Decoration (38 sprites
of ornamental chrome), Cursors (a full custom cursor set — cheap polish), Shapes, and
the Titillium font role.

### 5.4 Texture bundles for the dungeon/biome composer

~9 GB of per-biome tiling sets (notably **StylizedDungeonTextures** and
**StylizedNecromancerTextures**) map directly onto the chunk-composer dungeon north-star
(WO-479) and outer-world biomes — e.g. as terrain layers or dungeon material palettes.
Would need mirroring/Addressables like everything else (gitignored).

### 5.5 Unclaimed free content

`StylizedArmorBundle2/README.txt` + `UltimateBundle/README.txt` list ~15 armor sets
(Serf → Guard, ids 338224–338258) claimable for FREE on the owner's account but never
downloaded/imported. Low priority while armor swaps are junked, but the claim should
happen while the links are valid.

### 5.6 DO-NOT-REUSE list (so future sessions don't resurrect the junked path)

1. **Blink armor as the HERO's visual** — `HeroArmorVisual`'s full-body swap behind
   `FeatureFlags.BlinkArmor` (default OFF, `FeatureFlags.cs:47-52`). Do not flip the
   flag on, do not route `gear/armor/*` rows to the hero, without an explicit owner
   reversal. Hero = dedicated Knight rig via the hero Addressables package.
2. **The Blink LowPoly base body as the hero base** — `HeroBodySwapper` deliberately
   skips `hero/base/HumanMale` for the Knight (`HeroBodySwapper.cs:74-75`).
3. **Re-running `BlinkArmorHumanoidFixer` as a "fix"** for anything current — it solves
   a problem only the junked path has.
4. **Any direct runtime reference to `Assets/Blink/...`** — gitignored; breaks fresh
   clone/CI/WebGL. UI art goes through the RpgUi importers into committed Resources;
   gear/bodies go through `BlinkAddressableMarker` addresses. (Canon §5 of
   `docs/UI_BLINK_TEMPLATE_CANON.md`.)
5. **UI Toolkit/UXML for Blink UI** — settled landmine; the pack is uGUI sprites and
   the project is code-built uGUI only.
6. Note the boundary: `BlinkWardrobe` prefix-toggle dressing is still LIVE for
   dressable NPC bodies — junking applies to the hero pipeline, not to NPC dressing.

---

## 6. Executive summary

The `Assets/Blink` folder is not one pack — it is an eleven-product family from the
Asset Store publisher "Blink" (the RPG Builder studio), all bought on 2026-06-15, tied
together by their RPG Art Ultimate Bundle. On disk it holds four hundred low-poly
weapons in sixteen families, roughly two hundred ninety modular human character and
armor prefabs across two quality tiers, a complete orc NPC bundle, the OBSIDIAN UI kit
with twenty-two panel artworks and nearly sixty assembled interface prefabs plus four
licensed fonts, six hundred spell and class icons, and around nine gigabytes of biome
texture sets. The whole folder is gitignored, so nothing may reference it directly; the
project's two sanctioned pipelines are editor importers that mirror the used interface
art into committed Resources, and an Addressables marker that publishes gear prefabs
under stable addresses.

Two Blink products are load-bearing canon today. First, the OBSIDIAN UI kit is the
single source of every screen's chrome: the master factory in ElarionUiKit renders the
mirrored Blink frame sprites and hands screens pre-measured drop-zones, with the
mirrored assembled widgets tried first and procedural fallbacks kept everywhere.
Second, the weapon pack is the primary gear source: the catalog generator stamps every
weapon row with an Addressables address, and the equipment controller loads, seats
(trusting the pack's grip-at-origin authoring), and releases those prefabs at runtime.
By contrast, the character and armor rigs are formally junked for the hero — the swap
machinery survives behind a feature flag that defaults off, and the Knight deliberately
never loads the Blink base body — though the wardrobe dressing capability remains live
for modular NPC bodies.

The research surfaced two substantial unexploited holdings. The Stylized Orcs Bundle is
a professionally rigged, fully animated enemy family — Unity Humanoid rig, twenty-two
clips per archetype, ready animator controllers — that sat untouched through the entire
Tripo orc rigging struggle. The five hundred spell icons with class emblems and themed
action-bar slots are an equally untouched visual identity kit that maps naturally onto
the talent tree and ability bar, reachable through the importer pipeline that already
exists. Smaller gaps: the full-screen interface prefabs await their planned second
mirroring pass, the dungeon and necromancer texture sets fit the dungeon composer
direction, and about fifteen additional armor sets are claimable for free but were
never downloaded. The do-not-reuse boundary is written out in section 5.6 so the junked
hero-armor path stays junked.
