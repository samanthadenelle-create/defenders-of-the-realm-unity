// =============================================================================
// MotionCasterWindow — standalone clip-casting authoring tool (WO-670 slice 1
// + WO-671 lane A bundle fields).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Menu: Defenders → Animation → Motion Caster
// Canon: docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md (§2 closed vocabulary,
// §8 tool write contract, §9a adopted bundle fields) + WO-670 / WO-671.
//
// The owner loads any character model, the tool stands it up on a preview stage
// with EVERY motion clip we own (HeroPackages Extracted .anim / Action FBX packs
// / KayKit), the owner previews and picks, and each pick is tied to a KEYWORD
// (closed vocabulary — MotionCastings.Vocabulary) and saved per TARGET (enemy
// family | hero class — MotionCastings.Targets, data-driven) through
// MotionCastings.WriteRow (manual:true = CANON, dual-copy write).
//
// Bundle authoring (WO-671 §2): each row also carries vfxKey (HovlVfxCatalog
// key namespace), sfxId (SfxId enum names), vfxDelay, attachBone, playOneShot.
// The catalog keys are read via SerializedObject (DeNelle.Editor does not
// reference DeNelle.Village) and the SfxId names via a type lookup
// (DeNelle.Audio likewise unreferenced) — when either listing fails the field
// falls back to free text with a visible validation warning, never silently.
//
// Avatar verdict: PeopleCharacterImporter's verdict pass is welded to its
// per-pack model lists (SkeletonAvatarVerdict et al. take dst paths), so this
// window performs the same avatar.isValid/isHuman check inline and reuses the
// importer's exact verdict wording (OK Humanoid / WARN Generic / FAIL no map).
//
// Preview: PreviewRenderUtility stage; clips sampled via AnimationMode
// (humanoid retarget) with clip.SampleAnimation as the generic fallback.
// Un-retargeted FBX clips preview best-effort + a "needs extraction" warning
// naming the import menus (batch extract is WO-670 slice-2 out-of-scope).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Owner-in-the-loop authoring window for the Action Keyword Registry
    /// (motion-castings.json). Load model → preview clips → bind keyword +
    /// action bundle → Save (manual:true, canon, never auto-overwritten).
    /// </summary>
    public sealed class MotionCasterWindow : EditorWindow
    {
        // ── Clip sources (WO-670 §3) ──────────────────────────────────────────
        private const string HeroPackagesRoot = "Assets/HeroPackages";
        private const string ActionRoot       = "Assets/Action";
        private const string KayKitRoot       = "Assets/Models/KayKit";
        private const string HovlCatalogAssetPath = "Assets/Resources/VFX/HovlVfxCatalog.asset";

        private const string Log = "[MotionCaster] ";

        /// <summary>One previewable clip in the library list.</summary>
        private sealed class ClipEntry
        {
            public AnimationClip Clip;
            public string Path;            // containing asset path (.anim or .fbx)
            public string Source;          // "extracted" | "action" | "kaykit"
            public bool NeedsExtraction;   // FBX-borne clip — un-retargeted preview
            public string Category;        // vocabulary-category guess (chip filter)
            public string Label;           // list display
        }

        // ── Model / verdict ──────────────────────────────────────────────────
        private GameObject _model;
        private string _avatarVerdict;
        private MessageType _verdictType = MessageType.Info;

        // ── Preview stage ────────────────────────────────────────────────────
        private PreviewRenderUtility _preview;
        private GameObject _previewInstance;
        private float _orbitYaw = 35f;
        private bool _playing;
        private bool _loop = true;
        private float _time;
        private double _lastTick;

        // ── Library ──────────────────────────────────────────────────────────
        private List<ClipEntry> _library = new List<ClipEntry>();
        private ClipEntry _selected;
        private string _search = string.Empty;
        private int _chipIndex;              // 0 = All
        private string[] _chips = { "All" };
        private Vector2 _libScroll;
        private Vector2 _mainScroll;

        // ── Binding row (target × keyword → bundle) ──────────────────────────
        private string[] _targets = Array.Empty<string>();
        private int _targetIndex;
        private string _targetFree = string.Empty;   // fallback when registry has no targets
        private string[] _keywords = Array.Empty<string>();
        private string[] _keywordLabels = Array.Empty<string>();
        private int _keywordIndex;
        private string[] _vfxKeys;                   // null = listing failed → free text
        private int _vfxKeyIndex;                    // 0 = (none)
        private string _vfxKeyFree = string.Empty;
        private string[] _sfxIds;                    // null = listing failed → free text
        private int _sfxIdIndex;                     // 0 = None
        private string _sfxIdFree = string.Empty;
        private float _vfxDelay;
        private string _attachBone = string.Empty;
        private bool _playOneShot;

        [MenuItem("Defenders/Animation/Motion Caster")]
        public static void Open()
        {
            var win = GetWindow<MotionCasterWindow>("Motion Caster");
            win.minSize = new Vector2(760f, 480f);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            ScanLibrary();
            LoadPickerSources();
            _lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorTick;
            TearDownPreview();
        }

        private void TearDownPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }
            if (_preview != null)
            {
                _preview.Cleanup();
                _preview = null;
            }
        }

        private void OnEditorTick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTick);
            _lastTick = now;
            if (!_playing || _selected?.Clip == null) return;

            _time += dt;
            float len = Mathf.Max(_selected.Clip.length, 0.001f);
            if (_time > len)
            {
                if (_loop) _time %= len;
                else { _time = len; _playing = false; }
            }
            SamplePose();
            Repaint();
        }

        // ── Library scan (WO-670 §3) ─────────────────────────────────────────

        private void ScanLibrary()
        {
            _library = new List<ClipEntry>();
            _selected = null;

            // 1. Retargeted, ready: HeroPackages/*/Animations/Extracted/*.anim
            if (AssetDatabase.IsValidFolder(HeroPackagesRoot))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { HeroPackagesRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                        !path.Contains("/Animations/Extracted/")) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip != null) AddEntry(clip, path, "extracted", needsExtraction: false);
                }
            }
            else
            {
                Debug.LogWarning(Log + $"'{HeroPackagesRoot}' not found — no extracted hero clips listed.");
            }

            // 2. Raw packs: Action FBX clips (Mixamo + ActorCore — un-retargeted).
            ScanFbxClips(ActionRoot, "action", warnWhenAbsent: true);

            // 3. KayKit library — gitignored; warn-not-error when absent (WO-670 §4).
            if (AssetDatabase.IsValidFolder(KayKitRoot))
            {
                ScanFbxClips(KayKitRoot, "kaykit", warnWhenAbsent: false);
                ScanAnimClips(KayKitRoot, "kaykit");
            }
            else
            {
                Debug.LogWarning(Log + $"KayKit library absent at '{KayKitRoot}' (gitignored pack) — " +
                    "skipping; tool stays open (WO-670 acceptance).");
            }

            _library.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
        }

        private void ScanFbxClips(string root, string source, bool warnWhenAbsent)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                if (warnWhenAbsent)
                    Debug.LogWarning(Log + $"'{root}' not found — no {source} clips listed.");
                return;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip &&
                        !clip.name.StartsWith("__preview", StringComparison.Ordinal))
                        AddEntry(clip, path, source, needsExtraction: true);
            }
        }

        private void ScanAnimClips(string root, string source)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) AddEntry(clip, path, source, needsExtraction: false);
            }
        }

        private void AddEntry(AnimationClip clip, string path, string source, bool needsExtraction)
        {
            _library.Add(new ClipEntry
            {
                Clip = clip,
                Path = path,
                Source = source,
                NeedsExtraction = needsExtraction,
                Category = GuessCategory(clip.name),
                Label = $"{clip.name}  [{source}]",
            });
        }

        /// <summary>Vocabulary-category guess from the clip name (chip filter only —
        /// never written to data; the owner's keyword pick is the truth).</summary>
        private static string GuessCategory(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            if (Regex.IsMatch(n, @"death|dying|die\b|dead"))                          return "death";
            if (Regex.IsMatch(n, @"cast|spell|magic|channel|summon"))                 return "cast";
            if (Regex.IsMatch(n, @"atk|attack|slash|swing|combo|stab|punch|kick|shoot|melee")) return "attack";
            if (Regex.IsMatch(n, @"hit|impact|block|parry|dodge|knock|stun|react|getting"))    return "reaction";
            if (Regex.IsMatch(n, @"idle|walk|run|jog|sprint|strafe|turn|locomotion")) return "locomotion";
            return "signature";
        }

        // ── Picker sources (data-driven, canon §8) ───────────────────────────

        private void LoadPickerSources()
        {
            MotionCastings.Reload();

            // Targets — from the registry (enemies.json families + hero classes +
            // archetype roots are the seed; read from data, not hardcoded).
            var targets = new List<string>(MotionCastings.Targets);
            targets.Sort(StringComparer.Ordinal);
            _targets = targets.ToArray();
            _targetIndex = Mathf.Clamp(_targetIndex, 0, Mathf.Max(0, _targets.Length - 1));

            // Keywords — closed vocabulary, grouped by category for the popup +
            // the category names double as the library chips.
            var categories = new List<string>(MotionCastings.Categories);
            var keywords = new List<string>();
            var labels = new List<string>();
            foreach (string cat in categories)
                foreach (string kw in MotionCastings.CategoryKeywords(cat))
                {
                    keywords.Add(kw);
                    labels.Add(cat + "/" + kw);
                }
            if (keywords.Count == 0)
            {
                // Registry file absent/empty — the compile-time mirror is the same
                // closed set (one source, two views, canon §2).
                foreach (string kw in DeNelle.Core.Combat.ActionKeywords.All)
                {
                    keywords.Add(kw);
                    labels.Add(kw);
                }
                categories = new List<string>
                    { "locomotion", "attack", "cast", "reaction", "death", "signature" };
            }
            _keywords = keywords.ToArray();
            _keywordLabels = labels.ToArray();
            _keywordIndex = Mathf.Clamp(_keywordIndex, 0, Mathf.Max(0, _keywords.Length - 1));

            var chips = new List<string> { "All" };
            chips.AddRange(categories);
            _chips = chips.ToArray();
            _chipIndex = Mathf.Clamp(_chipIndex, 0, _chips.Length - 1);

            _vfxKeys = LoadHovlVfxKeys();
            _sfxIds = LoadSfxIdNames();
        }

        /// <summary>HovlVfxCatalog keys via SerializedObject (DeNelle.Editor does not
        /// reference DeNelle.Village — reads the asset's Rows[].Key directly).
        /// Null = listing failed → the window falls back to free text + warning.</summary>
        private static string[] LoadHovlVfxKeys()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(HovlCatalogAssetPath);
            if (asset == null)
            {
                Debug.LogWarning(Log + $"HovlVfxCatalog not found at '{HovlCatalogAssetPath}' — " +
                    "vfxKey falls back to free text (unvalidated).");
                return null;
            }
            var so = new SerializedObject(asset);
            var rows = so.FindProperty("Rows");
            if (rows == null || !rows.isArray)
            {
                Debug.LogWarning(Log + "HovlVfxCatalog has no readable 'Rows' array — " +
                    "vfxKey falls back to free text (unvalidated).");
                return null;
            }
            var keys = new List<string>();
            for (int i = 0; i < rows.arraySize; i++)
            {
                var key = rows.GetArrayElementAtIndex(i).FindPropertyRelative("Key");
                if (key != null && !string.IsNullOrEmpty(key.stringValue))
                    keys.Add(key.stringValue);
            }
            if (keys.Count == 0)
            {
                Debug.LogWarning(Log + "HovlVfxCatalog has zero keyed rows — " +
                    "vfxKey falls back to free text (unvalidated).");
                return null;
            }
            keys.Sort(StringComparer.Ordinal);
            keys.Insert(0, "(none)");
            return keys.ToArray();
        }

        /// <summary>SfxId enum names by type lookup (DeNelle.Editor does not reference
        /// DeNelle.Audio). Null = lookup failed → free text + warning.</summary>
        private static string[] LoadSfxIdNames()
        {
            var t = Type.GetType("DeNelle.Audio.SfxId, DeNelle.Audio");
            if (t == null)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("DeNelle.Audio.SfxId");
                    if (t != null) break;
                }
            if (t == null || !t.IsEnum)
            {
                Debug.LogWarning(Log + "SfxId enum type not found (DeNelle.Audio not loaded?) — " +
                    "sfxId falls back to free text (unvalidated).");
                return null;
            }
            return Enum.GetNames(t); // element 0 = None (sentinel — saved as empty)
        }

        // ── Model load + avatar verdict ──────────────────────────────────────

        private void SetModel(GameObject model)
        {
            _model = model;
            _avatarVerdict = AvatarVerdict(_model, out _verdictType);
            RebuildPreviewInstance();
        }

        /// <summary>Same avatar check + verdict wording as PeopleCharacterImporter's
        /// verdict pass (that pass is welded to its per-pack path lists, so the check
        /// is performed inline here rather than refactoring the importer).</summary>
        private static string AvatarVerdict(GameObject model, out MessageType type)
        {
            if (model == null) { type = MessageType.Info; return "Load a model (FBX or prefab) to begin."; }

            Avatar av = null;
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null) av = animator.avatar;
            if (av == null)
            {
                string path = AssetDatabase.GetAssetPath(model);
                if (!string.IsNullOrEmpty(path))
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset is Avatar found) { av = found; break; }
            }

            if (av != null && av.isValid && av.isHuman)
            {
                type = MessageType.Info;
                return "OK Humanoid avatar (retarget ready)";
            }
            if (av != null && av.isValid)
            {
                type = MessageType.Warning;
                return "WARN avatar valid but GENERIC (not human) — humanoid clips will NOT retarget";
            }
            type = MessageType.Error;
            return "FAIL no valid avatar — rig did NOT map (hand-map needed)";
        }

        private void RebuildPreviewInstance()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }
            if (_model == null) return;

            if (_preview == null)
            {
                _preview = new PreviewRenderUtility();
                _preview.camera.fieldOfView = 30f;
                _preview.camera.nearClipPlane = 0.05f;
                _preview.camera.farClipPlane = 100f;
                _preview.lights[0].intensity = 1.2f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                if (_preview.lights.Length > 1) _preview.lights[1].intensity = 0.6f;
                _preview.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            }

            _previewInstance = Instantiate(_model);
            _previewInstance.hideFlags = HideFlags.HideAndDontSave;
            _previewInstance.transform.position = Vector3.zero;
            _previewInstance.transform.rotation = Quaternion.identity;
            _preview.AddSingleGO(_previewInstance);
            _time = 0f;
            _playing = false;
            SamplePose();
        }

        /// <summary>Sample the selected clip at _time onto the preview instance —
        /// AnimationMode (humanoid retarget path) with clip.SampleAnimation as the
        /// generic/legacy fallback. Un-retargeted FBX clips are best-effort (the
        /// window shows the "needs extraction" warning alongside).</summary>
        private void SamplePose()
        {
            if (_previewInstance == null || _selected?.Clip == null) return;
            var clip = _selected.Clip;
            var animator = _previewInstance.GetComponentInChildren<Animator>(true);
            bool humanoidPath = clip.isHumanMotion &&
                animator != null && animator.avatar != null &&
                animator.avatar.isValid && animator.avatar.isHuman;
            if (humanoidPath)
            {
                if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_previewInstance, clip, _time);
                AnimationMode.EndSampling();
            }
            else
            {
                clip.SampleAnimation(_previewInstance, _time);
            }
        }

        // ── GUI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibraryColumn();
                using (var scroll = new EditorGUILayout.ScrollViewScope(_mainScroll))
                {
                    _mainScroll = scroll.scrollPosition;
                    DrawPreviewColumn();
                    EditorGUILayout.Space(6f);
                    DrawBindingColumn();
                }
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var model = (GameObject)EditorGUILayout.ObjectField(
                    "Model", _model, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck()) SetModel(model);

                if (GUILayout.Button("Rescan Library", GUILayout.Width(110f)))
                {
                    ScanLibrary();
                    LoadPickerSources();
                }
            }
            if (!string.IsNullOrEmpty(_avatarVerdict) || _model == null)
                EditorGUILayout.HelpBox(_avatarVerdict ?? "Load a model (FBX or prefab) to begin.",
                    _model == null ? MessageType.Info : _verdictType);
        }

        private void DrawLibraryColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300f), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField($"Motion Library ({_library.Count} clips)", EditorStyles.boldLabel);
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                _chipIndex = GUILayout.SelectionGrid(_chipIndex, _chips, 4, EditorStyles.miniButton);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_libScroll,
                    GUILayout.ExpandHeight(true)))
                {
                    _libScroll = scroll.scrollPosition;
                    string chip = _chipIndex > 0 && _chipIndex < _chips.Length ? _chips[_chipIndex] : null;
                    foreach (var entry in _library)
                    {
                        if (chip != null && entry.Category != chip) continue;
                        if (!string.IsNullOrEmpty(_search) &&
                            entry.Label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                            entry.Path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        bool isSelected = ReferenceEquals(entry, _selected);
                        var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
                        if (GUILayout.Button(entry.Label, style))
                        {
                            _selected = entry;
                            _time = 0f;
                            _playing = false;
                            SamplePose();
                        }
                    }
                }
            }
        }

        private void DrawPreviewColumn()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            Rect rect = GUILayoutUtility.GetRect(256f, 260f, GUILayout.ExpandWidth(true));
            HandleOrbitDrag(rect);
            if (_model == null)
            {
                EditorGUI.HelpBox(rect, "Load a model to preview clips.", MessageType.Info);
            }
            else if (Event.current.type == EventType.Repaint && _preview != null && _previewInstance != null)
            {
                RenderPreview(rect);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_selected?.Clip == null || _previewInstance == null))
                {
                    if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60f)))
                    {
                        _playing = !_playing;
                        float len = _selected != null ? _selected.Clip.length : 0f;
                        if (_playing && _time >= len) _time = 0f;
                    }
                    _loop = GUILayout.Toggle(_loop, "Loop", GUILayout.Width(50f));

                    float length = _selected?.Clip != null ? Mathf.Max(_selected.Clip.length, 0.001f) : 1f;
                    EditorGUI.BeginChangeCheck();
                    _time = EditorGUILayout.Slider(_time, 0f, length);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _playing = false;
                        SamplePose();
                    }
                }
            }

            if (_selected != null)
            {
                EditorGUILayout.LabelField(
                    $"{_selected.Clip.name}  ({_selected.Clip.length:0.00}s, {_selected.Source})",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(_selected.Path, EditorStyles.miniLabel);
                if (_selected.NeedsExtraction)
                    EditorGUILayout.HelpBox(
                        "Un-retargeted FBX clip — preview is best-effort and may not match the " +
                        "retargeted in-game read. Extract/retarget first via " +
                        "'Defenders → Heroes → Import Knight Hero Package' (hero packs) or " +
                        "'Defenders → Animation → Reimport Action Clips (force Humanoid)' " +
                        "(batch extract from inside this tool is WO-670 slice-2 out-of-scope).",
                        MessageType.Warning);
            }
        }

        private void HandleOrbitDrag(Rect rect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition))
            {
                _orbitYaw += e.delta.x * 0.7f;
                e.Use();
                Repaint();
            }
        }

        private void RenderPreview(Rect rect)
        {
            var bounds = ComputeBounds(_previewInstance);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            var pivot = bounds.center;
            var camRot = Quaternion.Euler(15f, _orbitYaw, 0f);
            var camPos = pivot - camRot * Vector3.forward * (radius * 3.2f);

            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.transform.SetPositionAndRotation(camPos, camRot);
            _preview.camera.Render();
            var tex = _preview.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        // ── Binding row + save (canon §8 write contract) ─────────────────────

        private void DrawBindingColumn()
        {
            EditorGUILayout.LabelField("Keyword Binding (action bundle)", EditorStyles.boldLabel);

            // Target — data-driven from the registry (families + classes + roots).
            if (_targets.Length > 0)
            {
                _targetIndex = EditorGUILayout.Popup("Target", _targetIndex, _targets);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Registry declares no targets (motion-castings.json absent/empty) — " +
                    "type the target id; it will be created on save.", MessageType.Warning);
                _targetFree = EditorGUILayout.TextField("Target", _targetFree);
            }

            _keywordIndex = EditorGUILayout.Popup("Keyword", _keywordIndex, _keywordLabels);

            // vfxKey — HovlVfxCatalog key dropdown; free text fallback when the
            // catalog keys couldn't be listed (warned in LoadHovlVfxKeys).
            if (_vfxKeys != null)
            {
                _vfxKeyIndex = EditorGUILayout.Popup("VFX Key", _vfxKeyIndex, _vfxKeys);
            }
            else
            {
                _vfxKeyFree = EditorGUILayout.TextField("VFX Key", _vfxKeyFree);
                if (!string.IsNullOrEmpty(_vfxKeyFree))
                    EditorGUILayout.HelpBox(
                        "HovlVfxCatalog keys could not be listed — this key is UNVALIDATED " +
                        "(an unknown key no-ops at play time, logged).", MessageType.Warning);
            }

            // sfxId — SfxId enum names; free text fallback when the type lookup failed.
            if (_sfxIds != null)
            {
                _sfxIdIndex = EditorGUILayout.Popup("SFX Id", _sfxIdIndex, _sfxIds);
            }
            else
            {
                _sfxIdFree = EditorGUILayout.TextField("SFX Id", _sfxIdFree);
                if (!string.IsNullOrEmpty(_sfxIdFree))
                    EditorGUILayout.HelpBox(
                        "SfxId enum could not be listed — this id is UNVALIDATED.",
                        MessageType.Warning);
            }

            _vfxDelay = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("VFX Delay (s)", "Seconds after animation start to fire the VFX."),
                _vfxDelay));
            _attachBone = EditorGUILayout.TextField(
                new GUIContent("Attach Bone", "Humanoid bone/attach name, e.g. hand.r / weapon / spine."),
                _attachBone);
            _playOneShot = EditorGUILayout.Toggle(
                new GUIContent("Play One-Shot", "Overlay that must not disturb the base state " +
                    "(hit reactions, impacts)."),
                _playOneShot);

            // Bundle summary — the "VFX key named" half of the WO-671 §2 preview.
            if (_selected != null)
                EditorGUILayout.LabelField(
                    $"Bundle: anim='{_selected.Clip.name}' vfx='{SelectedVfxKey()}'@{_vfxDelay:0.##}s " +
                    $"bone='{_attachBone}' sfx='{SelectedSfxId()}' oneShot={_playOneShot}",
                    EditorStyles.miniLabel);

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(_selected == null || SelectedTarget().Length == 0))
            {
                if (GUILayout.Button("Save Binding (manual = canon)", GUILayout.Height(28f)))
                    SaveBinding();
            }
        }

        private string SelectedTarget() =>
            _targets.Length > 0
                ? _targets[Mathf.Clamp(_targetIndex, 0, _targets.Length - 1)]
                : (_targetFree ?? string.Empty).Trim();

        private string SelectedKeyword() =>
            _keywords.Length > 0
                ? _keywords[Mathf.Clamp(_keywordIndex, 0, _keywords.Length - 1)]
                : string.Empty;

        private string SelectedVfxKey() =>
            _vfxKeys != null
                ? (_vfxKeyIndex <= 0 ? string.Empty : _vfxKeys[Mathf.Clamp(_vfxKeyIndex, 0, _vfxKeys.Length - 1)])
                : (_vfxKeyFree ?? string.Empty).Trim();

        private string SelectedSfxId() =>
            _sfxIds != null
                ? (_sfxIdIndex <= 0 ? string.Empty : _sfxIds[Mathf.Clamp(_sfxIdIndex, 0, _sfxIds.Length - 1)])
                : (_sfxIdFree ?? string.Empty).Trim();

        private void SaveBinding()
        {
            string target = SelectedTarget();
            string keyword = SelectedKeyword();
            string clipPath = _selected != null ? _selected.Path : null;
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(keyword) ||
                string.IsNullOrEmpty(clipPath))
            {
                Debug.LogError(Log + "save aborted — target, keyword and a selected clip are required.");
                return;
            }

            // Cast-category lint (canon §2): a cast/castChannel keyword bound to an
            // attack-taxonomy clip is a BLOCKING confirm — swings are never casts.
            bool isCastKeyword = IsCastKeyword(keyword);
            bool isAttackClip = Regex.IsMatch(clipPath, @"(^|[/\\])atk_|slash", RegexOptions.IgnoreCase) ||
                                Regex.IsMatch(_selected.Clip.name, @"^atk_|slash", RegexOptions.IgnoreCase);
            if (isCastKeyword && isAttackClip)
            {
                bool overrideLint = EditorUtility.DisplayDialog("Motion Caster — cast lint",
                    $"'{keyword}' is a CAST-category keyword but '{_selected.Clip.name}' is an " +
                    "attack-taxonomy clip (atk_* / *Slash*).\n\nCast-type actions fire CAST clips, " +
                    "never swings (Knight_Anim_Inventory rule).\n\nBind it anyway?",
                    "Bind anyway", "Cancel");
                if (!overrideLint)
                {
                    Debug.LogWarning(Log + $"save cancelled — cast lint on '{target}.{keyword}' " +
                        $"vs attack clip '{clipPath}'.");
                    return;
                }
            }

            // Manual-row preservation (Offset Forge law): overwriting an existing
            // owner pick requires an explicit confirm, passed through to WriteRow.
            bool allowManualOverwrite = false;
            if (MotionCastings.TryGetRow(target, keyword, out var existing) && existing.manual)
            {
                allowManualOverwrite = EditorUtility.DisplayDialog("Motion Caster — canon row",
                    $"'{target}.{keyword}' already has an OWNER PICK (manual:true):\n" +
                    $"{existing.clip}\n\nReplace it with '{_selected.Clip.name}'?",
                    "Replace (owner confirm)", "Keep existing");
                if (!allowManualOverwrite)
                {
                    Debug.Log(Log + $"save skipped — kept existing canon row '{target}.{keyword}'.");
                    return;
                }
            }

            var row = new CastingRow
            {
                clip        = clipPath,
                guid        = AssetDatabase.AssetPathToGUID(clipPath),
                vfxKey      = SelectedVfxKey(),
                sfxId       = SelectedSfxId(),
                vfxDelay    = _vfxDelay,
                attachBone  = (_attachBone ?? string.Empty).Trim(),
                playOneShot = _playOneShot,
                manual      = true,
                pickedUtc   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                source      = "motion-caster",
            };

            // WriteRow validates the closed vocabulary, refuses unknown keywords,
            // writes BOTH canonical copies, and logs the acceptance line:
            // [MotionCaster] '<target>.<keyword>' -> '<clip>' (manual) saved.
            if (MotionCastings.WriteRow(target, keyword, row, allowManualOverwrite))
            {
                AssetDatabase.ImportAsset(MotionCastings.DefaultRegistryPath);
                AssetDatabase.ImportAsset(MotionCastings.ResourcesRegistryPath);
                LoadPickerSources(); // a brand-new target id becomes pickable
                ShowNotification(new GUIContent($"Saved '{target}.{keyword}'"));
            }
            else
            {
                ShowNotification(new GUIContent("Save refused — see Console"));
            }
        }

        private static bool IsCastKeyword(string keyword)
        {
            var castKeywords = MotionCastings.CategoryKeywords("cast");
            if (castKeywords.Count == 0)
                castKeywords = DeNelle.Core.Combat.ActionKeywords.CastKeywords;
            foreach (string kw in castKeywords)
                if (string.Equals(kw, keyword, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
