// =============================================================================
// EnemyOutpost - a real, walk-to ENEMY OUTPOST in the open world (RAID bite of the
// outpost -> raid -> loot elephant). Clear it by killing the garrison.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE LOOP (this slice): walk to the outpost -> the hero + party AUTO-FIGHT the
// guards (real Enemy via TargetManager - ZERO new combat code) -> kill the whole
// garrison (1 boss + ~5-6 guards) -> the outpost is CLEARED, fires OnCleared, and
// pays a FLAT reward (XP + a little crystals). Loot DROPS are the NEXT bite.
//
// REUSE (no reinvented wheels - mirrors the proven CampGuards pattern):
//   * OutpostFoundationGenerator  - the WOOD catalog-piece fortification visual
//     (GenerateFootprintRecipe + Realize), the SAME StructureFactory.Create path
//     the village build mode uses. LOCAL cell math; no village-grid involvement.
//   * EnemyFactory.Build()        - the ONE enemy creation path (CLAUDE.md §9).
//   * Enemy / EnemyBrain          - the boss gets an EnemyBrain in the MiniBoss
//     role (tougher stat block); guards are plain charger/walker Enemy.
//   * Enemy.SetBrainTarget(anchor) - tethers each garrison member to the outpost so
//     they HOLD the outpost instead of marching the Heart of Elarion. The hero's
//     own aggro + TargetManager still pull them into the fight when she arrives.
//   * Enemy.Died                  - subscribe-only kill counting; AllDead -> Clear.
//   * ZoneManager                 - region/threat scaling (deeper = deadlier).
//
// PERSISTENCE: PlayerPrefs only (mirrors ClaimableCamp) - a cleared raid stays
// cleared on reload; the save SCHEMA is untouched (save-owner follow-up).
//
// ISOLATION: created/owned entirely by RaidOutpostSystem at runtime. Touches NO
// existing file. References only PUBLIC read-only APIs. Code-built; LogWarning,
// never error, if optional art/registry pieces are missing (pack-missing-safe).
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.World;
using DeNelle.Core.State;
using DeNelle.Core.Quests;

