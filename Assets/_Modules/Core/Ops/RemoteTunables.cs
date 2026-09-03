// =============================================================================
// RemoteTunables - PROD-022, the database-backed knobs the Pi crash loop is
// bisected with. THE STATE AND THE PARSE. Transport lives in
// RemoteTunablesService.cs, exactly the way MaintenanceCatalog / MaintenanceService
// are split, and for exactly the same reason: this half stays headlessly drivable
// by a regression oracle with no network and no PlayMode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ops
//
// -----------------------------------------------------------------------------
// WHY THIS EXISTS AT ALL. Owner ruling 2026-09-02, verbatim:
//     "make the testing as robust as possible with as many solutions as
//      possible... all we really have to do is just flip a flag and possibly
//      redeploy"
// A WebGL rebuild costs about thirty minutes. PROD-022 is a P0 crash loop that
// reproduces on the owner's iPhone inside Pi Browser and NOWHERE ELSE - desktop
// Chrome ran the identical build for 62 minutes. So every candidate mitigation
// ships in ONE build, each behind its OWN independent knob, all defaulting to
// today's behaviour. The bisect is then flag flips against the database, at
// seconds per hypothesis instead of half an hour.
//
// -----------------------------------------------------------------------------
// ⛔ THE INVARIANT THAT OUTRANKS EVERYTHING ELSE IN THIS FILE:
//     NO ROW, NO NETWORK, NO PARSE, NO SERVER  =>  TODAY'S BEHAVIOUR, EXACTLY.
// -----------------------------------------------------------------------------
// Every default in Registry below is the value that is hardcoded in the shipping
// code TODAY. A player who cannot reach the API, whose fetch times out, who gets
// a 404, or who receives malformed JSON resolves EVERY knob to that default. The
// remote read is an OVERRIDE and never a dependency. This is the same fail-to-the-
// safe-ground-state shape as MaintenanceCatalog, and it is asserted rather than
// asserted-in-a-comment: RemoteTunablesService never blocks, never awaits at a
// call site, and every parse goes through Guard.
//
// ⚠ ONE HONEST EXCEPTION, STATED RATHER THAN HIDDEN (WO-1327): the two vfx.* knobs
// are BUG FIXES, so their defaults are the CORRECTED values, not the broken ones.
// An empty table gives you this build's fixed VFX collision and light budget, not
// the art pack's perfectly-elastic fireballs and 25 concurrent point lights. The
// invariant still holds in the form that matters - NO ROW => EXACTLY WHAT THIS
// BUILD HARDCODES - and the previous behaviour is one flip away
// (vfx.particleBouncePct=100, vfx.maxParticleLights=25) if the owner, who owns
// every VFX call, judges the new feel wrong.
//
// -----------------------------------------------------------------------------
// PRECEDENCE, and it composes with FeatureFlags rather than fighting it:
//     LOCAL PlayerPrefs "ff.tun.<key>"   (most specific - a human at the device)
//         beats REMOTE payload           (the owner at the database)
//             beats DEFAULT              (what this build hardcodes = today)
// FeatureFlags.Get already resolves PlayerPrefs-over-default for the ff.* family;
// this file inserts the remote layer BETWEEN those two and leaves ff.* untouched.
// The prefix is "ff.tun." and NOT plain "ff." on purpose - a tunable key and a
// FeatureFlags name must never be able to collide in one PlayerPrefs namespace.
//
// -----------------------------------------------------------------------------
// THE OWNER-FACING LIST IS docs/PROD022_TUNABLE_FLAGS.md. The Registry array
// below is the MACHINE-READABLE source of truth (key, kind, default, what ON
// does, which hypothesis it tests) and the doc is written from it. If you change
// one, change the other in the same commit - CLAUDE.md section 15.
//
// -----------------------------------------------------------------------------
// NO SILENT ANYTHING (CLAUDE.md section 12). Every resolve is traced ONCE per key
// with its value AND its provenance, and the whole configuration is printed on one
// line at service boot and again on every accepted payload - so a felt-test
// capture always says which configuration produced it. A session whose config
// cannot be reconstructed afterwards is a wasted session.
//
// ASCII only. FlowTrace tag "Tunables". Never strip it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;

namespace DeNelle.Core.Ops
{
    /// <summary>What a knob's value means on the wire.</summary>
    public enum TunableKind
    {
        /// <summary>0 / 1. Read with <see cref="RemoteTunables.Bool"/>.</summary>
        Bool = 0,
        /// <summary>A whole number. Read with <see cref="RemoteTunables.Int"/>.</summary>
        Int = 1,
    }

    /// <summary>
    /// One knob's contract. Immutable, authored in <see cref="RemoteTunables.Registry"/>,
    /// and printed verbatim into the trace so a reader never has to open this file to
    /// know what a flag does.
    /// </summary>
    public sealed class TunableSpec
    {
        /// <summary>Wire key. Lower camel, dotted, ASCII. Matches the client_tunables PK.</summary>
        public readonly string Key;

        /// <summary>Bool or Int.</summary>
        public readonly TunableKind Kind;

        /// <summary>THE SHIPPING VALUE. Bools are 0/1. This is today's behaviour, always.</summary>
        public readonly int Default;

        /// <summary>What turning it on (or raising it) actually does, in one sentence.</summary>
        public readonly string WhatOnDoes;

        /// <summary>Which PROD-022 hypothesis flipping it tests.</summary>
        public readonly string Hypothesis;

        public TunableSpec(string key, TunableKind kind, int def, string whatOnDoes, string hypothesis)
        {
            Key = key;
            Kind = kind;
            Default = def;
            WhatOnDoes = whatOnDoes;
            Hypothesis = hypothesis;
        }
    }

    /// <summary>
    /// Static, transport-free knob table. Always answers, never throws, and answers
    /// the shipping default for every question it cannot answer from data.
    /// </summary>
    public static class RemoteTunables
    {
        /// <summary>FlowTrace system tag for the whole tunables lane.</summary>
        public const string Sys = "Tunables";

        /// <summary>Payload schema version this build was written against.</summary>
        public const int PayloadVersion = 1;

        /// <summary>PlayerPrefs prefix for a LOCAL override. Deliberately not plain "ff.".</summary>
        public const string LocalPrefix = "ff.tun.";

