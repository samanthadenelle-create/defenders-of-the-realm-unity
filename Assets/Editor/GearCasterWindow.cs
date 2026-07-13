// =============================================================================
// GearCasterWindow -- standalone weapons + armor imaging / offset / curation booth
// (owner ask 2026-07-12: "an imaging tool for all weapons and armor with tabs for
// sword/axe/bow/shield with an offset tool ... pulls all items of the armor and
// weapons (the 446) into a single list").
//
// Defenders > Gear > Gear Caster
//
// Library (left): EVERY weapon + armor row read straight from the canonical JSON
//   (Assets/StreamingAssets/Data/Canonical/weapons.json + armor.json). One unified
//   list; each row = id + displayName + type. TABS filter by the weapon `category`
//   field: All / Sword / Axe / Bow / Shield / Armor. Two extra filter toggles:
//   "Included only" (the curated set) and "Needs PNG only" (art-gap worklist).
//   Per-row TEXT flags (owner is red/green colorblind -- never hue-only):
//     [IN]        -> included in the curated set
//     [OUT]       -> excluded from the curated set
//     [NEEDS PNG] -> no usable icon/preview sprite resolves yet
//
// Preview (right): PreviewRenderUtility stage (mirrors VfxCasterWindow /
//   OffsetForgeWindow). If the item resolves a prefab it renders in 3D with the
//   authored offset applied (WYSIWYG); otherwise it draws the item's icon sprite;
//   otherwise a "no model / NEEDS PNG" placeholder.
//
// Offset tool: position / rotation / scale sliders that persist through the SAME
//   local-offset store the gear system already uses -- OffsetForge.OffsetTableIO ->
//   Assets/OffsetForge/offsets.json. The OffsetForgeMirrorSync postprocessor then
//   mirrors it into Resources and AttachmentOffsetRegistry reads it at equip time,
//   so a dialed offset sticks exactly like the Offset Forge / Seating Editor. No
//   parallel offset store is invented.
//
// Curation overlay: the per-item include/exclude checkbox + assigned-PNG path are
//   written to Assets/Editor/GearCurationPicks.json (row: { id, included, iconPath })
//   -- the same manual-overlay pattern the VFX Caster uses (VfxManualPicks.json).
//
// Cross-assembly rule: DeNelle.Editor does NOT reference DeNelle.Village. The gear
//   catalog is read from JSON directly (JsonUtility into editor-local mirror types),
//   NOT via GearCatalog. The only new asmdef reference added is OffsetForge.Runtime
//   (a game-agnostic package) so offsets persist through the existing store.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using OffsetForge;

namespace DeNelle.Editor
{
    /// <summary>Standalone weapon/armor imaging + offset + curation window.</summary>
    public sealed class GearCasterWindow : EditorWindow
    {
        private const string Log = "[GearCaster] ";
        private const string WeaponsPath  = "Assets/StreamingAssets/Data/Canonical/weapons.json";
        private const string ArmorPath    = "Assets/StreamingAssets/Data/Canonical/armor.json";
        private const string OffsetsPath  = "Assets/OffsetForge/offsets.json";
        private const string CurationPath = "Assets/Editor/GearCurationPicks.json";

        // ── JSON mirror types (JsonUtility ignores unknown fields) ───────────
        [Serializable] private sealed class GearRow
        {
            public string id;
            public string name;
            public string icon;       // emoji placeholder (NOT a PNG)
            public string category;   // weapons only: sword/axe/bow/shield/...
            public string weight;     // armor only: light/heavy/any
            public string job;
            public string rarity;
            public string iconPath;   // authored Resources sprite path (usually empty)
            public string prefabPath; // equippable model path (usually empty)
        }
        [Serializable] private sealed class WeaponFile { public List<GearRow> weapons; }
        [Serializable] private sealed class ArmorFile  { public List<GearRow> armor; }

        // ── Curation overlay types ───────────────────────────────────────────
        [Serializable] private sealed class CurationRow
        {
            public string id;
            public bool included;
            public string iconPath; // owner-assigned PNG (project asset or Resources path)
        }
        [Serializable] private sealed class CurationFile { public List<CurationRow> picks = new List<CurationRow>(); }

        // ── Unified library entry ────────────────────────────────────────────
        private sealed class Entry
        {
            public string Id;
            public string DisplayName;
            public string Type;        // "sword"/"axe"/.../"armor"
            public bool IsArmor;
            public string IconPath;    // authored icon path from JSON
            public string PrefabPath;  // authored model path from JSON
            public string Emoji;       // placeholder glyph
            public string Rarity;
            public string Job;
        }

