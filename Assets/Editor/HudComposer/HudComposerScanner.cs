// =============================================================================
// DeNelle.Editor.HudComposer.HudComposerScanner — project discovery for HUD Composer.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Pure AssetDatabase + reflection scan —
// holds NO runtime references, so it compiles BEFORE WO-541 Stage 1 lands. Every
// model is surfaced by STRING (type name) so a renamed/missing model is detectable
// rather than a hard compile error.
//
// Scans three buckets the HUD Composer binds together:
//   1. DATA SOURCES — the WO-541 Core HudModel records (first-class), plus project
//      types matching *VM / *Def / *Loadout / *Catalog and HUD-named ScriptableObjects.
//   2. SCREENS — prefabs whose name contains Panel / Hud / Modal / Screen.
//   3. 9-SLICE TEXTURES — textures with 9slice / Panel / Border / Frame in name or
//      path, flagged valid only when imported as a Sprite WITH a non-zero border.
//
// ASCII-only Debug.Log strings; [HUD Composer] tag throughout.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.HudComposer
{
    /// <summary>Kind of data source, for grouping + the View-reachability rule.</summary>
    public enum DataSourceKind
    {
        HudModelRecord,   // WO-541 Core record on CoreServices.HudModel (first-class)
        ViewModel,        // *VM
        Definition,       // *Def
        Loadout,          // *Loadout
        Catalog,          // *Catalog
        ScriptableObject  // HUD-named ScriptableObject asset/type
    }

    /// <summary>A bindable data source the dumb View can read from.</summary>
    public sealed class DataSourceInfo
    {
        public string DisplayName;       // grouped label for the dropdown
        public string FullTypeName;      // namespace-qualified; the saved key
        public string ShortName;         // type short name
        public string Namespace;
        public string AssemblyName;
        public DataSourceKind Kind;

        /// <summary>For HudModel records: the CoreServices.HudModel.&lt;Accessor&gt; property name.</summary>
        public string FacadeAccessor;

        /// <summary>True when a dumb View in DeNelle.HUD (refs Core only) can legally reference this type.</summary>
        public bool ReachableFromHud => Namespace != null && Namespace.StartsWith("DeNelle.Core", StringComparison.Ordinal);
    }

    /// <summary>A candidate HUD screen prefab.</summary>
    public sealed class ScreenInfo
    {
        public string Name;
        public string Path;
        public string Guid;
    }

    /// <summary>A candidate 9-slice background texture.</summary>
    public sealed class NineSliceInfo
    {
        public string Name;
        public string Path;
        public string Guid;
        public bool IsSprite;
        public Vector4 Border;
        public bool HasBorder => Border.sqrMagnitude > 0.0001f;
        public bool Valid => IsSprite && HasBorder;
    }

    public static class HudComposerScanner
    {
        // -- The frozen WO-541 model contract (WO541_MODEL_API.md). Surfaced as the --
        // -- first-class data sources even before the Core types exist on disk.      --
        // (record type name, CoreServices.HudModel accessor)
        private static readonly (string type, string accessor)[] HudModelRecords =
        {
            ("HeroVitalsModel",    "HeroVitals"),
            ("PartyModel",         "Party"),
            ("EconomyModel",       "Economy"),
            ("WaveModel",          "Wave"),
            ("TargetModel",        "Target"),
            ("TargetCycleModel",   "TargetCycle"),
            ("AbilityLoadoutModel","Abilities"),
            ("WorldMetricsModel",  "World"),
            ("MomentumModel",      "Momentum"),
            ("EchoModel",          "Echo"),
            ("HudContextModel",    "Context"),
        };

        private const string HudModelNamespace = "DeNelle.Core.HudModel";

        // =====================================================================
        // DATA SOURCES
        // =====================================================================
        public static List<DataSourceInfo> ScanDataSources()
        {
            var list = new List<DataSourceInfo>();
            var seen = new HashSet<string>();

            // 1. WO-541 HudModel records — ALWAYS first-class, whether or not the Core
            //    type exists yet (the contract is frozen). Reflection tells us if it has landed.
            foreach (var (type, accessor) in HudModelRecords)
            {
                string full = HudModelNamespace + "." + type;
                bool landed = ResolveType(full) != null;
                list.Add(new DataSourceInfo
                {
                    Kind = DataSourceKind.HudModelRecord,
                    ShortName = type,
                    FullTypeName = full,
                    Namespace = HudModelNamespace,
                    AssemblyName = "DeNelle.Core",
                    FacadeAccessor = accessor,
                    DisplayName = "[HudModel] " + type + (landed ? "" : "  (WO-541 pending)")
                });
                seen.Add(full);
            }

            // 2. Project types by name suffix, restricted to our own DeNelle.* assemblies.
            foreach (var t in EnumerateProjectTypes())
            {
                if (t == null || string.IsNullOrEmpty(t.Name)) continue;
                var ns = t.Namespace ?? "";
                if (!ns.StartsWith("DeNelle", StringComparison.Ordinal)) continue;

                DataSourceKind? kind = ClassifyBySuffix(t.Name);
                if (kind == null) continue;

                string full = string.IsNullOrEmpty(ns) ? t.Name : ns + "." + t.Name;
                if (!seen.Add(full)) continue;

                list.Add(new DataSourceInfo
                {
                    Kind = kind.Value,
                    ShortName = t.Name,
                    FullTypeName = full,
                    Namespace = ns,
                    AssemblyName = t.Assembly != null ? t.Assembly.GetName().Name : "",
                    DisplayName = "[" + KindTag(kind.Value) + "] " + t.Name + "  (" + ns + ")"
                });
            }

            list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return list;
        }

        private static DataSourceKind? ClassifyBySuffix(string name)
        {
            if (name.EndsWith("VM", StringComparison.Ordinal))       return DataSourceKind.ViewModel;
            if (name.EndsWith("Loadout", StringComparison.Ordinal))  return DataSourceKind.Loadout;
            if (name.EndsWith("Catalog", StringComparison.Ordinal))  return DataSourceKind.Catalog;
            if (name.EndsWith("Def", StringComparison.Ordinal))      return DataSourceKind.Definition;
            return null;
        }

        private static string KindTag(DataSourceKind k)
        {
            switch (k)
            {
                case DataSourceKind.ViewModel:        return "VM";
                case DataSourceKind.Definition:       return "Def";
                case DataSourceKind.Loadout:          return "Loadout";
                case DataSourceKind.Catalog:          return "Catalog";
                case DataSourceKind.ScriptableObject: return "SO";
                default:                              return "HudModel";
            }
        }

        // =====================================================================
        // SCREENS — prefabs named Panel / Hud / Modal / Screen
        // =====================================================================
        private static readonly string[] ScreenKeywords = { "Panel", "Hud", "Modal", "Screen" };

        public static List<ScreenInfo> ScanScreens()
        {
            var list = new List<ScreenInfo>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            if (guids == null) return list;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(file)) continue;
                if (!ScreenKeywords.Any(k => file.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                list.Add(new ScreenInfo { Name = file, Path = path, Guid = guid });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        // =====================================================================
        // 9-SLICE TEXTURES — name/path keyword + sprite-border probe
        // =====================================================================
        private static readonly string[] NineSliceKeywords = { "9slice", "9-slice", "Panel", "Border", "Frame" };

        public static List<NineSliceInfo> ScanNineSlices()
        {
            var list = new List<NineSliceInfo>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");
            if (guids == null) return list;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith("Packages/", StringComparison.Ordinal)) continue;   // project art only

                bool keyword = NineSliceKeywords.Any(k => path.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!keyword) continue;

                var info = new NineSliceInfo
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(path),
                    Path = path,
                    Guid = guid
                };

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    info.IsSprite = importer.textureType == TextureImporterType.Sprite;
                    info.Border = importer.spriteBorder;   // (L,B,R,T) in px; zero => not a 9-slice
                }
                list.Add(info);
            }

            // Valid 9-slices first, then by name.
            list.Sort((a, b) =>
            {
                if (a.Valid != b.Valid) return a.Valid ? -1 : 1;
                return string.CompareOrdinal(a.Name, b.Name);
            });
            return list;
        }

        // =====================================================================
        // Reflection helpers (guarded — ReflectionTypeLoadException safe)
        // =====================================================================
        public static Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            // Fast path: already-loaded by AQN-less full name across our assemblies.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); }
                catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        private static IEnumerable<Type> EnumerateProjectTypes()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name ?? "";
                if (!name.StartsWith("DeNelle", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { continue; }
                if (types == null) continue;
                foreach (var t in types)
                    if (t != null && t.IsClass) yield return t;
            }
        }
    }
}