        // Provenance literals. Also the values the oracle asserts, and the words that
        // appear in every trace line - "default" vs "remote" must never need inferring.
        public const string ProvenanceDefault = "default";
        public const string ProvenanceRemote = "remote";
        public const string ProvenanceLocal = "local-playerprefs";
        public const string ProvenanceCache = "remote-cached";

        // =====================================================================
        //  THE KEYS. One const per knob so no call site ever types a string.
        // =====================================================================

        /// <summary>Bool. Pi Browser runs the full desktop warm pass instead of on-demand.</summary>
        public const string KeyPiEagerStructureWarm = "pi.eagerStructureWarm";

        /// <summary>Bool. Pi awaits Addressables init + harvests keys before the first on-demand load.</summary>
        public const string KeyPiAwaitInitBeforeFirstLoad = "pi.awaitInitBeforeFirstLoad";

        /// <summary>Bool. Pi issues NO remote structure-art requests at all. The big hammer.</summary>
        public const string KeyPiDisableRemoteStructureArt = "pi.disableRemoteStructureArt";

        /// <summary>Int. Ceiling on concurrent residency requests. 0 = today (no explicit cap).</summary>
        public const string KeyAssetsMaxConcurrentRequests = "assets.maxConcurrentRequests";

        /// <summary>Int. The Pi Addressables per-request timeout, seconds.</summary>
        public const string KeyPiRequestTimeoutSeconds = "pi.requestTimeoutSeconds";

        /// <summary>Int. Async fetch attempts allowed per address per launch.</summary>
        public const string KeyAssetsMaxRequestAttempts = "assets.maxRequestAttempts";

        /// <summary>Int. VisualFactory resolve-miss escalate-then-throttle cap.</summary>
        public const string KeyVisualsMissLogCap = "visuals.missLogCap";

        /// <summary>Int. Verbosity of the [Flow:StructureAssets] / [Flow:VisualFactory] families.</summary>
        public const string KeyTraceAssetVerbosity = "trace.assetVerbosity";

        /// <summary>
        /// Int, PERCENT. How much of the damage a "drainshot" ability actually deals comes
        /// back to the caster as healing. 100 = today (heal == damage dealt).
        /// <para>
        /// ⭐ THIS ONE IS NOT A PROD-022 KNOB - it is a BALANCE knob, and it is here because
        /// the owner ruled that balance must move without a rebuild too (2026-09-02, verbatim:
        /// "be smart, dont make it need a code change, make it tweakable from a db call"). The
        /// rail is reused end to end rather than a second configuration mechanism being built
        /// - see the "no second bespoke mechanism" note in docs/PROD022_TUNABLE_FLAGS.md.
        /// </para>
        /// <para>
        /// The domain is EVERY drainshot ability, because HeroAbilities.HealFromDrain is the
        /// single owner of the drain heal - mage.siphon (the WO-1306 cost-1 base grant),
        /// mage.drain (the mage's stock E) and ranger.healing-shot all pass through it. It is
        /// therefore named combat.* and NOT mage.*: a mage-only knob would need a per-ability
        /// branch inside that one owner, which is the second mechanism this rule forbids.
        /// </para>
        /// </summary>
        /// <remarks>
        /// ⛔ 60, NOT 100, AND THAT IS A DELIBERATE DEPARTURE FROM THIS FILE'S OWN RULE.
        /// Do NOT "correct" it back. Owner ruling 2026-09-02, verbatim: <i>"keep drain at
        /// 60% for now"</i>, with the design intent she gave in the same breath: <i>"drain
        /// should help stave off not run the show"</i>.
        /// <para>
        /// Every other Default in the Registry below is the value the shipping code
        /// hardcoded, so that an empty table reproduces today's behaviour byte for byte.
        /// This one is a RULED BALANCE VALUE, so an empty table gives the drain the OWNER
        /// chose rather than the 100 percent WO-1306 shipped. Her ruling outranks the
        /// convention - the convention exists to stop a default DRIFTING silently, and a
        /// value she stated out loud is the opposite of drift. The invariant that still
        /// binds unchanged: no row, no network, no parse => EXACTLY WHAT THIS BUILD
        /// HARDCODES, with the remote read an override and never a dependency. The WO-861
        /// identity "heal == damage dealt" is now reachable by setting the row to 100.
        /// (Same shape as the two vfx.* knobs' exception, recorded in this file's header.)
        /// </para>
        /// </remarks>
        public const int DrainReturnPctDefault = 60;

        /// <summary>Int percent. Share of damage DEALT that a drainshot returns as healing.</summary>
        public const string KeyCombatDrainReturnPct = "combat.drainReturnPct";

        // ---------------------------------------------------------------------
        //  WO-1330 - THE OVER-TIME BALANCE RAIL. Three knobs, not six.
        //
        //  The ticket named three levers - tick MAGNITUDE, tick INTERVAL and
        //  DURATION - and required them tunable rather than hardcoded. They are
        //  registered ONCE and SHARED by every over-time effect of EITHER sign,
        //  because "how often does an over-time effect pulse" is the same concept
        //  whether the pulse hurts or mends. Per-ability duplicates were explicitly
        //  rejected by the work order ("Prefer ONE shared knob over per-ability
        //  duplicates where it is genuinely the same concept") and would also have
        //  had to be re-registered for every future DoT anyone authors.
        //
        //  ⭐ ALL THREE DEFAULTS ARE TODAY'S BEHAVIOUR, and this is checkable rather
        //  than asserted: 1000 ms is exactly the "const float tick = 1f" that both
        //  HeroAbilities.BurnDoT and HeroAbilities.PoisonDoT hardcoded before this
        //  work, and 100 percent is identity on the other two. An empty table, a
        //  404, a malformed row and an offline player therefore all reproduce the
        //  shipped mage.poison and knight.emberbrand-throw numbers exactly.
        //
        //  The consumer is DeNelle.Core.Combat.OverTimeTuning, which owns the
        //  clamps and is the ONLY reader - see that file for why each clamp exists.
        // ---------------------------------------------------------------------

        /// <summary>Milliseconds between over-time pulses. 1000 = today.</summary>
        public const int OverTimeTickMsDefault = 1000;

