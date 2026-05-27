using UnityEngine;
using UnityEngine.Video;

namespace DeNelle.BattleATB
{
    public class ATBBackgroundController : MonoBehaviour
    {
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private VideoClip[] _backgroundClips;

        private void Start()
        {
            if (_videoPlayer == null || _backgroundClips == null || _backgroundClips.Length == 0) return;
            _videoPlayer.clip = _backgroundClips[Random.Range(0, _backgroundClips.Length)];
            _videoPlayer.isLooping = true;
            _videoPlayer.Play();
        }
    }
}
