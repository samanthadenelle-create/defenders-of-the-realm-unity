# Grok-02 — Obsidian UI Guidance (Blink Studios, tight lens)

**Status:** LIVING guidance — owner / CLI implementation reference  
**Author:** Grok (SME pass) · **Date:** 2026-07-14  
**Series:** `Grok-02` = Blink **Obsidian UI only** (not weapons / orcs / armor bodies / texture packs)  
**Companion:** `docs/vfx/Grok-01-VFX-guidance.md` (Hovl combat VFX)  
**Implements / tracks via:** WO-714 Obsidian conformance (in flight / waves) · factory API in `ElarionUiKit*` · template canon below  

**Sources (binding):**
- `docs/UI_BLINK_TEMPLATE_CANON.md` — **THE formula** (owner-ratified)
- `docs/SME/BLINK_SME.md` §1.4 / §2.2 — pack inventory + how we consume UI
- `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md` — factory API, fill contract, hostile/friendly trees
- `docs/UI_MVVM_BINDING_MAP.md` — View never pulls game state
- `docs/BLINK_UI.md` — reskin path + masking-fill gotcha (partially historical)
- Pack showroom (gitignored source): `Assets/Blink/Art/UI/Obsidian_UI/_DEMO_UIPacks/OBSIDIAN_DEMO.unity`
- Publisher: [blinkstudios.dev](https://blinkstudios.dev) · docs hub [blink.developerhub.io](https://blink.developerhub.io/) · product **OBSIDIAN UI** (store id 206302)

> **Tight lens:** this file is only about **chrome, frames, slots, buttons, bars, fonts, and the factory recipe**.  
> Blink weapons, spell icons, stylized orcs, armor modular bodies, and biome textures are **out of scope** (see BLINK SME).  
> If this doc fights `UI_BLINK_TEMPLATE_CANON.md`, **the template canon wins**.  
> If this doc fights code, **code + newest RESULT win** for *what ships*; canon wins for *what right looks like*.

---

## 0. TL;DR

| Question | Answer |
|---|---|
| What is “Blink UI” for us? | **Obsidian UI** sprites + optional mirrored prefab params — **not** RPG Builder, not UXML |
| One-line law? | **The Blink frame IS the chrome. Screens drop chrome-less content into drop-zones and bind a VM.** |
| Runtime stack? | **Code-built uGUI only** → `ElarionUiKit` + `RpgUiCatalog` + committed `Resources/RpgUi/**` |
| Source pack path? | `Assets/Blink/Art/UI/Obsidian_UI` (**gitignored**) → mirrored by importers |
| Gold rule? | **Gold = accents & content only** (currency, rarity, highlights) — not chrome outline |
| Bar law? | Fill `Image` **always has a sprite** + `Filled` + only `fillAmount` (no sprite-less Filled) |
| Close law? | **One** 3-state Close construction; every panel uses it |
| Wallet law? | **One** `CurrencyChip` / `BuildWalletRow` — no hand-rolled currency strings |
| Fallback? | Sprite-first; null art → procedural black/steel — **never blank**, never primary look |
| Do NOT? | UI Toolkit / UXML at runtime · reference `Assets/Blink/**` directly · per-screen chrome |

---

## 1. Product reality (what Obsidian *is*)

Blink’s **OBSIDIAN UI** pack is a full **2D uGUI skin**:

| Folder (under `Obsidian_UI`) | Role | How we use it |
|---|---|---|
| **Panels_Obsidian** (~22) | Full ornate frames (Inventory, Crafting, Merchant, Dialogue, Quest, Settings, Talent_Tree, Core…) | Mirrored → `Resources/RpgUi/frame/frame_*` · factory `BuildObsidianPanel(frameName)` |
| **Slots_Obsidian** (~18) | Inventory / armor / action / character / rarity / socket | `Resources/RpgUi/slot/*` · grids never bare quads |
| **Buttons_Obsidian** (~42) | Button1–5 × Gray/Green/Red/Yellow + Close/Toggle/Slider/Dropdown | `BuildObsidianButton` family |
| **HUD_Obsidian** (~50) | HP/MP/cast bars, nameplates, target core, chat, quest tracker | `RoleHud` · `BuildObsidianBar` / nameplates |
| **Elements_Obsidian** (~31) | Stat plates, tabs, notifications, scroll bits | chips, tabs, toasts |
| **Icons_Obsidian** (~71) | Generic UI icons | **Low priority** — **our game icons stay ours** (owner mandate) |
| **Fonts_Obsidian** | Acme, Alata, Merriweather, Titillium | TMP SDF → `font_title` / `font_body` / `font_stamp` |
| **Prefabs_Obsidian** (~58 assembled) | HUDCore, Merchant, Inventory, CastBars, nameplates… | **Parameter source of truth** (measure hierarchy) — prefer kit assembly over raw Instantiate of pack GUIDs |
| **OBSIDIAN_DEMO.unity** | Full showroom | Felt A/B gold standard |

**Style language (owner):** dark **forged steel on smoky obsidian** — depth from embossing / rivets / texture. **Gold is reserved for accents and content**, never as the primary chrome border. Procedural flat-black + gold trim is **fallback only** — if a screen reads “unstyled,” the real frame sprite is missing or masked.

---

## 2. How WE consume it (pipeline — never break this)

```
Assets/Blink/Art/UI/Obsidian_UI/     ← gitignored, local only
        │
        ▼  Defenders/Art/Import RPG UI Pack   (RpgUiImporter — frames/slots/silhouettes)
        ▼  Defenders/Art/Import Blink UI Pack (BlinkUiImporter — hud/element/button/panel)
        ▼  BlinkFontImporter / BlinkPrefabMirror (fonts + prefab mirror / params)
        │
Assets/Resources/RpgUi/{frame,slot,button,hud,element,font,prefabs,...}   ← COMMITTED
        │
        ▼  RpgUiCatalog.Get(role, name)  →  null-safe
        ▼  ElarionUiKit.BuildObsidian*   →  sprite-first + procedural fallback
        ▼  Screen View binds VM only
```

| Rule | Why |
|---|---|
| **Never** `Resources.Load` or hard path into `Assets/Blink/**` | Fresh clone / CI / WebGL has no pack |
| **Always** sprite-first with null fallback | Absent art must not blank a panel |
| **CopyAsset** = new GUIDs into Resources | No pack GUID deps in shipped builds |
| Source filenames keep Blink typos; **canonical names are clean** | e.g. `Castt_Bar_Fill` → `bar_cast_fill` |

**Importers (regenerate, don’t hand-copy PNGs):**
- `Assets/Editor/RpgUiImporter.cs` — frames, silhouettes, core slots  
- `Assets/Editor/BlinkUiImporter.cs` — hud / element / button bulk  
- `Assets/Editor/BlinkFontImporter.cs` — Merriweather / Alata / Acme → TMP  
- `Assets/Editor/BlinkPrefabMirror.cs` — assembled prefabs → `RpgUi/prefabs/` (GUID-safe)

---

## 3. The master formula (binding)

From `UI_BLINK_TEMPLATE_CANON.md`:

```csharp
var chrome = ElarionUiKit.BuildObsidianPanel(parent, "CRAFTING", min, max, OnClose,
                 frameName: RpgUiCatalog.FrameCrafting);
// chrome.layout.header | body | medallion | footer | close (when measured)
BuildChromeLessContent(chrome.layout.body);   // lists, slots, text — NO second frame
BindViewModel(chrome.layout);                 // VM only — no EconomyService.Instance in View
```

### 3.1 Drop-zones

| Zone | Content |
|---|---|
| `header` | Title (kit may pre-build) |
| `body` | Main well — grid / list / detail |
| `medallion` | Portrait / paper-doll socket (when frame has one) |
| `footer` | Wallet chips / primary actions |
| `close` | Measured notch when art provides it (e.g. Stats top-right) |

**Tune layout only in `ZonesFor(frameName)`** — never per-screen fraction hacks that fight the frame border.

### 3.2 Frame → screen defaults (data table, not code switches)

| Surface | Frame id | Pack art (concept) |
|---|---|---|
| Inventory | `FrameInventory` | Inventory_Panel |
| Character / gear paper-doll | `FrameCharacter` / stats | Stats_Panel |
| Crafting / jeweler / alchemy | `FrameCrafting` | Crafting_Panel |
| Talent / skill tree | `FrameTalent` | Talent_Tree_Panel |
| Shop / vendor | `FrameMerchant` | Merchant_Panel |
| Dialogue | `FrameDialogue` (+ `_2`) | Dialogue panels |
| Quest / codex / lists | `FrameQuest` | Quest_Log_Panel |
| Settings | `FrameSettings` | Settings |
| Options / pause chrome | `FrameOptions` | Options |
| Loot | `FrameLoot` | Loot |
| Pet | `FramePet` | Pet |
| Generic / end-state | `FrameCore` / `FrameCore_2` | Core panels |

Frames are **data-agnostic containers** — any frame can host any VM shape; defaults are convention, not hard-welded types.

### 3.3 MVVM (presentation law)

1. **View never reads game state** (no `EconomyService.Instance`, no catalog pull for logic).  
2. **ViewModel never references** `GameObject` / `Image` / `Sprite` / `RectTransform`.  
3. **Same VM → any skin** (our kit panel today; mirrored prefab binder tomorrow).  

Repeating unit (Blink + ours): **frame → header/wallet/close → LayoutGroup of slots**, each slot = icon + name + cost/meta.

---

## 4. Factory API — use these, don’t invent widgets

All live under `Assets/_Modules/Core/UI/` (`ElarionUiKit`, `ElarionUiKitObsidian`, `ElarionUiKitConformance`, `RpgUiCatalog`).

| Builder | Use for | Critical contract |
|---|---|---|
| `BuildObsidianPanel` / `BuildObsidianModal` | Every modal / screen | Real `frameName`; content in zones |
| `BuildObsidianButton` (Style × Color) | All CTAs | 5×4 family; text meaning, not color alone |
| `ObsidianCloseButton` / close zone | Every dismiss | **One** 3-state close art |
| `BuildObsidianBar` | HP/MP/cast/XP/heart | **Fill sprite non-null**; only `fillAmount` |
| `CurrencyChip` / `BuildWalletRow` | Every wallet | Gold primary; CompactNumber; **no ellipsis** |
| `BuildTab` / `BuildTabRow` | Category tabs | Underline / selected plate — not hue alone |
| `BuildActionSlot` | Ability / hotbar / consumable | `slot_action` + cooldown ring |
| `BuildToast` / `ShowToast` | Feedback | Shared notif plates |
| `RaritySlot` / sparse grid | Inventory / loot | Empty slots still draw slot art |
| `BuildNameplate` / `BuildTargetFrame` | Combat/world plates | `Clear()` empties **all** fields |
| `EnsureFont(role)` | All text | title / body / stamp roles |

### 4.1 The fill-binding contract (game law)

Broken path class: **sprite-less `Image.Type.Filled`** → uGUI paints a full quad → HP looks full at 9/145.

1. Fill always has a non-null sprite (`bar_stat_fill` or bar art).  
2. `type = Filled`, horizontal, origin left.  
3. **Only** mutate `fillAmount = cur / max` — never width via `sizeDelta`.  
4. Drive from VM events, not per-frame service pulls when possible.

### 4.2 Style bar (owner)

| Do | Don’t |
|---|---|
| Real frame/slot/button sprites | Procedural black box as the *primary* look |
| Gold on currency, rarity, armed state | Gold outline as default chrome |
| Steel depth from pack embossing | Per-screen `Color(...)` chrome hacks |
| ASCII-safe TMP labels | Non-ASCII glyphs (tofu □ on device) |
| Meaning by icon + text + position | Meaning by red vs green alone (colorblind) |

### 4.3 WO-714 kit primitives (conformance wave)

When polishing screens, prefer already-landed kit pieces:

- `BuildTabRow` · `BuildWalletRow` · `RaritySlot` / sparse grids · `ShowToast` · font floors · open/close FX  
- Screens still hand-rolling tabs/wallets/slots = **non-conformant** until swapped  

---

## 5. Recommended Obsidian usage by surface (tight map)

| Surface | Frame / chrome | Content builders | Notes |
|---|---|---|---|
| **Inventory** | `FrameInventory` | slot grid + rarity frames | Empty slots still show `slot_item` |
| **Character / equip** | `FrameCharacter` + silhouette | armor/character slots + detail | Medallion = portrait |
| **Crafting / jeweler** | `FrameCrafting` | recipe list + detail card | Detail = parchment / `frame_textbg` in body, not second outer frame |
| **Shop / PackStore** | `FrameMerchant` | article slots + wallet footer | Merchant grammar; no hue-only affordability |
| **Quest / guide** | `FrameQuest` | master-detail list | Readable body type (Alata) |
| **Talent tree** | `FrameTalent` | talent slots | Tree content chrome-less inside body |
| **Dialogue** | `FrameDialogue` | speaker header + body + medallion | **Reference impl:** `DialogueView` |
| **Settings / Pause** | `FrameSettings` / Options | toggles/sliders from kit | Code-built only (UXML deleted) |
| **Build palette** | *In-world strip* (not always full frame) | `BuildTabRow` Town/Defenses · slot cards · wallet | See build-mode analysis; still kit buttons/slots |
| **HUD vitals** | HUD sprites, not modal frames | `BuildObsidianBar` + nameplates | Hostile tree high / friendly tree low (HUD arch) |
| **Ability bar** | — | `BuildActionSlot` | Our ability icons, Obsidian slot frame |
| **End state / wave report** | `FrameCore` or end template | sprite-first rows + wallet | Repair CTAs preserved |

**Icons:** keep **game** icons (`HudIcons`, ability art, currency glyphs). Do not bulk-replace with `Icons_Obsidian` unless a specific glyph is missing and colorblind-safe.

---

## 6. Common failure modes (and the fix)

| Symptom | Root class | Fix |
|---|---|---|
| Panel “looks unstyled” / flat black+gold | Procedural fallback or **opaque solid fill masking** the frame | Use real `frameName`; kill/mask SolidFill alpha (BLINK_UI gotcha) |
| Shop/inventory “same as before” | Fill inset over frame art | Alpha 0 on decorative fills that cover sprites |
| HP bar full at low HP | Sprite-less Filled | Factory bar contract |
| Done / Close untappable | HUD overlapping build / wrong raycast | PanelManager + hide combat HUD in build; one close button |
| Tofu □ glyphs | Non-ASCII in TMP | ASCII labels only |
| Currency clipped / “…” | Hand-rolled formatters | `CompactNumber` + `CurrencyChip` (no ellipsis) |
| Double frame | Content builder draws its own panel | Transparent host inside `layout.body` |
| WebGL blank UI | UIDocument / UXML | **Forbidden** — code-built uGUI only |
| Fresh clone missing art | Referenced `Assets/Blink` | Only `Resources/RpgUi` |

---

## 7. Verify method (owner law)

1. Build / restyle screen via kit.  
2. Capture with **graphics-enabled** player (fleet `-nographics` is blank).  
3. Compare to **Blink template PNG** under `Obsidian_UI/Panels_Obsidian/` (or OBSIDIAN_DEMO).  
4. Tune **zones once** in `ZonesFor` — not per screen.  
5. Regression: `UiObsidianConformanceRegression` when available; UI pair sheet `UI_REVIEW/INDEX.html` for WO-714.

---

## 8. Explicit non-goals (this Grok-02 lens)

| Out of scope | Why / where it lives |
|---|---|
| Blink **armor body swap** / modular hero dress | JUNKED for hero (`ff.blinkarmor` OFF) — BLINK SME §2.4 |
| Blink **weapons** as Addressables gear | Separate gear lane — not Obsidian UI |
| **Stylized Orcs** / NPC pack activation | Character packs SME — not UI |
| **500 spell icons** bulk import | Unused; ability icons are ours |
| **9 GB biome textures** | Environment — not UI |
| Adopting full **MerchantPanel.prefab** by raw GUID | Prefer kit + params extract; no pack GUID in builds |
| **UI Toolkit** reskins | Runtime landmine |
| Rebuilding presentation without factory | Violates template canon + HUD arch |

---

## 9. Implementation priority (if greening new work)

1. **Any new screen:** `BuildObsidianPanel` + VM + zones — zero new chrome systems.  
2. **Any bar:** factory fill contract only.  
3. **Any wallet:** `BuildWalletRow` / `CurrencyChip` only.  
4. **Any tabs:** `BuildTabRow` only.  
5. **Conform remaining screens** (WO-714): swap hand-rolled widgets → kit; image-pair until match.  
6. **Build HUD** (separate feel lane): still kit tabs/slots/wallet — optional full frame later; don’t invent a second design system.  
7. **Importer gaps:** only when a needed sprite is missing from `Resources/RpgUi` — add to importer table, regen, commit.

---

## 10. Related files index

| Path | Role |
|---|---|
| **`docs/UI/Grok-02-Obsidian-UI-guidance.md`** | **This file** — tight Obsidian lens |
| `docs/UI_BLINK_TEMPLATE_CANON.md` | BINDING one-line formula + zones |
| `docs/SME/BLINK_SME.md` | Full Blink pack SME (UI section §1.4/§2.2) |
| `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md` | Factory API depth + HUD trees |
| `docs/UI_MVVM_BINDING_MAP.md` | VM wire harness |
| `docs/BLINK_UI.md` | Early reskin notes + fill mask |
| `docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` | Skill tree / inventory design depth |
| `Assets/_Modules/Core/UI/ElarionUiKit*.cs` | Factory implementation |
| `Assets/_Modules/Core/UI/RpgUiCatalog.cs` | Role/name constants |
| `Assets/Resources/RpgUi/**` | Committed mirrored art |
| `Assets/Editor/RpgUiImporter.cs` · `BlinkUiImporter.cs` | Mirror pipeline |
| `WorkOrders` WO-714* | Conformance program |

---

## 11. One-screen recipe checklist

- [ ] `BuildObsidianPanel` / Modal with correct `frameName`  
- [ ] Content parented to `layout.body` (header/footer/medallion as appropriate)  
- [ ] No second chrome frame inside body  
- [ ] Buttons via `BuildObsidianButton`; Close via kit close  
- [ ] Wallet via chips; bars via fill contract  
- [ ] Slots via `RpgUiCatalog` slot art (empty still drawn)  
- [ ] View binds VM only  
- [ ] ASCII labels; no color-only meaning  
- [ ] Screenshot vs OBSIDIAN_DEMO / panel PNG  
- [ ] Blink-absent / missing sprite still falls back (no blank)

---

*Grok-02 — update in place when frame catalog or factory API gains widgets; keep UI_BLINK_TEMPLATE_CANON as the constitutional one-liner.*
