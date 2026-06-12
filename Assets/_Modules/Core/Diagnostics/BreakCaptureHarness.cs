// =============================================================================
// BreakCaptureHarness - an always-on "flight recorder" for playtests.
//
// It auto-installs at startup (no scene setup, no prefab) and passively records
// every BREAK while you play, so we get an objective punch-list toward the
// end-to-end-playable goal instead of relying on "I think it glitched here".
//
// What it captures:
//   * Errors / exceptions / failed asserts  (Application.logMessageReceived)
//       -> the highest-value, zero-false-positive signal.
//   * Possible softlocks  (no movement AND no progress event for a long stretch).
//   * Scene transitions    (breadcrumb trail so a break has context).
//
// Where it writes:
//   * <persistentDataPath>/break-log.jsonl  (one JSON record per line) + PNG
//     screenshots on a break  -- on Standalone this sits next to Player.log.
//   * A tagged "[BREAK]" console line (the only retrievable channel in WebGL,
//     where the sandbox filesystem is unreliable).
//   * EventTracker.Track("playtest_break", ...) -- rides the existing telemetry
//     pipe (queues locally even with the backend offline).
//
// Design rules: it must NEVER crash or spam the game it is watching -- every
// path is try/caught, the log handler is reentrancy-guarded (so it can't loop
// on its own output), screenshots are throttled, and identical errors dedupe.
//
// Lives in DeNelle.Core (uses only EventTracker + DialogueEventBus, same asm) so
// it adds no cross-module coupling. Removable later by deleting this one file.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Analytics;
// Force the engine logger even if a DeNelle.Core.Debug namespace exists
// (see memory: core-namespace-shadows-unityengine-statics).
using Debug = UnityEngine.Debug;

namespace DeNelle.Core.Diagnostics
{
    public sealed class BreakCaptureHarness : MonoBehaviour
    {
        // ---- tunables ----------------------------------------------------------
        const float SoftlockSeconds   = 75f;   // no movement AND no progress this long => "possible softlock"
        const float HeroMoveEpsilon   = 0.75f; // metres of movement that counts as "still progressing"
        const float WatchdogInterval  = 2f;    // how often the softlock watchdog samples
        const int   MaxScreenshots    = 25;    // per session, so an error storm can't fill the disk
        const string HeroTag          = "Player";

        // ---- state -------------------------------------------------------------
        static bool s_installed;
        [NonSerialized] bool _inHandler;       // reentrancy guard for the log handler
        readonly HashSet<string> _seen = new HashSet<string>();
        int _shotCount;
        string _logPath;
        bool _fileOk;

        Transform _hero;
        Vector3 _lastHeroPos;
        float _lastProgressTime;
        bool _softlockReported;
        float _nextWatchdog;

        // owner-pressed bug flag for subjective/visual bugs the code can't detect
        // on its own ("ugly", "feels off", "wrong text"). One tap = screenshot + mark.
        const KeyCode FlagKey = KeyCode.F8;
        int _flagCount;
        float _toastUntil = -1f;
        // note-entry: tap F8 -> screenshot clean frame -> freeze + type one line
        bool _noteMode;
        string _noteBuffer = "";
        int _noteShowFrame;
        float _prevTimeScale = 1f;

        // ---- bootstrap (zero setup) -------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (s_installed) return;
            // Dev/iteration tool only: never on WebGL (the ship surface) where its
            // file/screenshot writes are sandboxed anyway. Runs in editor + desktop
            // (Win exe) playtests, which is the iterative ticket-closing loop.
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            s_installed = true;
            try
            {
                var go = new GameObject("~BreakCaptureHarness");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<BreakCaptureHarness>();
            }
            catch { /* a diagnostic must never break startup */ }
        }

        void Awake()
        {
            try
            {
                _fileOk = Application.platform != RuntimePlatform.WebGLPlayer;
                _logPath = Path.Combine(Application.persistentDataPath, "break-log.jsonl");
                _lastProgressTime = Time.realtimeSinceStartup;
                _nextWatchdog = Time.realtimeSinceStartup + WatchdogInterval;

                Application.logMessageReceived += OnLog;
                SceneManager.sceneLoaded += OnSceneLoaded;
                DialogueEventBus.Fired += OnProgressEvent;

                Debug.Log($"[BreakCapture] active (press {FlagKey} to flag a bug). Break log -> {( _fileOk ? _logPath : "console only (WebGL)")}");
                Record("session_start", "BreakCaptureHarness online", null, screenshot: false);
            }
            catch (Exception e) { SafeWarn(e); }
        }

        void OnDestroy()
        {
            try
            {
                Application.logMessageReceived -= OnLog;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                DialogueEventBus.Fired -= OnProgressEvent;
            }
            catch { }
        }

        // ---- error / exception capture ----------------------------------------
        void OnLog(string condition, string stack, LogType type)
        {
            if (_inHandler) return;                                   // never recurse on our own logging
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _inHandler = true;
            try
            {
                string key = type + "|" + condition;
                bool firstTime = _seen.Add(key);                     // dedupe identical errors
                Record(type == LogType.Exception ? "exception" : "error",
                       condition, Truncate(stack, 1200), screenshot: firstTime);
            }
            catch { }
            finally { _inHandler = false; }
        }

