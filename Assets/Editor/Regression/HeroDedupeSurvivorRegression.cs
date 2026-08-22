// =============================================================================
// HeroDedupeSurvivorRegression (WO-1131) — the hero Ensure() operates on is the
// SURVIVOR of the dedupe, never an object the dedupe just destroyed.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS (owner F8 seq 3587, entering dg_hollow_roads: "issue with
// portal travel" — the hero arrived ~130m OUTSIDE the dungeon, the camera reported
// room=none size=(0x0), and the HUD fell through to overworld posture).
//
// HeroControlEnsurer.Ensure() asked the world for the hero TWICE, one line apart,
// with a mutation in between:
//
//     Vector3? displacedSeat = DedupeHeroes();   // calls Destroy(root) on the loser
//     var loco = FindLoco();                     // FindObjectsByType(...).FirstOrDefault()
//
// UnityEngine.Object.Destroy is DEFERRED to end of frame. So the second query still
// returned the object the first line had just destroyed — and FirstOrDefault has
// unspecified ordering, so which one came back was luck. The captured trace:
//
//     [Flow:Hero] Duplicate hero removed - destroying 'Hero (Blaise)' (scene=dg_hollow_roads)
//     [Flow:Hero] Ensure: found existing hero 'Hero (Blaise)' (via HeroLocomotion)   <- the bug
//
// Everything downstream then read the DOOMED object. The carried-hero guard tests
// `hero.transform.root.gameObject.scene.name == "DontDestroyOnLoad"`; the doomed
// hero's root scene is the DUNGEON, so the guard evaluated FALSE, and
// TryRecoverCarriedHero — the ONLY code in the project that seats a carried hero —
// never ran. The real (carried) hero therefore kept the TOWN world pose that
// CarryHeroAcrossSingleLoad preserves (SetParent(null, true)), x ~= -143: the hub's
// Hollow Roads portal mouth. The dungeon spans x in [-10, 10].
//
// THE FIX IS NOT A BEHAVIOUR CHANGE TO Destroy. DestroyImmediate would alter
// destruction semantics project-wide to paper over one symptom, and scene-filtering
// the second query would leave the second query — and the race shape — intact. The
// fix is that DedupeHeroes hands back the survivor it ALREADY COMPUTED (its local
// `keep`), and Ensure uses it. One query, one owner, no window.
//
// -----------------------------------------------------------------------------
// WHAT THIS ORACLE MEASURES, AND HOW IT CAN GENUINELY FAIL
//
// It does NOT grep for "Ensure calls TryRecoverCarriedHero" — that assertion passes
// whether or not the guard above it admits control, which is precisely the state the
// codebase was already in when the owner hit this. Instead:
//
//   CASE A (DRIVEN)  empty world  -> DedupeHeroes yields NO survivor and NO seat.
//   CASE B (DRIVEN)  one hero     -> DedupeHeroes yields THAT EXACT hero as the
//                                    survivor. This is the assertion with teeth: the
//                                    pre-fix method early-returned on `heroes.Length
//                                    <= 1` and handed the caller nothing, which is
//                                    what FORCED the second query. Restore that early
//                                    return and this case fails on the next run.
//   CASE C (DRIVEN)  two HeroLocomotion under ONE root -> the multi-hero branch runs
//                                    for real (keep-selection included), the survivor
//                                    is one of the two live components, nothing is
//                                    destroyed, and no seat is donated (a shared root
//                                    displaces nobody). Return null here and it fails.
//   CASE D (REFLECTED) the method's RETURN TYPE must expose a field the survivor can
//                                    be read out of. Narrow it back to a bare Vector3?
//                                    and this fails — no string matching involved, the
//                                    type system is the witness.
//   CASE E (SOURCE)  Ensure() must not call FindLoco() on the dedupe path. The one
//                                    surviving FindLoco() in that method is the
//                                    re-read AFTER SpawnEmergencyHero(), which is a
//                                    different path entirely (it is reached only when
//                                    the dedupe found zero heroes, so nothing was
//                                    destroyed). The rule is therefore positional:
//                                    the FIRST FindLoco() in the body, if any, must
//                                    come AFTER the emergency spawn. Re-introduce the
//                                    re-query at the top and this fails.
//
// DECLARED STAND-DOWN — read this, it is the honest half:
//   The DESTRUCTIVE path (two heroes on DISTINCT roots, which is the shape the owner
//   actually hit) CANNOT be driven from an edit-mode suite. UnityEngine.Object.Destroy
//   throws "Destroy may not be called from edit mode" and would abort DedupeHeroes
//   before it returns, so there is no return value to measure. That case rides as a
//   RegressionOutcome.PartialSkip and is named in the log rather than quietly counted
//   green. Cases B and C still pin the exact contract that closes the window: the
//   survivor is COMPUTED ONCE and HANDED BACK, so no caller has cause to re-ask.
//
// Contract mirrors the sibling suites: Run(out string reason), never throws.
// Registered EXACTLY ONCE in DataRegression.RunAll.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Editor.Regression;

