// =============================================================================
// HeroTalentPanel - bottom-sheet UIDocument that surfaces the three hero
// talent trees. Bound to the "T" key (toggle) by HeroTalentPanelBootstrap.
// -----------------------------------------------------------------------------
// Cross-module note: DeNelle.HUD does not (and must not) reference
// DeNelle.Village, but the catalog + the WisdomCurrencyService live in
// DeNelle.Village.Talents. We resolve those types by name via reflection -
// the bridge pattern documented in Assets/_Modules/Pets/PetHeroLeash.cs.
//
// Layout: one column per hero. Each column lists three tier rows (top-down
// tier3 -> tier1), each row showing two node cards. Locked-by-prereq nodes
// render dimmed. The Spend button calls WisdomCurrencyService.Unlock(id).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HeroTalentPanel : MonoBehaviour
    {
        // Cached reflection bridges to DeNelle.Village.Talents.* ----------------
        private static Type s_catalogType;
        private static Type s_serviceType;
        private static Type s_nodeType;
        private static Type s_treeType;
        private static MethodInfo s_getTree;
        private static PropertyInfo s_allTrees;
        private static PropertyInfo s_serviceInstance;
        private static PropertyInfo s_wisdomProp;
        private static PropertyInfo s_unlockedProp;
        private static MethodInfo s_unlockMethod;
        private static MethodInfo s_canUnlockMethod;
        private static EventInfo s_changedEvent;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private bool _visible;
        private Delegate _changedHandler;
        private object _serviceInstanceCache;
        // Modal arbiter handle (DEF-212): opening any panel closes the previously
        // open one. Hide() is the close action; _visible is the is-open probe.
        private PanelHandle _panelHandle;

        private static readonly string[] HeroOrder = { "mage", "knight", "ranger" };
        private static readonly string[] TierOrderTopDown = { "tier3", "tier2", "tier1" };

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _panelHandle = PanelManager.Register("Hero Talents", Hide, () => _visible);
            // DEF-213: let building interactions (Arcane Tower) open this panel by id,
            // without a cross-asmdef reference and without reflection.
            PanelRouter.Register(PanelId.HeroTalents, Show);
            ResolveBridges();
            BuildUi();
            Hide();
            SubscribeToService();
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.HeroTalents, Show);
            UnsubscribeFromService();
        }

        // -- Visibility ---------------------------------------------------------

        public void Toggle()
        {
            if (_visible) Hide(); else Show();
        }

        public void Show()
        {
            FlowTrace.Step("HeroTalent", "Show() — opening Hero Talents panel.");
            if (_overlay == null)
            {
                FlowTrace.Fail("HeroTalent",
                    "Show: _overlay is NULL — BuildUi never produced an overlay (no PanelSettings/root?). Show is a no-op.");
                return;
            }
            _visible = true;
            _overlay.style.display = DisplayStyle.Flex;
            // Tell the arbiter we're open; it closes any other panel first.
            PanelManager.NotifyOpened(_panelHandle);
            Repaint();
        }

        public void Hide()
        {
            if (_overlay == null) return;
            _visible = false;
            _overlay.style.display = DisplayStyle.None;
            // Clear our slot. No-op if the manager already swapped us out.
            PanelManager.NotifyClosed(_panelHandle);
        }

        // -- UI build -----------------------------------------------------------

        private void BuildUi()
        {
            using var _ = FlowTrace.Enter("HeroTalent", "BuildUi");
            // V (WO-465 class): a null UIDocument or a UIDocument with no PanelSettings yields a
            // null rootVisualElement — the panel would silently build nothing and never show. The
            // old `if (_root == null) return;` swallowed exactly that. Fail-loud so a run says why.
            if (_doc == null)
            {
                FlowTrace.Fail("HeroTalent", "BuildUi: UIDocument is NULL on this GameObject — talents cannot render.");
                return;
            }
            if (_doc.panelSettings == null)
                FlowTrace.Fail("HeroTalent",
                    "BuildUi: UIDocument has NO PanelSettings — rootVisualElement will be null and the " +
                    "panel will render invisible. Wire a PanelSettings on the HeroTalents UIDocument.");
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                FlowTrace.Fail("HeroTalent",
                    "BuildUi: _doc.rootVisualElement is NULL — Hero Talents UI cannot be built; panel will be empty.");
                return;
            }
            // Owner 2026-05-20: Build button + skillset clicks weren't
            // registering. The agent-spawned panel UIDocs each have a root
            // VE with default PickingMode.Position, which captures clicks
            // even when the inner overlay is display=None — blocking the
            // village HUD beneath. Set root to Ignore; the overlay itself
            // (when visible) still listens because it has its own
            // pickingMode = Position.
            _root.pickingMode = PickingMode.Ignore;

            _overlay = new VisualElement { name = "HeroTalentOverlay" };
            ElarionUi.StyleScrim(_overlay);
            _overlay.pickingMode = PickingMode.Position;
            _overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _overlay) Hide();
            });
            _root.Add(_overlay);

            var panel = new VisualElement { name = "HeroTalentPanel" };
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.paddingTop = 18;
            panel.style.paddingBottom = 18;
            panel.style.paddingLeft = 22;
            panel.style.paddingRight = 22;
            panel.style.minWidth = 880;
            panel.style.maxWidth = 1100;
            // Never let the sheet exceed the viewport — the columns scroll inside
            // it instead of spilling past the screen edges (owner: "talent window
            // extends outside box, needs a scrolling box to stay inside structure").
            panel.style.maxHeight = Length.Percent(90);
            // Elarion stone panel + gold rim (canon).
            ElarionUi.StylePanel(panel, dark: true);
            _overlay.Add(panel);

            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 14;
            panel.Add(headerRow);

            var title = new Label("Hero Talents");
            title.style.color = new StyleColor(ElarionUi.Gilt);
            title.style.fontSize = ElarionUi.FontTitle;
            title.style.letterSpacing = 1.5f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(title);

            _wisdomLabel = new Label("Wisdom: 0");
            _wisdomLabel.style.color = new StyleColor(ElarionUi.Gold);
            _wisdomLabel.style.fontSize = ElarionUi.FontHead;
            _wisdomLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            // DEF-212 item 6: Wisdom has no earn path in the current loop, so a
            // permanent "Wisdom: 0" reads as broken. Hide the counter until heroes
            // actually grant Wisdom (then flip this back on). The label is still
            // built + updated by Repaint so re-enabling is a one-line change.
            _wisdomLabel.style.display = DisplayStyle.None;
            headerRow.Add(_wisdomLabel);

            var closeBtn = new Button(Hide) { text = "Close (T)" };
            ElarionUi.StyleButton(closeBtn, ElarionUi.ButtonKind.Neutral);
            closeBtn.style.minHeight = StyleKeyword.Auto;
            closeBtn.style.paddingTop = 4; closeBtn.style.paddingBottom = 4;
            closeBtn.style.paddingLeft = 12; closeBtn.style.paddingRight = 12;
            headerRow.Add(closeBtn);

            // Thin gilt underline beneath the header (canon).
            panel.Add(ElarionUi.MakeRule());

            // The columns live inside a vertical ScrollView so a tall tree (or a
            // short window) scrolls within the capped panel rather than overflowing
            // it. The header above stays pinned; only the columns scroll.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.flexShrink = 1f;
            scroll.style.marginRight = -6;   // tuck the scrollbar against the rim
            panel.Add(scroll);

            _columnsRow = new VisualElement();
            _columnsRow.style.flexDirection = FlexDirection.Row;
            _columnsRow.style.justifyContent = Justify.SpaceBetween;
            _columnsRow.style.alignItems = Align.FlexStart;
            scroll.Add(_columnsRow);
        }

        private Label _wisdomLabel;
        private VisualElement _columnsRow;

        // -- Repaint ------------------------------------------------------------

        private void Repaint()
        {
            using var _ = FlowTrace.Enter("HeroTalent", "Repaint");
            if (_columnsRow == null)
            {
                FlowTrace.Fail("HeroTalent", "Repaint: _columnsRow is NULL — BuildUi never produced it (no root?). Repaint is a no-op.");
                return;
            }
            _columnsRow.Clear();

            int wisdom = GetWisdom();
            HashSet<string> unlocked = GetUnlockedSet();

            if (_wisdomLabel != null) _wisdomLabel.text = $"Wisdom: {wisdom}";

            int treesResolved = 0;
            foreach (var heroSlug in HeroOrder)
            {
                var tree = GetTree(heroSlug);
                if (tree != null) treesResolved++;
                var col = BuildHeroColumn(heroSlug, tree, wisdom, unlocked);
                _columnsRow.Add(col);
            }
            if (treesResolved == 0)
                FlowTrace.Warn("HeroTalent",
                    $"Repaint: 0 of {HeroOrder.Length} hero talent trees resolved (catalog bridge unavailable/empty) — " +
                    "every column shows the visible '(catalog unavailable)' note.");
        }

        private VisualElement BuildHeroColumn(string heroSlug, object treeObj, int wisdom, HashSet<string> unlocked)
        {
            var col = new VisualElement();
            col.style.flexGrow = 1f;
            col.style.flexShrink = 1f;
            col.style.flexBasis = 0;
            col.style.marginLeft = 6; col.style.marginRight = 6;
            col.style.paddingTop = 10; col.style.paddingBottom = 10;
            col.style.paddingLeft = 10; col.style.paddingRight = 10;
            ElarionUi.StyleWell(col);

            string displayName = ReadString(treeObj, "DisplayName") ?? heroSlug;
            var header = new Label(displayName);
            header.style.color = new StyleColor(ElarionUi.Parchment);
            header.style.fontSize = ElarionUi.FontBody;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.style.marginBottom = 8;
            col.Add(header);

            if (treeObj == null)
            {
                FlowTrace.Warn("HeroTalent",
                    $"BuildHeroColumn: no talent tree for hero '{heroSlug}' (catalog returned null) — " +
                    "showing visible '(catalog unavailable)' note for this column.");
                var missing = new Label("(catalog unavailable)");
                missing.style.color = new StyleColor(ElarionUi.Danger);
                missing.style.fontSize = ElarionUi.FontMicro;
                col.Add(missing);
                return col;
            }

            // Group nodes by tier
            var byTier = new Dictionary<string, List<object>>();
            var nodes = ReadList(treeObj, "Nodes");
            if (nodes != null)
            {
                foreach (var n in nodes)
                {
                    string tier = ReadString(n, "Tier") ?? "tier1";
                    if (!byTier.TryGetValue(tier, out var list))
                    {
                        list = new List<object>();
                        byTier[tier] = list;
                    }
                    list.Add(n);
                }
            }

            foreach (var tierKey in TierOrderTopDown)
            {
                var tierLabel = new Label(TierDisplay(tierKey));
                tierLabel.style.color = new StyleColor(ElarionUi.ParchmentDim);
                tierLabel.style.fontSize = ElarionUi.FontMicro;
                tierLabel.style.marginTop = 8; tierLabel.style.marginBottom = 4;
                tierLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                col.Add(tierLabel);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                col.Add(row);

                if (byTier.TryGetValue(tierKey, out var tierNodes))
                {
                    // Sort columns a, b
                    tierNodes.Sort((x, y) =>
                        string.CompareOrdinal(ReadString(x, "Column") ?? "", ReadString(y, "Column") ?? ""));
                    foreach (var node in tierNodes)
                    {
                        row.Add(BuildNodeCard(node, wisdom, unlocked));
                    }
                }
            }
            return col;
        }

        private VisualElement BuildNodeCard(object node, int wisdom, HashSet<string> unlocked)
        {
            string id = ReadString(node, "Id");
            string name = ReadString(node, "Name") ?? id ?? "?";
            string desc = ReadString(node, "Description") ?? "";
            int cost = ReadInt(node, "Cost");
            // Phase 1 added numeric stat fields to the node DTO; surface them so
            // players see an unlock's concrete effect before buying.
            float damageBonus = ReadFloat(node, "DamageBonus");
            float cdReduction = ReadFloat(node, "CdReduction");
            bool learned = unlocked != null && id != null && unlocked.Contains(id);
            bool canBuy = !learned && CanUnlock(id, wisdom, unlocked);

            var card = new VisualElement();
            card.style.flexGrow = 1f;
            card.style.flexBasis = 0;
            card.style.marginLeft = 4; card.style.marginRight = 4;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.borderTopLeftRadius = 6; card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6; card.style.borderBottomRightRadius = 6;
            card.style.borderTopWidth = 1; card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1; card.style.borderRightWidth = 1;

            Color bg, rim;
            float opacity = 1f;
            if (learned)
            {
                bg = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.16f);
                rim = ElarionUi.Affordable;
            }
            else if (canBuy)
            {
                bg = ElarionUi.PanelStoneDark;
                rim = ElarionUi.Gold;
            }
            else
            {
                bg = ElarionUi.PanelStoneDark;
                rim = ElarionUi.StoneTrim;
                opacity = 0.55f;
            }
            card.style.backgroundColor = new StyleColor(bg);
            card.style.borderTopColor = new StyleColor(rim);
            card.style.borderBottomColor = new StyleColor(rim);
            card.style.borderLeftColor = new StyleColor(rim);
            card.style.borderRightColor = new StyleColor(rim);
            card.style.opacity = opacity;

            var title = new Label(name);
            title.style.color = new StyleColor(ElarionUi.Parchment);
            title.style.fontSize = ElarionUi.FontLabel;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            card.Add(title);

            var body = new Label(desc);
            body.style.color = new StyleColor(ElarionUi.ParchmentDim);
            body.style.fontSize = ElarionUi.FontMicro;
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginTop = 4;
            body.style.marginBottom = 6;
            card.Add(body);

            // Talent impact line — a small bold readout of the node's concrete
            // numbers (DamageBonus 0.10 -> "+10% damage", CdReduction 0.10 ->
            // "-10% cooldown"). Only shown when at least one stat is non-zero, so
            // pure-utility nodes stay clean. Sits between the description and the
            // cost/Spend footer so players read the effect before buying.
            string impact = BuildImpactText(damageBonus, cdReduction);
            if (!string.IsNullOrEmpty(impact))
            {
                var impactLabel = new Label(impact);
                impactLabel.style.color = new StyleColor(ElarionUi.Aether);
                impactLabel.style.fontSize = ElarionUi.FontMicro;
                impactLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                impactLabel.style.whiteSpace = WhiteSpace.Normal;
                impactLabel.style.marginBottom = 6;
                card.Add(impactLabel);
            }

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;
            card.Add(footer);

            var costLabel = new Label(learned ? "Learned" : $"{cost}W");
            costLabel.style.color = new StyleColor(learned ? ElarionUi.Affordable : ElarionUi.Gold);
            costLabel.style.fontSize = ElarionUi.FontMicro;
            costLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            footer.Add(costLabel);

            if (!learned)
            {
                var spend = new Button(() => OnSpend(id)) { text = "Spend" };
                ElarionUi.StyleButton(spend, canBuy ? ElarionUi.ButtonKind.Gold : ElarionUi.ButtonKind.Disabled);
                spend.style.minHeight = StyleKeyword.Auto;
                spend.style.fontSize = ElarionUi.FontMicro;
                spend.style.paddingTop = 2; spend.style.paddingBottom = 2;
                spend.style.paddingLeft = 8; spend.style.paddingRight = 8;
                if (!canBuy) spend.SetEnabled(false);
                footer.Add(spend);
            }
            return card;
        }

        // Formats the node's numeric stat fields into a short impact string.
        // DamageBonus is a positive fraction (0.10 -> "+10% damage"); CdReduction
        // is a positive fraction representing how much cooldown is shaved
        // (0.10 -> "-10% cooldown"). Multiple stats are joined with a middot.
        // Returns "" when both are zero so utility nodes show no impact line.
        private static string BuildImpactText(float damageBonus, float cdReduction)
        {
            var parts = new List<string>();
            if (Mathf.Abs(damageBonus) > 0.0001f)
            {
                int pct = Mathf.RoundToInt(damageBonus * 100f);
                string sign = pct >= 0 ? "+" : "";
                parts.Add($"{sign}{pct}% damage");
            }
            if (Mathf.Abs(cdReduction) > 0.0001f)
            {
                // A positive reduction shortens the cooldown, so it reads as "-N%".
                int pct = Mathf.RoundToInt(cdReduction * 100f);
                string sign = pct >= 0 ? "-" : "+";
                parts.Add($"{sign}{Mathf.Abs(pct)}% cooldown");
            }
            return string.Join("  ·  ", parts);
        }

        private static string TierDisplay(string tierKey) => tierKey switch
        {
            "tier1" => "Tier 1 (1W each)",
            "tier2" => "Tier 2 (2W each)",
            "tier3" => "Tier 3 (3W each)",
            _ => tierKey,
        };

        private void OnSpend(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            var svc = GetServiceInstance();
            if (svc == null || s_unlockMethod == null)
            {
                FlowTrace.Fail("HeroTalent", "OnSpend: WisdomCurrencyService unavailable — cannot unlock node '" + nodeId + "'.");
                return;
            }
            try
            {
                s_unlockMethod.Invoke(svc, new object[] { nodeId });
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("HeroTalent", "OnSpend Unlock('" + nodeId + "') failed: " + ex.Message);
            }
            Repaint();
        }

        // -- Reflection bridge --------------------------------------------------

        private static void ResolveBridges()
        {
            if (s_catalogType != null && s_serviceType != null) return;
            // G: the type scan reflects across every loaded assembly — a hostile/odd assembly can
            // throw on GetType. Guard it so one bad assembly logs + is skipped, never blanks the panel.
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (s_catalogType == null)
                        s_catalogType = asm.GetType("DeNelle.Village.Talents.HeroTalentCatalog", false);
                    if (s_serviceType == null)
                        s_serviceType = asm.GetType("DeNelle.Village.Talents.WisdomCurrencyService", false);
                    if (s_nodeType == null)
                        s_nodeType = asm.GetType("DeNelle.Village.Talents.HeroTalentNodeDef", false);
                    if (s_treeType == null)
                        s_treeType = asm.GetType("DeNelle.Village.Talents.HeroTalentTreeDef", false);
                    if (s_catalogType != null && s_serviceType != null) break;
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("HeroTalent", "ResolveBridges: assembly type scan threw: " + ex.GetType().Name + ": " + ex.Message);
            }
            if (s_catalogType == null || s_serviceType == null)
                FlowTrace.Warn("HeroTalent",
                    $"ResolveBridges: DeNelle.Village.Talents bridge incomplete (catalog={(s_catalogType != null)}, " +
                    $"service={(s_serviceType != null)}) — talent trees / Wisdom may read empty.");
            if (s_catalogType != null)
            {
                s_getTree = s_catalogType.GetMethod("GetTree",
                    BindingFlags.Public | BindingFlags.Static);
                s_allTrees = s_catalogType.GetProperty("AllTrees",
                    BindingFlags.Public | BindingFlags.Static);
                s_canUnlockMethod = s_catalogType.GetMethod("CanUnlock",
                    BindingFlags.Public | BindingFlags.Static);
            }
            if (s_serviceType != null)
            {
                s_serviceInstance = s_serviceType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                s_wisdomProp = s_serviceType.GetProperty("Wisdom",
                    BindingFlags.Public | BindingFlags.Instance);
                s_unlockedProp = s_serviceType.GetProperty("Unlocked",
                    BindingFlags.Public | BindingFlags.Instance);
                s_unlockMethod = s_serviceType.GetMethod("Unlock",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(string) }, null);
                s_changedEvent = s_serviceType.GetEvent("Changed",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private void SubscribeToService()
        {
            var svc = GetServiceInstance();
            if (svc == null || s_changedEvent == null) return;
            var mi = typeof(HeroTalentPanel).GetMethod(nameof(OnServiceChanged),
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null) return;
            try
            {
                _changedHandler = Delegate.CreateDelegate(s_changedEvent.EventHandlerType, this, mi);
                s_changedEvent.AddEventHandler(svc, _changedHandler);
                _serviceInstanceCache = svc;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("HeroTalent", "Could not subscribe to WisdomCurrencyService.Changed: " + ex.Message);
            }
        }

        private void UnsubscribeFromService()
        {
            if (_changedHandler == null || s_changedEvent == null || _serviceInstanceCache == null) return;
            try
            {
                s_changedEvent.RemoveEventHandler(_serviceInstanceCache, _changedHandler);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("HeroTalent", "UnsubscribeFromService threw: " + ex.GetType().Name + ": " + ex.Message);
            }
            _changedHandler = null;
            _serviceInstanceCache = null;
        }

        private void OnServiceChanged()
        {
            if (_visible) Repaint();
        }

        private object GetServiceInstance()
        {
            if (s_serviceInstance == null) return null;
            try { return s_serviceInstance.GetValue(null, null); }
            catch { return null; }
        }

        private int GetWisdom()
        {
            var svc = GetServiceInstance();
            if (svc == null || s_wisdomProp == null) return 0;
            try { return (int)s_wisdomProp.GetValue(svc, null); }
            catch { return 0; }
        }

        private HashSet<string> GetUnlockedSet()
        {
            var svc = GetServiceInstance();
            if (svc == null || s_unlockedProp == null) return new HashSet<string>();
            try
            {
                var coll = s_unlockedProp.GetValue(svc, null) as System.Collections.IEnumerable;
                var set = new HashSet<string>();
                if (coll != null)
                    foreach (var o in coll)
                        if (o is string s) set.Add(s);
                return set;
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        private bool CanUnlock(string nodeId, int wisdom, HashSet<string> unlocked)
        {
            if (s_canUnlockMethod == null || string.IsNullOrEmpty(nodeId)) return false;
            try
            {
                return (bool)s_canUnlockMethod.Invoke(null,
                    new object[] { nodeId, wisdom, unlocked });
            }
            catch
            {
                return false;
            }
        }

        private object GetTree(string heroSlug)
        {
            if (s_getTree == null) return null;
            try { return s_getTree.Invoke(null, new object[] { heroSlug }); }
            catch { return null; }
        }

        // -- Tiny reflection helpers on the value DTOs --------------------------

        private static string ReadString(object obj, string fieldOrProp)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(fieldOrProp);
            if (f != null) return f.GetValue(obj) as string;
            var p = t.GetProperty(fieldOrProp);
            if (p != null) return p.GetValue(obj, null) as string;
            return null;
        }

        private static int ReadInt(object obj, string fieldOrProp)
        {
            if (obj == null) return 0;
            var t = obj.GetType();
            var f = t.GetField(fieldOrProp);
            if (f != null)
            {
                var v = f.GetValue(obj);
                return v is int i ? i : 0;
            }
            var p = t.GetProperty(fieldOrProp);
            if (p != null)
            {
                var v = p.GetValue(obj, null);
                return v is int i ? i : 0;
            }
            return 0;
        }

        private static float ReadFloat(object obj, string fieldOrProp)
        {
            if (obj == null) return 0f;
            var t = obj.GetType();
            var f = t.GetField(fieldOrProp);
            if (f != null) return ToFloat(f.GetValue(obj));
            var p = t.GetProperty(fieldOrProp);
            if (p != null) return ToFloat(p.GetValue(obj, null));
            return 0f;
        }

        // Boxed numeric values (the Phase-1 fields are floats, but be tolerant of
        // a double/int author choice) → float; anything else → 0.
        private static float ToFloat(object v)
        {
            if (v is float f) return f;
            if (v is double d) return (float)d;
            if (v is int i) return i;
            return 0f;
        }

        private static List<object> ReadList(object obj, string fieldOrProp)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            object raw = null;
            var f = t.GetField(fieldOrProp);
            if (f != null) raw = f.GetValue(obj);
            else
            {
                var p = t.GetProperty(fieldOrProp);
                if (p != null) raw = p.GetValue(obj, null);
            }
            if (raw is System.Collections.IEnumerable en)
            {
                var list = new List<object>();
                foreach (var item in en) list.Add(item);
                return list;
            }
            return null;
        }
    }
}