namespace DeNelle.Village.World.Camps
{
    /// <summary>A walk-to enemy outpost: a WOOD fort held by a boss-led garrison.
    /// Clear the whole garrison (hero + party auto-fight) to CLEAR it and collect a
    /// flat reward. Raises <see cref="OnCleared"/> once the last defender dies.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyOutpost : MonoBehaviour
    {
        // -- Garrison sizing --------------------------------------------------
        /// <summary>Base guard count (excludes the boss). Scaled up a touch by threat.</summary>
        public const int BaseGuardCount = 5;

        /// <summary>Radius the garrison stand-ring is laid out across, around the centre.</summary>
        public const float GarrisonRing = 6f;

        // -- Fortification footprint (LOCAL grid cells; cellSize = 3 m) --------
        private const int FortGridWidth = 6;
        private const int FortGridDepth = 6;

        // -- Clear reward base (threat-scaled in GrantClearReward) -------------
        /// <summary>Aether Crystals banked on clear (before the threat-tier bonus).</summary>
        public int BaseClearCrystals { get; private set; } = 40;
        /// <summary>Hero XP granted on clear (before the threat-tier bonus).</summary>
        public int BaseClearXp { get; private set; } = 120;

        // =====================================================================
        // LOOT TABLE (data-tunable) - a THREAT-SCALED roll on clear that routes
        // each drop type to its EXISTING system (no parallel loot-economy):
        //   * Resources (wood/iron)  -> EconomyService.Grant
        //   * Crystals / rare gems   -> GameState.AetherCrystals (premium wallet)
        //   * Weapon / armor          -> GearLoadout.EquipWeaponById/EquipArmorById
        //   * Raid quest progress     -> DailyQuestService.Report (clean hook)
        // Rarity/quantity rise with the outpost's ZoneManager threat tier, so a
        // deadlier outpost pays better/rarer loot. All knobs are const here.
        // =====================================================================

        // ALWAYS: base resources, scaled per threat tier.
        private const int BaseLootWood       = 40;   // + WoodPerThreat * threat
        private const int WoodPerThreat      = 12;
        private const int BaseLootIron       = 20;   // + IronPerThreat * threat
        private const int IronPerThreat      = 8;

        // CHANCE: a GEAR drop. Probability rises with threat (clamped). On a hit,
        // the rolled rarity is biased UP by threat (see RollGearRarity).
        private const float GearDropChanceBase    = 0.35f;  // at threat 0
        private const float GearDropChancePerTier = 0.06f;  // + per threat tier
        private const float GearDropChanceMax     = 0.85f;

        // RARER (high threat): a rare-gem bonus crystal payout on top of the base.
        private const int   RareGemThreatGate   = 4;     // only at threat >= this
        private const float RareGemChance       = 0.30f;
        private const int   RareGemCrystals     = 75;    // + RareGemPerThreat * threat
        private const int   RareGemPerThreat    = 15;

        // RARER (high threat): a QUEST ITEM token. No clean "grant quest item to
        // inventory" hook exists (DailyQuests rewards on completion, not the
        // reverse) - so we tick raid-clear quest PROGRESS (the clean hook) always,
        // and only deposit a tangible token when the item-drops larder lane is on.
        private const int   QuestItemThreatGate = 6;
        private const float QuestItemChance     = 0.25f;
        private const string QuestRaidEventId   = "combat.raid";   // DailyQuests Report() id
        private const string QuestItemMaterial  = "warlord-seal";  // token into the larder (lane-gated)

        // -- Persistence (PlayerPrefs only - schema untouched; mirror ClaimableCamp) --
        private const string PrefClearedKey = "dotr-raid-cleared-";   // +OutpostId -> "1"

        /// <summary>Raised once the entire garrison is dead (the outpost is cleared).</summary>
        public event Action<EnemyOutpost> OnCleared;

        // -- Config -----------------------------------------------------------
        public RegionId Region { get; private set; } = RegionId.Goldfields;
        public int ThreatLevel { get; private set; }
        /// <summary>Stable id (region-based) used as the PlayerPrefs persistence key.</summary>
        public string OutpostId { get; private set; }

        // -- Runtime state ----------------------------------------------------
        /// <summary>True once the whole garrison is dead (or it was restored cleared).</summary>
        public bool Cleared { get; private set; }
        /// <summary>Living garrison members remaining (0 once cleared).</summary>
        public int AliveCount => _aliveCount;
        /// <summary>Total garrison members this outpost started with (boss + guards).</summary>
        public int TotalGarrison => _garrison.Count;

        private Transform _garrisonRoot;
        private readonly List<Enemy> _garrison = new List<Enemy>();
        private int _aliveCount;
        private bool _spawned;
        private bool _rewardPaid;

        /// <summary>Called by RaidOutpostSystem immediately after AddComponent.</summary>
        public void Configure(RegionId region, int threat)
        {
            Region = region;
            ThreatLevel = Mathf.Max(0, threat);
            OutpostId = "raid_" + region;
        }

        private void Start()
        {
            // Restore a previously-cleared raid: stay peaceful, no garrison, no fort
            // re-raise (the fight is over). A fresh raid spawns the fort + garrison.
            if (PlayerPrefs.GetString(PrefClearedKey + OutpostId, null) == "1")
            {
                Cleared = true;
                Debug.Log($"[EnemyOutpost] {OutpostId} restored as already CLEARED - peaceful.");
                return;
            }

            BuildFortification();
            SpawnGarrison();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _garrison.Count; i++)
                if (_garrison[i] != null) _garrison[i].Died -= HandleGarrisonDied;
        }

        // =====================================================================
        // FORTIFICATION - the WOOD visual (full reuse of OutpostFoundationGenerator).
        // =====================================================================

        private void BuildFortification()
        {
            // A small ~6x6 wood ring (perimeter walls + corner towers + one gate),
            // generated + realized through the SAME StructureFactory.Create path the
            // village build mode uses. LOCAL cell math against this root only - never
            // the village-scoped PlacementGrid / GameState.BaseLayout singletons.
            var recipe = OutpostFoundationGenerator.GenerateFootprintRecipe(
                FortGridWidth, FortGridDepth, OutpostTier.Wood);
            OutpostFoundationGenerator.Realize(recipe, transform, ~0);
        }

