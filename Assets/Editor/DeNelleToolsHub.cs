// DeNelleToolsHub — one discoverable home for every DeNelle authoring/QA tool.
//
// Owner ask (2026-07-12): "move all my tools into a single DeNelle Tools — one spot
// to find all my tools." We do NOT move/rename the tool files (that would break each
// tool's own [MenuItem] path). Instead this window is a LAUNCHER: every button calls
// EditorApplication.ExecuteMenuItem("<the tool's existing menu path>") by STRING, so
// there are zero cross-assembly type references and nothing about the tools changes.
//
// Colorblind-safe (owner is red/green colorblind): hierarchy is by text label, bold,
// and size — never by hue. External (non-Unity) tools are shown as reveal/copy-path
// references, since they cannot run inside the editor.
//
// ASCII-only UI. Lives in the DeNelle.Editor asmdef (Assets/Editor).

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class DeNelleToolsHub : EditorWindow
    {
        // Discoverable top-level menu + a mirror under the existing Defenders menu.
        // Shortcut: Ctrl/Cmd + Shift + T (%#t). If that ever conflicts, drop the "_%#t".
        [MenuItem("DeNelle Tools/Open Tools Hub _%#t", priority = 0)]
        [MenuItem("Defenders/DeNelle Tools Hub", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<DeNelleToolsHub>(false, "DeNelle Tools", true);
            w.minSize = new Vector2(460f, 520f);
            w.Show();
        }

        // ---- Tool descriptors -------------------------------------------------

        private sealed class ToolEntry
        {
            public string Label;      // ASCII button label
            public string MenuPath;   // ExecuteMenuItem target (empty for reference-only)
            public string Desc;       // one-line description
            public ToolEntry(string label, string menuPath, string desc)
            {
                Label = label; MenuPath = menuPath; Desc = desc;
            }
        }

        private static readonly ToolEntry[] Authoring =
        {
            new ToolEntry("Gear Caster",   "Defenders/Gear/Gear Caster",
                "Cast/seat weapons + shields onto the hero rig; writes offsets.json."),
            new ToolEntry("VFX Caster",    "Defenders/Animation/VFX Caster",
                "Bind spell/impact VFX to animation moments."),
            new ToolEntry("Motion Caster", "Defenders/Animation/Motion Caster",
                "Cast mocap/motion clips onto the shared rig."),
            new ToolEntry("Offset Forge",  "Tools/Offset Forge",
                "Dial model alignment offsets (manual = canon)."),
            new ToolEntry("HUD Composer",  "Tools/DeNelle/HUD Composer",
                "Compose/preview the code-built HUD layout."),
            new ToolEntry("VFX Parade",    "Tools/VFX Parade",
                "Browse the VFX manifest; audition effects."),
        };

        private static readonly ToolEntry[] Qa =
        {
            new ToolEntry("Perf Budget (Standard Phone)", "Defenders/QA/Perf Budget (Standard Phone)",
                "Check the scene against the standard-phone perf budget."),
            new ToolEntry("Optimize All Assets", "Defenders/Optimize All Assets (FBX + Textures)",
                "Bulk FBX + texture import optimization pass."),
        };

        private Vector2 _scroll;
        private string _filter = string.Empty;

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            EditorGUILayout.LabelField("DeNelle Tools Hub", title);
            EditorGUILayout.LabelField(
                "All DeNelle tools in one spot. Buttons launch the tool's own window; " +
                "external tools show their path/command.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Filter", GUILayout.Width(38f));
                _filter = EditorGUILayout.TextField(_filter);
                if (GUILayout.Button("Clear", GUILayout.Width(52f))) _filter = string.Empty;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            Section("Authoring & Casters");
            foreach (var t in Authoring) LaunchButton(t);
            // Seating Editor is an IN-GAME runtime overlay (SeatingEditorOverlay),
            // opened from the live AdminOverlay dev menu ("Seating Editor (gear)"),
            // not an EditorWindow — so there is no menu path to launch it here.
            NoteRow("Seating Editor (gear)",
                "In-game overlay (WO-577). Open at runtime via the AdminOverlay dev menu; " +
                "not launchable from the editor.");

            Section("QA & Gates");
            foreach (var t in Qa) LaunchButton(t);
            CommandRow("Run Compile Gate",
                "Headless batchmode compile + NUL/brace gate. Runs outside the editor:",
                "powershell -ExecutionPolicy Bypass -File .\\run-unity-method.ps1 " +
                "-Method DeNelle.Editor.CompileGate.Run -LogName compile-gate");
            CommandRow("Run Data Regression",
                "Headless full-coverage data regression. Runs outside the editor:",
                "powershell -ExecutionPolicy Bypass -File .\\run-unity-method.ps1 " +
                "-Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression");

            Section("Ops & External (outside Unity)");
            RevealRow("DB / Metrics Viewer",
                "Owner DB + metrics dashboard (open in a browser).",
                "tools/db-viewer/index.html");
            CommandRow("Doc Diagnostics",
                "Canon freshness / stale-doc report:",
                "python3 tools/doc_diagnostics.py");
            RevealRow("WebGL -> Vercel Deploy Chain",
                "The proven overnight WebGL build + Vercel deploy script.",
                "webgl-vercel-overnight.ps1");
            RevealRow("Admin API (db / cleanup)",
                "Serverless admin endpoints (reference only).",
                "api/admin/db.js");

            EditorGUILayout.EndScrollView();
        }

        // ---- Row builders -----------------------------------------------------

        private bool Passes(string label, string desc)
        {
            if (string.IsNullOrEmpty(_filter)) return true;
            var f = _filter.ToLowerInvariant();
            return (label != null && label.ToLowerInvariant().Contains(f))
                || (desc != null && desc.ToLowerInvariant().Contains(f));
        }

        private void Section(string name)
        {
            EditorGUILayout.Space(8f);
            var s = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            EditorGUILayout.LabelField(name, s);
            var r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        private void LaunchButton(ToolEntry t)
        {
            if (!Passes(t.Label, t.Desc)) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool exists = Menu.GetEnabled(t.MenuPath) || MenuPathKnown(t.MenuPath);
                if (GUILayout.Button(t.Label, GUILayout.Height(24f)))
                {
                    bool ok = EditorApplication.ExecuteMenuItem(t.MenuPath);
                    if (!ok)
                        Debug.LogWarning("[DeNelleToolsHub] Menu item not found: " + t.MenuPath);
                }
                EditorGUILayout.LabelField(t.Desc, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("menu: " + t.MenuPath, EditorStyles.miniLabel);
                if (!exists)
                    EditorGUILayout.LabelField("(not found — tool may be excluded from this build)",
                        EditorStyles.miniLabel);
            }
        }

        // Menu.GetEnabled returns false for validate-less items even when present,
        // so treat any non-empty registered path as potentially valid; the real
        // check is ExecuteMenuItem's bool return on click.
        private static bool MenuPathKnown(string path)
        {
            return !string.IsNullOrEmpty(path);
        }

        private void NoteRow(string label, string desc)
        {
            if (!Passes(label, desc)) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void CommandRow(string label, string desc, string command)
        {
            if (!Passes(label, desc + " " + command)) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
                // Selectable so the owner can copy it; headless/batchmode note above.
                EditorGUILayout.SelectableLabel(command,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Copy Command", GUILayout.Height(20f)))
                    EditorGUIUtility.systemCopyBuffer = command;
            }
        }

        private void RevealRow(string label, string desc, string repoRelativePath)
        {
            if (!Passes(label, desc + " " + repoRelativePath)) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("path: " + repoRelativePath, EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reveal in Explorer", GUILayout.Height(20f)))
                    {
                        string abs = ToRepoAbsolute(repoRelativePath);
                        EditorUtility.RevealInFinder(abs);
                    }
                    if (GUILayout.Button("Copy Path", GUILayout.Height(20f)))
                        EditorGUIUtility.systemCopyBuffer = ToRepoAbsolute(repoRelativePath);
                }
            }
        }

        // Repo root = the parent of Application.dataPath ("<repo>/Assets").
        private static string ToRepoAbsolute(string repoRelativePath)
        {
            string repoRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return System.IO.Path.Combine(repoRoot, repoRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }
    }
}