namespace DeNelle.Editor
{
    public static class HeroDedupeSurvivorRegression
    {
        private const string EnsurerRel = "_Modules/Village/Hero/HeroControlEnsurer.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- HERO DEDUPE SURVIVOR (WO-1131: Ensure operates on the survivor, never a destroyed hero) ---");

            try
            {
                MethodInfo dedupe = null;
                FieldInfo survivorField = null;
                FieldInfo seatField = null;

                // ---- CASE D: the shape of the contract, witnessed by the type system ----
                dedupe = typeof(HeroControlEnsurer).GetMethod(
                    "DedupeHeroes", BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);

                if (dedupe == null)
                {
                    failures.Add("[contract] HeroControlEnsurer.DedupeHeroes() (private static, no parameters) is GONE — the single hero query that Ensure depends on cannot be located, so nothing below can be measured");
                }
                else
                {
                    var rt = dedupe.ReturnType;
                    foreach (var f in rt.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (survivorField == null && typeof(HeroLocomotion).IsAssignableFrom(f.FieldType)) survivorField = f;
                        if (seatField == null && f.FieldType == typeof(Vector3?)) seatField = f;
                    }

                    if (survivorField == null)
                        failures.Add($"[contract] DedupeHeroes returns '{rt.Name}', which exposes NO HeroLocomotion field. The survivor is therefore not handed back, so Ensure has to re-query the world one line after Destroy() — and Destroy is deferred to end of frame, so that query returns the hero the dedupe just destroyed. That is the dg_hollow_roads defect verbatim: the doomed object's root scene is the dungeon, the DontDestroyOnLoad carried-hero guard reads FALSE, TryRecoverCarriedHero never runs, and the real hero keeps its town pose ~130m outside the dungeon");
                    else
                        log.AppendLine($"OK: DedupeHeroes returns '{rt.Name}' exposing the survivor as '{survivorField.Name}' ({survivorField.FieldType.Name})");

                    if (seatField == null)
                        failures.Add($"[contract] DedupeHeroes returns '{rt.Name}', which exposes NO Vector3? field. A composed dungeon bakes no HeroStartPoint_PlayerSpawn marker, so the destroyed baked hero's position is the ONLY record of where that scene wanted a hero to stand — without it the carried hero has nowhere to be seated");
                    else
                        log.AppendLine($"OK: DedupeHeroes still donates the displaced seat as '{seatField.Name}'");
                }

                // ---- The driven cases need an uncontaminated world ----
                if (dedupe != null && survivorField != null && seatField != null)
                {
                    var preexisting = UnityEngine.Object.FindObjectsByType<HeroLocomotion>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);

                    if (preexisting != null && preexisting.Length > 0)
                    {
                        notes.Add(RegressionOutcome.PartialSkip("[drive] live DedupeHeroes cases",
                            $"the open editor scene already contains {preexisting.Length} HeroLocomotion object(s); driving the method here would measure that scene, not the fixture"));
                    }
                    else
                    {
                        DriveCases(dedupe, survivorField, seatField, failures, log);
                    }

                    // The destructive path is unreachable from edit mode. Say so out loud.
                    notes.Add(RegressionOutcome.PartialSkip("[drive] two heroes on DISTINCT roots (the destructive path)",
                        "UnityEngine.Object.Destroy throws 'may not be called from edit mode', so DedupeHeroes cannot reach its return statement in an edit-mode suite. The survivor-is-handed-back contract is pinned by the one-hero and shared-root cases instead"));
                }

