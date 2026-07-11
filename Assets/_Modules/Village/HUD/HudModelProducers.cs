// =============================================================================
// HudModelProducers — WO-541 Stage 2: the per-model producers (DeNelle.Village).
// -----------------------------------------------------------------------------
// Each producer READS one or more live gameplay systems and WRITES exactly one
// Core HUD model (DeNelle.Core.HudModel). DARK / ADDITIVE — nothing reads the
// models yet (Stage 3), so these change zero runtime behaviour. Producers
// subscribe to a source event where a clean, stable one exists and otherwise
// poll (throttled by HudProducer); every write is change-gated so a model's
// Changed event + [Flow:HUD] trace only fire when a value actually changes.
//
// SOURCE MAP (each member verified against the real API — see the WO result):
//   HeroVitals  <- HeroHealth.Instance (Hp/MaxHp, OnHealthChanged),
//                  HeroAbilities (Mana/MaxMana/HeroClass),
//                  HeroProgression.Instance (Xp/XpToNext/Level, OnXpChanged)
//   Party       <- the hero (HeroHealth/HeroAbilities) + StoryCompanion.Active
//                  (the same roster PartyHudBridge feeds VillageHudController from)
//   Economy     <- EconomyService.Instance (Coins=Gold, Wood, Iron, Food, Crystals, OnChanged)
//   Wave        <- WaveManager.Instance (Phase, CurrentWaveId, CountdownRemaining, LiveEnemies)
//   Target      <- HeroTargetIndicator.CurrentTarget -> Enemy + EnemyBrain.Role
//   TargetCycle <- Enemy scan (FindObjectsByType) sorted by distance to the hero
//   Abilities   <- HeroLoadoutAccess.Current + AbilityCatalog + HeroAbilities cooldowns
//   World       <- HeartController (Hp, max=100) + Tower count (stubs noted below)
//   Momentum    <- BattleStarRating + a battle clock started when context = Battle
//   Echo        <- EchoService.Instance (EchoCount, MaxEchoes, Silo, FillFraction, Changed)
//
// Assembly law: writes Core models (Village -> Core, legal). No HUD reference.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;         // ConceptIconResolver — resolvable concept key for ability slot icons (F1)
using DeNelle.Village;
using DeNelle.Village.Arena;   // BattleStarRating
using DeNelle.Village.Crafting;
using DeNelle.Core.HUD;
using CoreWavePhase = DeNelle.Core.HudModel.WavePhase;

namespace DeNelle.Village.Hud
{
    // ── HeroVitals ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="HeroVitalsModel"/> from HeroHealth + HeroAbilities + HeroProgression
    /// + WisdomCurrencyService (P4: wisdom through the model — retires the HUD's reflection
    /// pull of the service, VillageHudController.cs:1486-1497).
    /// </summary>
    internal sealed class HeroVitalsProducer : HudProducer
    {
        private HeroAbilities _abilities;
        private HeroHealth _health;
        private HeroProgression _prog;
        // last-pushed snapshot for change-gating
        private int _hp = int.MinValue, _maxHp, _mana, _maxMana, _xp, _xpToNext, _level, _wisdom;
        private string _classId;

        public HeroVitalsProducer(IHudModel m) : base(m, 0.20f) { }

        protected override void Poll()
        {
            // HP-desync ticket 2026-07-02: FOLLOW HeroHealth.Instance, don't cache-forever. The old
            // "resolve only when null" left this producer bound to a stale-but-alive hero body when a
            // scene spawned a fresh hero (Instance moved) — the HUD then showed the untouched body's
            // pool (93/155) while the real body took the hits (92/100 in the capture). One authoritative
            // source: the CURRENT HeroHealth.Instance, and HeroAbilities from that SAME body.
            var inst = HeroHealth.Instance;
            if (inst != null && !ReferenceEquals(_health, inst)) { _health = inst; _abilities = null; }
            else if (_health == null || !_health) _health = inst;
            if ((_abilities == null || !_abilities) && _health != null)
                _abilities = _health.GetComponent<HeroAbilities>();
            if (_abilities == null || !_abilities) _abilities = Object.FindAnyObjectByType<HeroAbilities>();
            if (_prog == null || !_prog) _prog = HeroProgression.Instance;

            // No hero resolved yet -> leave the model at default (don't push zeros over a stale-but-valid value).
            if (_health == null && _abilities == null && _prog == null) return;

            int hp      = _health != null ? Mathf.CeilToInt(Mathf.Max(0f, _health.Hp)) : _hp;
            int maxHp   = _health != null ? Mathf.CeilToInt(Mathf.Max(1f, _health.MaxHp)) : _maxHp;
            int mana    = _abilities != null ? Mathf.RoundToInt(_abilities.Mana) : _mana;
            int maxMana = _abilities != null ? Mathf.RoundToInt(_abilities.MaxMana) : _maxMana;
            int xp      = _prog != null ? Mathf.RoundToInt(_prog.Xp) : _xp;
            int xpToNext= _prog != null ? Mathf.RoundToInt(_prog.XpToNext) : _xpToNext;
            int level   = _prog != null ? _prog.Level : _level;
            string cls  = _abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass) ? _abilities.HeroClass : (_classId ?? "knight");
            // P4: unspent Wisdom straight off the service (Village -> Village, no reflection).
            // Singleton is Bootstrap-created; keep the last value while it is not up yet.
            var wis = DeNelle.Village.Talents.WisdomCurrencyService.Instance;
            int wisdom = wis != null ? wis.Wisdom : Mathf.Max(0, _wisdom);

