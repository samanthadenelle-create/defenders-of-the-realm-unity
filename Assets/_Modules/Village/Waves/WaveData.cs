// =============================================================================
// WaveData — typed records + loaders for the canonical waves.json + enemies.json
// -----------------------------------------------------------------------------
// Port spec Part 4 (data-extraction protocol): the canonical wave schedule lives
// in StreamingAssets/Data/Canonical/waves.json and the village-enemy stat blocks
// in .../enemies.json. This file is the strongly-typed C# mirror — Newtonsoft.Json
// deserialises the JSON into these PLAIN records, and the WaveManager hydrates
// the wave loop from them.
//
// JSON is the source of truth (port spec Part 4 "JSON wins over ScriptableObject").
// These records are plain C# — no MonoBehaviour, no ScriptableObject — so they
// unit-test cleanly and the same shapes can be reused by future content.
//
// LOADER PATTERN: mirrors DeNelle.Dungeons.DungeonLayoutLoader and
// DeNelle.Wallet.PackCatalog — canonical JSON under Application.streamingAssetsPath,
// read via UniTask (async because Android packs StreamingAssets inside the APK),
// parsed by Newtonsoft.Json. See unity-decisions.md 2026-05-18 (Newtonsoft over
// JsonUtility) and 2026-05-22.
// =============================================================================