        /// <summary>Percent scale on every over-time pulse's magnitude. 100 = today.</summary>
        public const int OverTimeMagnitudePctDefault = 100;

        /// <summary>Percent scale on every over-time effect's duration. 100 = today.</summary>
        public const int OverTimeDurationPctDefault = 100;

        /// <summary>Int, MILLISECONDS between over-time pulses (both signs).</summary>
        public const string KeyCombatOverTimeTickMs = "combat.overTimeTickMs";

        /// <summary>Int, PERCENT scale on over-time pulse magnitude (both signs).</summary>
        public const string KeyCombatOverTimeMagnitudePct = "combat.overTimeMagnitudePct";

        /// <summary>Int, PERCENT scale on over-time effect duration (both signs).</summary>
        public const string KeyCombatOverTimeDurationPct = "combat.overTimeDurationPct";

        /// <summary>
        /// Int, PERCENT. Restitution allowed on a WORLD-COLLIDING particle inside any VFX host
        /// the pooled spawner checks out. 0 = THIS BUILD'S DEFAULT: a particle that hits scene
        /// geometry stops there and terminates. 100 = leave the art pack's authored collision
        /// completely alone.
        /// <para>
        /// ⭐ NOT a PROD-022 knob and NOT a balance knob - a FEEL/PERF knob (WO-1327). It exists
        /// because the offending numbers live in a PREFAB inside a GITIGNORED art pack
        /// (<c>Assets/Spells Pack/</c>), so a hand-edit to that prefab is unreviewable,
        /// uncommittable, and erased by the next re-import. The clamp therefore lives at the ONE
        /// spawn owner (<c>VFXManager</c>) and rides this rail, exactly as the 2026-09-02 standing
        /// rule requires of a feel value.
        /// </para>
        /// <para>
        /// The clamp only ever TIGHTENS: bounce is lowered toward the cap, dampen and lifetime-loss
        /// are raised toward its complement. It can never make an effect bouncier than its author
        /// made it, so setting 100 is a true "do nothing".
        /// </para>
        /// </summary>
        public const int VfxParticleBouncePctDefault = 0;

        /// <summary>Int percent. Restitution ceiling for world-colliding VFX particles.</summary>
        public const string KeyVfxParticleBouncePct = "vfx.particleBouncePct";

        /// <summary>
        /// Int COUNT. Ceiling on the total concurrent real-time point lights ONE spawned VFX host
        /// may drive through its ParticleSystem LightsModules, summed across every emitter on the
        /// host. 4 = THIS BUILD'S DEFAULT. 0 turns particle lights off outright; a number at or
        /// above a host's authored total leaves that host untouched.
        /// <para>
        /// ⭐ Also WO-1327, and for the same reason: <c>Spell_Fire_9</c> drives 20 lights from its
        /// <c>Fireballs</c> emitter plus 5 from its <c>Explosion</c> sub-emitter - TWENTY-FIVE
        /// real-time point lights per cast, on a phone - and the dial is baked into a gitignored
        /// prefab. The budget is spent EVENLY across the host's enabled modules and each module's
        /// <c>ratio</c> is scaled down with it, so the lights stay spread across the effect instead
        /// of all sticking to the first few particles.
        /// </para>
        /// <para>⛔ This never deletes a light PROTOTYPE. The prototype is what the module clones
        /// from; removing it breaks the effect instead of tuning it.</para>
        /// </summary>
        public const int VfxMaxParticleLightsDefault = 4;

        /// <summary>Int count. Concurrent particle-driven real-time lights allowed per VFX host.</summary>
        public const string KeyVfxMaxParticleLights = "vfx.maxParticleLights";

        // ---------------------------------------------------------------------
        //  WO-1343 - THE NIGHT STORE'S AURA. Four knobs, and they exist because
        //  the owner asked a QUESTION SHE HAS EXPLICITLY NOT ANSWERED.
        //
        //  She tagged NightStoreoption_Aura (top_down_starfall_line_blue) for the
        //  Night Store; then tagged a SECOND candidate, Store_Aura (Loot_flicker),
        //  saying verbatim "i added another option for REalm store, not sure which
        //  will be best"; and separately asked "can we use these [the seven Aura_*
        //  spells] slowly one after another instead at the night store IF THE OTHER
        //  ONE DOESNT LOOK GOOD". Every one of those is a creative call conditional
        //  on device feel, so ALL of it ships in one build and the choice is a row,
        //  per her standing 2026-09-02 ruling: "be smart, dont make it need a code
        //  change, make it tweakable from a db call" / "i have been screaming this
        //  for months."
        //
        //  (S) HER FIRST PICK IS THE DEFAULT AND ROTATION SHIPS OFF. Mode 0 = the
        //  first key she tagged, played verbatim, pulsed every 30 minutes. An empty
        //  table, a 404, a malformed row and an offline player therefore all get
        //  exactly that. Nothing here promotes her second candidate or the family -
        //  choosing between them is the decision she reserved for herself.
        //
        //  (!) THE CADENCE MEANS TWO DIFFERENT THINGS, because the candidates are
        //  two different KINDS of effect (MEASURED, not assumed): both of her store
        //  tags are one-shot BURSTS (all ParticleSystems looping:0) while the Aura_*
        //  family is CONTINUOUS (looping:1). So in a burst mode the cadence RE-FIRES
        //  the burst, and in rotate mode it ADVANCES to the next aura. The consumer
        //  reports which meaning is live on every trace line.
        //
        //  The rotation membership is a BITMASK rather than a string list on
        //  purpose: it rides the existing integer rail instead of growing the
        //  tunables a new value kind, and "take that one out of the rotation" is
        //  still a single number with no code change and no schema change.
        //
        //  The consumer is DeNelle.Village.NightStoreAuraSelector, which owns the
        //  clamps and is the ONLY reader. Nothing here picks a prefab: the family
        //  names are a directory listing of the folder she screenshotted, and an
        //  untagged member is SKIPPED BY NAME rather than substituted.
        // ---------------------------------------------------------------------

        /// <summary>0 = TaggedStarfall (SHIPPED, her first pick). 1 = TaggedLootFlicker
        /// (her second candidate). 2 = RotateFamily. 3 = LegacyBeaconRing.</summary>
        public const int VfxNightStoreAuraModeDefault = 0;

