namespace DeNelle.Core
{
    /// <summary>
    /// Decoupling hook for the cinematic intro. The 9-screen Yarn intro is played by
    /// DeNelle.DialogueUI (which references Yarn/ClassicRPG), but the trigger lives on
    /// the Title screen in DeNelle.Onboarding — and Onboarding must NOT depend on the
    /// dialogue stack. So DialogueUI registers <see cref="Play"/> at startup, and the
    /// Title's "Play Intro" button invokes it through Core (which both already
    /// reference). Mirrors the CoreServices service-locator pattern.
    /// </summary>
    public static class IntroLauncher
    {
        /// <summary>
        /// Set by DeNelle.DialogueUI.IntroSequencePlayer at startup. Invoke via the
        /// null-conditional operator (it's null in scenes/builds without the intro).
        /// </summary>
        public static System.Action Play;
    }
}
