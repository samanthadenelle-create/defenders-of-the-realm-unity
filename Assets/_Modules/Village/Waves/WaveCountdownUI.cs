// =============================================================================
// WaveCountdownUI — screen-space wave-countdown overlay (WO-97 Bug 1).
// -----------------------------------------------------------------------------
// The dungeon-gate world-space "9" was the only countdown readout, causing a
// floating number to appear in-world. This screen-space singleton replaces it.
// WaveManager calls Instance?.StartCountdown(seconds) when each prepare-phase
// begins; this component drives its own on-screen label and auto-hides when
// the countdown reaches zero.
//
// Place this MonoBehaviour on any persistent UI GameObject (or let it
// self-create via the Instance accessor). It builds its own UIElements label
// when no external UIDocument is supplied.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;   // WO-1407: ElarionUi.Duration - the one countdown formatter

namespace DeNelle.Village
{
    /// <summary>
    /// Screen-space countdown display driven by <see cref="WaveManager"/>.
    /// Replaces the world-space dungeon-gate number (Bug 1, WO-97).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveCountdownUI : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private static WaveCountdownUI _instance;

        /// <summary>
        /// Returns the live instance. Lazily creates a GameObject with this
        /// component when none exists — ensures WaveManager can always call
        /// Instance?.StartCountdown without a null-check failure in the caller.
        /// </summary>
        public static WaveCountdownUI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<WaveCountdownUI>();
                // Don't auto-create at runtime — the scene builder places one;
                // return null safely so callers use null-conditional (?.) cleanly.
                return _instance;
            }
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Optional UIDocument whose root will host the countdown label. " +
                 "When null, a code-built label is added to the first available UIDocument.")]
        [SerializeField] private UIDocument _document;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Label _label;
        private Coroutine _routine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            BuildLabel();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Starts a visual countdown from <paramref name="seconds"/>. Calling
        /// while a countdown is running cancels the old one first.
        /// </summary>
        public void StartCountdown(float seconds)
        {
            StartCountdown(seconds, null);
        }

        /// <summary>
        /// Starts a visual countdown from <paramref name="seconds"/> and invokes
        /// <paramref name="onComplete"/> when it reaches zero (WO-91). Calling
        /// while a countdown is running cancels the old one first.
        /// </summary>
        /// <param name="seconds">Duration of the countdown in seconds.</param>
        /// <param name="onComplete">
        /// Optional callback fired once the countdown reaches zero and the
        /// label has shown the "Wave incoming!" flash. Pass null when no
        /// completion callback is needed (e.g. the simple WaveManager call-site
        /// that only needs the visual).
        /// </param>
        public void StartCountdown(float seconds, System.Action onComplete)
        {
            if (_routine != null) StopCoroutine(_routine);
            if (gameObject.activeInHierarchy && seconds > 0f)
                _routine = StartCoroutine(CountdownRoutine(seconds, onComplete));
            else
            {
                HideLabel();
                onComplete?.Invoke();
            }
        }

        // ── Implementation ────────────────────────────────────────────────────

        private IEnumerator CountdownRoutine(float total, System.Action onComplete = null)
        {
            float remaining = total;
            ShowLabel();

            while (remaining > 0f)
            {
                UpdateLabel(remaining);
                yield return null;
                remaining -= Time.deltaTime;
            }

            UpdateLabel(0f);
            yield return new WaitForSeconds(0.5f);
            HideLabel();
            _routine = null;

            // WO-91: fire the completion callback after the label hides so callers
            // can start the next phase (e.g. SpawnWave) once the UI is clear.
            onComplete?.Invoke();
        }

        private void UpdateLabel(float seconds)
        {
            if (_label == null) return;
            int whole = Mathf.CeilToInt(seconds);
            // WO-1407: "Next wave in 14m 15s", never a bare "855s" - one formatter, shared
            // with the HudKit wave plate (HudKitController.OnWave) and the queue rail.
            _label.text = whole > 0 ? "Next wave in " + ElarionUi.Duration(whole) : "Wave incoming!";
        }

        private void ShowLabel()
        {
            if (_label != null) _label.style.display = DisplayStyle.Flex;
        }

        private void HideLabel()
        {
            if (_label != null) _label.style.display = DisplayStyle.None;
        }

        private void BuildLabel()
        {
            // Find or use the assigned UIDocument.
            if (_document == null)
                _document = FindAnyObjectByType<UIDocument>();
            if (_document == null || _document.rootVisualElement == null) return;

            _label = new Label();
            _label.pickingMode = PickingMode.Ignore;
            _label.style.position = Position.Absolute;
            _label.style.top = 8;
            _label.style.left = Length.Percent(50);
            _label.style.translate = new StyleTranslate(new Translate(-50f, 0));
            _label.style.color = new Color(0.95f, 0.88f, 0.55f);
            _label.style.fontSize = 22;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.backgroundColor = new Color(0.05f, 0.04f, 0.08f, 0.72f);
            _label.style.paddingLeft = 12;
            _label.style.paddingRight = 12;
            _label.style.paddingTop = 4;
            _label.style.paddingBottom = 4;
            _label.style.borderTopLeftRadius = 8;
            _label.style.borderTopRightRadius = 8;
            _label.style.borderBottomLeftRadius = 8;
            _label.style.borderBottomRightRadius = 8;
            _label.style.display = DisplayStyle.None;

            _document.rootVisualElement.Add(_label);
        }
    }
}
