// =============================================================================
// DeckReturnDoorRegression [deck-return-door] (WO-1400) - a panel opened FROM a deck
// returns to that deck when it closes; a panel opened from the HUD does not.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression
// Markers:  DECK_RETURN_DOOR_OK / DECK_RETURN_DOOR_FAIL
//
// WHAT WAS FOUND (docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:62, dead end 9): PlayerDeckWorkspace
// .OpenCard closed the deck BEFORE opening the card's panel - the arbiter is exclusive
// (PanelManager.NotifyOpened closes `previous`) - and nothing remembered the parent, so
// Hero -> Bag -> close landed on the HUD. Four cards, four re-opens of the deck.
//
// THE FIX SHAPE THIS SUITE PINS: ONE arbiter-level RETURN DOOR (PanelManager.SetReturnDoor),
// set by the deck before it closes itself, ARMED by a close-to-nothing (NotifyClosed /
// CloseOpen with no successor), FIRED on the first pump strictly after the WO-1393 close
// grace, KEPT across a swap, CLEARED by CloseAll and by a back request with nothing open.
//
//   A  card-opened close returns: deck -> door -> deck closes -> card opens (door KEPT) ->
//      card closes -> pump ON the grace frame does nothing -> pump after it re-opens the
//      deck; the door is consumed; the trace carries "return door FIRED 'Hero deck'".
//   B  a swap keeps the door: card -> sibling -> close still returns to the deck.
//   C  HUD-opened close does NOT return: no door set, close, pump -> nothing opens.
//   D  CloseAll clears without firing (the combat posture flip).
//   E  PauseGate.RequestBack with nothing open clears without firing (reason=pause).
//   F  PauseGate.RequestBack on an open child (the Android back path -> CloseOpen) returns.
//   G  SOURCE: PlayerDeckWorkspace.OpenCard sets the door BEFORE Close() and its reopen
//      traces "deck return -> "; PanelManager.CloseAll clears "closeall"; PauseGate clears
//      "pause"; NotifyClosed and CloseOpen both arm.
//
// The arbiter and PauseGate are pure statics (no scene, no MonoBehaviour), so this runs in
// EditMode with real PanelHandles whose Open/Close mirror ObsidianNavigationWorkspace
// (Open -> NotifyOpened, Close -> NotifyClosed). Frames are driven through the public
// PumpReturnDoor(frame) seam - the play-mode pump calls the same method with Time.frameCount.
//
// RED-FIRST: on the pre-fix tree no return door exists, so A/B/F cannot re-open anything
// and G finds no SetReturnDoor in OpenCard. ONE-LINE MUTATION that reds it on the fixed
// tree: delete `ArmReturnDoor(handle.Name);` from PanelManager.NotifyClosed - A, B and G
// fail (the door is never armed, the deck never re-opens). Others: change
// `frame <= _returnDoorPendingUntilFrame` to `<` (A: fires inside the grace); delete
// `ClearReturnDoor("closeall")` (D); delete `PanelManager.ClearReturnDoor("pause")` (E).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class DeckReturnDoorRegression
    {
        private const string Tag = "[deck-return-door]";
        private const string DeckSrc = "Assets/_Modules/HUD/PlayerDeckWorkspace.cs";
        private const string ArbiterSrc = "Assets/_Modules/Core/UI/PanelManager.cs";
        private const string PauseSrc = "Assets/_Modules/Core/UI/PauseGate.cs";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DECK_RETURN_DOOR_OK - " + reason);
            else Debug.LogError("DECK_RETURN_DOOR_FAIL: " + reason);
        }

        /// <summary>A registered panel shaped like ObsidianNavigationWorkspace: Open records
        /// itself with the arbiter, Close notifies it. Opens counts every accepted open.</summary>
        private sealed class FakePanel
        {
            public readonly string Name;
            public bool IsOpen;
            public int Opens;
            public readonly PanelHandle Handle;

            public FakePanel(string name)
            {
                Name = name;
                Handle = PanelManager.Register(name, Close, () => IsOpen);
            }

            public bool Open()
            {
                IsOpen = true;
                if (!PanelManager.NotifyOpened(Handle)) { IsOpen = false; return false; }
                Opens++;
                return true;
            }

            public void Close()
            {
                if (!IsOpen) return;
                IsOpen = false;
                PanelManager.NotifyClosed(Handle);
            }
        }

        /// <summary>Captures every FlowTrace line so the suite can assert the Navigation trace
        /// the WO names, and forwards to the previous sink so the run log keeps them.</summary>
        private sealed class CapturingSink : ITraceSink
        {
            public readonly List<string> Lines = new List<string>();
            private readonly ITraceSink _inner;
            public CapturingSink(ITraceSink inner) { _inner = inner; }
            public void Info(string line)  { Lines.Add(line); _inner?.Info(line); }
            public void Warn(string line)  { Lines.Add(line); _inner?.Warn(line); }
            public void Error(string line) { Lines.Add(line); _inner?.Error(line); }
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DECK RETURN DOOR (WO-1400): card-opened panels return to their deck ---");

            bool enabledBefore = FlowTrace.Enabled;
            var sinkBefore = FlowTrace.Sink;
            var sink = new CapturingSink(sinkBefore);
            FlowTrace.Enabled = true;
            FlowTrace.AllOn();
            FlowTrace.Sink = sink;
            try
            {
                CaseA_CardCloseReturnsToDeck(failures, log, sink);
                CaseB_SwapKeepsDoor(failures, log, sink);
                CaseC_HudOpenedCloseDoesNotReturn(failures, log, sink);
                CaseD_CloseAllClears(failures, log, sink);
                CaseE_PauseWithNothingOpenClears(failures, log, sink);
                CaseF_BackOnChildReturns(failures, log, sink);
                CaseG_SourceShape(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add(Tag + " suite threw: " + ex.GetType().Name + " " + ex.Message);
            }
            finally
            {
                // Leave the arbiter as we found it: nothing open, no door.
                Guard.Try("Regression", "deck-return-door teardown", PanelManager.CloseAll);
                FlowTrace.Sink = sinkBefore;
                FlowTrace.Enabled = enabledBefore;
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "DECK_RETURN_DOOR_OK");
                reason = "DECK RETURN DOOR OK - card-opened panels return to their deck after the close grace, " +
                         "swaps keep the door, HUD-opened panels do not return, CloseAll and pause clear it";
                return true;
            }
            reason = "deck-return-door: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "DECK_RETURN_DOOR_FAIL: " + reason);
            return false;
        }

        // Fresh arbiter: nothing open, no door, and a marker into the trace so each case
        // asserts only the lines it produced.
        private static int Reset(CapturingSink sink)
        {
            PanelManager.CloseAll();
            PanelManager.ClearReturnDoor("regression-reset");
            return sink.Lines.Count;
        }

        private static bool TraceSince(CapturingSink sink, int from, string needle)
        {
            for (int i = from; i < sink.Lines.Count; i++)
                if (sink.Lines[i].Contains("[Flow:Navigation]") && sink.Lines[i].Contains(needle)) return true;
            return false;
        }

        // The deck's hand-off, byte-for-byte the shape PlayerDeckWorkspace.OpenCard uses:
        // set the door, close self, open the card.
        private static void HandOff(FakePanel deck, FakePanel card, string doorName)
        {
            PanelManager.SetReturnDoor(doorName, () => { deck.Open(); });
            deck.Close();
            card.Open();
        }

        private static void CaseA_CardCloseReturnsToDeck(List<string> failures, StringBuilder log, CapturingSink sink)
        {
            int mark = Reset(sink);
            var deck = new FakePanel("Player Deck");
            var card = new FakePanel("Inventory");

            if (!deck.Open()) { failures.Add(Tag + " A: the deck did not open (arbiter refused a plain handle)"); return; }
            HandOff(deck, card, "Hero deck");

            if (PanelManager.OpenPanelName != "Inventory")
                failures.Add(Tag + " A: after the hand-off the open panel is '" + (PanelManager.OpenPanelName ?? "<none>") + "', expected 'Inventory'");
            if (PanelManager.ReturnDoorName != "Hero deck")
                failures.Add(Tag + " A: the door was not kept across the deck's own close -> card open (ReturnDoorName='" + (PanelManager.ReturnDoorName ?? "<null>") + "')");
            if (PanelManager.ReturnDoorPending)
                failures.Add(Tag + " A: the door is still PENDING after the card opened - NotifyOpened must keep (not leave armed) the door");
            if (PanelManager.PumpReturnDoor(PanelManager.CloseGraceUntilFrame + 5) || deck.Opens != 1)
                failures.Add(Tag + " A: the pump re-opened the deck while the card was still open (deck.Opens=" + deck.Opens + ")");

            card.Close();
            int grace = PanelManager.CloseGraceUntilFrame;
            if (!PanelManager.ReturnDoorPending)
                failures.Add(Tag + " A: closing the card to nothing did not ARM the door");
            if (PanelManager.PumpReturnDoor(grace) || deck.IsOpen)
                failures.Add(Tag + " A: the door fired INSIDE the WO-1393 close grace (frame " + grace + ") - the re-open would eat the dismissing tap");
            if (!PanelManager.PumpReturnDoor(grace + 1))
                failures.Add(Tag + " A: the pump after the grace (frame " + (grace + 1) + ") did not fire the door");
            if (!deck.IsOpen || deck.Opens != 2 || PanelManager.OpenPanelName != "Player Deck")
                failures.Add(Tag + " A: closing a card-opened panel did not return to the deck (deck.IsOpen=" + deck.IsOpen +
                             " opens=" + deck.Opens + " open='" + (PanelManager.OpenPanelName ?? "<none>") + "')");
            if (PanelManager.ReturnDoorName != null)
                failures.Add(Tag + " A: the door was not consumed by firing (ReturnDoorName='" + PanelManager.ReturnDoorName + "')");
            if (!TraceSince(sink, mark, "return door SET 'Hero deck'"))
                failures.Add(Tag + " A: no '[Flow:Navigation] return door SET' line");
            if (!TraceSince(sink, mark, "return door FIRED 'Hero deck'"))
                failures.Add(Tag + " A: no '[Flow:Navigation] return door FIRED 'Hero deck'' line");
            log.AppendLine("  A: Hero deck -> Inventory -> close -> pump(grace)=hold, pump(grace+1)=deck re-opened, door consumed");
        }

        private static void CaseB_SwapKeepsDoor(List<string> failures, StringBuilder log, CapturingSink sink)
        {
            int mark = Reset(sink);
            var deck = new FakePanel("Player Deck");
            var card = new FakePanel("Character");
            var sibling = new FakePanel("Talent Tree");

            deck.Open();
            HandOff(deck, card, "Hero deck");
            if (!sibling.Open()) { failures.Add(Tag + " B: the sibling did not open"); return; }
            if (card.IsOpen)
                failures.Add(Tag + " B: the arbiter did not close the card when the sibling opened (exclusivity broken)");
            if (PanelManager.ReturnDoorName != "Hero deck")
                failures.Add(Tag + " B: the swap Character -> Talent Tree consumed the door (ReturnDoorName='" + (PanelManager.ReturnDoorName ?? "<null>") + "')");

            sibling.Close();
            PanelManager.PumpReturnDoor(PanelManager.CloseGraceUntilFrame + 1);
            if (!deck.IsOpen || deck.Opens != 2)
                failures.Add(Tag + " B: closing the swapped-to sibling did not return to the deck (deck.Opens=" + deck.Opens + ")");
            if (!TraceSince(sink, mark, "return door FIRED 'Hero deck'"))
                failures.Add(Tag + " B: no FIRED trace after the swap's close");
            log.AppendLine("  B: deck -> Character -> Talent Tree (swap, door kept) -> close -> deck re-opened");
        }

        private static void CaseC_HudOpenedCloseDoesNotReturn(List<string> failures, StringBuilder log, CapturingSink sink)
        {
            int mark = Reset(sink);
            var deck = new FakePanel("Player Deck");
            var card = new FakePanel("Inventory");

            if (!card.Open()) { failures.Add(Tag + " C: the HUD-opened panel did not open"); return; }
            card.Close();
            bool fired = PanelManager.PumpReturnDoor(PanelManager.CloseGraceUntilFrame + 1);
            if (fired || deck.Opens != 0 || deck.IsOpen || PanelManager.AnyOpen)
                failures.Add(Tag + " C: a HUD-opened panel's close re-opened something (fired=" + fired + " deck.Opens=" + deck.Opens +
                             " open='" + (PanelManager.OpenPanelName ?? "<none>") + "') - no door was ever set");
            if (TraceSince(sink, mark, "return door FIRED"))
                failures.Add(Tag + " C: a FIRED trace appeared with no door set");
            log.AppendLine("  C: HUD -> Inventory -> close -> nothing re-opens");
        }

        private static void CaseD_CloseAllClears(List<string> failures, StringBuilder log, CapturingSink sink)
        {
            int mark = Reset(sink);
            var deck = new FakePanel("Player Deck");
            var card = new FakePanel("Inventory");

            deck.Open();
            HandOff(deck, card, "Hero deck");
            PanelManager.CloseAll();
            if (PanelManager.AnyOpen)
                failures.Add(Tag + " D: CloseAll left '" + PanelManager.OpenPanelName + "' open");
            if (PanelManager.ReturnDoorName != null || PanelManager.ReturnDoorPending)
                failures.Add(Tag + " D: CloseAll did not clear the door (ReturnDoorName='" + (PanelManager.ReturnDoorName ?? "<null>") + "')");
            bool fired = PanelManager.PumpReturnDoor(PanelManager.CloseGraceUntilFrame + 1);
            if (fired || deck.Opens != 1 || deck.IsOpen)
                failures.Add(Tag + " D: the deck re-opened after CloseAll (the combat HUD would be covered two frames later)");
            if (!TraceSince(sink, mark, "return door CLEARED 'Hero deck' reason=closeall"))
                failures.Add(Tag + " D: no 'return door CLEARED ... reason=closeall' trace");
            log.AppendLine("  D: deck -> Inventory -> CloseAll -> door cleared, nothing re-opens");
        }

        private static void CaseE_PauseWithNothingOpenClears(List<string> failures, StringBuilder log, CapturingSink sink)
        {
            int mark = Reset(sink);
            var deck = new FakePanel("Player Deck");
            int reopens = 0;
            PanelManager.SetReturnDoor("Hero deck", () => { reopens++; deck.Open(); });
            if (PanelManager.AnyOpen) { failures.Add(Tag + " E: precondition broken - something is open"); return; }
            // No panel open: RequestBack raises PauseToggleRequested (no subscriber in EditMode).
            Guard.Try("Regression", "PauseGate.RequestBack with nothing open", PauseGate.RequestBack);
            if (PanelManager.ReturnDoorName != null)
                failures.Add(Tag + " E: a back request with nothing open did not clear the door (ReturnDoorName='" + PanelManager.ReturnDoorName + "')");
            bool fired = PanelManager.PumpReturnDoor(PanelManager.CloseGraceUntilFrame + 1);
            if (fired || reopens != 0)
                failures.Add(Tag + " E: the deck re-opened under the pause request (reopens=" + reopens + ")");
            if (!TraceSince(sink, mark, "return door CLEARED 'Hero deck' reason=pause"))
                failures.Add(Tag + " E: no 'return door CLEARED ... reason=pause' trace");
            log.AppendLine("  E: door set, nothing open, RequestBack -> door cleared, no re-open");
        }

        private static void CaseF_BackOnChildReturns(List<string> failures, StringBuilder log, CapturingSink sink)
        {
            int mark = Reset(sink);
            var deck = new FakePanel("Player Deck");
            var card = new FakePanel("Rumor Board");

            deck.Open();
            HandOff(deck, card, "Journey deck");
            // The Android back / dock Pause path: PauseGate -> PanelManager.CloseOpen.
            PauseGate.RequestBack();
            if (card.IsOpen || PanelManager.AnyOpen)
                failures.Add(Tag + " F: RequestBack did not close the open child");
            if (!PanelManager.ReturnDoorPending)
                failures.Add(Tag + " F: CloseOpen (the back path) did not ARM the door");
            PanelManager.PumpReturnDoor(PanelManager.CloseGraceUntilFrame + 1);
            if (!deck.IsOpen || deck.Opens != 2)
                failures.Add(Tag + " F: back on a card-opened panel did not return to the deck (deck.Opens=" + deck.Opens + ")");
            if (!TraceSince(sink, mark, "return door FIRED 'Journey deck'"))
                failures.Add(Tag + " F: no FIRED trace for the back path");
            log.AppendLine("  F: Journey deck -> Rumor Board -> back (CloseOpen) -> deck re-opened");
        }

        private static void CaseG_SourceShape(List<string> failures, StringBuilder log)
        {
            string deck = ReadOrNull(DeckSrc);
            string pm = ReadOrNull(ArbiterSrc);
            string pause = ReadOrNull(PauseSrc);
            if (deck == null || pm == null || pause == null)
            {
                failures.Add(Tag + " G: could not read " + DeckSrc + " / " + ArbiterSrc + " / " + PauseSrc);
                return;
            }

            // The deck sets the door BEFORE it closes itself, and the reopen traces the WO line.
            string openCard = Between(deck, "private void OpenCard(Card spec)", "private static Card Route(");
            if (openCard == null)
                failures.Add(Tag + " G: PlayerDeckWorkspace.OpenCard not found");
            else
            {
                int set = openCard.IndexOf("PanelManager.SetReturnDoor(", StringComparison.Ordinal);
                int close = openCard.IndexOf("Close();", StringComparison.Ordinal);
                if (set < 0)
                    failures.Add(Tag + " G: OpenCard does not set a return door - closing any card's screen drops to the HUD (dead end 9)");
                else if (close < 0 || set > close)
                    failures.Add(Tag + " G: OpenCard sets the return door AFTER Close() - the deck's own close would arm and fire it");
                if (!openCard.Contains("\"deck return -> \" + kind"))
                    failures.Add(Tag + " G: the deck's reopen does not trace '[Flow:Navigation] deck return -> <kind>'");
                if (!openCard.Contains("Open(new PlayerDeckPage(kind))"))
                    failures.Add(Tag + " G: the reopen is not the deck's own Open(page) - a second spawner is forbidden");
            }

            // The arbiter: both close-to-nothing paths arm; CloseAll clears; battle-lock clears.
            string notifyClosed = Between(pm, "public static void NotifyClosed(", "public static void CloseAll()");
            string closeAll = Between(pm, "public static void CloseAll()", "public static void CloseOpen()");
            string closeOpen = Between(pm, "public static void CloseOpen()", "OpenStateChanged?.Invoke();");
            if (notifyClosed == null || !notifyClosed.Contains("ArmReturnDoor(handle.Name);"))
                failures.Add(Tag + " G: PanelManager.NotifyClosed does not arm the return door");
            if (closeOpen == null || !closeOpen.Contains("ArmReturnDoor(open.Name);"))
                failures.Add(Tag + " G: PanelManager.CloseOpen (back/ESC) does not arm the return door");
            if (closeAll == null || !closeAll.Contains("ClearReturnDoor(\"closeall\");"))
                failures.Add(Tag + " G: PanelManager.CloseAll does not clear the return door");
            if (!pm.Contains("ClearReturnDoor(\"battle-lock\");"))
                failures.Add(Tag + " G: a battle-lock rejection does not clear the return door");
            // The pump fires strictly AFTER the WO-1393 grace, never inside it.
            if (!pm.Contains("if (frame <= _returnDoorPendingUntilFrame) return false;"))
                failures.Add(Tag + " G: PumpReturnDoor does not hold through the close-frame grace");

            // Pause with nothing open forgets the door.
            string requestBack = Between(pause, "public static void RequestBack()", "PauseToggleRequested?.Invoke();");
            if (requestBack == null || !requestBack.Contains("PanelManager.ClearReturnDoor(\"pause\");"))
                failures.Add(Tag + " G: PauseGate.RequestBack with nothing open does not clear the return door");

            log.AppendLine("  G: source - OpenCard sets before Close(); NotifyClosed/CloseOpen arm; CloseAll/pause/battle-lock clear; pump holds through grace");
        }

        private static string ReadOrNull(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        private static string Between(string src, string from, string until)
        {
            int a = src.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return null;
            int b = src.IndexOf(until, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? null : src.Substring(a, b - a);
        }
    }
}
