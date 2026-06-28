// =============================================================================
// SocialAccessCluster — WO-563: a reachable, TOUCH-FRIENDLY entry point for three
// finished-but-unreachable panels (gap audit): Clan Chat, Leaderboard, and the
// Music Jukebox. Each had only a keyboard hotkey (Y/L/J — several removed or gated
// behind DevHotkeys), so on mobile/WebGL they could never be opened. This builds a
// small uGUI button cluster on the RIGHT edge (near the rest of the HUD chrome),
// using the shared ElarionUiKit chrome, each button opening its panel.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// HOW IT OPENS EACH PANEL:
//   • Clan Chat / Leaderboard — same assembly (DeNelle.HUD) → call Toggle() directly.
//   • Music Jukebox — lives in DeNelle.Audio (HUD → Core only, no Audio asmdef ref) →
//     resolved + toggled by reflection (the same cross-asmdef pattern the panel
//     bootstraps already use to find the hero). MusicSelectionPanel.Toggle() is public.
//
// Code-built uGUI (NO UXML — CLAUDE.md §8). Self-bootstraps once per gameplay scene,
// skips Title (no hero) + enemy-owned RAID scenes (WO-550, mirrors the panels' own
// bootstraps so the buttons never point at a panel that wasn't spawned). Hidden during
// battle so it never overlaps the 9-zone FOCUS column on the mid-right.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    /// <summary>Touch-friendly access buttons for Clan Chat / Leaderboard / Music (WO-563).</summary>
    public sealed class SocialAccessCluster : MonoBehaviour
    {
        // Reflection handle for the cross-asmdef jukebox (DeNelle.Audio).
        private static System.Type _musicType;

        private Canvas _canvas;
        private RectTransform _container;
        private float _battlePollTimer;

        // ── Bootstrap (mirrors ClanChatPanelBootstrap / DailyQuestHudBootstrap) ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnInScene(scene);

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;

            // WO-550: the social panels do NOT bootstrap in enemy-owned RAID scenes (Village2),
            // so their access buttons must not either — gate on the ACTIVE scene (player context).
            if (HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "SocialAccessCluster suppressed in enemy-owned scene (WO-550/563)");
                return;
            }

            // GLOBAL dedupe — one instance across all loaded scenes.
            foreach (var existing in FindObjectsByType<SocialAccessCluster>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate SocialAccessCluster suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var go = new GameObject("SocialAccessCluster");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<SocialAccessCluster>();
            FlowTrace.Step("UI", "SocialAccessCluster created (WO-563 touch access to chat/ranks/jukebox).");
        }

        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = Object.FindObjectOfType(t) as Component;
            return obj != null ? obj.transform : null;
        }

        private void Awake()
        {
            EnsureEventSystem();
            Build();
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;   // above the base HUD chrome, below full-screen modals
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // RIGHT-edge vertical strip (mid-upper) — clear of the top chrome + the bottom
            // ability/joystick zones, and reachable for a right thumb. Tunable.
            _container = NewRect("SocialCluster", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            _container.pivot = new Vector2(1f, 0.5f);
            _container.sizeDelta = new Vector2(132f, 210f);
            _container.anchoredPosition = new Vector2(-12f, 70f);

            // Three stacked buttons (shared ElarionUiKit chrome).
            ElarionUiKit.Button(_container, "Chat",  ElarionUiKit.ButtonKind.Quiet,
                                new Vector2(0f, 0.69f), new Vector2(1f, 1.0f),  OpenClanChat);
            ElarionUiKit.Button(_container, "Ranks", ElarionUiKit.ButtonKind.Quiet,
                                new Vector2(0f, 0.345f), new Vector2(1f, 0.655f), OpenLeaderboard);
            ElarionUiKit.Button(_container, "Music", ElarionUiKit.ButtonKind.Quiet,
                                new Vector2(0f, 0.0f), new Vector2(1f, 0.31f), OpenJukebox);
        }

        private void Update()
        {
            // Keep the social access strip out of the way during battle — the 9-zone FOCUS column
            // owns the mid-right then. Cheap poll (BattleLock is the Core-clean in-battle probe).
            _battlePollTimer -= Time.unscaledDeltaTime;
            if (_battlePollTimer > 0f) return;
            _battlePollTimer = 0.4f;
            if (_container != null)
            {
                bool show = !DeNelle.Core.Combat.BattleLock.IsInBattle();
                if (_container.gameObject.activeSelf != show) _container.gameObject.SetActive(show);
            }
        }

        // ── Open intents ──────────────────────────────────────────────────────
        private void OpenClanChat()
        {
            var p = FindObjectOfType<ClanChatPanel>(true);
            if (p != null) { p.Toggle(); FlowTrace.Step("UI", "SocialAccessCluster -> ClanChat Toggle"); }
            else FlowTrace.Warn("UI", "SocialAccessCluster: no ClanChatPanel found to open.");
        }

        private void OpenLeaderboard()
        {
            var p = FindObjectOfType<LeaderboardPanel>(true);
            if (p != null) { p.Toggle(); FlowTrace.Step("UI", "SocialAccessCluster -> Leaderboard Toggle"); }
            else FlowTrace.Warn("UI", "SocialAccessCluster: no LeaderboardPanel found to open.");
        }

        // Cross-asmdef (DeNelle.Audio): resolve + toggle MusicSelectionPanel by reflection
        // (HUD → Core only; no Audio asmdef ref). MusicSelectionPanel.Toggle() is public (WO-563).
        private void OpenJukebox()
        {
            if (_musicType == null)
                _musicType = System.Type.GetType("DeNelle.Audio.MusicSelectionPanel, DeNelle.Audio");
            if (_musicType == null) { FlowTrace.Warn("UI", "SocialAccessCluster: MusicSelectionPanel type not found."); return; }

            var panel = Object.FindObjectOfType(_musicType);
            if (panel == null) { FlowTrace.Warn("UI", "SocialAccessCluster: no MusicSelectionPanel found to open."); return; }

            var toggle = _musicType.GetMethod("Toggle",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (toggle != null) { toggle.Invoke(panel, null); FlowTrace.Step("UI", "SocialAccessCluster -> Jukebox Toggle"); }
            else FlowTrace.Warn("UI", "SocialAccessCluster: MusicSelectionPanel.Toggle() not found.");
        }

        // ── helpers ─────────────────────────────────────────────────────────────
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            return rt;
        }
    }
}
