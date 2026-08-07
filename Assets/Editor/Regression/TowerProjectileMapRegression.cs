// =============================================================================
// TowerProjectileMapRegression — owner VfxManualPicks per-tier tower projectiles.
// -----------------------------------------------------------------------------
// SOURCE-LINT (reads the tower .cs under Assets/_Modules/** + the generated
// HovlVfxCatalog.asset, no PlayMode) so it slots into the DataRegression batch and
// runs in seconds. Owner-tags-VFX / CLI-maps-verbatim: the owner tagged NEW per-tier
// archer projectile keys (ArcherTowerLevel1/2_Projectile) in the VfxCaster; the names
// ARE the mapping. This gate proves the mapping is actually WIRED and every projectile
// key a tower fires is CATALOGUED (a dangling key = a bare pellet at runtime, no error).
//
// Proves:
//   (a) DefenseTower.ProjectileKeyFor references BOTH per-tier archer keys
//       (ArcherTowerLevel1_Projectile + ArcherTowerLevel2_Projectile) AND the base/top
//       ArcherTower_Projectile — i.e. the tier 1/2/3 archer arrow ladder is wired.
//   (b) the Arcane Spire's travelling-projectile hook is HELD: ArcaneTower.cs references
//       NO projectile key at all. GATE REWRITTEN TWICE ON 2026-08-04, and the history is
//       the justification, so do not "simplify" it away:
//         - It originally asserted a two-rung ladder (ARcaneTower_Projectile [upgraded] +
//           ArcaneTower-Baselevel_Projectile [base]).
//         - The owner then ruled "fireball can go from arcane tower ... we do not ever use
//           the other two", so it briefly asserted FireballTower_Projectile at every tier.
//         - WO-872's audit then found this tower deals AETHER damage but RENDERS FIRE and
//           ruled the visual must match the element. An orange fireball is precisely the
//           Fire visual that ruling removes, so the two rulings conflicted on one tower.
//           Owner resolved it: "Aether wins, retire the fireball mapping, and use fireball
//           in casting magic from DPS mages" (the fireball rows move to the hero cast lane,
//           WO-875 - they stay AUTHORED in VfxManualPicks.json / HovlVfxCatalog, code just
//           stops referencing them here).
//       So the spire now has NO tagged projectile, and this case gates the ABSENCE. That is
//       the only shape that protects an untagged hook: the risk is not a wrong key, it is
//       somebody filling the gap with a pick that "looks aetheric". DefenseTower.cs may
//       still reference ARcaneTower_Projectile for its own fallbacks - this reads only
//       ArcaneTower.cs.
//   (c) EVERY "*_Projectile" string literal referenced in DefenseTower.cs + ArcaneTower.cs
//       is a catalogued key in Resources/VFX/HovlVfxCatalog.asset ("  - Key: <key>").
//   (d) the owner's BALLISTA tag is wired: DefenseTower.cs carries both the catalog id
//       "tower_wall_wizard" and the tagged key "SimpleCast_Projectile" (owner 2026-08-04
//       "use the SimpleCast projectile for the ballista"). Archer and Ballista are
//       otherwise indistinguishable (both bolt / None / ground), so without the per-tower
//       id table the Ballista silently borrows the archer's arrow.
//   (e) the WO-870 range-derived sizing constants exist and sit in sane bands
//       (ProjectileVisualFraction, ProjectileFitMin, ProjectileFitMax).
//   (f) the SIZE BAND (the owner's "not stupidly large or tiny" criterion): for every tower
//       row in structures-catalog.json, the derived targetSize = fraction * range lands in
//       a legible 0.25 .. 3.0 m band. This is what stops a future one-character edit to the
//       constant silently shipping a speck or a comet across all five towers.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!TowerProjectileMapRegression.Run(out var r)) failures.Add(r); else log...("[tower-proj-map] " + r);
//
// Marker: TOWER_PROJECTILE_MAP_OK / TOWER_PROJECTILE_MAP_FAIL: <reason>
// =============================================================================
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TowerProjectileMapRegression
    {
        private const string DefenseTowerRel = "_Modules/Village/Buildings/DefenseTower.cs";
        private const string ArcaneTowerRel  = "_Modules/Village/Buildings/ArcaneTower.cs";
        private const string CatalogRel      = "Resources/VFX/HovlVfxCatalog.asset";
        private const string StructuresRel   = "Resources/Data/Canonical/structures-catalog.json";

        // (f) legible size band, in world metres, for a travelling tower projectile.
        private const float MinTargetSize = 0.25f;
        private const float MaxTargetSize = 3.0f;

        // Matches a "…_Projectile" string literal referenced in source (owner key convention).
        private static readonly Regex ProjKeyLiteral =
            new Regex("\"([A-Za-z0-9_\\-]+_Projectile)\"", RegexOptions.Compiled);

        // (e) the WO-870 sizing constants, parsed straight out of DefenseTower.cs source.
        private static readonly Regex FractionRx =
            new Regex("ProjectileVisualFraction\\s*=\\s*([0-9]*\\.?[0-9]+)f", RegexOptions.Compiled);
        private static readonly Regex FitMinRx =
            new Regex("ProjectileFitMin\\s*=\\s*([0-9]*\\.?[0-9]+)f", RegexOptions.Compiled);
        private static readonly Regex FitMaxRx =
            new Regex("ProjectileFitMax\\s*=\\s*([0-9]*\\.?[0-9]+)f", RegexOptions.Compiled);

        // (f) structures-catalog.json entry scan. "id" never collides with "behaviorId" (the
        // pattern requires the OPENING quote), so entry chunking on it is exact.
        private static readonly Regex EntryIdRx =
            new Regex("\"id\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex BehaviorRx =
            new Regex("\"behaviorId\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex RangeRx =
            new Regex("\"range\"\\s*:\\s*([0-9]*\\.?[0-9]+)", RegexOptions.Compiled);

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TOWER PROJECTILE MAP (owner VfxManualPicks per-tier keys) ---");

            string dtPath  = Path.Combine(Application.dataPath, DefenseTowerRel);
            string atPath  = Path.Combine(Application.dataPath, ArcaneTowerRel);
            string catPath = Path.Combine(Application.dataPath, CatalogRel);
            string strPath = Path.Combine(Application.dataPath, StructuresRel);

            string dtSrc = ReadOrFail(dtPath, "DefenseTower.cs", failures);
            string atSrc = ReadOrFail(atPath, "ArcaneTower.cs", failures);
            string catTxt = ReadOrFail(catPath, "HovlVfxCatalog.asset", failures);
            string strTxt = ReadOrFail(strPath, "structures-catalog.json", failures);

            if (failures.Count > 0)
            {
                reason = "tower-proj-map: " + string.Join("; ", failures);
                Debug.LogError(log.ToString() + "TOWER_PROJECTILE_MAP_FAIL: " + reason);
                return false;
            }

            // (a) Per-tier archer ladder wired in DefenseTower.ProjectileKeyFor.
            foreach (var k in new[] { "ArcherTowerLevel1_Projectile", "ArcherTowerLevel2_Projectile", "ArcherTower_Projectile" })
                if (!dtSrc.Contains("\"" + k + "\""))
                    failures.Add($"DefenseTower.cs does NOT reference the archer key '{k}' — per-tier archer projectile mapping not wired");
            log.AppendLine("  (a) DefenseTower archer tier ladder: L1=ArcherTowerLevel1_Projectile, L2=ArcherTowerLevel2_Projectile, L3=ArcherTower_Projectile");

            // (b) The Arcane Spire's travelling-projectile hook is HELD - it must reference NO
            //     projectile key at all. This is the gate for a deliberate ABSENCE, which is the
            //     only kind of gate that can protect an untagged hook: the standing VFX law is
            //     that the owner tags the key and the CLI maps it verbatim, so the failure mode
            //     worth catching is somebody helpfully filling the gap with a pick that "looks
            //     aetheric". Asserting a specific key cannot catch that; asserting emptiness can.
            //     A single quoted *_Projectile literal appearing in ArcaneTower.cs FAILS this.
            var arcaneKeys = new SortedSet<string>();
            foreach (Match m in ProjKeyLiteral.Matches(atSrc)) arcaneKeys.Add(m.Groups[1].Value);
            if (arcaneKeys.Count > 0)
                failures.Add("ArcaneTower.cs references projectile key(s) [" + string.Join(", ", arcaneKeys) +
                             "] but tower_arcane_spire's travel hook is HELD: it deals AETHER and has NO " +
                             "owner-tagged Aether projectile (owner 2026-08-04 'Aether wins, retire the fireball " +
                             "mapping, and use fireball in casting magic from DPS mages' - WO-872 requires the " +
                             "visual to match the damage element). Tag a key in the VfxCaster and update this " +
                             "case deliberately; do NOT substitute a pick that merely looks right");
            log.AppendLine("  (b) ArcaneTower travel key: HELD (no projectile key referenced) awaiting an Aether owner tag");

            // (c) Every "*_Projectile" key referenced by either tower is catalogued.
            var referenced = new SortedSet<string>();
            foreach (Match m in ProjKeyLiteral.Matches(dtSrc)) referenced.Add(m.Groups[1].Value);
            foreach (Match m in ProjKeyLiteral.Matches(atSrc)) referenced.Add(m.Groups[1].Value);

            if (referenced.Count == 0)
                failures.Add("no '*_Projectile' key literals found in DefenseTower.cs / ArcaneTower.cs (regex/convention drift?)");

            foreach (var key in referenced)
            {
                bool catalogued = catTxt.Contains("Key: " + key);
                if (!catalogued)
                    failures.Add($"projectile key '{key}' is referenced by a tower but is NOT catalogued in HovlVfxCatalog.asset (would fire a bare pellet at runtime)");
                log.AppendLine($"    key '{key}' -> catalogued={catalogued}");
            }
            log.AppendLine($"  (c) {referenced.Count} referenced projectile key(s) checked against the catalog");

            // (d) The owner's BALLISTA tag (2026-08-04 "use the SimpleCast projectile for the
            //     ballista") is wired through the per-tower catalog-id table. BOTH halves must be
            //     present: the key alone could be a stray comment, the id alone an unused field.
            if (!dtSrc.Contains("\"SimpleCast_Projectile\""))
                failures.Add("DefenseTower.cs does NOT reference 'SimpleCast_Projectile' - the owner's Ballista " +
                             "projectile tag (2026-08-04) is not wired");
            if (!dtSrc.Contains("\"tower_wall_wizard\""))
                failures.Add("DefenseTower.cs does NOT reference the catalog id 'tower_wall_wizard' - without the " +
                             "per-tower id table the Ballista is indistinguishable from the Archer (both bolt/None/" +
                             "ground) and silently borrows the archer arrow");
            log.AppendLine("  (d) Ballista owner tag: tower_wall_wizard -> SimpleCast_Projectile");

            // (e) WO-870 range-derived sizing constants exist and sit in sane bands.
            float fraction = ParseConst(dtSrc, FractionRx);
            float fitMin   = ParseConst(dtSrc, FitMinRx);
            float fitMax   = ParseConst(dtSrc, FitMaxRx);

            if (fraction <= 0f)
                failures.Add("DefenseTower.cs: ProjectileVisualFraction constant not found (range-derived projectile " +
                             "sizing removed or renamed?)");
            else if (fraction < 0.02f || fraction > 0.15f)
                failures.Add($"DefenseTower.cs: ProjectileVisualFraction={fraction:0.###} is outside the sane band " +
                             "0.02..0.15 (a projectile should read as a small fraction of its own flight path)");

            if (fitMin <= 0f || fitMax <= 0f)
                failures.Add("DefenseTower.cs: ProjectileFitMin / ProjectileFitMax clamp constants not found - a " +
                             "pathological prefab measurement could ship a speck or a comet unclamped");
            else if (!(fitMin < 1f && fitMax > 1f && fitMax <= 6f))
                failures.Add($"DefenseTower.cs: fit clamp band [{fitMin:0.###}, {fitMax:0.###}] must satisfy " +
                             "0 < min < 1 < max <= 6 (it has to be able to shrink AND grow an authored prefab)");
            log.AppendLine($"  (e) sizing constants: fraction={fraction:0.###} clamp=[{fitMin:0.###}, {fitMax:0.###}]");

            // (f) SIZE BAND - the owner's "not stupidly large or tiny" acceptance criterion,
            //     asserted against the REAL catalog ranges rather than a hand-copied table.
            var towers = ReadTowerRanges(strTxt);
            if (towers.Count < 5)
                failures.Add($"structures-catalog.json: parsed only {towers.Count} DefenseTower/ArcaneTower row(s) with " +
                             "a range - expected at least 5 (archer/ballista/sky-ballista/catapult/arcane spire); " +
                             "catalog or parse drift");
            foreach (var t in towers)
            {
                float targetSize = fraction * Mathf.Max(1f, t.Value);
                bool ok = targetSize >= MinTargetSize && targetSize <= MaxTargetSize;
                if (!ok)
                    failures.Add($"tower '{t.Key}' (range {t.Value:0.#}m) derives targetSize={targetSize:0.###}m, outside " +
                                 $"the legible band {MinTargetSize:0.##}..{MaxTargetSize:0.##}m - a speck or a comet " +
                                 "at that range (check ProjectileVisualFraction)");
                log.AppendLine($"    {t.Key,-22} range={t.Value,5:0.#}m -> targetSize={targetSize:0.###}m  ok={ok}");
            }
            log.AppendLine($"  (f) {towers.Count} tower row(s) size-banded at fraction={fraction:0.###}");

            // (g) WO-913 — ELEMENT == VISUAL. The Arcane Spire shipped "deals Aether, looks Fire"
            //     for days: gameplay Element was Aether while BoltVisualElement was Flame, driving
            //     a fire bolt, a fire detonation, a fire cast and a fire swirl. THIS SUITE WAS
            //     GREEN THROUGHOUT, because (a)-(f) only ever checked Hovl string keys and never
            //     read either element field. A gate that reports OK over a violated owner ruling is
            //     worse than no gate, so (g) reads the fields themselves.
            //     Owner ruling (WO-870/872): do NOT ship "deals Aether, looks Fire".
            {
                // atSrc is already read at the top of this method via ReadOrFail - reuse it rather
                // than opening the file a second time.
                string arc = atSrc;
                if (string.IsNullOrEmpty(arc))
                {
                    failures.Add($"{ArcaneTowerRel}: unreadable - (g) element==visual could not be checked");
                }
                else
                {
                    var mEl = Regex.Match(arc, @"DamageElement\s+Element\s*=\s*DamageElement\.(\w+)");
                    var mVis = Regex.Match(arc, @"DamageElement\s+BoltVisualElement\s*=\s*DamageElement\.(\w+)");

                    string el = mEl.Success ? mEl.Groups[1].Value : null;
                    string vis = mVis.Success ? mVis.Groups[1].Value : null;

                    if (el == null)
                        failures.Add("ArcaneTower.cs: could not find 'DamageElement Element = DamageElement.<X>' - (g) cannot verify the gameplay element");
                    if (vis == null)
                        failures.Add("ArcaneTower.cs: could not find 'DamageElement BoltVisualElement = DamageElement.<X>' - (g) cannot verify the visual element");

                    if (el != null && el != "Aether")
                        failures.Add($"ArcaneTower.Element is '{el}', expected 'Aether' - the Arcane Spire's gameplay element changed; if deliberate, rewrite (g) rather than loosening it");

                    if (el != null && vis != null && el != vis)
                        failures.Add($"ArcaneTower: Element='{el}' but BoltVisualElement='{vis}' - this is EXACTLY the " +
                                     "'deals Aether, looks Fire' mismatch the owner forbade (WO-870/872). The visual " +
                                     "element must equal the gameplay element.");

                    // Fire art must never hang off an Aether tower's hooks. EMPTY is allowed and is
                    // the honest current state: no Casting_Arcane / Spell_Arcane exists on disk, and
                    // substituting some other pack effect would be a creative pick, which is the
                    // owner's call (memory: vfx-map-owner-tags-no-creative-pick).
                    foreach (string hook in new[] { "BoltCastVfx", "BoltImpactExtraVfx" })
                    {
                        var mh = Regex.Match(arc, @"string\s+" + hook + @"\s*=\s*""([^""]*)""");
                        if (!mh.Success) continue;
                        string val = mh.Groups[1].Value;
                        if (val.IndexOf("Fire", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            failures.Add($"ArcaneTower.{hook} = '{val}' - FIRE art on an Aether tower. Empty is " +
                                         "allowed (no Aether cast/swirl art exists yet); fire is not.");
                    }

                    log.AppendLine($"  (g) ArcaneTower Element='{el}' == BoltVisualElement='{vis}'; cast/extra fire hooks forbidden (empty allowed)");
                }
            }

            if (failures.Count == 0)
            {
                reason = "TOWER_PROJECTILE_MAP_OK";
                Debug.Log(log.ToString() + "TOWER_PROJECTILE_MAP_OK");
                return true;
            }

            reason = "tower-proj-map: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TOWER_PROJECTILE_MAP_FAIL: " + reason);
            return false;
        }

        /// <summary>First capture of <paramref name="rx"/> in <paramref name="src"/> as a float
        /// (invariant culture), or 0 when absent/unparseable.</summary>
        private static float ParseConst(string src, Regex rx)
        {
            if (string.IsNullOrEmpty(src)) return 0f;
            var m = rx.Match(src);
            if (!m.Success) return 0f;
            return float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                 ? v : 0f;
        }

        /// <summary>
        /// Every DefenseTower / ArcaneTower catalog row that authors a repo.range, as
        /// (entry id -> range). Chunked on the "id" key: an entry's fields all live between its
        /// own id and the next one, and "behaviorId" cannot be mistaken for "id" (the pattern
        /// requires the opening quote). Source-lint only - no JSON deserializer, no PlayMode.
        /// </summary>
        private static List<KeyValuePair<string, float>> ReadTowerRanges(string json)
        {
            var rows = new List<KeyValuePair<string, float>>();
            if (string.IsNullOrEmpty(json)) return rows;

            var ids = EntryIdRx.Matches(json);
            for (int i = 0; i < ids.Count; i++)
            {
                int start = ids[i].Index;
                int end   = (i + 1 < ids.Count) ? ids[i + 1].Index : json.Length;
                string chunk = json.Substring(start, end - start);

                var beh = BehaviorRx.Match(chunk);
                if (!beh.Success) continue;
                string behavior = beh.Groups[1].Value;
                if (behavior != "DefenseTower" && behavior != "ArcaneTower") continue;

                var rng = RangeRx.Match(chunk);
                if (!rng.Success) continue;
                if (!float.TryParse(rng.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float range))
                    continue;
                if (range <= 0f) continue;

                rows.Add(new KeyValuePair<string, float>(ids[i].Groups[1].Value, range));
            }
            return rows;
        }

        private static string ReadOrFail(string path, string label, List<string> failures)
        {
            if (!File.Exists(path)) { failures.Add($"{label} not found at '{path}'"); return string.Empty; }
            try { return File.ReadAllText(path); }
            catch (System.Exception ex) { failures.Add($"{label} read threw: {ex.Message}"); return string.Empty; }
        }
    }
}