        /// <summary>Minutes between night-store aura cadence ticks. Her "every 30~min".</summary>
        public const int VfxNightStoreAuraCadenceMinDefault = 30;

        /// <summary>Bitmask over the seven Aura_* prefabs, folder order. 127 = all seven.
        /// Written in DECIMAL deliberately: tools/gen-tunable-manifest.mjs resolves a default
        /// const with a decimal-only regex, so a hex literal here would fail the generator.</summary>
        public const int VfxNightStoreAuraFamilyMaskDefault = 127;

        /// <summary>Seconds between EXTRA burst re-fires inside one cadence period.
        /// 0 = OFF, which is her spec read literally: one burst per cadence tick.</summary>
        public const int VfxNightStoreAuraBurstRepeatSecDefault = 0;

        /// <summary>Int enum. What drives the Night Store's aura seat:
        /// 0 tagged-starfall / 1 tagged-lootflicker / 2 rotate-family / 3 legacy-ring.</summary>
        public const string KeyVfxNightStoreAuraMode = "vfx.nightStoreAuraMode";

        /// <summary>Int minutes. Cadence of the night-store aura tick.</summary>
        public const string KeyVfxNightStoreAuraCadenceMin = "vfx.nightStoreAuraCadenceMin";

        /// <summary>Int bitmask. Which Aura_* family members the rotation may select.</summary>
        public const string KeyVfxNightStoreAuraFamilyMask = "vfx.nightStoreAuraFamilyMask";

        /// <summary>Int seconds. Extra burst re-fire period inside one cadence. 0 = off.</summary>
        public const string KeyVfxNightStoreAuraBurstRepeatSec = "vfx.nightStoreAuraBurstRepeatSec";

        // ---------------------------------------------------------------------
        //  Verbosity levels for KeyTraceAssetVerbosity.
        //
        //  ⛔ THERE IS NO "OFF". CLAUDE.md section 12 is binding: instrumentation is
        //  PERMANENT, and a Warn or a Fail that stops being emitted turns a logged
        //  failure back into a silent one. This knob only ever moves the STEP lines -
        //  the narration - and every level below still emits Warn and Fail in full.
        // ---------------------------------------------------------------------

        /// <summary>Failures and warnings only. No Step narration.</summary>
        public const int VerbosityQuiet = 0;

        /// <summary>Failures, warnings, and the lifecycle Steps that name a decision.</summary>
        public const int VerbosityNormal = 1;

        /// <summary>TODAY'S BEHAVIOUR. Every Step, including the per-request narration.</summary>
        public const int VerbosityVerbose = 2;