using System;
using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Village
{
    // =========================================================================
    //  enemies.json — the in-village wave roster (the Hollow Ones)
    // =========================================================================

    /// <summary>
    /// The AI archetype of a village enemy. Mirrors the port spec Part 3 enemy
    /// row ("Walker (default), Charger, Skirmisher"). v2-foundation Wave 1 ships
    /// <see cref="Walker"/> only; the others are authored ahead for later waves.
    /// </summary>
    public enum EnemyAiKind
    {
        /// <summary>Default — straight march toward the Heart, melee on contact.</summary>
        Walker = 0,
        /// <summary>Faster; shoulders past buildings rather than pathing around.</summary>
        Charger = 1,
        /// <summary>Fast; peels off the line to strike the weakest wall / gate.</summary>
        Skirmisher = 2,
    }

    /// <summary>
    /// One village-enemy stat block — the deserialised shape of an
    /// <c>enemies.json</c> entry. The Hollow Ones that march on Avalon.
    /// </summary>
    [Serializable]
    public sealed class EnemyDef
    {
        /// <summary>Stable id — the wave-data <c>type</c> field keys into this.</summary>
        [JsonProperty("id")] public string Id;
        /// <summary>Short canon name (e.g. "Hollow Walker").</summary>
        [JsonProperty("name")] public string Name;
        /// <summary>Display name shown on the HP bar / codex.</summary>
        [JsonProperty("displayName")] public string DisplayName;
        /// <summary>KayKit mesh key — resolved to Assets/Models/KayKit/enemies/&lt;key&gt;.glb.</summary>
        [JsonProperty("modelKey")] public string ModelKey;
        /// <summary>AI archetype token ("walker" / "charger" / "skirmisher").</summary>
        [JsonProperty("ai")] public string Ai = "walker";
        /// <summary>Resolved max hit points (React global buffs already baked in).</summary>
        [JsonProperty("hp")] public float Hp = 50f;
        /// <summary>NavMeshAgent speed — world units per second.</summary>
        [JsonProperty("moveSpeed")] public float MoveSpeed = 2.5f;
        /// <summary>Damage one melee hit deals to a building / wall / gate.</summary>
        [JsonProperty("contactDamage")] public float ContactDamage = 6f;
        /// <summary>Seconds between melee hits while in contact.</summary>
        [JsonProperty("attackInterval")] public float AttackInterval = 1.3f;
        /// <summary>Approximate mesh height (world units) — sizes the agent + HP bar.</summary>
        [JsonProperty("height")] public float Height = 1.7f;
        /// <summary>True for the Necromancer — leads boss waves.</summary>
        [JsonProperty("boss")] public bool Boss;
        /// <summary>Codex flavour line — narrative-bible voice.</summary>
        [JsonProperty("flavor")] public string Flavor;

        /// <summary>The <see cref="Ai"/> token parsed to the typed enum.</summary>
        public EnemyAiKind AiKind
        {
            get
            {
                switch ((Ai ?? "walker").Trim().ToLowerInvariant())
                {
                    case "charger": return EnemyAiKind.Charger;
                    case "skirmisher": return EnemyAiKind.Skirmisher;
                    default: return EnemyAiKind.Walker;
                }
            }
        }
    }

    /// <summary>The deserialised root of <c>enemies.json</c>.</summary>
    [Serializable]
    public sealed class EnemyCatalog
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("enemies")] public List<EnemyDef> Enemies = new List<EnemyDef>();

        /// <summary>Finds an enemy def by id, or null when the catalog has none.</summary>
        public EnemyDef Find(string id)
        {
            if (string.IsNullOrEmpty(id) || Enemies == null) return null;
            foreach (var e in Enemies)
                if (e != null && e.Id == id) return e;
            return null;
        }
    }

    // =========================================================================
    //  waves.json — the wave-spawn schedule
    // =========================================================================

    /// <summary>
    /// One spawn batch within a wave — a group of one enemy type released at a
    /// single spawn point. The deserialised shape of a <c>waves.json</c>
    /// <c>enemies[]</c> entry.
    /// </summary>
    [Serializable]
    public sealed class WaveBatch
    {
        /// <summary>Enemy id — keys into <see cref="EnemyCatalog"/>.</summary>
        [JsonProperty("type")] public string Type;
        /// <summary>How many enemies of <see cref="Type"/> this batch releases.</summary>
        [JsonProperty("count")] public int Count = 1;
        /// <summary>The <see cref="WaveSpawnPoint.SpawnId"/> the batch materialises at.</summary>
        [JsonProperty("spawnPoint")] public string SpawnPoint = "spawn-0";
        /// <summary>Seconds after the wave starts before this batch begins releasing.</summary>
        [JsonProperty("delay")] public float Delay;
        /// <summary>Seconds between successive enemy spawns within the batch.</summary>
        [JsonProperty("interval")] public float Interval = 1f;
    }

    /// <summary>
    /// The apex flying-boss declaration on a wave — the deserialised shape of a
    /// <c>waves.json</c> <c>apexBoss</c> object. This is DISTINCT from
    /// <see cref="WaveDef.Boss"/>: <see cref="WaveDef.Boss"/> names a ground
    /// <see cref="EnemyDef"/> released as a normal NavMesh enemy, whereas the
    /// apex boss is the kinematic-flight <c>DragonBoss</c> prefab — it does not
    /// path a NavMesh and is not in <c>enemies.json</c>. WaveManager spawns the
    /// Boss_Dragon prefab and calls <c>DragonBoss.Configure</c> for an apex wave.
    /// See docs/port-notes/dragon-boss.md §2 + dragon-wave-wiring.md.
    /// </summary>
    [Serializable]
    public sealed class ApexBossDef
    {
        /// <summary>Stable per-instance boss id — e.g. "boss-dragon-syndrath".</summary>
        [JsonProperty("id")] public string Id = "boss-dragon";
        /// <summary>
        /// Max hit points for the apex boss. ≤0 keeps the prefab's inspector HP
        /// (the DragonBoss default is 4200 — well above the Necromancer's 1700).
        /// </summary>
        [JsonProperty("hp")] public float Hp;
        /// <summary>
        /// canon-strings.json key for the boss display name (the proper noun —
        /// e.g. "bossSyndrath" -> "Syndrath the Devourer"). Resolved via
        /// <see cref="VillageStrings.Canon"/> by the HUD / boss bar.
        /// </summary>
        [JsonProperty("nameKey")] public string NameKey;
    }

    /// <summary>
    /// One wave — a Prepare-Phase countdown followed by a set of spawn batches.
    /// The deserialised shape of a <c>waves.json</c> <c>waves[]</c> entry.
    /// </summary>
    [Serializable]
    public sealed class WaveDef
    {
        /// <summary>1-based wave ordinal.</summary>
        [JsonProperty("waveId")] public int WaveId;
        /// <summary>Wave name (narrative flavour — e.g. "First Light").</summary>
        [JsonProperty("name")] public string Name;
        /// <summary>Seconds of calm Prepare Phase BEFORE this wave spawns.</summary>
        [JsonProperty("countdownSeconds")] public float CountdownSeconds = 45f;
        /// <summary>The ordered spawn batches that make up the wave.</summary>
        [JsonProperty("enemies")] public List<WaveBatch> Enemies = new List<WaveBatch>();
        /// <summary>Optional boss enemy id released alongside the wave (null = no boss).</summary>
        [JsonProperty("boss")] public string Boss;
        /// <summary>
        /// Optional apex flying-boss declaration (null = not an apex wave). When
        /// present this wave spawns the kinematic <c>DragonBoss</c> prefab instead
        /// of — or alongside — the normal enemy batches. See <see cref="ApexBossDef"/>.
        /// </summary>
        [JsonProperty("apexBoss")] public ApexBossDef ApexBoss;

        /// <summary>True when this wave fields the apex flying boss (the dragon).</summary>
        public bool IsApexBossWave => ApexBoss != null;

        /// <summary>Total enemy count across every batch (boss excluded).</summary>
        public int TotalEnemyCount
        {
            get
            {
                int n = 0;
                if (Enemies != null)
                    foreach (var b in Enemies) if (b != null) n += Mathf.Max(0, b.Count);
                return n;
            }
        }
    }

    /// <summary>The deserialised root of <c>waves.json</c>.</summary>
    [Serializable]
    public sealed class WaveSchedule
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("waves")] public List<WaveDef> Waves = new List<WaveDef>();

        /// <summary>Finds a wave by its 1-based id, or null when none matches.</summary>
        public WaveDef Find(int waveId)
        {
            if (Waves == null) return null;
            foreach (var w in Waves)
                if (w != null && w.WaveId == waveId) return w;
            return null;
        }
    }

    // =========================================================================
    //  Loader — async read from StreamingAssets (DungeonLayoutLoader pattern)
    // =========================================================================

    /// <summary>
    /// Loads the canonical <c>waves.json</c> and <c>enemies.json</c> from
    /// <c>StreamingAssets/Data/Canonical/</c>. Reading from StreamingAssets is
    /// async on Android (the files live inside the compressed APK), so every
    /// load returns a <see cref="UniTask"/> — never <c>async void</c> (port spec
    /// Part 3 mandate). Mirrors <c>DeNelle.Dungeons.DungeonLayoutLoader</c>.
    /// </summary>
    public static class WaveDataLoader
    {
        /// <summary>StreamingAssets-relative path to the canonical wave schedule.</summary>
        public const string WavesRelativePath = "Data/Canonical/waves.json";

        /// <summary>StreamingAssets-relative path to the village-enemy catalog.</summary>
        public const string EnemiesRelativePath = "Data/Canonical/enemies.json";

        /// <summary>
        /// Loads and deserialises the wave schedule. Returns null and logs an
        /// error when the file is missing or malformed.
        /// </summary>
        public static async UniTask<WaveSchedule> LoadWavesAsync()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, WavesRelativePath);
            string json = await ReadTextAsync(fullPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[WaveDataLoader] Could not read '{fullPath}'.");
                return null;
            }

            try
            {
                var schedule = JsonConvert.DeserializeObject<WaveSchedule>(json);
                if (schedule == null || schedule.Waves == null || schedule.Waves.Count == 0)
                {
                    Debug.LogError("[WaveDataLoader] waves.json deserialised to an empty schedule.");
                    return null;
                }
                return schedule;
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[WaveDataLoader] Malformed JSON in waves.json: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads and deserialises the village-enemy catalog. Returns null and
        /// logs an error when the file is missing or malformed.
        /// </summary>
        public static async UniTask<EnemyCatalog> LoadEnemiesAsync()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, EnemiesRelativePath);
            string json = await ReadTextAsync(fullPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[WaveDataLoader] Could not read '{fullPath}'.");
                return null;
            }

            try
            {
                var catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json);
                if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
                {
                    Debug.LogError("[WaveDataLoader] enemies.json deserialised to an empty catalog.");
                    return null;
                }
                return catalog;
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[WaveDataLoader] Malformed JSON in enemies.json: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads a text file. On platforms where StreamingAssets is a plain
        /// directory (editor / Windows / macOS) it reads directly; on Android it
        /// must go through <see cref="UnityWebRequest"/> because the file lives
        /// inside the compressed APK. Same caveat as DungeonLayoutLoader.
        /// </summary>
        private static async UniTask<string> ReadTextAsync(string fullPath)
        {
            bool needsWebRequest = fullPath.Contains("://") || fullPath.Contains(":///");

            if (!needsWebRequest)
            {
                if (!File.Exists(fullPath)) return null;
                return await File.ReadAllTextAsync(fullPath);
            }

            using var req = UnityWebRequest.Get(fullPath);
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[WaveDataLoader] UnityWebRequest failed: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
