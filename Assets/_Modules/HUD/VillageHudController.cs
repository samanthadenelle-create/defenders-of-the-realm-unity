// =============================================================================
// VillageHudController — THIN BOOTSTRAP + PUSH-SEAM ADAPTER (P23 total demolition).
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 A2: "nothing visual survives except the
// scaffolding". This file's 3,000 lines of widget construction are GONE.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// WHAT THIS NOW IS:
//   1. The per-scene HUD guarantor's target (VillageHudBootstrap spawns it) —
//      its only build act is HudKitController.Create(this): the factory-built,
//      model-bound, posture-occupied HUD kit (Kit/HudKitController.cs).
//   2. The COMMAND EVENT HOLDER: every UnityEvent the Village bridges subscribe
//      by reflection (BuildRequested, TalkRequested, StartWaveRequested,
//      AbilityRequested, ...) survives byte-for-byte so no bridge breaks.
//   3. The SURVIVING PUSH-SEAM ADAPTER: IVillageHud setters keep their exact
//      signatures (CoreServices.Hud consumers + reflection bridges), but DATA
//      setters are no-ops — the P4 producers (HudModelProducers.cs) already
//      push the same facts into Core.HudModel, which the kit binds. Only the
//      pushes with NO model source forward into the kit (repair prompt, wave
//      banner, start-wave availability, whole-HUD visibility, talk compat).
//
// §0 FIXES THAT LIVED IN THIS FILE, now structural:
//   • frozen party MP (:1878 fillAmount=1f) + SetMana no-op (:2906)  — the kit
//     binds HeroVitalsModel (§1.1 fill contract); this file draws nothing.
//   • resource red/green flash (:979/:293)                           — gone with
//     the widgets; CurrencyChip count-tweens by design (no flash exists).
//   • raid-"x" (:1343) / harvest-"Y" (:1350) glyph buttons           — DELETED
//     (earns-its-place: no verified backing surface; RaidRequested stays for
//     the bridge if a real entry point returns).
//   • runic border (:1226) + HudTheme dependency (all 78 uses)       — DELETED;
//     HudTheme.cs itself is removed from the project.
//   • wave-chrome bleed (ApplyContext :2389 / ApplyCombatGate :876)  — replaced
//     by hud-areas.json posture rows + WaveModel phase self-gating in the kit.
//   • talk availability (SetTalkAvailable :1671)                     — root cause
//     was TalkHudBridge's ONE-SHOT reflection hook onto this PER-SCENE component
//     (stale MethodInfo after a scene swap; MaxResolveAttempts exhausted; never
//     re-pushed). The push now rides the Core static PostureSignals.
//     SetTalkAvailable; this adapter forwards for any straggler caller.
//   • Pi sign-in on the HUD — verified Title-gated (PiSignInController); the
//     kit builds no Pi affordance.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;
using DeNelle.Core.HudModel;
using DeNelle.HUD.Kit;

namespace DeNelle.HUD
{
    /// <summary>Thin HUD bootstrap + push-seam adapter (see header). The visual HUD is
    /// the kit (<see cref="HudKitController"/>) — this component builds no widgets.</summary>
    public sealed class VillageHudController : MonoBehaviour, IVillageHud
    {
        /// <summary>Ability slot count (bridge contract).</summary>
        public const int AbilitySlotCount = 4;

        // ── Command events — the Village bridges' reflection contract (unchanged) ──
        public UnityEvent BuildRequested = new UnityEvent();
        public UnityEvent SkillsRequested = new UnityEvent();
        public UnityEvent ShopRequested = new UnityEvent();
        public UnityEvent TalkRequested = new UnityEvent();
        public UnityEvent InventoryRequested = new UnityEvent();
        public UnityEvent QuestsRequested = new UnityEvent();
        public UnityEvent IntelRequested = new UnityEvent();
        public UnityEvent RaidRequested = new UnityEvent();
        public UnityEvent RallyRequested = new UnityEvent();
        public UnityEvent RetreatRequested = new UnityEvent();
        public AbilitySlotEvent AbilityRequested = new AbilitySlotEvent();
        public UnityEvent RepairConfirmRequested = new UnityEvent();
        public UnityEvent RepairCancelRequested = new UnityEvent();
        public UnityEvent StartWaveRequested = new UnityEvent();

        /// <summary>Instance-independent BAG intent (bridges that outlive a scene HUD).</summary>
        public static event System.Action InventoryRequestedStatic;
        /// <summary>Raise the static BAG intent.</summary>
        public static void RaiseInventoryRequested() => InventoryRequestedStatic?.Invoke();

        /// <summary>UnityEvent&lt;int&gt; for ability-slot taps (bridge contract).</summary>
        [System.Serializable] public sealed class AbilitySlotEvent : UnityEvent<int> { }

        // ── State ─────────────────────────────────────────────────────────────
        private HudKitController _kit;
        private HudAreasHost _kitHost;
        private bool _villageOnlyForced;

        /// <summary>The whole-HUD fade group (legacy consumers; the kit host's group).</summary>
        public CanvasGroup TownHudGroup => _kitHost != null ? _kitHost.Group : null;

