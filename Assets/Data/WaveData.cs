// =============================================================================
// WaveData — WO-86. ScriptableObject describing one authored wave.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Data   Namespace: DeNelle.Data
//
// Create assets via Assets > Create > Defenders/Data/Wave.
//
// IMPORTANT: WaveManager already drives spawning from the canonical waves.json
// via WaveDef/EnemyDef. This SO is an ADDITIVE layer — WaveManager can accept
// an optional List<WaveData> alongside its JSON schedule. When both are present,
// the SO list takes precedence for the wave numbers it defines; JSON covers the
// rest. This lets designers author new waves without touching JSON.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Data
{
    /// <summary>
    /// One enemy type entry inside a <see cref="WaveData"/> asset.
    /// Defines how many of a given enemy type spawn and the interval between each.
    /// </summary>
    [System.Serializable]
    public struct EnemySpawnEntry
    {
        [Tooltip("ScriptableObject that defines this enemy's stats.")]
        public EnemyData enemyType;

        [Tooltip("How many of this enemy type to spawn in this wave.")]
        public int       count;

        [Tooltip("Seconds between each individual spawn of this enemy type.")]
        public float     spawnInterval;
    }

    [CreateAssetMenu(fileName = "NewWaveData", menuName = "Defenders/Data/Wave")]
    public class WaveData : ScriptableObject
    {
        [Header("Identity")]
        public int    waveNumber;
        public string waveTitle             = "Wave 1";

        [Header("Enemies")]
        public List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();

        [Header("Timing")]
        public float  prewaveDelay          = 2f;   // Calm seconds before first spawn
        public float  postWaveDelay         = 5f;   // Celebration window before next wave

        [Header("Weather")]
        public bool   isBigWave             = false;   // Triggers rain/wind in WeatherManager
        public float  rainIntensity         = 0.5f;

        [Header("Rewards")]
        public int    aetherReward          = 50;
        public int    woodReward            = 30;
        public bool   grantsBPXP            = true;
        public int    bpXPAmount            = 200;
    }
}
