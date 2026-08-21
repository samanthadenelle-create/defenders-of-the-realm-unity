// =============================================================================
// UpcomingWaveWarmPlanner — warm the NEXT wave's enemy art, IN THE ORDER THE
// PLAYER WILL SEE IT, while the town is idle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER REQUEST (2026-08-20, verbatim): "can we set the enemies to loadin order
// of seeing them when they are placing buldings? We know the first two enemies,
// they came in as pills".
//
// ⛔ THE DEFECT THIS CLOSES, stated as a gap and not as a theory.
// EnemyContentWarmer.WarmFamily has existed since the 2026-08-20 per-family
// ruling and its own header says "a spawner that knows what it is about to spawn
// should call WarmFamily first". NOTHING CALLED IT AHEAD OF TIME. The only
// runtime call sites were EnemyAssetLoader.PrewarmFamily reached from
// EnemyAnimatorLateBinder / EnemyLateSkinner / EnemyFactory — i.e. at SPAWN time,
// on the very frame the first body is built. The download therefore started when
// the enemy was already standing in front of the player, and the player watched
// the download finish: that is the tinted capsule ("pill") the owner reported.
// The seam was right; nobody rang the bell early.
//
// -----------------------------------------------------------------------------
// WHERE IT IS RUNG, AND WHY THAT POINT.
// BuildModeController.Enter() — the player has opened Build Mode. Three
// properties make it the correct hook and no other candidate has all three:
//   1. It is DEAD TIME. Enter() calls FreezeWaves() on its next lines: nothing is
//      spawning, nothing is being fought, and the frame budget is a menu's.
//   2. It is the point the owner described — "when they are placing buldings".
//   3. It is the ONE funnel. Build / upgrade / sell / move all route through
//      Enter() (see its own comment), so one call covers every town-edit verb.
// It is deliberately NOT hooked at wave-countdown start: by then the fetch has
// only the countdown to finish in, and on a cold cache that is exactly the window
// that was already losing.
//
// -----------------------------------------------------------------------------
// ⛔ TWO SOURCES, BECAUSE THERE ARE GENUINELY TWO. Do not collapse them.
// The owner's "first two enemies" are almost certainly the FTUE teaching wave,
// and that wave does NOT come from WaveCompositionBuilder: TutorialWaveSpawner
// bypasses the wave loop entirely (it spawns EXACTLY 2 of its own
// PreferredEnemyId via WaveManager.SpawnEnemyForExternalMode). Warming the
// composed roster would therefore have warmed the wrong families for the exact
// encounter that was reported. So:
//   • FTUE not finished (GameState.Onboarded == false) -> the TUTORIAL roster's
//     families lead the plan, because those bodies are what the player meets next.
//   • Then, always, the upcoming COMPOSED wave's families, appended and deduped.
// If TutorialWaveSpawner ever changes its roster, it changes it in ONE place —
// its own PreferredEnemyId const, which this file reads rather than copies.
//
// -----------------------------------------------------------------------------
// WHY COMPUTING THE ROSTER EARLY IS SAFE (verified at source, not assumed).
// WaveCompositionBuilder.Build seeds UnityEngine.Random on the wave id and
// RESTORES UnityEngine.Random.state before returning. Running it early is
// therefore side-effect free AND yields the identical roster the wave will
// actually spawn — no divergence, no double-consumed RNG. That property is what
// makes look-ahead possible at all, so it is PINNED by a regression
// (EnemyWarmOrderRegression case 2) rather than trusted.
//
// -----------------------------------------------------------------------------
// ⛔ PER-FAMILY, IN ORDER, NEVER ALL. The owner's ruling is "broken down to each
// family of enemy" and the enemy payload is ~64 MB. This plan contains ONLY the
// families the upcoming encounter actually contains, and it issues them ONE AT A
// TIME so the first body's bundle gets the bandwidth first — the ordering IS the
// feature. Warming every discovered family would be faster to write, would look
// like an improvement, and is the precise cost the seam exists to avoid.
// EnemyWarmOrderRegression case 3 fails if someone "simplifies" it that way.
//
// ⛔ NOTHING HERE BLOCKS. There is no bounded synchronous wait in Addressables
// 2.9.1 (EnemyContentWarmer's header proves it to file and line), so this file
// contains no blocking wait of any kind and never will: every Addressables touch
// is EnemyContentWarmer.WarmFamily, which starts an operation and returns, driven
// from a coroutine on a DontDestroyOnLoad host — i.e. from the player loop, the
// one thread that can actually finish the operation. EnemyWarmOrderRegression
// case 4 source-scans this file for the blocking call.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Plans and issues the enemy-family warm for the encounter the player will meet next,
    /// in first-appearance order, from town dead time. Pure planning is separated from the
    /// coroutine so the ORDER can be asserted headlessly.
    /// </summary>
    public static class UpcomingWaveWarmPlanner
    {
        /// <summary>FlowTrace system tag. Distinct from "EnemyAssets" on purpose: that tag
        /// answers "did the bytes arrive", this one answers "did we ask early, and in what
        /// order". A future "they came in as pills again" is read from these two together.</summary>
        public const string System = "EnemyWarmOrder";

        /// <summary>
        /// How many waves ahead the plan looks. ⛔ ONE, and it is a named constant so raising
        /// it is a deliberate act. Each extra wave adds families the player may not meet for
        /// minutes, and every family is a bundle: a look-ahead of three is most of the enemy
        /// payload wearing a per-family costume. The next wave is the only one whose art is
        /// certain to be needed next.
        /// </summary>
        public const int LookaheadWaves = 1;

        /// <summary>
        /// How long the ordered issue-loop gives one family to land before moving to the next.
        /// ⛔ NOT A TIMEOUT ON A BLOCKING CALL — nothing is blocked. It is the point at which
        /// holding the queue for a slow family costs the families behind it more than it buys,
        /// so the loop proceeds and lets the downloads overlap. Exceeding it costs a Warn line.
        /// </summary>
        public const float PerFamilyWaitSeconds = 10f;

        /// <summary>Wall-clock ceiling for one whole ordered pass, so a pathological session
        /// cannot leave a coroutine grinding for the life of the process.</summary>
        public const float PlanDeadlineSeconds = 90f;

        // Families already issued this launch. WarmFamily is itself idempotent; this is here so
        // a player who opens and closes the build palette ten times produces ONE trace story
        // instead of ten identical ones.
        private static readonly HashSet<string> s_issued =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static Host s_host;
        private static bool s_passRunning;

        /// <summary>How many families this planner has issued a warm for this launch (diagnostics).</summary>
        public static int IssuedFamilyCount => s_issued.Count;

        // =====================================================================
        //  PLANNING — pure, deterministic, headless-provable. No Addressables.
        // =====================================================================

        /// <summary>
        /// The ordered, de-duplicated family list for one composed wave: walk
        /// <see cref="EnemyWaveComposition.Entries"/> in order, resolve each entry's id to the
        /// model the spawner will actually use, take that model's family, and keep the FIRST
        /// appearance of each.
        /// <para>⛔ WHY ENTRY ORDER IS ENCOUNTER ORDER: the composed roster is released through
        /// SmartEnemySpawner in entry order, and WO-1113's concurrency cap holds the overflow in
        /// a `deferred` list that is also drained in entry order. So the first entry is the first
        /// body built, every time. If that release order ever changes, THIS is the comment that
        /// is now wrong.</para>
        /// <para>⛔ The family is taken from the MODEL, never from the enemy id: FamilyOf splits
        /// on the first underscore, so the id "hollow-walker" would yield the nonsense family
        /// "hollow-walker" while its model "Hollow_Walker" yields the real family "Hollow" — the
        /// token the enemy addresses are actually grouped by.</para>
        /// </summary>
        public static List<string> PlanFamilies(EnemyWaveComposition composition, EnemyCatalog catalog = null)
        {
            var ordered = new List<string>();
            if (composition?.Entries == null) return ordered;

            for (int i = 0; i < composition.Entries.Count; i++)
                AppendFamilyOf(ordered, composition.Entries[i].EnemyId, catalog);

            return ordered;
        }

        /// <summary>
        /// The ordered family list for the FTUE teaching wave — a separate source on purpose
        /// (see the header): TutorialWaveSpawner never touches WaveCompositionBuilder. Its
        /// roster is a single id repeated, so this is one family, and it is read from the
        /// spawner's own constant so the two cannot drift.
        /// </summary>
        public static List<string> PlanTutorialFamilies(EnemyCatalog catalog = null)
        {
            var ordered = new List<string>();
            AppendFamilyOf(ordered, TutorialWaveSpawner.PreferredEnemyId, catalog);
            return ordered;
        }

        /// <summary>
        /// Build the composition for <paramref name="waveId"/> and return its ordered families.
        /// Wrapped in <see cref="Guard.Try"/>: a catalog problem in TOWN must log and degrade to
        /// an empty plan (no warm, spawn-time fetch as before), never throw at a player who is
        /// placing a building.
        /// </summary>
        public static List<string> PlanFamiliesForWave(int waveId, bool waveHasAuthoredHeavy, EnemyCatalog catalog)
        {
            List<string> ordered = new List<string>();

            Guard.Try(System, $"compute upcoming wave {waveId} roster", () =>
            {
                EnemyWaveComposition comp =
                    WaveCompositionBuilder.Build(waveId, waveHasAuthoredHeavy, catalog);
                ordered = PlanFamilies(comp, catalog);
            });

            return ordered;
        }

        /// <summary>
        /// The full plan for what the player meets next: the FTUE teaching wave's families FIRST
        /// while the tutorial is unfinished, then the upcoming composed wave's, deduped across
        /// both. <paramref name="ftuePending"/> is passed in rather than read here so the whole
        /// ordering rule is decidable headlessly.
        /// </summary>
        public static List<string> PlanEncounterFamilies(
            bool ftuePending, int upcomingWaveId, bool waveHasAuthoredHeavy, EnemyCatalog catalog)
        {
            var ordered = new List<string>();

            if (ftuePending)
            {
                // The teaching wave is what the player sees NEXT — before any composed wave —
                // so its family leads, whatever the wave schedule says.
                foreach (string fam in PlanTutorialFamilies(catalog)) AddUnique(ordered, fam);
            }

            foreach (string fam in PlanFamiliesForWave(upcomingWaveId, waveHasAuthoredHeavy, catalog))
                AddUnique(ordered, fam);

            return ordered;
        }

        /// <summary>Resolve one enemy id to the family of the model the spawner will build, and
        /// append it if it is not already in the plan (FIRST appearance wins — that is the order).</summary>
        private static void AppendFamilyOf(List<string> ordered, string enemyId, EnemyCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(enemyId)) return;

            // Prefer the CATALOG def: it carries the enemies.json modelKey, which is the first
            // authority for the model (WO-954). A bare id-only def would silently skip that and
            // could name a different family than the spawn will use.
            EnemyDef def = catalog != null ? catalog.Find(enemyId) : null;
            if (def == null) def = new EnemyDef { Id = enemyId };

            string model = EnemyFactory.ModelForEnemy(def);
            if (string.IsNullOrWhiteSpace(model)) return;

            AddUnique(ordered, EnemyContentWarmer.FamilyOf(model));
        }

        private static void AddUnique(List<string> ordered, string family)
        {
            if (string.IsNullOrWhiteSpace(family)) return;
            for (int i = 0; i < ordered.Count; i++)
                if (string.Equals(ordered[i], family, StringComparison.OrdinalIgnoreCase)) return;
            ordered.Add(family);
        }

        // =====================================================================
        //  ISSUING — asynchronous, ordered, from the player loop
        // =====================================================================

        /// <summary>
        /// THE TOWN HOOK. Call from town dead time (BuildModeController.Enter). Works out what
        /// the player meets next, traces the plan, and starts the ordered warm. Non-blocking,
        /// idempotent while a pass is running, and completely inert outside play mode.
        /// </summary>
        public static void WarmForTown()
        {
            if (!Application.isPlaying) return;
            if (s_passRunning) return;

            bool ftuePending = true;
            int upcomingWaveId = 1;
            bool authoredHeavy = false;
            EnemyCatalog catalog = null;
            string waveSource = "no WaveManager — assuming wave 1";

            Guard.Try(System, "read upcoming-encounter state", () =>
            {
                var gs = DeNelle.Core.State.GameStateService.Instance;
                ftuePending = gs?.State == null || !gs.State.Onboarded;

                WaveManager wm = WaveManager.Instance;
                if (wm != null && wm.TryDescribeUpcomingWave(out int id, out bool heavy, out EnemyCatalog cat))
                {
                    upcomingWaveId = id;
                    authoredHeavy  = heavy;
                    catalog        = cat;
                    waveSource     = $"WaveManager phase={wm.Phase} currentWave={wm.CurrentWaveId}";
                }
            });

            List<string> plan = PlanEncounterFamilies(ftuePending, upcomingWaveId, authoredHeavy, catalog);

            FlowTrace.Step(System,
                $"town warm plan: lookahead={LookaheadWaves} wave -> wave {upcomingWaveId} ({waveSource}, " +
                $"authoredHeavy={authoredHeavy}, catalog={(catalog != null ? "loaded" : "NULL")}, " +
                $"ftuePending={ftuePending}). ORDERED families = [{string.Join(" -> ", plan)}] " +
                $"({plan.Count} famil(ies)). This is first-appearance order: the family listed first is " +
                "the first body the player will see, and its bundle is requested first. Only these " +
                "families are fetched — never the whole enemy payload.");

            if (plan.Count == 0)
            {
                FlowTrace.Warn(System,
                    "town warm plan is EMPTY — no roster could be computed (no catalog yet, or the " +
                    "composition resolved to nothing). Nothing is broken and nothing is blocked: enemy " +
                    "art falls back to the existing spawn-time per-family fetch, which is exactly the " +
                    "behaviour that shows capsules for a moment. If this line appears every time the " +
                    "build palette opens, THAT is the defect to chase.");
                return;
            }

            EnsureHost();
            if (s_host == null)
            {
                FlowTrace.Warn(System, "no coroutine host could be created — ordered warm skipped this session.");
                return;
            }

            s_passRunning = true;
            Guard.Try(System, "start ordered warm pass", () => s_host.StartCoroutine(WarmInOrder(plan)));
        }

        /// <summary>
        /// Issue each family's warm ONE AT A TIME, in plan order, waiting (by yielding — never by
        /// blocking) for each to land before asking for the next. Sequencing is the point: firing
        /// all of them on one frame would let the last family's bytes compete with the first
        /// family's, and the first family is the one standing in front of the player.
        /// </summary>
        private static IEnumerator WarmInOrder(List<string> plan)
        {
            using var _ = FlowTrace.Enter(System, "WarmInOrder");
            float t0 = Time.realtimeSinceStartup;

            for (int i = 0; i < plan.Count; i++)
            {
                string family = plan[i];

                if (Time.realtimeSinceStartup - t0 > PlanDeadlineSeconds)
                {
                    FlowTrace.Warn(System,
                        $"ordered warm pass hit its {PlanDeadlineSeconds}s reporting deadline at position " +
                        $"{i + 1}/{plan.Count} ('{family}') — abandoning the remaining families to the " +
                        "spawn-time fetch. Nothing blocked; this is a reporting stop, not a timeout.");
                    break;
                }

                if (EnemyContentWarmer.IsFamilyLocal(family))
                {
                    FlowTrace.Step(System,
                        $"[{i + 1}/{plan.Count}] family '{family}' is ALREADY LOCAL — no fetch needed; " +
                        "moving to the next family in encounter order.");
                    s_issued.Add(family);
                    continue;
                }

                FlowTrace.Step(System,
                    $"[{i + 1}/{plan.Count}] ISSUING warm for family '{family}' (position {i + 1} in " +
                    "encounter order). Requested now so its bundle has the bandwidth before the " +
                    "families behind it.");
                s_issued.Add(family);

                // ⛔ The ONE Addressables touch on this path. WarmFamily starts a
                // DownloadDependenciesAsync and returns on the same frame; it downloads this
                // family's bundles ONLY and loads no asset.
                Guard.Try(System, $"warm family '{family}'", () => EnemyContentWarmer.WarmFamily(family));

                float issuedAt = Time.realtimeSinceStartup;
                while (!EnemyContentWarmer.IsFamilyLocal(family) &&
                       Time.realtimeSinceStartup - issuedAt < PerFamilyWaitSeconds)
                    yield return null;   // the player loop keeps running; this is the whole design

                float took = Time.realtimeSinceStartup - issuedAt;
                if (EnemyContentWarmer.IsFamilyLocal(family))
                    FlowTrace.Step(System,
                        $"[{i + 1}/{plan.Count}] family '{family}' LANDED after {took:F1}s — its bodies " +
                        "will skin on their first frame instead of arriving as tinted capsules.");
                else
                    FlowTrace.Warn(System,
                        $"[{i + 1}/{plan.Count}] family '{family}' has NOT landed after {took:F1}s " +
                        $"(downloading={EnemyContentWarmer.IsFamilyDownloading(family)}, " +
                        $"catalogState={EnemyContentWarmer.State}). Moving on so the families behind it " +
                        "are not held up — its download CONTINUES in the background and its bodies " +
                        "re-skin when it lands. If the player meets this family now, it wears capsules.");
            }

            FlowTrace.Step(System,
                $"ordered warm pass finished in {Time.realtimeSinceStartup - t0:F1}s — " +
                $"{s_issued.Count} famil(ies) issued this launch, " +
                $"{EnemyContentWarmer.RequestedFamilyCount} known to the warmer.");
            s_passRunning = false;
        }

        // =====================================================================
        //  Plumbing
        // =====================================================================

        private static void EnsureHost()
        {
            if (s_host != null) return;
            if (!Application.isPlaying) return;

            Guard.Try(System, "create warm-order host", () =>
            {
                var go = new GameObject("UpcomingWaveWarmPlanner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                s_host = go.AddComponent<Host>();
            });
        }

        /// <summary>DontDestroyOnLoad coroutine host. Separate from BuildModeController on purpose:
        /// the warm must survive the player closing the build palette (which is precisely when the
        /// wave that needs the art is about to start).</summary>
        private sealed class Host : MonoBehaviour
        {
            private void OnDestroy()
            {
                if (s_host == this) { s_host = null; s_passRunning = false; }
            }
        }
    }
}
