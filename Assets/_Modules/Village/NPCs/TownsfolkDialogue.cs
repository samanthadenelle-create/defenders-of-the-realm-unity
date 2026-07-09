// =============================================================================
// TownsfolkDialogue — the ambient-villager flavour-line table (Workstream D).
// -----------------------------------------------------------------------------
// The dialogue content for Elarion's ambient townsfolk and the four named
// wardens who keep the village's trades alive. When the Keeper draws near an
// NPC a word bubble shows ONE of these lines; it hides again when the Keeper
// walks away (see AmbientNPC / TownsfolkController).
//
// LOCALIZE: every spoken string in this file is user-facing flavour text. The
// task brief keeps townsfolk dialogue OUT of the shared en.json (other agents
// own that file) — these are kept here as clearly-marked // LOCALIZE: constants
// so a future localization pass can lift them wholesale into the string table.
//
// Voice. Elarion is a magical + medieval crossover town that defends the Heart
// of Elarion against the Hollow Ones. Per the narrative bible §8 the tone is
// short, grounded, hopeful at the core, no fake-archaic "thee/thou", and the
// Hollow Ones read as grief rather than villains. Ambient lines lean cozy,
// lived-in, and a little anxious about the waves — they should make the town
// read as ALIVE in a grant demo without long-winded lore dumps. Each NPC "type"
// gets its own small pool so a market trader sounds different from the
// Blacksmith at her anvil or an off-duty wall guard.
//
// ── The four wardens (WO-116) ──
//   • Blacksmith   — keeps the steel; proud, sooty, plain-spoken.
//   • Quartermaster — keeps the stores; counts everything, jokes dryly.
//   • Archmage     — keeps the wards; weary scholar, watches the violet light.
//   • Farmer       — keeps the fields; patient, weather-wise, feeds the wall.
// These are the named tradesfolk the injector points the People-pack models at.
//
// No assembly dependency: this is a plain data class in DeNelle.Village,
// referenced only by the NPC MonoBehaviours in the same module.
// =============================================================================

using System;

namespace DeNelle.Village
{
    /// <summary>
    /// The flavour-line pools the ambient townsfolk and wardens speak. Static,
    /// read-only data — picked from by <see cref="AmbientNPC"/> through
    /// <see cref="LineFor"/>.
    /// </summary>
    public static class TownsfolkDialogue
    {
        /// <summary>
        /// The NPC archetypes. Each maps to its own line pool and a display name,
        /// so a wandering trader reads differently from an idle-by-the-well gossip,
        /// an off-duty wall guard, or one of the four named wardens.
        ///
        /// <para>Numeric values are STABLE — <see cref="AmbientNPC"/> serializes
        /// this enum by value, so the original five (0–4) keep their numbers and
        /// the four wardens (WO-116) are appended as 5–8. Do not renumber.</para>
        /// </summary>
        public enum Archetype
        {
            /// <summary>A market trader / craftsperson — busy, transactional, warm.</summary>
            Trader = 0,
            /// <summary>An everyday villager going about their day — cozy, grounded.</summary>
            Villager = 1,
            /// <summary>An off-duty guard / wall-watcher — wary, eyes on the gates.</summary>
            Guard = 2,
            /// <summary>A young child of the village — playful, awed by the Heart.</summary>
            Child = 3,
            /// <summary>An elder who remembers older wars — calm, encouraging.</summary>
            Elder = 4,

            // ── The four named wardens (WO-116) ──────────────────────────────
            /// <summary>The Blacksmith — keeps the village's steel; proud, plain-spoken.</summary>
            Blacksmith = 5,
            /// <summary>The Quartermaster — keeps the stores; dry-humoured, counts everything.</summary>
            Quartermaster = 6,
            /// <summary>The Archmage — keeps the wards; weary scholar of the violet light.</summary>
            Archmage = 7,
            /// <summary>The Farmer — keeps the fields; patient, weather-wise, feeds the wall.</summary>
            Farmer = 8,
        }

