using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Gate/portal SEAM between a hub scene (e.g. MainCastle_Hall) and the overworld.
    ///
    /// PROXIMITY-based (not OnTriggerEnter): when the hero (tagged Player — WO-1513 removed
    /// the never-declared "HeroTarget" tag; see HeroLocator) comes
    /// within <see cref="ProximityRadius"/> of this object, it ensures the target scene is
    /// loaded additively and <c>WarpTo</c>s the hero across to <see cref="targetPosition"/>.
    ///
    /// WHY proximity: the castle and overworld each have their OWN baked NavMesh (two scenes
    /// overlaid at runtime). A NavMeshAgent hero stops at the castle navmesh EDGE and can't
    /// physically reach a trigger box just beyond it — the "invisible barrier" / two-scene
    /// seam. A distance check fires reliably AT that edge and carries the hero across. The
    /// OnTriggerEnter path is kept as a fallback for movers that DO trip physics triggers.
    ///
    /// Used by CastleHubBuilder to wire the south gate connection to the overworld.
    /// </summary>
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [Tooltip("Scene to load additively (must be in Build Settings).")]
        public string targetSceneName = "Main_Castle_Overworld";

        [Tooltip("World position the player should appear at in the target scene (after load).")]
        public Vector3 targetPosition = Vector3.zero;

        [Tooltip("If true, load additive (recommended for seamless world).")]
        public bool loadAdditive = true;

        [Tooltip("Hero within this distance (m) of the gate triggers the crossing. RAISE this if " +
                 "the hero stops short at the navmesh edge and never fires; LOWER it if it fires " +
                 "too early before you reach the gate.")]
        public float ProximityRadius = 6f;

        [Tooltip("Optional narrative prompt label. When non-empty it REPLACES the default " +
                 "'Travel to <destination>' text (e.g. 'Enter the enemy stronghold' on a story portal). " +
                 "Empty = the default friendly-destination label.")]
        public string promptOverride = "";

        [Tooltip("When true this trigger shows NO confirm prompt/button. Used by passive walk-across " +
                 "seams (the runtime RegionGate) where HeroLinkCrossing handles the cross, so the " +
                 "'Travel to <dest>' button is redundant verbiage that breaks the seamless feel.")]
        public bool suppressPrompt = false;

        // CONFIRM-TO-CROSS is now the ONLY behaviour (owner directive 2026-06-18, root-cause
        // fix). The serialized field is RETAINED only so the baked scene components keep
        // deserializing without a missing-field warning — it is NO LONGER read by the runtime.
        // A hero walking into range can NEVER auto-teleport: travel ALWAYS requires the
        // explicit "Travel to <destination>" tap prompt. The old auto-cross code path, the
        // Awake() runtime guard, and the OutpostConnector name check are all gone.
        // NOTE: external editor/injector code still writes this field; it is harmless now
        // (the runtime ignores it) and the field is kept public for that back-compat.
        public bool requireConfirm = true;

        private bool _fired;
        private Transform _hero;
        private bool _promptShown;   // confirm-mode: an in-range prompt is currently up

        // --- SEAMTRACE diagnostics (temporary; strip once the exit bug is closed) ---
        // Owner directive 2026-06-13: instrument the WHOLE exit path so one F8 run shows
        // exactly which step fires and where it dies. The key signal is _minDist: the
        // CLOSEST the hero ever gets to this gate. If _minDist never drops below
        // ProximityRadius, the hero physically can't reach the seam (navmesh edge / gap)
        // and Cross() can never fire — that's the all-4-borders-blocked root cause.
        private float _traceTimer;
        private float _minDist = float.MaxValue;
        private bool _announced;
        private bool _heroEverFound;

        // --- NEAREST-IN-RANGE-SEAM-WINS + reach-the-edge radius (RCA 2026-06-13) ---
        // The castle bakes 4 radial gate lanes but only ONE (west) connects to the hero's
        // navmesh island; N/E/S stall the NavMeshAgent ~35m short of their seam markers, so
        // the 12m trigger never reaches and the Press-F prompt never appears. Confirm-to-cross
        // removed the only reason the radius was kept small (auto-fade), so we widen confirm
        // seams to reach the hero at the mesh edge (he WARPS across — he need not touch the
        // marker). BUT a wide radius means the ~35m spawn can sit inside MULTIPLE gate spheres:
        // two overlapping seams would each show a prompt (flicker) and each poll F (double Cross).
        // Guard: each frame, only the seam NEAREST the hero among all in-range confirm seams
        // shows its prompt + accepts the cross. (Proper fix = repair the bake so all 4 lanes
        // connect and the radius can return to 12 — queued as a follow-up.)
        private const float ConfirmMinRadius = 40f;   // clears the ~36m navmesh-edge stall + margin
        private static readonly System.Collections.Generic.List<SceneTransitionTrigger> s_inRangeConfirm =
            new System.Collections.Generic.List<SceneTransitionTrigger>();
        private static int s_collectFrame = -1;
        private float _curDist = float.MaxValue;

        // ROOT-CAUSE FIX (owner directive 2026-06-18, core-loop blocker): the prompt
        // shows but no tap ever crosses. The Request() that drives the shared
        // MobileInteractButton USED to be issued from this component's LateUpdate().
        // But MobileInteractButton renders the visible button + runs the tap hit-test
        // in ITS Update(), and resets `_requestedThisFrame` in ITS LateUpdate(). Unity
        // runs ALL Update()s before ANY LateUpdate(), so a Request() raised in our
        // LateUpdate() arrives AFTER MobileInteractButton.Update() already ran (button
        // stays SetActive(false), no hit-test) and is then cleared by its LateUpdate()
        // before the next frame's render — so the on-screen button NEVER becomes
        // visible/tappable. (AutoPilot still crossed because InvokeActive()/IsActive
        // read the request state DIRECTLY, not the rendered button — masking the bug.)
        //
        // Every other working caller (BuildingInteractable, DungeonPortal, MineNode,
        // ArenaHeraldSpawner, ...) issues Request() from its OWN Update(). We now do the
        // same: the prompt + Request live in Update(). To keep the "single nearest
        // in-range seam owns the prompt" guard order-independent without depending on
        // LateUpdate, each seam resolves the nearest using the list COMPLETED during the
        // PREVIOUS frame (every seam's _curDist is fully populated by frame end). On the
        // very first in-range frame the list may be a frame stale; that only delays the
        // prompt by one frame, never produces a wrong/duplicate prompt.
        private static readonly System.Collections.Generic.List<SceneTransitionTrigger> s_prevFrameInRange =
            new System.Collections.Generic.List<SceneTransitionTrigger>();

        // Effective radius: confirm-to-cross is unconditional, so a navmesh-edge GATE LANE reaches to
        // ConfirmMinRadius — the prompt appears at the navmesh edge where the hero stalls and he WARPS
        // across (he need not touch the marker).
        //
        // #62 EXCEPTION: a WALK-UP STORY PORTAL (a narrative entrance the hero physically reaches, e.g.
        // the enemy stronghold mouth — marked by a non-empty promptOverride) must honor its AUTHORED
        // ProximityRadius, NOT the 40m floor. The 40m floor exists ONLY for the castle gate-lane
        // navmesh stall; applied to a walk-up portal it blows the prompt zone out to 40m so the player
        // can almost never step far enough back to clear it on retreat -> the "enter enemy stronghold"
        // prompt sticks. Bounding it to the authored entry zone makes retreat clear the prompt via the
        // existing MobileInteractButton.Release the moment the hero leaves that zone (same as how
        // DungeonPortal clears its prompt). (Correct long-term: the RegionGate/dungeon-entry primitive
        // OWNS the entry-zone + prompt state so retreat is an explicit state transition — see WO.)
        private bool IsWalkUpEntry => !string.IsNullOrEmpty(promptOverride) && !suppressPrompt;
        private float EffRadius => IsWalkUpEntry
            ? ProximityRadius
            : Mathf.Max(ProximityRadius, ConfirmMinRadius);

        // =====================================================================
        // ROOT-CAUSE FIX (owner directive 2026-06-18): auto-cross is GONE.
        // ---------------------------------------------------------------------
        // History: the OutpostConnector_* seams were baked with requireConfirm:0, and
        // prior fixes tried to flip the field back on at runtime (Awake guard +
        // OutpostConnectorConfirmInjector). Those were field-dependent and unreliable —
        // the proximity AUTO-CROSS branch could fire on the boot/additive-load frame
        // BEFORE any guard ran, teleporting the hero with no tap (proven in Player.log).
        //
        // The durable fix is to DELETE the auto-cross code path entirely. There is now
        // no requireConfirm==false branch anywhere in this component, so no value of the
        // serialized field — baked false or otherwise — can ever auto-teleport. Travel
        // is tap-only for EVERY SceneTransitionTrigger. The Awake() guard and the
        // NameMarksOutpostConnector helper were removed as now-dead code.

        private bool _confirmAnnounced;   // FlowTrace proof line emitted once per trigger

        // Set TRUE only by the LateUpdate tap callback, immediately before it calls Cross().
        // Cross() asserts this is set — if it is ever reached with the flag still false, a
        // non-tap (proximity / auto / trigger-enter) caller has leaked back in, which is the
        // exact impossible state that cost 3 passes. The assertion fires loud (Fail rolls up).
        private bool _tapInitiated;

        // SINGLE-LOAD HERO CARRY (RCA: the "purple emergency pill") — set TRUE in Cross()
        // ONLY on the Single-load (!loadAdditive) branch, right after we DontDestroyOnLoad
        // the hero root so it survives the scene swap. RepositionPlayerAfterLoad reads it to
        // re-home the carried hero into the freshly-active target scene after the warp, so the
        // DDOL'd hero unloads normally on the NEXT transition instead of leaking/duplicating.
        // Additive seams never set this (the hero already survives an additive load).
        private bool _carriedHero;

        private void Update()
        {
            if (!_announced)
            {
                _announced = true;
                Debug.Log($"[SeamTrace] '{name}' ONLINE  target={targetSceneName}@{targetPosition}  gatePos={transform.position}  radius={ProximityRadius}m");
            }

            if (!_confirmAnnounced)
            {
                _confirmAnnounced = true;
                FlowTrace.Step("Seam",
                    $"'{name}' confirm-to-cross enforced — tap required, no auto-teleport");
            }

            if (_fired) return;
            if (_hero == null) _hero = ResolveHero();
            if (_hero == null)
            {
                _traceTimer += Time.deltaTime;
                if (_traceTimer >= 2f)
                {
                    _traceTimer = 0f;
                    Debug.LogWarning($"[SeamTrace] '{name}' still has NO hero (no Player tag and no HeroLocomotion).");
                }
                return;
            }
            if (!_heroEverFound)
            {
                _heroEverFound = true;
                Debug.Log($"[SeamTrace] '{name}' resolved hero '{_hero.name}' at {_hero.position}.");
            }

            float dist = Vector3.Distance(_hero.position, transform.position);
            if (dist < _minDist) _minDist = dist;

            _traceTimer += Time.deltaTime;
            if (_traceTimer >= 1f)
            {
                _traceTimer = 0f;
                Debug.Log($"[SeamTrace] '{name}' heroDist={dist:F1}m  closestEver={_minDist:F1}m  radius={EffRadius}m  {(dist <= EffRadius ? "IN-RANGE" : "out")}");
            }

            bool inRange = (_hero.position - transform.position).sqrMagnitude <= EffRadius * EffRadius;

            // OUTPOST FAST-TRAVEL GATE (owner 2026-06-19): while ff.outposttravel is OFF, a seam
            // whose destination is a garrison / raid OUTPOST (Garrison_* / Outpost_* / RaidBase_*)
            // must NOT offer its "Travel to <outpost>" prompt — reaching an outpost is earned by
            // walking (WO-453), not fast-travelled. The castle<->overworld crossing is NOT an
            // outpost destination, so it is never gated and continues to work. We force this seam
            // to read as out-of-range so it neither shows a prompt nor wins the nearest-seam contest.
            if (inRange && IsTravelGated())
            {
                FlowTrace.Once("Seam", $"{name}-travelgated",
                    $"'{name}' -> '{targetSceneName}' outpost fast-travel is feature-flagged OFF " +
                    "(ff.outposttravel) — prompt suppressed; walk to earn it.");
                inRange = false;
            }

            // CONFIRM-TO-CROSS (unconditional): NEVER auto-teleport. We REGISTER in-range
            // interest here and then resolve the prompt + Request for the single NEAREST
            // in-range seam — all WITHIN Update() (see ResolvePromptAndRequest + the
            // root-cause note above) so overlapping wide gate spheres never flicker two
            // prompts or double-fire Cross.
            // On the FIRST seam to touch the list this frame, roll the now-complete list
            // built last frame into s_prevFrameInRange (the snapshot the nearest-seam
            // decision reads), then clear the live list to start collecting this frame.
            if (Time.frameCount != s_collectFrame)
            {
                s_collectFrame = Time.frameCount;
                s_prevFrameInRange.Clear();
                s_prevFrameInRange.AddRange(s_inRangeConfirm);
                s_inRangeConfirm.Clear();
            }
            if (inRange)
            {
                // Trace the ABSENCE the old silent Awake guard hid: prove, ONCE per seam, that
                // the new confirm-to-cross path is actually live the moment the hero is in range.
                FlowTrace.Once("Seam", $"{name}-armed",
                    $"'{name}' confirm-to-cross armed — in range ({dist:F1}m <= {EffRadius}m), tap required to cross");
                _curDist = dist;
                if (!s_inRangeConfirm.Contains(this)) s_inRangeConfirm.Add(this);
            }
            else
            {
                _curDist = float.MaxValue;
                if (_promptShown)
                {
                    // Left range — drop the prompt immediately.
                    MobileInteractButton.Release(this);
                    _promptShown = false;
                }
            }

            // CONFIRM-mode prompt + Request, resolved to the single NEAREST in-range
            // confirm seam — issued HERE in Update() (NOT LateUpdate) so the shared
            // MobileInteractButton, which renders the visible button + runs its tap
            // hit-test in its own Update(), actually SEES the request this frame and
            // shows a real, tappable on-screen button. (See the root-cause note above.)
            ResolvePromptAndRequest();
        }

        // Resolve the single nearest in-range confirm seam and, if that is THIS seam,
        // keep the shared "Travel to <dest>" button requested every frame so it stays
        // visible + tappable. Order-independent: it reads the PREVIOUS frame's completed
        // in-range list (s_prevFrameInRange), so it does not matter which seam's Update()
        // runs first this frame. Called once per seam per frame from Update().
        // True when this seam's destination is an enemy raid OUTPOST scene (Garrison_* / Outpost_* /
        // RaidBase_*) rather than the OuterWorld / hub crossing. Used only to gate outpost fast-travel.
        private bool IsOutpostDestination()
        {
            string t = targetSceneName;
            if (string.IsNullOrEmpty(t)) return false;
            return t.StartsWith("Garrison", System.StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Outpost",  System.StringComparison.OrdinalIgnoreCase)
                || DeNelle.Core.HubScenes.IsRaid(t);
        }

        // Owner 2026-06-19: hide the "Travel to <outpost>" fast-travel prompt until earned. Gated ONLY
        // for outpost destinations; the OuterWorld / hub crossing is never gated. Flip ff.outposttravel
        // ON to restore the prompt.
        private bool IsTravelGated()
        {
            // A WALK-UP entry (a physical proximity door carrying a promptOverride — e.g. the OuterWorld
            // CavePortal into Outpost1, or the chain's "Enter the Dungeon"/"...Outpost2" doors) IS the
            // earned walk: reaching it means you walked there. The travel-gate (WO-453, ff.outposttravel
            // OFF) exists to block FAST-TRAVEL to outposts from afar, NOT to lock the player out of a
            // door they physically walked up to. So a walk-up entry is never travel-gated. (owner F8
            // 2026-06-30: "no options to port" — the CavePortal->Outpost1 prompt was suppressed here.)
            if (IsWalkUpEntry) return false;
            return IsOutpostDestination() && !DeNelle.Core.FeatureFlags.OutpostTravel;
        }

        private void ResolvePromptAndRequest()
        {
            if (_fired) return;

            // Not in range this frame → ensure our prompt is down.
            if (_curDist == float.MaxValue)
            {
                if (_promptShown) { MobileInteractButton.Release(this); _promptShown = false; }
                return;
            }

            // Find the nearest in-range confirm seam (from last frame's complete snapshot;
            // falls back to the live list on the very first frame before a snapshot exists).
            var pool = s_prevFrameInRange.Contains(this) ? s_prevFrameInRange : s_inRangeConfirm;
            SceneTransitionTrigger nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                var t = pool[i];
                if (t != null && t._curDist < best) { best = t._curDist; nearest = t; }
            }
            if (nearest == null) nearest = this; // only seam in range

            // Not the winner → yield the shared prompt to the nearer seam.
            if (nearest != this)
            {
                if (_promptShown) { MobileInteractButton.Release(this); _promptShown = false; }
                return;
            }

            // Passive walk-across seam (RegionGate): HeroLinkCrossing handles the cross, so show NO
            // prompt -- the button is redundant verbiage that breaks the seamless feel (owner 2026-06-23).
            if (suppressPrompt)
            {
                if (_promptShown) { MobileInteractButton.Release(this); _promptShown = false; }
                return;
            }

            // We are the nearest in-range seam: own the prompt + the confirmed cross.
            string dest = FriendlyDestinationName();
            // A story portal can override the label with its own narrative line
            // (e.g. "Enter the dungeon"); else use the default "Travel to <dest>".
            string label = string.IsNullOrEmpty(promptOverride) ? $"Travel to {dest}" : promptOverride;
            if (!_promptShown)
            {
                FlowTrace.Step("Seam", $"prompt built for '{name}' -> '{label}' ({_curDist:F1}m, radius {EffRadius}m) [Update path]");
                Debug.Log($"[SeamTrace] '{name}' NEAREST in-range ({_curDist:F1}m, radius {EffRadius}m) -> showing CONFIRM prompt '{label}'.");
                // STATE MUTATION: mark the prompt up so we don't re-log every frame.
                _promptShown = true;
            }

            // Re-issue the request EVERY frame (the shared button clears its claim each
            // LateUpdate, so a one-shot Request would vanish next frame). Issued from
            // Update() so MobileInteractButton.Update() renders + hit-tests it this frame.
            // The tap callback is wrapped so the log PROVES the cross came from a TAP.
            MobileInteractButton.Request(this, label, () =>
            {
                FlowTrace.Step("Seam", $"TAP -> Cross '{name}' (confirmed by player, dest '{dest}')");
                // STATE MUTATION: mark this cross as tap-initiated so Cross()'s assertion passes.
                _tapInitiated = true;
                Cross(_hero);
            });
        }

        // LateUpdate confirms the shared button actually RENDERED visible this frame for
        // our request — the proof point the old LateUpdate-Request path could never emit
        // (the button never became visible). MobileInteractButton.Update() has run by now
        // (Update precedes LateUpdate), so IsShowingFor(this) is true ONLY if a real,
        // tappable on-screen button is up for this seam. Emitted once per prompt session.
        private void LateUpdate()
        {
            if (_fired) return;
            if (_promptShown && !_buttonShownProven && MobileInteractButton.IsShowingFor(this))
            {
                _buttonShownProven = true;
                FlowTrace.Step("Seam",
                    $"button-shown: on-screen 'Travel' button VISIBLE + tappable for '{name}' (MobileInteractButton.IsShowingFor)");
            }
            else if (!_promptShown)
            {
                _buttonShownProven = false; // re-arm the proof for the next prompt session
            }
        }

        private bool _buttonShownProven;

        // OnTriggerEnter is intentionally a NO-OP for crossing: confirm-to-cross is now
        // unconditional, so trigger-enter must NOT auto-cross either. The player always has to
        // tap the on-screen Interact button. The Update() proximity loop registers in-range
        // interest and LateUpdate shows the prompt + handles the confirmed crossing. (Method
        // kept empty-of-cross deliberately — there is no path here that can teleport the hero.)

        // Release the shared interact button if this gate is torn down while prompting.
        private void OnDisable()
        {
            if (_promptShown)
            {
                MobileInteractButton.Release(this);
                _promptShown = false;
            }
        }

        // Map the raw target scene name to a friendly, player-facing destination.
        private string FriendlyDestinationName()
        {
            string s = targetSceneName ?? "";
            switch (s)
            {
                case "OuterWorld": return "the Outer World";  // WO-608: legacy, remapped to Main_Castle_Overworld at runtime
                case "Garrison_troll_outpost": return "Troll Outpost";
                case "Garrison_ruined_keep": return "Ruined Keep";
                case "Garrison_frost_keep": return "Frost Keep";
                case "MainCastle_Hall": return "the Castle";
                case "Main_Castle_Overworld": return "the Castle";   // WO-608: merged single-scene home hub (ff.MergedWorld)
                case "Village2": return "the Village";
            }
            // Generic cleanup: strip a leading "Garrison_" / "Outpost_" prefix and
            // Title-Case the underscore-separated remainder (e.g. "Garrison_dark_cave"
            // -> "Dark Cave"). Falls back to the raw name if empty.
            string body = s;
            if (body.StartsWith("Garrison_")) body = body.Substring("Garrison_".Length);
            else if (body.StartsWith("Outpost_")) body = body.Substring("Outpost_".Length);
            body = body.Replace('_', ' ').Trim();
            if (body.Length == 0) return string.IsNullOrEmpty(s) ? "the destination" : s;

            var parts = body.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) +
                           (parts[i].Length > 1 ? parts[i].Substring(1) : "");
            }
            return string.Join(" ", parts);
        }

        private void Cross(Transform player)
        {
            // Ride the whole cross thread down — one Enter scope nests every sub-step + the
            // RepositionPlayerAfterLoad coroutine's Step lines under it, so a single run renders
            // the full nested seam thread top-to-bottom and shows exactly where it stopped.
            using var _ = FlowTrace.Enter("Seam", $"Cross '{name}' -> {targetSceneName}");

            // ASSERT THE IMPOSSIBLE STATE: confirm-to-cross is the ONLY path. Cross() must be
            // reached exclusively from the LateUpdate tap callback (which sets _tapInitiated just
            // before calling). If it is ever reached without that flag, an AUTO-CROSS has leaked
            // back in — fail LOUD (rolls up to BreakCaptureHarness) instead of silently teleporting.
            if (!_tapInitiated)
                FlowTrace.Fail("Seam",
                    $"AUTO-CROSS reached on '{name}' — should be impossible (confirm-to-cross only; Cross() entered without a tap)");
            // Consume the latch so a re-entry (or a retry after a non-loadable abort) must be
            // re-armed by a fresh tap.
            _tapInitiated = false;

            // WO-608 RETURN-TARGET REMAP (ff.mergedworld): the standalone "OuterWorld" and
            // "MainCastle_Hall" scenes are RETIRED under the merge. A dungeon/outpost/arena/seam
            // that still targets them must NOT load a retired scene:
            //   • If we are ALREADY on the merged overworld scene, the castle<->overworld
            //     crossing is an in-scene WALK now — no-op the cross (both regions are here).
            //   • Otherwise (returning from a dungeon/arena/Village2), REMAP the target to the
            //     merged scene name so the return lands on Main_Castle_Overworld.
            // OFF path is byte-identical to today (whole block gated by MergedWorld).
            if (DeNelle.Core.FeatureFlags.MergedWorld &&
                (targetSceneName == "OuterWorld" || targetSceneName == "MainCastle_Hall"))
            {
                string activeName = SceneManager.GetActiveScene().name;
                if (DeNelle.Core.HubScenes.IsOverworld(activeName))
                {
                    FlowTrace.Step("Seam",
                        $"'{name}' -> '{targetSceneName}' retired under ff.mergedworld; already on merged '{activeName}' — in-scene walk, no cross.");
                    return;
                }
                FlowTrace.Step("Seam",
                    $"'{name}' target '{targetSceneName}' retired under ff.mergedworld — remapped to '{DeNelle.Core.SceneRouter.Castle}'.");
                targetSceneName = DeNelle.Core.SceneRouter.Castle;
            }

            if (_fired || player == null)
            {
                FlowTrace.Step("Seam", $"Cross no-op (fired={_fired}, player={(player != null ? player.name : "null")})");
                return;
            }
            // STATE MUTATION: latch _fired so a second tap/proximity can never re-enter.
            _fired = true;
            FlowTrace.Step("Seam", $"_fired=true latched; resolved hero '{player.name}' @ {player.position}");
            Debug.Log($"[SeamTrace] '{name}' Cross() ENTERED for '{player.name}'.");

            var targetScene = FlowTrace.Try("Seam", "GetSceneByName",
                () => SceneManager.GetSceneByName(targetSceneName), default);
            if (!targetScene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
                {
                    FlowTrace.Fail("Seam", $"ABORT: '{targetSceneName}' not in Build Settings — cannot transition");
                    Debug.LogWarning($"[SeamTrace] '{name}' Cross() ABORT: '{targetSceneName}' not in Build Settings — cannot transition.");
                    // STATE MUTATION (rollback): un-latch _fired so a retry is possible once loadable.
                    _fired = false;
                    FlowTrace.Step("Seam", "_fired=false rolled back (target not loadable)");
                    return;
                }

                FlowTrace.Step("Seam", $"load-additive: '{targetSceneName}' {(loadAdditive ? "Additive" : "Single")} (was NOT loaded)");
                Debug.Log($"[SeamTrace] '{name}' Cross() loading '{targetSceneName}' {(loadAdditive ? "additive" : "single")} (was not loaded).");

                // SINGLE-LOAD HERO CARRY (RCA: the "purple emergency pill") — an Additive load
                // keeps the previous scene (and the hero) alive, but a Single load DESTROYS every
                // root in the old scene, including the hero. When that happened the captured
                // `player` Transform died mid-load → RepositionPlayerAfterLoad saw a dead ref →
                // WarpTo no-op'd, and HeroControlEnsurer (finding no Player) spawned the purple
                // emergency pill at the origin. Fix: BEFORE the Single load, mark the hero ROOT
                // DontDestroyOnLoad so it survives the swap and the same live Transform can be
                // warped into the target scene. (Additive: do NOT DDOL — it already survives.)
                if (!loadAdditive)
                {
                    // Carry ONLY the hero — NOT player.root. In the merged overworld the Player is nested
                    // under CastleHubRoot, which ALSO holds WaveManager + HeartController + the Tree of Life;
                    // DDOL'ing player.root dragged the WHOLE hub into the arena/outpost (owner F8 2026-07-10
                    // "why is there a tree of life in map", + a stray wave countdown running there). Detach the
                    // hero from its parent first so its new root is itself, then DDOL just the hero GameObject.
                    // Fixes every Single-load seam out of the overworld (outposts, dungeons, arenas, Village2).
                    var loco = player.GetComponentInParent<HeroLocomotion>();
                    var heroGo = loco != null ? loco.gameObject : (player != null ? player.gameObject : null);
                    if (heroGo != null)
                    {
                        if (heroGo.transform.parent != null)
                            heroGo.transform.SetParent(null, true);   // detach from CastleHubRoot, keep world pose
                        Object.DontDestroyOnLoad(heroGo);
                        _carriedHero = true;
                        FlowTrace.Step("Seam",
                            $"carry: DontDestroyOnLoad hero '{heroGo.name}' (detached from hub root) across Single load to '{targetSceneName}'");
                    }
                    else
                    {
                        FlowTrace.Warn("Seam",
                            $"carry: could not resolve hero for Single load to '{targetSceneName}' — hero may be lost (pill risk)");
                    }
                }

                FlowTrace.Try("Seam", "LoadScene",
                    () => SceneManager.LoadScene(targetSceneName, loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single));
            }
            else
            {
                FlowTrace.Step("Seam", $"already-loaded: '{targetSceneName}' — repositioning only");
                Debug.Log($"[SeamTrace] '{name}' Cross() target '{targetSceneName}' already loaded — repositioning.");
            }

            FlowTrace.Step("Seam", "starting RepositionPlayerAfterLoad coroutine (fade -> warp -> fade)");
            // PERSISTENT HOST (owner F8 2026-06-30): on a SINGLE load the SOURCE scene (this trigger's
            // scene, e.g. overworld) UNLOADS — destroying THIS component and KILLING the coroutine
            // before it warps the hero (the trace died right after 'fade-to-black'; the hero kept its
            // carry position instead of seating at the entry). The hero root is already DDOL'd above,
            // so host the reposition on the hero's own (surviving) MonoBehaviour. Additive loads keep
            // this trigger alive, so 'this' stays the host there.
            MonoBehaviour coHost = this;
            if (!loadAdditive && player != null)
            {
                var heroMb = player.GetComponentInParent<HeroLocomotion>();
                if (heroMb == null) heroMb = player.GetComponentInChildren<HeroLocomotion>();
                if (heroMb != null) coHost = heroMb;
                else FlowTrace.Warn("Seam", "no HeroLocomotion to host reposition across Single load — using this (may die on unload).");
            }
            coHost.StartCoroutine(RepositionPlayerAfterLoad(player));
        }

        private Transform ResolveHero()
        {
            // WO-1513: the old second term read the "HeroTarget" tag, which
            // TagManager.asset has never declared — a permanently dead branch. The
            // guarded "Player" read plus the definitive HeroLocomotion fallback
            // (CLAUDE.md §7) now live once in HeroLocator, which is also the seam this
            // seam-trigger must use: it builds a fade-overlay Image, so the UI-MVVM
            // oracle classifies it as a View and bans a direct scene scan here.
            return HeroLocator.ResolveTransform();
        }

        // ---------------------------------------------------------------------
        // WO-410/seam-pop: code-built fade-to-black mask around the warp.
        // The old crossing was a one-frame teleport + camera hard-cut (a harsh
        // visual POP). We now fade to black, snap the hero under the black,
        // let the camera settle, then fade back in. UXML is deliberately NOT
        // used (it doesn't render in player builds) — this is a pure-code
        // ScreenSpaceOverlay Canvas + Image + CanvasGroup, created lazily and
        // cached + DontDestroyOnLoad so it survives the additive load.
        // ---------------------------------------------------------------------
        private static CanvasGroup s_fadeGroup;

        private static CanvasGroup EnsureFadeOverlay()
        {
            if (s_fadeGroup != null) return s_fadeGroup;

            var go = new GameObject("__SceneTransitionFade");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // draw above everything (HUD included)

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            s_fadeGroup = group;
            return group;
        }

        private static IEnumerator FadeTo(CanvasGroup group, float target, float duration)
        {
            if (group == null) yield break;
            float start = group.alpha;
            if (duration <= 0f) { group.alpha = target; yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // unscaled: survives any timeScale pause
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = target;
        }

        private IEnumerator RepositionPlayerAfterLoad(Transform playerTransform)
        {
            // Capture seam identity NOW: on a Single load this trigger's SOURCE scene UNLOADS partway
            // through this coroutine, destroying this component — so a native `name` access AFTER that
            // throws NRE and ABORTS the coroutine before the re-home step below (owner F8 2026-06-30,
            // line 643; that abort also left the carried hero in the DDOL scene = leak/dupe next hop).
            // targetPosition/targetSceneName are managed fields (safe post-destroy); only the native
            // `.name` getter throws, so snapshot it up front.
            string seamName = name;

            // (1) Fade to black BEFORE the snap so the teleport + camera cut
            //     happen unseen.
            FlowTrace.Step("Seam", "reposition: fade-to-black (0.25s)");
            var fade = EnsureFadeOverlay();
            yield return FadeTo(fade, 1f, 0.25f);

            // Give the additive scene a moment to activate objects / nav (under black).
            FlowTrace.Step("Seam", "reposition: waiting for additive scene activation (under black)");
            yield return new WaitForSeconds(0.15f);
            yield return null; // extra safety frame

            if (playerTransform != null)
            {
                // WO-383: don't hard-set transform.position — that fights HeroLocomotion's
                // off-mesh clamp + NavMeshAgent every frame (the "camera/direction break at the
                // gate" bug). Use the teleport-aware WarpTo: disables the agent, moves it,
                // re-warps onto the destination (additively-loaded) NavMesh, and raises
                // OnTeleported so the follow camera snaps instead of chasing the jump.
                var loco = playerTransform.GetComponent<HeroLocomotion>();
                if (loco != null)
                {
                    // STATE MUTATION: warp the hero (disables agent, moves, re-warps onto
                    // destination NavMesh, re-enables agent, raises OnTeleported). Try-wrapped
                    // so a warp throw rolls up instead of leaving the hero stranded under black.
                    FlowTrace.Step("Seam", $"warp via HeroLocomotion.WarpTo({targetPosition}) (disable->move->re-enable agent)");
                    FlowTrace.Try("Seam", "HeroLocomotion.WarpTo", () => loco.WarpTo(targetPosition));
                }
                else
                {
                    // Fallback (no HeroLocomotion): land on the nearest valid NavMesh point.
                    FlowTrace.Warn("Seam", "no HeroLocomotion — fallback NavMesh.SamplePosition warp");
                    Vector3 dest = FlowTrace.Try("Seam", "NavMesh.SamplePosition",
                        () => NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas) ? hit.position : targetPosition,
                        targetPosition);
                    // STATE MUTATION: hard-set position (no agent to warp).
                    playerTransform.position = dest;
                }

                var rb = playerTransform.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;

                FlowTrace.Step("Seam", $"repositioned: requested {targetPosition}, hero now @ {playerTransform.position} in '{targetSceneName}' (loco={(loco != null)})");
                Debug.Log($"[SeamTrace] '{seamName}' repositioned: requested {targetPosition}, hero now at {playerTransform.position} in '{targetSceneName}' (loco={(loco != null)}).");

                // SINGLE-LOAD HERO CARRY — re-home step (only when WE DDOL'd the hero above).
                // A DontDestroyOnLoad object lives in the special DDOL scene, NOT the target
                // scene; if we leave it there it would survive (leak/duplicate) the NEXT Single
                // load. Now that the target scene is active and the hero is warped into it, move
                // the hero root back into the active scene so it unloads normally on the next
                // transition. Guarded: only when _carriedHero (we actually DDOL'd it).
                if (_carriedHero && playerTransform.root != null)
                {
                    FlowTrace.Try("Seam", "MoveHeroToActiveScene", () =>
                    {
                        var active = SceneManager.GetActiveScene();
                        if (active.IsValid() && active.isLoaded)
                        {
                            SceneManager.MoveGameObjectToScene(playerTransform.root.gameObject, active);
                            FlowTrace.Step("Seam", $"carry: re-homed hero into active scene '{active.name}'");
                        }
                    });
                }
            }

            // (3) Let the follow camera settle on the warped hero for one frame
            //     under the black, THEN (4) fade back in.
            yield return null;

            // FIX B (FLAGGED, NOT APPLIED): the castle (MainCastle_Hall) stays
            // fully loaded + rendering behind OuterWorld here, which feeds the
            // WO-410 framerate collapse. Deactivating its roots is NOT safe yet:
            //   • RegionMobSpawner.ResolveHeart() (OuterWorld) calls
            //     FindAnyObjectByType<HeartController>() — HeartController is
            //     castle-owned and FindAnyObjectByType ignores INACTIVE objects,
            //     so deactivation would null the heart the mob spawner tethers to.
            //   • The crossing is ONE-WAY: there is no OuterWorld->castle return
            //     seam wired, so we can't guarantee a clean re-load on return.
            // Deactivation needs the return-seam WO + a heart-reference fix first.

            FlowTrace.Step("Seam", "reposition: fade-back-in (0.35s) — cross thread complete");
            yield return FadeTo(fade, 0f, 0.35f);
        }
    }
}
