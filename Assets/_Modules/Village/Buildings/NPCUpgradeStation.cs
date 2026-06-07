// =============================================================================
// NPCUpgradeStation — stationed NPC at a key building that offers tiered
// upgrades paid via the Economy class, with visual building transformation.
// -----------------------------------------------------------------------------
// Placed by VillageSceneBuilder at district buildings (Mill, Armorer, Forge,
// Lumbermill, Resource Upgrade, Jeweler).
//
// Interaction: Player approaches or activates → code-built modal (consistent
// with BuildPreviewModal style) showing current tier, benefits, ResourceCost.
// Confirm: EconomyService.Instance.TrySpend(cost) → upgrade visual (tier swap
// or StructureTierVisual animation) + register bonus (e.g. passive Economy
// grant tick or production boost).
//
// All resource handling routes through existing EconomyService (Grant/TrySpend
// / AddResource from WO-106). No duplicate wallets.
//
// Visual upgrade: For now swaps a "tier visual" child or calls StructureTierVisual
// if present on the building root. Future: full prefab swap per tier.
// =============================================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.State; // for Economy if needed, but use the Village one

namespace DeNelle.Village
{
    /// <summary>
    /// NPC stationed at a building offering upgrades. Ties directly to Economy.
    /// </summary>
    [RequireComponent(typeof(Collider))] // trigger or proximity
    public sealed class NPCUpgradeStation : MonoBehaviour
    {
        [Header("Identity")]
        public string BuildingName = "Workshop";
        public string ResourceTypeHint = "General"; // e.g. "Food", "Iron" for flavor

        [Header("Tiers")]
        public int CurrentTier = 1;
        public int MaxTier = 3;

        [Header("Economy Costs (base, scaled by tier)")]
        public ResourceCost BaseUpgradeCost = new ResourceCost(wood: 30, food: 20, iron: 10);

        [Header("Visual Root")]
        [Tooltip("The building visual root that will be upgraded (tier children or StructureTierVisual).")]
        public GameObject BuildingVisualRoot;

        [Header("NPC")]
        public Transform NpcStationPoint; // where the character stands
        public string NpcDialogueLine = "I can improve this building for the right resources.";

        private bool _uiOpen;
        private GameObject _upgradeUI;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !_uiOpen)
            {
                // WO-109: Prefer Yarn dialogue for stationed NPCs (leads to craft/equip/upgrade via commands).
                // The DialogueRunner + NPCCommandBridge was attached in builder PlaceNpcStation.
                var runner = GetComponent<Yarn.Unity.DialogueRunner>();
                if (runner != null && !runner.IsDialogueRunning)
                {
                    // Start the NPC-specific Yarn node (unique titles required across the .yarnproject).
                    // The .yarn files declare TalkToArmorer / TalkToForge etc. and embed the <<command:>> handlers.
                    string nodeTitle = "TalkTo" + (string.IsNullOrEmpty(BuildingName) ? "Generic" : BuildingName.Replace(" ", ""));
                    // Normalize common station names coming from the scene builder.
                    if (BuildingName.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0) nodeTitle = "TalkToForge";
                    else if (BuildingName.IndexOf("armorer", StringComparison.OrdinalIgnoreCase) >= 0) nodeTitle = "TalkToArmorer";
                    runner.StartDialogue(nodeTitle);
                    return;
                }
                // Fallback to direct UI (upgrade) if no runner or for non-Yarn stations.
                ShowUpgradeUI();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") && _uiOpen)
            {
                CloseUI();
            }
        }

