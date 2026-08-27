// =============================================================================
// HudModelHost — WO-541 Stage 2: the Village-side host that owns the Core HUD
// model layer and ticks the PRODUCERS that fill it from live gameplay systems.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hud
//
// DARK / ADDITIVE (Stage 2): this constructs ONE DeNelle.Core.HudModel.HudModel,
// registers it via CoreServices.RegisterHudModel, and drives a set of producer
// objects that READ existing systems (HeroHealth, HeroAbilities, WaveManager,
// EconomyService, EchoService, …) and WRITE the Core models each tick. NOTHING
// reads the models yet (views migrate in Stage 3), so runtime behaviour is
// unchanged. Producers live in DeNelle.Village and write DeNelle.Core models —
// Village -> Core is legal (CLAUDE.md §5). The one HudContextEvaluator derives
// its signals from WaveManager (own assembly) + BattleLock / HubScenes /
// PanelManager (Core) + the Village hero position, so it NEVER touches the HUD
// assembly — no Village<->HUD edge is created.
//
// Self-bootstraps via [RuntimeInitializeOnLoadMethod] (mirrors EchoWorkforce /
// HeroEquipHud / HeroProgression). Single DDOL instance for the whole run.
//
// Every producer self-throttles its poll + change-gates its writes so a model's
// Changed event (and its [Flow:HUD] trace) only fires when a value actually
// changes. The model mutators already emit the [Flow:HUD] line per the frozen
// contract (WorkOrders/WO541_MODEL_API.md).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;

namespace DeNelle.Village.Hud
{
    /// <summary>
    /// DDOL host that constructs the Core <see cref="HudModel"/>, registers it with
    /// <see cref="CoreServices"/>, and ticks the Stage-2 producers that fill it from
    /// live Village/Core systems. Purely additive — nothing consumes the models yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudModelHost : MonoBehaviour
    {
        /// <summary>The single live host (one per run).</summary>
        public static HudModelHost Instance { get; private set; }

        private HudModel _model;
        private readonly List<HudProducer> _producers = new List<HudProducer>();

        // ── Bootstrap (always-on system, mirrors HeroProgression / EchoWorkforce) ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("HudModelHost");
            DontDestroyOnLoad(go);
            go.AddComponent<HudModelHost>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _model = new HudModel();
            CoreServices.RegisterHudModel(_model);

            // Context evaluator FIRST — it is the single context authority and other
            // producers (e.g. Momentum) read the context it writes.
            _producers.Add(new HudContextEvaluator(_model, transform));
            _producers.Add(new HeroVitalsProducer(_model));
            _producers.Add(new PartyProducer(_model));
            _producers.Add(new EconomyProducer(_model));
            _producers.Add(new WaveProducer(_model));
            _producers.Add(new TargetProducer(_model));
            _producers.Add(new TargetCycleProducer(_model));
            _producers.Add(new AbilityLoadoutProducer(_model));
            _producers.Add(new AssignableLoadoutProducer(_model));
            _producers.Add(new ConsumableHotbarProducer(_model));
            _producers.Add(new StatusEffectsProducer(_model));
            _producers.Add(new WorldMetricsProducer(_model));
            _producers.Add(new MomentumProducer(_model));
            _producers.Add(new EchoProducer(_model));
            _producers.Add(new CastProducer(_model));   // P4 — enemy cast telegraph -> CastModel

            FlowTrace.Step("HUD", $"HudModelHost up — {_producers.Count} producers ticking the Core HUD model (DARK).");
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _producers.Count; i++)
            {
                var p = _producers[i];
                if (p == null) continue;
                // One faulty producer must never wedge the rest (and must never throw
                // into the player). Guard each tick; a throw self-reports + is skipped.
                Guard.Try("HUD", "HudProducer.Tick:" + p.GetType().Name, () => p.Tick(dt));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _producers.Count; i++) _producers[i]?.Dispose();
            _producers.Clear();
            if (_model != null) CoreServices.UnregisterHudModel(_model);
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>
    /// Base for a single-model producer. Each producer READS one or more existing
    /// systems and WRITES exactly one Core model, self-throttling its poll and
    /// change-gating its writes. <see cref="Tick"/> is called every frame by the host
    /// with the unscaled delta; <see cref="Dispose"/> drops any event subscriptions.
    /// </summary>
    internal abstract class HudProducer
    {
        protected readonly IHudModel Model;
        private readonly float _interval;
        private float _timer;

