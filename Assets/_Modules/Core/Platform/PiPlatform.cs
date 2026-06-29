namespace DeNelle.Core.Platform
{
    /// <summary>
    /// Resolves the active Pi platform: the real WebGL bridge inside a WebGL player
    /// (which is the only place window.Pi can exist), an inert stub everywhere else.
    /// Mirrors how the save/wallet seams pick a real-vs-stub provider. Gameplay asks
    /// for PiPlatform.Current and never learns which it got.
    /// </summary>
    public static class PiPlatform
    {
        private static IPiPlatform _current;

        public static IPiPlatform Current
        {
            get
            {
                if (_current == null)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    _current = new WebGLPiPlatform();
#else
                    _current = new EditorPiPlatform();
#endif
                }
                return _current;
            }
        }

        /// <summary>Test seam — inject a mock platform.</summary>
        public static void Override(IPiPlatform platform) => _current = platform;
    }
}
