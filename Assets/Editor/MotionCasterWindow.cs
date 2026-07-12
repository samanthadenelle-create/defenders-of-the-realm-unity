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
using System.IO;
using System.Reflection;
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

        /// <summary>One previewable clip in the library list. Multi-take FBXs
        /// (ActorCore zips ship a 0.04s '0_T-Pose' take first) produce ONE entry
        /// PER TAKE — name + length shown, junk takes flagged by TEXT (owner is
        /// red/green colorblind; never hue-only cues).</summary>
        private sealed class ClipEntry
        {
            public AnimationClip Clip;
            public string Path;            // containing asset path (.anim or .fbx)
            public string Source;          // "extracted" | "action" | "kaykit"
            public bool NeedsExtraction;   // FBX-borne clip — un-retargeted preview
            public string Category;        // vocabulary-category guess (chip filter)
            public string Label;           // list display
            public bool JunkTake;          // t-pose / bind / preview take — skip it
            public float RootTravel = -1f; // metres the root moves t0→tEnd (-1 = not yet measured)
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
        // Owner filter: hide the loose Mixamo clips + the KayKit pack so only the
        // studio-mocap / ActorCore / owner-drop clips list. Persisted across sessions.
        private bool _mocapOnly = EditorPrefs.GetBool("MotionCaster.MocapOnly", false);
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

        // ── VFX preview bundle (owner self-service — VFX fires inside the stage) ─
        private bool _previewBundle;
        private GameObject _vfxInstance;          // instantiated catalog prefab, child of the bone
        private string _vfxInstanceKey;           // key the live instance was built from
        private string _vfxInstanceBone;          // bone the live instance was attached to
        private ParticleSystem[] _vfxRoots =      // top-level systems — Simulate(t, children:true)
            Array.Empty<ParticleSystem>();
        private string _vfxPreviewMsg;            // inline resolve failure, never silent

        // ── SFX audition ─────────────────────────────────────────────────────
        private string _sfxAuditionMsg;           // inline 'no clip found' feedback

        // ── Save feedback (item 4) ───────────────────────────────────────────
        private string _lastSaveMsg;              // confirmation + rebake reminder

        // Owner drop folder — one-button ActorCore/Mixamo FBX intake.
        private const string OwnerDropsFolder = "Assets/Action/Knight/Motion/owner-drops";

        // Root travel above this (metres) = the take will slide/reset in-game.
        private const float RootTravelWarnThreshold = 0.25f;

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
            DestroyVfxInstance();   // no leaked preview VFX objects on window close
            StopSfxPreview();
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
            // Per-take listing: multi-take FBXs (ActorCore zips) already yield one
            // entry per AnimationClip sub-asset — name + LENGTH shown so a 0.04s
            // '0_T-Pose' first take is visibly junk, flagged by TEXT not colour.
            bool junk = IsJunkTake(clip);
            string label = junk
                ? $"{clip.name}  ({clip.length:0.00}s)  [SKIP: T-POSE/BIND]  [{source}]"
                : $"{clip.name}  ({clip.length:0.00}s)  [{source}]";
            _library.Add(new ClipEntry
            {
                Clip = clip,
                Path = path,
                Source = source,
                NeedsExtraction = needsExtraction,
                Category = GuessCategory(clip.name),
                Label = label,
                JunkTake = junk,
            });
        }

        /// <summary>T-pose / bind / preview takes (ActorCore ships a 0.04s
        /// '0_T-Pose' take first in every multi-take FBX) — flagged, never
        /// default-selected.</summary>
        private static bool IsJunkTake(AnimationClip clip)
        {
            if (clip.length <= 0.1f) return true;
            return Regex.IsMatch(clip.name, @"t[-_ ]?pose|bind|^__?preview|^preview\b",
                RegexOptions.IgnoreCase);
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
            DestroyVfxInstance();   // vfx child dies with its parent — clear refs first
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
            SampleVfx();   // particles follow the scrub time (PreviewRenderUtility never ticks them)
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

                // One-button ActorCore/Mixamo intake — no CLI round-trip (item 3).
                if (GUILayout.Button(new GUIContent("Import dropped FBX…",
                        "Pick an FBX anywhere on disk (e.g. an unzipped ActorCore download). " +
                        "It is copied into " + OwnerDropsFolder + ", imported as Humanoid, and " +
                        "its longest real take is selected in the list."),
                    GUILayout.Width(140f)))
                {
                    ImportDroppedFbx();
                }

                if (GUILayout.Button(new GUIContent("Reimport drops",
                        "Reimport every FBX already in owner-drops (fixes a failed first import " +
                        "that logged 'does not exist' / zero takes)."),
                    GUILayout.Width(100f)))
                {
                    ReimportOwnerDrops();
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
                bool mocapOnly = EditorGUILayout.ToggleLeft(
                    new GUIContent("Mocap only (hide Mixamo + KayKit)",
                        "Show only studio-mocap / ActorCore / owner-drops clips."),
                    _mocapOnly);
                if (mocapOnly != _mocapOnly)
                {
                    _mocapOnly = mocapOnly;
                    EditorPrefs.SetBool("MotionCaster.MocapOnly", _mocapOnly);
                }
                _chipIndex = GUILayout.SelectionGrid(_chipIndex, _chips, 4, EditorStyles.miniButton);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_libScroll,
                    GUILayout.ExpandHeight(true)))
                {
                    _libScroll = scroll.scrollPosition;
                    string chip = _chipIndex > 0 && _chipIndex < _chips.Length ? _chips[_chipIndex] : null;
                    foreach (var entry in _library)
                    {
                        if (_mocapOnly && !IsMocapEntry(entry)) continue;
                        if (chip != null && entry.Category != chip) continue;
                        if (!string.IsNullOrEmpty(_search) &&
                            entry.Label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                            entry.Path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        bool isSelected = ReferenceEquals(entry, _selected);
                        var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
                        if (GUILayout.Button(entry.Label, style))
                        {
                            SelectEntry(entry);
                        }
                    }
                }
            }
        }

        /// <summary>Mocap-only filter: studio mocap packs, ActorCore zips, and the
        /// owner-drops intake folder count as mocap; loose Mixamo clips (Action root,
        /// source "action" outside those folders) and the KayKit pack are hidden.</summary>
        private static bool IsMocapEntry(ClipEntry entry)
        {
            if (entry.Source == "kaykit") return false;
            string p = entry.Path;
            return p.IndexOf("/studio-mocap-", StringComparison.OrdinalIgnoreCase) >= 0
                || p.IndexOf("/actorcore-",   StringComparison.OrdinalIgnoreCase) >= 0
                || p.IndexOf("/owner-drops/", StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Source == "extracted"; // retargeted hero-package .anim = already curated
        }

        /// <summary>Select a library entry: reset playback, measure root travel
        /// once (lazy — scan-time sampling of every clip would be expensive), and
        /// rebuild the preview VFX instance for the new clip's timeline.</summary>
        private void SelectEntry(ClipEntry entry)
        {
            _selected = entry;
            _time = 0f;
            _playing = false;
            if (entry != null && entry.RootTravel < 0f)
                entry.RootTravel = ComputeRootTravel(entry.Clip);
            DestroyVfxInstance();       // clean per selection change — no leaks
            SyncVfxPreviewInstance();   // rebuild against the new clip if bundling
            SamplePose();
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

                    EditorGUI.BeginChangeCheck();
                    _previewBundle = GUILayout.Toggle(_previewBundle,
                        new GUIContent("Preview bundle",
                            "Instantiate the selected VFX Key's prefab onto the attach bone and " +
                            "fire it at VFX Delay into the clip — the felt read, in the stage."),
                        GUILayout.Width(110f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!_previewBundle) DestroyVfxInstance();
                        else SyncVfxPreviewInstance();
                        SamplePose();
                    }

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

            // Bundle-preview inline status — a missing prefab/bone is never silent.
            if (_previewBundle && !string.IsNullOrEmpty(_vfxPreviewMsg))
                EditorGUILayout.HelpBox(_vfxPreviewMsg, MessageType.Warning);

            if (_selected != null)
            {
                EditorGUILayout.LabelField(
                    $"{_selected.Clip.name}  ({_selected.Clip.length:0.00}s, {_selected.Source})",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(_selected.Path, EditorStyles.miniLabel);

                // Junk-take flag — text cue (owner is red/green colorblind).
                if (_selected.JunkTake)
                    EditorGUILayout.HelpBox(
                        "SKIP: this take looks like a T-POSE / BIND / preview take (ActorCore " +
                        "multi-take FBXs ship a 0.04s '0_T-Pose' take first). Pick a real take " +
                        "from the same FBX instead.", MessageType.Warning);

                // Root-motion travel — today's 'runs left to right then resets' lesson,
                // surfaced at pick time instead of discovered in-game.
                if (_selected.RootTravel > RootTravelWarnThreshold)
                    EditorGUILayout.HelpBox(
                        $"Root motion: this take travels {_selected.RootTravel:0.0} units — it " +
                        "will slide/reset in-game. Run 'Defenders → Animation → Fix Action Clip " +
                        "Root Motion (stop slide)' before binding.", MessageType.Warning);

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
            EditorGUI.BeginChangeCheck();
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
            if (EditorGUI.EndChangeCheck() && _previewBundle)
            {
                SyncVfxPreviewInstance();   // new key → new preview prefab
                SamplePose();
            }

            // sfxId — SfxId enum names; free text fallback when the type lookup
            // failed — plus an audition Play button (editor AudioUtil, item 2).
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_sfxIds != null)
                {
                    _sfxIdIndex = EditorGUILayout.Popup("SFX Id", _sfxIdIndex, _sfxIds);
                }
                else
                {
                    _sfxIdFree = EditorGUILayout.TextField("SFX Id", _sfxIdFree);
                }
                using (new EditorGUI.DisabledScope(SelectedSfxId().Length == 0))
                {
                    if (GUILayout.Button(new GUIContent("Play",
                            "Audition this SFX now (loads Resources/Sfx/<id>)."),
                        GUILayout.Width(44f)))
                        AuditionSfx(SelectedSfxId());
                }
                if (GUILayout.Button(new GUIContent("Stop", "Stop the SFX audition."),
                    GUILayout.Width(44f)))
                    StopSfxPreview();
            }
            if (_sfxIds == null && !string.IsNullOrEmpty(_sfxIdFree))
                EditorGUILayout.HelpBox(
                    "SfxId enum could not be listed — this id is UNVALIDATED.",
                    MessageType.Warning);
            if (!string.IsNullOrEmpty(_sfxAuditionMsg))
                EditorGUILayout.HelpBox(_sfxAuditionMsg, MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            _vfxDelay = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("VFX Delay (s)", "Seconds after animation start to fire the VFX."),
                _vfxDelay));
            if (EditorGUI.EndChangeCheck() && _previewBundle)
                SamplePose();   // same instance, new fire time — just resimulate

            EditorGUI.BeginChangeCheck();
            _attachBone = EditorGUILayout.TextField(
                new GUIContent("Attach Bone", "Humanoid bone/attach name, e.g. hand.r / weapon / spine."),
                _attachBone);
            if (EditorGUI.EndChangeCheck() && _previewBundle)
            {
                SyncVfxPreviewInstance();   // reattach to the new bone
                SamplePose();
            }
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

            string target = SelectedTarget();
            if (target.Length > 0)
            {
                EditorGUILayout.Space(2f);
                if (GUILayout.Button(new GUIContent("Rebake controller for target",
                        RebakeHint(target) + "\n\nRuns the matching Defenders menu bake now."),
                    GUILayout.Height(24f)))
                    RebakeForTarget(target, SelectedKeyword());
            }

            // Item 4: persistent inline confirmation — names the saved row + the
            // rebake the pick needs before it is felt in-game.
            if (!string.IsNullOrEmpty(_lastSaveMsg))
                EditorGUILayout.HelpBox(_lastSaveMsg, MessageType.Info);
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
                _lastSaveMsg =
                    $"SAVED: {target}.{keyword} -> '{_selected.Clip.name}' " +
                    $"(take of {Path.GetFileName(clipPath)})\n" +
                    $"REBAKE NEEDED before it is felt in-game: {RebakeHint(target)}";
                ShowNotification(new GUIContent($"Saved '{target}.{keyword}'"));
            }
            else
            {
                _lastSaveMsg = null;
                ShowNotification(new GUIContent("Save refused — see Console"));
            }
        }

        /// <summary>Which controller builder rebakes this target's animator —
        /// castings are read at BAKE time, so a saved row is inert until rebaked.</summary>
        private static string RebakeHint(string target)
        {
            string t = (target ?? string.Empty).ToLowerInvariant();
            if (t.StartsWith("orc", StringComparison.Ordinal))
                return "'Defenders → Tripo → Build Orc Humanoid Family Controllers (WO-491)'";
            if (t.StartsWith("knight", StringComparison.Ordinal))
                return "'Defenders → Heroes → Build Knight Package Controller' " +
                       "(+ 'Defenders → Animation → Build Knight Mocap Locomotion Controller' " +
                       "for locomotion keywords)";
            if (t == "warrior" || t == "mage" || t == "archer" ||
                t == "ranger" || t == "cleric" || t.StartsWith("hero", StringComparison.Ordinal))
                return "'Defenders → Animation → Build Hero Animators (Mixamo)'";
            return "'Defenders → Animation → Build Animator Controllers' " +
                   "(enemy families — AnimatorSetup)";
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

        // ── Item 1: VFX bundle in the preview stage ──────────────────────────
        // PreviewRenderUtility never ticks ParticleSystems — the instance's
        // top-level systems are Simulate()d manually at (scrub time − vfxDelay).

        /// <summary>Create/replace/remove the preview VFX instance so it matches
        /// the current (previewBundle, vfxKey, attachBone) state. Idempotent —
        /// safe to call from any change handler.</summary>
        private void SyncVfxPreviewInstance()
        {
            string key = _previewBundle ? SelectedVfxKey() : string.Empty;
            string bone = (_attachBone ?? string.Empty).Trim();

            bool stale = _vfxInstance != null &&
                (!string.Equals(_vfxInstanceKey, key, StringComparison.Ordinal) ||
                 !string.Equals(_vfxInstanceBone, bone, StringComparison.Ordinal));
            if (stale || key.Length == 0 || _previewInstance == null)
                DestroyVfxInstance();

            _vfxPreviewMsg = null;
            if (!_previewBundle) return;
            if (key.Length == 0)
            {
                _vfxPreviewMsg = "Preview bundle is ON but no VFX Key is selected.";
                return;
            }
            if (_previewInstance == null)
            {
                _vfxPreviewMsg = "Load a model first — the VFX attaches to the preview rig.";
                return;
            }
            if (_vfxInstance != null) return; // already current

            var prefab = LoadHovlVfxPrefab(key);
            if (prefab == null)
            {
                _vfxPreviewMsg = $"No prefab resolved for VFX key '{key}' in HovlVfxCatalog " +
                    "(row missing or its Prefab is null) — nothing to preview.";
                return;
            }

            Transform attach = ResolveAttachBone(bone, out bool boneFound);
            // Parenting under the (already-added) preview instance puts the VFX in
            // the PreviewRenderUtility scene, and it follows the sampled pose.
            _vfxInstance = Instantiate(prefab, attach, false);
            _vfxInstance.hideFlags = HideFlags.HideAndDontSave;
            _vfxInstance.transform.localPosition = Vector3.zero;
            _vfxInstanceKey = key;
            _vfxInstanceBone = bone;

            // Top-level systems only — Simulate(withChildren:true) covers subs.
            var all = _vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            var roots = new List<ParticleSystem>();
            foreach (var ps in all)
            {
                var parent = ps.transform.parent;
                if (parent == null || parent.GetComponentInParent<ParticleSystem>(true) == null)
                    roots.Add(ps);
            }
            _vfxRoots = roots.ToArray();

            if (!boneFound && bone.Length > 0)
                _vfxPreviewMsg = $"Attach bone '{bone}' not found on this rig — " +
                    "VFX attached to the model root instead.";
            if (all.Length == 0)
                _vfxPreviewMsg = $"'{key}' prefab has no ParticleSystems — " +
                    "only its static meshes will show in the stage.";
        }

        private void DestroyVfxInstance()
        {
            if (_vfxInstance != null) DestroyImmediate(_vfxInstance);
            _vfxInstance = null;
            _vfxInstanceKey = null;
            _vfxInstanceBone = null;
            _vfxRoots = Array.Empty<ParticleSystem>();
        }

        /// <summary>Drive the preview VFX to the current scrub time. Before the
        /// fire moment (vfxDelay) the systems are cleared; after it they are
        /// Simulate()d to the elapsed time — deterministic under scrubbing.</summary>
        private void SampleVfx()
        {
            if (_vfxInstance == null) return;
            float t = _time - _vfxDelay;
            foreach (var ps in _vfxRoots)
            {
                if (ps == null) continue;
                if (t < 0f)
                {
                    ps.Simulate(0f, true, true);
                    ps.Clear(true);
                }
                else
                {
                    ps.Simulate(t, true, true);
                }
            }
        }

        /// <summary>Resolve a vfxKey to its catalog prefab the same SerializedObject
        /// way the key dropdown is listed (DeNelle.Editor never references
        /// DeNelle.Village).</summary>
        private static GameObject LoadHovlVfxPrefab(string key)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(HovlCatalogAssetPath);
            if (asset == null) return null;
            var so = new SerializedObject(asset);
            var rows = so.FindProperty("Rows");
            if (rows == null || !rows.isArray) return null;
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var k = row.FindPropertyRelative("Key");
                if (k == null || !string.Equals(k.stringValue, key, StringComparison.Ordinal))
                    continue;
                var prefabProp = row.FindPropertyRelative("Prefab");
                return prefabProp != null ? prefabProp.objectReferenceValue as GameObject : null;
            }
            return null;
        }

        /// <summary>Attach-bone resolution on the PREVIEW model: humanoid alias →
        /// Animator.GetBoneTransform, then case-insensitive name search (exact,
        /// then contains), then the model root.</summary>
        private Transform ResolveAttachBone(string boneName, out bool found)
        {
            found = true;
            Transform root = _previewInstance.transform;
            if (string.IsNullOrEmpty(boneName)) return root;

            var animator = _previewInstance.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null &&
                animator.avatar.isValid && animator.avatar.isHuman)
            {
                HumanBodyBones hb = MapBoneAlias(boneName);
                if (hb != HumanBodyBones.LastBone)
                {
                    var t = animator.GetBoneTransform(hb);
                    if (t != null) return t;
                }
            }

            Transform contains = null;
            foreach (var t in _previewInstance.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, boneName, StringComparison.OrdinalIgnoreCase))
                    return t;
                if (contains == null &&
                    t.name.IndexOf(boneName, StringComparison.OrdinalIgnoreCase) >= 0)
                    contains = t;
            }
            if (contains != null) return contains;
            found = false;
            return root;
        }

        /// <summary>Registry bone-name conventions ("hand.r", "weapon", "spine")
        /// → humanoid bones. LastBone = no alias (name search takes over).</summary>
        private static HumanBodyBones MapBoneAlias(string boneName)
        {
            switch (boneName.Trim().ToLowerInvariant().Replace("_", ".").Replace(" ", "."))
            {
                case "hand.r": case "r.hand": case "righthand": case "hand.right":
                case "weapon": case "weapon.r":
                    return HumanBodyBones.RightHand;
                case "hand.l": case "l.hand": case "lefthand": case "hand.left":
                case "shield": case "offhand":
                    return HumanBodyBones.LeftHand;
                case "head":                       return HumanBodyBones.Head;
                case "neck":                       return HumanBodyBones.Neck;
                case "spine":                      return HumanBodyBones.Spine;
                case "chest":                      return HumanBodyBones.Chest;
                case "hips": case "pelvis": case "root":
                    return HumanBodyBones.Hips;
                case "foot.r": case "rightfoot":   return HumanBodyBones.RightFoot;
                case "foot.l": case "leftfoot":    return HumanBodyBones.LeftFoot;
                default:                           return HumanBodyBones.LastBone;
            }
        }

        // ── Item 2: SFX audition (editor AudioUtil via reflection) ───────────

        /// <summary>Play Resources/Sfx/&lt;id&gt; through the editor's preview
        /// channel (UnityEditor.AudioUtil — internal, reached by reflection; the
        /// standard editor audition trick). Failures are inline, never silent.</summary>
        private void AuditionSfx(string sfxId)
        {
            _sfxAuditionMsg = null;
            if (string.IsNullOrEmpty(sfxId)) return;

            var clip = Resources.Load<AudioClip>("Sfx/" + sfxId);
            if (clip == null)
            {
                _sfxAuditionMsg = $"No clip found at Resources/Sfx/{sfxId} — the id will be " +
                    "silent in-game until a clip lands there (or the SfxClipLibrary maps it).";
                return;
            }

            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null)
            {
                _sfxAuditionMsg = "Editor AudioUtil type not found — cannot audition in this " +
                    "Unity version (the saved id is still valid).";
                return;
            }
            var flags = BindingFlags.Static | BindingFlags.Public;
            var sig = new[] { typeof(AudioClip), typeof(int), typeof(bool) };
            // Unity 2020+ names it PlayPreviewClip; older editors used PlayClip.
            MethodInfo play = audioUtil.GetMethod("PlayPreviewClip", flags, null, sig, null)
                           ?? audioUtil.GetMethod("PlayClip", flags, null, sig, null);
            if (play == null)
            {
                _sfxAuditionMsg = "AudioUtil.PlayPreviewClip/PlayClip not found — cannot " +
                    "audition in this Unity version (the saved id is still valid).";
                return;
            }
            StopSfxPreview();
            play.Invoke(null, new object[] { clip, 0, false });
        }

        private static void StopSfxPreview()
        {
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var flags = BindingFlags.Static | BindingFlags.Public;
            MethodInfo stop = audioUtil?.GetMethod("StopAllPreviewClips", flags)
                           ?? audioUtil?.GetMethod("StopAllClips", flags);
            stop?.Invoke(null, null);
        }

        // ── Item 3: one-button FBX intake (ActorCore zip flow) ───────────────

        /// <summary>Copy any on-disk FBX into the owner-drops folder, import it
        /// Humanoid, rescan, and select its longest REAL take (multi-take ActorCore
        /// FBXs lead with a 0.04s '0_T-Pose' junk take — never default to it).</summary>
        private void ImportDroppedFbx()
        {
            string src = EditorUtility.OpenFilePanel(
                "Import motion FBX (ActorCore / Mixamo / any)", string.Empty, "fbx");
            if (string.IsNullOrEmpty(src)) return;

            EnsureOwnerDropsFolder();

            string fileName = Path.GetFileName(src);
            string dst = OwnerDropsFolder + "/" + fileName;
            string absDst = ProjectPathToAbsolute(dst);
            if (File.Exists(absDst) &&
                !EditorUtility.DisplayDialog("Motion Caster — replace?",
                    $"{fileName} is already in owner-drops.\n\nReplace and reimport it?",
                    "Replace", "Import as copy"))
            {
                dst = AssetDatabase.GenerateUniqueAssetPath(dst);
                absDst = ProjectPathToAbsolute(dst);
            }

            try
            {
                File.Copy(src, absDst, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.LogError(Log + $"import failed copying '{src}' -> '{dst}': {ex.Message}");
                EditorUtility.DisplayDialog("Motion Caster — import failed",
                    $"Could not copy the FBX into the project:\n{ex.Message}", "OK");
                return;
            }

            // File.Copy lands on disk first; ImportAsset before Refresh races Unity and
            // logs "'…fbx' does not exist" → no ModelImporter, zero takes (Editor.log RCA).
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            WaitForImport(dst);

            var importer = AssetImporter.GetAtPath(dst) as ModelImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    dirty = true;
                }
                if (importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    dirty = true;
                }
                if (dirty) importer.SaveAndReimport();
            }
            else
            {
                Debug.LogWarning(Log + $"'{dst}' imported but no ModelImporter found — " +
                    "Humanoid rig not forced.");
            }

            ScanLibrary();
            LoadPickerSources();

            var pick = SelectBestTakeFromPath(dst, out int takes, out int junk);

            string summary = takes == 0
                ? $"Imported {Path.GetFileName(dst)} — but NO animation takes were found in it. " +
                  "If this FBX is mesh-only or from a zip that needs extracting, pick a motion FBX instead."
                : $"Imported {Path.GetFileName(dst)}: {takes} take(s), {junk} junk " +
                  $"(t-pose/bind) — selected '{(pick != null ? pick.Clip.name : "none")}'.";
            Debug.Log(Log + summary);
            ShowNotification(new GUIContent(summary));
            if (takes == 0)
                EditorUtility.DisplayDialog("Motion Caster — no takes",
                    summary + "\n\nThe file is in:\n" + dst, "OK");
        }

        /// <summary>Block until Unity finishes importing <paramref name="assetPath"/>
        /// (or ~30s timeout). Prevents scanning the library before clips exist.</summary>
        private static void WaitForImport(string assetPath)
        {
            const double timeoutSec = 30.0;
            double start = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - start < timeoutSec)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null &&
                    AssetImporter.GetAtPath(assetPath) is ModelImporter)
                    return;
                System.Threading.Thread.Sleep(50);
            }
        }

        private void EnsureOwnerDropsFolder()
        {
            if (AssetDatabase.IsValidFolder(OwnerDropsFolder)) return;
            Directory.CreateDirectory(ProjectPathToAbsolute(OwnerDropsFolder));
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static string ProjectPathToAbsolute(string projectPath)
        {
            string rel = (projectPath ?? string.Empty).Replace('\\', '/');
            if (rel.StartsWith("Assets/", StringComparison.Ordinal))
                rel = rel.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, rel));
        }

        /// <summary>Reimport FBXs already sitting in owner-drops — recovers from the
        /// Refresh race that left takes at zero on the first import attempt.</summary>
        private void ReimportOwnerDrops()
        {
            EnsureOwnerDropsFolder();
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { OwnerDropsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Motion Caster — reimport drops",
                    $"No FBX files in {OwnerDropsFolder}.\n\nUse 'Import dropped FBX…' first.",
                    "OK");
                return;
            }

            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Motion Caster",
                        $"Reimporting {Path.GetFileName(paths[i])}…", (float)i / paths.Count);
                    AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
                    WaitForImport(paths[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ScanLibrary();
            LoadPickerSources();
            string last = paths[paths.Count - 1];
            var pick = SelectBestTakeFromPath(last, out int takes, out int junk);
            string summary = takes == 0
                ? $"Reimported {paths.Count} FBX(s) in owner-drops but found 0 animation takes."
                : $"Reimported {paths.Count} FBX(s) — {takes} take(s), {junk} junk — " +
                  $"selected '{(pick != null ? pick.Clip.name : "none")}'.";
            Debug.Log(Log + summary);
            ShowNotification(new GUIContent(summary));
            if (takes == 0)
                EditorUtility.DisplayDialog("Motion Caster — no takes", summary, "OK");
        }

        /// <summary>Pick the longest real take for <paramref name="assetPath"/> and
        /// select it in the library (loads sub-assets directly if the scan index lags).</summary>
        private ClipEntry SelectBestTakeFromPath(string assetPath, out int takes, out int junk)
        {
            takes = 0;
            junk = 0;
            ClipEntry best = null, bestAny = null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is not AnimationClip clip ||
                    clip.name.StartsWith("__preview", StringComparison.Ordinal)) continue;
                takes++;
                bool junkTake = IsJunkTake(clip);
                if (junkTake) junk++;

                ClipEntry entry = null;
                foreach (var e in _library)
                {
                    if (e.Clip == clip && string.Equals(e.Path, assetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = e;
                        break;
                    }
                }
                if (entry == null)
                {
                    entry = new ClipEntry
                    {
                        Clip = clip,
                        Path = assetPath,
                        Source = "action",
                        NeedsExtraction = assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase),
                        Category = GuessCategory(clip.name),
                        Label = junkTake
                            ? $"{clip.name}  ({clip.length:0.00}s)  [SKIP: T-POSE/BIND]  [action]"
                            : $"{clip.name}  ({clip.length:0.00}s)  [action]",
                        JunkTake = junkTake,
                    };
                    _library.Add(entry);
                    _library.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
                }

                if (bestAny == null || clip.length > bestAny.Clip.length) bestAny = entry;
                if (!junkTake && (best == null || clip.length > best.Clip.length)) best = entry;
            }

            var pick = best ?? bestAny;
            if (pick != null)
            {
                _search = string.Empty;
                _chipIndex = 0;
                SelectEntry(pick);
            }
            return pick;
        }

        /// <summary>Run the controller bake that consumes motion-castings for this target.</summary>
        private static void RebakeForTarget(string target, string keyword)
        {
            string t = (target ?? string.Empty).ToLowerInvariant();
            string kw = (keyword ?? string.Empty).ToLowerInvariant();
            bool locomotion = kw is "idle" or "walk" or "run" or "combatidle" or "combatwalk"
                or "combatrun" or "injuredidle" or "injuredwalk" or "injuredrun";

            try
            {
                if (t.StartsWith("knight", StringComparison.Ordinal))
                {
                    if (locomotion)
                        HeroAnimatorFactory.BuildKnightMocapController();
                    else
                        KnightPackageControllerBuilder.Build();
                }
                else if (t.StartsWith("orc", StringComparison.Ordinal))
                {
                    BuildOrcHumanoidController.Run();
                }
                else
                {
                    AnimatorSetup.BuildAnimators();
                }

                EditorUtility.DisplayDialog("Motion Caster — rebake done",
                    $"Rebaked controller for '{target}' ({keyword}).\n\nPlay mode / Windows build " +
                    "will pick it up after the usual save cycle.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError(Log + $"rebake failed for '{target}.{keyword}': {ex}");
                EditorUtility.DisplayDialog("Motion Caster — rebake failed",
                    ex.Message, "OK");
            }
        }

        /// <summary>Metres the clip's root travels t0→tEnd. Humanoid clips expose
        /// the baked root velocity (averageSpeed); generic/legacy clips are sampled
        /// on a throwaway GO. Travel &gt; threshold = will slide/reset in-game.</summary>
        private static float ComputeRootTravel(AnimationClip clip)
        {
            if (clip == null || clip.length <= 0f) return 0f;

            if (clip.isHumanMotion)
            {
                float travel = clip.averageSpeed.magnitude * clip.length;
                if (travel > 0.001f) return travel;
            }

            var temp = new GameObject("__MotionCasterRootProbe")
                { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                clip.SampleAnimation(temp, 0f);
                Vector3 p0 = temp.transform.position;
                clip.SampleAnimation(temp, clip.length);
                return (temp.transform.position - p0).magnitude;
            }
            finally
            {
                DestroyImmediate(temp);
            }
        }
    }
}
