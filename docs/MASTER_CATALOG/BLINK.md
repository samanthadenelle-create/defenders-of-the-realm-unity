# MASTER CATALOG — `Assets/Blink` (the "Blink" pack)

> SME writeup, verified from the actual files (code/.asset/.prefab/.meta/.gitignore), not comments.
> Date: 2026-06-27. Every claim cites `file:line` or a verified on-disk path.
> Cross-checked against canon: `docs/BLINK_NOTES.md`, `docs/BLINK_UI.md`, `FeatureFlags.cs`, project memory.

---

## What Blink is

**Blink is a large third-party Asset-Store ART MEGA-BUNDLE** (Blink Studios / "Spark Framework"
RPG Art ULTIMATE Bundle), NOT a UI-toolkit framework and NOT our code. It is the project's single
**largest gear + character-art warehouse**: ~16-category low-poly weapon pack, ~290 full-body
character armor/outfit SETS (Male+Female), thousands of class/ability icons, a stylized terrain-texture
library, NPC/enemy demo bodies, and a sprite-based "**Obsidian**" UI skin (panels/buttons/bars/fonts).
It ships only **7 `.cs` files** (a UI-reskin EDITOR tool + a `StatBar` demo + 2 weapon material helpers) —
everything else is art assets. The pack is **gitignored in its entirety** (`.gitignore:256` `/Assets/Blink/`;
`git ls-files Assets/Blink` → 0 tracked). Our project consumes it as a local "warehouse" via **Addressables**
(gear/hero body) and via **editor importers that copy the used slice into `Resources/`** (UI sprites) —
never by direct reference to gitignored assets in committed scenes.

Pack vendor confirmed by `Assets/Blink/UltimateBundle/README.txt` (Asset-Store "claim your packs" bundle)
and `docs/BLINK_NOTES.md:3` ("Blink RPG Art Bundle").

---

## Directory map (counts by file type)

Root: `Assets/Blink/`. Total ~9,580 files. File-type counts (verified via `find`):

| Type | Count | Notes |
|---|---:|---|
| `.meta` | 4,970 | one per asset |
| `.png` | 2,203 | icons, UI sprites, texture maps |
| `.prefab` | 780 | weapons, armor sets, customization, UI prefabs |
| `.fbx` | 760 | weapon/armor/character meshes |
| `.mat` | 390 | URP-ish low-poly materials |
| `.tga` | 160 | textures |
| `.terrainlayer` | 149 | stylized terrain layers |
| `.asset` | 66 | UIToolbox templates, TMP font assets, PP profiles, lighting |
| `.unity` | 28 | demo scenes |
| `.exr`/`.psd`/`.ttf` | 19/18/11 | HDRIs, source art, fonts |
| `.cs` | **7** | see Code Architecture |
| `.controller` | 5 | demo animator controllers |
| `.unitypackage` | 5 | nested installers |
| `.guiskin` | 1 | editor skin for the reskin tool |

Top-level tree (depth 2):

```
Assets/Blink/
├─ Art/
│  ├─ Animations/        Combat, Gathering, Movement clip sets
│  ├─ Characters/        Stylized + LowPoly (Humans, ArmorPacks, DEMO chars)
│  ├─ Icons/             Classes/, Emblems/, Extra/, SourceFiles/ (PSD), Demo/  (~thousands of PNG)
│  ├─ NPCs/Stylized/     Demo_NPCs (Demo_Orcs, ...)
│  ├─ Textures/          Stylized{Desert,Dungeon,Egypt,Forest,Ice,Lava,Necromancer,Viking}, RealisticIce
│  ├─ UI/                Obsidian_UI (the skin), Free_Blink_Icons, _DEMO_UIPacks, UI_Scripts (StatBar.cs)
│  └─ Weapons/LowPoly/MegaWeaponPack1/   400 weapon prefabs (16 cat × 25) + ~405 source FBX
├─ StylizedArmorBundle2/  README.txt only (installer stub)
├─ UltimateBundle/        README.txt only (installer stub)
└─ Tools/UIReSkinner/     the "UI Toolbox" reskin EDITOR tool (Editor/, Scripts/, EditorUI/, Resources/)
```

