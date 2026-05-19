// =============================================================================
// PetCatalog — typed model + loader for the canonical pets.json (spec Part 4).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/pets/ -> _Modules/Pets/. The three starter
// guardian pets are CONTENT, not code: PetCatalog reads
// Assets/StreamingAssets/Data/Canonical/pets.json at load time and hydrates
// typed PetDef records. Mirrors the PackCatalog.cs / AbilityCatalog.cs pattern
// (canonical JSON under StreamingAssets, Application.streamingAssetsPath,
// Newtonsoft.Json — unity-decisions.md, 2026-05-18 / 05-22).
//
// Data sourced from the React v1 repo: src/modules/pets/petData.ts (PETS,
// petAttack, BOND_STAGES, PET_PERKS, PET_PERK_DESC, combat tuning) and
// src/lib/aggression.ts (petMaxHp — per-species bond-scaled HP). The five
// bondRanks rows mirror the five BOND_STAGES; rank 0 is the "Wandering" start.
//
// Pet.cs reads PetDefs from here; it never types pet names / stats inline
// (port spec Part 4 — canon strings + tuning flow from JSON).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>
    /// One Bond-rank row of a pet's progression — the C# port of a
    /// <c>bondRanks[]</c> entry. Rank 0 ("Wandering") carries no perk.
    /// </summary>
    [Serializable]
    public sealed class PetBondRank
    {
        /// <summary>Bond rank 0–4.</summary>
        [JsonProperty("rank")] public int Rank;
        /// <summary>Bond stage name — Wandering / Kindled / Attuned / Warden / Heartsworn.</summary>
        [JsonProperty("stage")] public string Stage;
        /// <summary>Max HP at this rank — bond-scaled per species (aggression.ts petMaxHp).</summary>
        [JsonProperty("maxHp")] public float MaxHp;
        /// <summary>Per-hit attack damage at this rank — petData.ts petAttack(rank) = 9 + rank*5.</summary>
        [JsonProperty("attackDamage")] public float AttackDamage;
        /// <summary>Perk name unlocked at this rank — empty at rank 0.</summary>
        [JsonProperty("perk")] public string Perk;
        /// <summary>One-line perk description for the Attunement card.</summary>
        [JsonProperty("perkDescription")] public string PerkDescription;
    }

    /// <summary>
    /// One starter guardian pet's static definition — the C# port of React's
    /// <c>PetDef</c>. Hydrated from pets.json; never constructed inline.
    /// </summary>
    [Serializable]
    public sealed class PetDef
    {
        /// <summary>Stable id — e.g. <c>pet-aether-sprite</c>.</summary>
        [JsonProperty("id")] public string Id;
        /// <summary>Species id — aether-sprite / flame-pup / ice-wolf.</summary>
        [JsonProperty("species")] public string Species;
        /// <summary>Canon pet name — verbatim (Aether Sprite / Flame Pup / Ice Wolf).</summary>
        [JsonProperty("name")] public string Name;
        /// <summary>Element — aether / flame / ice.</summary>
        [JsonProperty("element")] public string Element;
        /// <summary>Archetype flavour label — e.g. Heart-Ward.</summary>
        [JsonProperty("archetype")] public string Archetype;
        /// <summary>Body tint hex.</summary>
        [JsonProperty("tint")] public string Tint;
        /// <summary>Glow colour hex.</summary>
        [JsonProperty("glowColor")] public string GlowColor;
        /// <summary>Particle colour hex.</summary>
        [JsonProperty("particleColor")] public string ParticleColor;
        /// <summary>Hunt move speed (units/sec) — petData.ts PET_HUNT_SPEED.</summary>
        [JsonProperty("huntSpeed")] public float HuntSpeed = 4.4f;
        /// <summary>Attack reach (units) — petData.ts PET_ATTACK_RANGE.</summary>
        [JsonProperty("attackRange")] public float AttackRange = 2.7f;
        /// <summary>Seconds between attacks — petData.ts PET_ATTACK_CD.</summary>
        [JsonProperty("attackCooldown")] public float AttackCooldown = 0.75f;
        /// <summary>Index of this pet's home deploy slot ringing the Heart (0,1,2).</summary>
        [JsonProperty("slotIndex")] public int SlotIndex;
        /// <summary>The five Bond-rank rows (ranks 0–4).</summary>
        [JsonProperty("bondRanks")] public List<PetBondRank> BondRanks = new List<PetBondRank>();

        /// <summary>The Bond-rank row for <paramref name="rank"/> (clamped 0–4).</summary>
        public PetBondRank RankAt(int rank)
        {
            if (BondRanks == null || BondRanks.Count == 0) return null;
            int clamped = Mathf.Clamp(rank, 0, BondRanks.Count - 1);
            // Rows are authored in order, but match by Rank field to be safe.
            foreach (var row in BondRanks)
                if (row != null && row.Rank == clamped) return row;
            return BondRanks[clamped];
        }

        /// <summary>The body tint parsed to a Unity <see cref="Color"/>.</summary>
        public Color TintColor =>
            ColorUtility.TryParseHtmlString(Tint ?? "#ffffff", out var c) ? c : Color.white;

        /// <summary>The glow colour parsed to a Unity <see cref="Color"/>.</summary>
        public Color GlowUnityColor =>
            ColorUtility.TryParseHtmlString(GlowColor ?? "#ffffff", out var c) ? c : Color.white;
    }

    /// <summary>The parsed pets.json root.</summary>
    [Serializable]
    public sealed class PetCatalogData
    {
        [JsonProperty("version")] public int Version;
        /// <summary>Radius of the deploy-slot ring around the Heart (petPost() in petData.ts).</summary>
        [JsonProperty("deployRadius")] public float DeployRadius = 11f;
        /// <summary>The three starter pet defs.</summary>
        [JsonProperty("pets")] public List<PetDef> Pets = new List<PetDef>();
    }

    /// <summary>
    /// Static surface over the canonical pets.json — loads + caches the three
    /// starter PetDefs, exposes lookups by id / species, and the home-slot ring
    /// geometry. The PackCatalog.cs loading pattern (port spec Part 4).
    /// </summary>
    public static class PetCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/pets.json";

        private static PetCatalogData _data;

        /// <summary>All starter pet defs, in catalog order (Aether / Flame / Ice).</summary>
        public static IReadOnlyList<PetDef> Pets
        {
            get { EnsureLoaded(); return _data.Pets; }
        }

        /// <summary>Radius of the pet deploy-slot ring around the Heart.</summary>
        public static float DeployRadius
        {
            get { EnsureLoaded(); return _data.DeployRadius; }
        }

        /// <summary>Looks up a pet def by its stable id. Returns null when not found.</summary>
        public static PetDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var pet in _data.Pets)
                if (pet != null && pet.Id == id) return pet;
            return null;
        }

        /// <summary>Looks up a pet def by species id. Returns null when not found.</summary>
        public static PetDef FindBySpecies(string species)
        {
            if (string.IsNullOrEmpty(species)) return null;
            EnsureLoaded();
            foreach (var pet in _data.Pets)
                if (pet != null && pet.Species == species) return pet;
            return null;
        }

        /// <summary>
        /// World position of pet deploy slot <paramref name="slotIndex"/> on the
        /// ring around the Heart. Verbatim port of <c>petPost()</c> in the React
        /// petData.ts: <c>a = i/3 * 2PI + 0.5; [sin(a)*R, cos(a)*R]</c>.
        /// </summary>
        /// <param name="slotIndex">Slot index (0,1,2).</param>
        /// <param name="heartPosition">World position of the Heart (the ring centre).</param>
        public static Vector3 DeploySlotPosition(int slotIndex, Vector3 heartPosition)
        {
            EnsureLoaded();
            float a = (slotIndex / 3f) * Mathf.PI * 2f + 0.5f;
            return heartPosition + new Vector3(Mathf.Sin(a) * _data.DeployRadius, 0f, Mathf.Cos(a) * _data.DeployRadius);
        }

        /// <summary>Forces a re-read of pets.json (used by tests / the Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        // =====================================================================
        //  Loading — identical strategy to PackCatalog.LoadCatalog().
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
        }

        private static PetCatalogData LoadCatalog()
        {
            // Canonical JSON lives under StreamingAssets so it is readable in a
            // player build (spec Part 3 forbids Resources.Load). ANDROID NOTE:
            // on Android StreamingAssets is packed inside the APK and a
            // UnityWebRequest read is required — to be added with the Week-7/8
            // Seeker build (same caveat as PackCatalog.cs / Theme.cs).
            var full = Path.Combine(Application.streamingAssetsPath, StreamingRelativePath);
            try
            {
                if (File.Exists(full))
                {
                    var parsed = JsonConvert.DeserializeObject<PetCatalogData>(File.ReadAllText(full));
                    if (parsed != null && parsed.Pets != null && parsed.Pets.Count > 0)
                        return parsed;
                    Debug.LogError($"[PetCatalog] pets.json at {full} parsed empty.");
                }
                else
                {
                    Debug.LogError($"[PetCatalog] pets.json not found at {full}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PetCatalog] Failed to read {full}: {ex.Message}");
            }

            Debug.LogError("[PetCatalog] pets.json could not be loaded — using an empty catalog.");
            return new PetCatalogData { Pets = new List<PetDef>() };
        }
    }
}
