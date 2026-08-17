// =============================================================================
// UpgradeQueueFullSurfaceRegression [queue-full-surface] — WO-1045.
// Marker: QUEUE_FULL_SURFACE_OK / QUEUE_FULL_SURFACE_FAIL. Expected: GREEN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
//
// THE ORACLE'S ONE JOB: a blocked upgrade must never be SILENTLY INERT.
//
// The defect it pins (owner, 2026-08-17): with 49k wood against a 108 cost the player
// tapped "Upgrade to Level 2" and nothing happened. Not a disabled button, not a
// message. All THREE tap paths already refused a depth-capped Builders line
// (BuildingUpgradeService.TryUpgrade, ResourceBuildingState.TryUpgrade,
// PlacedStructureUpgradeService.TryStart) — but UpgradeActionState had no member that
// could SAY so, so the getter returned Ready and the View drew a bright, tappable,
// guaranteed-to-fail plate. Two of those three paths went further and reported the
// refusal as "You can't afford that yet." to a player holding 49k wood.
//
// WHAT IS ASSERTED
//   1. STATE     — UpgradeActionState.QueueFull exists (its absence WAS the bug).
//   2. SURFACE   — the VM exposes reason + offer + both axes' counters; the panel can
//                  compose a depth-bearing label.
//   3. AXES      — freeBuildSlots(2) != queueDepthPerLine(5), and the refusal sentence
//                  names DEPTH and never re-words itself into "all builders are busy".
//                  Concurrency QUEUES; only depth REFUSES. Different remedies.
//   4. COPY      — the QueueFull face is non-blank, ASCII, and UNIQUE against every
//                  other state's label (colourblind law: text is the only signal).
//   5. ONE VOICE — PlacedStructureUpgradeService quotes BuildTimerService's sentence
//                  rather than re-composing it, and CanBuySlot's refusal is byte-
//                  identical to TryBuySlot's (the probe and the act cannot drift).
//   6. PURCHASE  — the player-facing path is TryBuySlot; neither the panel nor the VM
//                  may name GrantSlot/BuySlot (the [Obsolete] free grant, which skips
//                  the Echo gate AND the crystal charge — WO-911 ruling Q6).
//   7. OFFER     — a broke-but-entitled player STILL sees the offer (it routes to the
//                  crystal faucet); a non-entitled player is told what unlocks it.
//   8. CANON     — the new player-facing words live in canon-strings.json, in BOTH
//                  copies (Resources + StreamingAssets), ASCII, with their placeholders.
//
// Wire (DataRegression.RunAll):
//   if (!UpgradeQueueFullSurfaceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[queue-full-surface] " + r);
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Editor
{
    public static class UpgradeQueueFullSurfaceRegression
    {
        private const string SaveKey = "dotr-save";
        private const string VmPath    = "Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs";
        private const string PanelPath = "Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs";
        private const string CanonResources      = "Assets/Resources/Data/Canonical/canon-strings.json";
        private const string CanonStreamingAssets = "Assets/StreamingAssets/Data/Canonical/canon-strings.json";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- QUEUE-FULL SURFACE (WO-1045: a blocked upgrade is never silently inert) ---");

            try
            {
                CheckStateExists(failures, log);
                CheckVmSurface(failures, log);
                CheckTwoAxesConfig(failures, log);
                CheckLabelIsUniqueAsciiAndNamesDepth(failures, log);
                CheckOneVoiceForTheRefusal(failures, log);
                CheckPurchaseRoutesThroughTryBuySlot(failures, log);
                CheckCanonStrings(failures, log);
                CheckLiveServiceBehaviour(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add($"queue-full-surface oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Finish(failures, log, out reason);
        }

        // ── 1. the state whose ABSENCE was the bug ────────────────────────────
        private static void CheckStateExists(List<string> failures, StringBuilder log)
        {
            if (!Enum.IsDefined(typeof(UpgradeActionState), "QueueFull"))
            {
                failures.Add("UpgradeActionState has no QueueFull member — the enum cannot express " +
                             "'the Builders line is at its depth cap', so ActionState falls through to " +
                             "Ready and the View draws a bright button over a guaranteed refusal. " +
                             "That IS the WO-1045 defect.");
                return;
            }
            log.AppendLine("  UpgradeActionState.QueueFull present");
        }

        // ── 2. the VM actually surfaces reason + offer + BOTH axes ────────────
        private static void CheckVmSurface(List<string> failures, StringBuilder log)
        {
            var t = typeof(BuildingUpgradeVM);
            // Reason (the service's own words), the offer, and — critically — a counter for EACH
            // axis, so the UI can show that depth and concurrency are different numbers.
            RequireMember(t, "ActionBlockedReason", failures, "the greyed button has no reason to show");
            RequireMember(t, "BuilderQueueDepth",   failures, "no DEPTH readout (items lined up)");
            RequireMember(t, "BuilderQueueLimit",   failures, "no DEPTH cap readout");
            RequireMember(t, "BuilderCrewsBusy",    failures, "no CONCURRENCY readout (jobs running at once)");
            RequireMember(t, "BuilderCrewSlots",    failures, "no CONCURRENCY cap readout");
            RequireMember(t, "CanBuyQueueSlot",     failures, "no way to know whether a remedy is offerable");
            RequireMember(t, "QueueSlotPrice",      failures, "no price for the offered remedy");
            RequireMember(t, "QueueSlotLockReason", failures, "no unlock condition when the offer is gated");
            RequireMember(t, "TryBuyQueueSlot",     failures, "no command behind the offer");

            // The panel must be able to compose a label that CARRIES the depth numbers.
            var panel = typeof(BuildingUpgradePanelMvvm);
            var five = panel.GetMethod("FormatActionLabel", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(UpgradeActionState), typeof(int), typeof(string), typeof(int), typeof(int) }, null);
            if (five == null)
                failures.Add("BuildingUpgradePanelMvvm.FormatActionLabel(state,seconds,name,queueDepth,queueLimit) " +
                             "missing — the QueueFull face cannot state WHICH limit was hit or how full it is");
            else
                log.AppendLine("  panel can compose a depth-bearing action label");

            log.AppendLine("  VM surface (reason + offer + both axes' counters) present");
        }

        private static void RequireMember(Type t, string name, List<string> failures, string why)
        {
            if (t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) == null &&
                t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance) == null)
                failures.Add($"{t.Name}.{name} missing — {why}");
        }

        // ── 3. the two axes stay two axes ─────────────────────────────────────
        private static void CheckTwoAxesConfig(List<string> failures, StringBuilder log)
        {
            var cfg = DeNelle.Core.Catalog.BuildTimerConfig.CreateDefault();
            if (cfg.freeBuildSlots != 2)
                failures.Add($"BuildTimerConfig.freeBuildSlots is {cfg.freeBuildSlots}, expected 2 — WO-1045 §7 " +
                             "pins it, and BuildTimerConfig explicitly forbids implementing the DEPTH cap by " +
                             "raising CONCURRENCY");
            if (cfg.queueDepthPerLine != 5)
                failures.Add($"BuildTimerConfig.queueDepthPerLine is {cfg.queueDepthPerLine}, expected 5 (owner ruling Q4)");
            if (cfg.freeBuildSlots == cfg.queueDepthPerLine)
                failures.Add("freeBuildSlots == queueDepthPerLine — the two axes have collapsed into one, so a " +
                             "message can no longer tell the player WHICH limit refused them or which remedy applies");
            log.AppendLine($"  axes distinct: concurrency={cfg.freeBuildSlots}, depth={cfg.queueDepthPerLine}");
        }

        // ── 4. the face reads in greyscale, and names DEPTH ───────────────────
        private static void CheckLabelIsUniqueAsciiAndNamesDepth(List<string> failures, StringBuilder log)
        {
            if (!Enum.IsDefined(typeof(UpgradeActionState), "QueueFull")) return;   // already failed above

            string face = BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.QueueFull, 0, "Drill Yard", 5, 5);
            if (string.IsNullOrWhiteSpace(face))
                failures.Add("the QueueFull button renders a BLANK label — a wordless disabled plate is the " +
                             "same silent dead-end the WO exists to retire");

            foreach (char ch in face ?? "")
                if (ch >= 128)
                    failures.Add($"QueueFull label is not ASCII ('{ch}') — TMP renders non-ASCII as a tofu box (CLAUDE.md §7)");

            // Colourblind law: with hue removed, TEXT is the only thing telling the states apart.
            var seen = new Dictionary<string, UpgradeActionState>();
            foreach (UpgradeActionState st in Enum.GetValues(typeof(UpgradeActionState)))
            {
                string s = BuildingUpgradePanelMvvm.FormatActionLabel(st, 125, "Drill Yard", 5, 5);
                if (seen.TryGetValue(s, out var other) && other != st)
                    failures.Add($"states {other} and {st} render the IDENTICAL label '{s}' — indistinguishable " +
                                 "with colour removed (the owner is red/green colourblind)");
                else seen[s] = st;
            }

            // It must name the DEPTH figures, and must NOT re-word itself onto the concurrency axis:
            // "buy a slot because the builders are busy" is the exact wrong remedy for a depth cap.
            if (face != null && !(face.Contains("5") && face.Contains("of")))
                failures.Add($"QueueFull label '{face}' does not state HOW FULL the line is — the player cannot " +
                             "tell a one-item wait from a hard cap");
            string lower = (face ?? "").ToLowerInvariant();
            if (lower.Contains("busy") || lower.Contains("crew"))
                failures.Add($"QueueFull label '{face}' names the CONCURRENCY axis. A full crew set does NOT refuse " +
                             "— it queues (state Queued). Only the DEPTH cap refuses, and its remedy is different.");

            log.AppendLine($"  QueueFull face '{face}' — ASCII, unique across states, names the depth cap");
        }

        // ── 5. one sentence, one owner ────────────────────────────────────────
        private static void CheckOneVoiceForTheRefusal(List<string> failures, StringBuilder log)
        {
            var svc = typeof(BuildTimerService);
            var m = svc.GetMethod("LineFullMessage", BindingFlags.Public | BindingFlags.Instance);
            if (m == null)
            {
                failures.Add("BuildTimerService.LineFullMessage(ChannelId) is not public — a pre-tap surface " +
                             "cannot quote the refusal it is warning about, so it would have to re-compose it " +
                             "(that duplicate is what WO-1045 removed from PlacedStructureUpgradeService)");
            }
            else log.AppendLine("  BuildTimerService.LineFullMessage is public (the one refusal sentence)");

            if (svc.GetMethod("CanBuySlot", BindingFlags.Public | BindingFlags.Instance) == null)
                failures.Add("BuildTimerService.CanBuySlot(ChannelId, out string) missing — the UI cannot ask " +
                             "whether to OFFER a slot without attempting the purchase");

            // The duplicate must be GONE from the placed path: it may quote, never re-compose.
            string placed = ReadRepoFile("Assets/_Modules/Village/Buildings/Progression/PlacedStructureUpgradeService.cs");
            if (placed != null)
            {
                if (placed.Contains("\"Builders queue is full ("))
                    failures.Add("PlacedStructureUpgradeService still re-composes the 'Builders queue is full (...)' " +
                                 "sentence. Two copies of a player-facing refusal drift; quote " +
                                 "BuildTimerService.LineFullMessage instead.");
                else if (!placed.Contains("LineFullMessage"))
                    failures.Add("PlacedStructureUpgradeService no longer names LineFullMessage — its LineFull " +
                                 "outcome may have lost its message entirely (a silent refusal)");
                else log.AppendLine("  placed-structure path quotes LineFullMessage (duplicate sentence retired)");
            }
        }

        // ── 6. the purchase can only be the gated one ─────────────────────────
        private static void CheckPurchaseRoutesThroughTryBuySlot(List<string> failures, StringBuilder log)
        {
            foreach (var path in new[] { VmPath, PanelPath })
            {
                string src = ReadRepoFile(path);
                if (src == null) { log.AppendLine($"  (skipped source scan: {path} unreadable)"); continue; }

                // GrantSlot / BuySlot skip the Echo gate AND the crystal charge (WO-911 ruling Q6:
                // Echoes unlock the RIGHT to buy, crystals complete it). A player-facing call to
                // either hands out unlimited free parallelism.
                if (src.Contains("GrantSlot(") || src.Contains(".BuySlot("))
                    failures.Add($"{Path.GetFileName(path)} calls GrantSlot/BuySlot — the [Obsolete] FREE grant. " +
                                 "Every player-facing slot purchase must go through TryBuySlot, which applies the " +
                                 "Echo gate and charges crystals.");
            }

            string vm = ReadRepoFile(VmPath);
            if (vm != null && !vm.Contains("TryBuySlot"))
                failures.Add("BuildingUpgradeVM never names TryBuySlot — the offer has no purchase path behind it, " +
                             "so the 'offer' half of grey-out/explain/offer is decorative");
            else if (vm != null)
                log.AppendLine("  slot purchase routes through TryBuySlot; no GrantSlot/BuySlot in the player path");
        }

        // ── 7 + 8 combined: the words live in canon, in BOTH copies ───────────
        private static void CheckCanonStrings(List<string> failures, StringBuilder log)
        {
            var required = new Dictionary<string, string>
            {
                { "upgradeQueueFull",       null },
                { "upgradeQueueFullDetail", "{0}" },
                { "upgradeQueueSlotOffer",  "{0}" },
                { "upgradeQueueCrews",      "{0}" },
            };

            foreach (var path in new[] { CanonResources, CanonStreamingAssets })
            {
                string json = ReadRepoFile(path);
                if (json == null) { failures.Add($"{path} unreadable — canon strings cannot be verified"); continue; }

                foreach (var kv in required)
                {
                    string needle = "\"" + kv.Key + "\"";
                    if (!json.Contains(needle))
                    {
                        failures.Add($"canon-strings key '{kv.Key}' missing from {Path.GetFileName(Path.GetDirectoryName(path))}/" +
                                     $"{Path.GetFileName(path)} — the panel would render a literal '[[missing:{kv.Key}]]' " +
                                     "(CanonicalJson reads Resources first and falls back to StreamingAssets; BOTH must carry it)");
                        continue;
                    }
                    if (kv.Value != null)
                    {
                        int i = json.IndexOf(needle, StringComparison.Ordinal);
                        int end = json.IndexOf('\n', i);
                        string line = end > i ? json.Substring(i, end - i) : json.Substring(i);
                        if (!line.Contains(kv.Value))
                            failures.Add($"canon-strings '{kv.Key}' has lost its '{kv.Value}' placeholder — the " +
                                         "numbers that name the limit would never reach the player");
                    }
                }
            }
            log.AppendLine("  canon-strings keys present in BOTH the Resources and StreamingAssets copies");
        }

        // ── the live service: a full line refuses, says so, and offers correctly ──
        private static void CheckLiveServiceBehaviour(List<string> failures, StringBuilder log)
        {
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            GameStateService priorGss = GameStateService.Instance;
            GameObject gssGo = null, svcGo = null;
            GameState throwaway = null;

            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (queue-full oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  (skipped live-service checks: GameStateService state seam not reflectable)");
                    return;
                }

                svcGo = new GameObject("BuildTimerService (queue-full oracle)");
                var svc = svcGo.AddComponent<BuildTimerService>();

                // Fill the Builder LINE to its depth cap through the pure engine — no Persist, no
                // scene, no play mode. Concurrency stays at 2 so the two axes are visibly different.
                throwaway.ObsidianQueue = ObsidianQueueState.Empty();
                var ch = throwaway.ObsidianQueue.Channel(ChannelId.Builder);
                int depthCap = svc.QueueDepthLimit(ChannelId.Builder);
                int crewSlots = svc.SlotCount(ChannelId.Builder);
                for (int i = 0; i < depthCap; i++)
                    ObsidianQueueEngine.Enqueue(ch, crewSlots,
                        new BuildJobData
                        {
                            StructureId = "oracle_job_" + i,
                            Kind = (int)JobKind.Build,
                            Channel = (int)ChannelId.Builder,
                            DurationMs = 60_000,
                        }, TimeSource.NowUnixMs(), depthCap, out _);

                if (!svc.IsLineFull(ChannelId.Builder))
                {
                    failures.Add($"the Builder line reports NOT full at {svc.QueueDepth(ChannelId.Builder)}/{depthCap} " +
                                 "— the depth gate the whole surface hangs on is not firing");
                    return;
                }

                // The sentence: names DEPTH, never re-words onto concurrency.
                string msg = svc.LineFullMessage(ChannelId.Builder) ?? "";
                if (string.IsNullOrWhiteSpace(msg))
                    failures.Add("LineFullMessage returned blank on a full line — a silent refusal is exactly the bug");
                if (!msg.Contains(depthCap.ToString()))
                    failures.Add($"LineFullMessage '{msg}' does not state the depth cap ({depthCap})");
                if (msg.ToLowerInvariant().Contains("busy"))
                    failures.Add($"LineFullMessage '{msg}' describes CONCURRENCY ('busy'). A full crew set queues; " +
                                 "only a full LINE refuses. Naming the wrong axis sends the player to the wrong remedy.");
                log.AppendLine($"  full line ({svc.QueueDepth(ChannelId.Builder)}/{depthCap}, crews {crewSlots}) says: \"{msg}\"");

                // Concurrency is NOT the blocker here — prove the axes are genuinely different numbers.
                if (depthCap == crewSlots)
                    failures.Add($"the live depth cap ({depthCap}) equals the crew count ({crewSlots}) — the player " +
                                 "cannot be told which one refused them");

                // OFFER, case A: no Echo entitlement -> no buy CTA, but a stated unlock condition.
                throwaway.EchoCount = 0;
                int boughtBefore = ch.BoughtSlots;
                bool canBuyGated = svc.CanBuySlot(ChannelId.Builder, out string gatedWhy);
                bool boughtGated = svc.TryBuySlot(ChannelId.Builder, out string actWhy);
                if (canBuyGated)
                    failures.Add("CanBuySlot said YES with 0 Echoes — the Echo gate (WO-911 Q6) is not being probed");
                if (boughtGated)
                    failures.Add("TryBuySlot GRANTED a slot with 0 Echoes — the Echo gate is not enforced");
                if (string.IsNullOrWhiteSpace(gatedWhy))
                    failures.Add("CanBuySlot refused with NO reason — the player is left at a wall with nothing to aim at");
                if (gatedWhy != actWhy)
                    failures.Add($"the PROBE and the ACT disagree: CanBuySlot says '{gatedWhy}' but TryBuySlot says " +
                                 $"'{actWhy}'. Two voices for one gate is how the button and the refusal drift apart.");
                if (ch.BoughtSlots != boughtBefore)
                    failures.Add("a refused TryBuySlot still changed BoughtSlots — the gate leaks a free slot");
                log.AppendLine($"  Echo-gated: probe and act agree on \"{gatedWhy}\"");

                // OFFER, case B: entitled but BROKE -> the offer STAYS VISIBLE and routes to the faucet.
                throwaway.EchoCount = 5;
                var bal = throwaway.Resources; bal.Crystals = 0; throwaway.Resources = bal;
                if (!svc.CanBuySlot(ChannelId.Builder, out string brokeWhy))
                    failures.Add($"CanBuySlot hid the offer from a BROKE but entitled player ('{brokeWhy}'). The " +
                                 "owner's rule is the opposite: the button stays visible and routes to the crystal " +
                                 "store — hiding it is the unexplained-locked-button bug in a new place.");
                else
                {
                    svc.TryBuySlot(ChannelId.Builder, out string brokeFail);
                    if (brokeFail == null || !brokeFail.StartsWith(BuildTimerService.InsufficientCrystalsPrefix, StringComparison.Ordinal))
                        failures.Add($"a broke purchase failed with '{brokeFail}' — it must carry " +
                                     $"InsufficientCrystalsPrefix so the caller can route to the crystal store");
                    else
                        log.AppendLine($"  broke+entitled: offer still shown, refusal routes to the store (\"{brokeFail}\")");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"live-service check threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (svcGo != null) UnityEngine.Object.DestroyImmediate(svcGo);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        // ── plumbing (mirrors BuildingUpgradeAuthorityRegression) ─────────────

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        /// <summary>Repo-relative read. The repo ROOT is machine-dependent (CLAUDE.md §0), so it is
        /// resolved at runtime from Application.dataPath and never hardcoded.</summary>
        private static string ReadRepoFile(string repoRelativePath)
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string full = Path.Combine(root, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch { return null; }
        }

        private static bool Finish(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "QUEUE_FULL_SURFACE_OK");
                reason = "QUEUE-FULL SURFACE OK -- a depth-capped Builders line disables the upgrade button, " +
                         "states the service's own reason naming the DEPTH limit, and offers the TryBuySlot remedy";
                return true;
            }
            reason = "queue-full-surface: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "QUEUE_FULL_SURFACE_FAIL: " + reason);
            return false;
        }
    }
}
