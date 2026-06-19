// =============================================================================
// PetSkillTreePanel — opens with the P key. Shows a tabbed inspect/unlock
// surface over the player's three pets:
//   row of tabs (Aether Sprite / Flame Pup / Ice Wolf)
//   vertical flow of node cards:
//     starter  (top)
//     tier1 a + tier1 b
//     tier2 a + tier2 b
//     ultimate (bottom)
// Each card surfaces name, tier badge, type badge, description, cooldown for
// active skills, unlock-level requirement. Locked nodes render at 35% opacity.
// A node whose prereqs are met sprouts an Unlock button that calls into
// PetUnlockTracker.TryUnlock.
// -----------------------------------------------------------------------------
// Cross-asmdef: DeNelle.HUD does NOT reference DeNelle.Pets — the entire
// catalog is reached via reflection (see PetUnlockTracker / PetHeroLeash for
// the bridge pattern). All catalog access tolerates a null Type / null
// instance and falls back to an empty view + warning.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class PetSkillTreePanel : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _tabsRow;
        private VisualElement _treeColumn;
        private Label _headerLabel;
        private Label _statusLabel;

        private bool _open;
        private string _activeSpecies;
        // Modal arbiter handle (DEF-212): one panel open at a time.
        private PanelHandle _panelHandle;

        // Reflection cache — resolved lazily on first show.
        private static Type s_catalogType;
        private static PropertyInfo s_allTrees;
        private static MethodInfo s_getTree;
        private static bool s_bridgeResolved;
        private static bool s_bridgeWarned;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (existing == _doc || existing.panelSettings == null) continue;
                    _doc.panelSettings = existing.panelSettings;
                    break;
                }
            }
            if (_doc.panelSettings == null)
            {
                // V (WO-465 class — WORST offender): this previously disabled itself with ZERO
                // logging, so the Pet Skills panel just never appeared and nothing said why.
                // Fail-loud: no PanelSettings -> rootVisualElement is null -> the panel cannot
                // render. Self-report instead of silently disabling.
                FlowTrace.Fail("PetSkillTree",
                    "Awake: no PanelSettings resolved (none on this UIDocument and none borrowable " +
                    "from another scene UIDocument) — Pet Skills CANNOT render; disabling. " +
                    "Wire a PanelSettings or ensure another UI panel exists first.");
                enabled = false;
                return;
            }
            FlowTrace.Step("PetSkillTree", $"Awake: PanelSettings resolved ('{_doc.panelSettings.name}').");
            _doc.sortingOrder = 105; // above DailyQuestHud (80), below HelpMenu (?) but visible

            _panelHandle = PanelManager.Register("Pet Skills", Close, () => _open);
            // DEF-213: let the Pet House interaction open this panel by id.
            PanelRouter.Register(PanelId.PetSkillTree, Open);
            BuildUi();
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (PetUnlockTracker.Instance != null)
                PetUnlockTracker.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (PetUnlockTracker.Instance != null)
                PetUnlockTracker.Instance.Changed -= Repaint;
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.PetSkillTree, Open);
        }

        public bool IsOpen => _open;

        public void Toggle() => SetOpen(!_open);

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (open) FlowTrace.Step("PetSkillTree", "SetOpen(true) — opening Pet Skills panel.");
            _open = open;
            if (open && _overlay == null)
                FlowTrace.Fail("PetSkillTree",
                    "SetOpen(true): _overlay is NULL — BuildUi never produced an overlay (no PanelSettings/root?). Open is a no-op.");
            if (_overlay != null)
                _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            // Route through the modal arbiter so opening this closes any other panel,
            // and closing clears our slot (DEF-212).
            if (open) PanelManager.NotifyOpened(_panelHandle);
            else PanelManager.NotifyClosed(_panelHandle);
            if (open) Repaint();
        }

        // ── Build ────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            using var _ = FlowTrace.Enter("PetSkillTree", "BuildUi");
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                FlowTrace.Fail("PetSkillTree",
                    "BuildUi: _doc.rootVisualElement is NULL (PanelSettings present but no root) — " +
                    "Pet Skills UI cannot be built; panel will be empty.");
                return;
            }
            _root.pickingMode = PickingMode.Ignore; // don't block HUD beneath
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;

            _overlay = new VisualElement { name = "PetSkillTreeOverlay" };
            ElarionUi.StyleScrim(_overlay);
            // Anchor the card near the top (tabbed sheet) rather than centred.
            _overlay.style.flexDirection = FlexDirection.Column;
            _overlay.style.justifyContent = Justify.FlexStart;
            _overlay.style.paddingTop = 48;
            _overlay.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target == _overlay) Close();
            });
            _root.Add(_overlay);

            var card = new VisualElement { name = "PetSkillTreeCard" };
            card.style.width = 560;
            card.style.maxHeight = new Length(86f, LengthUnit.Percent);
            card.style.paddingLeft = 16; card.style.paddingRight = 16;
            card.style.paddingTop = 14;  card.style.paddingBottom = 14;
            // Elarion stone panel + gold rim (canon).
            ElarionUi.StylePanel(card, dark: true);
            _overlay.Add(card);

            // Header row: title + close button.
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 10;
            card.Add(headerRow);

            _headerLabel = new Label("Echo Skill Trees");
            _headerLabel.style.color = new StyleColor(ElarionUi.Gilt);
            _headerLabel.style.fontSize = ElarionUi.FontHead;
            _headerLabel.style.letterSpacing = 1.5f;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(_headerLabel);

            var closeBtn = new Button(() => Close()) { text = "Close (P)" };
            StyleSecondaryButton(closeBtn);
            headerRow.Add(closeBtn);

            // Thin gilt underline beneath the header (canon).
            card.Add(ElarionUi.MakeRule());

            _tabsRow = new VisualElement { name = "PetSkillTreeTabs" };
            _tabsRow.style.flexDirection = FlexDirection.Row;
            _tabsRow.style.justifyContent = Justify.SpaceBetween;
            _tabsRow.style.marginBottom = 12;
            card.Add(_tabsRow);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            card.Add(scroll);

            _treeColumn = new VisualElement { name = "PetSkillTreeColumn" };
            _treeColumn.style.flexDirection = FlexDirection.Column;
            _treeColumn.style.alignItems = Align.Stretch;
            scroll.Add(_treeColumn);

            _statusLabel = new Label("");
            _statusLabel.style.marginTop = 8;
            _statusLabel.style.color = new StyleColor(ElarionUi.Gold);
            _statusLabel.style.fontSize = ElarionUi.FontMicro;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            card.Add(_statusLabel);
        }

        // ── Repaint ──────────────────────────────────────────────────────────

        private void Repaint()
        {
            using var _ = FlowTrace.Enter("PetSkillTree", "Repaint");
            if (_tabsRow == null || _treeColumn == null)
            {
                FlowTrace.Fail("PetSkillTree",
                    "Repaint: tabs/tree containers are NULL — BuildUi never produced them (no root?). Repaint is a no-op.");
                return;
            }
            _tabsRow.Clear();
            _treeColumn.Clear();
            _statusLabel.text = "";

            if (!ResolveBridge())
            {
                // Roll-up: catalog asmdef unavailable -> visible note kept (R), self-reported (data-empty cause).
                FlowTrace.Warn("PetSkillTree",
                    "Repaint: DeNelle.Pets catalog bridge unavailable — showing the visible 'catalog not available' note.");
                var note = new Label("Echo catalog is not available. Try restarting the scene.");
                note.style.color = new StyleColor(ElarionUi.Danger);
                note.style.fontSize = ElarionUi.FontLabel;
                _treeColumn.Add(note);
                return;
            }

            var trees = GetAllTrees();
            if (trees == null || trees.Count == 0)
            {
                FlowTrace.Warn("PetSkillTree",
                    "Repaint: catalog returned 0 pet trees — showing the visible 'No pet trees defined' note (data-empty).");
                var note = new Label("No pet trees defined.");
                note.style.color = new StyleColor(ElarionUi.ParchmentDim);
                note.style.fontSize = ElarionUi.FontLabel;
                _treeColumn.Add(note);
                return;
            }

            // First-time activation: pick the first species.
            if (string.IsNullOrEmpty(_activeSpecies) || GetTreeByReflection(_activeSpecies) == null)
                _activeSpecies = ExtractSpecies(trees[0]);

            // Tabs.
            for (int i = 0; i < trees.Count; i++)
            {
                var t = trees[i];
                string species = ExtractSpecies(t);
                string display = ExtractDisplayName(t);
                _tabsRow.Add(BuildTab(species, display));
            }

            // Body.
            var active = GetTreeByReflection(_activeSpecies);
            if (active == null)
            {
                var note = new Label("Selected tree could not be loaded.");
                note.style.color = new StyleColor(ElarionUi.Danger);
                note.style.fontSize = ElarionUi.FontLabel;
                _treeColumn.Add(note);
                return;
            }

            // Header sub-line — pet level.
            int petLevel = PetUnlockTracker.Instance != null
                ? PetUnlockTracker.Instance.LevelOf(_activeSpecies)
                : 1;
            int xp = PetUnlockTracker.Instance != null
                ? PetUnlockTracker.Instance.XpOf(_activeSpecies)
                : 0;
            int need = PetUnlockTracker.XpForLevel(petLevel);
            var levelLine = new Label($"Level {petLevel}  ·  XP {xp}/{need}");
            levelLine.style.color = new StyleColor(ElarionUi.Aether);
            levelLine.style.fontSize = ElarionUi.FontLabel;
            levelLine.style.marginBottom = 8;
            levelLine.style.unityTextAlign = TextAnchor.MiddleCenter;
            _treeColumn.Add(levelLine);

            // Group by tier.
            var skills = ExtractSkills(active);
            var byTier = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase)
            {
                { "starter",  new List<object>() },
                { "tier1",    new List<object>() },
                { "tier2",    new List<object>() },
                { "ultimate", new List<object>() },
            };
            foreach (var s in skills)
            {
                string tier = ExtractString(s, "Tier") ?? "tier1";
                if (!byTier.TryGetValue(tier, out var list))
                {
                    list = new List<object>();
                    byTier[tier] = list;
                }
                list.Add(s);
            }

            // Render in vertical flow: starter → tier1 → tier2 → ultimate.
            AppendTierRow(byTier["starter"],  centered: true);
            AppendTierRow(byTier["tier1"]);
            AppendTierRow(byTier["tier2"]);
            AppendTierRow(byTier["ultimate"], centered: true);
        }

        private void AppendTierRow(List<object> skills, bool centered = false)
        {
            if (skills == null || skills.Count == 0) return;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = centered ? Justify.Center : Justify.SpaceBetween;
            row.style.marginBottom = 10;
            foreach (var s in skills) row.Add(BuildNodeCard(s));
            _treeColumn.Add(row);
        }

        // ── Tabs ─────────────────────────────────────────────────────────────

        private VisualElement BuildTab(string species, string display)
        {
            bool active = string.Equals(species, _activeSpecies, StringComparison.Ordinal);
            var btn = new Button(() =>
            {
                _activeSpecies = species;
                Repaint();
            })
            {
                text = string.IsNullOrEmpty(display) ? species : display,
            };
            btn.style.flexGrow = 1;
            btn.style.marginLeft = 2; btn.style.marginRight = 2;
            btn.style.paddingTop = 6; btn.style.paddingBottom = 6;
            btn.style.color = new StyleColor(active ? ElarionUi.Gilt : ElarionUi.ParchmentDim);
            btn.style.backgroundColor = new StyleColor(active
                ? ElarionUi.PanelStone
                : ElarionUi.PanelStoneDark);
            btn.style.borderLeftWidth = 0; btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;  btn.style.borderBottomWidth = active ? 2 : 1;
            btn.style.borderBottomColor = new StyleColor(active
                ? ElarionUi.Gold
                : new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f));
            btn.style.borderTopLeftRadius = 6; btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 0; btn.style.borderBottomRightRadius = 0;
            btn.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            return btn;
        }

        // ── Node card ────────────────────────────────────────────────────────

        private VisualElement BuildNodeCard(object skill)
        {
            string id = ExtractString(skill, "Id") ?? "";
            string name = ExtractString(skill, "Name") ?? id;
            string type = ExtractString(skill, "Type") ?? "passive";
            string tier = ExtractString(skill, "Tier") ?? "tier1";
            string desc = ExtractString(skill, "Description") ?? "";
            int unlockLevel = ExtractInt(skill, "UnlockLevel", 1);
            float? cd = ExtractFloatNullable(skill, "CooldownSeconds");

            bool unlocked = PetUnlockTracker.Instance != null
                && PetUnlockTracker.Instance.IsUnlocked(id);

            int petLevel = PetUnlockTracker.Instance != null
                ? PetUnlockTracker.Instance.LevelOf(_activeSpecies)
                : 1;
            var unlockedSet = PetUnlockTracker.Instance != null
                ? new HashSet<string>(PetUnlockTracker.Instance.UnlockedFor(_activeSpecies))
                : new HashSet<string>();

            bool canUnlock = !unlocked && CanUnlockByReflection(id, petLevel, unlockedSet);

            var card = new VisualElement();
            card.style.flexGrow = 1;
            card.style.marginLeft = 4; card.style.marginRight = 4;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.paddingTop = 8;   card.style.paddingBottom = 8;
            card.style.minWidth = 220;
            card.style.maxWidth = 260;
            card.style.borderTopLeftRadius = 8; card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8; card.style.borderBottomRightRadius = 8;
            card.style.borderLeftWidth = 1; card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;  card.style.borderBottomWidth = 1;
            Color rim = unlocked ? ElarionUi.Affordable
                       : canUnlock ? ElarionUi.Gold
                                   : ElarionUi.StoneTrim;
            Color bg  = unlocked
                ? new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.16f)
                : ElarionUi.PanelStoneDark;
            card.style.backgroundColor = new StyleColor(bg);
            card.style.borderLeftColor = new StyleColor(rim);
            card.style.borderRightColor = new StyleColor(rim);
            card.style.borderTopColor = new StyleColor(rim);
            card.style.borderBottomColor = new StyleColor(rim);
            // Locked = 35% opacity per spec.
            card.style.opacity = unlocked ? 1f : (canUnlock ? 1f : 0.35f);

            // Name (12pt bold).
            var nameLabel = new Label(name);
            nameLabel.style.color = new StyleColor(ElarionUi.Parchment);
            nameLabel.style.fontSize = ElarionUi.FontLabel;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(nameLabel);

            // Badge row.
            var badges = new VisualElement();
            badges.style.flexDirection = FlexDirection.Row;
            badges.style.marginTop = 2; badges.style.marginBottom = 4;
            badges.Add(BuildBadge(tier, TierBadgeColor(tier)));
            badges.Add(BuildBadge(type, TypeBadgeColor(type)));
            card.Add(badges);

            // Description.
            var descLabel = new Label(desc);
            descLabel.style.color = new StyleColor(ElarionUi.ParchmentDim);
            descLabel.style.fontSize = ElarionUi.FontMicro;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 4;
            card.Add(descLabel);

            // Meta row: cooldown (if active) + unlock-level requirement.
            var meta = new Label(BuildMetaLine(type, cd, unlockLevel));
            meta.style.color = new StyleColor(ElarionUi.ParchmentDim);
            meta.style.fontSize = ElarionUi.FontMicro;
            meta.style.marginBottom = 6;
            card.Add(meta);

            // Status / button.
            if (unlocked)
            {
                var owned = new Label("Unlocked");
                owned.style.color = new StyleColor(ElarionUi.Affordable);
                owned.style.fontSize = ElarionUi.FontMicro;
                owned.style.unityFontStyleAndWeight = FontStyle.Bold;
                owned.style.unityTextAlign = TextAnchor.MiddleCenter;
                card.Add(owned);
            }
            else if (canUnlock)
            {
                var btn = new Button(() => OnUnlockClicked(id, name)) { text = "Unlock" };
                StylePrimaryButton(btn);
                card.Add(btn);
            }
            else
            {
                string lockReason = LockReason(skill, petLevel, unlockedSet);
                var locked = new Label(lockReason);
                locked.style.color = new StyleColor(ElarionUi.Gold);
                locked.style.fontSize = ElarionUi.FontMicro;
                locked.style.whiteSpace = WhiteSpace.Normal;
                locked.style.unityTextAlign = TextAnchor.MiddleCenter;
                card.Add(locked);
            }

            return card;
        }

        private static VisualElement BuildBadge(string text, Color tint)
        {
            var b = new VisualElement();
            b.style.marginRight = 4;
            b.style.paddingLeft = 6; b.style.paddingRight = 6;
            b.style.paddingTop = 1;  b.style.paddingBottom = 1;
            b.style.borderTopLeftRadius = 6; b.style.borderTopRightRadius = 6;
            b.style.borderBottomLeftRadius = 6; b.style.borderBottomRightRadius = 6;
            b.style.backgroundColor = new StyleColor(new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 0.85f));
            var lbl = new Label(text);
            lbl.style.color = new StyleColor(tint);
            lbl.style.fontSize = 9;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.Add(lbl);
            return b;
        }

        // Tier badge tints — graded across the Elarion accent palette (canon)
        // so they read as ONE designed set, not ad-hoc rainbow.
        private static Color TierBadgeColor(string tier) => tier?.ToLowerInvariant() switch
        {
            "starter"  => ElarionUi.ParchmentDim,
            "tier1"    => ElarionUi.StoneTrim,
            "tier2"    => ElarionUi.Gold,
            "ultimate" => ElarionUi.Aether,
            _          => ElarionUi.ParchmentDim,
        };

        private static Color TypeBadgeColor(string type) =>
            string.Equals(type, "active", StringComparison.OrdinalIgnoreCase)
                ? ElarionUi.Danger
                : ElarionUi.Affordable;

        private static string BuildMetaLine(string type, float? cd, int unlockLevel)
        {
            string left = string.Equals(type, "active", StringComparison.OrdinalIgnoreCase) && cd.HasValue
                ? $"CD {Mathf.RoundToInt(cd.Value)}s"
                : "—";
            string right = $"Lv {unlockLevel}";
            return $"{left}   ·   Unlocks at {right}";
        }

        private static string LockReason(object skill, int petLevel, HashSet<string> unlocked)
        {
            int unlockLevel = ExtractInt(skill, "UnlockLevel", 1);
            if (petLevel < unlockLevel) return $"Requires level {unlockLevel}";
            var prereqs = ExtractStringList(skill, "Prerequisites");
            var missing = new List<string>();
            if (prereqs != null)
            {
                foreach (var p in prereqs)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    if (!unlocked.Contains(p)) missing.Add(PrettyId(p));
                }
            }
            if (missing.Count > 0) return "Needs: " + string.Join(", ", missing);
            return "Locked";
        }

        private static string PrettyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            int dot = id.IndexOf('.');
            string tail = dot >= 0 ? id.Substring(dot + 1) : id;
            return tail.Replace('-', ' ');
        }

        private void OnUnlockClicked(string skillId, string name)
        {
            if (PetUnlockTracker.Instance == null)
            {
                _statusLabel.text = "Unlock tracker unavailable.";
                return;
            }
            if (PetUnlockTracker.Instance.TryUnlock(skillId))
            {
                _statusLabel.text = $"Unlocked: {name}";
            }
            else
            {
                _statusLabel.text = $"Cannot unlock {name} yet.";
            }
            Repaint();
        }

        // ── Button styling ───────────────────────────────────────────────────

        private static void StylePrimaryButton(Button b)
        {
            // Gold CTA (canon).
            ElarionUi.StyleButton(b, ElarionUi.ButtonKind.Gold);
            b.style.minHeight = StyleKeyword.Auto;
            b.style.marginTop = 2;
            b.style.paddingTop = 4; b.style.paddingBottom = 4;
        }

        private static void StyleSecondaryButton(Button b)
        {
            // Neutral stone (canon).
            ElarionUi.StyleButton(b, ElarionUi.ButtonKind.Neutral);
            b.style.minHeight = StyleKeyword.Auto;
            b.style.paddingLeft = 10; b.style.paddingRight = 10;
            b.style.paddingTop = 4;   b.style.paddingBottom = 4;
        }

        // ── Catalog reflection bridge ───────────────────────────────────────

        private static bool ResolveBridge()
        {
            if (s_bridgeResolved) return s_catalogType != null;
            s_bridgeResolved = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("DeNelle.Pets.PetSkillTreeCatalog", false);
                    if (t != null) { s_catalogType = t; break; }
                }
                if (s_catalogType == null)
                {
                    if (!s_bridgeWarned)
                    {
                        FlowTrace.Fail("PetSkillTree", "ResolveBridge: DeNelle.Pets.PetSkillTreeCatalog not found — panel renders empty.");
                        s_bridgeWarned = true;
                    }
                    return false;
                }
                s_allTrees = s_catalogType.GetProperty("AllTrees", BindingFlags.Public | BindingFlags.Static);
                s_getTree  = s_catalogType.GetMethod("GetTree",   BindingFlags.Public | BindingFlags.Static);
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("PetSkillTree", "ResolveBridge failed: " + ex.Message);
                return false;
            }
        }

        private static List<object> GetAllTrees()
        {
            var result = new List<object>();
            if (s_allTrees == null) return result;
            try
            {
                var enumerable = s_allTrees.GetValue(null) as IEnumerable;
                if (enumerable == null) return result;
                foreach (var t in enumerable) if (t != null) result.Add(t);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", "AllTrees read failed: " + ex.Message);
            }
            return result;
        }

        private static object GetTreeByReflection(string species)
        {
            if (string.IsNullOrEmpty(species) || s_getTree == null) return null;
            try { return s_getTree.Invoke(null, new object[] { species }); }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", "GetTree failed: " + ex.Message);
                return null;
            }
        }

        private bool CanUnlockByReflection(string skillId, int petLevel, HashSet<string> unlocked)
        {
            if (s_catalogType == null) return false;
            try
            {
                var mi = s_catalogType.GetMethod("CanUnlock", BindingFlags.Public | BindingFlags.Static);
                if (mi == null) return false;
                object res = mi.Invoke(null, new object[] { skillId, petLevel, unlocked });
                return res is bool b && b;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", "CanUnlock failed: " + ex.Message);
                return false;
            }
        }

        // ── Reflection accessors over PetSkillTreeDef / PetSkillDef ─────────

        private static string ExtractSpecies(object tree)  => ExtractString(tree, "Species");
        private static string ExtractDisplayName(object tree) => ExtractString(tree, "DisplayName");

        private static List<object> ExtractSkills(object tree)
        {
            var list = new List<object>();
            if (tree == null) return list;
            try
            {
                var f = tree.GetType().GetField("Skills");
                if (f == null) return list;
                var en = f.GetValue(tree) as IEnumerable;
                if (en == null) return list;
                foreach (var s in en) if (s != null) list.Add(s);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", "Skills read failed: " + ex.Message);
            }
            return list;
        }

        private static string ExtractString(object obj, string field)
        {
            if (obj == null) return null;
            try
            {
                var f = obj.GetType().GetField(field);
                return f?.GetValue(obj) as string;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", $"ExtractString('{field}') threw: {ex.GetType().Name}: {ex.Message} — using null.");
                return null;
            }
        }

        private static int ExtractInt(object obj, string field, int fallback)
        {
            if (obj == null) return fallback;
            try
            {
                var f = obj.GetType().GetField(field);
                if (f == null) return fallback;
                object v = f.GetValue(obj);
                return v is int i ? i : fallback;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", $"ExtractInt('{field}') threw: {ex.GetType().Name}: {ex.Message} — using fallback {fallback}.");
                return fallback;
            }
        }

        private static float? ExtractFloatNullable(object obj, string field)
        {
            if (obj == null) return null;
            try
            {
                var f = obj.GetType().GetField(field);
                if (f == null) return null;
                object v = f.GetValue(obj);
                if (v == null) return null;
                if (v is float fv) return fv;
                if (v is double dv) return (float)dv;
                return null;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("PetSkillTree", $"ExtractFloatNullable('{field}') threw: {ex.GetType().Name}: {ex.Message} — using null.");
                return null;
            }
        }

        private static List<string> ExtractStringList(object obj, string field)
        {
            if (obj == null) return null;
            try
            {
                var f = obj.GetType().GetField(field);
                if (f == null) return null;
                var en = f.GetValue(obj) as IEnumerable;
                if (en == null) return null;
                var list = new List<string>();
                foreach (var v in en) if (v is string s) list.Add(s);
                return list;
            }
            catch { return null; }
        }
    }
}
