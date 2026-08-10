// =============================================================================
// HarvestTuning — owner-tunable pet-node demo rates (WO-953 deliverable 4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// PetHarvestBootstrap used to hardcode the placeholder starter-node economy
// (YieldPerExtract=5 / ExtractCooldown=6s at SpawnNode, site.BaseYield=5 at
// TryWrapAsHarvestSite). WO-953 promotes those three numbers to canonical data —
// Data/Canonical/harvest-tuning.json, DUAL-COPY (Resources + StreamingAssets,
// byte-identical, versioned) — so the owner can retune the demo faucet with no
// recompile. DEFAULT VALUES ARE UNCHANGED (5 / 6s / 5): the tuning pass is hers,
// not this WO's.
//
// Same loader shape as VillageStrings / EchoBalanceCatalog: lazy CanonicalJson
// read, Guard.Try'd, FlowTrace on load + on the missing-file fallback (never a
// silent default — §12), Reload() for the headless oracle.
// =============================================================================
using System;
using Newtonsoft.Json;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Static read-only access to the WO-953 harvest tuning knobs
    /// (<c>Data/Canonical/harvest-tuning.json</c>). Falls back to the shipped
    /// defaults (the pre-promotion hardcodes, values unchanged) when the file
    /// is absent or unreadable — logged, never silent.
    /// </summary>
    public static class HarvestTuning
    {
        /// <summary>Canonical-data relative path (dual-copy law: Resources copy wins,
        /// StreamingAssets is the desktop source — keep byte-identical).</summary>
        public const string RelativePath = "Data/Canonical/harvest-tuning.json";

        /// <summary>The schema version this build authors/expects. A different on-disk
        /// version loads anyway (additive law) with a warn trace.</summary>
        public const int ExpectedVersion = 1;

        // ── The pre-WO-953 hardcoded defaults (PetHarvestBootstrap.cs:172/173/189).
        //    These are the values the JSON ships with; they double as the offline
        //    fallback so a missing file changes NOTHING about the demo economy. ──
        public const int   DefaultPetNodeYieldPerExtract      = 5;
        public const float DefaultPetNodeExtractCooldownSecs  = 6f;
        public const int   DefaultPetNodeSiteBaseYield        = 5;

        // ── DTO (flat; additive fields only, per the canonical-data law) ──────
        [Serializable]
        private sealed class TuningDoc
        {
            [JsonProperty("version")] public int Version = ExpectedVersion;
            [JsonProperty("petNode")] public PetNodeDoc PetNode = new PetNodeDoc();
        }

        [Serializable]
        private sealed class PetNodeDoc
        {
            [JsonProperty("yieldPerExtract")]        public int   YieldPerExtract        = DefaultPetNodeYieldPerExtract;
            [JsonProperty("extractCooldownSeconds")] public float ExtractCooldownSeconds = DefaultPetNodeExtractCooldownSecs;
            [JsonProperty("siteBaseYield")]          public int   SiteBaseYield          = DefaultPetNodeSiteBaseYield;
        }

        private static TuningDoc _doc;

        /// <summary>Units one pet/worker/tap extract banks from a placeholder starter node.</summary>
        public static int PetNodeYieldPerExtract => Doc().PetNode.YieldPerExtract;

        /// <summary>Seconds between extracts on a placeholder starter node.</summary>
        public static float PetNodeExtractCooldownSeconds => Doc().PetNode.ExtractCooldownSeconds;

        /// <summary>Base units per HarvestSite tick when a starter node is wrapped as a site.</summary>
        public static int PetNodeSiteBaseYield => Doc().PetNode.SiteBaseYield;

        /// <summary>The loaded schema version (the fallback doc reports <see cref="ExpectedVersion"/>).</summary>
        public static int Version => Doc().Version;

        /// <summary>Drop the cached doc so the next read reloads from disk (headless oracle hook).</summary>
        public static void Reload() => _doc = null;

        private static TuningDoc Doc()
        {
            if (_doc != null) return _doc;
            _doc = Guard.Try("Harvest", "load harvest-tuning.json", Load,
                fallback: (TuningDoc)null) ?? Fallback();
            return _doc;
        }

        private static TuningDoc Load()
        {
            string json = DeNelle.Core.CanonicalJson.Read(RelativePath);
            if (string.IsNullOrEmpty(json))
            {
                // §12: a missing canonical file must be a LOGGED fallback, never a
                // silent default — otherwise an owner retune that fails to deploy
                // reads as "the tuning did nothing".
                FlowTrace.Warn("Harvest",
                    $"harvest-tuning.json not found ({RelativePath}) -- using shipped defaults " +
                    $"(yield {DefaultPetNodeYieldPerExtract}, cooldown {DefaultPetNodeExtractCooldownSecs:0.#}s, siteBase {DefaultPetNodeSiteBaseYield}).");
                return null;
            }

            var d = JsonConvert.DeserializeObject<TuningDoc>(json);
            if (d == null || d.PetNode == null)
            {
                FlowTrace.Warn("Harvest", "harvest-tuning.json parsed empty -- using shipped defaults.");
                return null;
            }
            if (d.Version != ExpectedVersion)
                FlowTrace.Warn("Harvest",
                    $"harvest-tuning.json version {d.Version} != expected {ExpectedVersion} -- loading anyway (additive).");
            FlowTrace.Step("Harvest",
                $"HarvestTuning loaded (version {d.Version}): petNode yield {d.PetNode.YieldPerExtract}/extract, " +
                $"cooldown {d.PetNode.ExtractCooldownSeconds:0.#}s, siteBaseYield {d.PetNode.SiteBaseYield}.");
            return d;
        }

        private static TuningDoc Fallback() => new TuningDoc();
    }
}
