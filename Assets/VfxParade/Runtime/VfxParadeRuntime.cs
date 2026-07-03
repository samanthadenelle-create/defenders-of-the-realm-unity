// =============================================================================
// VfxParade.VfxParadeRuntime - in-build (standalone exe) effect parade.
// -----------------------------------------------------------------------------
// The RUNTIME sibling of the editor tool VfxParadeWindow. It lets the owner
// curate VFX WITHOUT the Unity editor open: it loads the build-baked manifest
// (VfxParadeManifest, forced into the build by direct prefab refs), spawns each
// effect a few meters in front of the camera on a loop, and overlays a code-built
// uGUI Screen-Space panel to step through them, tag a combat MOMENT + NOTE, and
// BOOKMARK picks to Application.persistentDataPath/vfx-picks.json (load-append-
// write, never clobber). The AI then reads that JSON and wires the effects.
//
// VIEW CONTROL: drag the viewport to ORBIT the effect (yaw + pitch), wheel/pinch
// to ZOOM; preset buttons snap Front/Side/Top/45 and an AUTO-SPIN toggle yaws it
// hands-free. Orbit rotates the effect in place under the fixed game camera so it
// never fights Camera.main. View resets per-effect; auto-spin stays sticky.
//
// FILTER: with the full Spells Pack (~466) baked in, a "filter:" cycle button
// narrows the parade to entries whose path/name CONTAINS a token (Fire, Ice,
// Storm, ... Projectile, Explosion, ...) so the owner can dig a family (all
// fireballs) instead of scrolling everything. "All" = no filter (default).
//
// Code-built uGUI ONLY (no UXML - it does not ship in builds, project rule).
// Self-contained: builds its own ScreenSpaceOverlay Canvas + an EventSystem if
// the scene lacks one. Null-safe; a broken manifest entry is skipped with an
// ASCII Debug.LogWarning. ASCII-only strings throughout.
//
// Launch: AdminOverlay (Settings -> DevTools -> "VFX Parade") calls
// VfxParadeRuntime.Launch() by reflection (the HUD asmdef does not reference
// this assembly). Launch pauses time; Close restores it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VfxParade
{
    public sealed class VfxParadeRuntime : MonoBehaviour
    {
        // ---- JSON pick model (self-contained, JsonUtility) -------------------
        [Serializable]
        private sealed class Pick
        {
            public string path;
            public string name;
            public string moment;
            public string note;
        }

        [Serializable]
        private sealed class PickFile
        {
            public List<Pick> picks = new List<Pick>();
        }

        // ---- Moment tags (mirror the editor VfxParadeWindow set) -------------
        private static readonly string[] Moments =
        {
            "cast", "hit", "death", "buff", "projectile", "aura", "other"
        };

        // ---- Filter tokens (substring match on path+name) --------------------
        // "All" = no filter. Spells Pack paths encode element + type
        // (e.g. "Casting_Fire", "Explosion_Fire_2", "Projectile_Fire") so a
        // substring on the token surfaces a whole family (all fireballs etc).
        private static readonly string[] FilterTokens =
        {
            "All", "Fire", "Ice", "Storm", "Dark", "Light", "Nature", "Arcane",
            "Casting", "Projectile", "Explosion", "Aura", "Buff", "Shield"
        };
        private int _filterIndex;                       // index into FilterTokens
        private readonly List<int> _filtered = new List<int>(); // manifest indices in the active filter
        private int _filterPos;                         // position within _filtered

        // ---- State -----------------------------------------------------------
        private VfxParadeManifest _manifest;
        private int _index;
        private int _momentIndex;

        private GameObject _spawnAnchor;     // holds the current spawned effect
        private GameObject _spawnInstance;
        private float _loopTimer;            // re-spawns the effect on a loop
        private const float LoopSeconds = 3f;

        private bool _autoPlaying;
        private float _intervalSeconds = 10f;
        private float _advanceTimer;

        private float _savedTimeScale = 1f;

        // ---- Angle / orbit / zoom (UPGRADE 1) --------------------------------
        // The effect spawns in front of Camera.main (the game camera). To avoid
        // fighting that camera we ORBIT by rotating the spawned effect in place
        // (yaw + pitch on the anchor) and ZOOM by moving the anchor nearer/farther
        // along the camera's forward. Reset per-effect for clarity; auto-spin is
        // sticky across effects.
        private float _yaw;                 // degrees, around world up (relative to facing)
        private float _pitch;               // degrees, tilt
        private float _distance = 4f;       // metres in front of the camera
        private const float MinDistance = 1.5f;
        private const float MaxDistance = 12f;
        private const float DefaultDistance = 4f;

        private bool _autoSpin;
        private const float AutoSpinDegPerSec = 40f;

        // Drag tracking (mouse on desktop, single-touch on mobile).
        private bool _dragging;
        private Vector2 _lastDragPos;
        private const float DragYawSpeed = 0.35f;   // deg per pixel
        private const float DragPitchSpeed = 0.25f; // deg per pixel
        private const float MinPitch = -85f;
        private const float MaxPitch = 85f;

        // Pinch tracking (two-touch zoom on mobile).
        private float _lastPinchDist = -1f;

        private Text _spinLabel;

        // ---- uGUI handles ----------------------------------------------------
        private Canvas _canvas;
        private Text _indexLabel;
        private Text _momentLabel;
        private Text _countLabel;
        private Text _playLabel;
        private Text _filterLabel;
        private InputField _noteField;

        private PickFile _picks = new PickFile();

        // =====================================================================
        // Launch / lifecycle
        // =====================================================================

        /// <summary>Find-or-create the singleton overlay and show it. Called by
        /// AdminOverlay (by reflection) and usable directly. Pauses time.</summary>
        public static VfxParadeRuntime Launch()
        {
            var existing = FindAnyObjectByTypeCompat();
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }

            var go = new GameObject("VfxParadeRuntime");
            DontDestroyOnLoad(go);
            return go.AddComponent<VfxParadeRuntime>();
        }

        private static VfxParadeRuntime FindAnyObjectByTypeCompat()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<VfxParadeRuntime>();
#else
            return UnityEngine.Object.FindAnyObjectByType<VfxParadeRuntime>();
#endif
        }

        private void Awake()
        {
            LoadManifest();
            RebuildFilter();
            LoadPicks();
            BuildUi();
        }

        private void OnEnable()
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f; // freeze the game while curating
            SpawnCurrent();
        }

        private void OnDisable()
        {
            Time.timeScale = _savedTimeScale;
            DestroySpawn();
        }

        private void OnDestroy()
        {
            Time.timeScale = _savedTimeScale;
            DestroySpawn();
        }

        // =====================================================================
        // Manifest
        // =====================================================================
        private void LoadManifest()
        {
            try
            {
                _manifest = Resources.Load<VfxParadeManifest>(VfxParadeManifest.ResourcesPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] manifest load threw: " + e.Message);
                _manifest = null;
            }

            int n = (_manifest != null && _manifest.entries != null) ? _manifest.entries.Count : 0;
            if (_manifest == null)
                Debug.LogWarning("[VfxParade] no manifest at Resources/" + VfxParadeManifest.ResourcesPath +
                                 " - run DeNelle.Editor.VfxParadeManifestBuilder.Build in the editor first.");
            else
                Debug.Log("[VfxParade] manifest loaded with " + n + " entries.");
        }

        private int ManifestCount =>
            (_manifest != null && _manifest.entries != null) ? _manifest.entries.Count : 0;

        // The parade navigates the FILTERED list; "EntryCount" is its size.
        private int EntryCount => _filtered.Count;

        // ---- Filter ----------------------------------------------------------
        private string CurrentFilterToken =>
            FilterTokens[Mathf.Clamp(_filterIndex, 0, FilterTokens.Length - 1)];

        private bool FilterIsAll =>
            string.Equals(CurrentFilterToken, "All", StringComparison.OrdinalIgnoreCase);

        /// <summary>Rebuild _filtered (manifest indices matching the active token),
        /// reset to the first match. Falls back to ALL if a token matches nothing.</summary>
        private void RebuildFilter()
        {
            _filtered.Clear();
            int total = ManifestCount;
            if (total == 0) { _filterPos = 0; _index = 0; return; }

            string token = CurrentFilterToken;
            bool all = FilterIsAll;
            for (int i = 0; i < total; i++)
            {
                var e = _manifest.entries[i];
                if (e == null) continue;
                if (all)
                {
                    _filtered.Add(i);
                    continue;
                }
                string hay = ((e.path ?? "") + " " + (e.name ?? ""));
                if (hay.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    _filtered.Add(i);
            }

            // A token that surfaces nothing would blank the screen - fall back to ALL.
            if (_filtered.Count == 0 && !all)
            {
                Debug.LogWarning("[VfxParade] filter '" + token + "' matched 0 effects - showing ALL.");
                for (int i = 0; i < total; i++)
                    if (_manifest.entries[i] != null) _filtered.Add(i);
            }

            _filterPos = 0;
            _index = _filtered.Count > 0 ? _filtered[0] : 0;
        }

        private void CycleFilter()
        {
            _filterIndex = (_filterIndex + 1) % FilterTokens.Length;
            RebuildFilter();
            _advanceTimer = 0f;
            ResetView();          // fresh look at the first match in the new filter
            SpawnCurrent();
            RefreshLabels();
        }

        private VfxParadeEntry CurrentEntry()
        {
            if (EntryCount == 0) return null;
            _filterPos = Mathf.Clamp(_filterPos, 0, EntryCount - 1);
            _index = _filtered[_filterPos];
            if (_index < 0 || _index >= ManifestCount) return null;
            return _manifest.entries[_index];
        }

        // =====================================================================
        // Effect spawning (a few meters in front of Camera.main, looped)
        // =====================================================================
        private void SpawnCurrent()
        {
            DestroySpawn();
            _loopTimer = 0f;

            var entry = CurrentEntry();
            if (entry == null) { RefreshLabels(); return; }
            if (entry.prefab == null)
            {
                Debug.LogWarning("[VfxParade] entry '" + (entry.name ?? "<null>") +
                                 "' has a null prefab ref - skipping.");
                RefreshLabels();
                return;
            }

            _spawnAnchor = new GameObject("VfxParadeAnchor");
            ApplyView(); // position + orientation from camera + yaw/pitch/distance

            Vector3 pos = _spawnAnchor.transform.position;
            Quaternion rot = _spawnAnchor.transform.rotation;

            try
            {
                _spawnInstance = Instantiate(entry.prefab, pos, rot, _spawnAnchor.transform);
                _spawnInstance.transform.localPosition = Vector3.zero;
                _spawnInstance.transform.localRotation = Quaternion.identity;
                _spawnInstance.SetActive(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to spawn '" + (entry.name ?? "<null>") +
                                 "': " + e.Message);
                DestroySpawn();
            }

            RefreshLabels();
        }

        // Position + orient the anchor from the camera, then apply the orbit
        // (yaw/pitch) + zoom (distance). Cheap enough to call every frame so
        // drag / auto-spin update live without re-spawning the effect.
        private void ApplyView()
        {
            if (_spawnAnchor == null) return;

            Vector3 pos; Quaternion baseRot;
            var cam = Camera.main;
            if (cam != null)
            {
                pos = cam.transform.position + cam.transform.forward * _distance;
                // Base: face the camera (effects author "front" toward -forward).
                baseRot = Quaternion.LookRotation(-cam.transform.forward, Vector3.up);
            }
            else
            {
                pos = new Vector3(0f, 1.5f, 0f);
                baseRot = Quaternion.identity;
            }

            // Orbit = rotate the effect in place under the fixed game camera.
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            _spawnAnchor.transform.position = pos;
            _spawnAnchor.transform.rotation = baseRot * orbit;
        }

        // Reset orbit + zoom for a fresh, predictable view on each new effect.
        // Auto-spin is intentionally NOT reset (sticky, hands-free across effects).
        private void ResetView()
        {
            _yaw = 0f;
            _pitch = 0f;
            _distance = DefaultDistance;
        }

        private void DestroySpawn()
        {
            if (_spawnInstance != null) { Destroy(_spawnInstance); _spawnInstance = null; }
            if (_spawnAnchor != null) { Destroy(_spawnAnchor); _spawnAnchor = null; }
        }

        private void Update()
        {
            // Use unscaled time - the game clock is frozen while curating.
            float dt = Time.unscaledDeltaTime;

            // View interaction: drag-to-orbit, scroll/pinch-zoom, auto-spin.
            HandleViewInput(dt);

            // Keep the anchor parked + oriented to the camera every frame so the
            // orbit/zoom/auto-spin stay live and the view tracks a moving camera.
            ApplyView();

            // Loop the current effect so it replays even when a particle system finishes.
            if (_spawnAnchor != null)
            {
                _loopTimer += dt;
                if (_loopTimer >= LoopSeconds)
                    SpawnCurrent();
            }

            // Auto-advance the parade.
            if (_autoPlaying && EntryCount > 0)
            {
                _advanceTimer += dt;
                if (_advanceTimer >= _intervalSeconds)
                {
                    _advanceTimer = 0f;
                    Next();
                }
            }
        }

        // =====================================================================
        // Angle / orbit / zoom input (mouse + touch). Orbits the effect in place.
        // =====================================================================
        private void HandleViewInput(float dt)
        {
            // Auto-spin: slow continuous yaw, hands-free, sticky across effects.
            if (_autoSpin)
                _yaw = Mathf.Repeat(_yaw + AutoSpinDegPerSec * dt, 360f);

            // --- Touch (mobile): single-touch drag = orbit, two-touch = pinch zoom.
            int touchCount = Input.touchCount;
            if (touchCount >= 2)
            {
                _dragging = false; // pinch takes over
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);
                float d = Vector2.Distance(t0.position, t1.position);
                if (_lastPinchDist > 0f)
                {
                    float delta = d - _lastPinchDist;
                    // pinch out (fingers apart) -> zoom in (closer).
                    Zoom(-delta * 0.01f);
                }
                _lastPinchDist = d;
                return;
            }
            _lastPinchDist = -1f;

            if (touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    if (!IsOverUi(t.position)) { _dragging = true; _lastDragPos = t.position; }
                }
                else if (t.phase == TouchPhase.Moved && _dragging)
                {
                    OrbitByDrag(t.position - _lastDragPos);
                    _lastDragPos = t.position;
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _dragging = false;
                }
                return;
            }

            // --- Mouse (desktop): left-drag = orbit, wheel = zoom.
            if (Input.GetMouseButtonDown(0) && !IsOverUi(Input.mousePosition))
            {
                _dragging = true;
                _lastDragPos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
            }
            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 cur = Input.mousePosition;
                OrbitByDrag(cur - _lastDragPos);
                _lastDragPos = cur;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
                Zoom(-scroll * 0.5f);
        }

        private void OrbitByDrag(Vector2 deltaPixels)
        {
            _yaw = Mathf.Repeat(_yaw - deltaPixels.x * DragYawSpeed, 360f);
            _pitch = Mathf.Clamp(_pitch + deltaPixels.y * DragPitchSpeed, MinPitch, MaxPitch);
        }

        private void Zoom(float deltaMetres)
        {
            _distance = Mathf.Clamp(_distance + deltaMetres, MinDistance, MaxDistance);
        }

        // Don't start a drag when the pointer is over the control panel.
        private bool IsOverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }

        // ---- Preset angle snaps (buttons) ------------------------------------
        private void PresetFront() { _yaw = 0f;   _pitch = 0f;  RefreshLabels(); }
        private void PresetSide()  { _yaw = 90f;  _pitch = 0f;  RefreshLabels(); }
        private void PresetTop()   { _yaw = 0f;   _pitch = 80f; RefreshLabels(); }
        private void Preset45()    { _yaw = 45f;  _pitch = 30f; RefreshLabels(); }

        private void ToggleSpin()
        {
            _autoSpin = !_autoSpin;
            RefreshLabels();
        }

        private void Next()
        {
            if (EntryCount == 0) return;
            _filterPos = (_filterPos + 1) % EntryCount;
            _advanceTimer = 0f;
            ResetView();
            SpawnCurrent();
        }

        private void Prev()
        {
            if (EntryCount == 0) return;
            _filterPos = (_filterPos - 1 + EntryCount) % EntryCount;
            _advanceTimer = 0f;
            ResetView();
            SpawnCurrent();
        }

        private void CycleMoment()
        {
            _momentIndex = (_momentIndex + 1) % Moments.Length;
            RefreshLabels();
        }

        private void TogglePlay()
        {
            _autoPlaying = !_autoPlaying;
            _advanceTimer = 0f;
            RefreshLabels();
        }

        // =====================================================================
        // uGUI overlay (code-built, Screen Space Overlay)
        // =====================================================================
        private void BuildUi()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32500; // above the admin overlay (32000)

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            // Top index/name banner (large).
            _indexLabel = MakeLabel(_canvas.transform, "[0 / 0]  <none>", 34, TextAnchor.MiddleCenter);
            Anchor(_indexLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(20f, -90f), new Vector2(-20f, -20f));

            // Bottom control panel background. Tall enough for 5 rows.
            var panel = MakePanel(_canvas.transform);
            Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                   new Vector2(20f, 20f), new Vector2(-20f, 490f));

            // Row 1 (top): FILTER + view PRESETS + AUTO-SPIN (the "find gems" + angle row).
            _filterLabel = MakeButton(panel, "filter: All", new Vector2(20f, 430f), new Vector2(280f, 480f), CycleFilter);
            MakeButton(panel, "Front", new Vector2(300f, 430f), new Vector2(420f, 480f), PresetFront);
            MakeButton(panel, "Side", new Vector2(430f, 430f), new Vector2(550f, 480f), PresetSide);
            MakeButton(panel, "Top", new Vector2(560f, 430f), new Vector2(680f, 480f), PresetTop);
            MakeButton(panel, "45", new Vector2(690f, 430f), new Vector2(810f, 480f), Preset45);
            _spinLabel = MakeButton(panel, "Spin: off", new Vector2(820f, 430f), new Vector2(1010f, 480f), ToggleSpin);

            // Row 2: zoom hint + zoom buttons (drag to orbit, wheel/pinch to zoom).
            MakeButton(panel, "Zoom -", new Vector2(20f, 370f), new Vector2(170f, 420f), () => Zoom(1f));
            MakeButton(panel, "Zoom +", new Vector2(180f, 370f), new Vector2(330f, 420f), () => Zoom(-1f));
            var hint = MakeLabel(panel, "drag to orbit  -  wheel/pinch to zoom", 20, TextAnchor.MiddleLeft);
            Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(350f, 370f), new Vector2(1010f, 420f));

            // Row 3: transport.
            MakeButton(panel, "< Prev", new Vector2(20f, 310f), new Vector2(170f, 360f), Prev);
            _playLabel = MakeButton(panel, "Play", new Vector2(180f, 310f), new Vector2(330f, 360f), TogglePlay);
            MakeButton(panel, "Next >", new Vector2(340f, 310f), new Vector2(490f, 360f), Next);
            MakeButton(panel, "- interval", new Vector2(520f, 310f), new Vector2(670f, 360f),
                       () => SetInterval(_intervalSeconds - 5f));
            MakeButton(panel, "+ interval", new Vector2(680f, 310f), new Vector2(830f, 360f),
                       () => SetInterval(_intervalSeconds + 5f));

            // Row 4: moment selector + note field.
            _momentLabel = MakeButton(panel, "moment: cast", new Vector2(20f, 250f), new Vector2(330f, 300f), CycleMoment);
            _noteField = MakeInputField(panel, new Vector2(340f, 250f), new Vector2(900f, 300f), "note (how you'd use it)...");

            // Row 5 (bottom): bookmark + count + close.
            MakeButton(panel, "BOOKMARK", new Vector2(20f, 20f), new Vector2(330f, 230f), BookmarkCurrent);
            _countLabel = MakeLabel(panel, "Picks: 0", 26, TextAnchor.MiddleLeft);
            Anchor(_countLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(350f, 20f), new Vector2(700f, 230f));
            MakeButton(panel, "Close", new Vector2(720f, 20f), new Vector2(900f, 230f), Close);

            RefreshLabels();
        }

        private void Close()
        {
            // Destroy the whole overlay (restores timescale via OnDestroy).
            Destroy(gameObject);
        }

        private void SetInterval(float v)
        {
            _intervalSeconds = Mathf.Clamp(v, 5f, 60f);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            int total = EntryCount;               // size of the active filter
            int human = total == 0 ? 0 : _filterPos + 1;
            var entry = CurrentEntry();
            string name = entry != null ? (entry.name ?? "<unnamed>") : "<none>";
            string token = CurrentFilterToken;
            string tag = FilterIsAll ? "" : ("  {" + token + "}");
            if (_indexLabel != null) _indexLabel.text = "[" + human + " / " + total + "]" + tag + "  " + name;

            if (_filterLabel != null)
                _filterLabel.text = "filter: " + token;

            if (_spinLabel != null)
                _spinLabel.text = _autoSpin ? "Spin: ON" : "Spin: off";

            string moment = Moments[Mathf.Clamp(_momentIndex, 0, Moments.Length - 1)];
            if (_momentLabel != null)
                _momentLabel.text = "moment: " + moment;

            if (_playLabel != null)
                _playLabel.text = _autoPlaying ? ("Pause (" + (int)_intervalSeconds + "s)") : "Play";

            if (_countLabel != null)
            {
                int n = (_picks != null && _picks.picks != null) ? _picks.picks.Count : 0;
                _countLabel.text = "Picks: " + n;
            }
        }

        // ---- uGUI factory helpers -------------------------------------------
        private static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#else
            var es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#endif
            if (es != null) return;
            var go = new GameObject("EventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                   Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static RectTransform MakePanel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
            return go.GetComponent<RectTransform>();
        }

        private Text MakeLabel(Transform parent, string text, int fontSize, TextAnchor align)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = BuiltinFont();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // Returns the button's Text so the caller can re-label it (Play/Pause, moment).
        private Text MakeButton(RectTransform parent, string label,
                                Vector2 min, Vector2 max, Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.offsetMin = min; rt.offsetMax = max;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.20f, 0.28f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = MakeLabel(rt, label, 22, TextAnchor.MiddleCenter);
            Anchor(txt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return txt;
        }

        private InputField MakeInputField(RectTransform parent, Vector2 min, Vector2 max, string placeholder)
        {
            var go = new GameObject("NoteField", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.offsetMin = min; rt.offsetMax = max;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.10f, 0.13f, 1f);

            var field = go.AddComponent<InputField>();
            field.targetGraphic = img;

            var ph = MakeLabel(rt, placeholder, 20, TextAnchor.MiddleLeft);
            ph.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

            var txt = MakeLabel(rt, "", 20, TextAnchor.MiddleLeft);
            txt.supportRichText = false;
            Anchor(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

            field.textComponent = txt;
            field.placeholder = ph;
            return field;
        }

        private static Font BuiltinFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        // =====================================================================
        // Picks file IO (load-append-write, never clobber)
        // persistentDataPath/vfx-picks.json
        // =====================================================================
        private static string PicksPath =>
            Path.Combine(Application.persistentDataPath, "vfx-picks.json");

        private void LoadPicks()
        {
            _picks = new PickFile();
            try
            {
                string p = PicksPath;
                if (File.Exists(p))
                {
                    string json = File.ReadAllText(p);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var loaded = JsonUtility.FromJson<PickFile>(json);
                        if (loaded != null && loaded.picks != null)
                            _picks = loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to load picks file; starting fresh: " + e.Message);
                _picks = new PickFile();
            }
            if (_picks.picks == null) _picks.picks = new List<Pick>();
        }

        private void BookmarkCurrent()
        {
            var entry = CurrentEntry();
            if (entry == null)
            {
                Debug.LogWarning("[VfxParade] cannot bookmark: no current effect.");
                return;
            }

            // Re-load first so we never clobber picks written by another session.
            LoadPicks();

            string moment = Moments[Mathf.Clamp(_momentIndex, 0, Moments.Length - 1)];
            string note = (_noteField != null && _noteField.text != null) ? _noteField.text : "";

            _picks.picks.Add(new Pick
            {
                path = entry.path,
                name = entry.name,
                moment = moment,
                note = note
            });

            WritePicks();
            if (_noteField != null) _noteField.text = "";
            RefreshLabels();
            Debug.Log("[VfxParade] bookmarked '" + entry.name + "' as moment '" + moment +
                      "' -> " + PicksPath);
        }

        private void WritePicks()
        {
            try
            {
                string p = PicksPath;
                string dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonUtility.ToJson(_picks, true);
                File.WriteAllText(p, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to write picks file '" + PicksPath + "': " + e.Message);
            }
        }
    }
}