        // =====================================================================
        // GARRISON - boss + guards via EnemyFactory, tethered to the outpost.
        // Combat itself is FULL REUSE: these are real Enemy; the hero + party
        // auto-fight them via TargetManager. We write NO combat/targeting code.
        // =====================================================================

        private void SpawnGarrison()
        {
            if (_spawned) return;
            _spawned = true;

            _garrisonRoot = new GameObject("[Garrison]").transform;
            _garrisonRoot.SetParent(transform, false);
            _garrisonRoot.localPosition = Vector3.zero;

            // The BOSS holds the centre of the outpost (MiniBoss role).
            SpawnBoss();

            // The guard ring (charger/walker Enemy), threat-scaled count 5..8.
            int guards = BaseGuardCount + Mathf.Clamp(ThreatLevel / 2, 0, 3);
            for (int i = 0; i < guards; i++)
                SpawnGuard(i, guards);

            if (_aliveCount == 0)
            {
                // Nothing could spawn (no NavMesh / no roster) - treat as cleared so
                // the raid loop never deadlocks waiting on defenders that never existed.
                Debug.LogWarning($"[EnemyOutpost] no garrison spawned for {Region} outpost - auto-clearing.");
                Clear();
            }
        }

        private void SpawnBoss()
        {
            Vector3 pos = SnapToNav(transform.position);
            var def = BuildBossDef(ThreatLevel);

            var boss = EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot);
            if (boss == null) return;
            boss.gameObject.name = $"OutpostBoss ({Region})";

            var anchor = MakeAnchor("BossAnchor", pos);
            boss.Configure($"raidboss-{Region}", def, anchor);
            boss.SetBrainTarget(anchor);

            // The boss gets a brain in the MiniBoss role (tougher, holds the outpost).
            var brain = boss.gameObject.GetComponent<EnemyBrain>();
            if (brain == null) brain = boss.gameObject.AddComponent<EnemyBrain>();
            brain.Role = EnemyRole.MiniBoss;

