using UnityEngine;

namespace WaterChaos.Sounds
{
    /// <summary>
    /// A looping sound on an object (fire, pump motor, stream) that fades in and out as its owner turns it on and off,
    /// with the volume, range and pitch of <see cref="SoundSettings"/>. Silent and stopped while off.
    /// </summary>
    public sealed class SoundLoop
    {
        private readonly SoundId _id;
        private readonly float _fadeTime;
        private readonly AudioSource _source;
        private readonly float _basePitch;
        private float _level;

        /// <param name="host">Object the sound comes from (its position).</param>
        /// <param name="fadeTime">Time to fade fully in or out, in seconds (s).</param>
        public SoundLoop(GameObject host, SoundId id, float fadeTime = 0.25f)
        {
            _id = id;
            _fadeTime = Mathf.Max(0.01f, fadeTime);

            SoundSettings settings = SoundSettings.Instance;
            SoundSettings.Entry entry = settings != null ? settings.Get(id) : null;
            AudioClip clip = entry != null ? entry.RandomClip() : null;
            if (clip == null)
                return;

            _source = host.AddComponent<AudioSource>();
            SoundSettings.Configure(_source, entry);
            _source.loop = true;
            _source.clip = clip;
            _source.volume = 0f;
            _basePitch = Random.Range(entry.Pitch.x, entry.Pitch.y);
        }

        /// <summary>Call every frame: fades toward on/off at <paramref name="volumeScale"/> of the set volume.</summary>
        public void Update(bool on, float volumeScale = 1f, float pitch = 1f)
        {
            if (_source == null)
                return;

            SoundSettings settings = SoundSettings.Instance;
            SoundSettings.Entry entry = settings != null ? settings.Get(_id) : null;
            float full = entry != null ? entry.Volume * settings.MasterVolume : 0f;

            _level = Mathf.MoveTowards(_level, on ? 1f : 0f, Time.unscaledDeltaTime / _fadeTime);
            _source.volume = full * Mathf.Max(0f, volumeScale) * _level;
            _source.pitch = _basePitch * pitch;

            if (_level > 0f && !_source.isPlaying)
            {
                _source.time = Random.Range(0f, _source.clip.length * 0.9f);
                _source.Play();
            }
            else if (_level <= 0f && _source.isPlaying)
            {
                _source.Stop();
            }
        }

        /// <summary>Silences it at once.</summary>
        public void Stop()
        {
            _level = 0f;
            if (_source != null)
                _source.Stop();
        }
    }
}