        // ── Dragon foreshadow (owner directive 2026-07-08) ───────────────────
        /// <summary>
        /// How near the apex Black Dragon ("Syndrath the Devourer") is, driving
        /// which tier of rumor a townsperson drops. New players "have no clue the
        /// dragon is coming" — so the town telegraphs it diegetically, escalating
        /// from uneasy rumors to urgent shouts, nudging the Keeper to build the
        /// anti-air Sky Ballista BEFORE the dragon arrives. Text only (owner is
        /// colorblind — never encode this in colour).
        /// </summary>
        public enum DragonHintTier
        {
            /// <summary>Early — vague unease, birds and weather. No dragon named yet.</summary>
            Far = 0,
            /// <summary>Mid — second-hand sightings; something WINGED is out there.</summary>
            Mid = 1,
            /// <summary>Near (a wave or two out) — name the threat + the counter: spears to the sky.</summary>
            Near = 2,
            /// <summary>Imminent (the dragon wave) — it's here; man the ramparts, loose skyward.</summary>
            Imminent = 3,
        }

        /// <summary>
        /// The wave the apex Black Dragon arrives on — the terminal apexBoss wave
        /// in waves.json ("The Last Wing", waveId 4). Escalation tiers are computed
        /// relative to THIS. WaveManager does not expose the apex wave number
        /// publicly, so it is mirrored here as a documented design constant; if the
        /// schedule's apex wave moves, update this to match waves.json.
        /// </summary>
        public const int DragonWaveId = 4;

        // ── Display names per archetype ──────────────────────────────────────
        // LOCALIZE: shown as the speech-bubble attribution line. Index MUST track
        // the Archetype enum value (0..8).
        private static readonly string[] _names =
        {
            "Elarion Trader",   // 0 Trader
            "Villager",         // 1 Villager
            "Off-duty Guard",   // 2 Guard
            "Village Child",    // 3 Child
            "Village Elder",    // 4 Elder
            "Brunhild, the Smith",    // 5 Blacksmith
            "Aldric, Quartermaster",  // 6 Quartermaster
            "Archmage Sela",          // 7 Archmage
            "Goodman Harrow",         // 8 Farmer
        };

        // ── Trader lines ─────────────────────────────────────────────────────
        // LOCALIZE: ambient market-trader flavour.
        private static readonly string[] _trader =
        {
            "Fresh moon-pears, Keeper! Picked before the dew burned off.",
            "Crystal dust's gone dear since the last wave — everyone wants warding charms.",
            "Trade you a warm loaf for a kind word. The Heart keeps us, but bread keeps the cold out.",
            "Mind the east stall — old Harrow stacks his barrels where folk trip.",
            "Every coin I take goes to mending the walls. We all pay the wall its due.",
        };

        // ── Villager lines ───────────────────────────────────────────────────
        // LOCALIZE: ambient everyday-villager flavour.
        private static readonly string[] _villager =
        {
            "Morning, Keeper. The Heart glowed bright last night — slept like a babe, I did.",
            "They say the Hollow Ones fear the violet light. I hope they're right.",
            "My garden's blooming again. Funny how flowers don't care about the waves.",
            "If you're headed to the gates, give them a knock for luck from me.",
            "Heard the Healer took in a wanderer at the cottage. Strange days.",
            "Quiet today. Almost too quiet — but I'll take quiet over the drums.",
        };

        // ── Guard lines ──────────────────────────────────────────────────────
        // LOCALIZE: ambient off-duty-guard flavour.
        private static readonly string[] _guard =
        {
            "Walls held through the night. They always do — but I still count every stone.",
            "The wards hold steady on all four gates. Sleep easy, Keeper.",
            "Saw movement past the south lane at dusk. Could be deer. Could be worse.",
            "Off-duty, but a guard's never truly off. Shout if the horn sounds.",
            "Keep a tower manned and the Hollow Ones break against it like sea on rock.",
        };

        // ── Child lines ──────────────────────────────────────────────────────
        // LOCALIZE: ambient village-child flavour.
        private static readonly string[] _child =
        {
            "Are you the Keeper? My da says you're why the lights stay on!",
            "I'm not scared of the Hollow Ones. Well — not in the daytime.",
            "When I grow up I'll guard the Heart too. You'll teach me, won't you?",
            "Watch this! ...okay, I can't really do magic yet. Soon though!",
        };

        // ── Elder lines ──────────────────────────────────────────────────────
        // LOCALIZE: ambient village-elder flavour.
        private static readonly string[] _elder =
        {
            "I've seen three Keepers stand where you stand. The realm endures, child.",
            "The Heart of Elarion was old when my grandmother was young. Tend it well.",
            "Waves come and waves break. What matters is the town behind the wall.",
            "Rest when you can, Keeper. Even a guardian must close their eyes.",
        };