        // ---- progress + softlock watchdog -------------------------------------
        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            _hero = null;                                            // re-resolve after a load
            MarkProgress();
            Record("scene_loaded", s.name, null, screenshot: false);
        }

        void OnProgressEvent(string _) => MarkProgress();

        void MarkProgress()
        {
            _lastProgressTime = Time.realtimeSinceStartup;
            _softlockReported = false;
        }

        void Update()
        {
            try { if (Input.GetKeyDown(FlagKey)) FlagHere(); } catch { }   // poll the flag key every frame

            float now = Time.realtimeSinceStartup;
            if (now < _nextWatchdog) return;
            _nextWatchdog = now + WatchdogInterval;
            try
            {
                if (_hero == null) _hero = FindHero();
                if (_hero != null)
                {
                    if ((_hero.position - _lastHeroPos).sqrMagnitude > HeroMoveEpsilon * HeroMoveEpsilon)
                    {
                        _lastHeroPos = _hero.position;
                        MarkProgress();
                    }
                }
                if (!_softlockReported && now - _lastProgressTime > SoftlockSeconds)
                {
                    _softlockReported = true;
                    Record("possible_softlock",
                           $"No movement or progress for {SoftlockSeconds:0}s in '{SceneManager.GetActiveScene().name}'",
                           null, screenshot: true);
                }
            }
            catch (Exception e) { SafeWarn(e); }
        }

        Transform FindHero()
        {
            try { var go = GameObject.FindGameObjectWithTag(HeroTag); return go ? go.transform : null; }
            catch { return null; }   // tag undefined / none in scene
        }

        // ---- owner-pressed flag (F8): screenshot, freeze, type one line ---------
        void FlagHere()
        {
            if (_noteMode) return;                                 // already flagging
            // screenshot the CLEAN frame first (the note box draws next frame)
            try { ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, $"flag_{_flagCount:00}.png")); }
            catch { }
            _noteBuffer = "";
            _noteShowFrame = Time.frameCount + 1;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;                                   // freeze so typing can't drive the hero
            _noteMode = true;
        }

        void CommitFlag()
        {
            _noteMode = false;
            try { Time.timeScale = _prevTimeScale; } catch { Time.timeScale = 1f; }
            string note = string.IsNullOrWhiteSpace(_noteBuffer) ? "(no note)" : _noteBuffer.Trim();
            Record("flagged", $"[{SafeScene()}] {note}", null, screenshot: false);  // shot already taken
            _flagCount++;
            _toastUntil = Time.realtimeSinceStartup + 1.6f;
        }

        void OnGUI()
        {
            // note-entry box (only after the screenshot frame so it isn't captured)
            if (_noteMode && Time.frameCount >= _noteShowFrame)
            {
                try
                {
                    var e = Event.current;
                    if (e.type == EventType.KeyDown)
                    {
                        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { CommitFlag(); return; }
                        if (e.keyCode == KeyCode.Escape) { CommitFlag(); return; }
                    }
                    float w = 580f, x = (Screen.width - w) * 0.5f, y = Screen.height * 0.18f;
                    GUI.color = new Color(0f, 0f, 0f, 0.85f);
                    GUI.Box(new Rect(x - 12, y - 12, w + 24, 96), GUIContent.none);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x, y, w, 24), "⚑ What looks wrong?  (Enter = save · Esc = save blank)");
                    GUI.SetNextControlName("flagNote");
                    _noteBuffer = GUI.TextField(new Rect(x, y + 28, w, 30), _noteBuffer, 240);
                    GUI.FocusControl("flagNote");
                }
                catch { CommitFlag(); }
                return;
            }
            // brief "flagged" confirmation toast
            if (Time.realtimeSinceStartup > _toastUntil) return;
            try
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
                GUI.Label(new Rect(18, 16, 260, 34), "  ⚑ flagged", new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold });
                GUI.color = prev;
            }
            catch { }
        }

        string SafeScene()
        {
            try { return SceneManager.GetActiveScene().name; } catch { return "?"; }
        }

        // ---- the one place a break is recorded --------------------------------
        void Record(string kind, string message, string stack, bool screenshot)
        {
            string scene = "?";
            try { scene = SceneManager.GetActiveScene().name; } catch { }

            var rec = new BreakRecord
            {
                kind = kind,
                message = message ?? "",
                stack = stack ?? "",
                scene = scene,
                t = Time.realtimeSinceStartup,
                utc = DateTime.UtcNow.ToString("o")
            };

            // 1) telemetry pipe (WebGL-safe; queues even with backend offline)
            try { EventTracker.Track("playtest_break", rec); } catch { }

            // 2) console line (only retrievable channel in WebGL) - guard reentry
            bool prev = _inHandler; _inHandler = true;
            try { if (kind != "session_start" && kind != "scene_loaded") Debug.LogWarning($"[BREAK] {kind}: {message}"); }
            catch { }
            finally { _inHandler = prev; }

            // 3) jsonl file + screenshot (Standalone; best-effort)
            if (_fileOk)
            {
                try { File.AppendAllText(_logPath, JsonUtility.ToJson(rec) + "\n", Encoding.UTF8); }
                catch { _fileOk = false; }   // stop trying if the path is unwritable
            }
            if (screenshot && _shotCount < MaxScreenshots)
            {
                try
                {
                    string shot = Path.Combine(Application.persistentDataPath, $"break_{_shotCount:00}_{kind}.png");
                    ScreenCapture.CaptureScreenshot(shot);
                    _shotCount++;
                }
                catch { }
            }
        }

        static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "...[truncated]");

        void SafeWarn(Exception e)
        {
            bool prev = _inHandler; _inHandler = true;
            try { Debug.LogWarning("[BreakCapture] internal: " + e.Message); } catch { }
            finally { _inHandler = prev; }
        }

        [Serializable]
        struct BreakRecord
        {
            public string kind;
            public string message;
            public string stack;
            public string scene;
            public float  t;
            public string utc;
        }
    }
}