        /// <summary>
        /// THE REGISTRY. Every knob, its shipping default, what turning it on does, and
        /// which PROD-022 hypothesis it tests.
        /// <para>
        /// ⭐ EVERY Default HERE IS THE VALUE THE SHIPPING CODE USED BEFORE PROD-022
        /// TOUCHED IT. That is not a convention, it is the acceptance criterion: a build
        /// with an empty client_tunables table must behave byte-for-byte like the build
        /// before this work. The pairs are checked against their real owners in
        /// StructureContentWarmer.cs and VisualFactory.cs, which read them through this
        /// file and nowhere else.
        /// </para>
        /// </summary>
        public static readonly TunableSpec[] Registry =
        {
            new TunableSpec(KeyPiEagerStructureWarm, TunableKind.Bool, 0,
                "Pi Browser runs the FULL desktop warm pass (await Addressables init, harvest keys, " +
                "DownloadDependenciesAsync, then load and retain all 35 structure prefabs) instead of " +
                "the on-demand policy.",
                "That on-demand streaming is itself the problem and eager residency is the healthier " +
                "shape on this webview. Deliberately shipped OFF: WO-PROD-022 forbids re-enabling eager " +
                "residency WITHOUT PROOF, and this knob is how the proof is gathered rather than assumed."),

            new TunableSpec(KeyPiAwaitInitBeforeFirstLoad, TunableKind.Bool, 0,
                "Pi Browser awaits Addressables.InitializeAsync and harvests every registered key BEFORE " +
                "the first on-demand LoadAssetAsync is issued; requests raised in the meantime are queued " +
                "and drained when init lands. Residency policy is otherwise untouched (this is NOT the " +
                "eager warm).",
                "PRIME SUSPECT. Today the Pi branch of StructureContentWarmer.Boot returns without ever " +
                "awaiting init and without harvesting keys, so the FIRST on-demand request is the first " +
                "thing that touches the catalog, and State is Degraded from frame one - which makes " +
                "IsSettled TRUE immediately, so a WhenSettled retry can fire before a single location " +
                "exists. That is the shape of the observed 'model not found' storm."),

            new TunableSpec(KeyPiDisableRemoteStructureArt, TunableKind.Bool, 0,
                "Pi Browser issues NO remote structure-art request at all. Every caller keeps the path it " +
                "already takes when an asset is not resident - the baked twin or the pending-art proxy - " +
                "so the town still renders and nothing stalls or blanks.",
                "THE BIG HAMMER, and it is diagnostically decisive in BOTH directions. If the crash loop " +
                "STOPS with this on, asset streaming is implicated beyond argument. If it CONTINUES, " +
                "streaming is exonerated and the cause is elsewhere - which is worth just as much. It " +
                "trades visual fidelity for a clean signal, on purpose."),

            new TunableSpec(KeyAssetsMaxConcurrentRequests, TunableKind.Int, 0,
                "Caps how many residency fetches may be in flight at once, on every host. 0 = TODAY: Pi " +
                "serialises through its own latch and desktop is unbounded. 1 or more installs an explicit " +
                "shared queue with that ceiling.",
                "That several simultaneous multi-MB bundle downloads plus decompression blow a memory " +
                "ceiling that lives OUTSIDE the managed heap - which is exactly how the captured sessions " +
                "look, dying with mem=247MB flat and no exception."),

            new TunableSpec(KeyPiRequestTimeoutSeconds, TunableKind.Int, 20,
                "The UnityWebRequest timeout installed by the Pi Addressables WebRequestOverride.",
                "That 20s is the wrong bound - too long, so a stalled fetch holds the queue past the " +
                "30-60s lifetime we are trying to survive; or too short, so a slow-but-healthy fetch is " +
                "killed and retried. Untunable today, and the WO forbids GUESSING a new constant - so it " +
                "ships at 20 and moves only on data."),

            new TunableSpec(KeyAssetsMaxRequestAttempts, TunableKind.Int, 3,
                "How many async fetch attempts one address gets before it is retired for the launch.",
                "That the retry budget is mis-sized: too high and the retry storm itself is the load that " +
                "kills the tab; too low and one transient webview stall costs a building its art for the " +
                "whole session."),

            new TunableSpec(KeyVisualsMissLogCap, TunableKind.Int, 3,
                "How many full resolve-miss Fail lines VisualFactory emits per address before it " +
                "announces its cap and drops to a throttled line. It NEVER goes silent.",
                "That trace VOLUME is itself a contributor - the observed final seconds were nothing but " +
                "the same four addresses cycling, and every line is a remote trace POST from a device " +
                "that is already the suspect."),

            new TunableSpec(KeyTraceAssetVerbosity, TunableKind.Int, VerbosityVerbose,
                "Narration level for the [Flow:StructureAssets] and [Flow:VisualFactory] families. " +
                "2 = today (every Step). 1 = lifecycle Steps only. 0 = no Steps. Warn and Fail are " +
                "emitted at EVERY level and cannot be turned off.",
                "Same volume hypothesis as the miss-log cap, but separable: this one silences the " +
                "SUCCESS narration while leaving every failure line intact, so a quiet-but-still-" +
                "diagnostic session can be compared against a loud one."),

            new TunableSpec(KeyCombatDrainReturnPct, TunableKind.Int, DrainReturnPctDefault,
                "Percent of the damage a drainshot ability ACTUALLY DEALS that comes back to the caster " +
                "as healing. 100 = TODAY: heal == damage dealt, exactly. Applies to every drainshot - " +
                "mage.siphon, mage.drain and ranger.healing-shot - because HeroAbilities.HealFromDrain " +
                "is the single owner of the drain heal. Clamped to 0..1000 at the consumer.",
                "NOT a PROD-022 hypothesis - this is the BALANCE lever for WO-1306. The owner ruled the " +
                "mage's first talent point must buy a castable that SUSTAINS ('the blm needs to get some " +
                "healing , like drain to stay balanced (early)') and then that the strength must move " +
                "without a rebuild ('be smart, dont make it need a code change, make it tweakable from a " +
                "db call'). 100 is the value the shipped resolver hardcoded, so an offline player, a 404 " +
                "and an empty table all get exactly the drain that shipped; the knob only ever moves it " +
                "on her word."),

            new TunableSpec(KeyVfxParticleBouncePct, TunableKind.Int, VfxParticleBouncePctDefault,
                "Percent restitution allowed on a WORLD-COLLIDING particle inside any VFX host the pooled " +
                "spawner checks out. 0 = THIS BUILD: a particle that hits scene geometry stops there and " +
                "terminates (bounce 0, dampen 1, lifetime-loss 1). 100 = leave the art pack's authored " +
                "collision untouched. The clamp only ever tightens, so it can never make an effect bouncier " +
                "than its author made it.",
                "NOT a PROD-022 hypothesis - a FEEL knob (WO-1327). Spell_Fire_9's Fireballs emitter is " +
                "authored bounce 1.0 / dampen 0 / minKillSpeed 0 against ALL 32 LAYERS at High quality: " +
                "perfectly elastic, nothing ever kills the particle. Cast inside a walled town that is a " +
                "projectile in a box, and the owner reported the fire spell 'casts at me and stays at me' " +
                "twice (F8 seq 4152, 4644). Those numbers live in a GITIGNORED pack prefab, so the clamp " +
                "has to live at the spawn owner; this knob is how the owner moves it without a rebuild, and " +
                "how she puts the authored behaviour back in one word if the new feel is wrong."),

            new TunableSpec(KeyVfxMaxParticleLights, TunableKind.Int, VfxMaxParticleLightsDefault,
                "Ceiling on the total concurrent real-time point lights ONE spawned VFX host may drive " +
                "through its ParticleSystem LightsModules, summed across every emitter on that host. 4 = " +
                "THIS BUILD. 0 turns particle lights off outright. The budget is spent evenly across the " +
                "host's enabled modules and each module's ratio is scaled down with it. It never deletes a " +
                "light prototype.",
                "NOT a PROD-022 hypothesis - a MOBILE PERF knob (WO-1327). Spell_Fire_9 drives 20 lights " +
                "from Fireballs and 5 more from its Explosion sub-emitter: TWENTY-FIVE real-time point " +
                "lights per cast, at intensity 5 and range 5, on the Seeker. That is a frame-rate event on " +
                "every fireball. Like the bounce knob the dial is baked into a gitignored prefab, so the cap " +
                "belongs at the spawn owner - and it must move on device evidence rather than on a number " +
                "somebody picked."),

            new TunableSpec(KeyCombatOverTimeTickMs, TunableKind.Int, OverTimeTickMsDefault,
                "Milliseconds between the pulses of EVERY over-time effect, damage and healing alike - " +
                "the mage's wither, the knight's regen, the shipped burn on knight.emberbrand-throw and " +
                "mage.poison, and the Venombrand poison rider. 1000 = TODAY: exactly the 'const float " +
                "tick = 1f' both shipped DoT coroutines hardcoded. Magnitude per pulse is derived as " +
                "perSecond * interval, so moving this changes CADENCE ONLY - total delivery is invariant. " +
                "Clamped to 50..60000 at the consumer.",
                "NOT a PROD-022 hypothesis - a FEEL knob (WO-1330). How often a DoT ticks is the whole " +
                "READ of the effect: at 1000ms it is four discrete thuds over four seconds, at 250ms it " +
                "is a continuous drain. Which one communicates 'this is still hurting you' is a question " +
                "only felt-testing answers, and the owner is red/green colourblind, so RHYTHM is carrying " +
                "signal that colour cannot. It must move in seconds, not in a rebuild."),

            new TunableSpec(KeyCombatOverTimeMagnitudePct, TunableKind.Int, OverTimeMagnitudePctDefault,
                "Percent scale on the magnitude of every over-time pulse, both signs. 100 = TODAY: the " +
                "dotDamage / healPerSecond authored in abilities.json, unscaled. 50 halves every DoT and " +
                "every regen at once; 0 makes them inert without unauthoring anything. Clamped to 0..1000 " +
                "at the consumer.",
                "NOT a PROD-022 hypothesis - a BALANCE lever (WO-1330). It is ONE knob rather than one " +
                "per ability because the ticket required exactly that ('Prefer ONE shared knob over " +
                "per-ability duplicates where it is genuinely the same concept'): the first tuning " +
                "question is always whether over-time damage as a CLASS of effect is pulling its weight " +
                "against burst, and that is a single dial. Per-ability numbers stay in abilities.json."),

            new TunableSpec(KeyCombatOverTimeDurationPct, TunableKind.Int, OverTimeDurationPctDefault,
                "Percent scale on the duration of every over-time effect, both signs. 100 = TODAY: the " +
                "authored dotSeconds / seconds, unscaled. Raising it lengthens the window and therefore " +
                "adds pulses, so it moves TOTAL delivery where the magnitude knob moves per-pulse size. " +
                "Clamped to 0..1000 at the consumer.",
                "NOT a PROD-022 hypothesis - a BALANCE lever (WO-1330), and the one that decides whether " +
                "an over-time ability is a commitment or a garnish. Separated from the magnitude knob on " +
                "purpose: 'each tick hurts more' and 'it lasts longer' feel completely different at the " +
                "same total damage, and collapsing them into one dial would make that distinction " +
                "untestable."),

            new TunableSpec(KeyVfxNightStoreAuraMode, TunableKind.Int, VfxNightStoreAuraModeDefault,
                "What the Night Store's aura seat plays. 0 = THIS BUILD: her FIRST tagged key " +
                "NightStoreoption_Aura (top_down_starfall_line_blue), a one-shot burst re-fired on the " +
                "cadence. 1 = her SECOND tagged candidate Store_Aura (Loot_flicker), also a burst. " +
                "2 = walk the seven continuous Aura_* spell prefabs, one at a time in folder order, " +
                "advancing on the cadence. 3 = the Marker8 safe-zone ring this build replaced. Any " +
                "other number is ignored and resolves to 0.",
                "NOT a PROD-022 hypothesis - a PURE CREATIVE CHOICE the owner has explicitly not made. " +
                "She tagged one store aura, then a second ('i added another option for REalm store, not " +
                "sure which will be best'), then asked whether the Aura_* family could cycle 'slowly one " +
                "after another instead ... IF THE OTHER ONE DOESNT LOOK GOOD'. Three candidates and a " +
                "conditional. Building one and discarding the rest would either pick for her or cost a " +
                "30-minute rebuild per opinion; this knob makes it a 40-second flip on the device with " +
                "the thing in front of her. Her first pick ships as the default and nothing promotes " +
                "the others (memory vfx-map-owner-tags-no-creative-pick)."),

            new TunableSpec(KeyVfxNightStoreAuraCadenceMin, TunableKind.Int, VfxNightStoreAuraCadenceMinDefault,
                "Minutes between Night Store aura cadence ticks. 30 = TODAY, and it is her number " +
                "verbatim ('its to be random when in town every 30~min'). What a tick DOES depends on " +
                "the mode: in a burst mode it re-fires the burst, in rotate mode it advances to the " +
                "next aura, and against the continuous legacy ring it does nothing. Clamped to " +
                "1..1440 at the consumer. The clock ticks in TOWN only - never during a raid, a battle " +
                "or a dungeon.",
                "NOT a PROD-022 hypothesis - a FEEL knob (WO-1343). Whether a half-hourly pulse reads " +
                "as 'the store just caught my eye' or as 'nothing ever happens there' is a question " +
                "only felt-testing on the device answers, and the owner is red/green colourblind, so " +
                "RHYTHM is carrying signal that colour cannot. It has to move in seconds."),

            new TunableSpec(KeyVfxNightStoreAuraFamilyMask, TunableKind.Int, VfxNightStoreAuraFamilyMaskDefault,
                "Bitmask of which Aura_* prefabs the rotation may select, in the folder's own " +
                "alphabetical order: 1 Arcane, 2 Dark, 4 Fire, 8 Ice, 16 Light, 32 Nature, 64 Storm. " +
                "127 = THIS BUILD: all seven eligible. INERT unless the mode knob is 2. A member the " +
                "owner has not yet tagged in the VFX Caster does not resolve, is skipped BY NAME in " +
                "the trace, and is never substituted for. A mask that enables nothing falls back to " +
                "her first tagged key rather than leaving the store bare.",
                "NOT a PROD-022 hypothesis - the WO-1343 requirement that 'a prefab she dislikes comes " +
                "out without a code change'. A bitmask rather than a string list because that rides the " +
                "existing integer rail: adding a new tunable VALUE KIND for one feature is the second " +
                "configuration mechanism this whole rail exists to avoid."),

            new TunableSpec(KeyVfxNightStoreAuraBurstRepeatSec, TunableKind.Int, VfxNightStoreAuraBurstRepeatSecDefault,
                "Seconds between EXTRA re-fires of the burst INSIDE one cadence period. 0 = THIS " +
                "BUILD: off, exactly one burst per cadence tick, which is her spec read literally. " +
                "Set it to a few seconds to turn the half-hourly pulse into a slow heartbeat. Clamped " +
                "to 0..600. Ignored entirely in the two CONTINUOUS modes (rotate-family and the legacy " +
                "ring), where there is no burst to repeat.",
                "NOT a PROD-022 hypothesis - the escape hatch for the one number in this feature that " +
                "was measured rather than chosen. BOTH of her store tags were verified one-shot (every " +
                "ParticleSystem looping:0 on top_down_starfall_line_blue and Loot_flicker), so her " +
                "isLoop:false is CORRECT and a burst is what she authored. But '30~min' was a rough " +
                "number said in passing, and if one pulse per half hour turns out to read as nothing " +
                "at all, this fixes it without a rebuild and without anyone re-tagging her prefab."),
        };

