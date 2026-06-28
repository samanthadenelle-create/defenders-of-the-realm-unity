// =============================================================================
// VillageLoadOverlay — a code-built full-screen loading screen for the village.
// -----------------------------------------------------------------------------
// PROBLEM (owner): loading Village2 is a 3-6s SYNCHRONOUS hitch that shows the
// player a black screen with no feedback — a lumpy first minute. SceneRouter.
// GoVillage used SceneManager.LoadScene (a blocking call), so nothing could render
// during the load.
//
// FIX: an async load (LoadSceneAsync) with this overlay rendered ON TOP while it
// runs — "Loading Elarion…", an animated spinner, a progress bar, and a rotating
// lore tidbit so the wait feels intentional and on-brand. Hidden + destroyed the
// moment the new scene is active.
//
// BUILD-SAFETY: built entirely in CODE with uGUI (Canvas / Image / Text) — NOT
// UI Toolkit and NOT UXML. This sidesteps BOTH project landmines:
//   • "UXML/UIDocuments don't render in player builds" (CLAUDE.md §8), and
//   • the UIDocument PanelSettings-null regression (a UIDocument needs a
//     PanelSettings asset; a code-built uGUI Canvas needs nothing).
// A DontDestroyOnLoad canvas at a very high sortingOrder so it paints over the old
// AND the loading scene. Styled from the shared ElarionUi palette so it matches
// the rest of the game. Self-contained: one static Show()/HideAndDestroy() pair
// driven by SceneRouter.LoadVillageWithLoader.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// A self-contained, code-built (uGUI, no UXML / no PanelSettings) full-screen
    /// loading overlay shown during the async village scene load. Created via
    /// <see cref="Show"/>; driven + torn down by <see cref="SceneRouter"/>.
    /// </summary>
    public sealed class VillageLoadOverlay : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _spinner;
        private Image _progressFill;
        private Text _loreLabel;

        private float _spinSpeed = 220f;     // deg / sec
        private float _loreTimer;
        private int _loreIndex;
        private const float LoreRotateSeconds = 3.2f;

        // Rotating lore tidbits — short, on-brand, set the tone during the wait.
        private static readonly string[] Lore =
        {
            "Elarion holds because we hold the line.",
            "Hold the last light.",
            "The Echoes rest in the Hollow, waiting to be called.",
            "Every tower is a promise the gate will not fall.",
            "The enemy comes by night. We rebuild by day.",
            "Stone remembers. So do we.",
        };

        /// <summary>
        /// Builds + shows the loading overlay on a fresh DontDestroyOnLoad canvas and
        /// returns it. Call <see cref="SetProgress"/> while the scene loads and
        /// <see cref="HideAndDestroy"/> when it is active.
        /// </summary>
        public static VillageLoadOverlay Show()
        {
            var go = new GameObject("VillageLoadOverlay");
            DontDestroyOnLoad(go);
            var overlay = go.AddComponent<VillageLoadOverlay>();
            overlay.Build();
            return overlay;
        }

        private void Build()
        {
            // ── Canvas (screen-space overlay, very high sort so it's always on top) ──
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;   // above HUD / fade / everything
            gameObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;   // eat input under the loader

            // ── Full-screen background (menu bg texture if present, else stone fill) ─
            var bg = NewImage("Backdrop", transform);
            Stretch(bg.rectTransform);
            var menuTex = ElarionUi.MenuBackground;
            if (menuTex != null)
            {
                bg.sprite = Sprite.Create(menuTex,
                    new Rect(0, 0, menuTex.width, menuTex.height), new Vector2(0.5f, 0.5f));
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.024f, 0.016f, 0.047f, 1f);   // deep dusk
            }

            // A subtle scrim so text reads over any busy bg.
            var scrim = NewImage("Scrim", transform);
            Stretch(scrim.rectTransform);
            scrim.color = new Color(0.02f, 0.015f, 0.04f, 0.55f);

            // ── Title ────────────────────────────────────────────────────────────
            var title = NewText("Title", transform, "Loading Elarion…", 64, FontStyle.Bold);
            title.color = ElarionUi.Gilt;
            title.alignment = TextAnchor.MiddleCenter;
            var tRt = title.rectTransform;
            tRt.anchorMin = new Vector2(0.5f, 0.5f);
            tRt.anchorMax = new Vector2(0.5f, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = new Vector2(0, 160);
            tRt.sizeDelta = new Vector2(900, 120);

            // ── Spinner (a rotating ring glyph) ───────────────────────────────────
            var spin = NewText("Spinner", transform, "✦", 96, FontStyle.Bold);
            spin.color = ElarionUi.Gold;
            spin.alignment = TextAnchor.MiddleCenter;
            _spinner = spin.rectTransform;
            _spinner.anchorMin = new Vector2(0.5f, 0.5f);
            _spinner.anchorMax = new Vector2(0.5f, 0.5f);
            _spinner.pivot = new Vector2(0.5f, 0.5f);
            _spinner.anchoredPosition = new Vector2(0, 20);
            _spinner.sizeDelta = new Vector2(160, 160);

            // ── Progress bar (track + fill) ───────────────────────────────────────
            var track = NewImage("ProgressTrack", transform);
            track.color = new Color(0.110f, 0.086f, 0.058f, 0.97f);
            var trRt = track.rectTransform;
            trRt.anchorMin = new Vector2(0.5f, 0.5f);
            trRt.anchorMax = new Vector2(0.5f, 0.5f);
            trRt.pivot = new Vector2(0.5f, 0.5f);
            trRt.anchoredPosition = new Vector2(0, -120);
            trRt.sizeDelta = new Vector2(640, 18);

            _progressFill = NewImage("ProgressFill", track.transform);
            _progressFill.color = ElarionUi.Gold;
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFill.fillAmount = 0.05f;
            Stretch(_progressFill.rectTransform);

            // ── Rotating lore tidbit ──────────────────────────────────────────────
            _loreIndex = Random.Range(0, Lore.Length);
            var lore = NewText("Lore", transform, Lore[_loreIndex], 30, FontStyle.Italic);
            lore.color = new Color(0.78f, 0.74f, 0.66f, 1f);   // ParchmentDim
            lore.alignment = TextAnchor.MiddleCenter;
            lore.horizontalOverflow = HorizontalWrapMode.Wrap;
            _loreLabel = lore;
            var lRt = lore.rectTransform;
            lRt.anchorMin = new Vector2(0.5f, 0.5f);
            lRt.anchorMax = new Vector2(0.5f, 0.5f);
            lRt.pivot = new Vector2(0.5f, 0.5f);
            lRt.anchoredPosition = new Vector2(0, -210);
            lRt.sizeDelta = new Vector2(820, 100);
        }

        private void Update()
        {
            // Spin the ring (unscaled — a paused timescale during load must not freeze it).
            if (_spinner != null)
                _spinner.Rotate(0f, 0f, -_spinSpeed * Time.unscaledDeltaTime);

            // Rotate the lore tidbit on a timer.
            _loreTimer += Time.unscaledDeltaTime;
            if (_loreTimer >= LoreRotateSeconds && _loreLabel != null)
            {
                _loreTimer = 0f;
                _loreIndex = (_loreIndex + 1) % Lore.Length;
                _loreLabel.text = Lore[_loreIndex];
            }
        }

        /// <summary>Sets the progress bar fill (0..1). Clamped + given a small floor so it always reads as "moving".</summary>
        public void SetProgress(float t)
        {
            if (_progressFill != null)
                _progressFill.fillAmount = Mathf.Clamp(t, 0.05f, 1f);
        }

        /// <summary>Hides + destroys the overlay (call once the new scene is active).</summary>
        public void HideAndDestroy()
        {
            if (this != null) Destroy(gameObject);
        }

        // ── uGUI builder helpers ──────────────────────────────────────────────

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static Text NewText(string name, Transform parent, string text, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            // LegacyRuntime is the built-in default font on modern Unity (Arial was removed);
            // fall back to Arial for older editors.
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
