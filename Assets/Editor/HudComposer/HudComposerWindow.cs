// =============================================================================
// DeNelle.Editor.HudComposer.HudComposerWindow — first-party HUD authoring tool.
// -----------------------------------------------------------------------------
// Tools > DeNelle > HUD Composer. A dark, sectioned EditorWindow (mirrors VFX Parade /
// Offset Forge idioms: header, toolbar, scrolled sections, [HUD Composer] logging) for
// composing the game's HUD against the WO-541 One-Model architecture:
//
//   • auto-scans the project for DATA SOURCES (Core HudModel records first-class, plus
//     *VM / *Def / *Loadout / *Catalog / HUD ScriptableObjects), SCREEN prefabs, and
//     9-SLICE background textures;
//   • per screen: pick a CONTEXT (Town / Battle / Overworld / Modal => HudContextModel.
//     Context), a bound DATA SOURCE, and a 9-slice background — with live validation;
//   • saves the composition as a HudMappingAsset ScriptableObject;
//   • "Generate MVVM Stubs" emits a dumb View + thin VM + bootstrap per screen, bound to
//     CoreServices.HudModel.<accessor> and rendered per HudContextModel.Context;
//   • "Apply to Context" wires the screen->context map into the central context path
//     (or emits a manifest + TODO when WO-541 Stage 1 has not landed yet).
//
// Editor-only (DeNelle.Editor). Holds NO runtime references — models are scanned by
// name, so it compiles before WO-541 lands. ASCII-only Debug.Log strings.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.HudComposer
{
    public sealed class HudComposerWindow : EditorWindow
    {
        private const string LogTag = HudStubGenerator.LogTag;
        private const string PrefAssetGuid = "HudComposer.MappingAssetGuid";
        private const string HudContextModelType = "DeNelle.Core.HudModel.HudContextModel";

        private HudMappingAsset _asset;

        // Scanned project state (cached; Rescan refreshes).
        private List<DataSourceInfo> _dataSources = new List<DataSourceInfo>();
        private List<ScreenInfo> _screens = new List<ScreenInfo>();
        private List<NineSliceInfo> _nineSlices = new List<NineSliceInfo>();

        // Dropdown option caches (rebuilt on scan).
        private string[] _modelLabels = { "<none>" };
        private string[] _nineLabels = { "<none>" };

        private bool _showSources = true;
        private int _addScreenIndex;
        private Vector2 _scroll;

        // -- Section header style (lazy; EditorStyles unavailable at ctor time) ----
        private GUIStyle _sectionStyle;
        private GUIStyle SectionStyle =>
            _sectionStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

        // ---------------------------------------------------------------------
        [MenuItem("Tools/DeNelle/HUD Composer")]
        public static void Open()
        {
            var win = GetWindow<HudComposerWindow>("HUD Composer");
            win.minSize = new Vector2(560, 720);
            win.Show();
        }

        private void OnEnable()
        {
            TryRestoreAsset();
            Rescan();
        }

        // ---------------------------------------------------------------------
        // SCAN
        // ---------------------------------------------------------------------
        private void Rescan()
        {
            _dataSources = HudComposerScanner.ScanDataSources();
            _screens = HudComposerScanner.ScanScreens();
            _nineSlices = HudComposerScanner.ScanNineSlices();

            _modelLabels = BuildLabels(_dataSources.ConvertAll(d => d.DisplayName));
            var nine = new List<string>();
            foreach (var n in _nineSlices)
                nine.Add((n.Valid ? "OK  " : "??  ") + n.Name + "   (" + n.Path + ")");
            _nineLabels = BuildLabels(nine);

            Debug.Log($"{LogTag} scan: {_dataSources.Count} data source(s), {_screens.Count} screen(s), {_nineSlices.Count} 9-slice candidate(s).");
            Repaint();
        }

        private static string[] BuildLabels(List<string> items)
        {
            var arr = new string[items.Count + 1];
            arr[0] = "<none>";
            for (int i = 0; i < items.Count; i++) arr[i + 1] = items[i];
            return arr;
        }

        // ---------------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------------
        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSourceSummary();
            EditorGUILayout.Space(6);

            if (_asset == null)
            {
                EditorGUILayout.HelpBox(
                    "No HUD Mapping asset selected. Create one (toolbar) or pick an existing HudMappingAsset to begin composing.",
                    MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawOutputFolder();
            EditorGUILayout.Space(6);
            DrawMappings();
            EditorGUILayout.Space(6);
            DrawAddScreen();
            EditorGUILayout.Space(8);
            DrawValidation(out bool hasErrors);
            EditorGUILayout.Space(6);
            DrawActions(hasErrors);

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(10, 38, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.16f));
            var title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.92f, 0.86f, 0.66f) },
                padding = new RectOffset(10, 0, 8, 0)
            };
            GUI.Label(rect, "HUD Composer", title);
            var sub = new GUIStyle(EditorStyles.miniLabel) { padding = new RectOffset(12, 0, 24, 0) };
            GUI.Label(rect, "WO-541 One-Model  •  dumb Views over CoreServices.HudModel", sub);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Rescan();

                GUILayout.Space(8);
                EditorGUILayout.LabelField("Mapping", GUILayout.Width(56));
                EditorGUI.BeginChangeCheck();
                var picked = (HudMappingAsset)EditorGUILayout.ObjectField(_asset, typeof(HudMappingAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _asset = picked;
                    RememberAsset();
                }

                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    CreateNewAsset();

                using (new EditorGUI.DisabledScope(_asset == null))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        SaveAsset();
                }
                GUILayout.FlexibleSpace();
            }
        }

        // ---------------------------------------------------------------------
        // SOURCE SUMMARY (collapsible)
        // ---------------------------------------------------------------------
        private void DrawSourceSummary()
        {
            _showSources = EditorGUILayout.Foldout(_showSources,
                $"Discovered  —  {_dataSources.Count} data source(s), {_screens.Count} screen(s), {_nineSlices.Count} 9-slice(s)", true);
            if (!_showSources) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int models = 0, vms = 0, defs = 0;
                foreach (var d in _dataSources)
                {
                    if (d.Kind == DataSourceKind.HudModelRecord) models++;
                    else if (d.Kind == DataSourceKind.ViewModel) vms++;
                    else defs++;
                }
                EditorGUILayout.LabelField("Data sources", SectionStyle);
                EditorGUILayout.LabelField($"   HudModel records: {models}   •   ViewModels: {vms}   •   Def/Loadout/Catalog: {defs}", EditorStyles.miniLabel);

                int validNine = 0;
                foreach (var n in _nineSlices) if (n.Valid) validNine++;
                EditorGUILayout.LabelField("Backgrounds", SectionStyle);
                EditorGUILayout.LabelField($"   9-slice candidates: {_nineSlices.Count}   •   valid (sprite + border): {validNine}", EditorStyles.miniLabel);

                EditorGUILayout.LabelField("Screens", SectionStyle);
                EditorGUILayout.LabelField($"   prefabs matching Panel/Hud/Modal/Screen: {_screens.Count}", EditorStyles.miniLabel);

                if (HudComposerScanner.ResolveType(HudContextModelType) == null)
                    EditorGUILayout.HelpBox(
                        "WO-541 Stage 1 not detected (DeNelle.Core.HudModel.* missing). You can author + save mappings now; " +
                        "generated runtime stubs reference the One-Model API and will compile once Stage 1 lands.",
                        MessageType.Warning);
            }
        }

        private void DrawOutputFolder()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Output folder", GUILayout.Width(90));
                EditorGUI.BeginChangeCheck();
                string f = EditorGUILayout.TextField(_asset.outputFolder);
                if (EditorGUI.EndChangeCheck())
                {
                    _asset.outputFolder = f;
                    EditorUtility.SetDirty(_asset);
                }
            }
        }

        // ---------------------------------------------------------------------
        // MAPPINGS
        // ---------------------------------------------------------------------
        private void DrawMappings()
        {
            EditorGUILayout.LabelField("Screens", SectionStyle);

            if (_asset.mappings.Count == 0)
            {
                EditorGUILayout.HelpBox("No screens yet. Add one below.", MessageType.None);
                return;
            }

            int removeAt = -1;
            for (int i = 0; i < _asset.mappings.Count; i++)
            {
                var map = _asset.mappings[i];
                if (map == null) continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        string nm = EditorGUILayout.TextField("Screen", map.screenName);
                        var ctx = (HudComposerContext)EditorGUILayout.EnumPopup(map.context, GUILayout.Width(110));
                        if (EditorGUI.EndChangeCheck())
                        {
                            map.screenName = nm;
                            map.context = ctx;
                            EditorUtility.SetDirty(_asset);
                        }
                        if (GUILayout.Button("X", GUILayout.Width(24)))
                            removeAt = i;
                    }

                    // Data source dropdown.
                    int modelIdx = IndexOfModel(map.modelTypeName);
                    EditorGUI.BeginChangeCheck();
                    int newModelIdx = EditorGUILayout.Popup("Data source", modelIdx, _modelLabels);
                    if (EditorGUI.EndChangeCheck())
                    {
                        map.modelTypeName = newModelIdx <= 0 ? "" : _dataSources[newModelIdx - 1].FullTypeName;
                        EditorUtility.SetDirty(_asset);
                    }

                    // 9-slice dropdown.
                    int nineIdx = IndexOfNine(map.nineSliceGuid);
                    EditorGUI.BeginChangeCheck();
                    int newNineIdx = EditorGUILayout.Popup("9-slice bg", nineIdx, _nineLabels);
                    if (EditorGUI.EndChangeCheck())
                    {
                        map.nineSliceGuid = newNineIdx <= 0 ? "" : _nineSlices[newNineIdx - 1].Guid;
                        EditorUtility.SetDirty(_asset);
                    }

                    DrawMappingValidation(map);

                    if (!string.IsNullOrEmpty(map.generatedViewPath))
                        EditorGUILayout.LabelField("   generated: " + map.generatedViewPath, EditorStyles.miniLabel);
                }
            }

            if (removeAt >= 0)
            {
                _asset.mappings.RemoveAt(removeAt);
                EditorUtility.SetDirty(_asset);
            }
        }

        // Per-row inline validation hints.
        private void DrawMappingValidation(HudScreenMapping map)
        {
            var ds = FindSource(map.modelTypeName);

            if (string.IsNullOrEmpty(map.modelTypeName))
                EditorGUILayout.HelpBox("No data source bound.", MessageType.Warning);
            else if (ds == null)
                EditorGUILayout.HelpBox($"Data source '{map.modelTypeName}' not found (renamed/removed?). Re-pick it.", MessageType.Error);
            else if (!ds.ReachableFromHud)
                EditorGUILayout.HelpBox(
                    $"'{ds.ShortName}' lives in {ds.Namespace} — a dumb View in DeNelle.HUD cannot reference it. " +
                    "Surface it via a Core HudModel record (WO-541) or move the VM to DeNelle.Village.",
                    MessageType.Warning);

            if (!string.IsNullOrEmpty(map.nineSliceGuid))
            {
                var n = FindNine(map.nineSliceGuid);
                if (n == null)
                    EditorGUILayout.HelpBox("9-slice texture not found (moved/deleted?).", MessageType.Error);
                else if (!n.Valid)
                    EditorGUILayout.HelpBox(
                        $"'{n.Name}' is not a valid 9-slice (needs Sprite import type + a non-zero border).",
                        MessageType.Warning);
            }
        }

        // ---------------------------------------------------------------------
        // ADD SCREEN
        // ---------------------------------------------------------------------
        private void DrawAddScreen()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Blank screen", GUILayout.Width(120)))
                    AddMapping(new HudScreenMapping("NewScreen", HudComposerContext.Town));

                GUILayout.Space(12);

                if (_screens.Count > 0)
                {
                    var labels = new string[_screens.Count];
                    for (int i = 0; i < _screens.Count; i++) labels[i] = _screens[i].Name;
                    _addScreenIndex = Mathf.Clamp(_addScreenIndex, 0, _screens.Count - 1);
                    _addScreenIndex = EditorGUILayout.Popup(_addScreenIndex, labels);
                    if (GUILayout.Button("+ From prefab", GUILayout.Width(110)))
                    {
                        var s = _screens[_addScreenIndex];
                        var m = new HudScreenMapping(s.Name, GuessContext(s.Name)) { sourcePrefabGuid = s.Guid };
                        AddMapping(m);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("(no Panel/Hud/Modal/Screen prefabs found)", EditorStyles.miniLabel);
                }
            }
        }

        private static HudComposerContext GuessContext(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("battle") || n.Contains("combat") || n.Contains("wave")) return HudComposerContext.Battle;
            if (n.Contains("modal") || n.Contains("inventory") || n.Contains("shop") || n.Contains("gear")) return HudComposerContext.Modal;
            if (n.Contains("world") || n.Contains("overworld") || n.Contains("map")) return HudComposerContext.Overworld;
            return HudComposerContext.Town;
        }

        private void AddMapping(HudScreenMapping m)
        {
            _asset.mappings.Add(m);
            EditorUtility.SetDirty(_asset);
        }

        // ---------------------------------------------------------------------
        // VALIDATION PANEL
        // ---------------------------------------------------------------------
        private void DrawValidation(out bool hasErrors)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            var names = new HashSet<string>();
            foreach (var map in _asset.mappings)
            {
                if (map == null) continue;
                string id = HudMappingAsset.Sanitize(map.screenName);
                if (!names.Add(id))
                    errors.Add($"Duplicate screen identifier '{id}' (from '{map.screenName}') — would overwrite generated files.");

                var ds = FindSource(map.modelTypeName);
                if (string.IsNullOrEmpty(map.modelTypeName))
                    warnings.Add($"'{map.screenName}': no data source bound.");
                else if (ds == null)
                    errors.Add($"'{map.screenName}': data source '{map.modelTypeName}' not found.");
                else if (!ds.ReachableFromHud)
                    warnings.Add($"'{map.screenName}': source '{ds.ShortName}' not reachable from DeNelle.HUD.");

                if (!string.IsNullOrEmpty(map.nineSliceGuid))
                {
                    var n = FindNine(map.nineSliceGuid);
                    if (n == null) errors.Add($"'{map.screenName}': 9-slice texture missing.");
                    else if (!n.Valid) warnings.Add($"'{map.screenName}': '{n.Name}' is not a valid 9-slice (no border).");
                }
            }

            hasErrors = errors.Count > 0;

            EditorGUILayout.LabelField("Validation", SectionStyle);
            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("All mappings valid. Ready to generate.", MessageType.Info);
                return;
            }
            foreach (var e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
            foreach (var w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }

        // ---------------------------------------------------------------------
        // ACTIONS
        // ---------------------------------------------------------------------
        private void DrawActions(bool hasErrors)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(hasErrors || _asset.mappings.Count == 0))
                {
                    var gen = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontStyle = FontStyle.Bold };
                    if (GUILayout.Button("Generate MVVM Stubs", gen))
                        GenerateStubs();

                    if (GUILayout.Button("Apply to Context", gen, GUILayout.Width(150)))
                        ApplyToContext();
                }
            }
            if (hasErrors)
                EditorGUILayout.LabelField("Resolve the errors above before generating.", EditorStyles.miniLabel);
        }

        private void GenerateStubs()
        {
            if (_asset == null) return;

            bool landed = HudComposerScanner.ResolveType(HudContextModelType) != null;
            if (!landed)
            {
                bool go = EditorUtility.DisplayDialog("HUD Composer",
                    "WO-541 Stage 1 (DeNelle.Core.HudModel.*) is not present yet.\n\n" +
                    "The generated Views/VMs reference CoreServices.HudModel and HudContext — they will NOT compile " +
                    "until Stage 1 lands. Generate them now as TODO-flagged stubs anyway?",
                    "Generate anyway", "Cancel");
                if (!go) { Debug.Log($"{LogTag} generation cancelled (WO-541 not landed)."); return; }
            }

            SaveAsset();
            int n = 0;
            foreach (var map in _asset.mappings)
            {
                if (map == null) continue;
                var ds = FindSource(map.modelTypeName);
                if (HudStubGenerator.GenerateForMapping(_asset.outputFolder, map, ds) != null) n++;
            }
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LogTag} generated MVVM stubs for {n} screen(s) into {_asset.outputFolder}.");
            EditorUtility.DisplayDialog("HUD Composer", $"Generated MVVM stubs for {n} screen(s).\nFolder: {_asset.outputFolder}", "OK");
        }

        // Wire the screen->context map into the central context path. The HudContextModel
        // is written by ONE evaluator (HudContextEvaluator, WO-541) — the View self-gates on
        // it. This action records the authored intent as a data manifest the producer/evaluator
        // can consume, and logs the binding (owner-thinks-in-data-structures: data, not branches).
        private void ApplyToContext()
        {
            if (_asset == null) return;

            var manifest = new ContextManifest();
            foreach (var map in _asset.mappings)
            {
                if (map == null || string.IsNullOrEmpty(map.screenName)) continue;
                manifest.entries.Add(new ContextEntry
                {
                    screen = HudMappingAsset.Sanitize(map.screenName) + "PanelMvvm",
                    context = map.context.ToString(),
                    dataSource = map.modelTypeName
                });
            }

            string folder = string.IsNullOrEmpty(_asset.outputFolder) ? "Assets/_Modules/HUD/Generated" : _asset.outputFolder;
            EnsureFolderOnDisk(folder);
            string path = (folder.TrimEnd('/') + "/hud_context_manifest.json");
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
                AssetDatabase.ImportAsset(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} failed to write context manifest '{path}': {e.Message}");
            }

            bool landed = HudComposerScanner.ResolveType(HudContextModelType) != null;
            foreach (var e in manifest.entries)
                Debug.Log($"{LogTag} context binding: {e.screen} renders in HudContext.{e.context} (source {e.dataSource}).");

            if (landed)
                Debug.Log($"{LogTag} Apply to Context: views self-gate on HudContextModel.Context; the HudContextEvaluator is the ONE writer. Manifest -> {path}");
            else
                Debug.LogWarning($"{LogTag} Apply to Context: WO-541 HudContextModel not present yet. TODO: feed {path} to HudContextEvaluator when Stage 2 lands. (Views generated already self-gate on the documented API.)");

            EditorUtility.DisplayDialog("HUD Composer",
                $"Context map written to:\n{path}\n\nViews render per HudContextModel.Context; the central HudContextEvaluator (WO-541) is the single writer." +
                (landed ? "" : "\n\nNOTE: WO-541 Stage 1 not detected — TODO-flagged for the evaluator."),
                "OK");
        }

        // ---------------------------------------------------------------------
        // Lookup helpers
        // ---------------------------------------------------------------------
        private DataSourceInfo FindSource(string fullType)
        {
            if (string.IsNullOrEmpty(fullType)) return null;
            foreach (var d in _dataSources) if (d.FullTypeName == fullType) return d;
            return null;
        }

        private NineSliceInfo FindNine(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var n in _nineSlices) if (n.Guid == guid) return n;
            return null;
        }

        private int IndexOfModel(string fullType)
        {
            if (string.IsNullOrEmpty(fullType)) return 0;
            for (int i = 0; i < _dataSources.Count; i++)
                if (_dataSources[i].FullTypeName == fullType) return i + 1;
            return 0;   // not found -> <none> (validation flags the dangling name)
        }

        private int IndexOfNine(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return 0;
            for (int i = 0; i < _nineSlices.Count; i++)
                if (_nineSlices[i].Guid == guid) return i + 1;
            return 0;
        }

        // ---------------------------------------------------------------------
        // Asset lifecycle
        // ---------------------------------------------------------------------
        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New HUD Mapping", "HudMapping", "asset",
                "Choose where to save the HUD mapping asset.", "Assets");
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<HudMappingAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _asset = asset;
            RememberAsset();
            Debug.Log($"{LogTag} created mapping asset at {path}.");
        }

        private void SaveAsset()
        {
            if (_asset == null) return;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} saved mapping asset '{_asset.name}'.");
        }

        private void RememberAsset()
        {
            if (_asset == null) { EditorPrefs.DeleteKey(PrefAssetGuid); return; }
            string path = AssetDatabase.GetAssetPath(_asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid)) EditorPrefs.SetString(PrefAssetGuid, guid);
        }

        private void TryRestoreAsset()
        {
            if (_asset != null) return;
            string guid = EditorPrefs.GetString(PrefAssetGuid, "");
            if (string.IsNullOrEmpty(guid)) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                _asset = AssetDatabase.LoadAssetAtPath<HudMappingAsset>(path);
        }

        private static void EnsureFolderOnDisk(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolderOnDisk(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // -- Context manifest (JsonUtility) ------------------------------------
        [System.Serializable]
        private sealed class ContextManifest
        {
            public List<ContextEntry> entries = new List<ContextEntry>();
        }

        [System.Serializable]
        private sealed class ContextEntry
        {
            public string screen;
            public string context;
            public string dataSource;
        }
    }
}
