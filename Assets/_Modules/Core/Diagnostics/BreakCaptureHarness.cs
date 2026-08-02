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
        const float SoftlockSeconds   = 180f;  // no movement AND no progress this long => "possible softlock"
                                               // (raised 75->180 2026-06-24: 75s false-fired on casual AFK/
                                               //  reading during manual felt-tests; a real stuck-state the
                                               //  player would F8-flag anyway. Dialogue already suppressed.)
        const float HeroMoveEpsilon   = 0.75f; // metres of movement that counts as "still progressing"
        const float WatchdogInterval  = 2f;    // how often the softlock watchdog samples
        const int   MaxScreenshots    = 25;    // per session, so an error storm can't fill the disk
        const string HeroTag          = "Player";

        // ---- state -------------------------------------------------------------
        static bool s_installed;

        /// <summary>The live harness instance (set in Awake, cleared in OnDestroy). Lets the
        /// on-screen mobile FLAG button (<see cref="DeNelle.Core.Dev.FlagCaptureButton"/>) fire the
        /// SAME capture the F8 key fires, since the owner has no keyboard on the Android tester APK.
        /// Null before install / on WebGL (Install() early-outs there).</summary>
        public static BreakCaptureHarness Instance { get; private set; }
        [NonSerialized] bool _inHandler;       // reentrancy guard for the log handler
        readonly HashSet<string> _seen = new HashSet<string>();
        int _shotCount;
        string _logPath;
        string _outDir;        // per-run output dir (persistentDataPath/autopilot-runs/<id>) or persistentDataPath
        bool _fileOk;

        Transform _hero;
        Vector3 _lastHeroPos;
        float _lastProgressTime;
        bool _softlockReported;
        float _nextWatchdog;

        // Cross-assembly read of DeNelle.Village.HeroLocomotion.InputSuppressed via reflection.
        // Core must NOT reference Village (asmdef one-way: Village -> Core), so we cache the
        // static getter once (same pattern as SceneRouter.FindHeroLocomotion / PersistenceBridge).
        // The softlock MOVEMENT watchdog must not count scripted/dialogue/cutscene/autowalk time
        // as a stall: while input is suppressed the hero legitimately stands still (reading a line,
        // a camera beat, an autowalk), so the 75s timer would false-fire (it did, in MainCastle_Hall
        // intro). Real softlocks during dialogue still surface via the error/exception path.
        static bool _suppressProbed;       // have we attempted to resolve the getter yet
        static System.Reflection.MethodInfo _inputSuppressedGetter;  // null = type/prop absent -> behave as today

        // owner-pressed bug flag for subjective/visual bugs the code can't detect
        // on its own ("ugly", "feels off", "wrong text"). One tap = screenshot + mark.
        const KeyCode FlagKey = KeyCode.F8;
        int _flagCount;
        // EVIDENCE-LOSS FIX (triage 2026-07-08): flag_NN.png restarted at 00 each session, so a new
        // session OVERWROTE the previous session's screenshots — the F8-19/F8-22 ticket evidence was
        // destroyed exactly this way. Every flag shot now carries a per-session stamp so no session
        // can clobber another's captures (the RCA-proof-by-data rule depends on these files).
        readonly string _sessionStamp = System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        float _toastUntil = -1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // note-entry: tap F8 -> screenshot clean frame -> freeze + type one line.
        // WO-839 §3 release-safety: the typed-note flow is DEV-ONLY. The harness installs
        // on every non-WebGL platform, and this state (plus its OnGUI box below) was the
        // one piece OUTSIDE the dev guard — the "What looks wrong?" capture field could
        // render on a NON-development player build. Compiled out of release entirely.
        bool _noteMode;
        string _noteBuffer = "";
        int _noteShowFrame;
        float _prevTimeScale = 1f;
