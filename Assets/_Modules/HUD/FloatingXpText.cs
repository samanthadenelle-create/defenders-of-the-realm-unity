// =============================================================================
// FloatingXpText — pops a "+N XP" label above the hero on every XP gain.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// Cheap dopamine that pairs with the XP bar (XPBarController). Self-bootstraps
// via [RuntimeInitializeOnLoadMethod] and subscribes to HeroProgression's
// OnXpChanged(float currentXp, float xpNeeded) event via REFLECTION — the HUD
// assembly may not reference DeNelle.Village (cross-assembly rule), so it hooks
// the event by name exactly like XPBarController does.
//
// Validated against HeroProgression.cs:
//   Event:    OnXpChanged(float currentXp, float xpNeeded)
//   Property: WorldPosition (Vector3)   — anchor point above the hero's head
//
// Rendering: IMGUI overlay (OnGUI). WebGL-safe, code-built, no UXML/UIToolkit.
// Each float rises and fades over ~0.8s. Floats are pooled (a fixed-size ring)
// so no GameObjects are allocated and concurrent floats are capped.
//
// Gameplay-only: a float only draws when _prog (the HeroProgression instance)
// is non-null. On title/menu scenes no HeroProgression exists, so OnGUI draws
// nothing — same gate as XPBarController.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    /// <summary>
    /// Spawns short-lived "+N XP" labels above the hero whenever XP is gained.
    /// Subscribes to HeroProgression.OnXpChanged via reflection (HUD cannot ref
    /// Village). IMGUI-rendered, pooled, gameplay-scene only.
    /// </summary>
    public class FloatingXpText : MonoBehaviour
    {
        // ── Tuning ────────────────────────────────────────────────────────────
        private const float FloatLifetime = 0.85f;   // seconds each label lives
        private const float RiseDistance  = 64f;     // pixels the label rises
        private const int   MaxFloats     = 8;       // concurrent-float cap (pool size)

        // ── Pooled float record (struct ring — no GC, no GameObjects) ─────────
        private struct XpFloat
        {
            public bool    Active;
            public float   Age;        // seconds since spawn
            public int     Amount;     // +N
            public float   AnchorX;    // screen-space spawn anchor (px, y-down)
            public float   AnchorY;
        }

        private readonly XpFloat[] _floats = new XpFloat[MaxFloats];
        private int _nextSlot;

        // ── Reflection cache ──────────────────────────────────────────────────
        private object _prog;                 // the HeroProgression instance
        private System.Reflection.PropertyInfo _worldPosProp;

        // ── XP-delta tracking ─────────────────────────────────────────────────
        private float _lastXp = -1f;          // -1 = not yet seeded
        private bool  _seeded;

        // ── GUI style (built lazily on the GUI thread) ───────────────────────
        private GUIStyle _style;

        // ─────────────────────────────────────────────────────────────────────

        // Self-bootstrap: spawn ONE persistent host at game start, mirroring
        // XPBarController. The float only DRAWS when a HeroProgression exists
        // (gameplay scenes); elsewhere _prog stays null and OnGUI early-returns.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<FloatingXpText>() != null) return;
            var go = new GameObject("FloatingXpText (HUD)");
            DontDestroyOnLoad(go);
            go.AddComponent<FloatingXpText>();
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryHook();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // New scene drops the old (destroyed) HeroProgression; re-resolve and
        // clear any in-flight floats so nothing leaks across a scene change.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _prog = null;
            _worldPosProp = null;
            _seeded = false;
            _lastXp = -1f;
            for (int i = 0; i < _floats.Length; i++) _floats[i].Active = false;
            TryHook();
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException e)
            {
                FlowTrace.Warn("HUD", $"FindWithTag('{tag}') failed: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // Hook the hero's HeroProgression by name via reflection. The hero can
        // come up a beat after the scene loads, so Update() retries until found.
        private void TryHook()
        {
            if (_prog != null) return;

            var heroGo = GameObject.FindWithTag("Player")
                      ?? SafeFindWithTag("HeroTarget");

            Component prog = heroGo != null
                ? heroGo.GetComponent("HeroProgression")
                : null;

            if (prog == null)
            {
                var singletonGo = GameObject.Find("HeroProgression");
                if (singletonGo != null)
                    prog = singletonGo.GetComponent("HeroProgression");
            }

            if (prog == null) return;   // not up yet — Update() will retry

            _prog = prog;
            var type = prog.GetType();

            // Anchor source: HeroProgression.WorldPosition (Vector3). Optional —
            // if absent or non-Vector3, floats fall back to top-center.
            var wp = type.GetProperty("WorldPosition");
            if (wp != null && wp.PropertyType == typeof(Vector3))
                _worldPosProp = wp;

            // Subscribe: OnXpChanged(float currentXp, float xpNeeded).
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
                    Debug.LogWarning($"[FloatingXpText] OnXpChanged hook failed: {e.Message}");
                }
            }

            // Seed _lastXp with the current value so the first real gain (not the
            // initial seed) is the first thing that pops.
            var xpProp = type.GetProperty("Xp");
            if (xpProp != null && xpProp.PropertyType == typeof(float))
            {
                try { _lastXp = (float)xpProp.GetValue(prog); }
                catch { _lastXp = 0f; }
            }
            else
            {
                _lastXp = 0f;
            }
            _seeded = true;
        }

        // ── Called by HeroProgression.OnXpChanged ─────────────────────────────

        public void OnXpChanged(float currentXp, float xpNeeded)
        {
            // First callback after (re)hooking just establishes the baseline.
            if (!_seeded || _lastXp < 0f)
            {
                _lastXp = currentXp;
                _seeded = true;
                return;
            }

            float delta = currentXp - _lastXp;
            _lastXp = currentXp;

            // A level-up resets XP toward the next level, so currentXp can DROP
            // below _lastXp (negative delta). Only pop on a genuine positive gain.
            if (delta <= 0.5f) return;

            SpawnFloat(Mathf.RoundToInt(delta));
        }

        // ── Spawn into the pooled ring ────────────────────────────────────────

        private void SpawnFloat(int amount)
        {
            if (amount <= 0) return;

            // Compute the screen-space anchor at spawn time. (y is GUI-space,
            // top-down.) If the hero isn't on screen / no anchor prop, top-center.
            Vector2 anchor = ComputeAnchor();

            int slot = _nextSlot;
            _nextSlot = (_nextSlot + 1) % MaxFloats;

            _floats[slot].Active  = true;
            _floats[slot].Age     = 0f;
            _floats[slot].Amount  = amount;
            _floats[slot].AnchorX = anchor.x;
            _floats[slot].AnchorY = anchor.y;
        }

        private Vector2 ComputeAnchor()
        {
            float topCenterX = Screen.width * 0.5f;
            float topCenterY = Screen.height * 0.18f;   // a touch below the top edge

            if (_worldPosProp == null || _prog == null)
                return new Vector2(topCenterX, topCenterY);

            var cam = Camera.main;
            if (cam == null)
                return new Vector2(topCenterX, topCenterY);

            Vector3 world;
            try { world = (Vector3)_worldPosProp.GetValue(_prog); }
            catch (System.Exception e)
            {
                FlowTrace.Warn("HUD", $"hero world-pos read failed: {e.GetType().Name}: {e.Message}");
                return new Vector2(topCenterX, topCenterY);
            }

            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f)   // behind the camera — fall back to top-center
                return new Vector2(topCenterX, topCenterY);

            // Convert Unity screen-space (y-up) to GUI space (y-down).
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        // ── Update — retry the hook + age the floats ──────────────────────────

        private void Update()
        {
            if (_prog == null)
            {
                TryHook();
                return;
            }

            float dt = Time.unscaledDeltaTime;   // unscaled so floats animate even
            for (int i = 0; i < _floats.Length; i++)   // if the game is paused
            {
                if (!_floats[i].Active) continue;
                _floats[i].Age += dt;
                if (_floats[i].Age >= FloatLifetime)
                    _floats[i].Active = false;
            }
        }

        // ── IMGUI render ──────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Gameplay-only gate: no hero in this scene (title/menus) → draw nothing.
            if (_prog == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize  = 22
                };
            }

            for (int i = 0; i < _floats.Length; i++)
            {
                if (!_floats[i].Active) continue;

                float t = Mathf.Clamp01(_floats[i].Age / FloatLifetime);
                float rise = RiseDistance * t;
                float alpha = 1f - (t * t);   // ease-out fade

                float x = _floats[i].AnchorX - 60f;          // center a 120px box
                float y = _floats[i].AnchorY - rise - 16f;   // rise upward (y-down)

                var rect = new Rect(x, y, 120f, 32f);
                string txt = $"+{_floats[i].Amount} XP";

                // Drop shadow for legibility over any background.
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), txt, _style);

                // Foreground — Elarion violet, matching the XP bar palette.
                GUI.color = new Color(0.78f, 0.66f, 1f, alpha);
                GUI.Label(rect, txt, _style);

                GUI.color = prev;
            }
        }
    }
}
