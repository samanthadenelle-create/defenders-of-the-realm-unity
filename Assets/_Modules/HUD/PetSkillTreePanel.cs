// =============================================================================
// PetSkillTreePanel — the Echo (pet) skill-tree inspect/unlock surface.
// Tabs per species (Aether Sprite / Flame Pup / Ice Wolf); vertical flow of
// node cards in tier order (starter → tier1 → tier2 → ultimate). Each card:
// name, tier/type badges, description, cooldown + unlock-level meta, and the
// Unlock action (or the honest lock reason). Locked nodes render at 35% alpha.
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #5): UIDocument/UITK ->
// code-built uGUI on the Obsidian master frame (BuildObsidianModal: FramePet —
// the pack's pet panel — + medallion + the ONE shared Close + scrim), per the
// HelpMenu reference recipe. Node cards ride an inline ScrollRect column.
//
// Cross-asmdef bridge UNCHANGED: DeNelle.HUD does NOT reference DeNelle.Pets —
// the catalog is reached via reflection; every access tolerates a null Type /
// instance and falls back to an empty view + warning.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class PetSkillTreePanel : MonoBehaviour
    {
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _tabHost;
        private Transform _treeContent;    // ScrollRect content column
        private TextMeshProUGUI _statusLabel;

        private bool _open;
        private string _activeSpecies;
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
            _panelHandle = PanelManager.Register("Pet Skills", Close, () => _open);
            // DEF-213: let the Pet House interaction open this panel by id.
            PanelRouter.Register(PanelId.PetSkillTree, Open);
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
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        public bool IsOpen => _open;

        public void Toggle() => SetOpen(!_open);

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (open)
            {
                FlowTrace.Step("PetSkillTree", "SetOpen(true) — opening Pet Skills panel.");
                EnsureBuilt();
            }
            if (_modal == null || _modal.canvas == null) { _open = false; return; }
            _open = open;
            _modal.canvas.SetActive(open);
            // Route through the modal arbiter (DEF-212); battle-lock may reject —
            // revert and stay hidden, never force-show.
            if (open)
            {
                if (!PanelManager.NotifyOpened(_panelHandle))
                {
                    _open = false;
                    _modal.canvas.SetActive(false);
                    return;
                }
                Repaint();
            }
            else
            {
                PanelManager.NotifyClosed(_panelHandle);
            }
        }

        // ── Build (kit modal, lazy on first open) ────────────────────────────

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;
            using var _ = FlowTrace.Enter("PetSkillTree", "BuildUi");

            // PORTRAIT sizing (UI review 06): Pet_Panel is a PORTRAIT frame (~1230x1484). Anchor to a
            // narrow, tall center column so the rendered aspect matches the template instead of
            // stretching the ornate frame into a near-square landscape slab.
            _modal = ElarionUiKit.BuildObsidianModal("PetSkillTreeUI", "Echo Skill Trees",
                new Vector2(0.29f, 0.05f), new Vector2(0.71f, 0.95f), Close,
                frameName: RpgUiCatalog.FramePet, medallionIcon: "tree");

            // FULL content area, not just the frame's lower body zone (eyes-on 2026-07-03:
            // FramePet's portrait arch sat EMPTY above the tabs — half the panel read blank).
            // The species tab BLOCK lives where the arch is (two wrapped rows fit ~12 species);
            // the tree scroll fills the rest.
            var body = _modal.chrome.content.transform;

            _tabHost = ZoneRect(body, "SpeciesTabs", new Vector2(0.07f, 0.665f), new Vector2(0.93f, 0.855f));

            // SWEEP 9413 R2 (#8): content-fraction layout — the kit's Close reservation can't
            // protect it. Floor = closeBandTop (0.050 + 120px/panelHeight) + 0.02 ≈ 0.214 on this
            // 0.9-tall panel: the row list bottom (was 0.115, Close covered the last row) and the
            // status line (was 0.055–0.11, fully inside the Close band) both raised above it.
            var scrollHost = ZoneRect(body, "TreeScroll", new Vector2(0.07f, 0.28f), new Vector2(0.93f, 0.655f));
            _treeContent = BuildScrollColumn(scrollHost);

            _statusLabel = MakeText(body, "", 13, ElarionUi.Gold, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.272f));
            ElarionUiKit.FitSingleLine(_statusLabel);

            _modal.canvas.SetActive(false);   // built hidden; SetOpen shows it
        }

        // ── Repaint ──────────────────────────────────────────────────────────

        private void Repaint()
        {
            using var _ = FlowTrace.Enter("PetSkillTree", "Repaint");
            if (!_open || _tabHost == null || _treeContent == null) return;

            for (int i = _tabHost.childCount - 1; i >= 0; i--)
                Destroy(_tabHost.GetChild(i).gameObject);
            for (int i = _treeContent.childCount - 1; i >= 0; i--)
                Destroy(_treeContent.GetChild(i).gameObject);
            _statusLabel.text = "";

            if (!ResolveBridge())
            {
                FlowTrace.Warn("PetSkillTree",
                    "Repaint: DeNelle.Pets catalog bridge unavailable — showing the visible 'catalog not available' note.");
                AddNoteRow("Echo catalog is not available. Try restarting the scene.", ElarionUi.Danger);
                return;
            }

            var trees = GetAllTrees();
            if (trees == null || trees.Count == 0)
            {
                FlowTrace.Warn("PetSkillTree",
                    "Repaint: catalog returned 0 pet trees — showing the visible 'No pet trees defined' note (data-empty).");
                AddNoteRow("No pet trees defined.", ElarionUi.ParchmentDim);
                return;
            }

            // First-time activation: pick the first species.
            if (string.IsNullOrEmpty(_activeSpecies) || GetTreeByReflection(_activeSpecies) == null)
                _activeSpecies = ExtractSpecies(trees[0]);

            // Species tabs (Yellow = active) — WRAPPED rows of 4 (eyes-on 2026-07-03:
            // ~12 species crushed into one row rendered as unreadable slivers).
            const int perRow = 4;
            int rows = Mathf.Max(1, Mathf.CeilToInt(trees.Count / (float)perRow));
            for (int i = 0; i < trees.Count; i++)
            {
                var t = trees[i];
                string species = ExtractSpecies(t);
                string display = ExtractDisplayName(t);
                int row = i / perRow, col = i % perRow;
                float w = 1f / perRow, h = 1f / rows;
                float x0 = 0.005f + col * w, x1 = x0 + w - 0.01f;
                float y1 = 1f - row * h - 0.02f, y0 = y1 - h + 0.04f;
                string label = string.IsNullOrEmpty(display) ? species : display;
                bool active = string.Equals(species, _activeSpecies, StringComparison.Ordinal);
                ElarionUiKit.BuildObsidianButton(_tabHost, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    active ? ElarionUiKit.ObsidianButtonColor.Yellow
                           : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(x0, y0), new Vector2(x1, y1),
                    () => { _activeSpecies = species; Repaint(); });
            }

            // Body.
            var activeTree = GetTreeByReflection(_activeSpecies);
            if (activeTree == null)
            {
                AddNoteRow("Selected tree could not be loaded.", ElarionUi.Danger);
                return;
            }

            // Level sub-line.
            int petLevel = PetUnlockTracker.Instance != null
                ? PetUnlockTracker.Instance.LevelOf(_activeSpecies)
                : 1;
            int xp = PetUnlockTracker.Instance != null
                ? PetUnlockTracker.Instance.XpOf(_activeSpecies)
                : 0;
            int need = PetUnlockTracker.XpForLevel(petLevel);
            AddNoteRow($"Level {petLevel}  ·  XP {xp}/{need}", ElarionUi.Aether, bold: true);

            // Group by tier and render starter → tier1 → tier2 → ultimate.
            var skills = ExtractSkills(activeTree);
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
            foreach (var tier in new[] { "starter", "tier1", "tier2", "ultimate" })
                foreach (var s in byTier[tier])
                    BuildNodeCard(s);
        }

        private void AddNoteRow(string text, Color color, bool bold = false)
        {
            var rowGo = new GameObject("Note", typeof(RectTransform), typeof(LayoutElement));
            rowGo.transform.SetParent(_treeContent, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 30f;
            MakeText(rowGo.transform, text, 14, color, bold ? FontStyles.Bold : FontStyles.Normal,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        }

        // ── Node card ────────────────────────────────────────────────────────

        private void BuildNodeCard(object skill)
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

            var cardGo = new GameObject("Node", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CanvasGroup));
            cardGo.transform.SetParent(_treeContent, false);
            cardGo.GetComponent<LayoutElement>().preferredHeight = 128f;
            var bg = cardGo.GetComponent<Image>();
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_talent");
            if (slotSprite != null) { bg.sprite = slotSprite; bg.type = Image.Type.Sliced; }
            // Unlocked = affirmative tint; unlockable = gold; locked = plain.
            bg.color = unlocked
                ? new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.55f)
                : canUnlock ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f)
                            : Color.white;
            // Locked = 35% opacity per spec.
            cardGo.GetComponent<CanvasGroup>().alpha = (unlocked || canUnlock) ? 1f : 0.35f;

            // SWEEP 9413 R2 (#8): row titles straddled the card border — single-line fit so a
            // long skill name shrinks/ellipsizes inside its band instead of painting past it.
            var nameLabel = MakeText(cardGo.transform, name, 16, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.04f, 0.72f), new Vector2(0.70f, 0.97f));
            ElarionUiKit.FitSingleLine(nameLabel);
            // Badge line: tier + type, palette-graded (canon set, not ad-hoc rainbow). UI review 06:
            // bumped 11->13 for legibility (the sub-labels read dense/muddy at 11 over the textured well).
            MakeText(cardGo.transform,
                $"<color=#{ColorUtility.ToHtmlStringRGB(TierBadgeColor(tier))}>{tier.ToUpperInvariant()}</color>   " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(TypeBadgeColor(type))}>{type.ToUpperInvariant()}</color>",
                13, ElarionUi.ParchmentDim, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.04f, 0.56f), new Vector2(0.70f, 0.72f));
            MakeText(cardGo.transform, desc, 13, ElarionUi.Parchment, FontStyles.Italic,
                TextAlignmentOptions.TopLeft, new Vector2(0.04f, 0.22f), new Vector2(0.70f, 0.54f));
            // Meta line brightened to Parchment (was dim over the textured well) + 11->12.
            MakeText(cardGo.transform, BuildMetaLine(type, cd, unlockLevel), 12, ElarionUi.Parchment,
                FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.04f), new Vector2(0.70f, 0.20f));

            // Status / action, right column.
            if (unlocked)
            {
                MakeText(cardGo.transform, "Unlocked", 14, ElarionUi.Affordable, FontStyles.Bold,
                    TextAlignmentOptions.Center, new Vector2(0.72f, 0.35f), new Vector2(0.97f, 0.65f));
            }
            else if (canUnlock)
            {
                ElarionUiKit.BuildObsidianButton(cardGo.transform, "Unlock",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                    new Vector2(0.72f, 0.30f), new Vector2(0.97f, 0.70f),
                    () => OnUnlockClicked(id, name));
            }
            else
            {
                MakeText(cardGo.transform, LockReason(skill, petLevel, unlockedSet), 12,
                    ElarionUi.Gold, FontStyles.Normal, TextAlignmentOptions.Center,
                    new Vector2(0.72f, 0.15f), new Vector2(0.97f, 0.85f));
            }
        }

        // Tier badge tints — graded across the Elarion accent palette (canon).
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
            _statusLabel.text = PetUnlockTracker.Instance.TryUnlock(skillId)
                ? $"Unlocked: {name}"
                : $"Cannot unlock {name} yet.";
            Repaint();
        }

        // ── Catalog reflection bridge (UNCHANGED) ───────────────────────────

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
            catch (Exception e) { FlowTrace.Warn("PetSkillTree", $"ExtractStringList('{field}') reflected read threw: {e.GetType().Name}: {e.Message}"); return null; }
        }

        // ── uGUI helpers (same shapes as LeaderboardPanel / CosmeticShopPanel) ──

        private static Transform ZoneRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Transform BuildScrollColumn(Transform host)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(host, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return contentGo.transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
