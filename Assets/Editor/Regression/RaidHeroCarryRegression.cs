// =============================================================================
// RaidHeroCarryRegression (WO-1109) — the raid hero is the TOWN hero, carried,
// NOT the emergency fallback. Proven headless in milliseconds, no play mode.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS (found 2026-08-16, and it survived for months precisely
// because it was PLAYABLE):
//   SceneRouter.GoRaid did no DontDestroyOnLoad marking of the hero. A raid loads
//   SINGLE, so the town hero was destroyed with the town; HeroControlEnsurer's
//   TryRecoverCarriedHero (keyed on the DontDestroyOnLoad scene) then found
//   nothing, and Ensure() fell through to SpawnEmergencyHero() — whose FIRST line
//   is FlowTrace.Fail("Hero", "EMERGENCY pill spawned..."). So a Fail landed in the
//   F8 break-log on EVERY SINGLE RAID ENTRY. Per CLAUDE.md sec.14 the owner's F8
//   captures are triaged live, so a permanent, expected Fail trains every seat (and
//   the owner) to ignore a Fail from the Hero system. A trace that always fires is
//   worse than no trace. And the hero the player actually drove was a fabricated
//   rig with no HeroAbilities (no mana, no casting), no root CapsuleCollider, and a
//   lavender capsule body until HeroBodySwapper landed a real FBX.
//
//   The comment at HeroControlEnsurer.cs:36-41 asserted, as fact, that a
//   "RaidHeroSpawner builds the REAL class body one frame after load". NO SUCH TYPE
//   EVER EXISTED. That is CLAUDE.md's "comments lie" warning in its purest form —
//   the comment WAS the bug, because it made the broken path look designed.
//
// WHY A SOURCE-STRUCTURAL ORACLE (and not a play-mode test): no harness ever loads
// a RaidBase_* scene (that gap is WO-1111's own ticket), so there is nothing to
// assert against at runtime today. Every invariant below is decidable from the REAL
// .cs / .unity text on disk — the same file-scan idiom SceneRoutingRegression uses
// for the WO-777 dungeon-entry oracle and the compile gate uses for its NUL scan.
// It therefore proves the SHIPPED source, never a re-derivation of it.
//
// SEVEN INVARIANTS, each a hard failure:
//   1. GoRaid ARMS the carry (calls CarryHeroAcrossSingleLoad in its own body).
//   2. GoRaid arms it only for a REGISTERED scene — a hero left detached + DDOL by
//      an aborted load would be dragged into the next Single load instead.
//   3. CarryHeroAcrossSingleLoad exists and actually calls DontDestroyOnLoad.
//   4. Ensure() RE-HOMES a carried hero that is still parked in the DDOL scene.
//      This is the leak guard: FindLoco() returns DDOL objects, so without it the
//      carried hero is "found", TryRecoverCarriedHero never runs, and the hero
//      lives in DDOL for the rest of the session — surviving every later Single
//      load and stacking against each scene's own baked hero.
//   5. The EMERGENCY ALARM IS INTACT: SpawnEmergencyHero still opens with a
//      FlowTrace.Fail carrying the "EMERGENCY pill spawned" text. Downgrading it to
//      a Warn or deleting it would silence the alarm instead of the fault, which
//      CLAUDE.md sec.12 forbids ("NEVER STRIP FLOWTRACE"). After WO-1109 that Fail
//      firing in a raid MEANS something again, so it must keep its severity.
//   6. The EMERGENCY FALLBACK STILL WORKS: Ensure() still calls SpawnEmergencyHero.
//      The fix must stop it being the DEFAULT path, never remove it.
//   7. The phantom stays dead: no .cs anywhere under Assets/ DECLARES a
//      RaidHeroSpawner type, and the exact lie sentence is gone from the ensurer.
//   (+ a data check) Every ENABLED RaidBase_* build scene bakes the
//      HeroStartPoint_PlayerSpawn marker the carried hero is seated at.
//
// Contract mirrors SceneRoutingRegression.Run(out string reason):
//   true  = pass  (reason = one-line summary)
//   false = fail  (reason = the exact invariant that broke)
//
// Orchestrator (DataRegression.RunAll) registers it covenant-style:
//   if (!RaidHeroCarryRegression.Run(out var raidHeroReason)) failures.Add(raidHeroReason); else log.AppendLine("[raid-hero-carry] " + raidHeroReason);
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor
{
    public static class RaidHeroCarryRegression
    {
        private const string RouterRel  = "_Modules/Core/SceneRouter.cs";
        private const string EnsurerRel = "_Modules/Village/Hero/HeroControlEnsurer.cs";

        // The baked marker the carried hero is seated at on raid entry
        // (HeroControlEnsurer.FindSpawnMarkerPosition -> TryRecoverCarriedHero).
        private const string SpawnMarkerName = "HeroStartPoint_PlayerSpawn";

        // The phantom the stale comment promised. Split so this file's own mention of
        // the name can never be mistaken for a declaration by invariant 7.
        private const string PhantomType = "Raid" + "HeroSpawner";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- RAID HERO CARRY (WO-1109: the raid hero is the carried town hero, not the emergency fallback) ---");

            string assetsRoot = Application.dataPath; // ".../Assets"

            string routerSrc  = ReadOrFail(assetsRoot, RouterRel,  failures, out bool routerOk);
            string ensurerSrc = ReadOrFail(assetsRoot, EnsurerRel, failures, out bool ensurerOk);

            if (routerOk)  CheckRouterCarry(routerSrc, failures, log);
            if (ensurerOk) CheckEnsurerRehomeAndFallback(ensurerSrc, failures, log);
            if (ensurerOk) CheckPhantomStaysDead(assetsRoot, ensurerSrc, failures, log);

            CheckRaidScenesBakeTheMarker(failures, log);

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "RAID_HERO_CARRY_OK");
                reason = "RAID HERO CARRY OK — GoRaid arms the DDOL carry behind the build-settings gate, Ensure re-homes the carried hero out of DDOL, " +
                         "the EMERGENCY FlowTrace.Fail + its fallback are both intact, the phantom spawner stays undeclared, and every enabled RaidBase_* bakes " +
                         SpawnMarkerName;
                return true;
            }

            reason = "raid-hero-carry: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "RAID_HERO_CARRY_FAIL: " + reason);
            return false;
        }

        // ── Invariants 1-3: the router arms the carry, gated ────────────────────
        private static void CheckRouterCarry(string src, List<string> failures, StringBuilder log)
        {
            // (1)+(2) GoRaid's OWN body — brace-matched so a mention elsewhere in the
            // 700-line router cannot mask a GoRaid that stopped carrying.
            if (TryExtractMethodBody(src, "public static void GoRaid(string sceneName)", out string goRaidBody))
            {
                bool arms = goRaidBody.IndexOf("CarryHeroAcrossSingleLoad", System.StringComparison.Ordinal) >= 0;
                if (!arms)
                    failures.Add("WO-1109 REGRESSED: SceneRouter.GoRaid no longer calls CarryHeroAcrossSingleLoad — the town hero is not carried across the Single load, so HeroControlEnsurer will fall back to SpawnEmergencyHero and put an 'EMERGENCY pill spawned' FlowTrace.Fail in the break-log on EVERY raid entry again");
                else
                    log.AppendLine("OK: SceneRouter.GoRaid arms the hero carry (CarryHeroAcrossSingleLoad)");

                bool viaHook = goRaidBody.IndexOf("beforeLoad", System.StringComparison.Ordinal) >= 0;
                if (!viaHook)
                    failures.Add("WO-1109: SceneRouter.GoRaid arms the carry INLINE instead of as LoadSceneWithFade's beforeLoad hook. Inline is wrong on two counts: an unregistered scene aborts the load AFTER the hero was already detached from CastleHubRoot and DontDestroyOnLoad'd (an orphan the next Single load drags along), and the fade + backend save leave the player driving a detached DDOL hero around a still-live town for hundreds of ms");
                else
                    log.AppendLine("OK: the GoRaid carry is handed to LoadSceneWithFade as its beforeLoad hook (fires on the last line before the load commits)");
            }
            else
            {
                failures.Add("WO-1109: could not locate SceneRouter's public static void GoRaid(string sceneName) body — the router's shape changed; the raid hero carry is unverifiable");
            }

            // (2b) The hook must fire in the ONE position that makes it safe: after the
            //      build-settings gate, and immediately before the load commits. Ordering is
            //      the invariant, so assert the ORDER of the three landmarks inside
            //      LoadSceneWithFade rather than merely their presence.
            if (TryExtractMethodBody(src, "public static async UniTask LoadSceneWithFade(", out string fadeBody))
            {
                int gateAt = fadeBody.IndexOf("IsSceneRegistered", System.StringComparison.Ordinal);
                int hookAt = fadeBody.IndexOf("beforeLoad()", System.StringComparison.Ordinal);
                // NB: the landmark is the CALL, not the bare name — the WO-769 comment earlier
                // in this same body says "(LoadSceneAsync below never ran)", and matching that
                // mention made the ordering assertion fail against correct code.
                int loadAt = fadeBody.IndexOf("SceneManager.LoadSceneAsync(sceneName)", System.StringComparison.Ordinal);
                if (gateAt < 0 || hookAt < 0 || loadAt < 0)
                    failures.Add($"WO-1109: LoadSceneWithFade lost one of the three carry landmarks (IsSceneRegistered gate={gateAt >= 0}, beforeLoad() invoke={hookAt >= 0}, LoadSceneAsync={loadAt >= 0}) — the pre-load hook the raid hero carry rides on is gone or ungated");
                else if (!(gateAt < hookAt && hookAt < loadAt))
                    failures.Add($"WO-1109: LoadSceneWithFade invokes beforeLoad() OUT OF ORDER (gate@{gateAt}, hook@{hookAt}, load@{loadAt}); it must run AFTER the IsSceneRegistered gate — otherwise an aborted load still detaches + DDOLs the hero — and BEFORE LoadSceneAsync, or the carry misses the load entirely and the raid falls back to the emergency rig");
                else
                    log.AppendLine("OK: LoadSceneWithFade invokes beforeLoad() after the IsSceneRegistered gate and before LoadSceneAsync");

                if (fadeBody.IndexOf("Guard.Try", System.StringComparison.Ordinal) < 0)
                    failures.Add("WO-1109: LoadSceneWithFade no longer Guard-wraps the beforeLoad hook — a throwing hook would propagate out of the .Forget()'d task and STRAND the player on the current scene, the exact WO-769 failure shape the save flush was already hardened against");
                else
                    log.AppendLine("OK: the beforeLoad hook is Guard-wrapped (a throwing hook cannot strand the player)");
            }
            else
            {
                failures.Add("WO-1109: could not locate SceneRouter.LoadSceneWithFade(...) body — the pre-load hook the raid hero carry rides on is unverifiable");
            }

            // (3) The carry helper itself must exist and actually mark the hero DDOL.
            if (TryExtractMethodBody(src, "CarryHeroAcrossSingleLoad(string via", out string carryBody))
            {
                if (carryBody.IndexOf("DontDestroyOnLoad", System.StringComparison.Ordinal) < 0)
                    failures.Add("WO-1109: SceneRouter.CarryHeroAcrossSingleLoad no longer calls DontDestroyOnLoad — the hero is destroyed with the town on the Single load and the raid falls back to the emergency rig");
                else
                    log.AppendLine("OK: CarryHeroAcrossSingleLoad marks the hero DontDestroyOnLoad");

                // The hub nests the hero under CastleHubRoot, which also holds WaveManager +
                // HeartController + the Tree of Life. DDOL-ing the ROOT once dragged the whole
                // hub into the destination scene (owner F8 2026-07-10, "why is there a tree of
                // life in map"). Detaching first is what prevents that recurrence.
                if (carryBody.IndexOf("SetParent(null", System.StringComparison.Ordinal) < 0)
                    failures.Add("WO-1109: CarryHeroAcrossSingleLoad no longer detaches the hero from its parent before DontDestroyOnLoad — DDOL-ing the hub root drags WaveManager + HeartController + the Tree of Life into the raid scene (owner F8 2026-07-10)");
                else
                    log.AppendLine("OK: CarryHeroAcrossSingleLoad detaches the hero before DDOL (hub root is not dragged along)");
            }
            else
            {
                failures.Add("WO-1109: SceneRouter.CarryHeroAcrossSingleLoad(string via, ...) is GONE — the raid hero carry has no implementation");
            }
        }

        // ── Invariants 4-6: the ensurer re-homes, and the alarm survives ─────────
        private static void CheckEnsurerRehomeAndFallback(string src, List<string> failures, StringBuilder log)
        {
            // (4) The DDOL leak guard inside Ensure().
            if (TryExtractMethodBody(src, "private void Ensure()", out string ensureBody))
            {
                bool rehomes = ensureBody.IndexOf("DontDestroyOnLoad", System.StringComparison.Ordinal) >= 0
                               && ensureBody.IndexOf("TryRecoverCarriedHero", System.StringComparison.Ordinal) >= 0;
                if (!rehomes)
                    failures.Add("WO-1109 LEAK: HeroControlEnsurer.Ensure no longer re-homes a hero found parked in the DontDestroyOnLoad scene. FindLoco() returns DDOL objects, so a carried hero is 'found' and TryRecoverCarriedHero never runs — the hero is never seated at " + SpawnMarkerName + " and stays in DDOL for the rest of the session, surviving every later Single load and stacking against each scene's own baked hero");
                else
                    log.AppendLine("OK: Ensure() re-homes a DDOL-parked carried hero via TryRecoverCarriedHero (no DDOL leak across repeated town->raid->town cycles)");

                // (6) The fallback must SURVIVE — it just must not be the default path.
                if (ensureBody.IndexOf("SpawnEmergencyHero()", System.StringComparison.Ordinal) < 0)
                    failures.Add("WO-1109: HeroControlEnsurer.Ensure no longer calls SpawnEmergencyHero() — the emergency fallback was REMOVED rather than demoted. It must still catch the case where the intended path genuinely produces no hero, or a failed carry leaves the player with nothing to control");
                else
                    log.AppendLine("OK: Ensure() still calls SpawnEmergencyHero() — the fallback survives, demoted rather than deleted");
            }
            else
            {
                failures.Add("WO-1109: could not locate HeroControlEnsurer.Ensure() body — the ensurer's shape changed; the carried-hero re-home is unverifiable");
            }

            // (5) The alarm itself. CLAUDE.md sec.12: instrumentation is PERMANENT, and the
            // WO explicitly forbade "fixing" this by downgrading the Fail to a Warn.
            if (TryExtractMethodBody(src, "private void SpawnEmergencyHero()", out string emergencyBody))
            {
                bool hasFail = emergencyBody.IndexOf("FlowTrace.Fail(", System.StringComparison.Ordinal) >= 0;
                bool hasText = emergencyBody.IndexOf("EMERGENCY pill spawned", System.StringComparison.Ordinal) >= 0;
                if (!hasFail || !hasText)
                    failures.Add($"WO-1109 ALARM SILENCED: SpawnEmergencyHero no longer opens with FlowTrace.Fail(\"EMERGENCY pill spawned\") (Fail present={hasFail}, text present={hasText}). WO-1109 fixed the FAULT so this Fail would mean something again; removing or downgrading it silences the alarm instead — CLAUDE.md sec.12 forbids stripping instrumentation");
                else
                    log.AppendLine("OK: SpawnEmergencyHero still raises FlowTrace.Fail('EMERGENCY pill spawned') at full severity — the alarm means something again");
            }
            else
            {
                failures.Add("WO-1109: could not locate HeroControlEnsurer.SpawnEmergencyHero() body — the emergency path's alarm is unverifiable");
            }
        }

        // ── Invariant 7: the phantom spawner stays dead ─────────────────────────
        private static void CheckPhantomStaysDead(string assetsRoot, string ensurerSrc, List<string> failures, StringBuilder log)
        {
            // (a) The lie may only ever appear QUOTED AND RETRACTED. WO-1109 deliberately
            //     KEPT the sentence in HeroControlEnsurer's header — verbatim, as the quoted
            //     example of the failure — because CLAUDE.md's "comments lie" rule is best
            //     taught by the comment that cost us months. So "the string is absent" is the
            //     wrong invariant (it would fail against the correct fix). The right one:
            //     wherever that claim appears, the retraction must appear with it. A future
            //     seat that re-asserts it as fact, or deletes the retraction and leaves the
            //     claim standing, trips this.
            string lie        = PhantomType + " builds the REAL class body";
            string retraction = "THERE IS NO " + PhantomType;
            bool hasLie        = ensurerSrc.IndexOf(lie, System.StringComparison.Ordinal) >= 0;
            bool hasRetraction = ensurerSrc.IndexOf(retraction, System.StringComparison.Ordinal) >= 0;
            if (hasLie && !hasRetraction)
                failures.Add($"WO-1109: HeroControlEnsurer states '{lie}' with NO retraction ('{retraction}') anywhere in the file — that sentence describes a type that has never existed, and a seat believing it is exactly what let every raid ship on the emergency fallback for months");
            else if (!hasRetraction)
                failures.Add($"WO-1109: HeroControlEnsurer's header no longer RETRACTS the phantom {PhantomType} ('{retraction}'). The retraction is the fix's documentation — without it the next seat has nothing warning them off re-adding the claim");
            else
                log.AppendLine($"OK: HeroControlEnsurer quotes the stale {PhantomType} claim ONLY alongside its explicit retraction");

            // (b) No .cs anywhere under Assets/ DECLARES the type. If someone ever builds
            //     it for real (WO-1109 Option B), this fires and the ticket gets re-decided
            //     deliberately instead of two hero paths silently coexisting.
            string[] declTokens =
            {
                "class "     + PhantomType,
                "struct "    + PhantomType,
                "interface " + PhantomType,
            };
            var decls = new List<string>();
            foreach (var cs in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(cs) == "RaidHeroCarryRegression.cs") continue; // self-match guard
                string s;
                try { s = File.ReadAllText(cs); }
                catch { continue; }
                foreach (var tok in declTokens)
                    if (s.IndexOf(tok, System.StringComparison.Ordinal) >= 0)
                        decls.Add($"{Path.GetFileName(cs)} declares '{tok}'");
            }
            if (decls.Count > 0)
                failures.Add($"WO-1109: a {PhantomType} type now EXISTS ({string.Join(", ", decls)}) — WO-1109 shipped Option A (carry the town hero). Two hero-provisioning paths into a raid must not coexist silently; re-decide the ticket and update this oracle");
            else
                log.AppendLine($"OK: no {PhantomType} type is declared anywhere under Assets/ (Option A remains the single raid-hero path)");
        }

        // ── Data check: the seat target is baked into every shipped raid scene ──
        private static void CheckRaidScenesBakeTheMarker(List<string> failures, StringBuilder log)
        {
            int checkedCount = 0;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s == null || !s.enabled || string.IsNullOrEmpty(s.path)) continue;
                string sceneName = Path.GetFileNameWithoutExtension(s.path);
                if (!HubScenes.IsRaid(sceneName)) continue;

                checkedCount++;
                string text;
                try { text = File.ReadAllText(s.path); }
                catch (System.Exception e)
                {
                    failures.Add($"WO-1109: could not read raid scene '{s.path}' ({e.GetType().Name}) — cannot prove it bakes {SpawnMarkerName}");
                    continue;
                }
                if (text.IndexOf(SpawnMarkerName, System.StringComparison.Ordinal) < 0)
                    failures.Add($"WO-1109: enabled raid scene '{sceneName}' does NOT bake a '{SpawnMarkerName}' marker — the carried hero keeps its TOWN world-pose on arrival (off-map / inside geometry), because TryRecoverCarriedHero has no marker to seat it at");
                else
                    log.AppendLine($"OK: raid scene '{sceneName}' bakes '{SpawnMarkerName}' (carried hero has a seat)");
            }

            if (checkedCount == 0)
                failures.Add("WO-1109: NO enabled RaidBase_* scene is in Build Settings — GoRaid cannot load anything, so the raid hero carry is unreachable in a player build");
            else
                log.AppendLine($"checked {checkedCount} enabled raid scene(s) for the '{SpawnMarkerName}' seat marker");
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string ReadOrFail(string assetsRoot, string rel, List<string> failures, out bool ok)
        {
            string path = Path.Combine(assetsRoot, rel);
            if (!File.Exists(path))
            {
                failures.Add($"WO-1109: '{rel}' missing — the raid hero carry cannot be verified");
                ok = false;
                return string.Empty;
            }
            try { ok = true; return File.ReadAllText(path); }
            catch (System.Exception e)
            {
                failures.Add($"WO-1109: could not read '{rel}' ({e.GetType().Name}: {e.Message})");
                ok = false;
                return string.Empty;
            }
        }

        // Extracts the balanced-brace body (including the outer braces) of the first
        // method whose signature contains signatureNeedle. Same shape as
        // SceneRoutingRegression.TryExtractMethodBody — naive brace matching, fine for
        // these C# method bodies. Brace chars come from code points (123='{', 125='}')
        // so this file's own brace balance stays clean under the CLAUDE.md sec.1 gate.
        private static bool TryExtractMethodBody(string source, string signatureNeedle, out string body)
        {
            body = null;
            char openBrace = (char)123;
            char closeBrace = (char)125;
            int sig = source.IndexOf(signatureNeedle, System.StringComparison.Ordinal);
            if (sig < 0) return false;
            int open = source.IndexOf(openBrace, sig);
            if (open < 0) return false;
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == openBrace) depth++;
                else if (c == closeBrace)
                {
                    depth--;
                    if (depth == 0) { body = source.Substring(open, i - open + 1); return true; }
                }
            }
            return false;
        }
    }
}
