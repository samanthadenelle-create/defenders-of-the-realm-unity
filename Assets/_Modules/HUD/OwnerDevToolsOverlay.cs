// =============================================================================
// OwnerDevToolsOverlay — an OWNER-GATED, on-screen dev-tools button that ships in
// the PUBLISHED (release) WebGL build so the owner can test on MOBILE (Pi Browser
// has no keyboard / no console, so the F-key dev panels + Ctrl+Shift+A admin chord
// are unreachable there).
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (owner directive 2026-07-01): the existing dev surfaces
// (DebuggingController F9, DevPanel F1/F10, AdminOverlay Ctrl+Shift+A) are all
// keyboard-driven and/or #if DEVELOPMENT_BUILD-stripped, so NONE of them reach a
// mobile release player. This overlay is a RELEASE-SAFE, TOUCH-driven sibling that
// only appears for the signed-in OWNER Pi account ("samanthadenelle"). A pioneer
// never sees it (the button is not even built unless the Pi username matches).
//
// RELEASE-SAFE (BINDING): NO #if DEVELOPMENT_BUILD / UNITY_EDITOR gates, NO asmdef
//   defineConstraints changes. Only calls methods compiled into the release build:
//   • Direct   — DeNelle.HUD + DeNelle.Core (this asmdef references DeNelle.Core):
//                PiSignInController, SceneRouter, GameStateService, FeatureFlags,
//                FlowTrace, DebuggingController.Capture.
//   • Reflection — DeNelle.Village gameplay singletons (this asmdef does NOT
//                reference DeNelle.Village), using the SAME idiom AdminOverlay uses:
//                EconomyService / HeroProgression / WisdomCurrencyService / WaveManager.
//   These are REAL gameplay methods (not dev-only, not compile-stripped), so they
//   exist in the shipped .wasm.
//
// SELF-BOOTSTRAP: a RuntimeInitializeOnLoadMethod (AfterSceneLoad) spawns ONE
//   DontDestroyOnLoad host (mirrors DebuggingController.Bootstrap). Everything is
//   try/caught so it can NEVER break startup.
//
// OWNER GATE: reads DeNelle.Core.Platform.PiSignInController.SignedInUsername (may
//   already be set) AND subscribes to OnSignedIn (the username arrives async, a few
//   seconds after boot). The toggle button is built ONLY when the username equals
//   OwnerUsername (case-insensitive).
//
// MOBILE-SAFE UI: a ScreenSpaceOverlay uGUI Canvas at sortingOrder 5500 (above the
//   Pi sign-in button's 5000). A bottom-left toggle button (touch → Button.onClick)
//   opens a scrollable panel of uGUI tool buttons — no keyboard, no UITK gestures.
//   Bottom-left so it never collides with the top-right Pi sign-in button.
//
// ASMDEF: DeNelle.HUD (same assembly as DebuggingController / AdminOverlay). No new
//   assembly references were added.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;      // .Forget() for the UniTask-returning SceneRouter jumps
using DeNelle.Core;                 // SceneRouter, FeatureFlags, BattleParams
using DeNelle.Core.Diagnostics;     // FlowTrace
using DeNelle.Core.Platform;        // PiSignInController

namespace DeNelle.HUD
{
    /// <summary>
    /// Owner-only, touch-driven dev-tools overlay that ships in the release build. Dormant
    /// (no visible UI) for every account except the Pi owner; self-bootstraps, fully guarded.
    /// </summary>
    public sealed class OwnerDevToolsOverlay : MonoBehaviour
    {
        /// <summary>The Pi username that unlocks the overlay (case-insensitive).</summary>
        private const string OwnerUsername = "samanthadenelle";

        public static OwnerDevToolsOverlay Instance { get; private set; }

        private GameObject _canvasGo;
        private GameObject _panelGo;
        private Text _status;
        private bool _built;