        private static readonly string[] Tabs = { "All", "Sword", "Axe", "Bow", "Shield", "Armor" };

        // ── Library state ────────────────────────────────────────────────────
        private List<Entry> _library = new List<Entry>();
        private Entry _selected;
        private string _search = string.Empty;
        private int _tab;                // index into Tabs (loaded in OnEnable)
        private bool _includedOnly;
        private bool _needsPngOnly;
        private Vector2 _libScroll;

        // ── Curation overlay (id -> row) ─────────────────────────────────────
        private Dictionary<string, CurationRow> _curation =
            new Dictionary<string, CurationRow>(StringComparer.OrdinalIgnoreCase);

        // ── Preview stage ────────────────────────────────────────────────────
        private PreviewRenderUtility _preview;
        private GameObject _instance;    // 3D model clone (when a prefab resolves)
        private Sprite _iconSprite;      // 2D icon (when no model but an icon resolves)
        private float _camYaw = 30f;
        private float _camPitch = 15f;
        private float _camDistance = 3f;
        private Vector3 _camPivot = Vector3.zero;
        private bool _framed;

        // ── Offset tool (persisted through OffsetForge store) ────────────────
        private Vector3 _rotation = Vector3.zero;   // euler degrees
        private Vector3 _position = Vector3.zero;   // local position
        private float _scale = 1f;
        private bool _fullOverride;
        private string _saveId = string.Empty;      // defaults to the item id, editable

        private Vector2 _infoScroll;

        [MenuItem("Defenders/Gear/Gear Caster")]
        public static void Open()
        {
            var w = GetWindow<GearCasterWindow>("Gear Caster");
            w.minSize = new Vector2(900f, 560f);
        }

        private void OnEnable()
        {
            _tab = EditorPrefs.GetInt("GearCaster.Tab", 0);
            _includedOnly = EditorPrefs.GetBool("GearCaster.IncludedOnly", false);
            _needsPngOnly = EditorPrefs.GetBool("GearCaster.NeedsPngOnly", false);
            LoadCuration();
            ScanLibrary();
        }

        private void OnDisable()
        {
            DestroyInstance();
            if (_preview != null) { _preview.Cleanup(); _preview = null; }
        }

        // ── Library scan (JSON only -- no DeNelle.Village reference) ──────────

        private void ScanLibrary()
        {
            _library = new List<Entry>();
            _selected = null;

            int nWeapons = 0, nArmor = 0;

            var weaponFile = ReadJson<WeaponFile>(WeaponsPath, "weapons.json");
            if (weaponFile != null && weaponFile.weapons != null)
            {
                foreach (var w in weaponFile.weapons)
                {
                    if (w == null || string.IsNullOrEmpty(w.id)) continue;
                    string type = string.IsNullOrEmpty(w.category) ? InferWeaponType(w) : w.category.Trim().ToLowerInvariant();
                    _library.Add(new Entry
                    {
                        Id = w.id,
                        DisplayName = string.IsNullOrEmpty(w.name) ? w.id : w.name,
                        Type = type,
                        IsArmor = false,
                        IconPath = w.iconPath,
                        PrefabPath = w.prefabPath,
                        Emoji = w.icon,
                        Rarity = w.rarity,
                        Job = w.job,
                    });
                    nWeapons++;
                }
            }

            var armorFile = ReadJson<ArmorFile>(ArmorPath, "armor.json");
            if (armorFile != null && armorFile.armor != null)
            {
                foreach (var a in armorFile.armor)
                {
                    if (a == null || string.IsNullOrEmpty(a.id)) continue;
                    _library.Add(new Entry
                    {
                        Id = a.id,
                        DisplayName = string.IsNullOrEmpty(a.name) ? a.id : a.name,
                        Type = "armor",
                        IsArmor = true,
                        IconPath = a.iconPath,
                        PrefabPath = a.prefabPath,
                        Emoji = a.icon,
                        Rarity = a.rarity,
                        Job = a.job,
                    });
                    nArmor++;
                }
            }

            _library.Sort((x, y) => string.CompareOrdinal(x.Type + x.DisplayName, y.Type + y.DisplayName));
            Debug.Log(Log + $"loaded {nWeapons} weapons + {nArmor} armor = {_library.Count} items into the unified list.");
        }

