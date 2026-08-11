// =============================================================================
// InventoryUIBuilder — UI construction for the inventory modal (split from HeroInventoryController).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Extracted exactly from the original for behavior preservation. All construction
// (root, chrome, tabs, footer, shared helpers) lives here. The controller coordinates
// by calling these. Light polish: consistent ElarionUiKit dark-wood + gold (Forge shop
// look) via the kit; Tech pack overrides for W/A where present in original.
// No functionality, layout or behavior changes.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Hero;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        // --- ROOT + CHROME (moved from original BuildRoot) ---
        private void BuildRoot()
        {
            // Route the canvas through the kit's ONE standard modal canvas builder (same
            // 1080x1920 reference / 0.5 match / 31000 band the other Obsidian modals use)
            // instead of hand-rolling it, so this modal matches the rest. overrideSorting
            // (not set by the kit) is applied after, preserving the prior behaviour.
            _ui = ElarionUiKit.BuildModalCanvas("HeroInventoryUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;

            ElarionUiKit.Scrim(_ui.transform, Close);

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header +
            // the ONE standard Close button. Replaces the old backdrop + brown PanelFramed +
            // dark solidFill + ember glow + rune strip + per-panel "X".
            // Cosmetic flag B (owner 2026-07-24): the medallion "bag" glyph is REMOVED — the
            // hero portrait is seated into the socket below instead (no medallionIcon here).
            var panelChrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "INVENTORY",
                new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                Close, headerX0: 0.05f, headerX1: 0.80f,
                frameName: RpgUiCatalog.FrameInventory);
            var panel = panelChrome.content;

            // Cosmetic flag B — drop the ACTIVE hero's portrait into the medallion socket.
            // FIX 2026-08-05: this hardcoded "HeroPortraits/Grom", so a player who picked Sylas or
            // Thrain still saw Grom's face in his own inventory. The slug now comes from the
            // persisted class through the TWO maps that already exist — PlayableHeroes.JobKey
            // (Core: class -> "ranger") then PortraitSlug (InventoryPaperDoll.cs: "ranger" ->
            // "Sylas"). Deliberately NOT a third copy of the map: duplicated slug tables are exactly
            // how the portrait drifted from the hero in the first place.
            // The portraits are imported as plain Texture2D (spriteMode:0), so they MUST load as
            // Texture2D into a RawImage, not as a Sprite (mirrors HeroSelectController.ApplyPortrait).
            // preserveAspect=false via a full stretch fills the oval. FALLBACK: an ASCII name label
            // if the texture is missing (zero-risk floor). Canon note: Resources/HeroPortraits/ DOES
            // exist (Grom/Elara/Thrain/Sylas .jpg) — any stale doc claiming otherwise is wrong.
            var medallion = panelChrome.layout != null ? panelChrome.layout.medallion : null;
            if (medallion != null)
            {
                string portraitSlug = ActiveHeroPortraitSlug();
                var portraitTex = Resources.Load<Texture2D>("HeroPortraits/" + portraitSlug);
                if (portraitTex != null)
                {
                    var raw = new GameObject("HeroPortrait", typeof(RawImage));
                    var rrt = raw.GetComponent<RectTransform>();
                    rrt.SetParent(medallion, false);
                    rrt.anchorMin = Vector2.zero;
                    rrt.anchorMax = Vector2.one;
                    rrt.offsetMin = Vector2.zero;
                    rrt.offsetMax = Vector2.zero;
                    var rawImg = raw.GetComponent<RawImage>();
                    rawImg.texture = portraitTex;
                    rawImg.color = Color.white;   // preserveAspect=false: fill the oval
                    rawImg.raycastTarget = false;
                }
                else
                {
                    var nameLbl = ElarionUiKit.Label(medallion, portraitSlug.ToUpperInvariant(), 0f, 1f,
                        ElarionUi.Gilt, 40, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                    nameLbl.raycastTarget = false;
                }
            }

            // Left: Narrow portrait area to match mockup exactly - ornate gold frame with hero portrait, Lvl, name, colored bars, stats.
            // Matches the mockup's left panel width and style.
            // Eyes-sweep 2026-07-06 rule 2: all content columns end ABOVE the shared bottom-centre
            // Close band (SeatSharedCloseInside seats a fixed 360x120 box there) — bottom 0.12 -> 0.165.
            var niche = ElarionUiKit.Niche(panel.transform, new Vector2(0.04f, 0.165f), new Vector2(0.26f, 0.78f));
            _paperDoll = AddImage(niche.transform, "PaperDollArea", new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.99f), new Color(0, 0, 0, 0));
            NoRaycast(_paperDoll);

            // Tabs above the grid on the right, gold-trimmed to match mockup.
            _tabsRoot = AddImage(panel.transform, "TabsRow", new Vector2(0.28f, 0.82f), new Vector2(0.95f, 0.90f), new Color(0, 0, 0, 0));
            NoRaycast(_tabsRoot);
            BuildTabs(_tabsRoot.transform);

            // Right: Large grid filling most of the screen (5 cols landscape, ornate frames from pack, items fit with margins, scroll for more).
            // No sidebar/detail panel in main layout to match the mockup's full grid view (selection highlights in grid, equip on tap).
            // Grid extends lower for the big area in the mockup.
            // WO-582 frame pass: the grid sits in the Blink frame's own dark well now, so the grid
            // root is TRANSPARENT (no grey Well sub-frame, no old PanelInventory gold-grid sprite that
            // double-framed the middle). Items render directly on the frame's central well.
            // Eyes-sweep 2026-07-06: the right column re-stacked so NOTHING sits under the shared
            // bottom-centre Close band — grid 0.30–0.80, detail strip 0.240–0.295, footer 0.165–0.235.
            _gridRoot = AddImage(panel.transform, "GridRoot",
                                 new Vector2(0.30f, 0.30f), new Vector2(0.93f, 0.80f), new Color(0f, 0f, 0f, 0f));
            NoRaycast(_gridRoot);

            // WO-585 — selection DETAIL strip: a thin host between the grid bottom (0.16) and the
            // footer top (0.10). Empty/transparent until an item is tapped; RebuildSidebar then drops
            // the selected item's name + stats + an explicit Equip/Use CTA + the equip Status line
            // into it, so a tap has a visible, separate-from-equip response (was the inert feel).
            // (was 0.103–0.156 — the "Tap an item to inspect it." bar painted over the shared Close)
            _sidebarRoot = AddImage(panel.transform, "DetailStrip",
                                    new Vector2(0.30f, 0.240f), new Vector2(0.93f, 0.295f), new Color(0f, 0f, 0f, 0f));
            NoRaycast(_sidebarRoot);

            BuildFooterBar(panel.transform);

            // WO-713 — the ONE shared open ease (kit PanelOpenCloseFx, WO-714 P8): scale
            // target = the chrome panel rect (never the overlay canvas root). Attach-only
            // when inactive (headless-safe); Close stays instant (controller destroys _ui).
            if (panelChrome.root != null)
                ElarionUiKit.AttachPanelOpenFx(_ui, panelChrome.root.GetComponent<RectTransform>());

            // Modal arbiter registration: THIS file builds the top-band (31000) inventory modal, so
            // it routes the panel through the single-modal PanelManager arbiter here (back-button /
            // battle-lock / one-modal-at-a-time). Shared _panelHandle with HeroInventoryController's
            // Open()/Close(); the calls are idempotent (Register is guarded by the null check,
            // NotifyOpened/NotifyClosed no-op when already applied). The close delegate releases the
            // arbiter slot and then hides the modal via Close().
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Inventory",
                    () => { if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle); Close(); },
                    () => IsOpen);
            PanelManager.NotifyOpened(_panelHandle);
        }

        // ── Footer bar (mockup #41 bottom) ---
        private void BuildFooterBar(Transform panel)
        {
            // WO-582 frame pass: the footer sits on the Blink frame's ornate base now, so the tray is
            // a transparent layout host (no Track fill / rim / rule that boxed the bottom over the art).
            // Eyes-sweep 2026-07-06 rule 2 (was 0.035–0.100): the wallet tray now ends above the
            // shared bottom-centre Close band instead of sitting underneath it.
            var tray = AddImage(panel, "FooterTray",
                                new Vector2(0.30f, 0.165f), new Vector2(0.93f, 0.235f), new Color(0f, 0f, 0f, 0f));
            NoRaycast(tray);

            // WO-1015 E1: the dev-gated "Orient" button that used to seat here is REMOVED (see the
            // note at BuildGenericWalletChip's sibling comment below). The freed left of the footer
            // tray simply stays clear; the wallet wells keep their right-aligned positions.

            // WO-565: the Sort + Filter buttons were wired to EMPTY lambdas — visible controls
            // that silently did nothing. HIDDEN rather than ship a half-feature: category Filter
            // is already provided by the tab row (Weapons/Armor/Accessories/Consumables), and a
            // real Sort needs VM-level ordering + grid re-bind (non-trivial). Re-add here only
            // once InventoryVM exposes a sort/filter the grid can project. The wallet wells below
            // keep their right-aligned positions; the freed left of the footer simply stays clear.

            // Wallet balances come from the bound VM (InventoryVM.Coins/Crystals, sourced from the
            // injected IEconomy which reads GameState.Resources) — this View never reads
            // GameStateService directly (strict-MVVM). Null-safe: a missing VM shows 0/0.
            int coins    = _vm != null ? _vm.Coins : 0;
            int crystals = _vm != null ? _vm.Crystals : 0;

            // WO-713 A.6 + the appended owner ruling (2026-07-13): the footer is the STANDARD
            // kit chip row (CurrencyChip owns ALL currency presentation — CompactNumber,
            // icon-first identity, no flash), never hand-rolled wells. Gold + Crystals ride
            // the ONE wallet strip (WO-714 P2 BuildWalletRow); the third chip is the GENERIC
            // WALLET — icon + plain amount, NO Pi/SKR symbol. CurrencySkinResolver.Active
            // still drives the wallet chip's identity text (never a symbol typed inline), so
            // the Pi/SKR skins render correctly when the later crypto arc re-activates them.
            var chipHost = AddImage(tray.transform, "WalletChips",
                                    new Vector2(0.40f, 0.10f), new Vector2(0.985f, 0.90f),
                                    new Color(0f, 0f, 0f, 0f));
            NoRaycast(chipHost);
            var softHost = AddImage(chipHost.transform, "SoftCurrency",
                                    new Vector2(0f, 0f), new Vector2(0.64f, 1f),
                                    new Color(0f, 0f, 0f, 0f));
            NoRaycast(softHost);
            var chips = ElarionUiKit.BuildWalletRow(softHost.transform,
                new[] { ElarionUiKit.CurrencyKind.Gold, ElarionUiKit.CurrencyKind.Crystal });
            if (chips != null && chips.Length > 0 && chips[0] != null) chips[0].SetAmount(coins, animate: false);
            if (chips != null && chips.Length > 1 && chips[1] != null) chips[1].SetAmount(crystals, animate: false);

            BuildGenericWalletChip(chipHost.transform);
        }

        // The premium/wallet chip — GENERIC under the V1 "wallet" skin (owner ruling appended
        // to WO-713: "remove the Pi symbol on inventory screen ... leave generic as wallet").
        // Built on the SAME kit CurrencyChip as the soft currencies (no hand-rolled well),
        // then re-iconed to the wallet/bag art: icon + plain amount, zero symbol glyphs.
        // Identity text comes from the active skin's CurrencyName (colorblind law: when no
        // icon art resolves the chip still carries a text identifier, never a naked number).
        private void BuildGenericWalletChip(Transform host)
        {
            var skin = DeNelle.Core.Platform.CurrencySkinResolver.Active;
            string tagText = (string.IsNullOrEmpty(skin.CurrencyName) ? "Wallet" : skin.CurrencyName)
                             .ToUpperInvariant();
            var chip = ElarionUiKit.CurrencyChip(host, ElarionUiKit.CurrencyKind.Gold,
                new Vector2(0.66f, 0f), new Vector2(1f, 1f), primary: false, tag: tagText);
            if (chip == null || chip.root == null) return;
            chip.root.name = "WalletChip";

            var bag = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconInventory);
            if (bag != null && chip.icon != null)
            {
                // Wallet ICON (the bronze chest/bag) replaces the kind icon — generic read.
                chip.icon.sprite = bag;
                chip.icon.gameObject.SetActive(true);
                if (chip.tag != null) chip.tag.text = tagText;
            }
            else
            {
                // No wallet art: never let the GOLD kind icon mislabel the wallet — drop the
                // icon and make sure a text identifier carries the chip's identity instead.
                if (chip.icon != null) chip.icon.gameObject.SetActive(false);
                if (chip.tag != null) chip.tag.text = tagText;
                else
                {
                    var t = ElarionUiKit.Label(chip.root.transform, tagText, 0f, 1f,
                        ElarionUi.Parchment, ElarionUi.FontMicro,
                        TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.58f);
                    t.raycastTarget = false;
                    ElarionUiKit.FitSingleLine(t, 0f, ElarionUi.FontMicro);
                }
            }

            // V1 ships zero crypto and no local premium-balance model exists — the honest 0.
            chip.SetAmount(0, animate: false);
        }

        // ── WO-1015 E1: the in-panel "Orient" word-button is GONE from this screen ──────────
        // Same strand removed from EquipmentPanel and (by WO-1010 D1) from BuildPaletteUI. It was
        // dev-gated, which is NOT the same as invisible — the owner's felt-test builds are
        // development builds, so it rendered on every screen she looked at and overlapped the
        // content it was pinned beside. The seating editor keeps its ONE sanctioned entry point,
        // AdminOverlay (Assets/_Modules/HUD/AdminOverlay.cs:361 "Orient Asset" / :608 "Seating
        // Editor") — a dev screen, not a gameplay screen. Do not re-add a per-screen launcher.

        // ── TABS ---
        // WO-713 A.2 — the ONE uniform kit tab row (WO-714 P1 BuildTabRow): element_tab
        // plates, one size class, labels renamed so they FIT (owner spec): Weapons / Armor /
        // Trinkets / Potions / Skills (was "Accessories"/"Consumables" — the truncating pair).
        // Selected state = the kit's lit plate + bold, never color-only. "Skills" stays the
        // pseudo-tab: it isn't a content category — tapping it OPENS the MVVM skill tree via
        // PanelRouter (PanelManager swaps the inventory out, one-modal-at-a-time). The row is
        // rebuilt from the VM's active tab on every Render (RebuildTabsRow), so the VM stays
        // the source of truth for selection.
        // WO-911 (ruling Q10+Q13, 2026-08-06) adds a SIXTH entry: "Map". The Realm Map left the
        // bottom action bar (that is half of how the bar went 7 -> 6 faces) and lives here now.
        // Like "Skills" it is a PSEUDO-tab — not a content category, it just routes out via
        // PanelRouter — so it needs no InventoryTabKind and no VM change.
        //
        // ⚠ FEATURE-FLAGGED OFF (owner 2026-08-06, "we can ff map for now since those dont
        // connect right?"). RealmMapPanel's own header records travel as a DISABLED STUB until
        // WO-827: the realm's areas do not connect, so a visible tab would promise a journey the
        // game cannot take. When the flag is off the tab is ABSENT, never a greyed dead entry —
        // ElarionUiKit.BuildTabRow divides the width by the tab COUNT, so the remaining five
        // reflow with no gap. PlayerPrefs "ff.maptab" = 1 turns it back on with no rebuild.
        private void BuildTabs(Transform host)
        {
            bool mapTab = DeNelle.Core.FeatureFlags.MapTab;

            var labelList = new System.Collections.Generic.List<string>
                { "Weapons", "Armor", "Trinkets", "Potions", "Skills" };
            var tabList = new System.Collections.Generic.List<Tab>
                { Tab.Weapons, Tab.Armor, Tab.Outfits, Tab.Consumables, Tab.Weapons };
            const int skillsIndex = 4;
            int mapIndex = -1;

            if (mapTab)
            {
                mapIndex = labelList.Count;
                labelList.Add("Map");
                tabList.Add(Tab.Weapons);       // never selected; the route fires first
            }
            else
            {
                // §12: the absence must be READABLE in a capture, or the next person hunts a
                // vanished tab instead of finding the flag that hid it.
                FlowTrace.Step("UI",
                    "Bag: Map tab SUPPRESSED by FeatureFlags.MapTab (default OFF — realm travel is a " +
                    "WO-827 stub). Tab row reflows to " + labelList.Count + " tabs with no gap.");
            }

            int captureMapIndex = mapIndex;
            ElarionUiKit.BuildTabRow(host, labelList.ToArray(),
                idx =>
                {
                    if (idx == skillsIndex) { OpenSkillTree(); return; }
                    if (captureMapIndex >= 0 && idx == captureMapIndex) { OpenRealmMap(); return; }
                    SelectTab(tabList[idx]);
                },
                initial: (int)_tab);
        }

        // The "Map" pseudo-tab (WO-911): open the Realm Map parchment overworld. Routes through
        // PanelRouter so the inventory needs NO reference to RealmMapPanel; PanelManager swaps this
        // modal out (one-panel-at-a-time). Never a dead-end tap — an unregistered opener closes the
        // bag and reports, exactly like the Skills route above.
        private void OpenRealmMap()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.RealmMap))
                return;
            Close();
            FlowTrace.Warn("UI", "Map tab: PanelId.RealmMap has no registered opener — nothing to open.");
        }

        // The "Skills" pseudo-tab: open the code-built MVVM skill tree (HeroSkillTreePanelMvvm).
        // Routes through PanelRouter so the inventory needs NO reference to the Talents panel type;
        // PanelManager swaps this modal out for the skill tree (one-panel-at-a-time). When the
        // panel isn't registered (e.g. no hero), Close the inventory and log — never silently nothing.
        private void OpenSkillTree()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.HeroSkillTree))
                return;
            // Not registered — close the inventory so the tap isn't a dead end, and report.
            Close();
            FlowTrace.Warn("UI", "Skills tab: HeroSkillTree panel not registered (no hero?) — nothing to open.");
        }

        // Tapping the hero portrait opens the full Character / Gear Preview paper-doll
        // (EquipmentPanel). Prefer the registered PanelRouter route (PanelManager swaps the
        // inventory out, one-modal-at-a-time). If no panel host exists yet (nothing has opened
        // it this session), lazily create the host — its Awake registers it — then open directly,
        // mirroring the dialogue "OpenEquip" command. Null-safe; never a dead-end tap.
        private void OpenGearPreview()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.EquipmentPanel))
                return;
            var panel = FindAnyObjectByType<DeNelle.Village.Hero.EquipmentPanel>();
            if (panel == null)
                panel = new GameObject("EquipmentPanelHost").AddComponent<DeNelle.Village.Hero.EquipmentPanel>();
            panel.Open();   // NotifyOpened closes this inventory (it is the registered open panel)
        }

        // (TabPackIcon retired with the WO-713 kit tab row — BuildTabRow tabs are label-first;
        //  CreamLabel was already dead. Verified zero remaining references before deletion.)

        // ── Live 3D dressed-hero preview in the paper-doll niche ─────────────────────
        // REUSE (no new system): the SAME proven HeroPreviewViewer the Character / Gear screen
        // (EquipmentPanel.BuildPreviewWidget + BeginOrRetargetPreview) drives — it renders the
        // active hero, with the equipped weapon / shield / armor tier, into a RenderTexture a UI
        // RawImage shows. The viewer is PERSISTED across paper-doll rebuilds (the RT is reused and
        // RefreshGear mirrors equip changes) and disposed on Close (DisposeHeroPreview) so there is
        // no RenderTexture leak. Null-safe per §12: any failure leaves the niche to the 2D portrait/
        // crest fallback — no crash, no error spam (one FlowTrace line, never a silent swallow).
        private DeNelle.Village.Hero.HeroPreviewViewer _heroPreview;
        private RawImage _paperDollPreview;

        // Mount the live hero into <paramref name="parent"/> via a child RawImage. Returns true when
        // a live preview is showing; false when there is no hero body or the viewer can't build — the
        // caller then falls back to the static 2D portrait. Called from RebuildPaperDoll (first call
        // happens on Open via Bind->Render, so the preview begins when the panel opens).
        private bool TryMountHeroPreview(Transform parent)
        {
            if (parent == null) return false;
            try
            {
                var body = ResolvePreviewBody();
                if (body == null)
                {
                    FlowTrace.Step("Inventory", "Paper-doll: no hero body — using 2D portrait fallback.");
                    return false;
                }

                string weaponId  = _loadout != null && _loadout.EquippedWeapon  != null ? _loadout.EquippedWeapon.id  : null;
                string offHandId = _loadout != null && _loadout.EquippedOffHand != null ? _loadout.EquippedOffHand.id : null;
                int    armorTier = _loadout != null ? GearLoadout.ArmorVisualTier(_loadout.EquippedArmor) : 0;

                if (_heroPreview == null)
                {
                    _heroPreview = new DeNelle.Village.Hero.HeroPreviewViewer();
                    if (!_heroPreview.Begin(body, textureSize: 512, weaponId: weaponId,
                                            offHandId: offHandId, armorTier: armorTier))
                    {
                        DisposeHeroPreview();
                        return false;
                    }
                    _heroPreview.SetRotation(18f);   // same 3/4 hero angle as the Gear screen
                }
                else
                {
                    // Persisted across rebuilds: mirror the latest equipped look (weapon+shield+armor),
                    // reusing the existing RenderTexture — no re-Begin, no RT churn.
                    _heroPreview.RefreshGear(weaponId, offHandId, armorTier);
                }

                if (!_heroPreview.IsValid || _heroPreview.Texture == null)
                {
                    DisposeHeroPreview();
                    return false;
                }

                var imgGo = new GameObject("HeroPreviewRawImage", typeof(RectTransform), typeof(RawImage));
                imgGo.transform.SetParent(parent, false);
                var rt = imgGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.05f, 0.05f);
                rt.anchorMax = new Vector2(0.95f, 0.95f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _paperDollPreview = imgGo.GetComponent<RawImage>();
                _paperDollPreview.raycastTarget = false;
                _paperDollPreview.color = Color.white;
                _paperDollPreview.texture = _heroPreview.Texture;
                return true;
            }
            catch (System.Exception ex)
            {
                // No silent failure (§12): log once, drop to the 2D portrait, never crash the modal.
                FlowTrace.Warn("Inventory",
                    $"TryMountHeroPreview threw ({ex.GetType().Name}: {ex.Message}) — using 2D portrait fallback.");
                DisposeHeroPreview();
                return false;
            }
        }

        // The actor body to clone for the preview — mirrors EquipmentPanel.ResolveBody: the live
        // hero's "HeroBody" child (the visual rig) or the tagged root itself, falling back to the
        // resolved loadout's GameObject. Null when no hero is present (caller skips the preview).
        private GameObject ResolvePreviewBody()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) hero = SafeFindByTag("HeroTarget");
            if (hero != null)
            {
                var t = hero.transform.Find("HeroBody");
                if (t != null)
                {
                    var body = t.gameObject;
                    FlowTrace.Step("Preview",
                        $"ResolvePreviewBody: hero='{hero.name}' -> 'HeroBody' child '{body.name}' (children={body.transform.childCount})");
                    return body;
                }
                FlowTrace.Step("Preview",
                    $"ResolvePreviewBody: hero='{hero.name}' had NO 'HeroBody' child -> returning hero root '{hero.name}' (children={hero.transform.childCount})");
                return hero;
            }
            var fallback = _loadout != null ? _loadout.gameObject : null;
            FlowTrace.Step("Preview",
                $"ResolvePreviewBody: no 'Player'/'HeroTarget' hero found -> loadout fallback '{(fallback != null ? fallback.name : "NULL")}'" +
                (fallback != null ? $" (children={fallback.transform.childCount})" : ""));
            return fallback;
        }

        // Free the preview rig + its RenderTexture. Called on Close / OnDestroy (and on any build
        // failure) so the off-screen clone + RT never leak. Safe to call repeatedly.
        private void DisposeHeroPreview()
        {
            _heroPreview?.Dispose();
            _heroPreview = null;
            _paperDollPreview = null;
        }

        // (Shared UI primitives Add*/Dress*/AddCircle*/Rarity*/glyphs/Has/Cap/Hero* live once in the main partial file.
        // High-level builder chrome (root/tabs/footer) live here as the UIBuilder concern.
        // This guarantees single definition for the merged partial class.)
    }
}