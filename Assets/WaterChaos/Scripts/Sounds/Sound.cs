using UnityEngine;

namespace WaterChaos.Sounds
{
    /// <summary>
    /// Plays one-shot sounds from a small pool of sources, with the volume, pitch and range of <see cref="SoundSettings"/>.
    /// UI sounds also play while the game is paused (the story panel).
    /// </summary>
    public static class Sound
    {
        private const int PoolSize = 16;

        private static AudioSource[] s_pool;
        private static int s_next;
        private static bool s_quitting;

        // Domain reload on entering Play Mode is off in this project: start each run afresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_pool = null;
            s_next = 0;
            s_quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            s_quitting = true;
        }

        /// <summary>Plays the sound in the world at <paramref name="position"/> (flat if it is not spatial).</summary>
        public static void Play(SoundId id, Vector3 position, float volumeScale = 1f)
        {
            Play(id, position, volumeScale, false);
        }

        /// <summary>Plays a flat sound that is heard even while the game is paused (UI).</summary>
        public static void PlayUI(SoundId id)
        {
            Play(id, Vector3.zero, 1f, true);
        }

        private static void Play(SoundId id, Vector3 position, float volumeScale, bool ignorePause)
        {
            // Sounds belong to the game: none in the Editor outside Play Mode (e.g. edit-mode tests).
            // Nor while the game shuts down: objects destroyed then must not build a new pool.
            if (!Application.isPlaying || s_quitting)
                return;

            SoundSettings settings = SoundSettings.Instance;
            SoundSettings.Entry entry = settings != null ? settings.Get(id) : null;
            AudioClip clip = entry != null ? entry.RandomClip() : null;
            if (clip == null)
                return;

            AudioSource source = Next();
            SoundSettings.Configure(source, entry);
            source.transform.position = position;
            source.ignoreListenerPause = ignorePause;
            source.pitch = Random.Range(entry.Pitch.x, entry.Pitch.y);
            source.volume = entry.Volume * settings.MasterVolume * volumeScale;
            source.clip = clip;
            source.Play();
        }

        private static AudioSource Next()
        {
            if (s_pool == null || s_pool[0] == null)
            {
                GameObject root = new GameObject("Sound Pool");
                Object.DontDestroyOnLoad(root);
                s_pool = new AudioSource[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    GameObject voice = new GameObject("Voice " + i);
                    voice.transform.SetParent(root.transform, false);
                    s_pool[i] = voice.AddComponent<AudioSource>();
                }
            }

            // The oldest voice is reused; with a free one first if there is one.
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource candidate = s_pool[(s_next + i) % PoolSize];
                if (!candidate.isPlaying)
                {
                    s_next = (s_next + i + 1) % PoolSize;
                    return candidate;
                }
            }

            AudioSource oldest = s_pool[s_next];
            s_next = (s_next + 1) % PoolSize;
            return oldest;
        }
    }
}
