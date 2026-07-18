// =============================================================================
// JukeboxVMTests (EditMode) — §2c gate for the music-jukebox MVVM slice.
// -----------------------------------------------------------------------------
// Locks the selection model MOVED out of MusicSelectionPanel into the pure
// JukeboxVM (isSelected = track == chosen; chosen None -> context default).
// FAKE ISource — no scene, no AudioService.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Audio;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class JukeboxVMTests
    {
        private sealed class FakeSource : JukeboxVM.ISource
        {
            public bool ReadyFlag = true;
            public AudioService.AmbientContext Context = AudioService.AmbientContext.Village;
            public MusicTrack Chosen = MusicTrack.None;
            public MusicTrack Default = MusicTrack.Village;
            public readonly List<MusicChoice> Choices = new List<MusicChoice>();

            public MusicTrack LastSet = MusicTrack.None;
            public AudioService.AmbientContext LastCtx;
            public int SetCalls;

            public bool Ready => ReadyFlag;
            public AudioService.AmbientContext CurrentContext => Context;
            public MusicTrack GetAmbientChoice(AudioService.AmbientContext c) => Chosen;
            public void SetAmbientChoice(AudioService.AmbientContext c, MusicTrack t)
            { LastCtx = c; LastSet = t; SetCalls++; Chosen = t; }
            public IReadOnlyList<MusicChoice> ChoicesFor(AudioService.AmbientContext c) => Choices;
            public MusicTrack DefaultTrackFor(AudioService.AmbientContext c) => Default;
        }

        private static FakeSource TwoTracks()
        {
            var s = new FakeSource();
            s.Choices.Add(new MusicChoice(MusicTrack.Village, "Town"));
            s.Choices.Add(new MusicChoice(MusicTrack.Overworld, "World"));
            return s;
        }

        [Test]
        public void none_choice_selects_the_context_default()
        {
            var src = TwoTracks();
            src.Chosen = MusicTrack.None;
            src.Default = MusicTrack.Village;
            using var vm = new JukeboxVM(src, null);

            Assert.That(vm.AudioReady, Is.True);
            Assert.That(vm.Tracks.Count, Is.EqualTo(2));
            Assert.That(vm.Tracks[0].Track, Is.EqualTo(MusicTrack.Village));
            Assert.That(vm.Tracks[0].IsSelected, Is.True, "None choice => the default track is selected");
            Assert.That(vm.Tracks[1].IsSelected, Is.False);
        }

        [Test]
        public void explicit_choice_marks_that_track_selected()
        {
            var src = TwoTracks();
            src.Chosen = MusicTrack.Overworld;
            using var vm = new JukeboxVM(src, null);
            Assert.That(vm.Tracks[0].IsSelected, Is.False);
            Assert.That(vm.Tracks[1].IsSelected, Is.True);
        }

        [Test]
        public void set_choice_command_persists_and_refreshes_and_fires_changed()
        {
            var src = TwoTracks();
            src.Chosen = MusicTrack.Village;
            using var vm = new JukeboxVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;

            vm.SetAmbientChoice(MusicTrack.Overworld);

            Assert.That(src.SetCalls, Is.EqualTo(1));
            Assert.That(src.LastSet, Is.EqualTo(MusicTrack.Overworld));
            Assert.That(src.LastCtx, Is.EqualTo(AudioService.AmbientContext.Village));
            Assert.That(vm.Tracks[1].IsSelected, Is.True, "the row re-projects after the pick");
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void not_ready_yields_no_tracks()
        {
            var src = TwoTracks();
            src.ReadyFlag = false;
            using var vm = new JukeboxVM(src, null);
            Assert.That(vm.AudioReady, Is.False);
            Assert.That(vm.Tracks.Count, Is.EqualTo(0));
        }

        [Test]
        public void dispose_clears_changed()
        {
            var src = TwoTracks();
            var vm = new JukeboxVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;
            vm.Dispose();
            // A post-dispose command must not raise Changed (guarded + handler cleared).
            vm.SetAmbientChoice(MusicTrack.Overworld);
            Assert.That(changed, Is.EqualTo(0));
        }
    }
}
