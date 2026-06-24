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

        // ---- uGUI handles ----------------------------------------------------
        private Canvas _canvas;
        private Text _indexLabel;
        private Text _momentLabel;
        private Text _countLabel;
        private Text _playLabel;
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
            return UnityEngine.Object.FindObjectOfType<VfxParadeRuntime>();
#endif
        }

        private void Awake()
        {
            LoadManifest();
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

        private int EntryCount =>
            (_manifest != null && _manifest.entries != null) ? _manifest.entries.Count : 0;

        private VfxParadeEntry CurrentEntry()
        {
            if (EntryCount == 0) return null;
            _index = Mathf.Clamp(_index, 0, EntryCount - 1);
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

            Vector3 pos; Quaternion rot;
            ComputeAnchor(out pos, out rot);

            _spawnAnchor = new GameObject("VfxParadeAnchor");
            _spawnAnchor.transform.position = pos;
            _spawnAnchor.transform.rotation = rot;

            try
            {
                _spawnInstance = Instantiate(entry.prefab, pos, rot, _spawnAnchor.transform);
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

        private void ComputeAnchor(out Vector3 pos, out Quaternion rot)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                pos = cam.transform.position + cam.transform.forward * 4f;
                rot = Quaternion.LookRotation(-cam.transform.forward, Vector3.up);
            }
            else
            {
                pos = new Vector3(0f, 1.5f, 0f);
                rot = Quaternion.identity;
            }
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

        private void Next()
        {
            if (EntryCount == 0) return;
            _index = (_index + 1) % EntryCount;
            _advanceTimer = 0f;
            SpawnCurrent();
        }

        private void Prev()
        {
            if (EntryCount == 0) return;
            _index = (_index - 1 + EntryCount) % EntryCount;
            _advanceTimer = 0f;
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

            // Bottom control panel background.
            var panel = MakePanel(_canvas.transform);
            Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                   new Vector2(20f, 20f), new Vector2(-20f, 250f));

            // Row 1: transport.
            MakeButton(panel, "< Prev", new Vector2(20f, 190f), new Vector2(170f, 240f), Prev);
            _playLabel = MakeButton(panel, "Play", new Vector2(180f, 190f), new Vector2(330f, 240f), TogglePlay);
            MakeButton(panel, "Next >", new Vector2(340f, 190f), new Vector2(490f, 240f), Next);
            MakeButton(panel, "- interval", new Vector2(520f, 190f), new Vector2(670f, 240f),
                       () => SetInterval(_intervalSeconds - 5f));
            MakeButton(panel, "+ interval", new Vector2(680f, 190f), new Vector2(830f, 240f),
                       () => SetInterval(_intervalSeconds + 5f));

            // Row 2: moment selector + note field.
            _momentLabel = MakeButton(panel, "moment: cast", new Vector2(20f, 130f), new Vector2(330f, 180f), CycleMoment);
            _noteField = MakeInputField(panel, new Vector2(340f, 130f), new Vector2(900f, 180f), "note (how you'd use it)...");

            // Row 3: bookmark + count + close.
            MakeButton(panel, "BOOKMARK", new Vector2(20f, 20f), new Vector2(330f, 110f), BookmarkCurrent);
            _countLabel = MakeLabel(panel, "Picks: 0", 26, TextAnchor.MiddleLeft);
            Anchor(_countLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(350f, 20f), new Vector2(700f, 110f));
            MakeButton(panel, "Close", new Vector2(720f, 20f), new Vector2(900f, 110f), Close);

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
            int total = EntryCount;
            int human = total == 0 ? 0 : _index + 1;
            var entry = CurrentEntry();
            string name = entry != null ? (entry.name ?? "<unnamed>") : "<none>";
            if (_indexLabel != null) _indexLabel.text = "[" + human + " / " + total + "]  " + name;

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
            var es = UnityEngine.Object.FindObjectOfType<EventSystem>();
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
