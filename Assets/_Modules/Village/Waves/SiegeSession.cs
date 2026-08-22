// =============================================================================
// SiegeSession — the LIVE RECORDER for one attack on the player's town (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ⛔ THIS CLASS SPAWNS NOTHING. There is not one Instantiate in it, and there never
//    will be. WaveManager already owns "hostiles attack the player's town" and is the
//    ONLY thing that does. A siege is a SCHEDULED WaveManager wave that is being
//    WATCHED — a scheduler plus a recorder, never a second attacker. Two systems that
//    both attack the town drift apart; that is the failure this repo keeps hitting, so
//    SiegeSpawnAuthorityRegression fails the gate if a spawn call appears here.
//
// LIFECYCLE (exactly one open session at a time):
//   Open(...)        <- SiegeScheduler, immediately BEFORE it asks WaveManager to begin
//   ObserveTick(...) <- WaveManager.TickActiveWave, ONE null-safe line per frame
//   Close(outcome)   <- SiegeScheduler, from WaveManager.OnWaveCleared / OnDefeat
//
// WHY ObserveTick RATHER THAN HOOKING THE EXISTING BREACH DETECTOR (deviation from the
// WO-1026 plan §2.4, recorded here so nobody "fixes" it back):
//   The plan says "the breach detector already exists — hook it, do not write a second
//   one". It exists, but the WHOLE detector body sits inside
//       if (FeatureFlags.WaveBreachToAtb && _breachArmed && _heart != null)
//   (WaveManager.cs, TickActiveWave) and that flag is DEFAULT OFF — WO-579 turned it off
//   because crossing the ring used to yank the player into an ATB scene. So on the live
//   default path the detector NEVER RUNS and there is no breach signal to hook. Hooking
//   it would have recorded nothing, forever, silently. ObserveTick therefore does its own
//   ring test using WaveManager's OWN heart, radius and armed flag (passed in — no second
//   source of truth for the geometry) and records the crossing WITHOUT triggering the ATB
//   hand-off. It is an OBSERVER of the existing loop, not a second detector: it changes no
//   phase, cancels nothing, and spawns nothing.
//
// THE ROSTER IS A UNION OVER TIME, NOT A CENSUS (WO-1113):
//   The spawn budget releases a wave's roster in SLICES with reinforcement drips, so a
//   scene census at any single instant undercounts the force. ObserveTick unions every
//   enemy it has ever seen alive this session, keyed by def id — which is what actually
//   got fielded. This also means NO new public roster accessor on WaveManager was needed
//   (the plan's §9 contradiction 3 is moot).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Defense;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Records ONE town assault while it happens. Owned by <see cref="SiegeScheduler"/>.</summary>
    public sealed class SiegeSession
    {
        /// <summary>Max breach rows kept. A breach FLOOD (a wall gone, thirty crossings) must not
        /// bloat the save; the count is preserved in <see cref="TotalBreachCrossings"/> and the
        /// truncation is traced, never silent (the WaveDamageReport MaxRows precedent).</summary>
        public const int MaxBreachRows = 8;

        /// <summary>How close a crossing must be to a Gate/WallSegment to be NAMED after it.
        /// Beyond this the row honestly reads "Open ground" rather than blaming a nearby gate
        /// the attacker never touched.</summary>
        private const float BreachNameRadius = 14f;

        /// <summary>The one open session, or null. Read by WaveManager's single observe line.</summary>
        public static SiegeSession Current { get; private set; }

        /// <summary>Max path samples kept. At <see cref="PathSampleInterval"/> this covers a
        /// ~48s assault at full resolution and then thins (see <see cref="SamplePath"/>) rather
        /// than truncating, so a long fight still shows its whole approach.</summary>
        public const int MaxPathPoints = 24;

        /// <summary>Seconds between force-centroid samples. 2s is plenty to read an approach
        /// direction and keeps the persisted polyline tiny.</summary>
        private const float PathSampleInterval = 2f;

        private readonly DefenseOutcomeRecord _record = DefenseOutcomeRecord.NewEmpty();
        private readonly Dictionary<string, AttackerUnitRecord> _roster =
            new Dictionary<string, AttackerUnitRecord>();
        private readonly HashSet<int> _crossed = new HashSet<int>();   // enemy instance ids already counted
        private float _openedAtRealtime;
        private float _nextPathSampleAt;
        private bool _coreGeometryCaptured;

        /// <summary>Per-structure damage TIMING for this assault (hold time). Null only if the
        /// build failed, in which case every row honestly reports an unknown hold time rather
        /// than a fabricated zero.</summary>
        public StructureVitalsWatch Vitals { get; private set; }

        /// <summary>Total ring crossings observed, including any beyond <see cref="MaxBreachRows"/>.</summary>
        public int TotalBreachCrossings { get; private set; }

        /// <summary>The wave ordinal this session is watching.</summary>
        public int WaveId => _record.WaveId;

        /// <summary>Seconds since the session opened — ENGINE time, deliberately.
        /// <para>⛔ Every duration in this class (hold time, path sampling, DurationSeconds) is
        /// measured off this, i.e. off Time.realtimeSinceStartup, and never off the persisted
        /// wall clock. The DEV queue time-skip fast-forwards that clock, and warping a live
        /// battle is exactly what the owner ruled out — DevTimeSkipRegression case6 fails the
        /// gate if this file so much as names the skippable clock. The only two wall-clock
        /// values a session needs (the started/ended ledger stamps) are fetched from SiegeClock,
        /// which lives outside the swept tree; read its header before changing any of this.</para></summary>
        public float ElapsedSeconds => Mathf.Max(0f, Time.realtimeSinceStartup - _openedAtRealtime);

        // =====================================================================
        //  Open
        // =====================================================================

        /// <summary>
        /// Opens a session and installs it as <see cref="Current"/>. The defender snapshot is
        /// captured HERE — before the first spawn — because that is the base the player is about
        /// to be judged on. Returns the session (never null).
        /// </summary>
        public static SiegeSession Open(int waveId, AttackerIdentity attacker, DefenderSnapshot defender)
        {
            if (Current != null)
            {
                // Not fatal, but it means a Close was missed — say so loudly rather than
                // silently orphaning a half-recorded assault.
                FlowTrace.Warn("Siege",
                    $"Open(wave={waveId}) while session for wave {Current.WaveId} is still open -- " +
                    "the previous session is being ABANDONED (its Close never ran).");
            }

            var s = new SiegeSession();
            s._openedAtRealtime = Time.realtimeSinceStartup;
            s._record.WaveId = waveId;
            s._record.StartedAtUnixMs = SiegeClock.NowUnixMs();
            s._record.Resolution = DefenseResolution.Live;   // the ONLY value produced today
            if (attacker != null) s._record.Attacker = attacker;
            if (defender != null) s._record.Defender = defender;
            DefenseOutcomeRecord.Normalize(s._record);

            // The vitals watch MUST be built here, before the first spawn, or its
            // "already damaged when this started" flag would silently absorb damage from
            // THIS assault and report a wall as pre-broken. Heart position comes from the
            // Heart itself; the breach-ring radius arrives on the first observe tick (it is
            // WaveManager's own field and is handed over there rather than duplicated).
            var heart = Object.FindFirstObjectByType<HeartController>();
            Vector3 core = heart != null ? heart.transform.position : Vector3.zero;
            if (heart == null)
                FlowTrace.Warn("Siege", "no HeartController found -- distances/bands measured from world origin.");
            s.Vitals = StructureVitalsWatch.Build(core);
            s._record.Defender.CoreX = core.x;
            s._record.Defender.CoreZ = core.z;
            s._record.Defender.FrontRadius = s.Vitals != null ? s.Vitals.FrontRadius : 0f;

            Current = s;
            FlowTrace.Step("Siege",
                $"session OPEN wave={waveId} attacker={s._record.Attacker.DisplayName} " +
                $"source={s._record.Attacker.Source} power={s._record.Attacker.PowerRating} " +
                $"layout={s._record.Defender.LayoutHash} structures={s._record.Defender.StructureCount}.");
            return s;
        }

        // =====================================================================
        //  Observe — the ONE line WaveManager calls
        // =====================================================================

        /// <summary>
        /// Called once per frame from <c>WaveManager.TickActiveWave</c> with the manager's OWN
        /// geometry (no second source of truth). Unions the fielded roster and records inner-ring
        /// crossings. NEVER throws, never spawns, never changes wave phase.
        /// </summary>
        /// <param name="heartPos">The Heart position the wave is centred on.</param>
        /// <param name="innerRingRadius">WaveManager's configured breach ring radius.</param>
        /// <param name="breachArmed">WaveManager's arm flag (the post-spawn grace has elapsed).</param>
        /// <param name="live">WaveManager's live-enemy list.</param>
        public void ObserveTick(Vector3 heartPos, float innerRingRadius, bool breachArmed,
                                IReadOnlyList<Enemy> live)
        {
            if (live == null) return;

            Guard.Try("Siege", "observe assault tick", () =>
            {
                float now = ElapsedSeconds;

                // WaveManager's OWN breach-ring radius, handed over rather than duplicated:
                // the line that defines a breach is the line that defines the base's CORE band,
                // so there is exactly one number and the report stores the one that was used.
                if (!_coreGeometryCaptured)
                {
                    _coreGeometryCaptured = true;
                    _record.Defender.CoreRadius = innerRingRadius;
                }

                // Hold time (4 Hz internally — this call is cheap on the other frames).
                Vitals?.Poll(now);

                float ringSqr = innerRingRadius * innerRingRadius;
                Vector3 centroid = Vector3.zero;
                int liveCount = 0;

                for (int i = 0; i < live.Count; i++)
                {
                    Enemy e = live[i];
                    if (e == null || e.IsDead) continue;

                    centroid += e.transform.position;
                    liveCount++;

                    // --- roster union (WO-1113: reinforcements drip, so a census undercounts) ---
                    string defId = string.IsNullOrEmpty(e.EnemyDefId) ? "unknown" : e.EnemyDefId;
                    int id = e.GetInstanceID();
                    if (!_seen.Contains(id))
                    {
                        _seen.Add(id);
                        if (!_roster.TryGetValue(defId, out var row))
                        {
                            row = new AttackerUnitRecord { DefId = defId, Count = 0, Level = 1 };
                            _roster[defId] = row;
                        }
                        row.Count++;
                        if (e.Level > row.Level) row.Level = e.Level;
                    }

                    // --- ring crossing ---
                    if (!breachArmed || _crossed.Contains(id)) continue;
                    float planarSqr = Vector3.ProjectOnPlane(
                        e.transform.position - heartPos, Vector3.up).sqrMagnitude;
                    if (planarSqr > ringSqr) continue;

                    _crossed.Add(id);
                    RecordBreach(e);
                }

                SamplePath(now, centroid, liveCount);
            });
        }

        private readonly HashSet<int> _seen = new HashSet<int>();

        /// <summary>
        /// Records the force CENTROID — the approach polyline. Answering "which way did they
        /// come" is what makes a tower move-able; a per-unit trail would answer the same
        /// question at N times the cost and less legibly.
        ///
        /// <para>When the buffer is full it THINS (drops every other older sample) instead of
        /// truncating. Truncating would silently delete the APPROACH and keep only the ending,
        /// which is the half the player already watched — the opposite of useful.</para>
        /// </summary>
        private void SamplePath(float now, Vector3 centroidSum, int liveCount)
        {
            if (liveCount <= 0 || now < _nextPathSampleAt) return;
            _nextPathSampleAt = now + PathSampleInterval;

            Vector3 c = centroidSum / liveCount;
            _record.Path.Add(new AttackPathPoint
            {
                WorldX = c.x,
                WorldZ = c.z,
                AtSeconds = now,
                LiveCount = liveCount,
            });

            if (_record.Path.Count > MaxPathPoints)
            {
                for (int i = _record.Path.Count - 2; i > 0; i -= 2) _record.Path.RemoveAt(i);
                FlowTrace.Step("Siege",
                    $"path buffer full -- thinned to {_record.Path.Count} samples " +
                    "(kept the whole approach at half resolution; never truncated to the ending).");
            }
        }

        /// <summary>
        /// Records one inner-ring crossing. Public so a future producer (the WO-430-F
        /// fast-forward, or a real gate-destroyed event) can report a breach without going
        /// through the ring observer.
        /// </summary>
        public void RecordBreach(Enemy by)
        {
            TotalBreachCrossings++;

            if (_record.Breaches.Count >= MaxBreachRows)
            {
                FlowTrace.Throttle("Siege", "breach-rows-full", 2f,
                    $"breach row cap {MaxBreachRows} reached -- crossing {TotalBreachCrossings} " +
                    "counted but not rowed (the total is kept; the detail is truncated).");
                return;
            }

            Vector3 pos = by != null ? by.transform.position : Vector3.zero;
            ResolveNearestBarrier(pos, out string barrierId, out string barrierName);

            var b = new BreachRecord
            {
                BreachedId = barrierId,
                DisplayName = barrierName,
                WorldX = pos.x,
                WorldY = pos.y,
                WorldZ = pos.z,
                AtSeconds = ElapsedSeconds,
                AttackerDefId = by != null ? by.EnemyDefId : string.Empty,
            };
            _record.Breaches.Add(b);

            FlowTrace.Step("Siege",
                $"BREACH {b.DisplayName} @({b.WorldX:F0},{b.WorldZ:F0}) t={b.AtSeconds:F1}s by {b.AttackerDefId}.");
        }

        /// <summary>
        /// Names the crossing after the nearest Gate/WallSegment within
        /// <see cref="BreachNameRadius"/>. Beyond that the row reads "Open ground" —
        /// an honest label beats blaming a gate the attacker walked past.
        /// </summary>
        private static void ResolveNearestBarrier(Vector3 pos, out string id, out string name)
        {
            id = string.Empty;
            name = "Open ground";
            float bestSqr = BreachNameRadius * BreachNameRadius;

            var gates = Object.FindObjectsByType<Gate>(FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++)
            {
                var g = gates[i];
                if (g == null) continue;
                float d = (g.transform.position - pos).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d; id = g.name; name = g.name;
            }

            var walls = Object.FindObjectsByType<WallSegment>(FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                var w = walls[i];
                if (w == null) continue;
                float d = (w.transform.position - pos).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d; id = w.name; name = w.name;
            }
        }

        // =====================================================================
        //  Close
        // =====================================================================

        /// <summary>
        /// Settles the record ONCE, adapts the live damage aggregate into it, and returns it.
        /// Clears <see cref="Current"/>. The caller (SiegeScheduler) appends it to the ledger.
        /// Never throws. Calling twice returns the same settled record and traces the anomaly.
        /// </summary>
        public DefenseOutcomeRecord Close(DefenseOutcome outcome)
        {
            if (!ReferenceEquals(Current, this))
                FlowTrace.Warn("Siege", $"Close(wave={WaveId}) on a session that is not Current -- settling it anyway.");

            Guard.Try("Siege", "close siege session", () =>
            {
                _record.EndedAtUnixMs = SiegeClock.NowUnixMs();
                _record.DurationSeconds = ElapsedSeconds;

                // A cleared wave that let something across the ring is BREACHED, not HELD.
                // Overrun (the Heart fell) always wins — the caller passes it explicitly.
                if (outcome == DefenseOutcome.Held && _record.Breaches.Count > 0)
                    outcome = DefenseOutcome.Breached;
                _record.Outcome = outcome;

                // Roster union -> the attacker's fielded force + a power rating derived from it.
                _record.Attacker.Units.Clear();
                int power = 0;
                foreach (var kv in _roster)
                {
                    _record.Attacker.Units.Add(kv.Value);
                    power += kv.Value.Count * Mathf.Max(1, kv.Value.Level);
                }
                _record.Attacker.PowerRating = power;

                // THE DAMAGE AGGREGATE IS REUSED VERBATIM — WaveDamageReport.Collect() already
                // enumerates every damaged/destroyed player structure worst-first and priced.
                // We SERIALISE its output; we never re-scan the scene.
                DefenseReportBuilder.AdaptRows(WaveDamageReport.Collect(), _record.Rows);

                // ⭐ THE LEGIBILITY MERGE. The rows above say WHAT broke; this stamps WHERE it
                // stood, which BAND of the base it was in, and HOW LONG IT HELD -- the fields
                // that turn "Wall B destroyed" into "your east wall fell in 4s while the north
                // one held 40s". Merge only: no row is added, removed or re-priced here.
                DefenseReportBuilder.StampLegibility(_record, Vitals);

                // THE RULED LOSS (WO-1139, ruling 2026-08-22): COLLECTOR LOOTING ONLY, NO BANK
                // THEFT. This REPORTS what the broken collectors already lost (their own
                // LastLootStolen, summed) -- it computes nothing and takes nothing. It must run
                // AFTER AdaptRows above, so the ledger total and the per-collector "looted N" rows
                // describe the same set of breaks. SiegeScheduler.Settle then SEALS it.
                _record.ResourcesLost = DefenseReportBuilder.BuildStakes(_record);

                FlowTrace.Step("Siege",
                    $"CLOSED id={_record.Id} outcome={_record.Outcome} breaches={_record.Breaches.Count}" +
                    $"(of {TotalBreachCrossings} crossings) losses={_record.Rows.Count} " +
                    $"units={_record.Attacker.Units.Count} power={_record.Attacker.PowerRating} " +
                    $"dur={_record.DurationSeconds:F1}s.");
            });

            if (ReferenceEquals(Current, this)) Current = null;
            return _record;
        }

        /// <summary>Abandons the open session without producing a report (scene change, flag flip).
        /// Traced — a session that simply vanished would look like a scheduler that never fired.</summary>
        public static void Abandon(string why)
        {
            if (Current == null) return;
            FlowTrace.Warn("Siege", $"session for wave {Current.WaveId} ABANDONED: {why} (no report written).");
            Current = null;
        }
    }
}