                // ---- CASE E: no second query on the dedupe path ----
                CheckEnsureDoesNotRequery(failures, log);
            }
            catch (Exception e)
            {
                failures.Add("[harness] HeroDedupeSurvivorRegression threw: " + e.GetType().Name + ": " + e.Message);
            }

            foreach (var n in notes) log.AppendLine("  " + n);

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "HERO_DEDUPE_SURVIVOR_OK");
                reason = "HERO DEDUPE SURVIVOR OK — DedupeHeroes hands back the survivor it already computed (plus the displaced seat), " +
                         "and Ensure() does not re-query the world after the destroy" +
                         (notes.Count > 0 ? " — " + string.Join(" ; ", notes) : "");
                return true;
            }

            reason = "hero-dedupe-survivor: " + string.Join("; ", failures) +
                     (notes.Count > 0 ? " ; " + string.Join(" ; ", notes) : "");
            Debug.LogError(log.ToString() + "HERO_DEDUPE_SURVIVOR_FAIL: " + reason);
            return false;
        }

        // =====================================================================
        //  Cases A / B / C — actually invoke the method against a built fixture
        // =====================================================================
        private static void DriveCases(MethodInfo dedupe, FieldInfo survivorField, FieldInfo seatField,
                                       List<string> failures, StringBuilder log)
        {
            var spawned = new List<GameObject>();
            try
            {
                // --- CASE A: empty world ---
                object rA = dedupe.Invoke(null, null);
                var survivorA = survivorField.GetValue(rA) as HeroLocomotion;
                var seatA = (Vector3?)seatField.GetValue(rA);
                if (survivorA != null)
                    failures.Add($"[case-A/empty] DedupeHeroes reported a survivor ('{survivorA.name}') with no hero in the world — Ensure would then run its whole hero pipeline against a phantom");
                else if (seatA.HasValue)
                    failures.Add("[case-A/empty] DedupeHeroes donated a displaced seat with no hero in the world — nothing was displaced, so the carried hero would be seated at a fabricated coordinate");
                else
                    log.AppendLine("OK: case-A/empty — no hero in the world yields no survivor and no seat");

                // --- CASE B: exactly one hero. THE case with teeth. ---
                var soloGo = new GameObject("WO1131_Fixture_SoloHero");
                spawned.Add(soloGo);
                var solo = soloGo.AddComponent<HeroLocomotion>();

                object rB = dedupe.Invoke(null, null);
                var survivorB = survivorField.GetValue(rB) as HeroLocomotion;
                var seatB = (Vector3?)seatField.GetValue(rB);

                if (survivorB == null)
                    failures.Add("[case-B/solo] DedupeHeroes found ONE hero and handed back NO survivor. That is the pre-fix early return ('heroes.Length <= 1 -> return null'), and it is exactly what forced Ensure to ask the world a SECOND time one line after Destroy(). Deferred destruction then made that second query return the hero the dedupe had just destroyed — the dg_hollow_roads 130m-outside-the-dungeon defect. The survivor must be COMPUTED ONCE and HANDED BACK, so no caller has cause to re-ask");
                else if (survivorB != solo)
                    failures.Add($"[case-B/solo] DedupeHeroes handed back '{survivorB.name}' when the only hero in the world was '{solo.name}' — the survivor is not the object it resolved");
                else if (seatB.HasValue)
                    failures.Add("[case-B/solo] DedupeHeroes donated a displaced seat when nothing was displaced (a single hero destroys nobody) — a fabricated seat would teleport a carried hero to a position no scene asked for");
                else
                    log.AppendLine("OK: case-B/solo — one hero yields THAT hero as the survivor, no seat, nothing destroyed");

                if (solo == null)
                    failures.Add("[case-B/solo] the only hero in the world was DESTROYED by the dedupe — the survivor selection kept nobody");

                // --- CASE C: two HeroLocomotion sharing ONE root ---
                // Deliberately shared-root: it exercises the multi-hero branch (and its
                // keep-selection) for real, while the destroy loop's `root == keepRoot`
                // skip means Destroy is never reached — which is the only way this branch
                // can be observed at all outside play mode.
                var secondGo = new GameObject("WO1131_Fixture_SecondLoco");
                spawned.Add(secondGo);
                secondGo.transform.SetParent(soloGo.transform, false);
                var second = secondGo.AddComponent<HeroLocomotion>();

                object rC = dedupe.Invoke(null, null);
                var survivorC = survivorField.GetValue(rC) as HeroLocomotion;
                var seatC = (Vector3?)seatField.GetValue(rC);

                if (survivorC == null)
                    failures.Add("[case-C/shared-root] the multi-hero branch handed back NO survivor. Ensure has nothing to operate on and must re-query — reopening the deferred-destroy window this suite exists to close");
                else if (survivorC != solo && survivorC != second)
                    failures.Add($"[case-C/shared-root] the survivor '{survivorC.name}' is not one of the two heroes in the world");
                else if (seatC.HasValue)
                    failures.Add("[case-C/shared-root] a seat was donated although both heroes share ONE root, so nothing was displaced and nothing was destroyed");
                else
                    log.AppendLine($"OK: case-C/shared-root — the multi-hero branch resolves a live survivor ('{survivorC.name}') and donates no seat");

                if (solo == null || second == null)
                    failures.Add("[case-C/shared-root] the dedupe destroyed a hero that shares the kept root — the destroy loop no longer skips keepRoot, so it can now destroy the object it is keeping");
            }
            finally
            {
                for (int i = spawned.Count - 1; i >= 0; i--)
                    if (spawned[i] != null) UnityEngine.Object.DestroyImmediate(spawned[i]);
            }
        }

        // =====================================================================
        //  Case E — Ensure() must not re-query on the dedupe path
        // =====================================================================
        private static void CheckEnsureDoesNotRequery(List<string> failures, StringBuilder log)
        {
            string path = Path.Combine(Application.dataPath, EnsurerRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add("[shape] cannot read " + EnsurerRel + " — the no-second-query rule is unverifiable");
                return;
            }

            string src;
            try { src = File.ReadAllText(path); }
            catch (Exception e) { failures.Add("[shape] could not read " + EnsurerRel + ": " + e.Message); return; }

            if (!TryExtractMethodBody(src, "private void Ensure()", out string body))
            {
                failures.Add("[shape] could not locate HeroControlEnsurer.Ensure() — the call site the whole defect traced to is unverifiable");
                return;
            }

            // Ensure()'s PROSE legitimately names FindLoco() several times (it is explaining
            // the very race this rule enforces). Measure the CODE, not the commentary — a
            // rule that a comment can trip is a rule nobody will keep.
            body = StripComments(body);

            int firstFind = body.IndexOf("FindLoco()", StringComparison.Ordinal);
            int emergency = body.IndexOf("SpawnEmergencyHero()", StringComparison.Ordinal);

            if (firstFind < 0)
            {
                log.AppendLine("OK: Ensure() contains no FindLoco() call at all");
                return;
            }

            // The one legitimate FindLoco() is the re-read AFTER the emergency spawn. That
            // path is reached only when the dedupe found ZERO heroes, so nothing was
            // destroyed and there is no deferred-destruction window to fall into.
            if (emergency < 0 || firstFind < emergency)
            {
                failures.Add("[shape] Ensure() calls FindLoco() on the DEDUPE path (before SpawnEmergencyHero). That is the second world query, one line after DedupeHeroes calls Destroy() — and Destroy is deferred to end of frame, so it returns the hero that was just destroyed. Every test below it then reads the doomed object: its root scene is the destination scene rather than 'DontDestroyOnLoad', the carried-hero guard goes FALSE, TryRecoverCarriedHero never runs, and the real carried hero keeps its town world pose. Use the survivor DedupeHeroes already handed back");
            }
            else
            {
                log.AppendLine("OK: Ensure()'s only FindLoco() is the post-SpawnEmergencyHero re-read (a path on which nothing was destroyed)");
            }
        }

        /// <summary>
        /// Blanks // and /* */ comments (preserving length, so every index stays meaningful)
        /// and also blanks string/char literals, so a positional rule measures executable code.
        /// </summary>
        private static string StripComments(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            var buf = s.ToCharArray();
            int mode = 0; // 0=code 1=line-comment 2=block-comment 3=string 4=char
            for (int i = 0; i < buf.Length; i++)
            {
                char c = buf[i];
                char n = (i + 1 < buf.Length) ? buf[i + 1] : '\0';
                switch (mode)
                {
                    case 0:
                        if (c == '/' && n == '/') { mode = 1; buf[i] = ' '; buf[i + 1] = ' '; i++; }
                        else if (c == '/' && n == '*') { mode = 2; buf[i] = ' '; buf[i + 1] = ' '; i++; }
                        else if (c == '"') { mode = 3; buf[i] = ' '; }
                        else if (c == '\'') { mode = 4; buf[i] = ' '; }
                        break;
                    case 1:
                        if (c == '\n') mode = 0; else buf[i] = ' ';
                        break;
                    case 2:
                        if (c == '*' && n == '/') { mode = 0; buf[i] = ' '; buf[i + 1] = ' '; i++; }
                        else if (c != '\n') buf[i] = ' ';
                        break;
                    case 3:
                        if (c == '\\' && i + 1 < buf.Length) { buf[i] = ' '; buf[i + 1] = ' '; i++; }
                        else if (c == '"') { mode = 0; buf[i] = ' '; }
                        else if (c != '\n') buf[i] = ' ';
                        break;
                    case 4:
                        if (c == '\\' && i + 1 < buf.Length) { buf[i] = ' '; buf[i + 1] = ' '; i++; }
                        else if (c == '\'') { mode = 0; buf[i] = ' '; }
                        else if (c != '\n') buf[i] = ' ';
                        break;
                }
            }
            return new string(buf);
        }

        // =====================================================================
        //  Brace-matched method-body extractor (same idiom as the sibling suites)
        // =====================================================================
        private static bool TryExtractMethodBody(string source, string signatureNeedle, out string body)
        {
            body = null;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(signatureNeedle)) return false;

            int at = source.IndexOf(signatureNeedle, StringComparison.Ordinal);
            if (at < 0) return false;

            int open = source.IndexOf('{', at);
            if (open < 0) return false;

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) { body = source.Substring(open, i - open + 1); return true; }
                }
            }
            return false;
        }
    }
}
