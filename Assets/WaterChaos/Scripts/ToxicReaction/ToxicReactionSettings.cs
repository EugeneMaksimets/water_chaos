using UnityEngine;

namespace WaterChaos.ToxicReaction
{
    /// <summary>Shared tuning of the toxic fire reaction, its clouds and chain reactions.</summary>
    [CreateAssetMenu(menuName = "Water Chaos/Toxic Reaction Settings", fileName = "ToxicReactionSettings")]
    public sealed class ToxicReactionSettings : ScriptableObject
    {
        [Header("Reaction")]
        [Tooltip("How long a fire stays toxic after toxic liquid hits it, in seconds (s).")]
        [SerializeField, Range(0.5f, 10f)] private float _duration = 2.5f;

        [Tooltip("After a reaction, toxic liquid does nothing to this fire for this long, in seconds (s).")]
        [SerializeField, Range(0f, 30f)] private float _cooldown = 6f;

        [Header("Cloud")]
        [SerializeField] private ToxicCloud _cloudPrefab;

        [Tooltip("Cloud center above the fire, in meters (m).")]
        [SerializeField, Range(0f, 3f)] private float _cloudHeight = 1f;

        [Tooltip("Cloud radius when it appears, in meters (m).")]
        [SerializeField, Range(0.1f, 3f)] private float _cloudStartRadius = 0.6f;

        [Tooltip("Cloud radius once spread, in meters (m).")]
        [SerializeField, Range(0.5f, 10f)] private float _cloudMaxRadius = 3f;

        [Tooltip("Seconds (s) the cloud takes to spread to its full radius.")]
        [SerializeField, Range(0f, 10f)] private float _cloudSpreadTime = 2f;

        [Tooltip("Seconds (s) the cloud stays toxic.")]
        [SerializeField, Range(0.5f, 30f)] private float _cloudLifetime = 6f;

        [Header("Chain Reaction")]
        [Tooltip("How many steps a chain may go: 1 = a cloud sets off other fires once, their smaller clouds set off nothing.")]
        [SerializeField, Range(0, 3)] private int _maxChainGeneration = 1;

        [Tooltip("Each step of a chain makes a cloud this much smaller (0..1).")]
        [SerializeField, Range(0.1f, 1f)] private float _chainScale = 0.5f;

        [Tooltip("Small blast where a cloud sets off another fire (look only).")]
        [SerializeField] private GameObject _chainBlastPrefab;

        [Header("Splashes")]
        [Tooltip("Toxic puddles splashed around the fire when it starts reacting to toxic liquid.")]
        [SerializeField, Range(0, 8)] private int _splashCount = 3;

        [Tooltip("Liquid in each splash, in liters (L).")]
        [SerializeField, Range(0.5f, 20f)] private float _splashLiters = 4f;

        [Tooltip("How far around the fire splashes land, in meters (m).")]
        [SerializeField, Range(0.3f, 5f)] private float _splashRadius = 1.5f;

        public float Duration => _duration;
        public float Cooldown => _cooldown;
        public ToxicCloud CloudPrefab => _cloudPrefab;
        public float CloudHeight => _cloudHeight;
        public int MaxChainGeneration => _maxChainGeneration;
        public float ChainScale => _chainScale;
        public GameObject ChainBlastPrefab => _chainBlastPrefab;
        public int SplashCount => _splashCount;
        public float SplashLiters => _splashLiters;
        public float SplashRadius => _splashRadius;

        /// <summary>A cloud of the given chain generation (smaller the further down the chain).</summary>
        public ToxicCloudModel CreateCloud(Vector3 center, int generation)
        {
            float scale = ToxicChainRules.SizeScale(generation, _chainScale);
            return new ToxicCloudModel(center, _cloudStartRadius * scale, _cloudMaxRadius * scale,
                _cloudSpreadTime, _cloudLifetime * Mathf.Lerp(1f, scale, 0.5f), generation);
        }
    }
}
