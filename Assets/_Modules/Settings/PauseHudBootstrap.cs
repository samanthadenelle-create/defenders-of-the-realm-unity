// =============================================================================
// PauseHudBootstrap — makes Pause/Settings REACHABLE in-game (WO-714 W8).
// -----------------------------------------------------------------------------
// THE GAP (proved by repo-wide grep, 2026-07-13): PauseController/SettingsController
// were converted to code-built kit modals on 2026-07-03 (coverage row #47/#47b) but
// NOTHING routed to them — no scene places either component (script GUIDs appear in
// no .unity file), no HUD button exists, and PauseGate.RequestBack() had ZERO call
// sites outside comments. The panels existed; the player had no door.
//
// THE FIX — same RuntimeInitializeOnLoadMethod pattern as HelpMenuBootstrap /
// SettingsBootstrap (no scene wiring, survives scene rebuilds):
//   * Per gameplay scene, spawn one host GameObject carrying SettingsController +
//     PauseController (wired together via PauseController.AttachSettings — the
//     serialized field only works for scene-placed instances).
//   * Add PauseHudButton: a small kit-dressed on-screen MENU/PAUSE chip, top-right
//     edge BELOW the icon cluster — the exact spot the retired MusicToggleHud
//     vacated (top 200 / right 14; owner-approved as clear of mobile controls).
//     It calls PauseGate.RequestBack(): open modal -> close it; else -> toggle
//     pause. This is the designed-but-never-built caller the PauseGate header
//     describes ("The HUD's PAUSE/BACK button calls RequestBack()").
//   * Front-end scenes (Title / HeroSelect / PetSelect / intro / splash) are
//     skipped — pausing a menu is meaningless and the chip would foul the
//     front-end chrome (W9's lane).
//
// Colorblind law: the chip is a static affordance (no state carried by color).
// Sprite-first: Obsidian action-slot plate + gear icon from RpgUiCatalog; the
// null-art fallback draws two gold PAUSE bars from Image quads — glyph-proof
// (no unverified unicode, the star-tofu lesson), reads as "pause" by shape.
//
// Lives in DeNelle.Settings; references DeNelle.Core + UnityEngine.UI only.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Settings
{
    /// <summary>
    /// Installs the pause/settings stack (controllers + the on-screen pause chip)
    /// into every gameplay scene. See file header — this is the WO-714 W8 routing.
    /// </summary>
    public static class PauseHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;   // idempotent
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnInScene(scene);

        /// <summary>True for scenes where pausing is meaningless (front-end/menus).</summary>
        private static bool IsFrontEndScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return true;
            if (sceneName == "Title" || sceneName == "HeroSelect" || sceneName == "PetSelect")
                return true;   // SceneRouter's front-end trio
            // Defensive: story/splash/loading shells are also front-end.
            return sceneName.Contains("Intro") || sceneName.Contains("Splash")
                || sceneName.Contains("Loading");
        }

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (IsFrontEndScene(scene.name))
            {
                FlowTrace.Step("Pause", "PauseHudBootstrap: front-end scene '" + scene.name + "' — skipped.");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes, HelpMenuBootstrap pattern) —
            // additive loads (OuterWorld streaming into MainCastle_Hall) fire
            // sceneLoaded again; a per-scene check would double-install the chip.
            foreach (var existing in Object.FindObjectsByType<PauseController>(FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Step("Pause", "PauseHudBootstrap: PauseController already live — skipped duplicate.");
                    return;
                }
            }

            var go = new GameObject("PauseSettingsHost");
            SceneManager.MoveGameObjectToScene(go, scene);
            var settings = go.AddComponent<SettingsController>();
            var pause = go.AddComponent<PauseController>();
            pause.AttachSettings(settings);   // serialized ref is scene-only; wire at runtime
            go.AddComponent<PauseHudButton>();
            FlowTrace.Step("Pause",
                "PauseHudBootstrap: installed PauseController+SettingsController+PauseHudButton in '" +
                scene.name + "'.");
        }
    }

    /// <summary>
    /// The always-visible on-screen pause chip (top-right edge, below the HUD icon
    /// cluster). Tapping it calls <see cref="PauseGate.RequestBack"/> — close the
    /// open modal if any, else toggle the pause overlay. Hides while a modal is up
    /// (QuestTrackerHud pattern) so it never floats over panel chrome.
    /// </summary>
    public sealed class PauseHudButton : MonoBehaviour
    {
        private GameObject _ui;

        private void Start() => Build();

        private void OnEnable()
        {
            PanelManager.OpenStateChanged += SyncModalVisibility;
            SyncModalVisibility();
        }

        private void OnDisable()
        {
            PanelManager.OpenStateChanged -= SyncModalVisibility;
        }

        private void OnDestroy() { if (_ui != null) Destroy(_ui); }

        private void SyncModalVisibility()
        {
            if (_ui != null) _ui.SetActive(!PanelManager.AnyOpen);
        }

        private void Build()
        {
            if (_ui != null) return;

            _ui = new GameObject("PauseHudButtonUI");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;   // above QuestTracker chip (80), far below modals (30000+)

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _ui.AddComponent<GraphicRaycaster>();

            // Chip: 52px kit-dressed medallion at the MusicToggleHud's vacated spot
            // (right 14, top 200 — below the top-right icon cluster, clear of the
            // bottom mobile controls per the owner's 2026-07-12 overlap bug).
            var chip = new GameObject("PauseChip", typeof(Image), typeof(Button));
            chip.transform.SetParent(_ui.transform, false);
            var rt = chip.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(52f, 52f);
            rt.anchoredPosition = new Vector2(-14f, -200f);

            // Sprite-first plate (Obsidian action slot); translucent stone fallback.
            var plateImg = chip.GetComponent<Image>();
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotAction);
            if (plate != null)
            {
                plateImg.sprite = plate;
                plateImg.color = Color.white;
            }
            else
            {
                var bg = ElarionUi.PanelStoneDark;
                plateImg.color = new Color(bg.r, bg.g, bg.b, 0.72f);
            }
            chip.GetComponent<Button>().onClick.AddListener(OnChipClicked);

            // Face: gear icon (the menu/pause convention on mobile). Null-art
            // fallback = two gold PAUSE bars from Image quads — glyph-proof.
            var iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSettings);
            if (iconSprite != null)
            {
                var icon = new GameObject("Icon", typeof(Image));
                icon.transform.SetParent(chip.transform, false);
                var ir = icon.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.5f, 0.5f);
                ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = new Vector2(34f, 34f);
                var ii = icon.GetComponent<Image>();
                ii.sprite = iconSprite;
                ii.preserveAspect = true;
                ii.raycastTarget = false;   // the chip's Button takes the tap
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    var bar = new GameObject("PauseBar" + i, typeof(Image));
                    bar.transform.SetParent(chip.transform, false);
                    var br = bar.GetComponent<RectTransform>();
                    br.anchorMin = new Vector2(0.5f, 0.5f);
                    br.anchorMax = new Vector2(0.5f, 0.5f);
                    br.sizeDelta = new Vector2(8f, 26f);
                    br.anchoredPosition = new Vector2(i == 0 ? -8f : 8f, 0f);
                    var bi = bar.GetComponent<Image>();
                    bi.color = ElarionUi.Gilt;
                    bi.raycastTarget = false;
                }
            }

            FlowTrace.Step("Pause", "PauseHudButton: chip built (top-right, sort 90, plate="
                + (plate != null ? "sprite" : "fallback") + ", icon="
                + (iconSprite != null ? "gear" : "bars") + ").");
        }

        private void OnChipClicked()
        {
            FlowTrace.Step("Pause", "PauseHudButton: tapped -> PauseGate.RequestBack().");
            PauseGate.RequestBack();
        }
    }
}
