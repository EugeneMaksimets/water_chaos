using UnityEngine;

namespace WaterChaos.Sounds
{
    /// <summary>The background of the level (wind, birds): always playing, the same everywhere.</summary>
    public sealed class AmbienceSound : MonoBehaviour
    {
        [SerializeField] private SoundId _sound = SoundId.Ambience;

        private SoundLoop _loop;

        private void Awake()
        {
            _loop = new SoundLoop(gameObject, _sound, 2f);
        }

        private void Update()
        {
            _loop.Update(true);
        }
    }
}
