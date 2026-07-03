// =============================================================================
// PopulationBootstrap -- self-installs the Population growth coordinator + wires
// the 4 EXISTING earned-progress events into it (WORK_ORDER_587), no scene
// authoring / no VillageSceneBuilder re-save, mirroring EchoWorkforceBootstrap.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Population
//
// One persistent PopulationService across scenes (growth is global, not per-scene).
// Installed AfterSceneLoad so GameStateService (loads the save in its Awake) is up
// before PopulationService reads the persisted counters.
//
// PopulationGrowthBridge is the thin event router (mirrors EchoWaveUnlockBridge's
// scene-robust periodic re-bind). Each hook is a one-liner into AddPopulationXP:
//   - QuestService.QuestChanged  -> diff CompletedQuestCount -> "quest"  (+1 per new completion)
//   - EnemyOutpost.OnCleared     -> "outpost"
//   - WaveManager.OnWaveCleared  -> "wave"
//   - VillageTierService.Current rises -> OnVillageLevelChanged() (raises the derived cap)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;
using DeNelle.Village.Buildings.Progression;
using DeNelle.Village.World.Camps;

namespace DeNelle.Village.Population
{
    /// <summary>Installs the single persistent <see cref="PopulationService"/> + its event bridge.</summary>
    public static class PopulationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (PopulationService.Instance == null)
            {
                var go = new GameObject("PopulationService");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<PopulationService>();
                go.AddComponent<PopulationGrowthBridge>();   // routes quest/outpost/wave/village events -> Population XP
                FlowTrace.Step("Population", "PopulationBootstrap: installed PopulationService + growth bridge.");
            }
        }
    }

    /// <summary>
    /// Scene-robust router from the EXISTING quest / outpost / wave / village-tier events
    /// into <see cref="PopulationService"/>. Periodic re-bind (mirrors EchoWaveUnlockBridge)
    /// so it survives scene changes without scene authoring. Each XP grant is owner-tunable
    /// via the consts below (placeholders; the JSON milestone thresholds are the real dial).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PopulationGrowthBridge : MonoBehaviour
    {
        // -- Owner-tunable XP per earned event (placeholders; milestone gates live in the JSON). --
        private const int XpPerQuest = 150;
        private const int XpPerOutpost = 300;
        private const int XpPerWave = 80;

        private const float ScanInterval = 1.0f;   // cheap periodic re-bind to live systems
        private float _nextScan;

        private QuestService _quest;
        private int _lastCompletedQuests = -1;

        private WaveManager _wave;

        private int _lastVillageTier = -1;

        // EnemyOutposts we have already hooked (per-instance OnCleared); pruned of dead refs.
        private readonly HashSet<EnemyOutpost> _hookedOutposts = new HashSet<EnemyOutpost>();

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            BindQuests();
            BindWaves();
            BindOutposts();
            PollVillageTier();
        }

        // -- Quests: subscribe once; QuestChanged -> diff the completed count -----
        private void BindQuests()
        {
            var qs = QuestService.Instance;
            if (qs == _quest) return;

            if (_quest != null) _quest.QuestChanged -= OnQuestChanged;
            _quest = qs;
            if (_quest != null)
            {
                _quest.QuestChanged += OnQuestChanged;
                _lastCompletedQuests = _quest.CompletedQuestCount;   // baseline -- don't retro-award existing completions
                FlowTrace.Step("Population", "PopulationGrowthBridge: bound to QuestService.QuestChanged.");
            }
        }

        private void OnQuestChanged()
        {
            if (_quest == null) return;
            int now = _quest.CompletedQuestCount;
            if (_lastCompletedQuests < 0) { _lastCompletedQuests = now; return; }
            int delta = now - _lastCompletedQuests;
            _lastCompletedQuests = now;
            for (int i = 0; i < delta; i++)
                PopulationService.Instance?.AddPopulationXP(XpPerQuest, "quest");
        }

        // -- Waves: scene-robust re-bind to the live WaveManager ------------------
        private void BindWaves()
        {
#if UNITY_2023_1_OR_NEWER
            var wm = Object.FindAnyObjectByType<WaveManager>();
#else
            var wm = Object.FindAnyObjectByType<WaveManager>();
#endif
            if (wm == _wave) return;

            if (_wave != null) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            _wave = wm;
            if (_wave != null)
            {
                _wave.OnWaveCleared.AddListener(OnWaveCleared);
                FlowTrace.Step("Population", "PopulationGrowthBridge: bound to WaveManager.OnWaveCleared.");
            }
        }

        private void OnWaveCleared(int waveNumber)
        {
            PopulationService.Instance?.AddPopulationXP(XpPerWave, "wave");
        }

        // -- Outposts: hook each live EnemyOutpost's OnCleared exactly once --------
        private void BindOutposts()
        {
            _hookedOutposts.RemoveWhere(o => o == null);
#if UNITY_2023_1_OR_NEWER
            var outposts = Object.FindObjectsByType<EnemyOutpost>();
#else
            var outposts = Object.FindObjectsByType<EnemyOutpost>();
#endif
            if (outposts == null) return;
            foreach (var o in outposts)
            {
                if (o == null || _hookedOutposts.Contains(o)) continue;
                o.OnCleared += OnOutpostCleared;
                _hookedOutposts.Add(o);
            }
        }

        private void OnOutpostCleared(EnemyOutpost o)
        {
            PopulationService.Instance?.AddPopulationXP(XpPerOutpost, "outpost");
        }

        // -- Village upgrade: poll the static tier; rise -> raise the derived cap --
        private void PollVillageTier()
        {
            int tier = VillageTierService.Current;
            if (_lastVillageTier < 0) { _lastVillageTier = tier; return; }
            if (tier > _lastVillageTier)
            {
                _lastVillageTier = tier;
                PopulationService.Instance?.OnVillageLevelChanged();
            }
            else if (tier < _lastVillageTier)
            {
                _lastVillageTier = tier;   // New Game / reset -- re-baseline, no award
            }
        }

        private void OnDestroy()
        {
            if (_quest != null) _quest.QuestChanged -= OnQuestChanged;
            if (_wave != null) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            foreach (var o in _hookedOutposts) if (o != null) o.OnCleared -= OnOutpostCleared;
            _hookedOutposts.Clear();
        }
    }
}
