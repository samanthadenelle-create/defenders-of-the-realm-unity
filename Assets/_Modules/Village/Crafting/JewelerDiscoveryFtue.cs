using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village.Crafting
{
    /// <summary>One-time return-home discovery, driven by dungeon-earned history and completed by
    /// the first real rough-stone polish action. Current balance is deliberately irrelevant.</summary>
    public sealed class JewelerDiscoveryFtue : MonoBehaviour
    {
        public const string CompletionKey = "ftue.jeweler.first_polish";
        public const string DiscoveryCopy = "You recovered a rare rough stone. This first find is guaranteed; future stones are uncommon, and not every dungeon holds one.";
        private static JewelerDiscoveryFtue _instance;
        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _panel;
        private WorldHold.Handle _hold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) { _instance.TryPresent(); return; }
            var go = new GameObject("[JewelerDiscoveryFtue]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<JewelerDiscoveryFtue>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            JewelPolishService.FirstPolishActionStarted += Complete;
            TryPresent();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            JewelPolishService.FirstPolishActionStarted -= Complete;
            Close();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryPresent();

        private static bool Completed
        {
            get
            {
                var s = GameStateService.Instance?.State;
                return s != null && s.SeenTutorials.TryGetValue(CompletionKey, out bool seen) && seen;
            }
        }

        private void TryPresent()
        {
            if (_modal != null || !JewelerProgression.IsUnlocked || Completed) return;
            string scene = SceneManager.GetActiveScene().name ?? string.Empty;
            if (scene.IndexOf("Dungeon", System.StringComparison.OrdinalIgnoreCase) >= 0) return;

            _hold = WorldHold.Acquire("jeweler-discovery");
            _modal = ElarionUiKit.BuildObsidianModal("JewelerDiscoveryUI", "JEWELER DISCOVERED",
                ElarionUiKit.ModalArchetype.Compact, Close, sortingOrder: 31030);
            _panel = PanelManager.Register("Jeweler Discovery", Close,
                () => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy);
            if (!PanelManager.NotifyOpened(_panel)) { Close(); return; }
            Transform content = _modal.chrome.content.transform;
            var body = ElarionUiKit.Label(content,
                DiscoveryCopy + "\n\nCrafting transforms materials you own. The Jeweler can polish this raw stone into a refined gem.",
                0.34f, 0.91f, ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.TopLeft, 0.07f, 0.93f);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;
            ElarionUiKit.FitBlock(body, ElarionUi.FontFloorMobile, ElarionUi.FontBody);
            ElarionUiKit.Button(content, "Open Crafting: Jeweler", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.29f), OpenJeweler);
        }

        private void OpenJeweler()
        {
            Close();
            PanelRouter.Open(PanelId.JewelerCrafting);
        }

        private void Complete()
        {
            GameStateService.Instance?.MarkTutorialSeen(CompletionKey);
            Close();
        }

        private void Update() { if (_hold != null) WorldHold.Renew(_hold); }

        private void Close()
        {
            if (_panel != null) PanelManager.NotifyClosed(_panel);
            _hold?.Dispose(); _hold = null;
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
            _modal = null; _panel = null;
        }
    }
}