        // ── Blacksmith lines (WO-116 warden) ─────────────────────────────────
        // LOCALIZE: the village smith — proud of her steel, plain-spoken, sooty.
        private static readonly string[] _blacksmith =
        {
            "Bring me crystal and I'll bring you an edge that bites a Hollow One to dust.",
            "Every blade on that wall came off my anvil. I sleep fine knowing it.",
            "Mind the sparks, Keeper — the forge runs hot since the last wave thinned my stock.",
            "A good hammer and a steady hand. That's all that's ever held the dark back.",
            "Dent your sword on something? Good. Means you were swinging it. Leave it, I'll mend it.",
            "The Heart keeps the town. I keep the town's teeth. Fair trade, I'd say.",
        };

        // ── Quartermaster lines (WO-116 warden) ──────────────────────────────
        // LOCALIZE: keeps the stores — dry-humoured, counts everything twice.
        private static readonly string[] _quartermaster =
        {
            "Crystal, grain, arrows, bandages — I count it all twice and trust none of it once.",
            "You'll want supplies before the next wave, Keeper. I've set yours aside. Don't tell the others.",
            "Stores are holding. Holding isn't winning, mind — but it beats the alternative.",
            "Lose a thing on the wall and I hear about it. I hear about everything. It's the job.",
            "Spend wisely. The Hollow Ones don't take coin, but the walls surely do.",
            "Give me a full larder over a full armoury any day. A fed wall is a held wall.",
        };

        // ── Archmage lines (WO-116 warden) ───────────────────────────────────
        // LOCALIZE: keeps the wards — weary scholar who reads the violet light.
        private static readonly string[] _archmage =
        {
            "The wards drink crystal like a parched field drinks rain. Keep them fed, Keeper.",
            "I read the violet light the way a sailor reads the sky. Today it reads... uneasy.",
            "The Hollow Ones are not evil. They are grief that forgot how to stop. Remember that when you fight.",
            "Magic is only patience with a shape to it. The Heart taught me that, not any book.",
            "Wake me if a ward dims past amber. Some nights I do not sleep at all.",
            "Power flows from the Heart to the wall through me. I am only the channel. Tend the source.",
        };

        // ── Farmer lines (WO-116 warden) ─────────────────────────────────────
        // LOCALIZE: keeps the fields — patient, weather-wise, feeds the wall.
        private static readonly string[] _farmer =
        {
            "Soldiers hold the wall, Keeper, but it's the harvest that holds the soldiers.",
            "Frost came early to the east row. The land remembers the Hollow Ones too, I reckon.",
            "I've sown through three waves now. The seed doesn't care about the dark. Neither will I.",
            "Stop by at dusk — there's stew on, and a hungry Keeper's no use to anyone.",
            "Give me a season of quiet and I'll fill every larder in Elarion. Just a season.",
            "The fields run right up to the moat. Closest a peaceful thing gets to the wall, that.",
        };

        // ── Dragon-foreshadow rumor pools (owner directive 2026-07-08) ───────
        // LOCALIZE: escalating townsfolk rumors that telegraph the apex Black
        // Dragon so a new player learns to build the anti-air Sky Ballista BEFORE
        // it arrives. Spoken by ANY townsperson (see AmbientNPC), regardless of
        // archetype, when the dragon wave is near. Each tier is clearly MORE
        // alarmed than the last and nudges toward the counter (spears / walls /
        // the SKY / the Sky Ballista).
        // ⚠ FIRST DRAFT for the owner to revise — keep in-world, no "thee/thou".

        // FAR: vague unease. Birds, weather, animals — the dragon is not named.
        private static readonly string[] _dragonFar =
        {
            "The birds have fled the eastern peaks. My grandmother said that only happens before something with wings stirs.",
            "Cold wind off the mountains, and it carries ash. Uneasy nights lately, Keeper.",
            "The cattle won't settle — they keep staring up at the ridge, waiting on a storm that isn't in the sky.",
        };

        // MID: second-hand sightings. Something WINGED is out there; dread rising.
        private static readonly string[] _dragonMid =
        {
            "A trader swore he saw a shadow cross the moon — big as a barn. Tall tales... I hope.",
            "There's scorch on the ridge road, Keeper. No campfire did that. Something flew low, and it flew hot.",
            "The scouts came back white as milk. Whatever's out there doesn't march — it circles.",
        };