#endif

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
            Instance = this;                                       // mobile FLAG button entry point
            try
            {
                _fileOk = Application.platform != RuntimePlatform.WebGLPlayer;

                // Per-run output namespacing for FLEET mode: when launched with
                // --run=<id> (one of N parallel headless bots), redirect the
                // break log + screenshots into persistentDataPath/autopilot-runs/<id>/
                // so concurrent instances don't CLOBBER one shared break-log.jsonl.
                // No --run -> unchanged default path (normal single-player behavior).
                _outDir = ResolveOutDir();
                _logPath = Path.Combine(_outDir, "break-log.jsonl");
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

        // Resolve the output directory: persistentDataPath/autopilot-runs/<id> when a
        // --run=<id> arg is present (fleet mode), else persistentDataPath (default).
        // Fully guarded — a diagnostic must never throw at startup; on any failure we
        // fall back to the plain persistentDataPath.
        string ResolveOutDir()
        {
            string baseDir = Application.persistentDataPath;
            try
            {
                string runId = ParseRunId();
                if (string.IsNullOrEmpty(runId)) return baseDir;

                string dir = Path.Combine(Path.Combine(baseDir, "autopilot-runs"), runId);
                Directory.CreateDirectory(dir);
                Debug.Log($"[BreakCapture] fleet run --run={runId}: namespaced output -> {dir}");
                return dir;
            }
            catch (Exception e) { SafeWarn(e); return baseDir; }
        }

        // Parse --run=<id> from the command line (sanitized to a safe folder token).
        static string ParseRunId()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args == null) return null;
                foreach (var a in args)
                {
                    if (string.IsNullOrEmpty(a)) continue;
                    if (a.StartsWith("--run=", StringComparison.OrdinalIgnoreCase))
                    {
                        string id = a.Substring("--run=".Length).Trim();
                        if (string.IsNullOrEmpty(id)) return null;
                        var clean = new StringBuilder(id.Length);
                        foreach (char c in id)
                            clean.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
                        return clean.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        // MOBILE SCREENSHOT-PATH FIX (device RCA 2026-07-19): the FLAG tap wrote a "flagged"
        // break-log record but NO flag_NN.png on the RELEASE Android APK. Root cause is a
        // platform path-semantics mismatch, NOT a debug/editor gate: the break-log is written
        // with File.AppendAllText(<absolute path>) which Android honours, but the PNG is written
        // with ScreenCapture.CaptureScreenshot(...) which on Android/iOS treats its argument as
        // a path RELATIVE to Application.persistentDataPath and PREPENDS that base itself. So the
        // same absolute path becomes persistentDataPath + "/" + persistentDataPath + "/flag.png"
        // (nested / invalid) and the frame is silently dropped. On Standalone/editor absolute
        // paths work, which is why desktop F8 capture always produced a file. Fix: hand
        // ScreenCapture a path RELATIVE to persistentDataPath on mobile, keep the absolute path
        // on Standalone/editor. WebGL is unaffected (Install() early-outs there — screenshots
        // are legitimately off on the ship surface).
        string ScreenshotPath(string fileName)
        {
            string dir = _outDir ?? Application.persistentDataPath;
            string abs = Path.Combine(dir, fileName);
            if (Application.platform == RuntimePlatform.Android ||
                Application.platform == RuntimePlatform.IPhonePlayer)
            {
                try
                {
                    string root = Application.persistentDataPath;
                    if (!string.IsNullOrEmpty(root) &&
                        abs.StartsWith(root, StringComparison.Ordinal))
                    {
                        // ScreenCapture prepends persistentDataPath on mobile, so pass only the
                        // remainder (just the filename in normal play; autopilot-runs/<id>/... in fleet mode).
                        return abs.Substring(root.Length).TrimStart('/', '\\');
                    }
                }
                catch { /* fall through to absolute */ }
            }
            return abs;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
                // WO-459: dedupe by condition AND the top of the stack. Every NRE shares the
                // SAME condition string ("NullReferenceException: Object reference not set..."),
                // so a condition-only key collapsed ALL distinct NRE call-sites into one record
                // — hiding every source but the first and defeating "find the root of the spam".
                // Folding the first stack line into the key lets each distinct throw-site record
                // its own stack (so a single capture names ALL the dead objects, not just one),
                // while still deduping the per-frame repeat of the SAME site.
                string key = type + "|" + condition + "|" + FirstStackLine(stack);
                bool firstTime = _seen.Add(key);                     // dedupe identical errors at the same site
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

        bool _f8PollWarned;
        void Update()
        {
            // §12: NEVER swallow silently — if the F8 poll/flag throws, LOG it once (Debug.LogWarning)
            // so a DEAD F8 capture self-reports its cause instead of failing invisibly (owner: "no F8").
            try { if (Input.GetKeyDown(FlagKey)) FlagHere(); }
            catch (Exception ex) { if (!_f8PollWarned) { _f8PollWarned = true; Debug.LogWarning("[BreakCapture] F8 poll/flag threw (capture disabled?): " + ex); } }

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
                // SUPPRESS the movement watchdog while the hero is under scripted control:
                // dialogue line on screen, a cutscene/camera beat, or a scripted autowalk all
                // legitimately freeze the hero. Treat that as progress (reset the timer) so the
                // 75s stall never accumulates during scripted time and only resumes counting once
                // the player has free control and is still idle. DialogueEventBus advances already
                // count as progress; this covers the WAIT-on-a-line / cutscene stretches between.
                if (IsHeroInputSuppressed())
                {
                    if (_hero != null) _lastHeroPos = _hero.position;
                    MarkProgress();
                    return;
                }
                // Ticket #1 (2026-07-07): a modal owning the screen (Seating Editor, shop, help…)
                // is the PLAYER choosing to stand still — not a softlock. Two false captures in one
                // owner dial session (break-log 15:49:58 + 16:02:15, screenshots show the editor
                // open). PanelManager is the single-modal arbiter; treat any open modal as progress.
                if (UI.PanelManager.AnyOpen)
                {
                    if (_hero != null) _lastHeroPos = _hero.position;
                    MarkProgress();
                    return;
                }
                // F8-13 (2026-07-07): a live Build Mode edit session is the PLAYER choosing to
                // stand still — placing towers for minutes is normal (false capture 01:21Z while
                // the owner's Player.log showed [Flow:Build] PlaceConfirm CONFIRMED lines through
                // the window). Core-legal read: HudContextEvaluator (Village) already mirrors
                // BuildModeController.IsActive into the Core-side HudContextModel every 0.20s;
                // we read that mirror via CoreServices.HudModel — no Village reference needed.
                if (IsBuildModeActive())
                {
                    if (!_buildSuppressTraced)
                    {
                        _buildSuppressTraced = true;
                        FlowTrace.Step("BreakCapture", "softlock watchdog suppressed: build mode active");
                    }
                    if (_hero != null) _lastHeroPos = _hero.position;
                    MarkProgress();
                    return;
                }
                _buildSuppressTraced = false;   // re-trace on the next build session's rising edge

                // F8 (2026-07-08): sitting on a MENU / onboarding scene is not a softlock — the
                // player is legitimately parked on Title / HeroSelect / PetSelect / Intro / Store,
                // etc. (false capture: possible_softlock "No movement or progress for 180s in
                // 'Title'" while the player sat on the title screen). Same class as the F8-13 build-
                // mode gate. Core cannot reference DeNelle.HUD, so we MIRROR the HUD bootstrap's
                // allowlist (VillageHudBootstrap.MenuScenes — the canonical set; keep in sync) here.
                if (IsMenuOrOnboardingScene(SceneManager.GetActiveScene().name))
                {
                    MarkProgress();
                    return;
                }
                // F8 (2026-07-08): a modal owning the screen is the player reading a dialogue/panel,
                // not a softlock. PanelManager.AnyOpen above catches code-built modals; this also
                // honours the Core HUD context mirror (modal=True) the same Core-legal way F8-13
                // reads BuildModeActive — covers modals surfaced only through the HudContextModel.
                if (IsModalOpen())
                {
                    MarkProgress();
                    return;
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

        // True when DeNelle.Village.HeroLocomotion.InputSuppressed is set (dialogue / cutscene /
        // autowalk active). Read via cached reflection so Core stays decoupled from Village.
        // Null-safe: if the type or property can't be resolved (e.g. Village not loaded), returns
        // false so the watchdog behaves exactly as it did before this guard existed.
        static bool IsHeroInputSuppressed()
        {
            try
            {
                if (!_suppressProbed)
                {
                    _suppressProbed = true;
                    var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
                    var p = t != null
                        ? t.GetProperty("InputSuppressed",
                              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                        : null;
                    _inputSuppressedGetter = p != null ? p.GetGetMethod(nonPublic: false) : null;
                }
                if (_inputSuppressedGetter == null) return false;
                return _inputSuppressedGetter.Invoke(null, null) is bool b && b;
            }
            catch { return false; }   // any reflection failure -> behave as today (count as stall)
        }

        // F8-13: rising-edge flag so the build-mode suppression FlowTrace fires once per
        // build session (not every 2s watchdog tick); cleared when build mode exits.
        bool _buildSuppressTraced;

        // F8-13: true while a Build Mode edit session is live. Core-legal without reflection:
        // Village's HudContextEvaluator is the single writer that mirrors
        // BuildModeController.IsActive into the Core-side HudContextModel every 0.20s
        // (HUD_OBSIDIAN §3.3 seam); we read that mirror via CoreServices.HudModel.
        // Null-safe: no HudModelHost registered (headless/boot) -> false, watchdog
        // behaves exactly as before this guard existed.
        static bool IsBuildModeActive()
        {
            try
            {
                var ctx = CoreServices.HudModel?.Context;
                return ctx != null && ctx.BuildModeActive;
            }
            catch { return false; }
        }

        // F8 (2026-07-08): a modal owning the screen (context modal=True) is legitimate idle —
        // read the Core HUD context mirror the same null-safe way IsBuildModeActive does. Null-safe:
        // no HudModelHost registered (headless/boot) -> false, watchdog behaves as before this guard.
        static bool IsModalOpen()
        {
            try
            {
                var ctx = CoreServices.HudModel?.Context;
                return ctx != null && ctx.ModalOpen;
            }
            catch { return false; }
        }

        // F8 (2026-07-08): the menu / onboarding / front-end scenes where standing still is normal.
        // MIRROR of DeNelle.HUD VillageHudBootstrap.MenuScenes (Core cannot reference the HUD asm;
        // that list is the canonical source — keep these in sync). Matched case-insensitively.
        static readonly string[] MenuScenes =
        {
            "Title", "HeroSelect", "PetSelect", "Intro", "IntroFlow",
            "Store", "PackStore", "Boot", "Bootstrap", "MainMenu", "GameOver",
        };

        static bool IsMenuOrOnboardingScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < MenuScenes.Length; i++)
            {
                if (string.Equals(sceneName, MenuScenes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ---- owner-pressed flag (F8): screenshot, freeze, type one line ---------
        void FlagHere()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_noteMode) return;                                 // already flagging
            // screenshot the CLEAN frame first (the note box draws next frame)
            try { ScreenCapture.CaptureScreenshot(ScreenshotPath($"flag_{_sessionStamp}_{_flagCount:00}.png")); }
            catch { }
            _noteBuffer = "";
            _noteShowFrame = Time.frameCount + 1;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;                                   // freeze so typing can't drive the hero
            _noteMode = true;
#else
            // WO-839 §3 release-safety: the typed-note flow (freeze + IMGUI text field) is
            // compiled OUT of non-development players. F8 still captures — it routes through
            // the no-keyboard path (same screenshot + "flagged" record, no freeze), so a
            // release build can neither render the dev note box NOR softlock at timeScale 0
            // waiting for a commit no OnGUI block would ever deliver.
            FlagFromButton("F8 (release build - note entry is dev-only)");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void CommitFlag()
        {
            _noteMode = false;
            try { Time.timeScale = _prevTimeScale; } catch { Time.timeScale = 1f; }
            string note = string.IsNullOrWhiteSpace(_noteBuffer) ? "(no note)" : _noteBuffer.Trim();
            Record("flagged", $"[{SafeScene()}] {note}", null, screenshot: false);  // shot already taken
            _flagCount++;
            _toastUntil = Time.realtimeSinceStartup + 1.6f;
        }
#endif

        // =====================================================================
        // MOBILE FLAG BUTTON entry point (owner has NO keyboard on the Android
        // tester APK, so the F8 key + its typed-note flow above are unreachable
        // there). The on-screen tap chip (DeNelle.Core.Dev.FlagCaptureButton)
        // calls THIS to fire the SAME capture the F8 key fires: a clean-frame PNG
        // named exactly like FlagHere()'s (flag_<session>_NN.png) + a Record with
        // kind="flagged" (the identical break-log.jsonl entry F8 writes) + the same
        // per-session flag counter and confirmation toast. It just SKIPS the
        // keyboard note step (no freeze, no typed line) since there is no keyboard.
        // Fully guarded - a diagnostic entry point must never throw.
        // =====================================================================
        /// <summary>Force the same "flagged" capture the F8 key triggers (clean-frame screenshot +
        /// break-log record), minus the keyboard note flow. For the on-screen mobile FLAG button.</summary>
        public void FlagFromButton(string note = null)
        {
            try
            {
                // clean-frame screenshot, same naming/counter as FlagHere() (F8) so the button's
                // shot lands in the exact same flag_/break_ evidence set. ScreenshotPath() makes the
                // PNG land on the RELEASE Android APK (was silently dropped by the absolute-path bug).
                try { ScreenCapture.CaptureScreenshot(ScreenshotPath($"flag_{_sessionStamp}_{_flagCount:00}.png")); }
                catch { }
                string msg = string.IsNullOrWhiteSpace(note) ? "on-screen FLAG button" : note.Trim();
                Record("flagged", $"[{SafeScene()}] {msg}", null, screenshot: false);  // shot already taken
                _flagCount++;
                _toastUntil = Time.realtimeSinceStartup + 1.6f;
                FlowTrace.Step("BreakCapture", $"FLAG button capture -> flagged record ({msg})");
            }
            catch (Exception e) { SafeWarn(e); }
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // note-entry box (only after the screenshot frame so it isn't captured).
            // WO-839 §3: moved INSIDE the dev guard — this block previously sat OUTSIDE it,
            // so the "What looks wrong?" capture field could render on a RELEASE player
            // build (the harness installs on every non-WebGL platform).
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
            // (the dev guard opened at the top of OnGUI continues — note box + Flag button
            // are one dev-only region; the confirmation toast below stays in all builds)
            // RELIABLE capture trigger (owner: "no F8"). F8 is unreliable in the editor (Game-view
            // focus / function-key interception) and ABSENT on mobile — and this game is mobile-first.
            // A small always-visible IMGUI button flags a bug with ZERO input-system / focus dependency
            // (tap on mobile, click in the editor). F8 stays as the desktop shortcut. Dev/editor builds
            // only — never shown to players (BreakCaptureHarness itself ships, the button does not).
            if (!_noteMode)
            {
                var prevFlag = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.92f);
                if (GUI.Button(new Rect(8f, Screen.height * 0.5f - 18f, 96f, 36f), "⚑ Flag")) FlagHere();
                GUI.color = prevFlag;
            }
#endif

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

        // =====================================================================
        // WO-596 — player bug report: the F8 capture path, factored callable.
        // ---------------------------------------------------------------------
        // The player-facing bug-report form (BugReportView, HUD asm) reuses THIS
        // harness's proven clean-frame trick (screenshot BEFORE the form draws,
        // same as FlagHere) but needs BYTES (POSTable) instead of a disk PNG,
        // and must run on WebGL where the full harness never installs (Install()
        // early-outs — its file writes are sandboxed). So:
        //   * a lightweight TRACE-TAIL ring installs on ALL platforms (incl.
        //     WebGL) and keeps the last N [Flow:*]/error lines in memory;
        //   * CaptureForReport() is a static coroutine: hide privacy-registered
        //     UI (PrivacySensitiveUi) → wait for the rendered frame → grab the
        //     backbuffer as a Texture2D → JPEG re-encode under a size cap →
        //     restore UI → hand back bytes + tail. All guarded; never throws.
        // The F8 flow above is UNCHANGED (owner's own capture path stays as-is).
        // =====================================================================

        /// <summary>Result of <see cref="CaptureForReport"/>: a size-capped JPEG of the
        /// clean frame (null when capture failed or produced nothing under the cap),
        /// the recent trace tail, and the active scene name.</summary>
        public sealed class ReportCapture
        {
            public byte[]   ScreenshotJpg;
            public string[] TraceTail;
            public string   Scene;
        }

        // ---- trace-tail ring (all platforms, incl. WebGL) -----------------------
        const int TailCap        = 80;    // last N kept lines
        const int TailLineMax    = 300;   // per-line clamp so the ring stays tiny
        static readonly Queue<string> s_tail = new Queue<string>(TailCap);
        static bool s_tailInstalled;
        static bool s_inTailHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallReportTail()
        {
            if (s_tailInstalled) return;
            s_tailInstalled = true;
            try { Application.logMessageReceived += OnTailLog; }
            catch { /* a diagnostic must never break startup */ }
        }

        static void OnTailLog(string condition, string stack, LogType type)
        {
            if (s_inTailHandler) return;                      // never recurse on our own logging
            s_inTailHandler = true;
            try
            {
                bool isFlow  = !string.IsNullOrEmpty(condition) && condition.Contains("[Flow:");
                bool isBreak = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
                if (!isFlow && !isBreak) return;              // tail = [Flow:*] lines + hard breaks only

                string line = (isBreak && !isFlow ? type + ": " : "") + Truncate(condition, TailLineMax);
                lock (s_tail)
                {
                    while (s_tail.Count >= TailCap) s_tail.Dequeue();   // drop-oldest, bounded
                    s_tail.Enqueue(line);
                }
            }
            catch { }
            finally { s_inTailHandler = false; }
        }

        /// <summary>Snapshot of the recent [Flow:*]/error lines (oldest first).</summary>
        public static string[] RecentTraceTail()
        {
            try { lock (s_tail) { return s_tail.ToArray(); } }
            catch { return Array.Empty<string>(); }
        }

        // ---- clean-frame byte capture -------------------------------------------
        const int ReportJpgMaxBytes = 300 * 1024;  // WO-596 size cap (~300KB re-encoded)
        const int ReportMaxDim      = 1280;        // downscale huge frames before encoding

        /// <summary>
        /// WO-596 — capture the CLEAN frame (privacy-registered UI hidden for the frame,
        /// same one-frame trick as the F8 note box) as size-capped JPEG bytes + the recent
        /// trace tail. Run via StartCoroutine BEFORE the report form builds its own UI so
        /// the form is never in its own screenshot. <paramref name="onDone"/> always fires
        /// (with a null ScreenshotJpg on capture failure — the report still sends).
        /// </summary>
        public static System.Collections.IEnumerator CaptureForReport(Action<ReportCapture> onDone)
        {
            var result = new ReportCapture();
            result.TraceTail = RecentTraceTail();
            try { result.Scene = SceneManager.GetActiveScene().name; } catch { result.Scene = "?"; }

            IDisposable hide = null;
            try { hide = PrivacySensitiveUi.HideForCapture(); }
            catch { }

            // Let the hide (and any just-closed menu) leave the render, then capture the
            // END of the rendered frame — the same "screenshot the clean frame first"
            // ordering FlagHere() uses, but into memory instead of a sandboxed file.
            yield return null;
            yield return new WaitForEndOfFrame();

            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex != null)
                {
                    result.ScreenshotJpg = EncodeReportJpg(tex);
                    UnityEngine.Object.Destroy(tex);
                }
                else FlowTrace.Warn("BugReport", "CaptureForReport: backbuffer grab returned null — sending without screenshot");
            }
            catch (Exception e)
            {
                FlowTrace.Fail("BugReport", $"CaptureForReport screenshot threw: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                try { hide?.Dispose(); } catch { }
            }

            FlowTrace.Step("BugReport",
                $"clean-frame capture done — jpg={(result.ScreenshotJpg != null ? result.ScreenshotJpg.Length : 0)}B " +
                $"tail={result.TraceTail.Length} lines scene='{result.Scene}'");
            try { onDone?.Invoke(result); } catch (Exception e) { SafeStaticWarn(e); }
        }

        /// <summary>Downscale + JPEG-encode under <see cref="ReportJpgMaxBytes"/>; steps the
        /// quality down before giving up. Returns null when nothing fits the cap.</summary>
        static byte[] EncodeReportJpg(Texture2D src)
        {
            Texture2D work = null;
            try
            {
                work = DownscaleForReport(src, ReportMaxDim);
                int[] qualities = { 75, 55, 35 };
                foreach (int q in qualities)
                {
                    byte[] jpg = (work != null ? work : src).EncodeToJPG(q);
                    if (jpg != null && jpg.Length <= ReportJpgMaxBytes) return jpg;
                }
                FlowTrace.Warn("BugReport", "screenshot exceeds the 300KB cap even at quality 35 — dropped");
                return null;
            }
            catch (Exception e)
            {
                FlowTrace.Fail("BugReport", $"JPEG re-encode threw: {e.GetType().Name}: {e.Message}");
                return null;
            }
            finally
            {
                if (work != null && work != src) UnityEngine.Object.Destroy(work);
            }
        }

        /// <summary>GPU-blit downscale so a 4K frame doesn't blow the JPEG cap (WebGL-safe:
        /// RenderTexture + ReadPixels, no threads). Returns the source when already small.</summary>
        static Texture2D DownscaleForReport(Texture2D src, int maxDim)
        {
            if (src == null) return null;
            int big = Mathf.Max(src.width, src.height);
            if (big <= maxDim) return src;

            float s = (float)maxDim / big;
            int w = Mathf.Max(1, Mathf.RoundToInt(src.width * s));
            int h = Mathf.Max(1, Mathf.RoundToInt(src.height * s));

            RenderTexture rt = null;
            var prev = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                outTex.Apply(false);
                return outTex;
            }
            catch (Exception e)
            {
                FlowTrace.Warn("BugReport", $"downscale failed ({e.Message}) — encoding full-size frame");
                return src;
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        static void SafeStaticWarn(Exception e)
        {
            try { Debug.LogWarning("[BreakCapture] report-capture internal: " + e.Message); } catch { }
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
                    // ScreenshotPath(): mobile-relative on Android/iOS (else silently dropped), absolute on desktop.
                    ScreenCapture.CaptureScreenshot(ScreenshotPath($"break_{_shotCount:00}_{kind}.png"));
                    _shotCount++;
                }
                catch { }
            }
        }

        static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "...[truncated]");

        // WO-459: the first non-empty line of a stack trace — the throwing frame. Used in the
        // dedupe key so two DIFFERENT NRE sites (same condition string) don't collapse into one
        // record. Empty stack (some asserts) falls back to "" so behaviour is unchanged there.
        static string FirstStackLine(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            int nl = stack.IndexOf('\n');
            string line = nl < 0 ? stack : stack.Substring(0, nl);
            return line.Trim();
        }

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
