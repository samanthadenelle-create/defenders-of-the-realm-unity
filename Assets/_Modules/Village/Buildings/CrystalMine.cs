// =============================================================================
// CrystalMine — passive Aether Crystal generator; awards crystals per wave.
// -----------------------------------------------------------------------------
// Place this component on the "CrystalMine" GameObject in the village scene.
// The mine has three levels:
//
//   L1 (just built)  — no crystal yield; serves as a presence on the map.
//   L2               — 50% chance of +1 Crystal per wave.
//   L3 (max level)   — guaranteed +1 Crystal per wave.
//
// Crystal yield fires on WaveManager.OnWaveCleared via CrystalEconomy.AddCrystals.
// Upgrade cost is paid in Coins from GameStateService. The mine cannot be
// damaged by enemies (it is not an IDamageableStructure).
//
// Proximity interaction mirrors DungeonPortal / MarketplaceInteractor:
//   • 0.15 s throttled proximity check.
//   • "[F] Upgrade Crystal Mine" prompt bubble appears when in range.
//   • F-key shows a simple upgrade confirm (cost label) via InjectUpgradeUI().
//   • Escape / ✕ button dismisses.
//
// Scene setup:
//   1. Place a GameObject named "CrystalMine" in the village scene.
//   2. Add this component.
//   3. Optionally assign L1/L2/L3 visual prefabs. Falls back to a tinted cube.
//   4. Add it to the same persistent zone as CrystalEconomy so both live in scene.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class CrystalMine : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Levels")]
        [Tooltip("Visual prefab shown at Level 1. Null → procedural crystal-blue placeholder.")]
        [SerializeField] private GameObject _level1Prefab;
        [Tooltip("Visual prefab shown at Level 2.")]
        [SerializeField] private GameObject _level2Prefab;
        [Tooltip("Visual prefab shown at Level 3 (max level, yielding crystals every wave).")]
        [SerializeField] private GameObject _level3Prefab;

        [Tooltip("WO-110: when true, skip building the placeholder/prefab visual — an external " +
                 "crystal mesh (+ CrystalVisual spin/pulse) is the mine's body. Gameplay unchanged.")]
        [SerializeField] private bool _useExternalVisual = false;

        [Header("Upgrade costs (Coins)")]
        [Tooltip("Coin cost to upgrade from Level 1 to Level 2.")]
        [SerializeField] private int _costL1toL2 = 200;
        [Tooltip("Coin cost to upgrade from Level 2 to Level 3.")]
        [SerializeField] private int _costL2toL3 = 400;

        [Header("Proximity")]
        [SerializeField] private float _promptHeight  = 3.5f;
        [SerializeField, Min(1f)] private float _activateRadius = 3.5f;

        [Header("UI (optional UIDocument for upgrade panel)")]
        [Tooltip("Optional UIDocument root. If null, a simple world-space prompt is used.")]
        [SerializeField] private GameObject _upgradeUiRoot;

        // ── Constants ─────────────────────────────────────────────────────────

        public const int MaxLevel = 3;
        private const float CheckInterval = 0.15f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private int _currentLevel = 1;
        private Transform _hero;
        private HeroLocomotion _heroLoco;
        private bool _heroFound;
        private bool _isInRange;
        private bool _uiOpen;
        private bool _awaitingSimpleConfirm;   // bug-triage P1: gate simple-mode upgrade behind a 2nd, deliberate F press
        private GameObject _promptGo;
        private float _nextCheck;
        private WaveManager _wave;
        private GameObject _currentVisual;

        public int CurrentLevel => _currentLevel;
        public bool IsMaxLevel => _currentLevel >= MaxLevel;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            ResolveHero();
            ResolveWave();
            ApplyVisual();

            if (_upgradeUiRoot != null) _upgradeUiRoot.SetActive(false);
        }

        private void OnEnable()
        {
            SubscribeToWave();
        }

        private void OnDisable()
        {
            UnsubscribeFromWave();
            if (_uiOpen) CloseUpgradeUI();
        }

        private void Update()
        {
            if (!_heroFound) { ResolveHero(); return; }
            if (_hero == null) { _heroFound = false; return; }

            if (_uiOpen)
            {
                // bug-triage P1: simple-mode upgrade now requires a 2nd, deliberate F press
                // (the first press only opens the confirm bubble; it no longer spends coins).
                if (_awaitingSimpleConfirm && Input.GetKeyDown(KeyCode.F))
                {
                    _awaitingSimpleConfirm = false;
                    TryUpgrade();
                    CloseUpgradeUI();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape)) CloseUpgradeUI();
                return;
            }

            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + CheckInterval;
                float sqr = (_hero.position - transform.position).sqrMagnitude;
                bool nowIn = sqr <= _activateRadius * _activateRadius;
                if (nowIn != _isInRange)
                {
                    _isInRange = nowIn;
                    if (_isInRange) ShowPrompt();
                    else            HidePrompt();
                }
            }

            if (_isInRange && Input.GetKeyDown(KeyCode.F))
                OpenUpgradeUI();
        }

        // ── Wave crystal yield ────────────────────────────────────────────────

        private void OnWaveCleared(int waveId)
        {
            if (_currentLevel < MaxLevel) return;

            // L3: guaranteed +1 crystal. L2 would be 50% — extend here if desired.
            var economy = CrystalEconomy.Instance;
            if (economy == null)
            {
                Debug.LogWarning("[CrystalMine] CrystalEconomy service not found — no crystal awarded.");
                return;
            }
            economy.AddCrystals(1);
            Debug.Log($"[CrystalMine] Wave {waveId} cleared — +1 Aether Crystal awarded (L3 mine).");
        }

        // ── Upgrade ───────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to upgrade the mine one level. Deducts coins from GameState.
        /// Returns false if already at max level or coins are insufficient.
        /// </summary>
        public bool TryUpgrade()
        {
            if (_currentLevel >= MaxLevel)
            {
                Debug.Log("[CrystalMine] Already at max level.");
                return false;
            }

            int cost = _currentLevel == 1 ? _costL1toL2 : _costL2toL3;

            var svc = GameStateService.Instance;
            if (svc?.State == null)
            {
                Debug.LogWarning("[CrystalMine] GameStateService unavailable.");
                return false;
            }

            var res = svc.State.Resources;
            if (res.Coins < cost)
            {
                Debug.Log($"[CrystalMine] Upgrade requires {cost} Coins — have {res.Coins}.");
                return false;
            }

            // Deduct coins and save.
            var r = res;
            r.Coins -= cost;
            svc.State.Resources = r;
            svc.Save();

            _currentLevel++;
            ApplyVisual();

            // Re-subscribe so the new level is active immediately.
            SubscribeToWave();

            Debug.Log($"[CrystalMine] Upgraded to Level {_currentLevel}. Coins remaining: {r.Coins}.");
            return true;
        }

        // ── Upgrade UI ────────────────────────────────────────────────────────

        private void OpenUpgradeUI()
        {
            HidePrompt();
            _uiOpen = true;
            if (_heroLoco != null) _heroLoco.enabled = false;

            if (_upgradeUiRoot != null)
            {
                _upgradeUiRoot.SetActive(true);
                InjectUpgradePanel();
            }
            else
            {
                // Simple world-space label showing cost and F/Esc hint.
                ShowSimpleUpgradePrompt();
            }
        }

        private void CloseUpgradeUI()
        {
            _uiOpen = false;
            _awaitingSimpleConfirm = false;
            if (_upgradeUiRoot != null) _upgradeUiRoot.SetActive(false);
            if (_heroLoco != null) _heroLoco.enabled = true;
        }

        /// <summary>
        /// Injects a minimal upgrade panel into the assigned UIDocument root —
        /// shows current level, upgrade cost, Confirm + Close buttons.
        /// </summary>
        private void InjectUpgradePanel()
        {
            var doc = _upgradeUiRoot?.GetComponent<UIDocument>();
            if (doc?.rootVisualElement == null) return;

            var root = doc.rootVisualElement;
            root.Clear();

            // Outer container.
            var panel = new VisualElement();
            panel.style.position       = Position.Absolute;
            panel.style.top            = 20; panel.style.left  = 20;
            panel.style.right          = 20; panel.style.bottom = 20;
            panel.style.backgroundColor = new StyleColor(new Color(0.05f, 0.02f, 0.12f, 0.94f));
            panel.style.borderTopLeftRadius     = 10;
            panel.style.borderTopRightRadius    = 10;
            panel.style.borderBottomLeftRadius  = 10;
            panel.style.borderBottomRightRadius = 10;
            panel.style.paddingTop = panel.style.paddingBottom =
            panel.style.paddingLeft = panel.style.paddingRight = 20;
            root.Add(panel);

            // Title.
            var title = new Label("✦  Crystal Mine");
            title.style.fontSize = 22;
            title.style.color = new StyleColor(new Color(0.80f, 0.60f, 1.0f));
            title.style.marginBottom = 12;
            panel.Add(title);

            // Level display.
            var levelLbl = new Label($"Current Level: {_currentLevel} / {MaxLevel}");
            levelLbl.style.fontSize = 15;
            levelLbl.style.color = new StyleColor(new Color(0.90f, 0.85f, 1.0f));
            panel.Add(levelLbl);

            // Yield description.
            string yieldText = _currentLevel switch
            {
                1 => "Yields: Nothing yet — upgrade to begin mining.",
                2 => "Yields: 50% chance of +1 Crystal per wave.",
                _ => "Yields: +1 Aether Crystal per wave (max level).",
            };
            var yieldLbl = new Label(yieldText);
            yieldLbl.style.fontSize = 13;
            yieldLbl.style.color = new StyleColor(new Color(0.70f, 0.90f, 0.70f));
            yieldLbl.style.marginTop = 6;
            yieldLbl.style.marginBottom = 16;
            panel.Add(yieldLbl);

            if (!IsMaxLevel)
            {
                int cost = _currentLevel == 1 ? _costL1toL2 : _costL2toL3;
                int coins = (int)(GameStateService.Instance?.State?.Resources.Coins ?? 0);
                bool canAfford = coins >= cost;

                var costLbl = new Label($"Upgrade cost: {cost} Coins  (you have {coins})");
                costLbl.style.fontSize = 14;
                costLbl.style.color = new StyleColor(canAfford
                    ? new Color(1f, 0.85f, 0.3f)
                    : new Color(1f, 0.3f, 0.3f));
                costLbl.style.marginBottom = 12;
                panel.Add(costLbl);

                var upgradeBtn = new Button(() =>
                {
                    if (TryUpgrade()) CloseUpgradeUI();
                    else InjectUpgradePanel(); // refresh to show updated coins
                })
                { text = canAfford ? "Upgrade ✦" : "Need more Coins" };
                upgradeBtn.SetEnabled(canAfford);
                StyleButton(upgradeBtn, new Color(0.20f, 0.08f, 0.38f));
                upgradeBtn.style.marginBottom = 8;
                panel.Add(upgradeBtn);
            }
            else
            {
                var maxLbl = new Label("✦  Fully upgraded — harvesting crystals each wave.");
                maxLbl.style.fontSize = 14;
                maxLbl.style.color = new StyleColor(new Color(0.80f, 0.60f, 1.0f));
                maxLbl.style.marginBottom = 12;
                panel.Add(maxLbl);
            }

            var closeBtn = new Button(CloseUpgradeUI) { text = "✕  Close" };
            StyleButton(closeBtn, new Color(0.18f, 0.06f, 0.30f));
            panel.Add(closeBtn);
        }

        private static void StyleButton(Button btn, Color bgColor)
        {
            btn.style.paddingTop = btn.style.paddingBottom = 8;
            btn.style.paddingLeft = btn.style.paddingRight = 20;
            btn.style.fontSize = 14;
            btn.style.backgroundColor = new StyleColor(bgColor);
            btn.style.color = new StyleColor(new Color(0.95f, 0.88f, 1.0f));
            btn.style.borderTopLeftRadius     = btn.style.borderTopRightRadius    = 6;
            btn.style.borderBottomLeftRadius  = btn.style.borderBottomRightRadius = 6;
        }

        /// <summary>Fallback when no UIDocument is assigned — shows a world-space prompt above the mine.</summary>
        private void ShowSimpleUpgradePrompt()
        {
            if (IsMaxLevel)
            {
                _promptGo = BuildBubble("✦  Max Level — crystals active",
                    _promptHeight + 0.5f,
                    new Color(0.10f, 0.04f, 0.22f, 0.96f),
                    new Color(0.70f, 0.50f, 1.0f));
            }
            else
            {
                int cost = _currentLevel == 1 ? _costL1toL2 : _costL2toL3;
                _promptGo = BuildBubble($"〔 F 〕 Confirm Upgrade — {cost} Coins",
                    _promptHeight + 0.5f,
                    new Color(0.10f, 0.04f, 0.22f, 0.96f),
                    new Color(0.70f, 0.50f, 1.0f));
                // bug-triage P1: show a confirm bubble and wait for a 2nd F press in Update.
                // Keep _uiOpen true so the standard F-path doesn't re-open. Do NOT spend now.
                _awaitingSimpleConfirm = true;
            }
        }

        // ── Visual ────────────────────────────────────────────────────────────

        private void ApplyVisual()
        {
            if (_currentVisual != null) Destroy(_currentVisual);

            // WO-110: an external crystal mesh (+ CrystalVisual) is the body — don't build our own.
            if (_useExternalVisual) return;

            GameObject prefab = _currentLevel switch
            {
                1 => _level1Prefab,
                2 => _level2Prefab,
                _ => _level3Prefab,
            };

            if (prefab != null)
            {
                _currentVisual = Instantiate(prefab, transform);
                _currentVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else
            {
                _currentVisual = BuildPlaceholder(_currentLevel);
            }
        }

        private GameObject BuildPlaceholder(int level)
        {
            // Octahedron-ish cluster from overlapping cubes — minimal but readable.
            var go = new GameObject($"CrystalMineVisual_L{level}");
            go.transform.SetParent(transform, false);

            Color tint = level switch
            {
                1 => new Color(0.35f, 0.20f, 0.65f),   // dim purple
                2 => new Color(0.50f, 0.30f, 0.90f),   // brighter violet
                _ => new Color(0.70f, 0.50f, 1.00f),   // glowing aether
            };

            float h = 0.8f + level * 0.4f;
            AddCrystalShard(go, tint, new Vector3(0f,   h * 0.5f, 0f), new Vector3(0.4f, h, 0.4f));
            AddCrystalShard(go, tint, new Vector3(0.3f, h * 0.3f, 0.1f), new Vector3(0.25f, h * 0.7f, 0.25f), 15f);
            AddCrystalShard(go, tint, new Vector3(-0.25f, h * 0.25f, -0.1f), new Vector3(0.22f, h * 0.6f, 0.22f), -10f);
            if (level >= 2)
                AddCrystalShard(go, tint, new Vector3(0.0f, h * 0.2f, -0.3f), new Vector3(0.2f, h * 0.5f, 0.2f), 20f);
            if (level >= 3)
                AddCrystalShard(go, tint, new Vector3(-0.15f, h * 0.35f, 0.3f), new Vector3(0.18f, h * 0.55f, 0.18f), -25f);

            return go;
        }

        private void AddCrystalShard(GameObject parent, Color tint, Vector3 localPos, Vector3 localScale, float yRot = 0f)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Shard";
            DestroyImmediate(shard.GetComponent<Collider>());
            shard.transform.SetParent(parent.transform, false);
            shard.transform.localPosition = localPos;
            shard.transform.localScale    = localScale;
            shard.transform.localRotation = Quaternion.Euler(0f, yRot, 15f);

            var rend = shard.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");
                var mat = new Material(s);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                else mat.color = tint;
                rend.sharedMaterial = mat;
            }
        }

        // ── Prompt bubble ─────────────────────────────────────────────────────

        private void ShowPrompt()
        {
            string label = IsMaxLevel
                ? "✦  Crystal Mine — Active"
                : $"〔 F 〕  Upgrade Mine  (L{_currentLevel}→{_currentLevel + 1})";

            _promptGo = BuildBubble(label, _promptHeight,
                new Color(0.06f, 0.02f, 0.14f, 0.96f),
                new Color(0.65f, 0.45f, 1.00f));
        }

        private void HidePrompt()
        {
            if (_promptGo != null) Destroy(_promptGo);
            _promptGo = null;
        }

        private GameObject BuildBubble(string text, float localY, Color bgColor, Color outlineColor)
        {
            var go = new GameObject("CrystalMinePrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * localY;

            float charsApprox = Mathf.Max(text.Length, 8);
            float w = Mathf.Clamp(charsApprox * 0.10f + 0.4f, 1.2f, 4.0f);
            float h = 0.38f;

            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            outline.transform.localScale    = new Vector3(w + 0.06f, h + 0.06f, 1f);
            ApplyFlat(outline.GetComponent<Renderer>(), outlineColor);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            bg.transform.localScale    = new Vector3(w, h, 1f);
            ApplyFlat(bg.GetComponent<Renderer>(), bgColor);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localScale = Vector3.one * 0.055f;
            var tm = txtGo.AddComponent<TextMesh>();
            tm.text = text; tm.fontSize = 96; tm.characterSize = 0.30f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.90f, 1.00f);

            var billboard = go.AddComponent<PromptBillboard>();
            billboard.Camera = Camera.main;
            return go;
        }

        private static void ApplyFlat(Renderer renderer, Color colour)
        {
            if (renderer == null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (s == null) return;
            var mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     colour);
            renderer.sharedMaterial = mat;
        }

        // ── Wave subscription ─────────────────────────────────────────────────

        private void SubscribeToWave()
        {
            // bug-triage P1: resolve FIRST, then detach+attach on the manager we actually use
            // (RemoveListener is idempotent). The old order unsubscribed from a stale _wave
            // before ResolveWave overwrote it -> leaked listeners / duplicated crystal yield.
            ResolveWave();
            if (_wave == null) return;
            _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            _wave.OnWaveCleared.AddListener(OnWaveCleared);
        }

        private void UnsubscribeFromWave()
        {
            if (_wave != null) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        private void ResolveWave()
        {
            var found = FindObjectsByType<WaveManager>(FindObjectsSortMode.None);
            _wave = found.Length > 0 ? found[0] : null;
        }

        // ── Hero resolution ───────────────────────────────────────────────────

        private void ResolveHero()
        {
            if (_heroFound) return;
            var loco = FindObjectOfType<HeroLocomotion>();
            if (loco == null) return;
            _hero = loco.transform;
            _heroLoco = loco;
            _heroFound = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.65f, 0.45f, 1.0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _activateRadius);
        }
#endif
    }
}