**Obsidian_UI** (the UI skin) sub-folders: `Buttons_`, `Cursors_`, `Decoration_`, `Elements_`, `Fonts_`,
`HUD_`, `Icons_`, `Integrations_` (RPGBuilder templates), `Panels_`, `Prefabs_`, `Shapes_`, `Slots_`.
The canonical "Blink panel / chrome" lives here: `Assets/Blink/Art/UI/Obsidian_UI/Panels_Obsidian/` +
`Buttons_Obsidian/` (9-sliceable PNG sprites — this is what our `ff.blinkchrome` path shows through).

---

## Code architecture (7 `.cs` files — Assembly-CSharp, namespace `BLINK.*`)

There is **NO `.asmdef` anywhere under `Assets/Blink`** (verified). All Blink scripts compile into the
default **`Assembly-CSharp`** — they are NOT in our `DeNelle.*` assemblies. There are **no `.uxml`/`.uss`**
files in the pack (verified) — the "UI Toolbox" is an IMGUI editor tool + ScriptableObject templates, not UI-Toolkit.

| File | Namespace | Type kind | Role |
|---|---|---|---|
| `Tools/UIReSkinner/Editor/Blink_UI_ReSkinner.cs` | `BLINK.UIToolbox` | `EditorWindow` (`Blink_UI_ReSkinner`) | Editor tool, menu `BLINK/UI ReSkinner` (`:28`); loads `Resources/EditorData/UIToolboxEditorSkin` guiskin (`:47`); bulk-applies `UIPanelTemplate`s onto scene UI GameObjects. |
| `Tools/UIReSkinner/Scripts/UIPanelTemplate.cs` | `BLINK.UIToolbox` | `ScriptableObject` | `[CreateAssetMenu "BLINK/UI Toolbox/UI Element Template"]` (`:7`); holds `panelName` + `List<UIElementTemplate>` (`:10-11`). |
| `Tools/UIReSkinner/Scripts/UIElementTemplate.cs` | `BLINK.UIToolbox` | `ScriptableObject` | One reskinnable UI element's full field set (sprite/color/font/rect/transition `EntryField`s, `:18-65`). |
| `Tools/UIReSkinner/Scripts/UIToolboxData.cs` | `BLINK.UIToolbox` | plain `[Serializable]` data (`UIEntry`, `EntryField`, `EntryType` enum, `TransitionData`, `PanelElement`, `PanelTemplateDATA`) | Data records the reskinner serializes (`:8-141`). |
| `Art/UI/UI_Scripts/StatBar.cs` | `BLINK.UI` | `MonoBehaviour` (`StatBar`) | DEMO only — ping-pongs an `Image.fillAmount` (`:25-31`). Cosmetic filler bar. |
| `Art/Weapons/LowPoly/MegaWeaponPack1/Editor/MaterialTilingOffset.cs` | (weapon-pack util) | editor helper | Material tiling/offset helper for the weapon pack (duplicated below). |
| `Art/Weapons/LowPoly/MegaWeaponPack1/Scripts_MWP1/MaterialTilingOffset.cs` | (weapon-pack util) | helper | Same helper, runtime copy. |

**Entry points / facades:** the only meaningful entry point is the editor `Blink_UI_ReSkinner` window
(`BLINK/UI ReSkinner` menu). **None of these 7 scripts are referenced by our `DeNelle.*` code** — our
integration is to the ART (prefabs/sprites/fbx/textures), never to Blink's scripts. `StatBar` is demo-only.

