// =============================================================================
// VfxCasterWindow — standalone VFX browsing/audition booth (owner ask 2026-07-11:
// "a stand alone tool just for the vfx, i need to see everything similar to
// [Motion Caster] to really dig into them").
//
// Defenders > Animation > VFX Caster
//
// Library (left): EVERY effect —
//   • the HovlVfxCatalog rows (the keys gameplay can fire via VFXManager.PlayKey),
//   • PLUS every prefab under Assets/Hovl Studio that is NOT catalogued yet,
//     flagged "[uncatalogued]" by TEXT (owner is red/green colorblind — never
//     hue-only cues). Uncatalogued effects can be previewed but have no key to
//     bind until they're added to the catalog (Defenders > VFX > Regenerate).
//
// Preview (right): PreviewRenderUtility stage — Play / Pause / Restart / Loop /
// scrub, orbit-drag + distance slider. Particles are Simulate()d to the scrub
// time (deterministic; the stage never ticks them itself).
//
// Dig-in panel: prefab path, particle-system count, estimated duration,
// looping flag, and a SHADER AUDIT — each material's shader listed, with
// "[BROKEN]" text flags for Hidden/InternalErrorShader + non-URP legacy
// particle shaders (the F8-49 "magenta at source" class), so a bad effect
// names itself before it ever ships.
//
// Cross-assembly rule: DeNelle.Editor does NOT reference DeNelle.Village —
// the catalog is read via SerializedObject exactly like MotionCasterWindow.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Standalone VFX audition window: browse every catalogued key +
    /// every raw Hovl prefab, preview with scrub/loop/orbit, and audit shaders.</summary>
    public sealed class VfxCasterWindow : EditorWindow
    {
        private const string HovlCatalogAssetPath = "Assets/Resources/VFX/HovlVfxCatalog.asset";
        private const string HovlPackRoot = "Assets/Hovl Studio";
        private const string Log = "[VfxCaster] ";

        private sealed class VfxEntry
        {
            public string Key;         // catalog key ("" = uncatalogued)
            public string Path;        // prefab asset path
            public string Label;       // list display
            public bool Catalogued;
        }

        // ── Library ──────────────────────────────────────────────────────────
        private List<VfxEntry> _library = new List<VfxEntry>();
        private VfxEntry _selected;
        private string _search = string.Empty;
        // Loaded in OnEnable — EditorPrefs is forbidden in field initializers
        // (ScriptableObject-constructor UnityException, same class as the
        // MotionCasterWindow capture 2026-07-11).
        private bool _cataloguedOnly;
        private Vector2 _libScroll;

        // ── Preview stage ────────────────────────────────────────────────────
        private PreviewRenderUtility _preview;
        private GameObject _instance;
        private ParticleSystem[] _roots = Array.Empty<ParticleSystem>();
        private float _time;
        private bool _playing;
        private bool _loop = true;
        private float _duration = 3f;      // estimated effect length (scrub range)
        private bool _looping;             // any root system loops
        private float _orbitYaw = 35f;
        private float _camDistance = 6f;
        private float _camHeight = 1.2f;
        private double _lastTick;

        // ── Dig-in info ──────────────────────────────────────────────────────
        private string[] _shaderAudit = Array.Empty<string>();
        private int _brokenShaderCount;
        private int _psCount;
        private Vector2 _infoScroll;

        // ── Tag & Catalog (owner ask 2026-07-12: "tag as spell hit projectile") ─
        // Writes the manual picks overlay (Assets/Editor/VfxManualPicks.json) that
        // Defenders > VFX > Regenerate merges AFTER the built-in picks — manual is
        // CANON and wins on key collision. Roles = the catalog triplet convention:
        // Cast / Projectile / Impact ("spell hit" = Impact), plus Aura for loops.
        private static readonly string[] TagRoles = { "Cast", "Projectile", "Impact", "Aura" };
        private string _tagBaseName = string.Empty;
        private bool _tagLoop;
        // path -> overlay keys, rebuilt on scan + after each tag ("pending regenerate").
        private Dictionary<string, List<string>> _overlayKeysByPath =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Defenders/Animation/VFX Caster")]
        public static void Open()
        {
            var w = GetWindow<VfxCasterWindow>("VFX Caster");
            w.minSize = new Vector2(860f, 520f);
        }

        private void OnEnable()
        {
            _cataloguedOnly = EditorPrefs.GetBool("VfxCaster.CataloguedOnly", false);
            ScanLibrary();
            _lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorTick;
            DestroyInstance();
            if (_preview != null) { _preview.Cleanup(); _preview = null; }
        }

        private void OnEditorTick()
        {
            if (!_playing) { _lastTick = EditorApplication.timeSinceStartup; return; }
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - _lastTick), 0f, 0.25f);
            _lastTick = now;
            _time += dt;
            if (_time > _duration)
            {
                if (_loop) _time = 0f;
                else { _time = _duration; _playing = false; }
            }
            SampleVfx();
            Repaint();
        }

        // ── Library scan ─────────────────────────────────────────────────────

        private void ScanLibrary()
        {
            _library = new List<VfxEntry>();
            _selected = null;

            // 1. Catalogued keys (SerializedObject read — no DeNelle.Village reference).
            var cataloguedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var asset = AssetDatabase.LoadMainAssetAtPath(HovlCatalogAssetPath);
            if (asset != null)
            {
                var so = new SerializedObject(asset);
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
                        _library.Add(new VfxEntry
                        {
                            Key = key,
                            Path = path,
                            Catalogued = true,
                            Label = $"{key}  [catalogued]",
                        });
                    }
                }
            }
            else
            {
                Debug.LogWarning(Log + $"HovlVfxCatalog not found at '{HovlCatalogAssetPath}' — " +
                    "only raw pack prefabs will list (no bindable keys).");
            }

            // 2. Manual overlay rows (tagged here, catalogued on next Regenerate) —
            //    path -> keys lookup for the dig-in panel + the list flags below.
            RefreshOverlayLookup();

            // 3. Every raw Hovl pack prefab not already catalogued. An overlay tag
            //    that hasn't been regenerated yet reads "[tagged - regenerate]" by
            //    TEXT (owner colorblind — never hue-only).
            if (AssetDatabase.IsValidFolder(HovlPackRoot))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { HovlPackRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (cataloguedPaths.Contains(path)) continue;
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    bool tagged = _overlayKeysByPath.ContainsKey(path);
                    _library.Add(new VfxEntry
                    {
                        Key = string.Empty,
                        Path = path,
                        Catalogued = false,
                        Label = tagged ? $"{name}  [tagged - regenerate]" : $"{name}  [uncatalogued]",
                    });
                }
            }
            else
            {
                Debug.LogWarning(Log + $"'{HovlPackRoot}' not found — no raw pack prefabs listed.");
            }

            _library.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
        }

        /// <summary>Rebuild the prefab-path -> overlay-keys lookup from the manual
        /// picks JSON (guarded read — a bad file warns and yields empty).</summary>
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

        // ── Selection / preview instance ─────────────────────────────────────

        private void SelectEntry(VfxEntry entry)
        {
            _selected = entry;
            _time = 0f;
            _playing = true;   // audition immediately — that's what the booth is for
            RebuildInstance();
        }

        private void RebuildInstance()
        {
            DestroyInstance();
            if (_selected == null) return;

            if (_preview == null)
            {
                _preview = new PreviewRenderUtility();
                _preview.camera.fieldOfView = 30f;
                _preview.camera.nearClipPlane = 0.05f;
                _preview.camera.farClipPlane = 200f;
                _preview.lights[0].intensity = 1.2f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                if (_preview.lights.Length > 1) _preview.lights[1].intensity = 0.6f;
                _preview.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_selected.Path);
            if (prefab == null)
            {
                Debug.LogWarning(Log + $"prefab missing at '{_selected.Path}' — rescan the library.");
                return;
            }
            _instance = Instantiate(prefab);
            _instance.hideFlags = HideFlags.HideAndDontSave;
            _instance.transform.position = Vector3.zero;
            _instance.transform.rotation = Quaternion.identity;
            _preview.AddSingleGO(_instance);

            // Top-level systems only — Simulate(withChildren:true) covers subs.
            var all = _instance.GetComponentsInChildren<ParticleSystem>(true);
            var roots = new List<ParticleSystem>();
            foreach (var ps in all)
            {
                var parent = ps.transform.parent;
                if (parent == null || parent.GetComponentInParent<ParticleSystem>(true) == null)
                    roots.Add(ps);
            }
            _roots = roots.ToArray();
            _psCount = all.Length;

            // Estimated duration + looping flag → scrub range.
            _duration = 1f;
            _looping = false;
            foreach (var ps in all)
            {
                var main = ps.main;
                if (main.loop) _looping = true;
                float end = main.duration + main.startLifetime.constantMax + main.startDelay.constantMax;
                if (end > _duration) _duration = end;
            }
            _duration = Mathf.Clamp(_duration, 1f, 30f);

            AuditShaders(_instance);
            SampleVfx();
        }

        private void DestroyInstance()
        {
            if (_instance != null) DestroyImmediate(_instance);
            _instance = null;
            _roots = Array.Empty<ParticleSystem>();
            _shaderAudit = Array.Empty<string>();
            _brokenShaderCount = 0;
            _psCount = 0;
        }

        private void SampleVfx()
        {
            foreach (var ps in _roots)
            {
                if (ps == null) continue;
                ps.Simulate(_time, true, true);
            }
        }

        /// <summary>List every renderer material's shader; TEXT-flag the broken
        /// classes (F8-49): InternalErrorShader (magenta), missing materials, and
        /// non-URP legacy particle shaders that URP renders magenta.</summary>
        private void AuditShaders(GameObject go)
        {
            var lines = new List<string>();
            _brokenShaderCount = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                    {
                        lines.Add($"[BROKEN] {r.name}: <missing material>");
                        _brokenShaderCount++;
                        continue;
                    }
                    string shader = m.shader != null ? m.shader.name : "<null shader>";
                    bool broken =
                        m.shader == null ||
                        shader.Contains("InternalErrorShader") ||
                        shader.StartsWith("Legacy Shaders/", StringComparison.Ordinal) ||
                        (shader.StartsWith("Particles/", StringComparison.Ordinal) &&
                         !shader.Contains("Universal"));
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

        // ── GUI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibraryColumn();
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawPreview();
                    DrawInfo();
                }
            }
        }

        private void DrawLibraryColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(320f), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"VFX Library ({_library.Count})", EditorStyles.boldLabel);
                    if (GUILayout.Button("Rescan", GUILayout.Width(60f))) ScanLibrary();
                }
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                bool catOnly = EditorGUILayout.ToggleLeft(
                    new GUIContent("Catalogued only (bindable keys)",
                        "Hide raw pack prefabs that have no HovlVfxCatalog key yet."),
                    _cataloguedOnly);
                if (catOnly != _cataloguedOnly)
                {
                    _cataloguedOnly = catOnly;
                    EditorPrefs.SetBool("VfxCaster.CataloguedOnly", catOnly);
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(_libScroll, GUILayout.ExpandHeight(true)))
                {
                    _libScroll = scroll.scrollPosition;
                    foreach (var entry in _library)
                    {
                        if (_cataloguedOnly && !entry.Catalogued) continue;
                        if (!string.IsNullOrEmpty(_search) &&
                            entry.Label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                            entry.Path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        bool isSelected = ReferenceEquals(entry, _selected);
                        if (GUILayout.Button(entry.Label,
                                isSelected ? EditorStyles.boldLabel : EditorStyles.label))
                            SelectEntry(entry);
                    }
                }
            }
        }

        private void DrawPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(256f, 300f, GUILayout.ExpandWidth(true));

            // Orbit drag inside the stage rect.
            var e = Event.current;
            if (e.type == EventType.MouseDrag && e.button == 0 && rect.Contains(e.mousePosition))
            {
                _orbitYaw += e.delta.x * 0.7f;
                e.Use();
                Repaint();
            }

            if (_selected == null)
            {
                EditorGUI.HelpBox(rect, "Pick an effect from the library to audition it.", MessageType.Info);
            }
            else if (e.type == EventType.Repaint && _preview != null && _instance != null)
            {
                var camRot = Quaternion.Euler(15f, _orbitYaw, 0f);
                var focus = new Vector3(0f, _camHeight, 0f);
                _preview.camera.transform.position = focus + camRot * (Vector3.back * _camDistance);
                _preview.camera.transform.rotation = camRot;
                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.Render(true);
                GUI.DrawTexture(rect, _preview.EndPreview(), ScaleMode.StretchToFill, false);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_instance == null))
                {
                    if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60f)))
                    {
                        _playing = !_playing;
                        if (_playing && _time >= _duration) _time = 0f;
                    }
                    if (GUILayout.Button("Restart", GUILayout.Width(60f)))
                    {
                        _time = 0f;
                        _playing = true;
                        SampleVfx();
                    }
                    _loop = GUILayout.Toggle(_loop, "Loop", GUILayout.Width(50f));

                    EditorGUI.BeginChangeCheck();
                    _time = GUILayout.HorizontalSlider(_time, 0f, _duration);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _playing = false;
                        SampleVfx();
                    }
                    GUILayout.Label($"{_time:0.00}s / {_duration:0.0}s", GUILayout.Width(90f));
                }
            }
            _camDistance = EditorGUILayout.Slider("Distance", _camDistance, 1f, 30f);
            _camHeight = EditorGUILayout.Slider("Focus height", _camHeight, 0f, 4f);
        }

        private void DrawInfo()
        {
            if (_selected == null) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Effect", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    _selected.Catalogued ? $"Key: {_selected.Key}" : "Key: (uncatalogued — not bindable yet)",
                    EditorStyles.miniLabel);
                if (_selected.Catalogued && GUILayout.Button(
                        new GUIContent("Copy Key", "Copy the key for a Motion Caster VFX Key binding."),
                        GUILayout.Width(70f)))
                    EditorGUIUtility.systemCopyBuffer = _selected.Key;
                if (GUILayout.Button("Ping Prefab", GUILayout.Width(80f)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(_selected.Path));
            }
            EditorGUILayout.LabelField(_selected.Path, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Particle systems: {_psCount}   Est. length: {_duration:0.0}s" +
                (_looping ? "   LOOPING effect" : ""),
                EditorStyles.miniLabel);

            DrawTagAndCatalog();

            EditorGUILayout.LabelField(
                _brokenShaderCount > 0
                    ? $"Shader audit — {_brokenShaderCount} BROKEN (renders magenta in URP):"
                    : "Shader audit — all materials OK:",
                EditorStyles.boldLabel);
            DrawShaderAuditScroll();
        }

        /// <summary>Tag &amp; Catalog block: bind the selected prefab to a
        /// &lt;BaseName&gt;_&lt;Role&gt; catalog key via the manual picks overlay.
        /// State reads by TEXT (existing keys listed, "pending regenerate" flag) —
        /// never hue alone.</summary>
        private void DrawTagAndCatalog()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Tag & Catalog (manual = canon)", EditorStyles.boldLabel);

            // Existing keys for THIS prefab — catalog rows + overlay rows, as text.
            foreach (var e in _library)
            {
                if (e.Catalogued &&
                    string.Equals(e.Path, _selected.Path, StringComparison.OrdinalIgnoreCase))
                    EditorGUILayout.LabelField($"In catalog: {e.Key}", EditorStyles.miniLabel);
            }
            if (_overlayKeysByPath.TryGetValue(_selected.Path, out var overlayKeys))
            {
                foreach (var k in overlayKeys)
                    EditorGUILayout.LabelField($"Tagged (pending regenerate): {k}", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Spell / base name", GUILayout.Width(110f));
                _tagBaseName = EditorGUILayout.TextField(_tagBaseName);
                _tagLoop = GUILayout.Toggle(_tagLoop,
                    new GUIContent("Loop", "IsLoop for the row. Projectile/Aura set it ON when pressed " +
                        "(projectiles fly until impact); still editable before pressing a role."),
                    GUILayout.Width(50f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool nameOk = !string.IsNullOrEmpty(_tagBaseName?.Trim());
                using (new EditorGUI.DisabledScope(!nameOk))
                {
                    foreach (var role in TagRoles)
                    {
                        if (GUILayout.Button(role))
                            TagSelected(role);
                    }
                }
                if (!nameOk)
                    EditorGUILayout.LabelField("(enter a base name)", EditorStyles.miniLabel, GUILayout.Width(120f));
            }
            EditorGUILayout.LabelField(
                "Key = <BaseName>_<Role>, e.g. Fireball_Impact = the spell hit. " +
                "Run Defenders > VFX > Generate Hovl VFX Catalog to make tags bindable.",
                EditorStyles.miniLabel);
        }

        /// <summary>Write/update the overlay row for the selected prefab as
        /// &lt;BaseName&gt;_&lt;role&gt;. Projectile/Aura force IsLoop on.</summary>
        private void TagSelected(string role)
        {
            string baseName = _tagBaseName.Trim().Replace(" ", string.Empty);
            if (role == "Projectile" || role == "Aura") _tagLoop = true;
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
                Debug.Log(Log + $"tagged '{System.IO.Path.GetFileNameWithoutExtension(_selected.Path)}' " +
                          $"-> key '{key}' (manual)");
                RefreshOverlayLookup();
                if (!_selected.Catalogued)
                    _selected.Label =
                        $"{System.IO.Path.GetFileNameWithoutExtension(_selected.Path)}  [tagged - regenerate]";
            }
            // WriteManualPick already warned on failure — no silent path.
        }

        private void DrawShaderAuditScroll()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_infoScroll, GUILayout.MinHeight(70f)))
            {
                _infoScroll = scroll.scrollPosition;
                foreach (var line in _shaderAudit)
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
        }
    }
}
