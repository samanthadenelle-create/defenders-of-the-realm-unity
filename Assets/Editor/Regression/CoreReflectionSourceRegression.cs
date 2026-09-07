// =============================================================================
// CoreReflectionSourceRegression — WO-1511, extended by WO-1510
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// TWO CASES, both about reflection standing in for a reference that should not be
// a string. Case 1 (WO-1511) below. Case 2 (WO-1510) is the mirror image and is
// documented at its own regex: nothing under Assets/_Modules/Core may reach UP
// into DeNelle.Village by Type.GetType — that seam is now IVillageBridge on
// CoreServices.
//
// THE INVARIANT: a runtime file whose OWNING .asmdef already references
// DeNelle.Core (or IS DeNelle.Core) must NEVER reach a Core type through
// `Type.GetType("DeNelle.Core.…")`. When the compiler can already see the type,
// reflection buys nothing and costs three things CLAUDE.md §10 names: a type
// lookup per call, the compiler's rename/signature safety, and — every single
// time in this repo — a null path or a swallowing catch that turns a hard
// failure into a silent no-op (WO-1510's finding).
//
// WHY THE ASMDEF IS READ RATHER THAN A FILE ALLOWLIST: the rule is about
// VISIBILITY, and visibility is declared in exactly one place — the .asmdef
// (CLAUDE.md §5: "READ THE .asmdef — it is the authority on what may reference
// what"). A hand-maintained list of offending files is duplicated state and goes
// stale exactly like §2's WO-number block and §5's old dependency table. So this
// suite walks up from each .cs to its nearest .asmdef and asks that file.
//
// AND WHEN THE WALK NAMES NO ASMDEF, THAT IS ALSO AN ANSWER (2026-09-06, from the
// reg-wave3b red this guard produced against two deliberately asmdef-less folders).
// Not every runtime tree under Assets/_Modules carries an .asmdef: files with no
// enclosing one compile into Unity's default Assembly-CSharp, which references
// every assembly whose asmdef sets autoReferenced. So the fallback resolves the
// file to Assembly-CSharp and answers the visibility question from the real
// authority - DeNelle.Core.asmdef's own "autoReferenced" field - rather than
// guessing in either direction. It stays a FAIL only when that field says false
// or the Core asmdef cannot be located by name, because THEN the answer really is
// unknown (CLAUDE.md sec. 12: an unknown must not read as a pass).
//
// SCOPE — deliberately `Assets/_Modules` only. That is the runtime tree the rule
// governs. Editor-only oracles (this folder included) legitimately probe for
// types across assemblies they do not reference, and sweeping them in would make
// the suite a nuisance rather than a gate.
//
// NOT COVERED, ON PURPOSE: the HUD -> DeNelle.Village reflection sites
// (AdminOverlay's WaveManager resolve). DeNelle.HUD.asmdef does NOT reference
// DeNelle.Village, and CLAUDE.md §5 states that reflection there is EVIDENCE of
// the rule, not a violation of it. This suite fires only where the reference
// already exists — which is precisely why it can never flag a sanctioned seam.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>Fails if reflection reaches a Core type from an assembly that already references Core.</summary>
    public static class CoreReflectionSourceRegression
    {
        private const string CoreAssembly = "DeNelle.Core";

        // Sentinel key for the asmdef-keyed visibility cache. It is not a path, so it can never
        // collide with a real .asmdef file name, and it keeps the Core asmdef parsed ONCE rather
        // than once per asmdef-less file.
        private const string DefaultAssemblyCacheKey = "<Assembly-CSharp>";

        // Matches `Type.GetType("DeNelle.Core.` and `System.Type.GetType("DeNelle.Core.`,
        // with any whitespace. It does NOT match `asm.GetType("DeNelle.Core.…")` — an
        // Assembly.GetType probe is a different shape (assembly-scan diagnostics use it
        // legitimately) and is out of this rule's scope.
        private static readonly Regex CoreTypeReflection =
            new Regex("Type\\s*\\.\\s*GetType\\s*\\(\\s*\"DeNelle\\.Core\\.", RegexOptions.Compiled);

        // ── WO-1510: the SECOND case, and the opposite direction ─────────────
        // DeNelle.Core reaching UP into DeNelle.Village. That is a layering INVERSION, not a
        // sanctioned seam: Core is the bottom of the graph and DeNelle.Core.asmdef references
        // no game assembly at all, so a Village type name inside Core can only ever be a
        // string. Four such sites existed (SceneRouter x2, PersistenceBridge,
        // BreakCaptureHarness); WO-1510 replaced them with IVillageBridge on CoreServices, and
        // this pin is what keeps them from growing back. Scoped to Assets/_Modules/Core only —
        // HUD -> Village reflection is a DIFFERENT question (CLAUDE.md §5 calls it evidence of
        // the rule) and is deliberately untouched here.
        private static readonly Regex VillageTypeReflection =
            new Regex("Type\\s*\\.\\s*GetType\\s*\\(\\s*\"DeNelle\\.Village", RegexOptions.Compiled);

        // Comment strip ONLY — string literals are KEPT, because the literal
        // "DeNelle.Core.…" IS the thing being pinned. (SourceLint.ReadCode strips literal
        // CONTENTS, which would blind this oracle completely.) Comments must go: the fixed
        // files document the reflection they replaced, and a comment satisfying a pin is
        // the hollow pass SourceLint's header warns about.
        private static string StripComments(string source)
        {
            return Regex.Replace(source, @"//[^\r\n]*|/\*[\s\S]*?\*/", m => new string(' ', m.Length));
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string assets = Application.dataPath.Replace('\\', '/');
            string modules = Path.Combine(assets, "_Modules");

            if (!Directory.Exists(modules))
            {
                reason = "core-reflection-source: Assets/_Modules not found - the scan walked nothing";
                return false;
            }

            var asmdefSeesCore = new Dictionary<string, bool>();
            int scanned = 0, coreVisible = 0, defaultAssemblyFiles = 0;

            foreach (string path in Directory.GetFiles(modules, "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                if (!SeesCore(path, assets, asmdefSeesCore, failures, ref defaultAssemblyFiles)) continue;
                coreVisible++;

                string source;
                try { source = StripComments(File.ReadAllText(path)); }
                catch (IOException ex) { failures.Add("could not read " + Rel(path, assets) + ": " + ex.Message); continue; }

                foreach (Match m in CoreTypeReflection.Matches(source))
                {
                    int line = 1;
                    for (int i = 0; i < m.Index && i < source.Length; i++) if (source[i] == '\n') line++;
                    failures.Add(Rel(path, assets) + ":" + line +
                                 " reflects into DeNelle.Core, which its own asmdef already references - call the type directly");
                }
            }

            // ── WO-1510 case: no file under Assets/_Modules/Core names a Village type ──
            // Path-scoped, not asmdef-scoped, and that is deliberate: the question here is not
            // "can this file see Village" (nothing can — Core references no game assembly), it
            // is "does the BOTTOM of the layering reach UP by string". The answer must be no
            // for every .cs that lives in the Core tree, including the sub-assemblies nested
            // under it (DeNelle.AI, the payment providers), which sit below Village too.
            string coreTree = Path.Combine(modules, "Core");
            int coreScanned = 0;
            if (!Directory.Exists(coreTree))
            {
                failures.Add("core-tree scan: Assets/_Modules/Core not found - the Village pin walked nothing");
            }
            else
            {
                foreach (string path in Directory.GetFiles(coreTree, "*.cs", SearchOption.AllDirectories))
                {
                    coreScanned++;
                    string source;
                    try { source = StripComments(File.ReadAllText(path)); }
                    catch (IOException ex) { failures.Add("could not read " + Rel(path, assets) + ": " + ex.Message); continue; }

                    foreach (Match m in VillageTypeReflection.Matches(source))
                    {
                        int line = 1;
                        for (int i = 0; i < m.Index && i < source.Length; i++) if (source[i] == '\n') line++;
                        failures.Add(Rel(path, assets) + ":" + line +
                                     " reflects UP into DeNelle.Village from the Core tree - layering inversion; " +
                                     "cross via an interface on CoreServices (IVillageBridge, WO-1510)");
                    }
                }
            }

            // ── The oracle's own oracle ──────────────────────────────────────
            // A broken walk (wrong root, asmdef parse that never says yes, a regex that
            // matches nothing) reports a clean run that looks exactly like a healthy one.
            // These two controls make that failure LOUD.
            if (!CoreTypeReflection.IsMatch("var t = System.Type.GetType(\"DeNelle.Core.State.GameStateService, DeNelle.Core\");"))
                failures.Add("self-test: the detector failed to match a known-bad line - the pin is inert");
            if (CoreTypeReflection.IsMatch("var t = asm.GetType(\"DeNelle.Core.Economy.ISkrLedger\", false);"))
                failures.Add("self-test: the detector matched an Assembly.GetType probe - the pin is over-broad");
            if (coreVisible < 50)
                failures.Add($"self-test: only {coreVisible} of {scanned} _Modules files resolved to a Core-referencing " +
                             "asmdef - the asmdef walk is broken, so a pass proves nothing");
            if (!VillageTypeReflection.IsMatch("var t = System.Type.GetType(\"DeNelle.Village.HeroLocomotion, DeNelle.Village\");"))
                failures.Add("self-test: the Village detector failed to match a known-bad line - that pin is inert");
            if (VillageTypeReflection.IsMatch("var t = asm.GetType(\"DeNelle.Village.WaveManager\", false);"))
                failures.Add("self-test: the Village detector matched an Assembly.GetType probe - that pin is over-broad");
            if (coreScanned < 50)
                failures.Add($"self-test: only {coreScanned} .cs files found under Assets/_Modules/Core - " +
                             "the Core-tree walk is broken, so a pass proves nothing");

            if (failures.Count != 0)
            {
                reason = "core-reflection-source: " + string.Join(" | ", failures);
                return false;
            }

            reason = $"CORE_REFLECTION_SOURCE_OK - {coreVisible}/{scanned} _Modules files sit in an assembly that " +
                     "references DeNelle.Core; zero of them reach a Core type by Type.GetType (asmdef-driven, no allowlist). " +
                     $"{defaultAssemblyFiles} of those carry no enclosing .asmdef and were resolved to Assembly-CSharp, " +
                     "which sees Core because DeNelle.Core.asmdef is autoReferenced. " +
                     $"WO-1510: {coreScanned} files under Assets/_Modules/Core; zero reach UP into DeNelle.Village by Type.GetType";
            return true;
        }

        private static string Rel(string path, string assets)
        {
            string p = path.Replace('\\', '/');
            int i = p.IndexOf("/Assets/", System.StringComparison.Ordinal);
            return i >= 0 ? p.Substring(i + 1) : p;
        }

        /// <summary>
        /// True when the file's nearest enclosing asmdef IS, or references, DeNelle.Core - or, when the
        /// walk names no asmdef at all, when Unity's default Assembly-CSharp can see Core.
        /// </summary>
        private static bool SeesCore(string csPath, string assets, Dictionary<string, bool> cache, List<string> failures,
                                     ref int defaultAssemblyFiles)
        {
            string asmdef = FindOwningAsmdef(csPath, assets);
            // NO ENCLOSING ASMDEF IS A REAL SHAPE, NOT A BROKEN WALK. Some runtime folders
            // under Assets/_Modules deliberately carry no .asmdef and compile into Unity's
            // default Assembly-CSharp - Assets/_Modules/Data and Assets/_Modules/Environment
            // each say so in their own README ("Assembly-CSharp (no asmdef)"). An earlier
            // revision of this guard asserted the opposite ("every runtime .cs is owned by a
            // tracked .asmdef") and cited CLAUDE.md section 5 for it; section 5 makes no such
            // claim - it says to READ the .asmdef, and it warns against exactly this kind of
            // restated fact. So the null case is answered, not failed: resolve to
            // Assembly-CSharp and ask DeNelle.Core.asmdef whether it is autoReferenced, which
            // is what decides whether Assembly-CSharp can see Core. Still a FAIL when that
            // cannot be read, because an unknown must not read as a pass (CLAUDE.md sec. 12).
            if (asmdef == null)
            {
                bool defaultSeesCore = DefaultAssemblySeesCore(assets, cache, failures);
                if (defaultSeesCore)
                {
                    defaultAssemblyFiles++;
                    DeNelle.Core.Diagnostics.FlowTrace.Step("Regression",
                        "core-reflection-source: " + Rel(csPath, assets) + " has no enclosing .asmdef - " +
                        "resolved to Assembly-CSharp, which sees DeNelle.Core because that asmdef is " +
                        "autoReferenced; scanning it");
                }
                return defaultSeesCore;
            }
            if (cache.TryGetValue(asmdef, out bool cached)) return cached;

            bool sees = false;
            int unresolvedRefs = 0;
            try
            {
                var json = JObject.Parse(File.ReadAllText(asmdef));
                string name = (string)json["name"];
                if (name == CoreAssembly) sees = true;
                var refs = json["references"] as JArray;
                if (!sees && refs != null)
                {
                    foreach (var r in refs)
                    {
                        string resolved = ResolveReferenceName((string)r);
                        if (resolved == null) { unresolvedRefs++; continue; }
                        if (resolved == CoreAssembly) { sees = true; break; }
                    }
                }
                // A GUID reference that resolves to nothing is the same unknown as a missing
                // asmdef, and it only matters when the answer came out NO - an unresolved
                // entry could have been DeNelle.Core, which would silently drop this file
                // (and every file under it) out of the scan.
                if (!sees && unresolvedRefs > 0)
                {
                    failures.Add(Rel(asmdef, assets) + ": " + unresolvedRefs + " reference entr" +
                                 (unresolvedRefs == 1 ? "y" : "ies") + " could not be resolved to an assembly name, " +
                                 "so a NO answer here is unproven - files under it would be skipped silently");
                }
            }
            catch (System.Exception ex)
            {
                // Loud, never silent (CLAUDE.md §12): an unreadable asmdef means this file's
                // visibility is UNKNOWN, and an unknown must not read as a pass.
                failures.Add("could not parse asmdef " + Rel(asmdef, assets) + ": " + ex.Message);
            }

            cache[asmdef] = sees;
            return sees;
        }

        /// <summary>
        /// True when Unity's default Assembly-CSharp (the assembly an .asmdef-less .cs lands in)
        /// can see DeNelle.Core. Assembly-CSharp auto-references every assembly whose .asmdef sets
        /// "autoReferenced", so that field IS the answer - it is read off the file, never assumed.
        /// The Core asmdef is found BY NAME, not by a literal path, so moving the folder cannot
        /// silently turn this into a stale-path failure.
        /// </summary>
        private static bool DefaultAssemblySeesCore(string assets, Dictionary<string, bool> cache, List<string> failures)
        {
            if (cache.TryGetValue(DefaultAssemblyCacheKey, out bool cached)) return cached;

            bool sees = false;
            string coreAsmdef = null;
            string modules = Path.Combine(assets, "_Modules");
            try
            {
                if (Directory.Exists(modules))
                {
                    foreach (string candidate in Directory.GetFiles(modules, "*.asmdef", SearchOption.AllDirectories))
                    {
                        var probe = JObject.Parse(File.ReadAllText(candidate));
                        if ((string)probe["name"] != CoreAssembly) continue;
                        coreAsmdef = candidate;
                        // A missing "autoReferenced" key is Unity's default of TRUE, which is why
                        // the null case reads as true rather than as an unknown.
                        var flag = probe["autoReferenced"];
                        sees = flag == null || (bool)flag;
                        break;
                    }
                }

                if (coreAsmdef == null)
                {
                    failures.Add("Assembly-CSharp fallback: no .asmdef named " + CoreAssembly +
                                 " found under Assets/_Modules, so whether an asmdef-less file can see Core " +
                                 "is unproven - the files with no enclosing asmdef were not scanned");
                }
                else if (!sees)
                {
                    failures.Add(Rel(coreAsmdef, assets) + ": autoReferenced is false, so Assembly-CSharp does NOT " +
                                 "see " + CoreAssembly + " - every asmdef-less file under Assets/_Modules is outside " +
                                 "this pin and that is a coverage hole, not a pass");
                }
            }
            catch (System.Exception ex)
            {
                sees = false;
                failures.Add("Assembly-CSharp fallback: could not resolve " + CoreAssembly +
                             "'s autoReferenced flag: " + ex.Message);
            }

            cache[DefaultAssemblyCacheKey] = sees;
            return sees;
        }

        /// <summary>Reference entries are either a plain assembly name or "GUID:&lt;hex&gt;".</summary>
        private static string ResolveReferenceName(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            if (!entry.StartsWith("GUID:", System.StringComparison.Ordinal)) return entry;
            string assetPath = AssetDatabase.GUIDToAssetPath(entry.Substring(5));
            if (string.IsNullOrEmpty(assetPath)) return null;
            try { return (string)JObject.Parse(File.ReadAllText(assetPath))["name"]; }
            catch (System.Exception) { return null; }
        }

        private static string FindOwningAsmdef(string csPath, string assets)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(csPath));
            string root = Path.GetFullPath(assets).Replace('\\', '/').TrimEnd('/');
            while (dir != null)
            {
                var found = dir.GetFiles("*.asmdef");
                if (found.Length > 0) return found[0].FullName;
                string here = dir.FullName.Replace('\\', '/').TrimEnd('/');
                if (string.Equals(here, root, System.StringComparison.OrdinalIgnoreCase)) break;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
