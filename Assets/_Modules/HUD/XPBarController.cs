// =============================================================================
// XPBarController — persistent XP strip at the bottom of the village HUD.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// Subscribes to HeroProgression events via reflection so DeNelle.HUD never
// gains a compile dependency on DeNelle.Village (cross-assembly rule).
//
// Exact property / event names validated against HeroProgression.cs:
//   Properties:  Level (int), Xp (float), XpToNext (float), LifetimeXp (float)
//   Events:      OnXpChanged(float currentXp, float xpNeeded)
//                OnLevelUp(int newLevel)
//
// UXML fallback: if no VisualElements are found (UXML unavailable in builds),
// an IMGUI overlay is drawn instead — same data, no UIToolkit dependency.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    /// <summary>
    /// Drives a persistent XP strip at the bottom of the village HUD.
    /// Subscribes to HeroProgression events via reflection (HUD cannot ref Village).
    /// Falls back to IMGUI when UIToolkit elements are unavailable.
    /// </summary>
    public class XPBarController : MonoBehaviour
    {
        // ── UIToolkit elements (resolved at Start from the UIDocument) ─────────
        private VisualElement _xpFill;
        private Label _xpLabel;
        private Label _levelLabel;

        // ── Animation ─────────────────────────────────────────────────────────
        [SerializeField] private float _fillLerpSpeed = 4f;
        [SerializeField] private float _pulseDuration  = 0.35f;

        private float _targetFill;    // 0..1
        private float _currentFill;
        private bool  _pulsing;

        // ── IMGUI fallback state ───────────────────────────────────────────────
        private bool  _uiReady;       // true once UXML elements were found
        private int   _imguiLevel = 1;
        private float _imguiCurrentXp;
        private float _imguiXpNeeded  = 200f;

        // ── Reflection cache ──────────────────────────────────────────────────
        private object _prog;         // the HeroProgression MonoBehaviour instance

        // ── Scene gating ──────────────────────────────────────────────────────
        // The XP strip is a GAMEPLAY-only HUD. ProgressionManager DontDestroyOnLoads
        // a HeroProgression, so a HeroProgression can exist even on the Title /
        // HeroSelect / PetSelect onboarding scenes — the old `_prog == null` guard
        // alone let the bar (e.g. "Lv 1 · 0/150 XP") leak onto the title under the
        // hero cards. These onboarding scenes are a small closed set; any other
        // scene (Village / OuterWorld / Dungeon_* / PatriciaLightMode / ATBBattle)
        // is gameplay and DOES show the bar. Names match Build Settings exactly.
        private static readonly System.Collections.Generic.HashSet<string> NonGameplayScenes =
            new System.Collections.Generic.HashSet<string>
            {
                "Title",      // SceneRouter.Title — onboarding landing
                "HeroSelect", // SceneRouter.HeroSelect
                "PetSelect",  // SceneRouter.PetSelect
            };

        /// <summary>True only on a gameplay scene — false on Title/HeroSelect/PetSelect.</summary>
        private static bool IsGameplayScene()
        {
            string active = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(active) && !NonGameplayScenes.Contains(active);
        }

        // ─────────────────────────────────────────────────────────────────────

        // ── Self-bootstrap ────────────────────────────────────────────────────
        // Nothing in the scene hosted this strip, so it never appeared (the bar was
        // built but unused). Spawn ONE persistent host at game start — the same
        // self-bootstrap pattern as AudioBootstrap / SkillSystem. The bar only DRAWS
        // when a HeroProgression exists (gameplay scenes); on the title/menus _prog
        // stays null and OnGUI early-returns, so it adds nothing there.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<XPBarController>() != null) return;
            var go = new GameObject("XPBarController (HUD)");
            DontDestroyOnLoad(go);
            go.AddComponent<XPBarController>();
        }

        private void Start()
        {
            ResolveUiElements();
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(HookWithRetry());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Re-resolve the hero each scene — the village hero spawns after scene load,
        // and a new scene drops the old (destroyed) HeroProgression reference.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _prog = null;
            ResolveUiElements();
            StartCoroutine(HookWithRetry());
        }

        private void ResolveUiElements()
        {
            var doc = GetComponent<UIDocument>() ?? FindObjectOfType<UIDocument>();
            if (doc?.rootVisualElement != null)
            {
                _xpFill     = doc.rootVisualElement.Q<VisualElement>("xp-fill");
                _xpLabel    = doc.rootVisualElement.Q<Label>("xp-label");
                _levelLabel = doc.rootVisualElement.Q<Label>("xp-level-label");
                _uiReady    = (_xpFill != null);   // at least the bar needs to exist
            }
        }

        // The hero / HeroProgression can come up a beat after the scene loads — poll
        // for a few seconds rather than hooking once and giving up.
        private IEnumerator HookWithRetry()
        {
            // Gameplay-only: never hook (or draw) on the onboarding scenes — the
            // DDOL HeroProgression would otherwise make the bar appear on the title.
            if (!IsGameplayScene()) yield break;
            for (int i = 0; i < 30 && _prog == null; i++)
            {
                HookHeroProgression();
                if (_prog != null) yield break;
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private void HookHeroProgression()
        {
            // Try the static Instance path first (fastest, avoids FindWithTag cost).
            // HeroProgression.Instance is accessible by name via reflection.
            // "Player" is a built-in tag; "HeroTarget" may be undefined (FindWithTag
            // throws on an undefined tag) — guard the fallback.
            var heroGo = GameObject.FindWithTag("Player")
                      ?? SafeFindWithTag("HeroTarget");

            // Also check the DontDestroyOnLoad singleton that may live at world-origin.
            Component prog = heroGo != null
                ? heroGo.GetComponent("HeroProgression")
                : null;

            if (prog == null)
            {
                // Bootstrap singleton lives on a GO named "HeroProgression".
                var singletonGo = GameObject.Find("HeroProgression");
                if (singletonGo != null)
                    prog = singletonGo.GetComponent("HeroProgression");
            }

            if (prog == null)
            {
                // Not found YET — HookWithRetry keeps polling; no warning to avoid
                // spamming the log every retry on scenes that have no hero.
                return;
            }

            _prog = prog;
            var type = prog.GetType();

            // Subscribe: OnXpChanged(float currentXp, float xpNeeded)
            var onXpChanged = type.GetEvent("OnXpChanged");
            if (onXpChanged != null)
            {
                try
                {
                    onXpChanged.AddEventHandler(prog,
                        System.Delegate.CreateDelegate(
                            onXpChanged.EventHandlerType, this,
                            nameof(OnXpChanged)));
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[XPBarController] OnXpChanged hook failed: {e.Message}");
                }
            }

            // Subscribe: OnLevelUp(int newLevel)
            var onLevelUp = type.GetEvent("OnLevelUp");
            if (onLevelUp != null)
            {
                try
                {
                    onLevelUp.AddEventHandler(prog,
                        System.Delegate.CreateDelegate(
                            onLevelUp.EventHandlerType, this,
                            nameof(OnLevelUp)));
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[XPBarController] OnLevelUp hook failed: {e.Message}");
                }
            }

            // Seed with current values so the bar shows correct data immediately.
            var levelProp  = type.GetProperty("Level");
            var xpProp     = type.GetProperty("Xp");
            var xpNextProp = type.GetProperty("XpToNext");

            int   seedLevel  = levelProp  != null ? (int)(levelProp.GetValue(prog))   : 1;
            float seedXp     = xpProp     != null ? (float)(xpProp.GetValue(prog))    : 0f;
            float seedNeeded = xpNextProp != null ? (float)(xpNextProp.GetValue(prog)) : 200f;

            SetLevel(seedLevel);
            ApplyXpValues(seedXp, seedNeeded);
        }

        // ── Called by HeroProgression.OnXpChanged ────────────────────────────

        public void OnXpChanged(float currentXp, float xpNeeded)
        {
            ApplyXpValues(currentXp, xpNeeded);
            if (!_pulsing) StartCoroutine(PulseRoutine());
        }

        // ── Called by HeroProgression.OnLevelUp ──────────────────────────────

        public void OnLevelUp(int newLevel)
        {
            SetLevel(newLevel);
            _targetFill = 0f;   // reset bar fill on level-up
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyXpValues(float currentXp, float xpNeeded)
        {
            _targetFill       = xpNeeded > 0f ? Mathf.Clamp01(currentXp / xpNeeded) : 0f;
            _imguiCurrentXp   = currentXp;
            _imguiXpNeeded    = xpNeeded;

            if (_xpLabel != null)
                _xpLabel.text = $"{Mathf.FloorToInt(currentXp):N0} / {Mathf.FloorToInt(xpNeeded):N0} XP";
        }

        private void SetLevel(int level)
        {
            _imguiLevel = level;
            if (_levelLabel != null) _levelLabel.text = $"Lv. {level}";
        }

        // ── Update — smooth fill lerp ─────────────────────────────────────────

        private void Update()
        {
            if (Mathf.Approximately(_currentFill, _targetFill)) return;
            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * _fillLerpSpeed);

            if (_xpFill != null)
                _xpFill.style.width = Length.Percent(_currentFill * 100f);
        }

        private IEnumerator PulseRoutine()
        {
            _pulsing = true;
            _xpFill?.AddToClassList("xp-fill--pulse");
            yield return new WaitForSeconds(_pulseDuration);
            _xpFill?.RemoveFromClassList("xp-fill--pulse");
            _pulsing = false;
        }

        // ── IMGUI fallback — renders when UXML elements were not found ────────
        //
        // RETIRED (owner request 2026-06-08): the full-width bottom XP strip is gone.
        // XP now shows as a thin YELLOW line directly ABOVE the hero's HP bar inside
        // the VillageHudController vitals panel (BuildVitalsCluster → "XPTrack"). This
        // controller keeps its HeroProgression hook so any UXML "xp-fill" element (if
        // ever present) still updates, but it NO LONGER draws a full-screen IMGUI bar.
        private void OnGUI()
        {
            return; // full-screen XP strip retired — see VillageHudController vitals line
#pragma warning disable CS0162 // unreachable retained for reference / quick re-enable
            if (_uiReady) return;   // UXML is driving the UI — skip IMGUI
            if (!IsGameplayScene()) return;   // onboarding scene (Title/HeroSelect/PetSelect) — draw nothing
            if (_prog == null) return;   // no hero in this scene yet — draw nothing

            // Position: bottom strip, full width, 28px tall.
            float sw = Screen.width;
            float sh = Screen.height;
            float barH   = 14f;
            float labelH = 16f;
            float marginX = 16f;
            float marginB = 8f;

            float barW = sw - marginX * 2f;
            float barY = sh - marginB - labelH - 2f - barH;

            // Background
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(marginX, barY, barW, barH), Texture2D.whiteTexture);

            // Fill
            GUI.color = _pulsing
                ? new Color(0.69f, 0.54f, 1f, 1f)
                : new Color(0.48f, 0.37f, 0.66f, 1f);
            GUI.DrawTexture(new Rect(marginX, barY, barW * _currentFill, barH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Labels row below the bar
            float labelY = barY + barH + 2f;
            GUI.Label(new Rect(marginX, labelY, 80f, labelH), $"Lv. {_imguiLevel}");

            string xpText = $"{Mathf.FloorToInt(_imguiCurrentXp):N0} / {Mathf.FloorToInt(_imguiXpNeeded):N0} XP";
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(marginX, labelY, barW, labelH), xpText, style);

            GUI.color = Color.white;
#pragma warning restore CS0162
        }
    }
}
