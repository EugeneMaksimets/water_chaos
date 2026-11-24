using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Shared fire spreading tuning. An asset (not prefab fields) so every fire uses the same values
    /// and the fire prefab reference is not remapped to the instance on Instantiate.
    /// </summary>
    [CreateAssetMenu(fileName = "FireSpreadSettings", menuName = "Water Chaos/Fire Spread Settings")]
    public sealed class FireSpreadSettings : ScriptableObject
    {
        [Header("Fire")]
        [Tooltip("Prefab spawned on a flammable object that catches fire.")]
        [SerializeField] private FireExtinguishHandler _firePrefab;

        [Header("Spread")]
        [Tooltip("How far a fire looks for flammable objects, in meters (m).")]
        [SerializeField, Min(0.1f)] private float _spreadRadius = 3f;

        [Tooltip("Seconds (s) between spread attempts of one fire.")]
        [SerializeField, Min(FireSpreadModel.MinInterval)] private float _spreadInterval = 6f;

        [Tooltip("Intensity (0..1) a fire needs to spread; watering it below this stops spreading.")]
        [SerializeField, Range(0f, 1f)] private float _minIntensity = 0.5f;

        [Tooltip("Probability (0..1) that a spread attempt succeeds.")]
        [SerializeField, Range(0f, 1f)] private float _ignitionChance = 0.5f;

        [Tooltip("Seconds (s) from a successful attempt until the new fire appears.")]
        [SerializeField, Min(0f)] private float _ignitionDelay = 2f;

        [Tooltip("Random ground points a fire checks per attempt; more = finds a free spot more often.")]
        [SerializeField, Range(1, 16)] private int _surfaceSamples = 8;

        [Tooltip("Height of the fire's pivot above the burning point, in meters (m).")]
        [SerializeField, Min(0f)] private float _spawnHeight = 0.5f;

        [Header("Limits")]
        [Tooltip("No new fire within this distance of an existing one, in meters (m).")]
        [SerializeField, Min(0f)] private float _exclusionRadius = 1.5f;

        [Tooltip("Fires (burning or waiting to reignite) allowed in the scene at once.")]
        [SerializeField, Min(1)] private int _maxActiveFires = 25;

        [Header("Queries")]
        [Tooltip("Layers searched for flammable objects.")]
        [SerializeField] private LayerMask _targetMask = ~0;

        [Tooltip("Layers searched for existing fires.")]
        [SerializeField] private LayerMask _fireMask = ~0;

        [Tooltip("Water volumes (triggers); no fire appears inside them.")]
        [SerializeField] private LayerMask _waterMask = 1 << 4;

        public FireExtinguishHandler FirePrefab => _firePrefab;
        public float SpreadRadius => _spreadRadius;
        public float SpreadInterval => _spreadInterval;
        public float MinIntensity => _minIntensity;
        public float IgnitionChance => _ignitionChance;
        public float IgnitionDelay => _ignitionDelay;
        public int SurfaceSamples => _surfaceSamples;
        public float ExclusionRadius => _exclusionRadius;
        public int MaxActiveFires => _maxActiveFires;
        public LayerMask TargetMask => _targetMask;
        public LayerMask FireMask => _fireMask;
        public LayerMask WaterMask => _waterMask;

        /// <summary>Where the fire's pivot goes for a fire burning at <paramref name="burningPoint"/>.</summary>
        public Vector3 GetFirePosition(Vector3 burningPoint) => burningPoint + Vector3.up * _spawnHeight;
    }
}