        // ------------------------------------------------------------------
        // SELF-BOOTSTRAP (mirrors DebuggingController.Bootstrap)
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            try
            {
                var go = new GameObject("OwnerDevToolsOverlay");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<OwnerDevToolsOverlay>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OwnerDev] Bootstrap failed (non-fatal): " + e.Message);
            }
        }

        private void Start()
        {
            try
            {
                // The username may ALREADY be set (sign-in completed before this Start ran)…
                TryActivateForOwner(PiSignInController.SignedInUsername);
                // …but it usually arrives async a few seconds after boot — subscribe for that.
                PiSignInController.OnSignedIn += OnSignedIn;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OwnerDev] Start failed (non-fatal): " + e.Message);
            }
        }

        private void OnDestroy()
        {
            try { PiSignInController.OnSignedIn -= OnSignedIn; } catch { /* teardown, ignore */ }
            if (Instance == this) Instance = null;
        }

        // OnSignedIn is raised on the main thread (from PiSignInController.SignInAsync via
        // UniTask), so building UI here is safe.
        private void OnSignedIn(string uid, string username) => TryActivateForOwner(username);

        // ------------------------------------------------------------------
        // OWNER GATE
        // ------------------------------------------------------------------
        private void TryActivateForOwner(string username)
        {
            if (_built) return;
            if (string.IsNullOrEmpty(username)) return;
            if (!username.Equals(OwnerUsername, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                BuildOverlay();
                _built = true;
                FlowTrace.Step("OwnerDev", $"overlay ACTIVATED for owner '{username}' (release dev-tools shown).");
            }
            catch (Exception e)
            {
                FlowTrace.Warn("OwnerDev", $"BuildOverlay threw: {e.GetType().Name}: {e.Message}");
            }
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------
        private void BuildOverlay()
        {
            _canvasGo = new GameObject("OwnerDevCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5500;   // above the Pi sign-in button (5000)

            // --- always-visible toggle button (bottom-left, away from the Pi button) ---
            var toggle = MakeButton(_canvasGo.transform, "DEV", () => TogglePanel());
            var trt = toggle.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f);
            trt.pivot = new Vector2(0f, 0f);
            trt.anchoredPosition = new Vector2(10f, 10f);
            trt.sizeDelta = new Vector2(132f, 54f);
            toggle.GetComponent<Image>().color = new Color(0.55f, 0.16f, 0.16f, 0.95f); // owner-red

            // --- the scrollable tools panel (built once, hidden until toggled) ---
            BuildPanel();
            if (_panelGo != null) _panelGo.SetActive(false);
        }

        private void BuildPanel()
        {
            _panelGo = new GameObject("OwnerDevPanel", typeof(Image), typeof(ScrollRect));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            _panelGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.94f);
            var prt = _panelGo.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(10f, 72f);   // sits just above the toggle button
            prt.sizeDelta = new Vector2(360f, 480f);

            // Title strip (fixed, top).
            var title = MakeText(_panelGo.transform, "OWNER DEV TOOLS", 18, TextAnchor.MiddleCenter,
                                 new Color(1f, 0.85f, 0.4f, 1f));
            var tirt = title.GetComponent<RectTransform>();
            tirt.anchorMin = new Vector2(0f, 1f); tirt.anchorMax = new Vector2(1f, 1f);
            tirt.pivot = new Vector2(0.5f, 1f);
            tirt.sizeDelta = new Vector2(0f, 34f);
            tirt.anchoredPosition = Vector2.zero;

            // Status strip (fixed, bottom) — the mobile "console" (no F8/Editor.log on device).
            _status = MakeText(_panelGo.transform, "ready", 13, TextAnchor.MiddleLeft,
                               new Color(0.85f, 0.9f, 0.85f, 1f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            var srt = _status.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(-12f, 42f);
            srt.anchoredPosition = new Vector2(0f, 0f);

            // Viewport (clips the scrolling content between the title + status strips).
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(_panelGo.transform, false);
            var vrt = viewportGo.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(6f, 42f);    // leave the bottom status strip
            vrt.offsetMax = new Vector2(-6f, -34f);  // leave the top title strip

            // Content (vertical stack of tool buttons; auto-sizes so the ScrollRect can scroll).
            var contentGo = new GameObject("Content", typeof(RectTransform),
                                           typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, 0f);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = _panelGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.viewport = vrt;
            scroll.content = crt;

            PopulateTools(contentGo.transform);
        }

        /// <summary>All the tool rows. Each is a uGUI button that runs its action guarded.</summary>
        private void PopulateTools(Transform parent)
        {
            // --- Resources / progression (DeNelle.Village via reflection) ---
            AddTool(parent, "Give Resources (50k all)", GiveResources);
            AddTool(parent, "+1000 XP",                 () => AddHeroXp(1000f));
            AddTool(parent, "Set Level 10",             () => SetHeroLevel(10));
            AddTool(parent, "+100 Wisdom",              () => GiveWisdom(100));
            AddTool(parent, "Trigger next wave",        TriggerNextWave);

            // --- Scene jumps (DeNelle.Core.SceneRouter, direct) ---
            AddTool(parent, "Go: Castle (home hub)",    SceneRouter.GoCastle);
            AddTool(parent, "Go: Village",              SceneRouter.GoVillage);
            AddTool(parent, "Go: Hero Select",          SceneRouter.GoHeroSelect);
            AddTool(parent, "Go: Title",                SceneRouter.GoTitle);
            AddTool(parent, "Go: Dungeon (Healer)",     () => SceneRouter.GoDungeon("HealersCottage").Forget());
            AddTool(parent, "Go: Battle (ATB test)",    () => SceneRouter.GoBattle(new BattleParams { Wave = 1 }).Forget());

            // --- Feature-flag toggles (PlayerPrefs; FeatureFlags reads them live) ---
            AddFlagToggle(parent, "devhotkeys",   () => FeatureFlags.DevHotkeys);
            AddFlagToggle(parent, "noautoheal",   () => FeatureFlags.NoAutoHeal);
            AddFlagToggle(parent, "lockon",       () => FeatureFlags.LockOn);
            AddFlagToggle(parent, "basebuilding", () => FeatureFlags.BaseBuilding);
            // 07-07 sheathed-pose A/B: ON = drawn offset composes pos+rot onto the back pose
            // (the 0492d7dc behavior); OFF = pos-only nudge. Re-equip applies it on next
            // sheathe re-parent (walk/combat flip), no restart needed.
            AddFlagToggle(parent, "sheathdrawnrot", () => FeatureFlags.SheathedDrawnRotFallback);
            // (WO-682: the ff.strategicplacement toggle was removed — strategic placement
            // is always on; the flag no longer exists.)

            // --- State / diagnostics ---
            AddTool(parent, "Reset to New Game",        ResetToNewGame);
            AddTool(parent, "Dump state (F8 capture)",  () => DebuggingController.Capture("owner-dev"));
        }

        // ------------------------------------------------------------------
        // TOOL IMPLEMENTATIONS
        // ------------------------------------------------------------------

        // Give a full base of spendable resources through the SAME EconomyService wallet the
        // shop + upgrade flow read. Reflected — HUD asmdef can't reference DeNelle.Village
        // (identical idiom to AdminOverlay.OnLoadResources).
        private void GiveResources()
        {
            var eco = ResolveVillageSingleton("DeNelle.Village.EconomyService", out var ecoType);
            if (eco == null) { SetStatus("Resources: EconomyService not alive yet."); return; }

            // GrantSpendable(int wood=0, int food=0, int iron=0, int crystals=0) — mirrors
            // Wood/Iron into GameState so the upgrade flow can spend them too.
            var grant = ecoType.GetMethod("GrantSpendable",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            if (grant == null) { SetStatus("Resources: GrantSpendable(int,int,int,int) not found."); return; }
            grant.Invoke(eco, new object[] { 50000, 25000, 50000, 25000 }); // wood, food, iron, crystals

            // AddCoins(int) tops up the shop/sell gold wallet (GrantSpendable has no coins arg).
            var addCoins = ecoType.GetMethod("AddCoins",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            addCoins?.Invoke(eco, new object[] { 50000 });

            SetStatus("Loaded: +50k Gold/Wood/Iron, +25k Food/Crystals.");
        }

        private void AddHeroXp(float amount)
        {
            var hp = ResolveVillageSingleton("DeNelle.Village.HeroProgression", out var hpType);
            if (hp == null) { SetStatus("XP: HeroProgression not in scene yet."); return; }
            var addXp = hpType.GetMethod("AddXp",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);
            if (addXp == null) { SetStatus("XP: HeroProgression.AddXp(float) not found."); return; }
            addXp.Invoke(hp, new object[] { amount });
            SetStatus($"+{amount:F0} XP granted.");
        }

        // Mirror AdminOverlay.OnSetHeroLevel: repeated AddXp(XpToNext + 1) until the target level,
        // so each crossed level runs the real ApplyLevelRewards (banks Wisdom + a skill point).
        private void SetHeroLevel(int target)
        {
            target = Mathf.Max(1, target);
            var hp = ResolveVillageSingleton("DeNelle.Village.HeroProgression", out var hpType);
            if (hp == null) { SetStatus("Level: HeroProgression not in scene yet."); return; }

            var levelProp = hpType.GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
            var xpToNextProp = hpType.GetProperty("XpToNext", BindingFlags.Public | BindingFlags.Instance);
            var addXp = hpType.GetMethod("AddXp",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);
            if (levelProp == null || xpToNextProp == null || addXp == null)
            {
                SetStatus("Level: HeroProgression API (Level/XpToNext/AddXp) not found.");
                return;
            }

            int LevelNow() => levelProp.GetValue(hp) is int l ? l : 0;
            float XpToNextNow() => xpToNextProp.GetValue(hp) is float x ? x : 0f;

            int guard = 0;
            while (LevelNow() < target && guard++ < 500)
                addXp.Invoke(hp, new object[] { XpToNextNow() + 1f });

            SetStatus($"Set hero to Lv.{LevelNow()} (target {target}).");
        }

        private void GiveWisdom(int amount)
        {
            var wis = ResolveVillageSingleton("DeNelle.Village.Talents.WisdomCurrencyService", out var wisType);
            if (wis == null) { SetStatus("Wisdom: WisdomCurrencyService not in scene yet."); return; }
            var grant = wisType.GetMethod("Grant",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            if (grant == null) { SetStatus("Wisdom: WisdomCurrencyService.Grant(int) not found."); return; }
            grant.Invoke(wis, new object[] { amount });
            SetStatus($"+{amount} Wisdom granted.");
        }

        private void TriggerNextWave()
        {
            // Re-find each time: a cached ref goes stale across scene loads (Unity fake-null on a
            // destroyed WaveManager doesn't trip a plain reference-equality guard) — see AdminOverlay.OnTriggerWave.
            var wmType = Type.GetType("DeNelle.Village.WaveManager, DeNelle.Village");
            var wm = wmType != null ? UnityEngine.Object.FindAnyObjectByType(wmType) : null;
            if (wm == null) { SetStatus("Wave: no WaveManager in this scene."); return; }
            var m = wmType.GetMethod("ForceSpawnNextWaveNow", BindingFlags.Public | BindingFlags.Instance);
            if (m == null) { SetStatus("Wave: ForceSpawnNextWaveNow() not found."); return; }
            m.Invoke(wm, null);
            SetStatus("Jumped to next wave (spawns immediately).");
        }

        // GameStateService lives in DeNelle.Core (referenced) → call directly.
        private void ResetToNewGame()
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            if (svc == null) { SetStatus("Reset: GameStateService not alive yet."); return; }
            svc.ResetToNewGame();
            SetStatus("Reset to New Game (progression wiped).");
        }

        // ------------------------------------------------------------------
        // FEATURE-FLAG TOGGLE (PlayerPrefs "ff.<name>" = 0/1 — FeatureFlags reads live)
        // ------------------------------------------------------------------
        private void AddFlagToggle(Transform parent, string flagKey, Func<bool> resolved)
        {
            Button btn = null;
            btn = MakeButton(parent, FlagLabel(flagKey, resolved), () =>
            {
                FlowTrace.Step("OwnerDev", $"tool tapped: toggle ff.{flagKey}");
                try
                {
                    bool on = !resolved();   // resolved current value, then invert
                    PlayerPrefs.SetInt("ff." + flagKey, on ? 1 : 0);
                    PlayerPrefs.Save();
                    var lbl = btn != null ? btn.GetComponentInChildren<Text>() : null;
                    if (lbl != null) lbl.text = FlagLabel(flagKey, resolved);
                    SetStatus($"ff.{flagKey} = {(on ? "ON" : "OFF")} (live).");
                }
                catch (Exception e)
                {
                    FlowTrace.Warn("OwnerDev", $"toggle ff.{flagKey} FAILED: {e.GetType().Name}: {e.Message}");
                    SetStatus($"toggle ff.{flagKey} FAILED: {e.Message}");
                }
            });
            AddLayout(btn.gameObject);
        }

        private static string FlagLabel(string flagKey, Func<bool> resolved)
        {
            bool on = false;
            try { on = resolved(); } catch { /* default off label */ }
            return $"ff.{flagKey}: {(on ? "ON" : "OFF")}";
        }

        // ------------------------------------------------------------------
        // UI HELPERS
        // ------------------------------------------------------------------
        private void TogglePanel()
        {
            if (_panelGo == null) return;
            bool show = !_panelGo.activeSelf;
            _panelGo.SetActive(show);
            FlowTrace.Step("OwnerDev", $"panel {(show ? "OPENED" : "closed")}.");
        }

        /// <summary>Adds a tool button to <paramref name="parent"/> whose action runs GUARDED
        /// (FlowTrace.Step on tap, FlowTrace.Warn on throw — never breaks the overlay).</summary>
        private void AddTool(Transform parent, string label, Action action)
        {
            var btn = MakeButton(parent, label, () => RunTool(label, action));
            AddLayout(btn.gameObject);
        }

        private void RunTool(string label, Action action)
        {
            FlowTrace.Step("OwnerDev", $"tool tapped: {label}");
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                FlowTrace.Warn("OwnerDev", $"tool '{label}' FAILED: {e.GetType().Name}: {e.Message}");
                SetStatus($"{label} FAILED: {e.Message}");
            }
            // Close the panel after a tool runs so it stops covering the game (2026-07-01 fix): it was
            // eating the very next tap. Critical for "Dump state" — its capture-next-click must land on
            // the game element the owner wants inspected (e.g. the diamond HUD), NOT on this panel.
            try { if (_panelGo != null) _panelGo.SetActive(false); } catch { /* never break a tool */ }
        }

        private static void AddLayout(GameObject buttonGo)
        {
            var le = buttonGo.AddComponent<LayoutElement>();
            le.minHeight = 46f;
            le.preferredHeight = 46f;
        }

        private static Button MakeButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.20f, 0.22f, 0.28f, 0.98f);
            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = MakeText(go.transform, label, 16, TextAnchor.MiddleCenter, Color.white);
            var lrt = txt.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 2f); lrt.offsetMax = new Vector2(-8f, -2f);
            txt.raycastTarget = false;
            return btn;
        }

        private static Text MakeText(Transform parent, string content, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            return t;
        }

        private void SetStatus(string s)
        {
            if (_status != null) _status.text = s;
        }

        // ------------------------------------------------------------------
        // REFLECTION HELPER (DeNelle.Village singletons — same idiom as AdminOverlay)
        // ------------------------------------------------------------------
        private static object ResolveVillageSingleton(string fullTypeName, out Type type)
        {
            type = Type.GetType(fullTypeName + ", DeNelle.Village");
            if (type == null) return null;
            var prop = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null);
        }
    }
}