        public void ShowUpgradeUI()
        {
            if (_uiOpen) return;
            _uiOpen = true;

            _upgradeUI = new GameObject("UpgradeUI_" + BuildingName);
            _upgradeUI.transform.SetParent(transform, false);

            var canvas = _upgradeUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            // Simple panel
            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_upgradeUI.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4f, 3f);
            rect.localPosition = new Vector3(0, 3f, 0);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.92f);

            // Title
            CreateText(panel.transform, $"{BuildingName} (Tier {CurrentTier}/{MaxTier})", new Vector2(0, 1.1f), 22, Color.white);

            // Cost preview
            var nextCost = GetNextTierCost();
            CreateText(panel.transform, $"Cost: {CostString(nextCost)}", new Vector2(0, 0.4f), 16, Color.yellow);

            // Benefits (flavor + future hook)
            CreateText(panel.transform, $"+ Productivity ({ResourceTypeHint})", new Vector2(0, -0.1f), 14, new Color(0.6f, 1f, 0.6f));

            // Buttons
            CreateButton(panel.transform, "Upgrade", new Vector2(-0.8f, -0.9f), TryUpgrade);
            CreateButton(panel.transform, "Close", new Vector2(0.8f, -0.9f), CloseUI);

            Debug.Log($"[NPCUpgradeStation] Opened upgrade UI for {BuildingName}");
        }

        private void CreateText(Transform parent, string txt, Vector2 anchored, int size, Color c)
        {
            var go = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = txt;
            tmp.fontSize = size;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = c;
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = anchored;
            r.sizeDelta = new Vector2(3.6f, 0.5f);
        }

        private void CreateButton(Transform parent, string label, Vector2 anchored, Action onClick)
        {
            var btnGO = new GameObject($"Btn_{label}", typeof(Button), typeof(Image));
            btnGO.transform.SetParent(parent, false);
            var rect = btnGO.GetComponent<RectTransform>();
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(1.4f, 0.5f);

            btnGO.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.15f);

            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txtGO = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            txtGO.transform.SetParent(btnGO.transform, false);
            var tmp = txtGO.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var tRect = txtGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;
        }

        private ResourceCost GetNextTierCost()
        {
            float scale = 1f + (CurrentTier - 1) * 0.6f;
            return new ResourceCost(
                (int)(BaseUpgradeCost.Wood * scale),
                (int)(BaseUpgradeCost.Food * scale),
                (int)(BaseUpgradeCost.Iron * scale),
                (int)(BaseUpgradeCost.Crystals * scale)
            );
        }

        private string CostString(ResourceCost c)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (c.Wood > 0) parts.Add($"{c.Wood} Wood");
            if (c.Food > 0) parts.Add($"{c.Food} Food");
            if (c.Iron > 0) parts.Add($"{c.Iron} Iron");
            if (c.Crystals > 0) parts.Add($"{c.Crystals} Crystals");
            return parts.Count > 0 ? string.Join(", ", parts) : "Free";
        }

        private void TryUpgrade()
        {
            if (CurrentTier >= MaxTier)
            {
                Debug.Log("[NPCUpgradeStation] Already max tier.");
                CloseUI();
                return;
            }

            var cost = GetNextTierCost();
            var econ = EconomyService.Instance;
            if (econ == null || !econ.TrySpend(cost))
            {
                Debug.LogWarning($"[NPCUpgradeStation] Cannot afford upgrade for {BuildingName}.");
                return;
            }

            CurrentTier++;

            // Visual upgrade (integrate with existing tier system)
            ApplyVisualUpgrade();

            // Economy benefit hook (example: grant a small immediate bonus + register for future)
            // In real system this could register a ProductionSource with Economy.
            econ.Grant(wood: 5, food: 5); // symbolic "first harvest boost"
            Debug.Log($"[NPCUpgradeStation] {BuildingName} upgraded to tier {CurrentTier}. Economy charged.");

            // Refresh UI or close
            CloseUI();
            // Re-open to show new tier (or leave closed)
            // ShowUpgradeUI();
        }

        private void ApplyVisualUpgrade()
        {
            GameSfx.PlayBuildingUpgrade();   // satisfying upgrade chime
            if (BuildingVisualRoot == null) BuildingVisualRoot = gameObject;

            // Prefer existing StructureTierVisual if present on the building
            var tierVis = BuildingVisualRoot.GetComponent<StructureTierVisual>();
            if (tierVis != null)
            {
                // Assume it has a SetTier or similar; call via reflection or extend later
                // For now, simple scale bump as "growth"
                BuildingVisualRoot.transform.localScale = Vector3.one * (0.95f + CurrentTier * 0.08f);
                // TODO: full anim / particle "construction" burst
                Debug.Log($"[NPCUpgradeStation] Applied tier visual via StructureTierVisual path for {BuildingName}");
                return;
            }

            // Fallback: simple "upgrade" by scaling + slight color shift on renderers (demo)
            BuildingVisualRoot.transform.localScale = Vector3.one * (0.95f + CurrentTier * 0.08f);
            foreach (var r in BuildingVisualRoot.GetComponentsInChildren<Renderer>())
            {
                if (r.material != null)
                {
                    // Slight "better materials" tint
                    var c = r.material.color;
                    r.material.color = Color.Lerp(c, new Color(0.95f, 0.92f, 0.85f), 0.3f);
                }
            }

            // Optional: spawn a small "upgrade complete" effect (if VFX available)
            // For now just log + scale as proof of visual transformation.
            Debug.Log($"[NPCUpgradeStation] Visual upgrade applied (scale + tint) for {BuildingName} tier {CurrentTier}");
        }

        private void CloseUI()
        {
            if (_upgradeUI != null) Destroy(_upgradeUI);
            _upgradeUI = null;
            _uiOpen = false;
        }
    }
}
