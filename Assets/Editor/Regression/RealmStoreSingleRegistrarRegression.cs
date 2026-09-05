// =============================================================================
// RealmStoreSingleRegistrarRegression [realm-store-single-registrar] (WO-1395) -
// PanelId.RealmStore has exactly ONE registrar per shipped artifact, and every
// registrar answers BOTH call shapes (plain and door-context) with the same screen.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression
// Markers:  REALM_STORE_SINGLE_REGISTRAR_OK / REALM_STORE_SINGLE_REGISTRAR_FAIL
//
// WHAT THE WO CLAIMED, AND WHAT THE TREE SAYS.
//   WO-1395 (from docs/qa/UI_SCREEN_GRAPH_2026-09-04.md dead end 2) read two
//   PanelRouter.Register(PanelId.RealmStore, ...) sites - PackStoreBootstrap.cs and
//   GooglePlayStorefront.cs - and concluded they race on static-init order in a
//   GOOGLE_PLAY build. They do not: DeNelle.Wallet.asmdef carries defineConstraints
//   ["!GOOGLE_PLAY"] (WO-1282 Lane B, commit c06a66de5) and DeNelle.GooglePlay.asmdef
//   carries ["GOOGLE_PLAY"], so exactly one registrar is compiled into any artifact.
//   The graph itself said "NOT proven either way". This suite makes the real invariant
//   a measured one so the next reader does not have to re-derive it from two asmdefs:
//
//   A  ONE REGISTRAR PER PARTITION. Every plain (Action) registration of
//      PanelId.RealmStore under Assets/_Modules is located, its owning .asmdef is
//      resolved by walking up the directory tree, and its defineConstraints are read.
//      A registrar is on the GOOGLE_PLAY side, the !GOOGLE_PLAY side, or - if its
//      asmdef carries neither - in EVERY artifact. Each side must then hold exactly
//      ONE registrar. PackStoreBootstrap.cs must be one of them, and no file may
//      register the plain opener twice.
//   B  BOTH CALL SHAPES, ONE SCREEN. Every file that registers the plain opener also
//      registers the door-context opener ((Action<string>) ...) for the same id.
//      PanelRouter.Open(id, context) prefers the context opener and falls back to
//      the plain one; a registrar with only a plain opener drops the WO-1388 door
//      (store_opened {door}) on that artifact. THIS IS THE RED-FIRST CASE: on the
//      pre-fix tree GooglePlayStorefront.cs:17 registered the plain opener only.
//   C  THE REGISTRAR NAMES ITSELF. Each registrar emits a [Flow:Store] line of the form
//      "RealmStore registrar=<ClassName> skin=" so a device log names which storefront
//      owns the id in that build. Also RED on the pre-fix tree (neither file had it).
//   D  THE LOAD-BEARING LINE. DeNelle.Wallet.asmdef still carries "!GOOGLE_PLAY". This
//      duplicates part of A on purpose: A tells you HOW MANY registrars collided, D
//      tells you WHICH line was deleted to cause it.
//
// MUTATIONS THAT RED THIS SUITE (each one line):
//   * delete "!GOOGLE_PLAY" from Assets/_Modules/Wallet/DeNelle.Wallet.asmdef
//       -> A (two registrars on the GOOGLE_PLAY side) and D.
//   * delete `PanelRouter.Register(PanelId.RealmStore, (Action<string>)OpenFromDoor);`
//     from GooglePlayStorefront.cs -> B.
//   * add `PanelRouter.Register(PanelId.RealmStore, SomeOpener);` to any other module
//     file -> A (a side now holds two registrars, or an unconstrained one sits in both).
//   * drop the "RealmStore registrar=" trace from either registrar -> C.
//
// Source-scan only: it needs no scene, no play mode and no define set, so it runs
// identically in a DAPP_STORE Editor (the only Editor that can compile this assembly -
// DeNelle.EditorRegression references DeNelle.Wallet, which a GOOGLE_PLAY Editor
// compiles out).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RealmStoreSingleRegistrarRegression
    {
        private const string Tag = "[realm-store-single-registrar]";
        private const string ModulesRoot = "Assets/_Modules";
        private const string RegisterCall = "PanelRouter.Register(PanelId.RealmStore,";
        private const string ContextCast = "(Action<string>)";
        private const string BootstrapFile = "PackStoreBootstrap.cs";
        private const string WalletAsmdef = "Assets/_Modules/Wallet/DeNelle.Wallet.asmdef";
        private const string TracePrefix = "RealmStore registrar=";

        private const string SidePlay = "GOOGLE_PLAY";
        private const string SideNotPlay = "!GOOGLE_PLAY";
        private const string SideAll = "<every artifact>";

        /// <summary>One file that registers PanelId.RealmStore, with what it registers and where it compiles.</summary>
        private sealed class Registrar
        {
            public string File;
            public string ClassName;
            public int PlainCount;
            public bool HasContext;
            public List<int> Lines = new List<int>();
            public string Asmdef;
            public string Side;
        }

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("REALM_STORE_SINGLE_REGISTRAR_OK - " + reason);
            else Debug.LogError("REALM_STORE_SINGLE_REGISTRAR_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                List<Registrar> registrars = FindRegistrars(failures, notes);
                CaseA_OneRegistrarPerPartition(registrars, failures, notes);
                CaseB_BothCallShapes(registrars, failures);
                CaseC_RegistrarNamesItself(registrars, failures);
                CaseD_WalletIsExcludedFromPlay(failures);
            }
            catch (Exception ex)
            {
                failures.Add(Tag + " THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "REALM STORE SINGLE REGISTRAR OK - one PanelId.RealmStore registrar per artifact " +
                         "(GOOGLE_PLAY / !GOOGLE_PLAY), each registering plain + door-context openers and " +
                         "naming itself in the registration trace" + noteStr;
                return true;
            }
            reason = "realm-store-single-registrar FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        // =====================================================================
        //  Locate every registration site and its compile partition
        // =====================================================================
        private static List<Registrar> FindRegistrars(List<string> failures, List<string> notes)
        {
            var found = new List<Registrar>();
            if (!Directory.Exists(ModulesRoot))
            {
                failures.Add(Tag + " " + ModulesRoot + " is not a directory");
                return found;
            }

            string[] files = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string code = StripComments(File.ReadAllText(file));
                int at = code.IndexOf(RegisterCall, StringComparison.Ordinal);
                if (at < 0) continue;

                var reg = new Registrar
                {
                    File = file.Replace('\\', '/'),
                    ClassName = Path.GetFileNameWithoutExtension(file),
                };
                while (at >= 0)
                {
                    // Slice to the statement's ';', not the first ')': a cast such as
                    // (Action<string>)OpenFromDoor closes its own paren before the call does.
                    int close = code.IndexOf(';', at + RegisterCall.Length);
                    string arg = close < 0 ? code.Substring(at + RegisterCall.Length)
                                           : code.Substring(at + RegisterCall.Length, close - at - RegisterCall.Length);
                    // A cast to Action<string> is the context opener; a cast to a two-arg Action is the
                    // mode opener (not used by the store); anything else is the plain Action opener.
                    if (arg.IndexOf(ContextCast, StringComparison.Ordinal) >= 0) reg.HasContext = true;
                    else if (arg.IndexOf("Action<string, string>", StringComparison.Ordinal) < 0) reg.PlainCount++;
                    reg.Lines.Add(LineOf(code, at));
                    at = code.IndexOf(RegisterCall, at + RegisterCall.Length, StringComparison.Ordinal);
                }

                reg.Asmdef = FindOwningAsmdef(Path.GetDirectoryName(file));
                reg.Side = ReadSide(reg.Asmdef, failures);
                found.Add(reg);
            }

            notes.Add(files.Length + " module .cs files scanned, " + found.Count + " file(s) register PanelId.RealmStore");
            foreach (var r in found)
                notes.Add(r.ClassName + " plain=" + r.PlainCount + " context=" + (r.HasContext ? "yes" : "no") +
                          " side=" + r.Side + " lines=" + string.Join(",", r.Lines.ConvertAll(l => l.ToString()).ToArray()));
            return found;
        }

        // =====================================================================
        //  A - exactly one plain registrar on each side of the GOOGLE_PLAY axis
        // =====================================================================
        private static void CaseA_OneRegistrarPerPartition(List<Registrar> registrars, List<string> failures, List<string> notes)
        {
            bool bootstrapFound = false;
            var playSide = new List<string>();
            var notPlaySide = new List<string>();

            foreach (var r in registrars)
            {
                if (r.PlainCount == 0) continue;   // context-only files do not own the plain slot
                if (r.File.EndsWith("/" + BootstrapFile, StringComparison.Ordinal)) bootstrapFound = true;
                if (r.PlainCount > 1)
                    failures.Add(Tag + " " + r.File + " registers the PLAIN PanelId.RealmStore opener " + r.PlainCount +
                                 " times (lines " + string.Join(",", r.Lines.ConvertAll(l => l.ToString()).ToArray()) +
                                 ") - PanelRouter.Register is last-writer-wins, so only the last one is live");

                switch (r.Side)
                {
                    case SidePlay: playSide.Add(r.ClassName); break;
                    case SideNotPlay: notPlaySide.Add(r.ClassName); break;
                    default:
                        // No GOOGLE_PLAY-axis constraint: this registrar ships in EVERY artifact and
                        // therefore collides with whichever storefront that artifact carries.
                        playSide.Add(r.ClassName + "(" + SideAll + ")");
                        notPlaySide.Add(r.ClassName + "(" + SideAll + ")");
                        break;
                }
            }

            if (!bootstrapFound)
                failures.Add(Tag + " " + BootstrapFile + " no longer registers the plain PanelId.RealmStore opener - " +
                             "the Night Market has no door in a DAPP_STORE artifact");

            if (playSide.Count != 1)
                failures.Add(Tag + " GOOGLE_PLAY artifact would carry " + playSide.Count + " PanelId.RealmStore registrar(s) [" +
                             string.Join(", ", playSide.ToArray()) + "] - exactly one storefront may own the id; two race on " +
                             "BeforeSceneLoad order and PanelRouter.Register replaces silently");
            if (notPlaySide.Count != 1)
                failures.Add(Tag + " !GOOGLE_PLAY (DAPP_STORE / WebGL / Editor) artifact would carry " + notPlaySide.Count +
                             " PanelId.RealmStore registrar(s) [" + string.Join(", ", notPlaySide.ToArray()) +
                             "] - exactly one storefront may own the id");

            notes.Add("GOOGLE_PLAY side: " + string.Join(", ", playSide.ToArray()) +
                      "; !GOOGLE_PLAY side: " + string.Join(", ", notPlaySide.ToArray()));
        }

        // =====================================================================
        //  B - every plain registrar also registers the door-context opener
        // =====================================================================
        private static void CaseB_BothCallShapes(List<Registrar> registrars, List<string> failures)
        {
            foreach (var r in registrars)
            {
                if (r.PlainCount == 0) continue;
                if (!r.HasContext)
                    failures.Add(Tag + " " + r.File + " registers the plain PanelId.RealmStore opener but no " +
                                 ContextCast + " door-context opener - PanelRouter.Open(RealmStore, door) falls back " +
                                 "to the plain opener on this artifact and the WO-1388 store_opened door is dropped. " +
                                 "Both call shapes must land on the same screen WITH the door latched (WO-1395)");
            }
        }

        // =====================================================================
        //  C - the registration trace names the registrar
        // =====================================================================
        private static void CaseC_RegistrarNamesItself(List<Registrar> registrars, List<string> failures)
        {
            foreach (var r in registrars)
            {
                if (r.PlainCount == 0) continue;
                string code = StripComments(File.ReadAllText(r.File));
                string expected = "\"" + TracePrefix + r.ClassName + " skin=";
                if (code.IndexOf(expected, StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " " + r.File + " does not emit the registration trace " + expected +
                                 "...\" - a device log must name which storefront owns PanelId.RealmStore in that build (WO-1395)");
            }
        }

        // =====================================================================
        //  D - the load-bearing constraint line
        // =====================================================================
        private static void CaseD_WalletIsExcludedFromPlay(List<string> failures)
        {
            string side = ReadSide(WalletAsmdef, failures);
            if (side != SideNotPlay)
                failures.Add(Tag + " " + WalletAsmdef + " defineConstraints no longer carry \"!GOOGLE_PLAY\" (read: " + side +
                             ") - WO-1282 Lane B excludes the Solana storefront from a Play artifact; without it " +
                             "PackStoreBootstrap and GooglePlayStorefront both register PanelId.RealmStore in the same build");
        }

        // -- helpers ------------------------------------------------------------

        /// <summary>Walks up from <paramref name="dir"/> to Assets/_Modules looking for the one .asmdef that owns it.</summary>
        private static string FindOwningAsmdef(string dir)
        {
            string root = Path.GetFullPath(ModulesRoot);
            string cur = dir;
            while (!string.IsNullOrEmpty(cur))
            {
                string[] defs = Directory.GetFiles(cur, "*.asmdef", SearchOption.TopDirectoryOnly);
                if (defs.Length > 0) return defs[0].Replace('\\', '/');
                if (string.Equals(Path.GetFullPath(cur).TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) break;
                cur = Path.GetDirectoryName(cur);
            }
            return null;
        }

        /// <summary>Which side of the GOOGLE_PLAY axis an asmdef compiles on: "GOOGLE_PLAY", "!GOOGLE_PLAY" or every artifact.</summary>
        private static string ReadSide(string asmdefPath, List<string> failures)
        {
            if (string.IsNullOrEmpty(asmdefPath) || !File.Exists(asmdefPath))
            {
                failures.Add(Tag + " no owning .asmdef found for a PanelId.RealmStore registrar (" + (asmdefPath ?? "<null>") +
                             ") - cannot tell which artifact it ships in");
                return SideAll;
            }
            try
            {
                var o = JObject.Parse(File.ReadAllText(asmdefPath));
                var constraints = o["defineConstraints"] as JArray;
                if (constraints == null) return SideAll;
                bool play = false, notPlay = false;
                foreach (var c in constraints)
                {
                    string v = ((string)c ?? string.Empty).Trim();
                    if (v == SidePlay) play = true;
                    else if (v == SideNotPlay) notPlay = true;
                }
                if (play && notPlay)
                {
                    failures.Add(Tag + " " + asmdefPath + " carries BOTH GOOGLE_PLAY and !GOOGLE_PLAY - it compiles nowhere");
                    return SideAll;
                }
                return play ? SidePlay : notPlay ? SideNotPlay : SideAll;
            }
            catch (Exception ex)
            {
                failures.Add(Tag + " could not parse " + asmdefPath + ": " + ex.Message);
                return SideAll;
            }
        }

        private static int LineOf(string code, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < code.Length; i++) if (code[i] == '\n') line++;
            return line;
        }

        /// <summary>Source with line and block comments blanked (newlines kept so line numbers stay
        /// true) and string literals KEPT. Same shape as StoreNameSingleSourceRegression.StripComments.</summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '\'')
                {
                    sb.Append(c); i++;
                    while (i < source.Length && source[i] != '\n')
                    {
                        char s = source[i];
                        if (s == '\\' && i + 1 < source.Length) { sb.Append(s).Append(source[i + 1]); i += 2; continue; }
                        sb.Append(s); i++;
                        if (s == '\'') break;
                    }
                    continue;
                }
                if (c == '"')
                {
                    bool verbatim = i > 0 && source[i - 1] == '@';
                    sb.Append(c); i++;
                    while (i < source.Length)
                    {
                        char s = source[i];
                        if (!verbatim && s == '\\' && i + 1 < source.Length) { sb.Append(s).Append(source[i + 1]); i += 2; continue; }
                        if (s == '"' && verbatim && i + 1 < source.Length && source[i + 1] == '"') { sb.Append("\"\""); i += 2; continue; }
                        sb.Append(s); i++;
                        if (s == '"') break;
                        if (!verbatim && s == '\n') break;
                    }
                    continue;
                }
                if (c == '/' && n == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && n == '*')
                {
                    i += 2;
                    while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/'))
                    { if (source[i] == '\n') sb.Append('\n'); i++; }
                    i += 2;
                    continue;
                }
                sb.Append(c); i++;
            }
            return sb.ToString();
        }
    }
}