**Class graph (Blink's own):** `Blink_UI_ReSkinner` (window) → operates on `UIPanelTemplate` SOs → each
holds `UIElementTemplate` SOs → both share the `EntryField`/`EntryType`/`UIEntry` data vocabulary in
`UIToolboxData.cs`. Self-contained; no edges into our code.

---

## Assets & data

- **Weapons** — `Art/Weapons/LowPoly/MegaWeaponPack1/_Prefabs_MWP1/` (verified present): 400 prefabs,
  16 categories × 25 (Axe1h/2h, Sword1h/2h, Dagger1h, Bow2h, Crossbow2h, Shield1h, Mace1h, Polearm2h,
  Scythe2h, Hammer2h, Staff2h, Wand1h, SpellBook1h, Claws1h) per `docs/BLINK_NOTES.md:15-19`.
- **Armor / outfit SETS** — `Art/Characters/LowPoly/Humans_LowPoly/ArmorPacks/Prefabs/` (verified) +
  `Art/Characters/Stylized/...` — ~290 full-body Male/Female outfit-set prefabs (Cloth/Leather/Plate/named
  sets). Modeled in our item system as `Gear`, `slot = Body`, full-body (`docs/BLINK_NOTES.md:21-27`).
- **Playable base body** — `Art/Characters/LowPoly/Humans_LowPoly/Prefabs_Humans/HumanMale_Character.prefab`
  (verified present). This is the Blink LowPoly humanoid rig the armor sets share. (A second
  `Stylized/Humans/Prefabs_Humans/HumanMale_Character.prefab` also exists — the marker uses the **LowPoly** one.)
- **Obsidian UI sprites** — `Art/UI/Obsidian_UI/{Panels,Buttons,Elements,Slots,HUD,Decoration}_Obsidian/*.png`
  (9-sliceable chrome). TMP font assets under `Fonts_Obsidian/{Acme,Alata,Titillium}/*.asset`.
- **UI Toolbox SO templates** (data, not ours) — `Obsidian_UI/Integrations_Obsidian/RPGBuilder_Obsidian/
  UIToolboxTemplates_Obsidian/...` (`HUD_Main.asset`, `Inventory.asset`, `Merchant.asset`, etc. — these are
  RPGBuilder-integration templates, irrelevant to our codebase).
- **Stylized terrain textures** — `Art/Textures/Stylized*` (149 terrainlayers, 160 tga). **Note: at least one is
  load-bearing** — `Assets/Resources/Arena/Grass_1.mat.meta:14` points `assetPath:
  Assets/Blink/Art/Textures/StylizedForestTextures/Grass_1/Grass_1.mat` (the Arena grass material references a
  Blink texture).
- **Icons** — thousands of class/ability/emblem PNGs under `Art/Icons/` (largest file group).
- `StylizedArmorBundle2/` and `UltimateBundle/` contain ONLY `README.txt` installer stubs (no assets).

---

## Integration surface — how OUR code touches Blink

Our `DeNelle.*` code references the Blink ART (never Blink scripts). Two committed bridges convert the
gitignored warehouse into something the build can load: **Addressables** (gear + hero body) and **editor
importers that copy the used slice into `Resources/`** (UI sprites). Key call sites:

| Our file:line | What it does |
|---|---|
| `Assets/Editor/Catalog/BlinkAddressableMarker.cs:44-69` | Editor util `MarkBlinkGear()` — marks Blink weapon prefabs (`_Prefabs_MWP1`), armor-set prefabs (`ArmorPacks/Prefabs`), and the base body (`HumanMale_Character.prefab`) Addressable under group `Gear` with scheme `gear/weapon/*`, `gear/armor/<set>_<gender>`, `hero/base/HumanMale`. Idempotent + guarded for the gitignored-absent case. |
| `Assets/Editor/BlinkUiImporter.cs:26-45` | Editor util `Run()` (menu `Defenders > Art > Import Blink UI Pack`) — mirrors the Obsidian sprite slice from `Assets/Blink/Art/UI/Obsidian_UI` INTO `Assets/Resources/RpgUi/<role>/<canonical>.png` (committed), so the sprite-first kit re-skins. PROOF slice = panels+buttons. |
| `Assets/Editor/BlinkArmorHumanoidFixer.cs:35,64` | Editor util — finds Blink `t:Model` FBX under `Assets/Blink` and fixes Humanoid avatar import settings. |
| `Assets/Editor/Catalog/GearCatalogGenerator.cs:568-717` | `BlinkGearSource` `IGearSource` — enumerates Blink weapon/armor prefabs (via `BlinkAddressableMarker.Enumerate*`) into the gear catalog JSON; addresses match the marker's scheme. |
| `Assets/Editor/Catalog/GearIconRenderer.cs:66,267-270` | Renders gear icons by loading the Blink prefab from its GUID via the `Gear` Addressables group. |
| `Assets/_Modules/Village/Hero/HeroBodySwapper.cs:43,80-81,87-172` | RUNTIME — loads the Blink base body Addressable `hero/base/HumanMale`, builds the hero body via `VisualFactory.Skin`, post-wires. **BUT `:73-78` Knight SKIPS the Blink base entirely** and routes to the legacy Tripo `Resources/Heroes/Knight.fbx` body. |
| `Assets/_Modules/Village/BlinkWardrobe.cs:31` | RUNTIME — `BlinkWardrobe` (our class, named for the Blink modular body) — rig-level Dressable capability keyed on Blink outfit-set renderer names (`Starter_*`/`Cloth*_*`). |
| `Assets/_Modules/Village/Hero/HeroArmorVisual.cs` | RUNTIME — would render equipped Blink armor sets on the body (gated by `ff.blinkarmor`, default OFF — inert). |
| `Assets/_Modules/Village/Hero/GearCatalog.cs:99-100,176-177` | Stamps `loadVia = addressable` on every Blink gear row. |
| `Assets/_Modules/Village/Hero/EquipmentPanel.cs:113-114,475-600` | `ff.blinkchrome`-gated dressing of equip-panel slots with Blink Obsidian slot plates. |
| `Assets/_Modules/Core/UI/ElarionUiKit.cs:154-205,1045-1063` | `ff.blinkchrome`-gated — neutralizes our chrome so the Blink Obsidian panel sprite shows through. |
| `Assets/_Modules/Core/FeatureFlags.cs:42-88,300-314` | Defines `BlinkArmor` + `BlinkChrome` flags (see below). |
| `Assets/Editor/Regression/DataRegression.cs:755` | Regression loads the Blink-marked Addressable gear entries to validate the catalog. |
| `Assets/Resources/Arena/Grass_1.mat.meta:14` | Arena grass material references a Blink StylizedForest texture (art dependency). |

**Systems that depend on Blink:** (1) **Gear/item catalog** (weapons + armor sets — the primary feed,
`ITEM_MODEL`); (2) **Hero body / wardrobe** (base body + outfit-set dressing — though Knight bypasses it);
(3) **UI skin** (`ff.blinkchrome` Obsidian panels via the importer mirror); (4) **Arena terrain art** (grass texture).

---

## FeatureFlags (the two Blink gates)

Both default **OFF** (`Assets/_Modules/Core/FeatureFlags.cs`):

- **`ff.blinkarmor`** → `BlinkArmor` (`:47`), `defaultOn:false`. When OFF, `HeroArmorVisual` is **inert** —
  no addressable armored-body swap, no rig bone-mapping (the bone-map spam `"ShareBaseSkeleton FAILED"` is
  suppressed). Header `:42-46`: "**PIVOT (owner 2026-06-22): Blink armor is JUNKED.**" Flip ON via PlayerPrefs.
- **`ff.blinkchrome`** → `BlinkChrome` (`:88`), `defaultOn:false`. When ON, OUR decorative chrome is hidden
  so the Blink Obsidian panel sprite + content show clean (`:83-87`). Editor toggle menu
  `Defenders/Debug/Blink Chrome (hide our UI dressing)` (`:300-314`). Default OFF = our chrome shows.

(`ff.knightonly` `:53` `defaultOn:true` and `ff.singlehero` `:40` `defaultOn:true` are not Blink flags but
gate whether the Blink base-body runtime path is reachable — see Live vs Dead.)

---

## Live vs Dead

**LIVE / load-bearing now:**
- **Gear catalog feed** — Blink weapons + armor sets are the primary gear source via `BlinkAddressableMarker`
  + `BlinkGearSource` → gear JSON + Addressables `Gear` group (editor-time generation; regression-validated
  `DataRegression.cs:755`). This is the most load-bearing use.
- **`BlinkWardrobe`** runtime dressing capability (`BlinkWardrobe.cs`) — active for dressable humanoid bodies
  (companions/arena fighters), keyed on Blink outfit-renderer names.
- **Arena grass texture** (`Resources/Arena/Grass_1.mat` → Blink StylizedForest) — a live art dependency.
- **Obsidian UI re-skin pipeline** EXISTS and works (`BlinkUiImporter` mirrors sprites into committed
  `Resources/RpgUi`), but is **inactive by default** (`ff.blinkchrome` OFF; the chrome path only swaps look).

**DEAD / dormant / superseded:**
- **Blink ARMOR swap** — JUNKED by the 2026-06-22 single-Knight pivot (`FeatureFlags.cs:42`; memory
  `blink-canonical-art-foundation` REVERSED). `ff.blinkarmor` default OFF → `HeroArmorVisual` inert. The
  owner disliked the look + it spammed bone-map errors.
- **Blink playable HERO base body** — for the SHIPPING hero this path is effectively DEAD: with
  `ff.knightonly` ON (default) the only playable class is Knight, and `HeroBodySwapper.cs:73-78` makes
  **Knight SKIP the Blink base** and use the legacy Tripo `Resources/Heroes/Knight.fbx`. The Blink-base
  Addressable load (`hero/base/HumanMale`) only runs for non-Knight classes (gated off today). The hero
  is now ONE Tripo self-rigged model (memory `combat-pivot-single-hero-northstar`).
- **Blink's own 7 scripts** — not referenced by our game; `StatBar` + the UIReSkinner tool + weapon
  `MaterialTilingOffset` are dormant pack scaffolding. The UIReSkinner / RPGBuilder UIToolbox templates are
  unused (we keep our own code-built UI).
- **UXML/UI-Toolkit path** — N/A: the pack ships NO uxml/uss, and canon (`docs/BLINK_UI.md:9`) is explicit:
  do NOT move to UI-Toolkit; "UXML does not survive WebGL builds here." We use Blink **sprites** on code-built panels.

---

## Risks / drift / open questions

1. **Gitignored — absent on fresh clone.** `.gitignore:256` excludes all of `Assets/Blink/`. Every consumer
   is guarded (`BlinkAddressableMarker` LogWarnings if roots missing; `HeroBodySwapper` falls back to legacy
   Resources body; `BlinkUiImporter` skips missing sprites). A fresh clone has NO Blink art until the owner
   re-imports the purchased packs. Committed `Resources/RpgUi` mirrors + Addressable GUID refs persist, but
   the underlying assets won't resolve. **Top risk: anyone editing Blink wiring must re-acquire the pack.**
2. **Bone-map spam is gated, not removed.** `ShareBaseSkeleton FAILED` was the reason armor was junked
   (`FeatureFlags.cs:43`). It only returns if `ff.blinkarmor` is flipped ON. Do NOT flip it without expecting spam.
3. **Two `HumanMale_Character.prefab` exist** (LowPoly + Stylized). `BlinkAddressableMarker.cs:59-60` targets
   the **LowPoly** one; both are present so no current break, but mismarking would swap the rig.
4. **Stale canon, banner-flagged correctly.** `docs/BLINK_NOTES.md:1` carries the `⚠ STALE` banner (predates
   the single-Knight pivot — Blink-hero/party-of-4 framing superseded). The gear-warehouse + Addressables facts
   in it remain TRUE; the hero/armor framing is dead. `docs/BLINK_UI.md` (2026-06-17) is still architecturally
   accurate (sprite re-skin, no UXML) but the re-skin is dormant by default.
5. **Addressables build dependency.** Gear/hero load via the `Gear` Addressables group; a build that didn't
   include the gitignored Blink assets in the Addressables content will 404 those addresses at runtime
   (HeroBodySwapper handles the base-body 404 with a legacy fallback; gear icons would be blank).
6. **Open question:** is the dormant `ff.blinkchrome` Obsidian re-skin still a desired V1 look, or has the
   Tripo-direction art pivot also retired the Obsidian UI theme? Canon doesn't say; verify with owner before
   investing more in the UI re-skin slice.

---

## Obsidian UI — as-designed usage (verified) — knowledge capture for WO-542 (not implemented)

This section absorbs an external (Grok) research note on the Obsidian UI sub-asset and reconciles it
against THIS project's ACTUAL wiring. Knowledge-capture only — no tool is being built. Every claim is
sourced to a real file / filename. **Headline correction: the runtime load key is `RpgUi/<role>/<name>`
via `RpgUiCatalog.Get`, NOT `Resources.Load("Icons_Obsidian/...")`** — the Blink pack is gitignored and
lives outside `Resources`, so it is not `Resources.Load`-able at its pack path.

### Real folder + sprite inventory (verified on-disk)

The Obsidian skin = `Assets/Blink/Art/UI/Obsidian_UI/`. Verified sub-folders & counts (`find`, non-meta):

- **`Icons_Obsidian/`** — 72 PNGs. Named glyphs CONFIRMED present: `Health_Potion.png`, `Mana_Potion.png`,
  `Poison_Elixir.png`, `Helmet.png`, `Sword.png`, `Sword_1.png`, `icon-spellbook.png`, `bag-icon.png`,
  `inventory-icon.png`, `Copper_Currency.png`, `Silver_Currency.png`, `Gold_Currency.png`, `Copper_Ore.png`,
  `Silver_Ore.png`, `Gold_Ore.png`, `Iron_Bar_1.png`, `Iron_Bar_2.png`, `Fiber.png`, `Rune2.png`/`Rune_2.png`/
  `Rune_3.png`, `Human_Race.png`/`Elf_Race.png`/`Orc_Race.png`, `Icon-Male.png`/`Icon-Female.png`,
  plus action/menu glyphs (`crafting-icon`, `loot-icon`, `quest-icon`, `questlog-icon`, `pet-icon`,
  `settings-icon`, `socketing-icon`, `stat-icon`, `talent-icon`, `characterslot-icon`) and 34 generic
  `icon1..icon34.png` ability glyphs.
- **`HUD_Obsidian/`** — 50 PNGs: bars (`Health_Bar`, `Mana_Bar`, `Energy_Bar`, `STamina_Bar` [sic],
  `Stat_Bar_Background`, `Stat_Bar_White`, `Stat_Orb_Diablo`, `hud-xpbar`, `Cast_Bar_1..3`+`Castt_Bar_Fill`),
  nameplates (`Nameplate_*`, `Party_Nameplate`), cores (`HIUD_Core` [sic], `HUD_Diablo-Core`, `Target_Core`,
  `Chat_Core`), `Potrait_Border` [sic], `Quest_Tracker(_BAR)`, `Diablo_Art_1..3`. (Note the misspelled
  source filenames — copy them verbatim.)
- **`Panels_Obsidian/`** — 22 PNGs (the 9-slice windows): `Core_Panel`, `Core_2_Panel`, `Merchant_Panel`,
  `Inventory_Panel`, `Crafting_Panel`, `Loot_Panel`, `Quest_Panel`, `Quest_Log_Panel`, `Stats_Panel`,
  `Options_Panel`, `Settings_Panel`, `Pet_Panel`, `Dialogue_Panel`/`Dialogue_2_Panel`, **`Talent_Tree_Panel`**,
  `Panel_Element`, `Text_Background`, `{Male,Female,Pet}_Silouhette*` [sic].
- **`Slots_Obsidian/`** — 18 PNGs: `Inventory_Slot`, `Armor_Slot(_2)`, `Character_Slot`, `Action_Bar_Slot`,
  `Socketing_Slot(_2)`, `Rarity_1..5`, **`Talent_Border_1..6`** (the skill-node frames).
- Also: `Buttons_Obsidian/` (Button1..5 × Gray/Green/Red/Yellow, `Close_Button_*`, sliders, toggles),
  `Decoration_`, `Elements_`, `Shapes_`, `Cursors_`, `Fonts_Obsidian/` (Acme/Alata/Titillium TMP assets),
  `Integrations_Obsidian/` (RPGBuilder templates — irrelevant to us).

### The CORRECT runtime load path (Grok's key is wrong-for-us)

The pack is gitignored + outside `Resources` → NOT `Resources.Load`-able at `Icons_Obsidian/...`. Our real path:

1. **Editor mirror:** `DeNelle.Editor.BlinkUiImporter.Run()` (`Assets/Editor/BlinkUiImporter.cs:38-58`,
   menu `Defenders > Art > Import Blink UI Pack`) `CopyAsset`s the USED Obsidian slice into committed
   `Assets/Resources/RpgUi/<role>/<canonical>.png` (`ResRoot = "Assets/Resources/RpgUi"`, `:29`), forcing
   Sprite import + the 9-slice border.
2. **Runtime accessor:** `DeNelle.Core.UI.RpgUiCatalog` (`Assets/_Modules/Core/UI/RpgUiCatalog.cs`) —
   `ResRoot = "RpgUi/"` (`:43`); lazily `Resources.LoadAll<Sprite>("RpgUi/<role>")`. Public API:
   `Sprite Get(string role)` (`:114`), `Sprite Get(string role, string spriteName)` (`:126`),
   `bool TryGet(...)` (`:136`), `IReadOnlyList<Sprite> All(role)` (`:143`). Role constants `RolePanel`/
   `RoleButton`/`RoleIcons`/`RoleSlot`/`RoleBars`/`RolePotion`/`RoleBadge` (`:52-58`).
   **So the real key is `RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, "icon_sword")` → loads `RpgUi/icons/icon_sword`.**
3. The canonical NAMES are OUR ids (`panel_vendor`, `button_gold`, `icon_sword`, `slot_item`…), set by the
   importer's Obsidian→canonical map (`BlinkUiImporter.cs:62-91`), NOT the raw Obsidian filenames.

### Real 9-slice border data (replaces Grok's guessed "20-30")

Source Obsidian `.meta` files carry `spriteBorder {0,0,0,0}` (verified — no non-zero borders in the pack).
The **importer assigns** the 9-slice border on copy via `ForceSprite(border)` (`BlinkUiImporter.cs:93-113`,
sets `spriteBorder = (b,b,b,b)` + `SpriteMeshType.FullRect`). Actual values used (`:65-85`):

| Role / sprite | Border (px/side) |
|---|---|
| Panels (`panel_vendor`, `panel_window`, `panel_grid`, `panel_inventory`, `panel_quest`, `panel_portrait`, `panel_window_dark`) | **48** |
| `panel_bar` | 32 |
| Buttons (`button_frame`, `button_gold`, `button_confirm`), `panel_tab`, `slot_item` | 24 |
| `button_exit` (Close_Button) | 12 |
| Icons (whole glyphs) | **0** (no 9-slice; `preserveAspect`) |

### ElarionUiKit helpers to use (do NOT hand-roll 9-slice)

`Assets/_Modules/Core/UI/ElarionUiKit.cs` already builds sprite-first, 9-sliced UI over dark glass:

- `PanelFramed(parent, anchorMin, anchorMax, ...)` (`:958`) — RolePanel frame over glass, sprite-first +
  procedural fallback. The "apply an Obsidian 9-slice panel" primitive Grok sketched ALREADY EXISTS here.
- `Panel` (`:134`), `Well` (`:144`), `Niche` (`:152`), `Slot(parent, rarityIndex, …)` (`:397`),
  `Card(parent, rarityIndex, name, icon, …)` (`:463`), `TechGearSocket` (`:338`), bar builder (`:733`).
- Sliced application is internal: `img.type = Image.Type.Sliced` (`:307,:375,:973,:1004,:1039`); icons use
  `preserveAspect = true` (`:811`). Sprites pulled via `RpgUiCatalog.Get(RolePanel/RoleBars/RoleIcons,…)`
  (`:368,:733,:859`). **`ff.blinkchrome` (default OFF) gates whether our chrome is hidden so the Obsidian
  panel sprite reads clean** (`:154-205,1045-1063`; see FeatureFlags section).

So the as-designed View pattern = build with `ElarionUiKit.PanelFramed/Slot/Card` + feed `RpgUiCatalog`
ids; the kit applies the Obsidian sprite + 9-slice for you. No `ObsidianUiHelper` needs to be written —
`RpgUiCatalog` + `ElarionUiKit` ARE that helper.

### Icon → game-concept mapping (corrected to real filenames + real ids)

Grok's concept mappings are mostly sound, but the load is two-hop (Obsidian filename → imported canonical id):

| Game concept | Obsidian source file (`Icons_Obsidian/…`) | Canonical id today |
|---|---|---|
| Health potion / Mending Salve | `Health_Potion.png` | (potion role: `potion_health`, sourced from the RPG pack today — Obsidian `Health_Potion` not yet mirrored) |
| Mana / Poison | `Mana_Potion.png` / `Poison_Elixir.png` | `potion_mana` / `potion_fire` (RPG-pack art currently) |
| Spell / skill | `icon-spellbook.png` | (not yet mirrored — candidate for skill nodes) |
| Weapon / melee | `Sword.png`, `Sword_1.png` | `icon_sword` (mirrored from `Sword.png`, `BlinkUiImporter.cs:85`) |
| Armor / helmet | `Helmet.png` | (not yet mirrored) |
| Inventory / bag | `inventory-icon.png` / `bag-icon.png` | `icon_inventory` (from `inventory-icon.png`, `:87`) |
| Quest | `quest-icon.png` | `icon_quest` (`:88`) |
| Settings | `settings-icon.png` | `icon_settings` (`:86`) |
| Currency | `Copper_/Silver_/Gold_Currency.png` | (not yet mirrored) |

**Only a SUBSET is mirrored today** — panels (9), buttons (4), slot (1), and 4 icons (`icon_sword`,
`icon_settings`, `icon_inventory`, `icon_quest`). The other ~68 icons + all HUD bars are NOT yet in
`Resources/RpgUi` (importer comment defers bars: `BlinkUiImporter.cs:89-90`). Extending the map = adding
rows to `BuildTable()` then re-running the importer.

### Skill-tree node pattern (already implemented — without Obsidian sprites)

Grok's Locked/Owned/Equipped node pattern ALREADY EXISTS as a built MVVM screen:
`HeroSkillTreePanelMvvm` (View, `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs`) binds
`HeroSkillTreeVM` (`HeroSkillTreeVM.cs`); loadout via `GearLoadout.cs` + `HeroLoadoutVM.cs`. Verified node
states (`HeroSkillTreePanelMvvm.cs:299-348`): **Owned → green plate + "Owned"/Gilt** (`:301,:332-335`);
**unlockable → gold plate + "<n> Wisdom"/Affordable** (`:339-340`); **Locked → dim plate + LockReason/Danger**
(`:345-346`); **Equipped → chip** (`:324-325`). It is built from `ElarionUiKit` color states, **NOT** the
Blink `Talent_Border_1..6.png` / `Talent_Tree_Panel.png` sprites — those Obsidian assets exist (`Slots_`/
`Panels_Obsidian`) but are unused. Skinning the skill tree Obsidian = mirror `Talent_Tree_Panel` +
`Talent_Border_*` into `RpgUi/panel`+`RpgUi/slot` and point the View's plates at them.

### Grok-said-X / reality-is-Y reconciliation

| Grok claim | Reality (this project) | Source |
|---|---|---|
| Load via `Resources.Load<Sprite>("Icons_Obsidian/Health_Potion")` | WRONG-for-us — pack is gitignored/outside Resources. Use `RpgUiCatalog.Get(role, canonicalId)` → `RpgUi/<role>/<name>`. | `RpgUiCatalog.cs:43,114-143`; `.gitignore:256` |
| Best practice = build a "ObsidianIcons" Sprite Atlas | We don't; the importer copies individual PNGs into `Resources/RpgUi` + `LoadAll<Sprite>` per role folder. No atlas. | `BlinkUiImporter.cs:47-52`; `RpgUiCatalog.cs` |
| 9-slice borders ~20-30/side | Source metas are 0; importer assigns 48 (panels) / 32 / 24 / 12. | `BlinkUiImporter.cs:65-85,104-110` |
| Write `ObsidianUiHelper.GetIcon/ApplyObsidianPanel` | Already exists: `RpgUiCatalog.Get` + `ElarionUiKit.PanelFramed/Slot/Card`. Don't duplicate. | `ElarionUiKit.cs:958,397,463`; `RpgUiCatalog.cs:126` |
| Skill tree: 9-slice node bg + Locked/Owned/Equipped(gold rim) states | Pattern already built in `HeroSkillTreePanelMvvm` via color plates (Owned green / unlock gold / locked dim / equipped chip); Obsidian `Talent_Border_*` sprites exist but unused. | `HeroSkillTreePanelMvvm.cs:299-348` |
| Icon filenames (Health_Potion, Helmet, icon-spellbook, bag-icon, Copper_Currency) | CONFIRMED present in `Icons_Obsidian/` (verbatim). | on-disk `Icons_Obsidian/` |
| HUD_Obsidian = panel bg/frames/buttons | Partly — HUD_Obsidian is bars/nameplates/cores; PANELS live in `Panels_Obsidian/`, buttons in `Buttons_Obsidian/`. | on-disk folders |
| MVVM: View = dumb skin, VM owns data (`HeroSkillTreeVM`, `GearLoadout`) | CONFIRMED — those exact classes exist; View never reads game state. | `Talents/HeroSkillTreeVM.cs`, `Hero/GearLoadout.cs` |
| Asset = "Obsidian UI" Asset-Store #206302, dark/gothic 9-slice + icons | Plausible (matches the sprite set); the store ID is NOT verifiable from the files — FLAGGED unverified. | — |

**Unverifiable (flagged):** the Asset-Store ID #206302, the "64×64/128×128 designed size" claim (not
checked against texture import sizes), and any "designed" atlas-name "ObsidianIcons" — none appear in our files.