        // NEAR (a wave or two out): name the threat AND the counter — spears skyward.
        private static readonly string[] _dragonNear =
        {
            "It comes from the SKY, Keeper — ground walls won't save us. We need spears that reach the clouds.",
            "Mount the ballistas on the walls! Only a bolt loosed skyward will bring a dragon down.",
            "Arrows and towers can't touch a thing that flies. Raise the Sky Ballista, Keeper — give us a spear for the heavens, or we're kindling.",
        };

        // IMMINENT (the dragon wave): it's HERE — man the ramparts, loose skyward.
        private static readonly string[] _dragonImminent =
        {
            "DRAGON! To the ramparts — spears to the sky!",
            "It's here — WINGS over the ridge! The Sky Ballista, NOW, or we burn!",
            "Look UP, Keeper! Loose everything skyward — the Devourer is upon us!",
        };

        /// <summary>
        /// Maps the current wave to a foreshadow tier from how close the dragon
        /// (apex) wave is: 2+ waves out = Far, then Mid, Near, and the dragon wave
        /// itself (or past it) = Imminent. Pure — never throws.
        /// </summary>
        public static DragonHintTier TierForWave(int currentWaveId, int dragonWaveId = DragonWaveId)
        {
            int wavesUntil = dragonWaveId - currentWaveId;
            if (wavesUntil <= 0) return DragonHintTier.Imminent;
            if (wavesUntil == 1) return DragonHintTier.Near;
            if (wavesUntil == 2) return DragonHintTier.Mid;
            return DragonHintTier.Far;
        }

        /// <summary>The rumor pool for a foreshadow tier. Never null / never empty.</summary>
        public static string[] DragonRumorPool(DragonHintTier tier)
        {
            switch (tier)
            {
                case DragonHintTier.Far:      return _dragonFar;
                case DragonHintTier.Mid:      return _dragonMid;
                case DragonHintTier.Near:     return _dragonNear;
                case DragonHintTier.Imminent: return _dragonImminent;
                default:                      return _dragonFar;
            }
        }

        /// <summary>
        /// Picks a dragon-foreshadow rumor for <paramref name="tier"/>. Same
        /// modulo "steps deterministically / never throws / never null" contract
        /// as <see cref="LineFor"/>.
        /// </summary>
        public static string DragonRumor(DragonHintTier tier, int index)
        {
            string[] pool = DragonRumorPool(tier);
            if (pool == null || pool.Length == 0) return string.Empty;
            int i = index % pool.Length;
            if (i < 0) i += pool.Length;
            return pool[i];
        }

        /// <summary>Returns the speech-bubble display name for an archetype.</summary>
        public static string NameFor(Archetype archetype)
        {
            int i = (int)archetype;
            return (i >= 0 && i < _names.Length) ? _names[i] : "Villager";
        }

        /// <summary>The full line pool for an archetype.</summary>
        public static string[] PoolFor(Archetype archetype)
        {
            switch (archetype)
            {
                case Archetype.Trader:        return _trader;
                case Archetype.Villager:      return _villager;
                case Archetype.Guard:         return _guard;
                case Archetype.Child:         return _child;
                case Archetype.Elder:         return _elder;
                case Archetype.Blacksmith:    return _blacksmith;
                case Archetype.Quartermaster: return _quartermaster;
                case Archetype.Archmage:      return _archmage;
                case Archetype.Farmer:        return _farmer;
                default:                      return _villager;
            }
        }

        /// <summary>
        /// Picks a line from <paramref name="archetype"/>'s pool. <paramref name="index"/>
        /// is taken modulo the pool size, so a caller can step deterministically
        /// through the pool (each fresh approach shows the next line) or pass a
        /// random value for a random pick. Never throws, never returns null.
        /// </summary>
        public static string LineFor(Archetype archetype, int index)
        {
            string[] pool = PoolFor(archetype);
            if (pool == null || pool.Length == 0) return string.Empty;
            int i = index % pool.Length;
            if (i < 0) i += pool.Length;
            return pool[i];
        }

        /// <summary>The number of distinct archetypes — handy for round-robin assignment.</summary>
        public static int ArchetypeCount => Enum.GetValues(typeof(Archetype)).Length;
    }
}
