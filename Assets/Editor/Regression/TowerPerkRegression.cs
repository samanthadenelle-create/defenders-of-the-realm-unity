// =============================================================================
// TowerPerkRegression — WO-432 (owner 2026-06-28). Headless gate for the DESIGNED,
// data-driven tower-upgrade tech (tower-perks.json + DeNelle.Village.TowerPerkTable).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core), so
// it loads tower-perks.json through the SAME CanonicalJson loader the game uses and
// drives the REAL interpreter — a schema break or a bad number is a hard FAIL line,
// not a silent "upgrade does nothing" at runtime (the no-op this WO closed).
//
// Proves: (a) tower-perks.json is present + parses to >= 3 tiers with sane fields;
//         (b) the apply math is MONOTONIC — per tier damage RISES (tier2 > tier1 >
//             base), range is non-decreasing then rising, and the fire cooldown
//             SHRINKS (faster fire). i.e. a tower genuinely gains dmg/range/fire-rate
//             on upgrade.
//
// Wire into the suite from DataRegression.RunAll (one line — see the WO report):
//   if (!TowerPerkRegression.Run(out var towerPerkReason)) failures.Add(towerPerkReason);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class TowerPerkRegression
    {
        // Mirror of the JSON shape for the direct parse-presence check (independent of
        // the interpreter's built-in fallback, so a MISSING file is caught, not masked).
        private sealed class PerkRow
        {
            [JsonProperty("tier")]             public int Tier;
            [JsonProperty("name")]             public string Name = "";
            [JsonProperty("damageMult")]       public float DamageMult = 1f;
            [JsonProperty("damageAdd")]        public float DamageAdd;
            [JsonProperty("rangeAdd")]         public float RangeAdd;
            [JsonProperty("fireRateMult")]     public float FireRateMult = 1f;
            [JsonProperty("signatureAbility")] public string SignatureAbility = "";
        }

        private sealed class PerkFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("tiers")]   public List<PerkRow> Tiers = new List<PerkRow>();
        }

        /// <summary>
        /// Batchmode entry point for THIS suite alone (WO-1170) — so the [tower-fallback-parity]
        /// freshness gate can be exercised, and seen to FAIL on drift, without running the whole
        /// DataRegression fleet. Judge it by the marker on a fresh log, never the exit code
        /// (CLAUDE.md §8): Run() emits TOWER_PERKS_OK or TOWER_PERKS_FAIL.
        ///   powershell -NoProfile -File .\run-unity-method.ps1 `
        ///       -Method DeNelle.Editor.TowerPerkRegression.RunHeadless `
        ///       -LogName tower-perks.log -ExpectMarker TOWER_PERKS_OK
        /// </summary>
        public static void RunHeadless()
        {
            string reason;
            Run(out reason);
        }

        /// <summary>
        /// Runs the tower-perk regression. Returns true on pass; on failure returns false and
        /// sets <paramref name="reason"/> to a single aggregated failure line for the suite.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TOWER PERKS (tower-perks.json -> TowerPerkTable) ---");

            // (a) DIRECT presence/parse — catches a missing file the interpreter would otherwise
            //     hide behind its built-in fallback. Uses the same WebGL-safe loader the game uses.
            string json = DeNelle.Core.CanonicalJson.Read(TowerPerkTable.RelativePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                failures.Add($"tower-perks.json not found/empty at '{TowerPerkTable.RelativePath}' (CanonicalJson.Read returned null)");
            }
            else
            {
                PerkFile file = null;
                try { file = JsonConvert.DeserializeObject<PerkFile>(json); }
                catch (System.Exception ex) { failures.Add($"tower-perks.json failed to parse: {ex.Message}"); }

                if (file == null || file.Tiers == null || file.Tiers.Count < 3)
                    failures.Add($"tower-perks.json deserialized to {(file?.Tiers?.Count ?? 0)} tier(s) — expected >= 3 (Lvl 1/2/3)");
                else
                {
                    log.AppendLine($"tower-perks.json -> {file.Tiers.Count} tier rows (v{file.Version})");
                    foreach (var r in file.Tiers)
                    {
                        if (r == null) { failures.Add("tower-perks.json has a null tier row"); continue; }
                        if (r.Tier < 1) failures.Add($"tower-perks.json tier row has invalid tier {r.Tier}");
                        if (r.DamageMult <= 0f) failures.Add($"tower-perks.json tier {r.Tier} damageMult <= 0 ({r.DamageMult})");
                        if (r.FireRateMult <= 0f) failures.Add($"tower-perks.json tier {r.Tier} fireRateMult <= 0 ({r.FireRateMult})");
                        log.AppendLine($"  T{r.Tier} '{r.Name}' dmg x{r.DamageMult:0.00}+{r.DamageAdd} range +{r.RangeAdd} fireRate x{r.FireRateMult:0.00} sig='{r.SignatureAbility}'");
                    }
                }
            }

            // (b) APPLY MATH through the REAL interpreter — monotonic gains per tier.
            TowerPerkTable.Reload();

            const float baseDamage = 20f;
            const float baseRange  = 14f;
            const float baseCd     = 1.1f;

            float dBase = baseDamage;
            float d1 = TowerPerkTable.EffectiveDamage(baseDamage, 1);
            float d2 = TowerPerkTable.EffectiveDamage(baseDamage, 2);
            float d3 = TowerPerkTable.EffectiveDamage(baseDamage, 3);

            // The headline invariant the owner asked for: upgrading GIVES more damage.
            if (!(d1 > dBase)) failures.Add($"tower perk: tier-1 damage {d1:0.0} is not > base {dBase:0.0} (upgrade grants nothing)");
            if (!(d2 > d1))    failures.Add($"tower perk: tier-2 damage {d2:0.0} is not > tier-1 {d1:0.0} (not monotonic)");
            if (!(d3 > d2))    failures.Add($"tower perk: tier-3 damage {d3:0.0} is not > tier-2 {d2:0.0} (not monotonic)");

            float r1 = TowerPerkTable.EffectiveRange(baseRange, 1);
            float r2 = TowerPerkTable.EffectiveRange(baseRange, 2);
            float r3 = TowerPerkTable.EffectiveRange(baseRange, 3);
            if (!(r1 >= baseRange)) failures.Add($"tower perk: tier-1 range {r1:0.0} dropped below base {baseRange:0.0}");
            if (!(r2 > r1))         failures.Add($"tower perk: tier-2 range {r2:0.0} is not > tier-1 {r1:0.0}");
            if (!(r3 > r2))         failures.Add($"tower perk: tier-3 range {r3:0.0} is not > tier-2 {r2:0.0}");

            float c1 = TowerPerkTable.EffectiveCooldown(baseCd, 1);
            float c2 = TowerPerkTable.EffectiveCooldown(baseCd, 2);
            float c3 = TowerPerkTable.EffectiveCooldown(baseCd, 3);
            if (!(c2 < c1)) failures.Add($"tower perk: tier-2 cooldown {c2:0.00} is not < tier-1 {c1:0.00} (fire rate not faster)");
            if (!(c3 < c2)) failures.Add($"tower perk: tier-3 cooldown {c3:0.00} is not < tier-2 {c2:0.00} (fire rate not faster)");

            log.AppendLine($"  apply math: dmg base={dBase:0.0} -> L1={d1:0.0} -> L2={d2:0.0} -> L3={d3:0.0}");
            log.AppendLine($"  apply math: range {r1:0.0}/{r2:0.0}/{r3:0.0} | cooldown {c1:0.00}/{c2:0.00}/{c3:0.00}");

            // (c) THE GENERATED FALLBACK IS FRESH — WO-1170 site #1.
            CheckFallbackParity(failures, log);

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log.ToString() + "TOWER_PERKS_OK");
                return true;
            }

            reason = "tower-perks: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TOWER_PERKS_FAIL: " + reason);
            return false;
        }

        // =====================================================================
        //  [tower-fallback-parity] — WO-1170 site #1 (owner ruling 2026-08-24).
        // ---------------------------------------------------------------------
        //  TowerPerkTable's load-FAILURE path used to be a hand-written four-row
        //  table whose comment claimed it was "identical to the shipped JSON".
        //  Nothing enforced that, and it was asserting COMBAT BALANCE: the first
        //  tune of tower-perks.json made the two disagree, and a parse failure
        //  would then silently revert every tower in the game to old numbers.
        //
        //  The fallback is now GENERATED (DeNelle.Editor.TowerPerkFallbackGenerator)
        //  — the file embedded byte-for-byte and parsed through TowerPerkTable's own
        //  ParseRows. Field-level parity is true BY CONSTRUCTION; there are no
        //  numbers left to compare. SO THIS GATE PROVES THE ONE THING CODEGEN CAN
        //  STILL GET WRONG: FRESHNESS.
        //    A. the two canonical copies (Resources + StreamingAssets) are byte-identical;
        //    B. TowerPerkFallbackData.SourceSha256 equals the SHA-256 of the file on
        //       disk — i.e. the generated file is NOT STALE;
        //    C. the embedded string still hashes to that same SHA — i.e. nobody
        //       hand-edited the generated file (its banner says not to);
        //    D. the declared tier count / schema version match the file; and
        //    E. the embedded copy actually PARSES, through the real interpreter's own
        //       ParseRows, to the same tiers — a fallback that compiles but parses to
        //       nothing is precisely the "upgrades do nothing" defect WO-432 closed.
        //
        //  Every failure names the regeneration command verbatim. A gate whose remedy
        //  the reader has to go look up is a gate people route around.
        // =====================================================================

        /// <summary>Repo-relative canonical copy the generator reads (CanonicalJson resolves it first).</summary>
        private const string CanonicalResourcesCopy =
            "Assets/Resources/Data/Canonical/tower-perks.json";

        /// <summary>Repo-relative authoring copy. Must stay BYTE-IDENTICAL to the Resources copy.</summary>
        private const string CanonicalStreamingCopy =
            "Assets/StreamingAssets/Data/Canonical/tower-perks.json";

        private static void CheckFallbackParity(List<string> failures, StringBuilder log)
        {
            string regen = TowerPerkFallbackData.RegenerateCommand;
            int failuresOnEntry = failures.Count;
            string repoRoot = System.IO.Path.GetDirectoryName(Application.dataPath);

            string resAbs    = System.IO.Path.Combine(repoRoot, CanonicalResourcesCopy.Replace('/', System.IO.Path.DirectorySeparatorChar));
            string streamAbs = System.IO.Path.Combine(repoRoot, CanonicalStreamingCopy.Replace('/', System.IO.Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(resAbs))
            {
                failures.Add($"[tower-fallback-parity] {CanonicalResourcesCopy} is MISSING — the freshness of the " +
                             "generated perk fallback cannot be proven, and the runtime read path has nothing to read");
                return;
            }
            if (!System.IO.File.Exists(streamAbs))
            {
                failures.Add($"[tower-fallback-parity] {CanonicalStreamingCopy} is MISSING — the dual-copy invariant " +
                             "is broken; both canonical copies must exist and be byte-identical");
                return;
            }

            byte[] resBytes    = System.IO.File.ReadAllBytes(resAbs);
            byte[] streamBytes = System.IO.File.ReadAllBytes(streamAbs);
            string resSha      = Sha256Hex(resBytes);
            string streamSha   = Sha256Hex(streamBytes);

            // A. dual-copy.
            if (resSha != streamSha)
            {
                failures.Add($"[tower-fallback-parity] the two canonical tower-perks copies are NOT byte-identical: " +
                             $"{CanonicalResourcesCopy} sha256={resSha} ({resBytes.Length} bytes) vs " +
                             $"{CanonicalStreamingCopy} sha256={streamSha} ({streamBytes.Length} bytes). " +
                             "Reconcile them, then regenerate the fallback: " + regen);
            }

            // B. STALENESS — the one thing codegen can still get wrong.
            if (TowerPerkFallbackData.SourceSha256 != resSha)
            {
                failures.Add($"[tower-fallback-parity] THE GENERATED TOWER-PERK FALLBACK IS STALE. " +
                             $"TowerPerkFallbackData.g.cs was generated from a perk table with " +
                             $"sha256={TowerPerkFallbackData.SourceSha256} ({TowerPerkFallbackData.SourceByteLength} bytes), " +
                             $"but {CanonicalResourcesCopy} now hashes to {resSha} ({resBytes.Length} bytes). " +
                             "Until it is regenerated, a runtime failure to READ the perk table would fight the whole " +
                             "game on OLD COMBAT BALANCE — old damage, old range, old fire rate — with nothing on " +
                             "screen saying so. FIX: run " + regen);
            }

            // C. the generated file was hand-edited (its banner says DO NOT EDIT).
            string embedded = TowerPerkFallbackData.Json;
            byte[] embeddedBytes = new System.Text.UTF8Encoding(false).GetBytes(embedded);
            string embeddedSha = Sha256Hex(embeddedBytes);
            if (embeddedSha != TowerPerkFallbackData.SourceSha256)
            {
                failures.Add($"[tower-fallback-parity] TowerPerkFallbackData.Json does not hash to its own declared " +
                             $"SourceSha256 (embedded={embeddedSha} {embeddedBytes.Length} bytes vs declared=" +
                             $"{TowerPerkFallbackData.SourceSha256} {TowerPerkFallbackData.SourceByteLength} bytes) — " +
                             "the GENERATED file has been hand-edited, which is exactly what its DO-NOT-EDIT banner " +
                             "forbids. Edit " + CanonicalResourcesCopy + " instead, then run " + regen);
            }

            // D. declared shape.
            PerkFile onDisk = null;
            try { onDisk = JsonConvert.DeserializeObject<PerkFile>(new System.Text.UTF8Encoding(false).GetString(resBytes)); }
            catch (System.Exception ex)
            {
                failures.Add($"[tower-fallback-parity] {CanonicalResourcesCopy} did not parse: {ex.Message}");
            }
            int diskTiers = onDisk?.Tiers?.Count ?? 0;
            if (onDisk != null && TowerPerkFallbackData.SourceTierCount != diskTiers)
            {
                failures.Add($"[tower-fallback-parity] TowerPerkFallbackData declares {TowerPerkFallbackData.SourceTierCount} " +
                             $"tier row(s) but the perk table on disk parses to {diskTiers} — the generated fallback " +
                             "does not describe the current table. FIX: run " + regen);
            }
            if (onDisk != null && TowerPerkFallbackData.SourceVersion != onDisk.Version)
            {
                failures.Add($"[tower-fallback-parity] TowerPerkFallbackData declares schema v{TowerPerkFallbackData.SourceVersion} " +
                             $"but the perk table on disk is v{onDisk.Version}. FIX: run " + regen);
            }

            // E. it actually WORKS — the embedded copy driven through the REAL interpreter's own
            //    parse method, not a mirror of it. A fallback that compiles but parses to nothing
            //    is the silent "upgrades do nothing" defect this whole path exists to prevent.
            var parse = typeof(TowerPerkTable).GetMethod(
                "ParseRows",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (parse == null)
            {
                failures.Add("[tower-fallback-parity] TowerPerkTable.ParseRows is not reflectable (renamed/removed) — " +
                             "the JSON-failure path is UNPROVEN; re-point this gate at whatever replaced it");
                return;
            }

            object parsed;
            try { parsed = parse.Invoke(null, new object[] { embedded }); }
            catch (System.Reflection.TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                failures.Add($"[tower-fallback-parity] parsing the embedded perk table threw: " +
                             $"{inner.GetType().Name}: {inner.Message}");
                return;
            }

            var rows = parsed as System.Array;
            if (rows == null || rows.Length <= 1)
            {
                failures.Add("[tower-fallback-parity] the embedded perk table parsed to ZERO tier rows — on the " +
                             "load-failure path every tower upgrade would grant NOTHING. FIX: run " + regen);
                return;
            }

            int liveTiers = 0;
            for (int t = 1; t < rows.Length; t++) if (rows.GetValue(t) != null) liveTiers++;
            if (onDisk != null && liveTiers != diskTiers)
            {
                failures.Add($"[tower-fallback-parity] the embedded perk table yields {liveTiers} tier row(s) but the " +
                             $"file on disk yields {diskTiers} — the fallback is not the file. FIX: run " + regen);
            }

            if (failures.Count == failuresOnEntry)
            {
                log.AppendLine($"  [tower-fallback-parity] generated fallback is FRESH: {liveTiers} tier(s), " +
                               $"v{TowerPerkFallbackData.SourceVersion}, sha256={resSha} " +
                               $"({resBytes.Length} bytes), both canonical copies byte-identical");
            }
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
