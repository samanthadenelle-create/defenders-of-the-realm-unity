// =============================================================================
// TalentTreePanel (DEF-51) — Wisdom-spend talent tree screen.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// WHAT IT DOES:
//   Renders the current hero's 3-tier × 2-column talent tree from
//   HeroTalentCatalog, shows the live Wisdom balance, and lets the player
//   unlock nodes by tapping them. A respec button refunds spent Wisdom
//   in exchange for Crystals (EconomyService.TrySpend).
//
//   Node visual states:
//     Locked    — prerequisite not met (grey tint, button disabled)
//     Available — prereqs met, affordable (green outline, button active)
//     Unlocked  — already learned (gold background, button disabled)
//
// ARCHITECTURE:
//   * Code-UI-Toolkit (no UXML) — same pattern as LevelUpSkillPopup /
//     TowerUpgradeButton so it renders correctly in player builds.
//   * [RequireComponent(typeof(UIDocument))] — same deployment model as
//     the project's other HUDs.
//   * Open(heroSlug) / Close() are the public API; the HUD's talent button
//     calls Open() with the hero class from HeroAbilities.HeroClass.
//   * Subscribes WisdomCurrencyService.Changed in OnEnable, unsubscribes
//     in OnDisable — never holds stale state.
//   * Respec costs HeroTalentCatalog.RespecCostCrystals from EconomyService
//     (Crystals-only spend; the old Wood-only path would have been wrong).
//
// INTEGRATOR NOTE:
//   Add a UIDocument + PanelSettings + this component to the Village HUD
//   GameObject. Wire the talent-tree button's click to Open(heroClass).
//   The panel starts hidden; call Close() (or click the × button) to hide it.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Village.Talents;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// Talent-tree Wisdom-spend panel. Open via <see cref="Open(string)"/>;
    /// subscribe to <see cref="WisdomCurrencyService.Changed"/> to refresh
    /// any external Wisdom displays.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TalentTreePanel : MonoBehaviour
    {
        // ── Column layout metadata ─────────────────────────────────────────────
        // The catalog uses column "a" and "b" within each tier.
        private static readonly string[] Columns = { "a", "b" };
        private static readonly string[] Tiers   = { "tier1", "tier2", "tier3" };

        // ── Colors for node states ─────────────────────────────────────────────
        private static readonly StyleColor ColLocked    = new StyleColor(new Color(0.32f, 0.32f, 0.32f));
        private static readonly StyleColor ColAvailable = new StyleColor(new Color(0.25f, 0.75f, 0.30f));
        private static readonly StyleColor ColUnlocked  = new StyleColor(new Color(0.95f, 0.75f, 0.10f));
        private static readonly StyleColor ColText      = new StyleColor(Color.white);
        private static readonly StyleColor ColSubText   = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        private static readonly StyleColor ColBg        = new StyleColor(new Color(0.07f, 0.07f, 0.09f, 0.96f));
        private static readonly StyleColor ColPanelBg   = new StyleColor(new Color(0.10f, 0.12f, 0.15f, 0.98f));

        // ── Runtime ────────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _overlay;
        private Label         _wisdomLabel;
        private VisualElement _gridContainer;
        private string        _currentHeroSlug;
        private bool          _uiBuilt;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            BuildUiIfNeeded();
            SetVisible(false);

            if (WisdomCurrencyService.Instance != null)
                WisdomCurrencyService.Instance.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (WisdomCurrencyService.Instance != null)
                WisdomCurrencyService.Instance.Changed -= Refresh;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the panel for <paramref name="heroSlug"/> (e.g. "mage").
        /// Call from the HUD's talent-tree button, passing
        /// <see cref="HeroAbilities.HeroClass"/>.
        /// </summary>
        public void Open(string heroSlug = null)
        {
            BuildUiIfNeeded();
            _currentHeroSlug = string.IsNullOrWhiteSpace(heroSlug) ? "mage" : heroSlug.Trim().ToLowerInvariant();
            RebuildGrid();
            Refresh();
            SetVisible(true);
        }

        /// <summary>Closes the panel.</summary>
        public void Close()
        {
            SetVisible(false);
        }

        // ── Build ──────────────────────────────────────────────────────────────

        private void BuildUiIfNeeded()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            var doc = GetComponent<UIDocument>();
            if (doc == null) return;

            // Full-screen centering overlay
            _overlay = new VisualElement
            {
                name = "TalentTreeOverlay",
                style =
                {
                    position        = Position.Absolute,
                    top             = 0, bottom = 0, left = 0, right = 0,
                    alignItems      = Align.Center,
                    justifyContent  = Justify.Center,
                    backgroundColor = ColBg,
                }
            };
            _overlay.RegisterCallback<ClickEvent>(evt =>
            {
                // Tap outside the card → close
                if (evt.target == _overlay) Close();
            });

            // Card
            var card = new VisualElement
            {
                name = "TalentTreeCard",
                style =
                {
                    backgroundColor = ColPanelBg,
                    borderTopLeftRadius     = 12, borderTopRightRadius    = 12,
                    borderBottomLeftRadius  = 12, borderBottomRightRadius = 12,
                    paddingTop    = 20, paddingBottom = 20,
                    paddingLeft   = 24, paddingRight  = 24,
                    minWidth      = 420, maxWidth = 520,
                }
            };

            // ── Header row ────────────────────────────────────────────────────
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 14 } };

            var title = new Label("Talent Tree")
            {
                style = { color = ColText, fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
            };

            _wisdomLabel = new Label("Wisdom: 0")
            {
                style = { color = ColUnlocked, fontSize = 15, unityTextAlign = TextAnchor.MiddleRight }
            };

            var closeBtn = new Button(Close) { text = "✕" };
            closeBtn.style.color           = ColText;
            closeBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.12f, 0.12f));
            closeBtn.style.borderTopLeftRadius     = 4;
            closeBtn.style.borderTopRightRadius    = 4;
            closeBtn.style.borderBottomLeftRadius  = 4;
            closeBtn.style.borderBottomRightRadius = 4;
            closeBtn.style.marginLeft = 8;

            header.Add(title);
            header.Add(_wisdomLabel);
            header.Add(closeBtn);

            // ── Node grid ─────────────────────────────────────────────────────
            _gridContainer = new VisualElement { name = "TalentGrid" };
            _gridContainer.style.marginBottom = 16;

            // ── Respec button ─────────────────────────────────────────────────
            int respecCost = HeroTalentCatalog.RespecCostCrystals;
            var respecBtn  = new Button(() => OnRespecClicked())
            {
                text = $"Respec — {respecCost} 💎",
                style =
                {
                    alignSelf       = Align.Center,
                    backgroundColor = new StyleColor(new Color(0.3f, 0.15f, 0.35f)),
                    color           = ColText,
                    paddingTop      = 8, paddingBottom = 8,
                    paddingLeft     = 20, paddingRight = 20,
                    borderTopLeftRadius     = 6, borderTopRightRadius    = 6,
                    borderBottomLeftRadius  = 6, borderBottomRightRadius = 6,
                }
            };

            card.Add(header);
            card.Add(_gridContainer);
            card.Add(respecBtn);
            _overlay.Add(card);

            doc.rootVisualElement.Add(_overlay);
            _root = doc.rootVisualElement;
        }

        // ── Grid rebuild (called on Open / hero change) ────────────────────────

        private void RebuildGrid()
        {
            if (_gridContainer == null) return;
            _gridContainer.Clear();

            var tree = string.IsNullOrEmpty(_currentHeroSlug)
                ? null
                : HeroTalentCatalog.GetTree(_currentHeroSlug);

            if (tree == null)
            {
                var msg = new Label($"No talent tree found for '{_currentHeroSlug}'.")
                {
                    style = { color = ColSubText, unityTextAlign = TextAnchor.MiddleCenter, marginTop = 12 }
                };
                _gridContainer.Add(msg);
                return;
            }

            // Build tier separator rows, each containing two node cards side-by-side.
            foreach (var tierKey in Tiers)
            {
                // Tier label
                var tierLabel = new Label(TierDisplayName(tierKey))
                {
                    style = { color = ColSubText, fontSize = 11, marginBottom = 4, marginTop = 6 }
                };
                _gridContainer.Add(tierLabel);

                // Row of two columns
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                foreach (var col in Columns)
                {
                    var node = FindNode(tree, tierKey, col);
                    row.Add(BuildNodeCard(node));
                }
                _gridContainer.Add(row);
            }
        }

        // ── Node card builder ──────────────────────────────────────────────────

        private VisualElement BuildNodeCard(HeroTalentNodeDef node)
        {
            var card = new VisualElement
            {
                style =
                {
                    flexGrow      = 1,
                    marginRight   = 8,
                    marginBottom  = 4,
                    paddingTop    = 10, paddingBottom = 10,
                    paddingLeft   = 10, paddingRight  = 10,
                    borderTopLeftRadius     = 8, borderTopRightRadius    = 8,
                    borderBottomLeftRadius  = 8, borderBottomRightRadius = 8,
                    borderTopWidth    = 1.5f, borderBottomWidth    = 1.5f,
                    borderLeftWidth   = 1.5f, borderRightWidth     = 1.5f,
                    minWidth = 160,
                }
            };

            if (node == null)
            {
                // Empty slot — transparent placeholder to keep the grid square.
                card.style.borderTopColor    = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0f));
                card.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0f));
                card.style.borderLeftColor   = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0f));
                card.style.borderRightColor  = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0f));
                return card;
            }

            // Derive initial visual state
            NodeState state = ComputeState(node.Id);
            ApplyCardStyle(card, state);

            var nameLabel = new Label(node.Name ?? node.Id)
            {
                style = { color = ColText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13 }
            };
            var descLabel = new Label(node.Description ?? string.Empty)
            {
                style = { color = ColSubText, fontSize = 11, marginTop = 2, whiteSpace = WhiteSpace.Normal }
            };
            var costLabel = new Label($"Cost: {node.Cost} Wisdom")
            {
                style = { color = state == NodeState.Unlocked ? ColUnlocked : ColSubText, fontSize = 11, marginTop = 4 }
            };

            card.Add(nameLabel);
            card.Add(descLabel);
            card.Add(costLabel);

            // Unlock button — only visible + enabled when Available
            var unlockBtn = new Button(() => OnUnlockClicked(node.Id))
            {
                text = state == NodeState.Unlocked ? "✓ Learned" : "Unlock",
            };
            unlockBtn.style.marginTop = 6;
            unlockBtn.style.color     = ColText;
            unlockBtn.style.backgroundColor = state == NodeState.Available
                ? new StyleColor(new Color(0.20f, 0.55f, 0.25f))
                : new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            unlockBtn.style.borderTopLeftRadius     = 4;
            unlockBtn.style.borderTopRightRadius    = 4;
            unlockBtn.style.borderBottomLeftRadius  = 4;
            unlockBtn.style.borderBottomRightRadius = 4;
            unlockBtn.SetEnabled(state == NodeState.Available);

            card.Add(unlockBtn);

            return card;
        }

        // ── Refresh — called after any Wisdom change ───────────────────────────

        private void Refresh()
        {
            int wisdom = WisdomCurrencyService.Instance != null
                ? WisdomCurrencyService.Instance.Wisdom
                : 0;

            if (_wisdomLabel != null)
                _wisdomLabel.text = $"Wisdom: {wisdom}";

            // Re-run state computation on every card button.
            if (_gridContainer == null) return;

            var tree = string.IsNullOrEmpty(_currentHeroSlug)
                ? null
                : HeroTalentCatalog.GetTree(_currentHeroSlug);
            if (tree == null) return;

            // Rebuild the grid so button states update (cheap — 6 nodes only).
            RebuildGrid();
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void OnUnlockClicked(string nodeId)
        {
            if (WisdomCurrencyService.Instance == null) return;
            bool ok = WisdomCurrencyService.Instance.Unlock(nodeId);
            if (!ok)
                Debug.Log($"[TalentTreePanel] Could not unlock '{nodeId}' — check Wisdom or prereqs.");
            // Refresh() fires automatically via Changed event.
        }

        private void OnRespecClicked()
        {
            if (string.IsNullOrEmpty(_currentHeroSlug)) return;

            int crystalCost = HeroTalentCatalog.RespecCostCrystals;
            var cost = ResourceCost.CrystalsOnly(crystalCost);
            if (EconomyService.Instance == null || !EconomyService.Instance.TrySpend(cost))
            {
                Debug.Log($"[TalentTreePanel] Respec failed — need {crystalCost} Crystals.");
                return;
            }

            WisdomCurrencyService.Instance?.RespecHero(_currentHeroSlug);
            Debug.Log($"[TalentTreePanel] Respec complete for '{_currentHeroSlug}'.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private enum NodeState { Locked, Available, Unlocked }

        private static NodeState ComputeState(string nodeId)
        {
            if (WisdomCurrencyService.Instance == null) return NodeState.Locked;

            if (WisdomCurrencyService.Instance.IsUnlocked(nodeId))
                return NodeState.Unlocked;

            // Use the catalog's full prereq + wisdom check
            var unlocked = WisdomCurrencyService.Instance.Unlocked as System.Collections.Generic.HashSet<string>
                        ?? new System.Collections.Generic.HashSet<string>(WisdomCurrencyService.Instance.Unlocked);
            bool canUnlock = HeroTalentCatalog.CanUnlock(
                nodeId,
                WisdomCurrencyService.Instance.Wisdom,
                unlocked);

            return canUnlock ? NodeState.Available : NodeState.Locked;
        }

        private static void ApplyCardStyle(VisualElement card, NodeState state)
        {
            StyleColor borderCol = state switch
            {
                NodeState.Unlocked  => ColUnlocked,
                NodeState.Available => ColAvailable,
                _                   => new StyleColor(new Color(0.28f, 0.28f, 0.28f)),
            };
            StyleColor bgCol = state switch
            {
                NodeState.Unlocked  => new StyleColor(new Color(0.22f, 0.18f, 0.04f)),
                NodeState.Available => new StyleColor(new Color(0.08f, 0.18f, 0.09f)),
                _                   => new StyleColor(new Color(0.14f, 0.14f, 0.16f)),
            };
            card.style.backgroundColor = bgCol;
            card.style.borderTopColor    = borderCol;
            card.style.borderBottomColor = borderCol;
            card.style.borderLeftColor   = borderCol;
            card.style.borderRightColor  = borderCol;
        }

        private static HeroTalentNodeDef FindNode(HeroTalentTreeDef tree, string tier, string col)
        {
            if (tree?.Nodes == null) return null;
            foreach (var n in tree.Nodes)
                if (n.Tier == tier && n.Column == col) return n;
            return null;
        }

        private static string TierDisplayName(string tierKey) => tierKey switch
        {
            "tier1" => "— Tier I —",
            "tier2" => "— Tier II —",
            "tier3" => "— Tier III —",
            _       => tierKey,
        };

        private void SetVisible(bool visible)
        {
            if (_overlay != null)
                _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
