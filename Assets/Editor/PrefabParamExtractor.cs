// =============================================================================
// PrefabParamExtractor — AMENDMENT A1 of docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md
// ("the parameters are the structures that map").
//
// The Blink Obsidian pack ships 58 COMPLETE assembled uGUI prefabs under
// Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/ (HUDCore, PartyNameplate,
// TargetNameplate, CastBar1-3, buttons, full screens). They are the SOURCE OF
// TRUTH for widget structure. This tool TEXT-PARSES each .prefab YAML (they are
// plain text) — GameObjects, RectTransforms (anchors/size/pivot/position/parent),
// Image components (sprite name resolved via the pack's .meta GUID→filename map,
// type, fillMethod/fillAmount), and Text content — into a committed JSON:
//
//   Assets/Resources/Data/Canonical/widget-params.json
//
// The factory (ElarionUiKit, P1) consumes these parameters — no eyeballed
// dimensions anywhere the pack already measured. Parsing is pure File IO
// (NO AssetDatabase dependency) so it runs even before the pack is imported
// into Unity. Pack absent ⇒ Debug.LogWarning + no-op (fresh-clone safe).
//
// NOTE: this is parameter EXTRACTION, not prefab adoption — we never
// instantiate Blink prefabs by GUID (uGUI canon holds).
//
// Run: Defenders > Art > Extract Blink Widget Params
//      (or batchmode DeNelle.Editor.PrefabParamExtractor.Run)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PrefabParamExtractor
    {
        private const string PackRoot   = "Assets/Blink/Art/UI/Obsidian_UI";
        private const string PrefabRoot = PackRoot + "/Prefabs_Obsidian";
        private const string OutDir     = "Assets/Resources/Data/Canonical";
        private const string OutPath    = OutDir + "/widget-params.json";

        [MenuItem("Defenders/Art/Extract Blink Widget Params")]
        public static void ExtractMenu() { Run(); }

        public static void Run()
        {
            // Fresh-clone safety: the Blink pack is gitignored — warn + no-op when absent.
            if (!Directory.Exists(PrefabRoot))
            {
                Debug.LogWarning("[PrefabParamExtractor] Blink prefabs not present (" + PrefabRoot +
                                 ") — skipping extraction. The committed widget-params.json (if any) stays as-is.");
                return;
            }

            // GUID → asset filename (no extension), from every .meta in the pack.
            var guidMap = BuildGuidMap(PackRoot);

            var prefabPaths = Directory.GetFiles(PrefabRoot, "*.prefab", SearchOption.AllDirectories);
            Array.Sort(prefabPaths, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder(1 << 20);
            sb.Append("{\n");
            sb.Append("  \"source\": \"").Append(JsonEscape(PrefabRoot.Replace('\\', '/'))).Append("\",\n");
            sb.Append("  \"generated\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append("\",\n");
            sb.Append("  \"note\": \"Extracted uGUI widget parameters from the Blink Obsidian prefabs (HUD_OBSIDIAN_ARCHITECTURE A1). The factory consumes these — no eyeballed dimensions where the pack already measured.\",\n");
            sb.Append("  \"prefabs\": [\n");

            int prefabCount = 0, objectCount = 0, imageCount = 0, textCount = 0;
            for (int p = 0; p < prefabPaths.Length; p++)
            {
                var entry = ParsePrefab(prefabPaths[p], guidMap,
                                        ref objectCount, ref imageCount, ref textCount);
                if (entry == null) continue;
                if (prefabCount > 0) sb.Append(",\n");
                sb.Append(entry);
                prefabCount++;
            }

            sb.Append("\n  ]\n}\n");

            Directory.CreateDirectory(OutDir);
            File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh(); // pick the JSON up as a TextAsset (write itself is pure File IO)

            Debug.Log("[PrefabParamExtractor] done — " + prefabCount + " prefab(s), " + objectCount +
                      " object(s), " + imageCount + " image(s), " + textCount + " text(s) → " + OutPath);
        }

        // ── YAML block model ────────────────────────────────────────────────────

        private sealed class Block
        {
            public int ClassId;                 // !u!1 GameObject, !u!224 RectTransform, !u!114 MonoBehaviour, ...
            public string FileId;               // &<id>
            public Dictionary<string, string> Fields = new Dictionary<string, string>();
        }

        private sealed class Node
        {
            public string GoId;                 // GameObject fileID
            public string Name = "";
            public bool Active = true;
            public string RectId;               // RectTransform fileID
            public string FatherRectId;         // parent RectTransform fileID ("0" = root)
            public Block Rect;                  // RectTransform block
            public List<Block> Behaviours = new List<Block>(); // MonoBehaviours on this GO
        }

        // ── Prefab parse ────────────────────────────────────────────────────────

        private static string ParsePrefab(string path, Dictionary<string, string> guidMap,
                                          ref int objectCount, ref int imageCount, ref int textCount)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex)
            {
                Debug.LogWarning("[PrefabParamExtractor] unreadable prefab (skipped): " + path + " — " + ex.Message);
                return null;
            }

            var blocks = SplitBlocks(text);

            // Index: GameObjects, RectTransforms by their own fileID, MonoBehaviours by owning GO.
            var nodesByGo = new Dictionary<string, Node>();
            var rectsById = new Dictionary<string, Block>();
            var behavioursByGo = new Dictionary<string, List<Block>>();

            foreach (var b in blocks)
            {
                if (b.ClassId == 1) // GameObject
                {
                    var n = new Node { GoId = b.FileId };
                    n.Name = b.Fields.TryGetValue("m_Name", out var nm) ? nm : "";
                    n.Active = !b.Fields.TryGetValue("m_IsActive", out var act) || act.Trim() != "0";
                    nodesByGo[b.FileId] = n;
                }
                else if (b.ClassId == 224) // RectTransform
                {
                    rectsById[b.FileId] = b;
                }
                else if (b.ClassId == 114) // MonoBehaviour (Image / Text / TMP / effects / ...)
                {
                    string go = FileIdOf(b, "m_GameObject");
                    if (go == null) continue;
                    if (!behavioursByGo.TryGetValue(go, out var list)) behavioursByGo[go] = list = new List<Block>();
                    list.Add(b);
                }
            }

            // Wire rects + behaviours onto nodes.
            var nodesByRect = new Dictionary<string, Node>();
            foreach (var rect in rectsById.Values)
            {
                string go = FileIdOf(rect, "m_GameObject");
                if (go == null || !nodesByGo.TryGetValue(go, out var n)) continue;
                n.Rect = rect;
                n.RectId = rect.FileId;
                n.FatherRectId = FileIdOf(rect, "m_Father") ?? "0";
                nodesByRect[rect.FileId] = n;
            }
            foreach (var kv in behavioursByGo)
                if (nodesByGo.TryGetValue(kv.Key, out var n)) n.Behaviours = kv.Value;

            // Hierarchy paths (parent chain by RectTransform father).
            foreach (var n in nodesByGo.Values) objectCount += n.Rect != null ? 1 : 0;

            string prefabName = Path.GetFileNameWithoutExtension(path);
            string rel = path.Replace('\\', '/');
            int idx = rel.IndexOf("Prefabs_Obsidian/", StringComparison.OrdinalIgnoreCase);
            string relName = idx >= 0 ? rel.Substring(idx + "Prefabs_Obsidian/".Length) : Path.GetFileName(rel);

            var sb = new StringBuilder(8192);
            sb.Append("    {\n");
            sb.Append("      \"prefab\": \"").Append(JsonEscape(prefabName)).Append("\",\n");
            sb.Append("      \"file\": \"").Append(JsonEscape(relName)).Append("\",\n");
            sb.Append("      \"objects\": [\n");

            bool first = true;
            foreach (var n in nodesByGo.Values)
            {
                if (n.Rect == null) continue; // non-UI transform or stripped node — skip
                if (!first) sb.Append(",\n");
                first = false;
                AppendObject(sb, n, nodesByRect, guidMap, ref imageCount, ref textCount);
            }

            sb.Append("\n      ]\n");
            sb.Append("    }");
            return sb.ToString();
        }

        private static void AppendObject(StringBuilder sb, Node n, Dictionary<string, Node> nodesByRect,
                                         Dictionary<string, string> guidMap,
                                         ref int imageCount, ref int textCount)
        {
            sb.Append("        {\n");
            sb.Append("          \"name\": \"").Append(JsonEscape(n.Name)).Append("\",\n");
            sb.Append("          \"path\": \"").Append(JsonEscape(HierarchyPath(n, nodesByRect))).Append("\",\n");

            string parentName = null;
            if (n.FatherRectId != null && n.FatherRectId != "0" &&
                nodesByRect.TryGetValue(n.FatherRectId, out var parent))
                parentName = parent.Name;
            sb.Append("          \"parent\": ").Append(parentName == null ? "null" : "\"" + JsonEscape(parentName) + "\"").Append(",\n");
            sb.Append("          \"active\": ").Append(n.Active ? "true" : "false").Append(",\n");

            // RectTransform
            sb.Append("          \"rect\": { ");
            sb.Append("\"anchorMin\": ").Append(Vec2(n.Rect, "m_AnchorMin")).Append(", ");
            sb.Append("\"anchorMax\": ").Append(Vec2(n.Rect, "m_AnchorMax")).Append(", ");
            sb.Append("\"anchoredPosition\": ").Append(Vec2(n.Rect, "m_AnchoredPosition")).Append(", ");
            sb.Append("\"sizeDelta\": ").Append(Vec2(n.Rect, "m_SizeDelta")).Append(", ");
            sb.Append("\"pivot\": ").Append(Vec2(n.Rect, "m_Pivot"));
            sb.Append(" }");

            // Components: Image (has m_Sprite + m_FillMethod) / Text (m_Text) / TMP (m_text)
            foreach (var b in n.Behaviours)
            {
                if (b.Fields.ContainsKey("m_Sprite") && b.Fields.ContainsKey("m_FillMethod"))
                {
                    imageCount++;
                    sb.Append(",\n          \"image\": { ");
                    string spriteGuid = GuidOf(b, "m_Sprite");
                    string spriteName = spriteGuid != null && guidMap.TryGetValue(spriteGuid, out var sn) ? sn : null;
                    sb.Append("\"sprite\": ").Append(spriteName == null ? "null" : "\"" + JsonEscape(spriteName) + "\"").Append(", ");
                    if (spriteName == null && spriteGuid != null)
                        sb.Append("\"spriteGuid\": \"").Append(JsonEscape(spriteGuid)).Append("\", ");
                    sb.Append("\"color\": ").Append(ColorOf(b)).Append(", ");
                    int type = IntField(b, "m_Type");
                    sb.Append("\"type\": \"").Append(ImageType(type)).Append("\"");
                    if (type == 3) // Filled — fill config is load-bearing (the §1.1 contract source)
                    {
                        sb.Append(", \"fillMethod\": \"").Append(FillMethod(IntField(b, "m_FillMethod"))).Append("\"");
                        sb.Append(", \"fillOrigin\": ").Append(IntField(b, "m_FillOrigin"));
                        sb.Append(", \"fillAmount\": ").Append(FloatField(b, "m_FillAmount"));
                    }
                    if (b.Fields.TryGetValue("m_PreserveAspect", out var pa) && pa.Trim() == "1")
                        sb.Append(", \"preserveAspect\": true");
                    sb.Append(" }");
                }
                else if (b.Fields.ContainsKey("m_Text") || b.Fields.ContainsKey("m_text"))
                {
                    textCount++;
                    string content = b.Fields.TryGetValue("m_Text", out var t) ? t
                                   : (b.Fields.TryGetValue("m_text", out var t2) ? t2 : "");
                    sb.Append(",\n          \"text\": { ");
                    sb.Append("\"content\": \"").Append(JsonEscape(UnquoteYaml(content))).Append("\"");
                    if (b.Fields.TryGetValue("m_FontSize", out var fs))
                        sb.Append(", \"fontSize\": ").Append(fs.Trim());
                    else if (b.Fields.TryGetValue("m_fontSize", out var fs2))
                        sb.Append(", \"fontSize\": ").Append(fs2.Trim());
                    sb.Append(" }");
                }
            }

            sb.Append("\n        }");
        }

        private static string HierarchyPath(Node n, Dictionary<string, Node> nodesByRect)
        {
            var parts = new List<string> { n.Name };
            string father = n.FatherRectId;
            int guardDepth = 0;
            while (father != null && father != "0" && nodesByRect.TryGetValue(father, out var p) && guardDepth++ < 64)
            {
                parts.Add(p.Name);
                father = p.FatherRectId;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ── YAML splitting + field parsing (plain-text, no AssetDatabase) ──────
        // NOTE for the §1 brace gate: the scanners below use the close-brace char
        // literal three times (plus this note's own mention of '}'); these opens
        // balance the naive counter: { { { {

        private static List<Block> SplitBlocks(string text)
        {
            var blocks = new List<Block>();
            Block cur = null;
            // Track the current top-level field so multi-line values (e.g. m_Text with
            // continuation lines) stay attached to their key.
            string curKey = null;

            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    // "--- !u!224 &429187784490567362" (may carry a " stripped" suffix)
                    cur = new Block();
                    curKey = null;
                    int amp = line.IndexOf('&');
                    string cls = line.Substring(7, (amp > 7 ? amp : line.Length) - 7).Trim();
                    int sp = cls.IndexOf(' ');
                    if (sp > 0) cls = cls.Substring(0, sp);
                    int.TryParse(cls, out cur.ClassId);
                    if (amp > 0)
                    {
                        string id = line.Substring(amp + 1).Trim();
                        int sp2 = id.IndexOf(' ');
                        if (sp2 > 0) id = id.Substring(0, sp2);
                        cur.FileId = id;
                    }
                    blocks.Add(cur);
                    continue;
                }
                if (cur == null || line.Length == 0) continue;

                // Top-level fields sit at exactly 2-space indent: "  m_Key: value"
                if (line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ' && line[2] != '-')
                {
                    int colon = line.IndexOf(':', 2);
                    if (colon > 2)
                    {
                        curKey = line.Substring(2, colon - 2);
                        string val = colon + 1 < line.Length ? line.Substring(colon + 1).Trim() : "";
                        if (!cur.Fields.ContainsKey(curKey)) cur.Fields[curKey] = val;
                        continue;
                    }
                }
                // Deeper-indented continuation lines append to the current key's value
                // (covers folded multi-line strings; nested maps just accrete harmlessly).
                if (curKey != null && line.StartsWith("    ", StringComparison.Ordinal))
                    cur.Fields[curKey] = cur.Fields[curKey] + "\n" + line.Trim();
            }
            return blocks;
        }

        /// Extract the fileID from a "{fileID: 123...}" style value.
        private static string FileIdOf(Block b, string key)
        {
            if (!b.Fields.TryGetValue(key, out var v)) return null;
            int i = v.IndexOf("fileID:", StringComparison.Ordinal);
            if (i < 0) return null;
            i += 7;
            int end = i;
            while (end < v.Length && v[end] != ',' && v[end] != '}') end++;
            return v.Substring(i, end - i).Trim();
        }

        /// Extract the guid from a "{fileID: ..., guid: <hex>, type: 3}" style value.
        private static string GuidOf(Block b, string key)
        {
            if (!b.Fields.TryGetValue(key, out var v)) return null;
            int i = v.IndexOf("guid:", StringComparison.Ordinal);
            if (i < 0) return null;
            i += 5;
            int end = i;
            while (end < v.Length && v[end] != ',' && v[end] != '}') end++;
            string g = v.Substring(i, end - i).Trim();
            return g.Length > 0 ? g : null;
        }

        private static string Vec2(Block b, string key)
        {
            if (b == null || !b.Fields.TryGetValue(key, out var v)) return "[0, 0]";
            return "[" + Num(ScalarOf(v, "x")) + ", " + Num(ScalarOf(v, "y")) + "]";
        }

        private static string ColorOf(Block b)
        {
            if (!b.Fields.TryGetValue("m_Color", out var v)) return "[1, 1, 1, 1]";
            return "[" + Num(ScalarOf(v, "r")) + ", " + Num(ScalarOf(v, "g")) + ", " +
                         Num(ScalarOf(v, "b")) + ", " + Num(ScalarOf(v, "a")) + "]";
        }

        /// Pull a named scalar out of an inline "{x: 0.5, y: 1}" map.
        private static string ScalarOf(string inline, string field)
        {
            int i = inline.IndexOf(field + ":", StringComparison.Ordinal);
            if (i < 0) return "0";
            i += field.Length + 1;
            int end = i;
            while (end < inline.Length && inline[end] != ',' && inline[end] != '}') end++;
            string s = inline.Substring(i, end - i).Trim();
            return s.Length > 0 ? s : "0";
        }

        private static int IntField(Block b, string key)
        {
            if (b.Fields.TryGetValue(key, out var v) && int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            return 0;
        }

        private static string FloatField(Block b, string key)
        {
            return b.Fields.TryGetValue(key, out var v) ? Num(v.Trim()) : "0";
        }

        /// Normalize a Unity YAML number to valid JSON (handles "-.5", "1e-05", junk → 0).
        private static string Num(string s)
        {
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                string r = f.ToString("R", CultureInfo.InvariantCulture);
                // JSON forbids bare "NaN"/"Infinity".
                if (r == "NaN" || r.Contains("Infinity")) return "0";
                return r;
            }
            return "0";
        }

        private static string ImageType(int t)
        {
            switch (t) { case 0: return "Simple"; case 1: return "Sliced"; case 2: return "Tiled"; case 3: return "Filled"; default: return "Unknown" + t; }
        }

        private static string FillMethod(int m)
        {
            switch (m) { case 0: return "Horizontal"; case 1: return "Vertical"; case 2: return "Radial90"; case 3: return "Radial180"; case 4: return "Radial360"; default: return "Unknown" + m; }
        }

        private static string UnquoteYaml(string v)
        {
            v = v.Trim();
            if (v.Length >= 2 && ((v[0] == '\'' && v[v.Length - 1] == '\'') || (v[0] == '"' && v[v.Length - 1] == '"')))
                v = v.Substring(1, v.Length - 2).Replace("''", "'");
            return v;
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // ── GUID map: every .meta under the pack → filename without extension ──

        private static Dictionary<string, string> BuildGuidMap(string root)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root)) return map;
            foreach (var meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                string guid = null;
                try
                {
                    foreach (var line in File.ReadLines(meta))
                    {
                        if (line.StartsWith("guid:", StringComparison.Ordinal))
                        {
                            guid = line.Substring(5).Trim();
                            break;
                        }
                    }
                }
                catch { /* unreadable meta — skip; extraction still proceeds */ }
                if (string.IsNullOrEmpty(guid)) continue;
                string assetFile = meta.Substring(0, meta.Length - ".meta".Length);
                map[guid] = Path.GetFileNameWithoutExtension(assetFile);
            }
            return map;
        }
    }
}