            Track(boss);
        }

        private void SpawnGuard(int index, int count)
        {
            // A ring of stand positions around the outpost centre, NavMesh-snapped.
            float ang = (count > 0 ? (index / (float)count) : 0f) * Mathf.PI * 2f;
            Vector3 want = transform.position +
                           new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (GarrisonRing * 0.7f);
            Vector3 pos = SnapToNav(want);

            float depth = ZoneManager.Depth(transform.position);
            string enemyId = RegionSpawnTable.HasRoster(Region)
                ? RegionSpawnTable.PickEnemyId(Region, depth, UnityEngine.Random.value)
                : "orc-raider";
            if (string.IsNullOrEmpty(enemyId)) enemyId = "orc-raider";

            var def = BuildGuardDef(enemyId, ThreatLevel);

            var guard = EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot);
            if (guard == null) return;
            guard.gameObject.name = $"OutpostGuard ({enemyId} - {Region})";

            var anchor = MakeAnchor($"GuardAnchor-{index}", pos);
            guard.Configure($"raidguard-{Region}-{index}", def, anchor);
            guard.SetBrainTarget(anchor);

            Track(guard);
        }

        private void Track(Enemy e)
        {
            e.Died += HandleGarrisonDied;
            _garrison.Add(e);
            _aliveCount++;
        }

        private Transform MakeAnchor(string name, Vector3 pos)
        {
            // A local tether anchor so the defender HOLDS the outpost rather than
            // marching the Heart. Enemy.SetBrainTarget(anchor) overrides the Heart-
            // march; Enemy's own hero-aggro still pulls it into the fight on approach.
            var go = new GameObject(name);
            go.transform.SetParent(_garrisonRoot, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static Vector3 SnapToNav(Vector3 want)
        {
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, GarrisonRing + 6f, NavMesh.AllAreas))
                return hit.position;
            return want;
        }

        // =====================================================================
        // CLEAR - the last defender dies -> mark CLEARED, pay reward, persist.
        // =====================================================================

        private void HandleGarrisonDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleGarrisonDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0)
                Clear();
        }

        /// <summary>Mark the outpost cleared, pay the flat reward, and persist. Idempotent.</summary>
        public void Clear()
        {
            if (Cleared) return;
            Cleared = true;

            GrantClearReward();

            PlayerPrefs.SetString(PrefClearedKey + OutpostId, "1");
            PlayerPrefs.Save();

            Debug.Log($"[EnemyOutpost] {OutpostId} CLEARED - garrison wiped.");
            OnCleared?.Invoke(this);
        }

        // THREAT-SCALED LOOT TABLE: rolled once on clear, every drop routed to an
        // EXISTING system (no parallel loot-economy). Always pays resources +
        // crystals + XP; a chance (rising with threat) adds a GEAR drop; high-threat
        // outposts can also drop a rare gem (bonus crystals) and a quest token.
        // Idempotent - paid at most once per outpost.
        private void GrantClearReward()
        {
            if (_rewardPaid) return;
            _rewardPaid = true;

            var summary = new StringBuilder();

            // --- ALWAYS: resources -> EconomyService.Grant -------------------
            int wood = BaseLootWood + WoodPerThreat * ThreatLevel;
            int iron = BaseLootIron + IronPerThreat * ThreatLevel;
            if (EconomyService.Instance != null)
            {
                EconomyService.Instance.Grant(wood: wood, iron: iron);
                summary.Append($"{wood} wood, {iron} iron");
            }
            else
            {
                Debug.LogWarning("[EnemyOutpost] EconomyService null - resources not granted.");
            }

            // --- ALWAYS: crystals -> GameState.AetherCrystals (premium wallet) -
            int crystals = BaseClearCrystals + ThreatLevel * 10;

            // --- RARER (high threat): a rare GEM = bonus crystals ------------
            string gemNote = null;
            if (ThreatLevel >= RareGemThreatGate && UnityEngine.Random.value < RareGemChance)
            {
                int gem = RareGemCrystals + RareGemPerThreat * ThreatLevel;
                crystals += gem;
                gemNote = $"rare gem (+{gem} crystals)";
            }

            var state = GameStateService.Instance?.State;
            if (state != null)
            {
                state.AetherCrystals += crystals;
                GameStateService.Instance.ResourcesChanged.Invoke();
                summary.Append(summary.Length > 0 ? ", " : "").Append($"{crystals} crystals");
            }
            else
            {
                Debug.LogWarning("[EnemyOutpost] GameState null - clear crystals not banked.");
            }
            if (gemNote != null) summary.Append(", ").Append(gemNote);

            // --- ALWAYS: hero XP -> HeroProgression -------------------------
            int xp = BaseClearXp + ThreatLevel * 25;
            HeroProgression.Instance?.AddXp(xp);
            summary.Append(summary.Length > 0 ? ", " : "").Append($"{xp} XP");

            // --- CHANCE (rises with threat): a GEAR drop -> GearLoadout ------
            string gear = TryGrantGearDrop();
            if (gear != null) summary.Append(", [").Append(gear).Append("]");

            // --- RARER (high threat): a QUEST ITEM token --------------------
            // Clean hook = tick raid-clear quest PROGRESS (always). A literal
            // "grant quest item into inventory" has NO clean API (FLAGGED): the
            // closest tangible store is the item-drops larder, which only accepts
            // deposits when that lane is enabled - so the token is best-effort.
            DailyQuestService.Instance?.Report(QuestRaidEventId, 1);
            if (ThreatLevel >= QuestItemThreatGate && UnityEngine.Random.value < QuestItemChance)
            {
                DeNelle.Village.Items.ItemInventory.GrantDrop(QuestItemMaterial, 1); // no-op unless ItemDropSystem lane is on
                summary.Append(", [Warlord's Seal (quest)]");
            }

            Debug.Log($"[EnemyOutpost] Raid cleared ({OutpostId}, threat {ThreatLevel}) - looted: {summary}.");
        }

        // Roll the gear-drop chance (rising with threat); on a hit, pick a catalog
        // weapon OR armor at a threat-biased rarity that the hero qualifies for, and
        // grant it via the REAL armory API (GearLoadout.EquipWeaponById/EquipArmorById).
        // Returns the granted item's display name, or null if nothing dropped.
        private string TryGrantGearDrop()
        {
            float chance = Mathf.Min(GearDropChanceMax,
                GearDropChanceBase + GearDropChancePerTier * Mathf.Max(0, ThreatLevel));
            if (UnityEngine.Random.value > chance) return null;

            var hero = FindHeroLoadout();
            if (hero == null)
            {
                Debug.LogWarning("[EnemyOutpost] gear dropped but no hero GearLoadout - skipped.");
                return null;
            }

            string job   = hero.HeroClass;
            int    level = hero.HeroLevel;
            string targetRarity = RollGearRarity(ThreatLevel);

            // 50/50 weapon vs armor; fall back to the other type if the first yields none.
            bool wantWeapon = UnityEngine.Random.value < 0.5f;

            if (wantWeapon)
            {
                var w = PickWeapon(job, level, targetRarity);
                if (w != null) { hero.EquipWeaponById(w.id); return w.name; }
                var a = PickArmor(level, targetRarity);
                if (a != null) { hero.EquipArmorById(a.id); return a.name; }
            }
            else
            {
                var a = PickArmor(level, targetRarity);
                if (a != null) { hero.EquipArmorById(a.id); return a.name; }
                var w = PickWeapon(job, level, targetRarity);
                if (w != null) { hero.EquipWeaponById(w.id); return w.name; }
            }
            return null;
        }

        // Bias rarity UP with threat: low threat = mostly common/uncommon, high
        // threat = a real shot at rare/epic. Pure weighted roll over the catalog
        // rarity tiers; the actual eligible item is gated by the hero's job+level.
        private static string RollGearRarity(int threat)
        {
            // Weights shift toward higher tiers as threat rises.
            float t = Mathf.Clamp01(Mathf.Max(0, threat) / 10f);
            float wCommon   = Mathf.Lerp(0.55f, 0.10f, t);
            float wUncommon = 0.30f;
            float wRare     = Mathf.Lerp(0.12f, 0.35f, t);
            float wEpic     = Mathf.Lerp(0.03f, 0.25f, t);

            float total = wCommon + wUncommon + wRare + wEpic;
            float pick  = UnityEngine.Random.value * total;
            if ((pick -= wCommon)   <= 0f) return "common";
            if ((pick -= wUncommon) <= 0f) return "uncommon";
            if ((pick -= wRare)     <= 0f) return "rare";
            return "epic";
        }

        // Pick the eligible weapon nearest the target rarity (prefer exact rarity;
        // else the best the hero qualifies for). Returns null if the catalog is empty.
        private static WeaponDef PickWeapon(string job, int level, string rarity)
        {
            WeaponDef exact = null;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null) continue;
                if (!JobOk(w.job, job)) continue;
                if (w.req != null && level < w.req.level) continue;
                if (string.Equals(w.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || w.damageMult > exact.damageMult) exact = w;
                }
            }
            if (exact != null) return exact;
            return GearCatalog.BestWeapon(job, level); // fallback: best the hero qualifies for
        }

        private static ArmorDef PickArmor(int level, string rarity)
        {
            ArmorDef exact = null;
            foreach (var a in GearCatalog.AllArmors())
            {
                if (a == null) continue;
                if (a.req != null && level < a.req.level) continue;
                if (string.Equals(a.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || a.defense > exact.defense) exact = a;
                }
            }
            if (exact != null) return exact;
            return GearCatalog.BestArmor("any", level); // fallback (armor jobs are "any")
        }

        private static bool JobOk(string itemJob, string heroJob)
        {
            if (string.IsNullOrEmpty(itemJob)) return true;
            if (itemJob.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
            return itemJob.Equals(heroJob ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Locate the active hero's GearLoadout (the real armory grant surface),
        // mirroring ShopPanel.FindActiveHeroGO: Player tag -> add/get GearLoadout.
        private static HeroGearRef FindHeroLoadout()
        {
            GameObject heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return null;

            var loadout = heroGo.GetComponent<GearLoadout>();
            if (loadout == null) loadout = heroGo.AddComponent<GearLoadout>();

            var abilities    = heroGo.GetComponent<HeroAbilities>();
            var progression  = heroGo.GetComponent<HeroProgression>();
            return new HeroGearRef(loadout, abilities, progression);
        }

        // Small bundle so the loot roll can read job/level + grant gear through ONE
        // handle (the real GearLoadout equip API), without re-finding the hero.
        private sealed class HeroGearRef
        {
            private readonly GearLoadout _loadout;
            public string HeroClass { get; }
            public int    HeroLevel { get; }

            public HeroGearRef(GearLoadout loadout, HeroAbilities abilities, HeroProgression progression)
            {
                _loadout  = loadout;
                HeroClass = abilities != null ? abilities.HeroClass : AbilityCatalog.DefaultClass;
                HeroLevel = progression != null ? progression.Level : 1;
            }

            public void EquipWeaponById(string id) => _loadout?.EquipWeaponById(id);
            public void EquipArmorById(string id)  => _loadout?.EquipArmorById(id);
        }

        // =====================================================================
        // Stat blocks (code-built EnemyDef, threat-scaled). The boss is a tougher
        // tank/miniboss; the guards mirror CampGuards' synthesised roster so they
        // read the same as the rest of the open-world enemies.
        // =====================================================================

        private static EnemyDef BuildBossDef(int threat)
        {
            float scale = 1f + 0.12f * Mathf.Max(0, threat);
            var def = new EnemyDef
            {
                Id = "orc-warlord",
                Name = "Outpost Warlord",
                DisplayName = "Outpost Warlord",
                Ai = "charger",
                Hp = 420f * scale,
                MoveSpeed = 2.6f,
                ContactDamage = 22f * scale,
                AttackInterval = 1.4f,
                Height = 2.6f,
                AggroRadius = 16f,
                XpReward = 80 + threat * 5,
                GlimmerReward = 12,
            };
            return ApplyEarlyEase(def);
        }

        private static EnemyDef BuildGuardDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);

            string id = string.IsNullOrEmpty(enemyId) ? "orc-raider" : enemyId;
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    name = "Outpost Raider";  ai = "charger";    hp = 95f;  spd = 3.1f; dmg = 12f; interval = 1.3f; height = 2.0f; xp = 22; break;
                case "caveman":
                    name = "Outpost Brute";   ai = "walker";     hp = 70f;  spd = 2.7f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Outpost Hound";   ai = "skirmisher"; hp = 42f;  spd = 4.2f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Outpost Cultist"; ai = "skirmisher"; hp = 80f;  spd = 3.4f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Outpost Warden";  ai = "walker";     hp = 140f; spd = 2.2f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Outpost Guard";   ai = "walker";     hp = 60f;  spd = 3.0f; dmg = 8f;  interval = 1.3f; height = 1.8f; xp = 15; break;
            }

            var def = new EnemyDef
            {
                Id = id,
                Name = name,
                DisplayName = name,
                Ai = ai,
                Hp = hp * scale,
                MoveSpeed = spd,
                ContactDamage = dmg * scale,
                AttackInterval = interval,
                Height = height,
                AggroRadius = 14f,
                XpReward = xp + threat,
                GlimmerReward = 3,
            };
            return ApplyEarlyEase(def);
        }

        // Early-game ease (same ramp as CampGuards / RegionMobSpawner): a brand-new
        // player meets soft defenders (x0.35 HP/damage) that ramp to full by ~BestWave 6,
        // so the FIRST raid is a winnable power-fantasy and scales with progression.
        private static EnemyDef ApplyEarlyEase(EnemyDef def)
        {
            float ease = Mathf.Lerp(0.35f, 1f,
                Mathf.Clamp01((GameStateService.Instance?.State?.BestWave ?? 0) / 6f));
            def.Hp = Mathf.Max(1f, def.Hp * ease);
            def.ContactDamage = Mathf.Max(0f, def.ContactDamage * ease);
            return def;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Cleared
                ? new Color(0.2f, 0.9f, 0.4f, 0.35f)
                : new Color(0.9f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, GarrisonRing);
        }
#endif
    }
}
