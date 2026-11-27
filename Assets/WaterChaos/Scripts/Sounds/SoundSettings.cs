using System;
using UnityEngine;

namespace WaterChaos.Sounds
{
    /// <summary>
    /// All game sounds in one place: for each, its clips (a random one plays), volume, pitch range and whether it sounds
    /// in the world (3D, quieter with distance and silent beyond Max Distance) or flat (UI, the player's own steps).
    /// Lives at Resources/SoundSettings; changes made in Play Mode are heard at once.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundSettings", menuName = "Water Chaos/Sound Settings")]
    public sealed class SoundSettings : ScriptableObject
    {
        private const string ResourcePath = "SoundSettings";

        [Serializable]
        public sealed class Entry
        {
            public SoundId Id;

            [Tooltip("A random one plays each time.")]
            public AudioClip[] Clips = new AudioClip[0];

            [Range(0f, 1f)] public float Volume = 0.8f;

            [Tooltip("Random pitch between these values.")]
            public Vector2 Pitch = new Vector2(0.95f, 1.05f);

            [Tooltip("Sounds in the world (3D): quieter farther away, silent beyond Max Distance. Off = flat (UI).")]
            public bool Spatial = true;

            [Tooltip("Full volume up to this distance, in meters (m).")]
            [Min(0.1f)] public float MinDistance = 2f;

            [Tooltip("Silent beyond this distance, in meters (m).")]
            [Min(0.2f)] public float MaxDistance = 25f;

            public AudioClip RandomClip()
            {
                return Clips == null || Clips.Length == 0 ? null : Clips[UnityEngine.Random.Range(0, Clips.Length)];
            }
        }

        private static SoundSettings s_instance;

        [Tooltip("Volume of every sound of the game.")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;

        [SerializeField] private Entry[] _sounds = new Entry[0];

        public float MasterVolume => _masterVolume;

        /// <summary>The settings in Resources; null if there are none (then the game is silent).</summary>
        public static SoundSettings Instance => s_instance != null ? s_instance : (s_instance = Resources.Load<SoundSettings>(ResourcePath));

        public Entry Get(SoundId id)
        {
            foreach (Entry entry in _sounds)
            {
                if (entry != null && entry.Id == id)
                    return entry;
            }

            return null;
        }

        /// <summary>Sets up a source for this sound: 3D with a linear fall-off to silence, or flat.</summary>
        public static void Configure(AudioSource source, Entry entry)
        {
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = entry.Spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = entry.MinDistance;
            source.maxDistance = Mathf.Max(entry.MinDistance + 0.1f, entry.MaxDistance);
        }
    }
}