        // Weapon has no `category` field -> heuristic from id/name (flagged in report).
        private static string InferWeaponType(GearRow w)
        {
            string key = ((w.id ?? "") + " " + (w.name ?? "")).ToLowerInvariant();
            if (Has(key, "sword", "blade", "saber", "sabre", "claymore", "greatsword", "longsword")) return "sword";
            if (Has(key, "axe", "hatchet", "cleaver")) return "axe";
            if (Has(key, "bow", "longbow", "shortbow", "recurve")) return "bow";
            if (Has(key, "shield", "buckler", "aegis")) return "shield";
            if (Has(key, "staff", "wand", "scepter", "rod")) return "staff";
            if (Has(key, "dagger", "knife", "dirk")) return "dagger";
            if (Has(key, "mace", "hammer", "maul")) return "mace";
            return "other"; // reachable via the All tab
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static T ReadJson<T>(string path, string label) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning(Log + $"{label} not found at '{path}'.");
                    return null;
                }
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Log + $"failed to read {label}: {ex.Message}");
                return null;
            }
        }

        // ── Curation overlay load / save ─────────────────────────────────────

        private void LoadCuration()
        {
            _curation = new Dictionary<string, CurationRow>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(CurationPath)) return;
                var file = JsonUtility.FromJson<CurationFile>(File.ReadAllText(CurationPath));
                if (file == null || file.picks == null) return;
                foreach (var row in file.picks)
                {
                    if (row == null || string.IsNullOrEmpty(row.id)) continue;
                    _curation[row.id] = row;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Log + $"curation load failed: {ex.Message} -- starting empty.");
            }
        }

        private void SaveCuration()
        {
            try
            {
                var file = new CurationFile();
                foreach (var kv in _curation)
                    file.picks.Add(kv.Value);
                file.picks.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                string dir = Path.GetDirectoryName(CurationPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(CurationPath, JsonUtility.ToJson(file, true));
                AssetDatabase.ImportAsset(CurationPath, ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Log + $"curation save failed: {ex.Message}");
            }
        }

        private CurationRow GetOrCreateRow(string id)
        {
            if (!_curation.TryGetValue(id, out var row))
            {
                row = new CurationRow { id = id, included = false, iconPath = string.Empty };
                _curation[id] = row;
            }
            return row;
        }

        private bool IsIncluded(string id)
        {
            return _curation.TryGetValue(id, out var row) && row.included;
        }

        // A usable PNG exists when the authored iconPath resolves, OR the owner has
        // assigned a PNG in the curation overlay that resolves to a real asset/sprite.
        private bool HasUsablePng(Entry e)
        {
            if (e == null) return false;
            if (ResolveSpritePath(e.IconPath) != null) return true;
            if (_curation.TryGetValue(e.Id, out var row) && ResolveSpritePath(row.iconPath) != null) return true;
            return false;
        }

        // Resolve a sprite from either a project asset path (Assets/.../foo.png) or a
        // Resources-relative path (no extension). Returns null when nothing resolves.
        private static Sprite ResolveSpritePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                return null;
            }
            return Resources.Load<Sprite>(path);
        }

        // ── Selection / preview instance ─────────────────────────────────────

        private void SelectEntry(Entry entry)
        {
            _selected = entry;
            _saveId = entry != null ? entry.Id : string.Empty;
            ClearOffsetCell();
            LoadSavedOffsetForCurrentId();
            RebuildInstance();
        }

        private void RebuildInstance()
        {
            DestroyInstance();
            if (_selected == null) return;

            EnsurePreview();

            // 1) Prefer a 3D model (WYSIWYG offset preview).
            var prefab = ResolvePrefab(_selected.PrefabPath);
            if (prefab != null)
            {
                try
                {
                    _instance = Instantiate(prefab);
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    _preview.AddSingleGO(_instance);
                    _framed = false;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(Log + $"instantiate '{_selected.PrefabPath}' failed: {ex.Message}");
                    _instance = null;
                }
            }

            // 2) Otherwise resolve an icon sprite for the imaging pane.
            if (_instance == null)
            {
                _iconSprite = ResolveSpritePath(_selected.IconPath);
                if (_iconSprite == null && _curation.TryGetValue(_selected.Id, out var row))
                    _iconSprite = ResolveSpritePath(row.iconPath);
            }
        }

        private static GameObject ResolvePrefab(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return Resources.Load<GameObject>(path);
        }

        private void EnsurePreview()
        {
            if (_preview != null) return;
            _preview = new PreviewRenderUtility();
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.20f, 0.20f, 0.23f, 1f);
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 1000f;
            _preview.camera.fieldOfView = 40f;
            if (_preview.lights != null && _preview.lights.Length > 0)
            {
                _preview.lights[0].intensity = 1.1f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                if (_preview.lights.Length > 1) _preview.lights[1].intensity = 0.6f;
            }
            _preview.ambientColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        }

        private void DestroyInstance()
        {
            if (_instance != null) DestroyImmediate(_instance);
            _instance = null;
            _iconSprite = null;
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
                    DrawInfoAndOffset();
                }
            }
        }

        private void DrawLibraryColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(360f), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Gear Library ({_library.Count})", EditorStyles.boldLabel);
                    if (GUILayout.Button("Rescan", GUILayout.Width(60f))) { LoadCuration(); ScanLibrary(); }
                }

                int newTab = GUILayout.Toolbar(_tab, Tabs);
                if (newTab != _tab) { _tab = newTab; EditorPrefs.SetInt("GearCaster.Tab", _tab); }

                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool inc = EditorGUILayout.ToggleLeft("Included only", _includedOnly, GUILayout.Width(120f));
                    if (inc != _includedOnly) { _includedOnly = inc; EditorPrefs.SetBool("GearCaster.IncludedOnly", inc); }
                    bool png = EditorGUILayout.ToggleLeft("Needs PNG only", _needsPngOnly, GUILayout.Width(130f));
                    if (png != _needsPngOnly) { _needsPngOnly = png; EditorPrefs.SetBool("GearCaster.NeedsPngOnly", png); }
                }

                int shown = 0;
                using (var scroll = new EditorGUILayout.ScrollViewScope(_libScroll, GUILayout.ExpandHeight(true)))
                {
                    _libScroll = scroll.scrollPosition;
                    foreach (var e in _library)
                    {
                        if (!PassesTab(e)) continue;
                        bool included = IsIncluded(e.Id);
                        if (_includedOnly && !included) continue;
                        bool needsPng = !HasUsablePng(e);
                        if (_needsPngOnly && !needsPng) continue;
                        if (!string.IsNullOrEmpty(_search) &&
                            e.DisplayName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                            e.Id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        shown++;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Include/exclude checkbox (persisted to the curation overlay).
                            bool newInc = EditorGUILayout.Toggle(included, GUILayout.Width(18f));
                            if (newInc != included)
                            {
                                GetOrCreateRow(e.Id).included = newInc;
                                SaveCuration();
                            }

                            string flag = (newInc ? "[IN] " : "[OUT] ") + (needsPng ? "[NEEDS PNG] " : "");
                            string label = $"{flag}{e.DisplayName}  ({e.Id} / {e.Type})";
                            bool isSel = ReferenceEquals(e, _selected);
                            if (GUILayout.Button(label, isSel ? EditorStyles.boldLabel : EditorStyles.label))
                                SelectEntry(e);
                        }
                    }
                }
                EditorGUILayout.LabelField($"Shown: {shown}", EditorStyles.miniLabel);
            }
        }

        private bool PassesTab(Entry e)
        {
            switch (Tabs[Mathf.Clamp(_tab, 0, Tabs.Length - 1)])
            {
                case "All":    return true;
                case "Sword":  return !e.IsArmor && e.Type == "sword";
                case "Axe":    return !e.IsArmor && e.Type == "axe";
                case "Bow":    return !e.IsArmor && e.Type == "bow";
                case "Shield": return !e.IsArmor && e.Type == "shield";
                case "Armor":  return e.IsArmor;
                default:       return true;
            }
        }

        private void DrawPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(256f, 300f, GUILayout.ExpandWidth(true));

            var ev = Event.current;
            if (rect.Contains(ev.mousePosition))
            {
                if (ev.type == EventType.ScrollWheel)
                {
                    _camDistance = Mathf.Clamp(_camDistance * (1f + ev.delta.y * 0.05f), 0.1f, 500f);
                    ev.Use(); Repaint();
                }
                else if (ev.type == EventType.MouseDrag && ev.button == 0)
                {
                    _camYaw += ev.delta.x * 0.5f;
                    _camPitch = Mathf.Clamp(_camPitch + ev.delta.y * 0.5f, -89f, 89f);
                    ev.Use(); Repaint();
                }
            }

            if (_selected == null)
            {
                EditorGUI.HelpBox(rect, "Pick a weapon or armor from the library to image it.", MessageType.Info);
                return;
            }

            if (ev.type != EventType.Repaint) return;

            if (_instance != null && _preview != null)
            {
                try
                {
                    ApplyOffsetToInstance();
                    if (!_framed) FrameCamera();
                    PositionCamera();
                    _preview.BeginPreview(rect, GUIStyle.none);
                    _preview.Render(true, false);
                    GUI.DrawTexture(rect, _preview.EndPreview(), ScaleMode.StretchToFill, false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(Log + $"3D preview failed: {ex.Message}");
                    EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));
                }
            }
            else if (_iconSprite != null && _iconSprite.texture != null)
            {
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));
                var tr = _iconSprite.textureRect;
                var tex = _iconSprite.texture;
                var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
                float side = Mathf.Min(rect.width, rect.height) * 0.8f;
                var dst = new Rect(rect.center.x - side * 0.5f, rect.center.y - side * 0.5f, side, side);
                GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));
                var prev = GUI.color;
                GUI.color = new Color(0.75f, 0.75f, 0.75f);
                GUI.Label(rect, "No model, NEEDS PNG (glyph: " + (_selected.Emoji ?? "") + ")",
                    EditorStyles.centeredGreyMiniLabel);
                GUI.color = prev;
            }
        }

        private void FrameCamera()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool has = false;
            if (_instance != null)
            {
                foreach (var r in _instance.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
                }
            }
            if (has)
            {
                _camPivot = bounds.center;
                _camDistance = Mathf.Max(0.2f, bounds.extents.magnitude) * 2.5f;
            }
            else { _camPivot = Vector3.zero; _camDistance = 3f; }
            _framed = true;
        }

        private void PositionCamera()
        {
            Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            _preview.camera.transform.position = _camPivot - (rot * Vector3.forward) * _camDistance;
            _preview.camera.transform.rotation = rot;
        }

        private void ApplyOffsetToInstance()
        {
            if (_instance == null) return;
            _instance.transform.localRotation = Quaternion.Euler(_rotation);
            _instance.transform.localPosition = _position;
            _instance.transform.localScale = Vector3.one * (_scale <= 0f ? 1f : _scale);
        }

        private void DrawInfoAndOffset()
        {
            if (_selected == null) return;

            using (var scroll = new EditorGUILayout.ScrollViewScope(_infoScroll))
            {
                _infoScroll = scroll.scrollPosition;

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Item", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{_selected.DisplayName}   ({_selected.Id})", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Type: {_selected.Type}   Rarity: {_selected.Rarity ?? "-"}   Job: {_selected.Job ?? "-"}",
                    EditorStyles.miniLabel);

                bool included = IsIncluded(_selected.Id);
                bool needsPng = !HasUsablePng(_selected);
                EditorGUILayout.LabelField(
                    "Curated: " + (included ? "[IN]" : "[OUT]") + "     Art: " + (needsPng ? "[NEEDS PNG]" : "[HAS PNG]"),
                    EditorStyles.boldLabel);

                DrawCurationBlock();
                EditorGUILayout.Space(6f);
                DrawOffsetBlock();
            }
        }

        private void DrawCurationBlock()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Curation (Assets/Editor/GearCurationPicks.json)", EditorStyles.boldLabel);

            bool included = IsIncluded(_selected.Id);
            bool newInc = EditorGUILayout.ToggleLeft("Include this item in the curated set", included);
            if (newInc != included)
            {
                GetOrCreateRow(_selected.Id).included = newInc;
                SaveCuration();
            }

            // Assign PNG: pick a Sprite/Texture asset; its path is recorded into the overlay.
            var row = _curation.TryGetValue(_selected.Id, out var r) ? r : null;
            string current = row != null ? row.iconPath : string.Empty;
            var currentSprite = ResolveSpritePath(current);

            EditorGUILayout.LabelField("Assign PNG (records icon path into the overlay):", EditorStyles.miniLabel);
            var picked = (Sprite)EditorGUILayout.ObjectField("Icon sprite", currentSprite, typeof(Sprite), false);
            if (picked != currentSprite)
            {
                string path = picked != null ? AssetDatabase.GetAssetPath(picked) : string.Empty;
                GetOrCreateRow(_selected.Id).iconPath = path;
                SaveCuration();
                RebuildInstance();
            }
            EditorGUILayout.LabelField("Assigned path: " + (string.IsNullOrEmpty(current) ? "(none)" : current),
                EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_selected.IconPath))
                EditorGUILayout.LabelField("Authored iconPath: " + _selected.IconPath, EditorStyles.miniLabel);
        }

        private void DrawOffsetBlock()
        {
            EditorGUILayout.LabelField("Offset (persists via OffsetForge -> Assets/OffsetForge/offsets.json)",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Rotation (euler degrees)");
            _rotation.x = EditorGUILayout.Slider("X", _rotation.x, -180f, 180f);
            _rotation.y = EditorGUILayout.Slider("Y", _rotation.y, -180f, 180f);
            _rotation.z = EditorGUILayout.Slider("Z", _rotation.z, -180f, 180f);

            EditorGUILayout.LabelField("Position (metres)");
            _position.x = EditorGUILayout.Slider("X", _position.x, -0.5f, 0.5f);
            _position.y = EditorGUILayout.Slider("Y", _position.y, -0.5f, 0.5f);
            _position.z = EditorGUILayout.Slider("Z", _position.z, -0.5f, 0.5f);

            _scale = EditorGUILayout.FloatField("Scale", _scale);
            if (_scale <= 0f) _scale = 1f;
            _fullOverride = EditorGUILayout.ToggleLeft("Full override (absolute in-hand delta)", _fullOverride);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Offset", GUILayout.Width(120f)))
                {
                    ClearOffsetCell();
                    Repaint();
                }
                if (GUILayout.Button("Frame", GUILayout.Width(80f)))
                {
                    _framed = false;
                    Repaint();
                }
            }

            _saveId = EditorGUILayout.TextField("Save id (key)", _saveId);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_saveId)))
            {
                if (GUILayout.Button("Save Offset"))
                    SaveOffset();
            }
            EditorGUILayout.LabelField(
                "Saved under the item id; the runtime reads the same offsets.json via AttachmentOffsetRegistry " +
                "(mirror-synced to Resources), so a dialed offset sticks like the Seating Editor.",
                EditorStyles.miniLabel);
        }

        private void ClearOffsetCell()
        {
            _rotation = Vector3.zero;
            _position = Vector3.zero;
            _scale = 1f;
            _fullOverride = false;
        }

        // Round-trip: load this id's own saved entry from offsets.json (if present).
        private void LoadSavedOffsetForCurrentId()
        {
            if (string.IsNullOrEmpty(_saveId) || !File.Exists(OffsetsPath)) return;
            try
            {
                var table = OffsetTableIO.Load(File.ReadAllText(OffsetsPath));
                var e = table != null ? table.Find(_saveId) : null;
                if (e == null) return;
                _rotation = new Vector3(e.rot.x, e.rot.y, e.rot.z);
                _position = new Vector3(e.pos.x, e.pos.y, e.pos.z);
                _scale = e.scale > 0f ? e.scale : 1f;
                _fullOverride = e.fullOverride;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Log + $"load existing offset for '{_saveId}' failed: {ex.Message}");
            }
        }

        // Persist through the SAME store OffsetForge writes (offsets.json). The
        // OffsetForgeMirrorSync postprocessor mirrors it into Resources on import.
        private void SaveOffset()
        {
            if (string.IsNullOrEmpty(_saveId))
            {
                Debug.LogWarning(Log + "cannot save: id is empty.");
                return;
            }
            try
            {
                OffsetTable table = File.Exists(OffsetsPath)
                    ? OffsetTableIO.Load(File.ReadAllText(OffsetsPath))
                    : new OffsetTable();

                table.Upsert(new OffsetEntry
                {
                    id = _saveId,
                    rot = new Vec3(_rotation.x, _rotation.y, _rotation.z),
                    pos = new Vec3(_position.x, _position.y, _position.z),
                    scale = _scale <= 0f ? 1f : _scale,
                    fullOverride = _fullOverride,
                });

                string dir = Path.GetDirectoryName(OffsetsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(OffsetsPath, OffsetTableIO.ToJson(table));
                AssetDatabase.ImportAsset(OffsetsPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log(Log + $"saved offset id='{_saveId}' -> {OffsetsPath} (mirror will sync to Resources).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Log + $"save offset failed: {ex.Message}");
            }
        }
    }
}
