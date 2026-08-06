// =============================================================================
// VfxCasterWindow — VFX audition booth (rebuild 2026-07-24).
// -----------------------------------------------------------------------------
// Defenders > Animation > VFX Caster
//
// WHY REBUILT: PreviewRenderUtility + partial Simulate made multi-layer packs
// (Lana Flamethrower, Spells, Hovl) show grey boxes, one layer only, or nothing.
// D:\flames works because effects live in a real scene under URP Update.
//
// ARCHITECTURE (best path for URP):
//   1. Library — scan ALL VFX pack roots + catalog → carousel.
//   2. Stage — DontSave GameObject in the active scene holds the prefab instance.
//   3. Playback — every ParticleSystem Play + Simulate(dt) (all layers).
//   4. Materials — remap Built-in Particles/* → URP Particles/Unlit on the INSTANCE.
//   5. Helpers — hide MeshRenderer cubes (MagentaFix grey boxes) on the INSTANCE.
//   6. View — dedicated URP camera on the stage renders into a window RT (void bg).
//
// Prefab assets on disk are NEVER modified. Mental model (WO-758): prefab = recipe.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeNelle.Editor
{
    public sealed class VfxCasterWindow : EditorWindow
    {
        private const string HovlCatalogAssetPath = "Assets/Resources/VFX/HovlVfxCatalog.asset";
        private const string LibraryIndexPath = "Assets/Editor/VfxCasterLibraryIndex.json";
        private const string StageRootName = "__VFX_Caster_Stage__";
        private const string Log = "[VfxCaster] ";

        private static readonly string[] KnownVfxRoots =
        {
            "Assets/Hovl Studio",
            "Assets/UnityTechnologies/ParticlePack",
            "Assets/Spells Pack",
            "Assets/Mirza Beig/Particle Systems",
            "Assets/Lana Studio/Casual RPG VFX",
            "Assets/Art/VFX",
            "Assets/Resources/VFX",
            "Assets/VfxParade",
            "Assets/_Modules/Village/Vfx",
            "Assets/_Modules/Core/VFX",
        };

        private static readonly string[] TagRoles = { "Cast", "Projectile", "Impact", "Aura" };

        private sealed class VfxEntry
        {
            public string Key;
            public string Path;
            public string Label;
            public string Pack;
            public bool Catalogued;
        }

        // Library
        private List<VfxEntry> _library = new List<VfxEntry>();
        private List<VfxEntry> _filtered = new List<VfxEntry>();
        private VfxEntry _selected;
        private string _search = "";
        private bool _cataloguedOnly;
        private bool _requireParticleSystem = true;
        private Vector2 _libScroll;
        private List<string> _packNames = new List<string>();
        private readonly Dictionary<string, bool> _packEnabled =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Carousel
        private int _carouselIndex;
        private bool _autoAdvance;
        private float _autoInterval = 10f;
        private double _autoLastAdvance;

        // Stage + playback
        private GameObject _stageRoot;
        private Camera _stageCam;
        private GameObject _instance;
        private ParticleSystem[] _allSystems = Array.Empty<ParticleSystem>();
        private string[] _layerNames = Array.Empty<string>();
        private RenderTexture _rt;
        private float _time;
        private bool _playing = true;
        private bool _loop = true;
        private float _duration = 6f;
        private bool _looping;
        private float _orbitYaw = 25f;
        private float _camDistance = 8f;
        private float _camHeight = 1.4f;
        private double _lastTick;
        private int _psCount;
        private int _hiddenMeshes;
        private int _fixedMats;
        private int _brokenShaderCount;
        private string[] _shaderAudit = Array.Empty<string>();
        private Vector2 _infoScroll;

        // Tag
        private string _tagBaseName = "";
        // IsLoop is NOT an authoring preference -- it is a FACT about the prefab, so this
        // field is DERIVED, never typed. It used to be a free-floating sticky checkbox that
        // was additionally force-set true for the Projectile/Aura roles, and whatever it
        // held got written into the catalog row. That is how 95 of 135 HovlVfxCatalog rows
        // ended up IsLoop:1 while being rate-0 burst prefabs (PP_BigExplosion,
        // PP_MuzzleFlash, PP_EarthShatter ...). A loop row NEVER auto-returns its pool slot
        // -- VFXManager.Hovl.cs ~283-288 bumps _activeLoops and registers no reclaim
        // deadline, and the only loop reclaim (PruneDestroyedFromSet, VFXManager.cs ~973)
        // frees DESTROYED hosts, which pooled objects never are. So every fire-and-forget
        // play of a mis-flagged burst permanently burned one of the 20 slots
        // (_maxActiveLoops, VFXManager.cs:142); six F8 captures caught the cap saturated at
        // 20/20 and starving a live effect. The prefab is the authority now.
        private bool _tagLoop;
        private string _tagLoopPath;      // path _tagLoop was derived from (re-derive on change)
        private string _tagLoopDetail = "";
        private Dictionary<string, List<string>> _overlayKeysByPath =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Defenders/Animation/VFX Caster")]
        public static void Open()
        {
            var w = GetWindow<VfxCasterWindow>("VFX Caster");
            w.minSize = new Vector2(920f, 560f);
            w.Show();
        }

        private void OnEnable()
        {
            _cataloguedOnly = EditorPrefs.GetBool("VfxCaster.CataloguedOnly", false);
            _requireParticleSystem = EditorPrefs.GetBool("VfxCaster.RequirePS", true);
            _autoAdvance = EditorPrefs.GetBool("VfxCaster.AutoAdvance", false);
            _autoInterval = EditorPrefs.GetFloat("VfxCaster.AutoInterval", 10f);
            ScanLibrary();
            _lastTick = EditorApplication.timeSinceStartup;
            _autoLastAdvance = _lastTick;
            EditorApplication.update += OnEditorTick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorTick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            TeardownStage(destroyRoot: true);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Stage is DontSave — rebuild selection after domain/play transitions.
            if (state == PlayModeStateChange.EnteredEditMode && _selected != null)
                SpawnSelected();
        }

        private void OnEditorTick()
        {
            double now = EditorApplication.timeSinceStartup;

            if (_autoAdvance && _filtered.Count > 0 &&
                now - _autoLastAdvance >= Mathf.Max(2f, _autoInterval))
            {
                _autoLastAdvance = now;
                CarouselNext();
            }

            float dt = Mathf.Clamp((float)(now - _lastTick), 0f, 0.1f);
            _lastTick = now;

            if (_playing && _instance != null && dt > 0f)
            {
                _time += dt;
                if (_time > 3600f) _time = 0f;
                AdvanceAllSystems(dt);
                SceneView.RepaintAll();
                Repaint();
            }
            else if (_instance != null)
            {
                // Keep RT fresh while scrubbing/paused.
                Repaint();
            }
        }

        // =====================================================================
        //  Library scan — all VFX folders
        // =====================================================================

        private void ScanLibrary()
        {
            string keep = _selected?.Path;
            _library = new List<VfxEntry>();
            var cataloguedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Catalog keys
            var cat = AssetDatabase.LoadMainAssetAtPath(HovlCatalogAssetPath);
            if (cat != null)
            {
                var so = new SerializedObject(cat);
                var rows = so.FindProperty("Rows");
                if (rows != null && rows.isArray)
                {
                    for (int i = 0; i < rows.arraySize; i++)
                    {
                        var row = rows.GetArrayElementAtIndex(i);
                        string key = row.FindPropertyRelative("Key")?.stringValue;
                        var prefab = row.FindPropertyRelative("Prefab")?.objectReferenceValue as GameObject;
                        if (string.IsNullOrEmpty(key) || prefab == null) continue;
                        string path = AssetDatabase.GetAssetPath(prefab);
                        cataloguedPaths.Add(path);
                        string pack = InferPack(path);
                        _library.Add(new VfxEntry
                        {
                            Key = key,
                            Path = path,
                            Pack = pack,
                            Catalogued = true,
                            Label = $"[{pack}] {key}  [catalogued]",
                        });
                    }
                }
            }

            RefreshOverlayLookup();

            foreach (var root in DiscoverVfxRoots())
                AddPackPrefabs(root, PackLabelFromRoot(root), cataloguedPaths);

            _library.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            RebuildPackFilterList();
            RebuildFiltered();
            WriteLibraryIndex();

            if (!string.IsNullOrEmpty(keep))
            {
                int idx = _filtered.FindIndex(e =>
                    string.Equals(e.Path, keep, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _carouselIndex = idx;
                    _selected = _filtered[idx];
                }
            }

            Debug.Log(Log + $"scan complete: {_library.Count} prefabs, filtered={_filtered.Count}.");
        }

        private static List<string> DiscoverVfxRoots()
        {
            var roots = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string p)
            {
                if (string.IsNullOrEmpty(p) || !AssetDatabase.IsValidFolder(p)) return;
                if (seen.Add(p)) roots.Add(p);
            }
            foreach (var r in KnownVfxRoots) Add(r);
            foreach (string guid in AssetDatabase.FindAssets("t:DefaultAsset", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) continue;
                if (path.Split('/').Length > 3) continue;
                string name = System.IO.Path.GetFileName(path);
                if (LooksLikeVfxFolder(name) || LooksLikeVfxFolder(path))
                    Add(path);
            }
            roots.Sort(StringComparer.OrdinalIgnoreCase);
            return roots;
        }

        private static bool LooksLikeVfxFolder(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            return s.Contains("vfx") || s.Contains("particle") || s.Contains("spell")
                || s.Contains("hovl") || s.Contains("effect") || s.Contains("mirza")
                || s.Contains("lana") || s.Contains("flame") || s.Contains("projectile");
        }

        private static string PackLabelFromRoot(string root)
        {
            if (root.IndexOf("ParticlePack", StringComparison.OrdinalIgnoreCase) >= 0) return "ParticlePack";
            if (root.IndexOf("Hovl", StringComparison.OrdinalIgnoreCase) >= 0) return "Hovl";
            if (root.IndexOf("Spells Pack", StringComparison.OrdinalIgnoreCase) >= 0) return "Spells";
            if (root.IndexOf("Mirza", StringComparison.OrdinalIgnoreCase) >= 0) return "Mirza";
            if (root.IndexOf("Lana", StringComparison.OrdinalIgnoreCase) >= 0) return "Lana";
            if (root.IndexOf("Art/VFX", StringComparison.OrdinalIgnoreCase) >= 0) return "ArtVFX";
            if (root.IndexOf("Resources/VFX", StringComparison.OrdinalIgnoreCase) >= 0) return "ResourcesVFX";
            return System.IO.Path.GetFileName(root.TrimEnd('/'));
        }

        private static string InferPack(string path)
        {
            foreach (var root in KnownVfxRoots)
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return PackLabelFromRoot(root);
            return PackLabelFromRoot(path);
        }

        private void AddPackPrefabs(string packRoot, string packLabel, HashSet<string> cataloguedPaths)
        {
            if (!AssetDatabase.IsValidFolder(packRoot)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { packRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || cataloguedPaths.Contains(path)) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf("Demo", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf("EffectExamples", StringComparison.OrdinalIgnoreCase) < 0 &&
                    path.IndexOf("/Prefabs/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (_requireParticleSystem && !PrefabHasParticleSystem(path)) continue;

                bool tagged = _overlayKeysByPath.ContainsKey(path);
                _library.Add(new VfxEntry
                {
                    Key = "",
                    Path = path,
                    Pack = packLabel,
                    Catalogued = false,
                    Label = $"[{packLabel}] {file}  {(tagged ? "[tagged - regenerate]" : "[uncatalogued]")}",
                });
            }
        }

        private static bool PrefabHasParticleSystem(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null && go.GetComponentInChildren<ParticleSystem>(true) != null;
        }

        private void RebuildPackFilterList()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in _library)
                if (!string.IsNullOrEmpty(e.Pack)) names.Add(e.Pack);
            _packNames = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var n in _packNames)
                if (!_packEnabled.ContainsKey(n)) _packEnabled[n] = true;
        }

        private void RebuildFiltered()
        {
            _filtered = new List<VfxEntry>();
            foreach (var e in _library)
            {
                if (_cataloguedOnly && !e.Catalogued) continue;
                string pack = e.Pack ?? "Other";
                if (_packEnabled.TryGetValue(pack, out bool on) && !on) continue;
                if (!string.IsNullOrEmpty(_search) &&
                    e.Label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    e.Path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                _filtered.Add(e);
            }
            if (_filtered.Count == 0) { _carouselIndex = 0; return; }
            _carouselIndex = Mathf.Clamp(_carouselIndex, 0, _filtered.Count - 1);
        }

        private void WriteLibraryIndex()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"scannedUtc\": \"{DateTime.UtcNow:o}\",");
                sb.AppendLine($"  \"count\": {_library.Count},");
                sb.AppendLine("  \"entries\": [");
                for (int i = 0; i < _library.Count; i++)
                {
                    var e = _library[i];
                    string path = (e.Path ?? "").Replace("\\", "/").Replace("\"", "\\\"");
                    string pack = (e.Pack ?? "").Replace("\"", "\\\"");
                    string key = (e.Key ?? "").Replace("\"", "\\\"");
                    sb.Append("    {\"pack\":\"").Append(pack).Append("\",\"key\":\"").Append(key)
                        .Append("\",\"catalogued\":").Append(e.Catalogued ? "true" : "false")
                        .Append(",\"path\":\"").Append(path).Append("\"}");
                    sb.AppendLine(i < _library.Count - 1 ? "," : "");
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                System.IO.File.WriteAllText(LibraryIndexPath, sb.ToString());
            }
            catch (Exception ex) { Debug.LogWarning(Log + "index write: " + ex.Message); }
        }

        private void RefreshOverlayLookup()
        {
            _overlayKeysByPath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in HovlVfxCatalogGenerator.ReadManualPicks())
            {
                if (string.IsNullOrEmpty(row.key) || string.IsNullOrEmpty(row.prefabPath)) continue;
                if (!_overlayKeysByPath.TryGetValue(row.prefabPath, out var keys))
                {
                    keys = new List<string>();
                    _overlayKeysByPath[row.prefabPath] = keys;
                }
                keys.Add(row.key);
            }
        }

        private void CarouselPrev()
        {
            RebuildFiltered();
            if (_filtered.Count == 0) return;
            _carouselIndex = (_carouselIndex - 1 + _filtered.Count) % _filtered.Count;
            SelectEntry(_filtered[_carouselIndex]);
            _autoLastAdvance = EditorApplication.timeSinceStartup;
        }

        private void CarouselNext()
        {
            RebuildFiltered();
            if (_filtered.Count == 0) return;
            _carouselIndex = (_carouselIndex + 1) % _filtered.Count;
            SelectEntry(_filtered[_carouselIndex]);
            _autoLastAdvance = EditorApplication.timeSinceStartup;
        }

        // =====================================================================
        //  Stage (real scene object + URP camera) — the reliable path
        // =====================================================================

        private void EnsureStage()
        {
            if (_stageRoot == null)
                _stageRoot = GameObject.Find(StageRootName);

            if (_stageRoot == null)
            {
                _stageRoot = new GameObject(StageRootName);
                _stageRoot.hideFlags = HideFlags.DontSave;
                Undo.ClearUndo(_stageRoot);
            }

            if (_stageCam == null)
            {
                var camGo = _stageRoot.transform.Find("PreviewCam");
                if (camGo == null)
                {
                    var c = new GameObject("PreviewCam");
                    c.hideFlags = HideFlags.DontSave;
                    c.transform.SetParent(_stageRoot.transform, false);
                    camGo = c.transform;
                }
                _stageCam = camGo.GetComponent<Camera>();
                if (_stageCam == null) _stageCam = camGo.gameObject.AddComponent<Camera>();
                _stageCam.enabled = false; // we call Render() manually
                _stageCam.fieldOfView = 35f;
                _stageCam.nearClipPlane = 0.05f;
                _stageCam.farClipPlane = 200f;
                _stageCam.allowHDR = true;
                _stageCam.depthTextureMode = DepthTextureMode.Depth;
                _stageCam.clearFlags = CameraClearFlags.SolidColor;
                ApplyVoidClear(_stageCam);

                // URP camera data so particles/shaders match Play Mode.
                var urp = camGo.GetComponent<UniversalAdditionalCameraData>();
                if (urp == null) urp = camGo.gameObject.AddComponent<UniversalAdditionalCameraData>();
                urp.renderType = CameraRenderType.Base;
                urp.renderPostProcessing = false;
            }

            EnsureRt(512, 320);
        }

        private void EnsureRt(int w, int h)
        {
            if (_rt != null && _rt.IsCreated() && _rt.width == w && _rt.height == h) return;
            if (_rt != null)
            {
                if (_stageCam != null) _stageCam.targetTexture = null;
                _rt.Release();
                DestroyImmediate(_rt);
            }
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "VfxCasterRT",
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _rt.Create();
            if (_stageCam != null) _stageCam.targetTexture = _rt;
        }

        private static void ApplyVoidClear(Camera cam)
        {
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color c = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f, 0f)
                : new Color(0.76f, 0.76f, 0.76f, 0f);
            cam.backgroundColor = c;
        }

        private void TeardownStage(bool destroyRoot)
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance);
                _instance = null;
            }
            _allSystems = Array.Empty<ParticleSystem>();
            _layerNames = Array.Empty<string>();
            _psCount = 0;
            _hiddenMeshes = 0;
            _fixedMats = 0;

            if (_stageCam != null) _stageCam.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                DestroyImmediate(_rt);
                _rt = null;
            }

            if (destroyRoot && _stageRoot != null)
            {
                DestroyImmediate(_stageRoot);
                _stageRoot = null;
                _stageCam = null;
            }
        }

        private void SelectEntry(VfxEntry entry)
        {
            _selected = entry;
            SpawnSelected();
        }

        private void SpawnSelected()
        {
            if (_selected == null) return;
            EnsureStage();

            // Clear previous instance
            if (_instance != null) DestroyImmediate(_instance);
            _instance = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_selected.Path);
            if (prefab == null)
            {
                Debug.LogWarning(Log + "missing prefab: " + _selected.Path);
                return;
            }

            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _stageRoot.transform);
            if (_instance == null)
            {
                _instance = Instantiate(prefab, _stageRoot.transform);
            }
            _instance.name = prefab.name + " (VFX Caster)";
            _instance.hideFlags = HideFlags.DontSave;
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _instance.transform.localScale = Vector3.one;

            // Activate entire tree (some packs leave children off).
            foreach (var t in _instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);

            // Fix Built-in particle shaders → URP on THIS instance only.
            _fixedMats = RemapBrokenParticleMaterials(_instance);

            // Hide MagentaFix / Lit helper meshes (Lana Flamethrower grey boxes).
            _hiddenMeshes = HideNonParticleMeshHelpers(_instance);

            _allSystems = _instance.GetComponentsInChildren<ParticleSystem>(true);
            _psCount = _allSystems.Length;
            _layerNames = _allSystems.Select(ps => ps != null ? ps.gameObject.name : "?").ToArray();

            MeasureDuration();
            AuditShaders(_instance);
            DemoPlayFromStart();
            FrameCamera();

            // Select stage in hierarchy so Scene view is easy to find.
            Selection.activeGameObject = _instance;
            Debug.Log(Log + $"spawned '{prefab.name}' layers={_psCount} matFixes={_fixedMats} hidMeshes={_hiddenMeshes}");
        }

        private void MeasureDuration()
        {
            _duration = 2f;
            _looping = false;
            foreach (var ps in _allSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                if (main.loop) _looping = true;
                float life = main.startLifetime.constantMax;
                float delay = main.startDelay.constantMax;
                float end = main.duration + life + delay;
                if (end > _duration) _duration = end;
            }
            if (_looping) _duration = Mathf.Max(_duration, 6f);
            _duration = Mathf.Clamp(_duration, 1f, 30f);
        }

        private void DemoPlayFromStart()
        {
            _time = 0f;
            _playing = true;
            _lastTick = EditorApplication.timeSinceStartup;
            if (_allSystems == null) return;

            foreach (var ps in _allSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                if (_loop) main.loop = true;
                main.playOnAwake = false;
                var emission = ps.emission;
                emission.enabled = true;
                var ren = ps.GetComponent<ParticleSystemRenderer>();
                if (ren != null) ren.enabled = true;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
            }

            // Play EACH system (sibling layers under empty roots need this).
            foreach (var ps in _allSystems)
            {
                if (ps == null) continue;
                ps.Play(withChildren: false);
            }

            // Warm-up for rate-over-time density.
            for (int i = 0; i < 15; i++)
                AdvanceAllSystems(1f / 30f);
            _time = 0.5f;
        }

        private void AdvanceAllSystems(float dt)
        {
            if (_allSystems == null || dt <= 0f) return;
            foreach (var ps in _allSystems)
            {
                if (ps == null) continue;
                if (!ps.gameObject.activeInHierarchy) ps.gameObject.SetActive(true);
                if (!ps.isPlaying) ps.Play(withChildren: false);
                // false restart = continuous stream; fixedTimeStep false = smoother editor dt
                ps.Simulate(dt, withChildren: false, restart: false, fixedTimeStep: false);
            }
        }

        private void ResimulateAbsolute(float t)
        {
            if (_allSystems == null) return;
            foreach (var ps in _allSystems)
            {
                if (ps == null) continue;
                ps.Simulate(t, withChildren: false, restart: true, fixedTimeStep: true);
            }
        }

        private void FrameCamera()
        {
            if (_stageCam == null) return;
            float dist = Mathf.Clamp(_camDistance, 2f, 40f);
            var rot = Quaternion.Euler(12f, _orbitYaw, 0f);
            var focus = new Vector3(0f, _camHeight, 0f);
            _stageCam.transform.position = focus + rot * (Vector3.back * dist);
            _stageCam.transform.rotation = rot;
            ApplyVoidClear(_stageCam);
        }

        private void RenderPreviewToRt(Rect rect)
        {
            if (_stageCam == null || _instance == null) return;
            int w = Mathf.Max(64, Mathf.RoundToInt(rect.width));
            int h = Mathf.Max(64, Mathf.RoundToInt(rect.height));
            EnsureRt(w, h);
            FrameCamera();
            _stageCam.targetTexture = _rt;
            _stageCam.Render();
        }

        // =====================================================================
        //  Material / mesh hygiene (instance only)
        // =====================================================================

        private static int HideNonParticleMeshHelpers(GameObject root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r is ParticleSystemRenderer) continue;
                if (r is TrailRenderer || r is LineRenderer) continue;
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    r.enabled = false;
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// Aggressive magenta heal for preview instances only.
        /// Spells Pack / Lana / Built-in Default-Particle use Particles/Standard Unlit etc.
        /// which render MAGENTA or black under URP. Recipe mirrors MagentaMaterialFixer +
        /// LanaUrpMaterialFix / VFXManager.ConfigureUrpParticleBlend.
        /// </summary>
        private static int RemapBrokenParticleMaterials(GameObject root)
        {
            Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticles == null)
            {
                Debug.LogError(Log + "URP Particles/Unlit shader missing — cannot heal magenta.");
                return 0;
            }

            // Shared soft particle texture fallback (built-in Default-Particle).
            Texture2D defaultParticleTex =
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");

            int fixedCount = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                // Only heal particle (and trail) renderers — mesh helpers are hidden separately.
                bool isParticle = r is ParticleSystemRenderer;
                bool isTrail = r is TrailRenderer || r is LineRenderer;
                if (!isParticle && !isTrail) continue;

                // Force instance materials so we never dirty pack assets.
                var mats = r.materials;
                if (mats == null || mats.Length == 0)
                {
                    // Null slot → assign a default URP particle mat.
                    var fallback = BuildUrpParticleMaterial(urpParticles, null, Color.white, true, defaultParticleTex);
                    r.sharedMaterial = fallback;
                    fixedCount++;
                    continue;
                }

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                    {
                        mats[i] = BuildUrpParticleMaterial(urpParticles, null, Color.white, true, defaultParticleTex);
                        changed = true;
                        fixedCount++;
                        continue;
                    }

                    string sn = m.shader != null ? m.shader.name : "";
                    bool alreadyUrpParticle =
                        sn.IndexOf("Universal Render Pipeline/Particles", StringComparison.OrdinalIgnoreCase) >= 0
                        || sn.IndexOf("Shader Graphs/", StringComparison.OrdinalIgnoreCase) >= 0; // Hovl SG often OK

                    // FORCE remap: any Built-in particle, error shader, MagentaFix, Standard, or
                    // non-URP shader on a ParticleSystemRenderer (except known-good Hovl SG).
                    bool needsHeal = NeedsMagentaHeal(sn, isParticle, alreadyUrpParticle);
                    if (!needsHeal) continue;

                    bool additive = sn.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0
                                    || sn.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0
                                    || sn.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase) < 0;

                    Texture tex = null;
                    if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex == null) tex = m.mainTexture;

                    Color col = Color.white;
                    if (m.HasProperty("_BaseColor")) col = m.GetColor("_BaseColor");
                    else if (m.HasProperty("_Color")) col = m.GetColor("_Color");
                    else if (m.HasProperty("_TintColor")) col = m.GetColor("_TintColor");

                    mats[i] = BuildUrpParticleMaterial(urpParticles, tex, col, additive, defaultParticleTex);
                    mats[i].name = (m.name ?? "Particle") + " (URP heal)";
                    changed = true;
                    fixedCount++;
                }
                if (changed) r.materials = mats;
            }
            return fixedCount;
        }

        private static bool NeedsMagentaHeal(string shaderName, bool isParticleRenderer, bool alreadyUrpParticle)
        {
            if (string.IsNullOrEmpty(shaderName)) return true;
            if (shaderName.Contains("InternalErrorShader")) return true;
            if (shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal)) return true;
            if (shaderName.IndexOf("MagentaFix", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (shaderName.Equals("Standard", StringComparison.Ordinal)) return true;
            // Built-in particle family (Spells Pack default)
            if (shaderName.StartsWith("Particles/", StringComparison.Ordinal) &&
                shaderName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0)
                return true;
            // Mobile/Particles, Nature/SpeedTree, etc. on particle renderers
            if (isParticleRenderer && !alreadyUrpParticle)
            {
                // Keep Hovl Shader Graph materials — they already work in URP (Blue capture).
                if (shaderName.IndexOf("Shader Graphs/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                if (shaderName.IndexOf("HS_", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                // Anything else on a particle renderer that isn't URP Particles → heal
                if (shaderName.IndexOf("Universal Render Pipeline/Particles", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
            }
            return false;
        }

        /// <summary>Build instance URP particle mat with additive or alpha blend (proven recipe).</summary>
        private static Material BuildUrpParticleMaterial(
            Shader urpParticles, Texture tex, Color tint, bool additive, Texture2D fallbackTex)
        {
            var neu = new Material(urpParticles);
            // Transparent surface
            if (neu.HasProperty("_Surface")) neu.SetFloat("_Surface", 1f);
            // URP BaseShaderGUI: 0 Alpha, 1 Premultiply, 2 Additive, 3 Multiply
            if (neu.HasProperty("_Blend")) neu.SetFloat("_Blend", additive ? 2f : 0f);
            if (neu.HasProperty("_SrcBlend"))
                neu.SetFloat("_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
            if (neu.HasProperty("_DstBlend"))
                neu.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (neu.HasProperty("_ZWrite")) neu.SetFloat("_ZWrite", 0f);
            neu.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive)
            {
                neu.EnableKeyword("_ALPHAPREMULTIPLY_ON"); // some URP versions use this with blend=2
                neu.DisableKeyword("_ALPHAMODULATE_ON");
            }
            else
            {
                neu.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                neu.DisableKeyword("_ALPHAMODULATE_ON");
            }
            neu.SetOverrideTag("RenderType", "Transparent");
            neu.renderQueue = (int)RenderQueue.Transparent;

            Texture useTex = tex != null ? tex : fallbackTex;
            if (useTex != null)
            {
                if (neu.HasProperty("_BaseMap")) neu.SetTexture("_BaseMap", useTex);
                neu.mainTexture = useTex;
            }
            if (neu.HasProperty("_BaseColor")) neu.SetColor("_BaseColor", tint);
            else neu.color = tint;
            return neu;
        }

        private void AuditShaders(GameObject go)
        {
            var lines = new List<string>();
            _brokenShaderCount = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                    {
                        lines.Add($"[BROKEN] {r.name}: <missing material>");
                        _brokenShaderCount++;
                        continue;
                    }
                    string shader = m.shader != null ? m.shader.name : "<null>";
                    bool broken =
                        m.shader == null ||
                        shader.Contains("InternalErrorShader") ||
                        (shader.StartsWith("Particles/", StringComparison.Ordinal) &&
                         shader.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0);
                    if (broken) _brokenShaderCount++;
                    lines.Add(broken
                        ? $"[BROKEN] {r.name}: {m.name} -> {shader}"
                        : $"{r.name}: {m.name} -> {shader}");
                }
            }
            lines.Sort((a, b) =>
                b.StartsWith("[BROKEN]", StringComparison.Ordinal)
                    .CompareTo(a.StartsWith("[BROKEN]", StringComparison.Ordinal)));
            _shaderAudit = lines.ToArray();
        }

        // =====================================================================
        //  GUI
        // =====================================================================

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawPreviewPane();
                    DrawInfo();
                }
            }
        }

        private void DrawLibrary()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(340f), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"VFX Library ({_filtered.Count}/{_library.Count})",
                        EditorStyles.boldLabel);
                    if (GUILayout.Button("Rescan", GUILayout.Width(60f))) ScanLibrary();
                }
                EditorGUILayout.LabelField(
                    "All VFX pack folders · stage = real URP scene camera (like D:/flames).",
                    EditorStyles.miniLabel);

                string s = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (s != _search) { _search = s; RebuildFiltered(); }

                bool cat = EditorGUILayout.ToggleLeft("Catalogued only", _cataloguedOnly);
                if (cat != _cataloguedOnly)
                {
                    _cataloguedOnly = cat;
                    EditorPrefs.SetBool("VfxCaster.CataloguedOnly", cat);
                    RebuildFiltered();
                }
                bool req = EditorGUILayout.ToggleLeft("Only prefabs with ParticleSystem", _requireParticleSystem);
                if (req != _requireParticleSystem)
                {
                    _requireParticleSystem = req;
                    EditorPrefs.SetBool("VfxCaster.RequirePS", req);
                    ScanLibrary();
                }

                EditorGUILayout.LabelField("Packs", EditorStyles.miniBoldLabel);
                int col = 0;
                EditorGUILayout.BeginHorizontal();
                foreach (var pack in _packNames)
                {
                    if (col > 0 && col % 3 == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }
                    bool on = !_packEnabled.TryGetValue(pack, out bool e) || e;
                    bool next = GUILayout.Toggle(on, pack, EditorStyles.miniButton);
                    if (next != on) { _packEnabled[pack] = next; RebuildFiltered(); }
                    col++;
                }
                EditorGUILayout.EndHorizontal();

                // Carousel
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Carousel", EditorStyles.boldLabel);
                int total = _filtered.Count;
                EditorGUILayout.LabelField(
                    total == 0 ? "No matches" : $"[{_carouselIndex + 1} / {total}]",
                    EditorStyles.largeLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(total == 0))
                    {
                        if (GUILayout.Button("◀ Prev", GUILayout.Height(28f))) CarouselPrev();
                        if (GUILayout.Button("Play this", GUILayout.Height(28f)) && total > 0)
                            SelectEntry(_filtered[Mathf.Clamp(_carouselIndex, 0, total - 1)]);
                        if (GUILayout.Button("Next ▶", GUILayout.Height(28f))) CarouselNext();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool auto = GUILayout.Toggle(_autoAdvance, "Auto-advance", GUILayout.Width(100f));
                    if (auto != _autoAdvance)
                    {
                        _autoAdvance = auto;
                        EditorPrefs.SetBool("VfxCaster.AutoAdvance", auto);
                        _autoLastAdvance = EditorApplication.timeSinceStartup;
                    }
                    float iv = EditorGUILayout.Slider(_autoInterval, 2f, 30f);
                    if (!Mathf.Approximately(iv, _autoInterval))
                    {
                        _autoInterval = iv;
                        EditorPrefs.SetFloat("VfxCaster.AutoInterval", iv);
                    }
                }

                if (total > 0)
                {
                    var cur = _filtered[Mathf.Clamp(_carouselIndex, 0, total - 1)];
                    EditorGUILayout.LabelField(cur.Label, EditorStyles.wordWrappedMiniLabel);
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(cur.Path);
                    if (p != null)
                    {
                        var thumb = AssetPreview.GetAssetPreview(p) ?? AssetPreview.GetMiniThumbnail(p);
                        if (thumb != null) GUILayout.Label(thumb, GUILayout.Width(96), GUILayout.Height(96));
                    }
                }

                EditorGUILayout.LabelField("All matches", EditorStyles.miniBoldLabel);
                using (var scroll = new EditorGUILayout.ScrollViewScope(_libScroll, GUILayout.ExpandHeight(true)))
                {
                    _libScroll = scroll.scrollPosition;
                    for (int i = 0; i < _filtered.Count; i++)
                    {
                        var entry = _filtered[i];
                        bool sel = i == _carouselIndex || ReferenceEquals(entry, _selected);
                        if (GUILayout.Button(entry.Label, sel ? EditorStyles.boldLabel : EditorStyles.label))
                        {
                            _carouselIndex = i;
                            SelectEntry(entry);
                            _autoLastAdvance = EditorApplication.timeSinceStartup;
                        }
                    }
                }
            }
        }

        private void DrawPreviewPane()
        {
            Rect rect = GUILayoutUtility.GetRect(320f, 300f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(260f));
            var e = Event.current;
            if (e.type == EventType.MouseDrag && e.button == 0 && rect.Contains(e.mousePosition))
            {
                _orbitYaw += e.delta.x * 0.7f;
                e.Use();
                Repaint();
            }

            // Void chrome behind
            Color voidBg = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f, 1f)
                : new Color(0.76f, 0.76f, 0.76f, 1f);
            EditorGUI.DrawRect(rect, voidBg);

            if (_selected == null)
            {
                EditorGUI.HelpBox(rect, "Pick an effect — it spawns on __VFX_Caster_Stage__ and renders here via URP.", MessageType.Info);
            }
            else if (e.type == EventType.Repaint && _instance != null && _stageCam != null)
            {
                RenderPreviewToRt(rect);
                if (_rt != null)
                    GUI.DrawTexture(rect, _rt, ScaleMode.StretchToFill, false);
            }
            else if (_instance == null)
            {
                EditorGUI.HelpBox(rect, "Failed to spawn prefab — check Console.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_instance == null))
                {
                    if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60f)))
                    {
                        _playing = !_playing;
                        if (_playing)
                        {
                            _lastTick = EditorApplication.timeSinceStartup;
                            foreach (var ps in _allSystems)
                                if (ps != null && !ps.isPlaying) ps.Play(false);
                        }
                    }
                    if (GUILayout.Button("Restart", GUILayout.Width(70f)))
                        DemoPlayFromStart();
                    _loop = GUILayout.Toggle(_loop, "Loop", GUILayout.Width(50f));
                    if (GUILayout.Button(new GUIContent("Heal Magenta",
                            "Re-run URP particle mat remap on this instance (Built-in Particles → URP)."),
                        GUILayout.Width(100f)))
                    {
                        if (_instance != null)
                        {
                            _fixedMats = RemapBrokenParticleMaterials(_instance);
                            _hiddenMeshes = HideNonParticleMeshHelpers(_instance);
                            AuditShaders(_instance);
                            DemoPlayFromStart();
                            Debug.Log(Log + $"Heal Magenta: fixed={_fixedMats} hidMeshes={_hiddenMeshes}");
                        }
                    }
                    if (GUILayout.Button("Frame Scene", GUILayout.Width(90f)))
                    {
                        if (_instance != null)
                        {
                            Selection.activeGameObject = _instance;
                            if (SceneView.lastActiveSceneView != null)
                                SceneView.lastActiveSceneView.FrameSelected();
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    float scrub = GUILayout.HorizontalSlider(Mathf.Clamp(_time, 0f, _duration), 0f, Mathf.Max(0.1f, _duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _time = scrub;
                        _playing = false;
                        ResimulateAbsolute(_time);
                    }
                    GUILayout.Label($"{_time:0.00}s", GUILayout.Width(48f));
                }
            }

            _camDistance = EditorGUILayout.Slider("Distance", _camDistance, 1.5f, 40f);
            _camHeight = EditorGUILayout.Slider("Focus height", _camHeight, 0f, 5f);
            EditorGUILayout.LabelField(
                "Stage object: __VFX_Caster_Stage__  ·  grey boxes hidden  ·  Built-in particle mats remapped to URP on instance",
                EditorStyles.miniLabel);
        }

        private void DrawInfo()
        {
            if (_selected == null) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Effect", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    _selected.Catalogued ? $"Key: {_selected.Key}" : "Key: (uncatalogued)",
                    EditorStyles.miniLabel);
                if (_selected.Catalogued && GUILayout.Button("Copy Key", GUILayout.Width(70f)))
                    EditorGUIUtility.systemCopyBuffer = _selected.Key;
                if (GUILayout.Button("Ping Prefab", GUILayout.Width(80f)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(_selected.Path));
            }
            EditorGUILayout.LabelField(_selected.Path, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Pack: {_selected.Pack}   Layers: {_psCount}   " +
                $"URP mat fixes: {_fixedMats}   Hidden mesh helpers: {_hiddenMeshes}   " +
                (_looping ? "LOOPING" : "one-shot"),
                EditorStyles.miniLabel);

            if (_layerNames.Length > 0)
            {
                var sb = new StringBuilder("Systems: ");
                for (int i = 0; i < _layerNames.Length; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    sb.Append(_layerNames[i]);
                    if (i >= 12) { sb.Append(" …"); break; }
                }
                EditorGUILayout.LabelField(sb.ToString(), EditorStyles.wordWrappedMiniLabel);
            }

            DrawTagAndCatalog();

            EditorGUILayout.LabelField(
                _brokenShaderCount > 0
                    ? $"Shader audit — {_brokenShaderCount} still BROKEN after preview remap:"
                    : "Shader audit — OK (preview instance):",
                EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_infoScroll, GUILayout.MinHeight(60f)))
            {
                _infoScroll = scroll.scrollPosition;
                foreach (var line in _shaderAudit)
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
        }

        // Re-derive the loop flag whenever the selection changes. Hooked here (and in
        // TagSelected) rather than at each `_selected = ...` site so no future assignment
        // path can leave a stale flag behind.
        //
        // Derived from the PREFAB ASSET on disk, never from _allSystems: the stage instance
        // is mutated by DemoPlayFromStart (`if (_loop) main.loop = true;`) for preview
        // scrubbing, so reading the instance would report every effect as a loop -- the
        // exact untruth this fix removes.
        private void EnsureTagLoopDerived()
        {
            string path = _selected != null ? _selected.Path : null;
            if (string.Equals(path, _tagLoopPath, StringComparison.Ordinal)) return;
            _tagLoopPath = path;
            _tagLoop = false;
            _tagLoopDetail = "no selection";
            if (string.IsNullOrEmpty(path)) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool derived;
            string detail;
            if (!Regression.VfxLoopFlagRegression.TryDerive(prefab, out derived, out detail))
            {
                // Undeterminable (no prefab / no ParticleSystem) tags as ONESHOT: a oneshot
                // is reclaimed on a deadline, so a wrong guess costs one pool slot for a few
                // seconds. A wrong LOOP guess costs it for the whole session.
                _tagLoopDetail = "undeterminable (" + detail + ") -- tagging as one-shot";
                return;
            }
            _tagLoop = derived;
            _tagLoopDetail = detail;
        }

        private void DrawTagAndCatalog()
        {
            EnsureTagLoopDerived();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Tag & Catalog (manual = canon)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Spell / base name", GUILayout.Width(110f));
                _tagBaseName = EditorGUILayout.TextField(_tagBaseName);
                // READ-ONLY. Shown because the value matters to whoever is tagging, disabled
                // because the prefab decides it. See the _tagLoop field comment for the leak.
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Toggle(_tagLoop, "Loop", GUILayout.Width(50f));
            }
            EditorGUILayout.LabelField(
                "Loop is DERIVED from the prefab emission (read-only): " + _tagLoopDetail,
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool ok = !string.IsNullOrEmpty(_tagBaseName?.Trim());
                using (new EditorGUI.DisabledScope(!ok))
                {
                    foreach (var role in TagRoles)
                        if (GUILayout.Button(role)) TagSelected(role);
                }
            }
            EditorGUILayout.LabelField(
                "Then: Defenders > VFX > Generate Hovl VFX Catalog to make tags bindable.",
                EditorStyles.miniLabel);
        }

        private void TagSelected(string role)
        {
            string baseName = _tagBaseName.Trim().Replace(" ", "");
            // The role-based force-set that used to live here (Projectile/Aura => loop) is
            // DELETED: it manufactured most of the 95 bad IsLoop:1 rows. A role is a naming
            // convention, not evidence about emission -- PP_MuzzleFlash is a "Projectile"
            // key and a rate-0 burst. Re-derive instead, so the row written is the truth
            // even if the window has been open across a prefab re-import.
            _tagLoopPath = null;
            EnsureTagLoopDerived();
            string key = $"{baseName}_{role}";
            var row = new HovlVfxCatalogGenerator.ManualPickRow
            {
                key = key,
                prefabPath = _selected.Path,
                isLoop = _tagLoop,
                scale = 1f,
                manual = true,
            };
            if (HovlVfxCatalogGenerator.WriteManualPick(row))
            {
                Debug.Log(Log + $"tagged -> {key}");
                RefreshOverlayLookup();
            }
        }
    }
}