        // Swapped atomically by ApplyPayload. Never mutated in place.
        private static Dictionary<string, string> s_remote;
        private static string s_provenance = ProvenanceDefault;

        /// <summary>Where the standing table came from: "default" | "remote" | "remote-cached".</summary>
        public static string TableProvenance => s_provenance;

        /// <summary>True once any payload (live or cached) has been accepted.</summary>
        public static bool Loaded => s_remote != null;

        /// <summary>Keys in the standing table. 0 means every knob answers its default.</summary>
        public static int RowCount => s_remote == null ? 0 : s_remote.Count;

        /// <summary>Bumped on every accepted payload. Lets a reader see the config change mid-session.</summary>
        public static int Generation { get; private set; }

        // =====================================================================
        //  READ SIDE - always answers, never throws, ABSENCE => the DEFAULT.
        // =====================================================================

        /// <summary>Find a knob's contract, or null for an unregistered key (a caller bug).</summary>
        public static TunableSpec SpecFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < Registry.Length; i++)
                if (string.Equals(Registry[i].Key, key, StringComparison.Ordinal))
                    return Registry[i];
            return null;
        }

        /// <summary>
        /// Resolve a BOOL knob. NEVER throws. An unregistered key, an absent row, an
        /// unparseable value and an unreachable server ALL resolve to the shipping default.
        /// </summary>
        public static bool Bool(string key) => Int(key) != 0;

        /// <summary>
        /// Resolve an INT knob. NEVER throws, and the failure answer is always the default.
        /// <para>
        /// Traced ONCE per key per provenance-and-value, so a capture states which value was
        /// used AND where it came from without a reader having to guess (CLAUDE.md section 12).
        /// Re-traced when a later payload changes the answer, because a knob that changed
        /// mid-session is precisely the thing a felt-test needs to see.
        /// </para>
        /// </summary>
        public static int Int(string key)
        {
            var spec = SpecFor(key);
            if (spec == null)
            {
                // A key nobody registered is a CALLER bug, not a data problem. Say so loudly
                // and answer 0 - there is no default to fall back to because there is no knob.
                FlowTrace.Once(Sys, "unregistered:" + key,
                    "UNREGISTERED tunable key '" + (key ?? "null") + "' was read. There is no spec and " +
                    "therefore no default; answering 0. Add it to RemoteTunables.Registry and to " +
                    "docs/PROD022_TUNABLE_FLAGS.md in the same commit.");
                return 0;
            }

            int value = spec.Default;
            string provenance = ProvenanceDefault;

            // (1) REMOTE. The owner at the database.
            var table = s_remote;
            if (table != null && table.TryGetValue(spec.Key, out string raw))
            {
                if (TryParseValue(raw, spec, out int parsed))
                {
                    value = parsed;
                    provenance = s_provenance == ProvenanceCache ? ProvenanceCache : ProvenanceRemote;
                }
                else
                {
                    // Malformed row. It does NOT poison the knob - it falls to the default and
                    // says so. Throttled rather than Once: the row can be corrected live, and a
                    // reader needs to see that the bad value is STILL there.
                    FlowTrace.Throttle(Sys, "badvalue:" + spec.Key, 30f,
                        "row '" + spec.Key + "' carries an unusable value '" + Flatten(raw) + "' for kind " +
                        spec.Kind + " - IGNORED, this knob resolves to its shipping default " +
                        Describe(spec, spec.Default) + ". Fix the row; nothing is broken meanwhile.");
                }
            }

            // (2) LOCAL PlayerPrefs. The human at the device. Most specific, so it wins last.
            int local = ReadLocalOverride(spec);
            if (local != int.MinValue)
            {
                value = local;
                provenance = ProvenanceLocal;
            }

            FlowTrace.Once(Sys, "resolve:" + spec.Key + "=" + value + "@" + provenance,
                "KNOB " + spec.Key + " = " + Describe(spec, value) + "  provenance=" + provenance +
                "  (shipping default " + Describe(spec, spec.Default) + ", generation=" + Generation + "). " +
                (provenance == ProvenanceDefault
                    ? "No database row and no local override - this is TODAY'S BEHAVIOUR, unchanged."
                    : "This is an OVERRIDE of the shipping default."));

            return value;
        }

        /// <summary>
        /// PlayerPrefs override for one knob, or <c>int.MinValue</c> when absent.
        /// Guarded: PlayerPrefs on a hardened WebGL host can throw on access, and a
        /// diagnostic knob must never be the thing that takes the app down.
        /// </summary>
        private static int ReadLocalOverride(TunableSpec spec)
        {
            const int absent = int.MinValue;
            return Guard.Try(Sys, "read local override " + spec.Key, () =>
            {
                int v = UnityEngine.PlayerPrefs.GetInt(LocalPrefix + spec.Key, absent);
                if (v == absent) return absent;
                if (spec.Kind == TunableKind.Bool) return v != 0 ? 1 : 0;
                return v;
            }, absent);
        }

        /// <summary>Parse one wire value. Accepts 0/1 and true/false for bools.</summary>
        private static bool TryParseValue(string raw, TunableSpec spec, out int value)
        {
            value = 0;
            if (raw == null) return false;
            string s = raw.Trim();
            if (s.Length == 0) return false;

            if (spec.Kind == TunableKind.Bool)
            {
                if (s.Equals("1", StringComparison.Ordinal) ||
                    s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("on", StringComparison.OrdinalIgnoreCase)) { value = 1; return true; }
                if (s.Equals("0", StringComparison.Ordinal) ||
                    s.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("off", StringComparison.OrdinalIgnoreCase)) { value = 0; return true; }
                return false;
            }

            return int.TryParse(s, System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Human wording for a value: ON/OFF for bools, the number for ints.</summary>
        public static string Describe(TunableSpec spec, int value)
        {
            if (spec == null) return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (spec.Kind == TunableKind.Bool) return value != 0 ? "ON" : "OFF";
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // =====================================================================
        //  THE CONFIGURATION LINE - one line, every knob, every session
        // =====================================================================

        /// <summary>
        /// Print the WHOLE configuration on one line: every knob, its resolved value and its
        /// provenance.
        /// <para>
        /// ⭐ THIS IS WHY A FELT-TEST IS NOT WASTED. The owner will flip knobs between runs and
        /// report "that one felt better". Without this line the capture cannot say which
        /// configuration produced it, and the run proves nothing. Emitted at service boot AND
        /// again on every accepted payload, so a mid-session change is visible too.
        /// </para>
        /// Never throws. Uses Warn deliberately when ANY knob is overridden - an overridden build
        /// is not the shipping build, and that must not read as ordinary narration.
        /// </summary>
        public static void LogConfiguration(string why)
        {
            Guard.Try(Sys, "log tunable configuration", () =>
            {
                var sb = new StringBuilder(512);
                int overridden = 0;
                sb.Append("CONFIG (").Append(why ?? "?").Append("): generation=").Append(Generation)
                  .Append(" tableProvenance=").Append(s_provenance)
                  .Append(" rows=").Append(RowCount).Append(" | ");

                for (int i = 0; i < Registry.Length; i++)
                {
                    var spec = Registry[i];
                    int v = Int(spec.Key);
                    if (v != spec.Default) overridden++;
                    if (i > 0) sb.Append("  ");
                    sb.Append(spec.Key).Append('=').Append(Describe(spec, v));
                    if (v != spec.Default) sb.Append("(OVERRIDDEN, default ").Append(Describe(spec, spec.Default)).Append(')');
                }

                if (overridden == 0)
                {
                    FlowTrace.Step(Sys, sb.ToString() +
                        " || EVERY knob is at its shipping default - this session is TODAY'S BEHAVIOUR, " +
                        "unchanged. Nothing was overridden by the database or by PlayerPrefs.");
                }
                else
                {
                    FlowTrace.Warn(Sys, sb.ToString() +
                        " || " + overridden + " knob(s) are OVERRIDDEN. This session is NOT the shipping " +
                        "default configuration - quote this line in any felt-test report, because it is " +
                        "the only record of what produced the run. See docs/PROD022_TUNABLE_FLAGS.md.");
                }
            });
        }

        // =====================================================================
        //  WRITE SIDE
        // =====================================================================

        /// <summary>
        /// Drop the standing table. Every knob answers its shipping default afterwards,
        /// which is the correct resting state and the one this system must always be able
        /// to fall back to.
        /// </summary>
        public static void Clear(string provenance = ProvenanceDefault)
        {
            s_remote = null;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceDefault : provenance;
            Generation++;
        }

        /// <summary>
        /// Parse a payload and ATOMICALLY swap it in. Returns false on a hard parse failure,
        /// and on that path the EXISTING table is left exactly as it was - a malformed live
        /// payload must never half-apply over a good standing table.
        /// <para>
        /// A payload whose readOk is false is the SERVER saying it could not read its own
        /// table. That clears to defaults rather than being mistaken for "no knobs are set".
        /// </para>
        /// </summary>
        public static bool ApplyPayload(string json, string provenance)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                FlowTrace.Warn(Sys, "payload rejected: empty body from provenance='" +
                                    (provenance ?? "null") + "'. Every knob keeps whatever it already " +
                                    "resolved (tableProvenance=" + s_provenance + ").");
                return false;
            }

            TunablePayload dto = Guard.Try<TunablePayload>(
                Sys, "parse tunables payload (" + (provenance ?? "null") + ")",
                () => JsonConvert.DeserializeObject<TunablePayload>(json),
                null);

            if (dto == null)
            {
                FlowTrace.Warn(Sys, "payload rejected: unparseable (provenance='" + (provenance ?? "null") +
                                    "'). Every knob resolves to its SHIPPING DEFAULT - the remote read is " +
                                    "an override, never a dependency.");
                return false;
            }

            if (dto.Version != PayloadVersion)
            {
                FlowTrace.Warn(Sys, "payload version " + dto.Version + " != expected " + PayloadVersion +
                                    " - parsing anyway (forward-compatible).");
            }

            if (!dto.ReadOk)
            {
                s_remote = null;
                s_provenance = ProvenanceDefault;
                Generation++;
                FlowTrace.Warn(Sys, "server reported readOk=false (reason='" + (dto.Reason ?? "?") +
                                    "') - the tunables table is unreadable ON THE SERVER. Every knob is " +
                                    "back at its shipping default, i.e. today's behaviour. No knob can be " +
                                    "changed until the table reads again.");
                LogConfiguration("server readOk=false");
                return true;
            }

            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            int unknown = 0;

            if (dto.Values != null)
            {
                foreach (var pair in dto.Values)
                {
                    string key = pair.Key;
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (SpecFor(key) == null)
                    {
                        // Forward compatibility, and it is deliberately not an error: the
                        // database may carry a key a NEWER build understands. Say so and move on.
                        unknown++;
                        FlowTrace.Step(Sys, "payload carries unregistered key '" + key +
                                            "' - ignored by this build (it may belong to a newer one).");
                        continue;
                    }
                    next[key] = pair.Value;
                }
            }

            // ATOMIC: one assignment, whole table.
            s_remote = next;
            s_provenance = string.IsNullOrEmpty(provenance) ? ProvenanceRemote : provenance;
            Generation++;

            LogConfiguration("payload accepted, rows=" + next.Count + " unknown=" + unknown);
            return true;
        }

        /// <summary>
        /// Serialise the standing table back to the wire shape, for the on-device cache.
        /// Returns null when there is nothing to cache. Never throws.
        /// </summary>
        public static string SerializeStandingTable()
        {
            var table = s_remote;
            if (table == null) return null;
            return Guard.Try<string>(Sys, "serialize standing tunables", () =>
            {
                var dto = new TunablePayload
                {
                    Version = PayloadVersion,
                    ReadOk = true,
                    Reason = "cache",
                    Values = new Dictionary<string, string>(table, StringComparer.Ordinal),
                };
                return JsonConvert.SerializeObject(dto);
            }, null);
        }

        /// <summary>Flatten a value for one-line logging. Bounded - a row is operator data.</summary>
        private static string Flatten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            string t = s.Replace('\r', ' ').Replace('\n', ' ');
            return t.Length <= 64 ? t : t.Substring(0, 64) + "...";
        }

        // ---------------------------------------------------------------------
        //  Wire DTO - Newtonsoft. JsonUtility cannot express the 'values' map.
        // ---------------------------------------------------------------------

        [Serializable]
        internal sealed class TunablePayload
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("readOk")] public bool ReadOk { get; set; }
            [JsonProperty("reason")] public string Reason { get; set; }
            [JsonProperty("values")] public Dictionary<string, string> Values { get; set; }
        }
    }
}