        protected HudProducer(IHudModel model, float pollInterval)
        {
            Model = model;
            _interval = pollInterval;
        }

        /// <summary>Drives the producer; runs <see cref="Poll"/> at most every poll interval.</summary>
        public void Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _interval;
            Poll();
        }

        /// <summary>Read the source systems + write the model (change-gated by the producer).</summary>
        protected abstract void Poll();

        /// <summary>Drop event subscriptions / caches. Default no-op.</summary>
        public virtual void Dispose() { }

        // ── Shared helpers ────────────────────────────────────────────────────

        /// <summary>Maps the Village tactical <see cref="EnemyRole"/> to the Core HUD role.</summary>
        protected static HudRole ToHudRole(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Tank:   return HudRole.Tank;
                case EnemyRole.Ranged: return HudRole.Mage;   // ranged caster reads as Mage
                case EnemyRole.Healer: return HudRole.Mage;   // support caster
                default:               return HudRole.Warrior; // DPS / MiniBoss
            }
        }

        /// <summary>The enemy's tactical role (DPS when no brain present), mirrors BattleHud9Zone.RoleOf.</summary>
        protected static EnemyRole RoleOf(Enemy e)
        {
            if (e == null) return EnemyRole.DPS;
            var brain = e.GetComponent<EnemyBrain>();
            return brain != null ? brain.Role : EnemyRole.DPS;
        }

        /// <summary>
        /// A readable target name from the raw GameObject name (strips spawn prefixes +
        /// trailing index, Title-Cases, folds the duplicate role token). Mirrors the
        /// intent of BattleHud9Zone.FriendlyTargetName, kept compact.
        /// </summary>
        protected static string FriendlyName(Enemy en, EnemyRole role)
        {
            // Owner ticket ("Orc Mage Wizard" — two stacked titles): the CATALOG display
            // name is the single source of truth when present ("Orcish Mage"). The
            // GameObject-name parse below is only the fallback for def-less enemies.
            string catalog = en != null ? en.DisplayName : null;
            if (!string.IsNullOrEmpty(catalog)) return catalog;

            string raw = en != null ? en.name : null;
            if (string.IsNullOrEmpty(raw)) return RoleName(role);
            raw = raw.Replace("(Clone)", "").Trim();
            string[] prefixes = { "ArenaEnemy_", "encounter-", "encounter_", "Enemy_", "Enemy-" };
            foreach (var pre in prefixes)
            {
                if (raw.Length >= pre.Length &&
                    raw.Substring(0, pre.Length).ToLowerInvariant() == pre.ToLowerInvariant())
                {
                    raw = raw.Substring(pre.Length);
                    break;
                }
            }
            string roleLower = RoleName(role).ToLowerInvariant();
            var parts = raw.Split(new[] { '-', '_', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string w = parts[i];
                if (int.TryParse(w, out _)) continue;               // trailing _N index
                if (w.ToLowerInvariant() == roleLower) continue;    // role appended below
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w.Substring(1).ToLowerInvariant());
            }
            string family = sb.ToString().Trim();
            // ONE precise title, never two stacked ("Orc Mage" + "Wizard" was the bug):
            // the parsed family name stands alone; the role word only fills a blank.
            return string.IsNullOrEmpty(family) ? RoleName(role) : family;
        }

        protected static string RoleName(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Tank:     return "Tank";
                case EnemyRole.Healer:   return "Healer";
                case EnemyRole.Ranged:   return "Mage";
                case EnemyRole.MiniBoss: return "Boss";
                default:                 return "DPS";
            }
        }

        // WO-1232: EnemyLevelStub is DELETED, not repaired. It derived a "level" from the
        // RUNTIME maxHp (round-to-int of the runtime maxHp over 25) — the heuristic WO-611 F3 retired when
        // Enemy.Level landed — so it crept upward every wave as scaling inflated HP (a wave-7
        // enemy at 1700 HP read as "Lv 68" beside a Lv 5 hero). It had no callers left, so nothing
        // replaces it here — a stub reintroduced in any form is the same defect returning.
        //
        // ⚠ AND THE SECOND HALF, which the first pass got wrong: Enemy.Level is ITSELF round(def.Hp
        // / 25) — there is NO authored level field anywhere. So the owner's FINAL ruling (2026-08-26)
        // removed the number outright rather than re-pointing it: the target frame now shows the
        // AUTHORED classification word (DeNelle.Village.Hud.EnemyBadge — BOSS / ELITE / nothing).
        // Do not add a level accessor to this file, or to any producer, in any form.
    }
}