            if (hp == _hp && maxHp == _maxHp && mana == _mana && maxMana == _maxMana &&
                xp == _xp && xpToNext == _xpToNext && level == _level && cls == _classId &&
                wisdom == _wisdom) return;

            _hp = hp; _maxHp = maxHp; _mana = mana; _maxMana = maxMana;
            _xp = xp; _xpToNext = xpToNext; _level = level; _classId = cls; _wisdom = wisdom;
            Model.HeroVitals.Set(hp, maxHp, mana, maxMana, xp, xpToNext, level, cls, wisdom);
        }
    }

    // ── Party ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="PartyModel"/>: slot 0 = the hero, slots 1..3 = the live
    /// StoryCompanion roster (the same source PartyHudBridge feeds the HUD from).
    /// Companions are hidden when ff.singlehero is ON (matches PartyHudBridge).
    /// </summary>
    internal sealed class PartyProducer : HudProducer
    {
        private HeroHealth _health;
        private HeroAbilities _abilities;
        private string _signature;

        public PartyProducer(IHudModel m) : base(m, 0.40f) { }

        protected override void Poll()
        {
            // HP-desync ticket 2026-07-02: follow HeroHealth.Instance (see HeroVitalsProducer.Poll) —
            // slot 0 must show the SAME hero body the game is damaging, and its mana must come from
            // the HeroAbilities on that same body (stale-cached abilities = the frozen-at-full MP frame).
            var inst = HeroHealth.Instance;
            if (inst != null && !ReferenceEquals(_health, inst)) { _health = inst; _abilities = null; }
            else if (_health == null || !_health) _health = inst;
            if ((_abilities == null || !_abilities) && _health != null)
                _abilities = _health.GetComponent<HeroAbilities>();
            if (_abilities == null || !_abilities) _abilities = Object.FindAnyObjectByType<HeroAbilities>();

            var members = new List<PartyMemberRecord>(4);

            // Slot 0 — the controlled hero.
            if (_health != null || _abilities != null)
            {
                int hp = _health != null ? Mathf.CeilToInt(Mathf.Max(0f, _health.Hp)) : 0;
                int maxHp = _health != null ? Mathf.CeilToInt(Mathf.Max(1f, _health.MaxHp)) : 1;
                int mana = _abilities != null ? Mathf.RoundToInt(_abilities.Mana) : 0;
                int maxMana = _abilities != null ? Mathf.RoundToInt(_abilities.MaxMana) : 0;
                string cls = _abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass) ? _abilities.HeroClass : "knight";
                bool alive = _health == null || _health.IsAlive;
                members.Add(new PartyMemberRecord("Hero", cls, hp, maxHp, mana, maxMana, cls, alive, true));
            }

            // Slots 1..3 — story companions (hidden in single-hero mode).
            if (!FeatureFlags.SingleHero)
            {
                var roster = StoryCompanion.Active;
                int added = 0;
                for (int i = 0; i < roster.Count && added < 3; i++)
                {
                    var c = roster[i];
                    if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy) continue;
                    int max = c.MaxHp > 0f ? Mathf.CeilToInt(c.MaxHp) : 100;
                    int cur = Mathf.Clamp(Mathf.CeilToInt(c.Hp), 0, max);
                    string name = string.IsNullOrEmpty(c.DisplayName) ? "Companion" : c.DisplayName.Split(',')[0].Trim();
                    members.Add(new PartyMemberRecord(name, "companion", cur, max, 0, 0, name, cur > 0, true));
                    added++;
                }
            }

            // Change-gate on a cheap signature (count + each member's name/hp/maxHp).
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                sb.Append(m.Name).Append(':').Append(m.Hp).Append('/').Append(m.MaxHp).Append('|');
            }
            string sig = sb.ToString();
            if (sig == _signature) return;
            _signature = sig;
            Model.Party.SetMembers(members);
        }
    }

    // ── Economy ───────────────────────────────────────────────────────────────

    /// <summary>Fills <see cref="EconomyModel"/> from EconomyService (event + poll fallback).</summary>
    internal sealed class EconomyProducer : HudProducer
    {
        private EconomyService _econ;
        private bool _bound;
        private int _gold = int.MinValue, _wood, _iron, _food, _crystals;

        public EconomyProducer(IHudModel m) : base(m, 0.30f) { }

        protected override void Poll()
        {
            var econ = EconomyService.Instance;
            if (!ReferenceEquals(econ, _econ))
            {
                Unbind();
                _econ = econ;
                if (_econ != null) { _econ.OnChanged += OnEconChanged; _bound = true; }
            }
            Push();
        }

        private void OnEconChanged(ResourceSnapshot _) => Push();

        private void Push()
        {
            if (_econ == null) return;
            int gold = _econ.Coins, wood = _econ.Wood, iron = _econ.Iron, food = _econ.Food, crystals = _econ.Crystals;
            if (gold == _gold && wood == _wood && iron == _iron && food == _food && crystals == _crystals) return;
            _gold = gold; _wood = wood; _iron = iron; _food = food; _crystals = crystals;
            Model.Economy.Set(gold, wood, iron, food, crystals);
        }

        private void Unbind() { if (_bound && _econ != null) _econ.OnChanged -= OnEconChanged; _bound = false; }
        public override void Dispose() => Unbind();
    }

    // ── Wave ──────────────────────────────────────────────────────────────────

    /// <summary>Fills <see cref="WaveModel"/> from WaveManager (Phase maps to the Core WavePhase).</summary>
    internal sealed class WaveProducer : HudProducer
    {
        private const float ImminentThreshold = 5f; // countdown seconds at which a wave reads "imminent"

        private CoreWavePhase _phase = (CoreWavePhase)(-1);
        private int _number = -1, _live = -1, _total = -1;
        private float _countdown = -1f;
        private bool _imminent;

        public WaveProducer(IHudModel m) : base(m, 0.20f) { }

        protected override void Poll()
        {
            var wm = WaveManager.Instance;
            if (wm == null) return; // no wave loop in this scene -> leave model at default

            CoreWavePhase phase = MapPhase(wm.Phase);
            int number = Mathf.Max(0, wm.CurrentWaveId);
            float countdown = wm.CountdownRemaining;

            int live = 0, total = 0;
            var enemies = wm.LiveEnemies;
            if (enemies != null)
            {
                total = enemies.Count;
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i] != null && enemies[i].IsAlive) live++;
            }

            bool imminent = phase == CoreWavePhase.Countdown && countdown <= ImminentThreshold;

            // Change-gate (countdown bucketed to whole seconds so the timer doesn't churn every poll).
            int cdBucket = Mathf.CeilToInt(countdown);
            int lastCd = Mathf.CeilToInt(_countdown);
            if (phase == _phase && number == _number && live == _live && total == _total &&
                cdBucket == lastCd && imminent == _imminent) return;

            _phase = phase; _number = number; _live = live; _total = total;
            _countdown = countdown; _imminent = imminent;

            // LookoutStatus / Max / ClearBanner have no clean WaveManager source (the
            // schedule total + banner copy are not exposed) — left as neutral stubs.
            Model.Wave.Set(phase, number, 0, countdown, imminent, null, live, total, null);
        }

        private static CoreWavePhase MapPhase(DeNelle.Village.WavePhase p)
        {
            switch (p)
            {
                case DeNelle.Village.WavePhase.Countdown: return CoreWavePhase.Countdown;
                case DeNelle.Village.WavePhase.Active:    return CoreWavePhase.Active;
                case DeNelle.Village.WavePhase.Breached:  return CoreWavePhase.Breached;
                case DeNelle.Village.WavePhase.Complete:  return CoreWavePhase.Cleared;
                case DeNelle.Village.WavePhase.Defeated:  return CoreWavePhase.Defeated;
                default:                                  return CoreWavePhase.Idle;
            }
        }
    }

    // ── Target ────────────────────────────────────────────────────────────────

    /// <summary>Fills <see cref="TargetModel"/> from HeroTargetIndicator's locked target.</summary>
    internal sealed class TargetProducer : HudProducer
    {
        private HeroTargetIndicator _indicator;
        private bool _hadTarget;
        private string _sig;

        public TargetProducer(IHudModel m) : base(m, 0.15f) { }

        protected override void Poll()
        {
            if (_indicator == null || !_indicator) _indicator = Object.FindAnyObjectByType<HeroTargetIndicator>();

            IDamageable cur = _indicator != null ? _indicator.CurrentTarget : null;
            var curMb = cur as MonoBehaviour;
            var en = curMb != null ? curMb.GetComponentInParent<Enemy>() : null;

            if (cur == null || !cur.IsAlive || en == null)
            {
                if (_hadTarget) { _hadTarget = false; _sig = null; Model.Target.Clear(); }
                return;
            }

            var role = RoleOf(en);
            string name = FriendlyName(en, role);
            int hp = Mathf.CeilToInt(Mathf.Max(0f, en.Hp));
            int maxHp = Mathf.CeilToInt(Mathf.Max(1f, en.MaxHp));
            float frac = en.HpFraction;
            // F3 (WO-611): truthful level — read the enemy's REAL Level (authored per-def, stable),
            // not the old EnemyLevelStub HP/25 heuristic that crept upward as wave-scaling inflated
            // maxHp. Enemy.Level is derived from the authored def in Configure (see Enemy.Level).
            int level = en.Level;
            bool locked = _indicator != null && _indicator.LockEngaged;

            // Difficulty tell on the TARGET FRAME (mirrors the over-head ThreatSkullPlate).
            // The kit's Bind only forwards Name (extra=LOCKED) to the frame, so surface the
            // warning as a rich-text-coloured prefix on the name — gold "!" for caution,
            // red "!!" for danger — using the SAME owner-tunable ThreatSkullPlate thresholds
            // (TierFor) so the two surfaces always agree. ASCII glyph = font/WebGL-safe.
            int playerLevel = HeroProgression.Instance != null ? Mathf.Max(1, HeroProgression.Instance.Level) : 1;
            int threatTier = ThreatSkullPlate.TierFor(level, playerLevel);
            if (threatTier > 0)
                name = (threatTier >= 2 ? "<color=#FF3B30><b>!!</b></color> "
                                        : "<color=#FFD24A><b>!</b></color> ") + name;

            string sig = $"{name}|{level}|{hp}/{maxHp}|{role}|{locked}";
            if (sig == _sig) return;
            _sig = sig;
            _hadTarget = true;
            Model.Target.Set(true, name, level, hp, maxHp, frac, ToHudRole(role), locked);
        }
    }

    // ── TargetCycle ───────────────────────────────────────────────────────────

    /// <summary>Fills <see cref="TargetCycleModel"/> from the live enemies, distance-sorted to the hero.</summary>
    internal sealed class TargetCycleProducer : HudProducer
    {
        private HeroAbilities _hero;
        private string _sig;

        public TargetCycleProducer(IHudModel m) : base(m, 0.30f) { }

        protected override void Poll()
        {
            if (_hero == null || !_hero) _hero = Object.FindAnyObjectByType<HeroAbilities>();
            Vector3 me = _hero != null ? _hero.transform.position : Vector3.zero;

            var enemies = Object.FindObjectsByType<Enemy>();
            System.Array.Sort(enemies, (a, b) =>
            {
                if (a == null) return 1; if (b == null) return -1;
                return (a.transform.position - me).sqrMagnitude.CompareTo((b.transform.position - me).sqrMagnitude);
            });

            var list = new List<TargetRecord>(enemies.Length);
            for (int i = 0; i < enemies.Length; i++)
            {
                var en = enemies[i];
                if (en == null || en.IsDead) continue;
                var role = RoleOf(en);
                list.Add(new TargetRecord(en.GetInstanceID().ToString(), FriendlyName(en, role),
                                          en.HpFraction, ToHudRole(role), en.IsAlive));
            }

            // Change-gate on id+hp signature (HpFraction bucketed to avoid per-poll churn).
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
                sb.Append(list[i].Id).Append(':').Append(Mathf.RoundToInt(list[i].HpFraction * 20f)).Append('|');
            string sig = sb.ToString();
            if (sig == _sig) return;
            _sig = sig;
            Model.TargetCycle.SetTargets(list);
        }
    }

    // ── AbilityLoadout ────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="AbilityLoadoutModel"/> (4 slots Q/W/E/R). Q = the class basic
    /// attack (AbilityCatalog.Find); W/E/R = the equipped skill-tree ability via
    /// HeroLoadoutAccess + AbilityCatalog.FindById. Cooldowns from HeroAbilities.
    /// Mirrors BattleHud9Zone.ResolveSlotDef.
    /// </summary>
    internal sealed class AbilityLoadoutProducer : HudProducer
    {
        private HeroAbilities _abilities;
        private string _sig;

        public AbilityLoadoutProducer(IHudModel m) : base(m, 0.20f) { }

        protected override void Poll()
        {
            if (_abilities == null || !_abilities) _abilities = Object.FindAnyObjectByType<HeroAbilities>();

            string cls = _abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass) ? _abilities.HeroClass : "knight";
            var slots = new List<AbilitySlotRecord>(4);
            var sb = new System.Text.StringBuilder();
            List<string> unmapped = null;   // F8-33: equipped slots whose concept resolved NO real art

            for (int i = 0; i < 4; i++)
            {
                var slot = (AbilitySlot)i;
                string key = slot.ToString();
                AbilityDef def = ResolveSlotDef(slot, cls);
                bool equipped = def != null;
                float total = equipped ? def.Cooldown : 0f;
                float remaining = _abilities != null ? _abilities.CooldownRemaining(slot) : 0f;
                string name = equipped && !string.IsNullOrEmpty(def.Name) ? def.Name : key;
                // F1 (ABILITY_ICON_AUDIT_2026-07-05): do NOT forward def.Icon — that is the decorative
                // glyph ("✦"), which no concept-icons.json key matches, so every slot resolved null and
                // rendered blank (0/11). Store a RESOLVABLE concept key (abilityId, then effect) so
                // UiStyle.Icon(IconKey) downstream draws real RpgUi art; ResolveKey==null (nothing maps)
                // falls through to def.Effect/def.Id so the SetIcon default-sprite backstop still fills it.
                string resolvedKey = equipped ? ConceptIconResolver.ResolveKey(def.Id, def.Effect) : null;
                string icon = equipped ? (resolvedKey ?? def.Effect ?? def.Id) : null;
                // OWNER PLACEHOLDER (2026-07-11, verbatim): "for now instead of the heroic leap
                // image use word Dodge/Attack" — the Q medallion renders TEXT while the leap is
                // being rebound to a jump+stab dodge/attack move. In-band "text:" IconKey prefix;
                // HudKitController.OnAbilities routes it to ActionSlotHandle.SetLabel (words on the
                // standard chrome — meaning carried by text, never color alone). Remove this block
                // once the rebound ability ships its own icon.
                if (equipped && slot == AbilitySlot.Q && def.Id == "knight.q")
                {
                    icon = "text:Dodge/\nAttack";
                    resolvedKey = icon;   // deliberate text face — not an unmapped-icon fallback (F8-33)
                }
                // F8-33 (owner: right-side ability icons hard-coded/placeholder): a slot whose
                // concept did NOT resolve real art renders the SetIcon default backstop — that
                // fallback must never be silent. Collected here, warned once below on a loadout
                // signature change (Poll runs 5x/s — warning per poll would spam the log).
                if (equipped && resolvedKey == null)
                {
                    if (unmapped == null) unmapped = new List<string>(2);
                    unmapped.Add($"{key}='{def.Id ?? def.Name}' (effect='{def.Effect}')");
                }
                string accent = equipped ? def.Color : null;

                slots.Add(new AbilitySlotRecord(key, key, name, "", icon, accent, equipped, remaining, total));
                sb.Append(key).Append('=').Append(equipped ? name : "-")
                  .Append(':').Append(Mathf.CeilToInt(remaining)).Append('/').Append(Mathf.RoundToInt(total)).Append('|');
            }

            string sig = sb.ToString();
            if (sig == _sig) return;
            _sig = sig;
            // F8-33: name every ability that fell back to placeholder art — once per loadout
            // change, never per poll. No silent placeholder (CLAUDE.md §12 "no silent failures").
            if (unmapped != null)
                for (int i = 0; i < unmapped.Count; i++)
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("HudModel",
                        "ability icon unmapped — placeholder shown for " + unmapped[i] +
                        "; add a concept-icons.json entry to bind real art");
            Model.Abilities.SetSlots(slots);
        }

        private static AbilityDef ResolveSlotDef(AbilitySlot slot, string cls)
        {
            if (slot == AbilitySlot.Q) return AbilityCatalog.Find(cls, slot);
            var lo = HeroLoadoutAccess.Current;
            string id = lo != null ? lo.AbilityIdForSlot(slot) : null;
            // F8 2026-07-11 "where are the defaults for the action rails": empty loadout
            // slots fall back to the CLASS KIT (Bash/Charge/Radiant) — the legacy bridge
            // (HeroAbilitiesHudBridge.ResolveSlotDef:340) always did this; the v8 producer
            // dropped the line, leaving W/E/R blank in normal play.
            var eq = string.IsNullOrEmpty(id) ? null : AbilityCatalog.FindById(id);
            return eq ?? AbilityCatalog.Find(cls, slot);
        }
    }

    // ── WorldMetrics ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="WorldMetricsModel"/> from the Heart (Hp, max=100) + a live
    /// tower count. Population / passive-XP / forgetting / wards / minimap have no
    /// clean single source yet and are left as neutral stubs (noted in the WO result).
    /// </summary>
    internal sealed class WorldMetricsProducer : HudProducer
    {
        private const float HeartMaxHp = 100f;   // HeartController.Hp is 0..100 (HeartHudBridge.HeartMaxHp)

        private HeartController _heart;
        private int _heartHp = int.MinValue, _towers;

        public WorldMetricsProducer(IHudModel m) : base(m, 0.50f) { }

        protected override void Poll()
        {
            if (_heart == null || !_heart) _heart = Object.FindAnyObjectByType<HeartController>();
            if (_heart == null) return; // no Heart in this scene -> leave at default

            int heartHp = Mathf.CeilToInt(Mathf.Max(0f, _heart.Hp));
            int maxHp = Mathf.CeilToInt(HeartMaxHp);
            float pct = HeartMaxHp > 0f ? Mathf.Clamp01(_heart.Hp / HeartMaxHp) : 0f;

            var towersArr = Object.FindObjectsByType<Tower>();
            int towers = towersArr != null ? towersArr.Length : 0;

            if (heartHp == _heartHp && towers == _towers) return;
            _heartHp = heartHp; _towers = towers;

            // Stubs: TowersMax=0, Population=0, PassiveXpPerMin=0, PassiveTowerCount=towers,
            // ForgettingLevel=0, Wards 0/0 — no clean producer source for these yet.
            Model.World.SetMetrics(heartHp, maxHp, pct, towers, 0, 0, 0f, towers, 0, 0, 0, null);
        }
    }

    // ── Momentum ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="MomentumModel"/> from BattleStarRating + a battle clock started
    /// when the HudContext becomes Battle. Combo / KillStreak have no clean producer
    /// source yet (the HUD's SetComboCount is pushed by combat events not exposed as a
    /// pollable counter) and are left at 0 (noted in the WO result).
    /// </summary>
    internal sealed class MomentumProducer : HudProducer
    {
        private float _battleStart = -1f;
        private int _stars = int.MinValue, _elapsedBucket = -1;

        public MomentumProducer(IHudModel m) : base(m, 0.25f) { }

        protected override void Poll()
        {
            bool inBattle = Model.Context != null && Model.Context.CombatActive;
            if (!inBattle)
            {
                if (_battleStart >= 0f)
                {
                    _battleStart = -1f; _stars = 0; _elapsedBucket = 0;
                    Model.Momentum.Set(0, 0, 0, 0f, 0f);
                }
                return;
            }

            if (_battleStart < 0f) _battleStart = Time.time;
            float elapsed = Time.time - _battleStart;
            int stars = BattleStarRating.StarsForDuration(elapsed);

            // Seconds remaining before the next star threshold drops (keep-star countdown).
            float nextDrop = elapsed <= BattleStarRating.ThreeStarSeconds ? BattleStarRating.ThreeStarSeconds
                           : elapsed <= BattleStarRating.TwoStarSeconds ? BattleStarRating.TwoStarSeconds
                           : -1f;
            float keepStar = nextDrop < 0f ? 0f : Mathf.Max(0f, nextDrop - elapsed);

            int bucket = Mathf.FloorToInt(elapsed);
            if (stars == _stars && bucket == _elapsedBucket) return;
            _stars = stars; _elapsedBucket = bucket;
            Model.Momentum.Set(0, 0, stars, elapsed, keepStar);
        }
    }

    // ── Echo ──────────────────────────────────────────────────────────────────

    /// <summary>Fills <see cref="EchoModel"/> from EchoService (event + poll fallback).</summary>
    internal sealed class EchoProducer : HudProducer
    {
        private EchoService _echo;
        private bool _bound;
        private int _count = int.MinValue, _max;
        private float _silo = -1f, _fill = -1f;

        public EchoProducer(IHudModel m) : base(m, 0.40f) { }

        protected override void Poll()
        {
            var echo = EchoService.Instance;
            if (!ReferenceEquals(echo, _echo))
            {
                Unbind();
                _echo = echo;
                if (_echo != null) { _echo.Changed += Push; _bound = true; }
            }
            Push();
        }

        private void Push()
        {
            if (_echo == null) return;
            int count = _echo.EchoCount, max = _echo.MaxEchoes;
            float silo = (float)_echo.Silo, fill = _echo.FillFraction;
            if (count == _count && max == _max &&
                Mathf.Approximately(silo, _silo) && Mathf.Approximately(fill, _fill)) return;
            _count = count; _max = max; _silo = silo; _fill = fill;
            Model.Echo.Set(count, max, silo, fill);
        }

        private void Unbind() { if (_bound && _echo != null) _echo.Changed -= Push; _bound = false; }
        public override void Dispose() => Unbind();
    }

    // ── Cast (enemy telegraph) ────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="CastModel"/> from the Enemy rooted-cast seam (P4, HUD_OBSIDIAN
    /// §3.4): subscribes Enemy.CastStarted/CastEnded (push) and interpolates Progress01
    /// on its own fast poll, change-gated to 2% buckets so the model never fires on an
    /// unchanged value. One cast bar (V1): the LATEST cast wins; an ended earlier cast is
    /// ignored once superseded. SELF-EXPIRES on (start + windUp) or a dead/destroyed
    /// caster — a caster destroyed mid-cast kills its coroutine before CastEnded fires.
    /// </summary>
    internal sealed class CastProducer : HudProducer
    {
        private Enemy _caster;
        private string _ability;
        private float _start = -1f, _duration = 1f;
        private int _lastBucket = -1;
        private bool _visible;

        public CastProducer(IHudModel m) : base(m, 0.10f)
        {
            Enemy.CastStarted += OnCastStarted;
            Enemy.CastEnded += OnCastEnded;
        }

        private void OnCastStarted(Enemy caster, string ability, float windUpSeconds)
        {
            if (caster == null) return;
            _caster = caster;
            _ability = ability;
            _duration = Mathf.Max(0.05f, windUpSeconds);
            _start = Time.time;
            _lastBucket = -1;          // force the first push of the new cast
            Push(0f);
        }

        private void OnCastEnded(Enemy caster)
        {
            // Only the cast we are tracking may clear the bar (a superseded cast's
            // end event must not kill the newer cast's bar).
            if (!ReferenceEquals(caster, _caster)) return;
            Hide();
        }

        protected override void Poll()
        {
            if (_start < 0f) return;   // no live cast
            // Self-expiry: destroyed/dead casters end the coroutine without CastEnded.
            if (_caster == null || !_caster || _caster.IsDead) { Hide(); return; }
            float t = Mathf.Clamp01((Time.time - _start) / _duration);
            if (t >= 1f) { Hide(); return; }
            Push(t);
        }

        // Change gate: 2% progress buckets — a poll with an unchanged bucket writes nothing.
        private void Push(float t01)
        {
            int bucket = Mathf.RoundToInt(t01 * 50f);
            if (_visible && bucket == _lastBucket) return;
            _visible = true;
            _lastBucket = bucket;
            Model.Cast.Set(FriendlyName(_caster, RoleOf(_caster)), _ability, t01);
        }

        private void Hide()
        {
            _start = -1f;
            _caster = null;
            if (!_visible) return;     // change gate: never clear an already-clear model
            _visible = false;
            _lastBucket = -1;
            Model.Cast.Clear();
        }

        public override void Dispose()
        {
            Enemy.CastStarted -= OnCastStarted;
            Enemy.CastEnded -= OnCastEnded;
        }
    }

    // ── AssignableLoadout (WO-609) ────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="AssignableLoadoutModel"/> from the hero's
    /// <see cref="AssignableSkillBar"/> (4 hotswap slots) + extra cooldowns.
    /// </summary>
    internal sealed class AssignableLoadoutProducer : HudProducer
    {
        private HeroAbilities _abilities;
        private string _sig;

        public AssignableLoadoutProducer(IHudModel m) : base(m, 0.20f) { }

        protected override void Poll()
        {
            if (_abilities == null || !_abilities) _abilities = Object.FindAnyObjectByType<HeroAbilities>();
            var bar = AssignableSkillBarAccess.Current;

            var slots = new List<AbilitySlotRecord>(AssignableSkillBar.SlotCount);
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < AssignableSkillBar.SlotCount; i++)
            {
                string id = bar != null ? bar.AbilityIdForSlot(i) : null;
                bool equipped = !string.IsNullOrEmpty(id);
                AbilityDef def = equipped ? AbilityCatalog.FindById(id) : null;
                float total = def != null ? def.Cooldown : 0f;
                float remaining = equipped && _abilities != null ? _abilities.ExtraCooldownRemaining(id) : 0f;
                string name = def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : (equipped ? id : "—");
                // F1 (ABILITY_ICON_AUDIT_2026-07-05): store a RESOLVABLE concept key (abilityId, then
                // effect) — not the decorative glyph def.Icon — so the hot-swap slot draws real art.
                string icon = def != null
                    ? (ConceptIconResolver.ResolveKey(def.Id, def.Effect) ?? def.Effect ?? def.Id)
                    : null;
                string accent = def != null ? def.Color : null;
                string key = "A" + i;

                slots.Add(new AbilitySlotRecord(key, key, name, "", icon, accent, equipped, remaining, total));
                sb.Append(key).Append('=').Append(equipped ? name : "-")
                  .Append(':').Append(Mathf.CeilToInt(remaining)).Append('/').Append(Mathf.RoundToInt(total)).Append('|');
            }

            string sig = sb.ToString();
            if (sig == _sig) return;
            _sig = sig;
            Model.Assignable.SetSlots(slots);
        }
    }

    // ── ConsumableHotbar (WO-609) ─────────────────────────────────────────────

    /// <summary>Pushes battle potion counts from the village larder.</summary>
    internal sealed class ConsumableHotbarProducer : HudProducer
    {
        private int _hp = int.MinValue, _mana = int.MinValue;

        public ConsumableHotbarProducer(IHudModel m) : base(m, 0.50f) { }

        protected override void Poll()
        {
            var inv = VillageInventory.Instance;
            int hp = inv != null ? inv.Get(HudCommands.HpPotionId) : 0;
            int mana = inv != null ? inv.Get(HudCommands.ManaPotionId) : 0;
            if (hp == _hp && mana == _mana) return;
            _hp = hp;
            _mana = mana;
            Model.Consumables.Set(hp, mana);
        }
    }

    // ── StatusEffects (WO-609 Phase 2) ────────────────────────────────────────

    /// <summary>Fills player + locked-target status rows from combat status trackers.</summary>
    internal sealed class StatusEffectsProducer : HudProducer
    {
        private const int MaxIcons = 6;
        private static readonly List<ActiveStatusSnapshot> Scratch = new List<ActiveStatusSnapshot>(8);
        private static readonly List<StatusIconRecord> Icons = new List<StatusIconRecord>(8);

        private HeroTargetIndicator _indicator;
        private string _playerSig;
        private string _targetSig;

        public StatusEffectsProducer(IHudModel m) : base(m, 0.20f) { }

        protected override void Poll()
        {
            PollPlayer();
            PollTarget();
        }

        private void PollPlayer()
        {
            Scratch.Clear();
            Icons.Clear();
            var status = HeroCombatStatus.Current;
            status?.CollectActive(Scratch, MaxIcons);
            for (int i = 0; i < Scratch.Count; i++)
                Icons.Add(ToRecord(Scratch[i]));

            string sig = BuildSig(Icons);
            if (sig == _playerSig) return;
            _playerSig = sig;
            Model.PlayerStatus.SetIcons(Icons);
        }

        private void PollTarget()
        {
            if (_indicator == null || !_indicator) _indicator = Object.FindAnyObjectByType<HeroTargetIndicator>();

            Scratch.Clear();
            Icons.Clear();
            IDamageable cur = _indicator != null ? _indicator.CurrentTarget : null;
            var curMb = cur as MonoBehaviour;
            var dmg = curMb != null ? curMb.GetComponentInParent<EnemyDamageable>() : null;

            if (dmg != null && cur != null && cur.IsAlive)
                dmg.CollectActive(Scratch, MaxIcons);

            for (int i = 0; i < Scratch.Count; i++)
                Icons.Add(ToRecord(Scratch[i]));

            string sig = BuildSig(Icons);
            if (sig == _targetSig) return;
            _targetSig = sig;
            Model.TargetStatus.SetIcons(Icons);
        }

        private static StatusIconRecord ToRecord(ActiveStatusSnapshot s)
            => new StatusIconRecord(s.Id, s.Label, s.Id, s.IsBuff, s.RemainingSeconds, s.TotalSeconds);

        private static string BuildSig(IReadOnlyList<StatusIconRecord> icons)
        {
            if (icons == null || icons.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < icons.Count; i++)
            {
                var ic = icons[i];
                sb.Append(ic.Id).Append(':').Append(Mathf.CeilToInt(ic.RemainingSeconds)).Append('|');
            }
            return sb.ToString();
        }
    }
}
