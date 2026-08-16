// =============================================================================
// BannedVfxRegression [banned-vfx]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
//
// Pins the OWNER VFX BANS of 2026-08-16, verbatim:
//
//   BAN 1: "D:\EoA\Assets\Resources\VFX\Projectiles\Spell_Fire_6.prefab - Do
//          Not use anywhere"
//   BAN 2: "Assets\Hovl Studio\Magic circles\Prefabs\Loop version\Magic circle
//          sun loop.prefab" - "remove"
//
// For BAN 1 the owner tagged minutes earlier "BigExplosion.prefab
// (UnityTechnologies ParticlePack) -> Fire Spell impact" - so BigExplosion
// (via its committed StatusVfxMirrors mirror, Assets/Resources/VFX/Status/
// BigExplosion.prefab) replaces Spell_Fire_6 for fire impacts. For BAN 2 the
// same-day owner tags were "Magic circle dark star.prefab - use this rotated
// for the portals" (Dungeon_Portal_Gate) and "Aura_PetLevel2 -> Node Auras"
// (Poi_NodeAura); Arcane_Aura is WITHHELD awaiting an owner tag. This suite
// makes the bans DURABLE: any future code or catalog row that re-points at a
// banned prefab goes red at the data gate instead of shipping.
//
// SCOPE NOTES (deliberate, recorded so nobody widens a ban by inference):
// * BAN 1: the Spells Pack also contains COLOUR VARIANTS - Spell_Fire_6_Blue
//   Variant / _Green / _Purple / _Yellow. The owner named ONLY the base
//   Spell_Fire_6 prefab; the variants were NOT banned and this suite
//   deliberately does not match them. Spell_Fire_6_Iddle (the banned prefab's
//   OWN idle animation) IS matched - part of the banned asset, not a variant.
// * BAN 2: the ban names the LOOP prefab ONLY. Its siblings "Magic circle
//   sun.prefab" (non-loop, still the Heal_Cast pick), "Magic circle sun
//   sparks.prefab" and "Magic circle sunS loop.prefab" (plural - a different
//   asset) are NOT banned. The matcher is exact on the banned stem: "Magic
//   circle sun loop" is not a substring of any of those three, so a plain
//   whole-stem match distinguishes them (proven in the lint simulation).
//
// Cases:
//   1 [source-lint]   Every .cs under Assets/_Modules and Assets/Editor
//                     (excluding this file) is comment-stripped and linted for
//                     a banned basename in CODE (string literals included).
//                     Fails naming file:line. Comments are exempt on purpose -
//                     history/ban prose must be allowed to name the thing it
//                     bans.
//   2 [catalog-asset] The baked catalog outputs (VFXCatalog.asset +
//                     HovlVfxCatalog.asset, text YAML) are scanned for each
//                     banned prefab's GUID (live .meta preferred, ban-time
//                     recorded GUID as the clean-clone fallback) and for the
//                     banned basename. Catches a stale catalog that still
//                     references a banned prefab after the generator source
//                     was fixed - re-run the owning generator to clear it.
//
// Markers: BANNED_VFX_OK / BANNED_VFX_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.BannedVfxRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class BannedVfxRegression
    {
        /// <summary>Basenames the owner has banned outright. Each entry names a .meta used
        /// to resolve the asset GUID for the baked-catalog scan, plus the GUID RECORDED at
        /// ban time as a fallback: the live meta is preferred (a re-import re-keys it), but
        /// a gitignored-pack meta is absent on a clean clone while the baked .asset still
        /// carries the GUID as committed text - the recorded value keeps that scan alive.
        /// Add a row per future ban.</summary>
        private static readonly (string baseName, string metaPath, string knownGuid, string banText)[] Banned =
        {
            ("Spell_Fire_6",
             "Assets/Resources/VFX/Projectiles/Spell_Fire_6.prefab.meta",
             "261eb9072f04721f8c173b555ffde263",
             "owner ban 2026-08-16: 'Assets/Resources/VFX/Projectiles/Spell_Fire_6.prefab - Do Not use anywhere'"),
            ("Magic circle sun loop",
             "Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle sun loop.prefab.meta",
             "70b62e0f3bbfee4409228673d6c15926",
             "owner ban 2026-08-16: 'Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle sun loop.prefab' - 'remove' (LOOP prefab only; sun / sun sparks / sunS loop siblings NOT banned)"),
        };

        /// <summary>Colour-variant suffixes the ban does NOT cover (see header scope note).</summary>
        private static readonly Regex VariantExempt =
            new Regex(@"_(Blue|Green|Purple|Yellow)", RegexOptions.IgnoreCase);

        private static readonly string[] LintRoots = { "Assets/_Modules", "Assets/Editor" };
        private const string SelfFile = "BannedVfxRegression.cs";
        /// <summary>Baked catalog outputs (text YAML) scanned for banned GUIDs/basenames.</summary>
        private static readonly string[] CatalogAssets =
        {
            "Assets/Resources/VFX/VFXCatalog.asset",
            "Assets/Resources/VFX/HovlVfxCatalog.asset",
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BANNED_VFX_OK - " + reason);
            else Debug.LogError("BANNED_VFX_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "source-lint", () => Case1_SourceLint(failures, notes));
                Case(failures, "catalog-asset", () => Case2_CatalogAsset(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "BANNED VFX OK - no code under Assets/_Modules or Assets/Editor and no baked " +
                         "VFXCatalog row references a banned prefab (" + Banned.Length + " banned name(s); " +
                         "colour variants deliberately out of scope)" + noteStr;
                return true;
            }
            reason = "banned-vfx FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - source lint: no CODE reference to a banned basename
        // =====================================================================
        private static void Case1_SourceLint(List<string> failures, List<string> notes)
        {
            int filesScanned = 0;
            foreach (var root in LintRoots)
            {
                if (!Directory.Exists(root))
                {
                    failures.Add("[source-lint] lint root missing: " + root + " - the tree moved; re-point this suite");
                    continue;
                }

                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string baseFile = Path.GetFileName(file);
                    if (string.Equals(baseFile, SelfFile, StringComparison.OrdinalIgnoreCase)) continue;

                    filesScanned++;
                    string[] lines;
                    try { lines = File.ReadAllLines(file); }
                    catch (Exception ex)
                    {
                        failures.Add("[source-lint] could not read " + Norm(file) + ": " + ex.GetType().Name);
                        continue;
                    }

                    bool inBlock = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string code = StripCommentsInLine(lines[i], ref inBlock);
                        if (code.Length == 0) continue;

                        foreach (var (baseName, _, _, banText) in Banned)
                        {
                            foreach (Match m in Regex.Matches(code, Regex.Escape(baseName), RegexOptions.IgnoreCase))
                            {
                                // Colour variants are out of scope (see header): exempt a
                                // match immediately followed by _Blue/_Green/_Purple/_Yellow.
                                string tail = code.Substring(m.Index + m.Length);
                                var v = VariantExempt.Match(tail);
                                if (v.Success && v.Index == 0) continue;

                                failures.Add("[source-lint] " + Norm(file) + ":" + (i + 1) + " references banned VFX '" +
                                             baseName + "' in code (" + banText + ") - re-point it at the " +
                                             "owner-tagged replacement recorded in this suite's header, or withhold " +
                                             "the key if none is tagged (never substitute)");
                                break; // one failure per banned name per line is enough
                            }
                        }
                    }
                }
            }
            notes.Add(filesScanned + " .cs files linted");
        }

        /// <summary>Removes // and /* */ comment content from ONE line, preserving string
        /// literals (a banned name inside a string is exactly what must be caught) and
        /// carrying block-comment state across lines so line numbers stay exact.</summary>
        private static string StripCommentsInLine(string line, ref bool inBlock)
        {
            var sb = new StringBuilder(line.Length);
            bool inStr = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inBlock)
                {
                    if (c == '*' && i + 1 < line.Length && line[i + 1] == '/') { inBlock = false; i++; }
                    continue;
                }
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < line.Length) { sb.Append(line[i + 1]); i++; }
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*') { inBlock = true; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // =====================================================================
        //  CASE 2 - the baked catalog asset carries no banned GUID / basename
        // =====================================================================
        private static void Case2_CatalogAsset(List<string> failures, List<string> notes)
        {
            foreach (var asset in CatalogAssets)
            {
                if (!File.Exists(asset))
                {
                    notes.Add("catalog-asset: " + asset + " not on disk (generator not yet run) - skipped");
                    continue;
                }

                string text;
                try { text = File.ReadAllText(asset); }
                catch (Exception ex)
                {
                    failures.Add("[catalog-asset] could not read " + asset + ": " + ex.GetType().Name);
                    continue;
                }

                if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                {
                    failures.Add("[catalog-asset] " + asset + " is not text-serialized YAML - this suite can no " +
                                 "longer see whether a banned prefab is referenced; force text serialization or " +
                                 "re-point the scan deliberately");
                    continue;
                }

                foreach (var (baseName, metaPath, knownGuid, banText) in Banned)
                {
                    // GUID scan: the catalog stores GUID refs, not names. Prefer the LIVE .meta
                    // (a re-import re-keys it and the live read follows); fall back to the GUID
                    // recorded at ban time, because a gitignored pack's meta is absent on a
                    // clean clone while the baked .asset still carries the GUID as committed
                    // text - without the fallback that clone would silently skip the scan.
                    string guid = ReadGuid(metaPath) ?? knownGuid;
                    if (string.IsNullOrEmpty(guid))
                    {
                        notes.Add("catalog-asset: no GUID resolvable for '" + baseName + "'; basename-only scan");
                    }
                    else if (text.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        failures.Add("[catalog-asset] " + asset + " references banned VFX '" + baseName +
                                     "' by GUID " + guid + " (" + banText + ") - re-run the owning generator " +
                                     "(VFXCatalogGenerator.Generate / HovlVfxCatalogGenerator.Generate) so the " +
                                     "baked catalog matches the re-pointed Map");
                    }

                    // Belt-and-braces basename scan (also covers m_Name-style text rows).
                    foreach (Match m in Regex.Matches(text, Regex.Escape(baseName), RegexOptions.IgnoreCase))
                    {
                        string tail = text.Substring(m.Index + m.Length);
                        var v = VariantExempt.Match(tail);
                        if (v.Success && v.Index == 0) continue;
                        failures.Add("[catalog-asset] " + asset + " contains the banned basename '" + baseName +
                                     "' as text (" + banText + ")");
                        break;
                    }
                }
            }
        }

        private static string ReadGuid(string metaPath)
        {
            if (string.IsNullOrEmpty(metaPath) || !File.Exists(metaPath)) return null;
            try
            {
                var m = Regex.Match(File.ReadAllText(metaPath), @"guid:\s*([0-9a-fA-F]{32})");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }

        private static string Norm(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