        /// <summary>Town-context readout (legacy consumers) — the ONE context model's view.</summary>
        public bool InVillage
        {
            get
            {
                if (_villageOnlyForced) return true;
                var hm = CoreServices.HudModel;
                return hm != null && hm.Context != null && hm.Context.Context == HudContext.Town;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Start()
        {
            CoreServices.RegisterHud(this);
            _kit = Guard.Try("HudKit", "build HUD kit", () => HudKitController.Create(this), null);
            _kitHost = _kit != null ? _kit.GetComponent<HudAreasHost>() : null;
            if (_kit == null)
                FlowTrace.Fail("HudKit", "HudKitController.Create returned null — HUD absent this scene");
            else
                FlowTrace.Step("HudKit", "VillageHudController bootstrapped the kit (scene '" +
                               gameObject.scene.name + "')");
        }

        private void OnDestroy()
        {
            CoreServices.UnregisterHud(this);
        }

        /// <summary>Legacy entrance-animation seam — the kit owns entrance presentation.</summary>
        public void AnimateIn() { }

        // =====================================================================
        // SURVIVING PUSH-SEAM ADAPTERS (forwarded into the kit / Core statics).
        // =====================================================================

        /// <summary>Talk availability (root-caused §0 fix — see header). Forwards to the
        /// Core static so ANY caller path lands on the same signal the kit binds.</summary>
        public void SetTalkAvailable(bool available) => PostureSignals.SetTalkAvailable(available);

        /// <summary>Start-Wave CTA availability (StartWaveHudBridge push).</summary>
        public void SetStartWaveAvailable(bool available)
        {
            if (_kit != null) _kit.SetStartWaveAvailable(available);
        }

        /// <summary>Repair prompt (WallRepairHudBridge push) — shared toast in the kit.</summary>
        public void ShowRepairPrompt(string wallLabel, float damagePercent)
        {
            if (_kit != null) _kit.ShowRepairToast(wallLabel, damagePercent);
        }

        /// <summary>Repair prompt dismiss — the toast self-expires; nothing to hide.</summary>
        public void HideRepairPrompt() { }

        /// <summary>Wave-clear banner push — shared toast (kills the old :2670 no-op).</summary>
        public void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            if (_kit != null) _kit.ShowWaveClearToast(waveNumber, enemiesDefeated, flavourLine);
        }

        /// <summary>Wave banner dismiss — the toast self-expires.</summary>
        public void HideWaveClearBanner() { }

        /// <summary>Whole-HUD visibility (cinematics/dialogue push).</summary>
        public void SetHudVisible(bool visible)
        {
            if (_kit != null) _kit.SetHudVisible(visible);
        }

        /// <summary>Legacy combat-gate push — the posture rows own combat visibility now (A4).</summary>
        public void SetCombatHudVisible(bool visible) { }

        /// <summary>Force town context (legacy raid/scene-setup push).</summary>
        public void SetVillageContextForced(bool forced) { _villageOnlyForced = forced; }

        // =====================================================================
        // DATA SETTERS — NO-OP ADAPTERS. Each fact already flows producer -> model
        // -> kit; the P4 producer named in each comment is the SINGLE binding
        // source. (The dual-fill-source seam — architecture risk #2 — is closed by
        // making these no-ops rather than double-driving the kit.)
        // =====================================================================

        public void SetWave(int waveNumber) { /* WaveProducer -> WaveModel */ }
        public void SetCountdown(float secondsRemaining) { /* WaveProducer -> WaveModel */ }
        public void SetWaveImminent(bool imminent) { /* WaveProducer -> WaveModel */ }
        public void SetWaveProgress(int current, int total) { /* WaveProducer -> WaveModel */ }
        public void SetLookoutStatus(int status) { /* WaveProducer -> WaveModel (stub source) */ }
        public void SetHeartHp(float current, float maxHp) { /* WorldMetricsProducer -> WorldMetricsModel */ }
        public void SetTownMetrics(float heartPct01, int towersBuilt, int towersMax, int population)
        { /* heart via WorldMetricsProducer; tower 0/0 + population came OFF (owner bottom-bar ruling) */ }
        public void SetCrystals(int amount) { /* EconomyProducer -> EconomyModel */ }
        public void SetGold(int amount) { /* EconomyProducer -> EconomyModel */ }
        public void SetResources(int wood, int iron, int food, int gems) { /* EconomyProducer -> EconomyModel */ }
        public void SetAttackDirections(bool north, bool east, bool south, bool west) { /* CompassHud component */ }
        public void SetForgettingLevel(float level01) { /* ward-tether presentation deferred (P23 report) */ }
        public void SetWardsReadout(int wardsLit, int wardsTotal, string summary) { /* Arcane Tower panel */ }
        public void SetPassiveXp(int xpPerMin, int towerCount) { /* earns-its-place: OFF until real+verified */ }
        public void SetPassiveXpVisible(bool visible) { /* earns-its-place: OFF */ }
        public void SetMinimapPoi(string kind, float worldX, float worldZ) { /* minimap deferred (P23 report) */ }
        public void ClearMinimapPois() { /* minimap deferred */ }
        public void SetComboCount(int count) { /* WO-563: momentum badge removed */ }
        public void SetKillStreak(int streak) { /* WO-563: momentum badge removed */ }
        public void SetEnemyCount(int live, int total) { /* WaveProducer -> WaveModel */ }
        public void SetMana(float current, float max) { /* HeroVitalsProducer -> HeroVitalsModel (MP LIVE in the kit) */ }
        public void SetHeroHp(float current, float max) { /* HeroVitalsProducer -> HeroVitalsModel */ }
        public void SetAbilityCooldown(int slot, float remaining, float total) { /* AbilityLoadoutProducer */ }
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description)
        { /* AbilityLoadoutProducer -> AbilityLoadoutModel */ }
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description, string accentHex)
        { /* AbilityLoadoutProducer -> AbilityLoadoutModel */ }
        public void SetPartyMember(int slot, string name, float current, float max) { /* PartyProducer -> PartyModel */ }
        public void SetPartyMemberVisible(int slot, bool visible) { /* PartyProducer -> PartyModel */ }
    }
}
