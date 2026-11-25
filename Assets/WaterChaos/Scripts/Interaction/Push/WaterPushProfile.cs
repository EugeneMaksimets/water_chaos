using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// How one category of objects (light plastic, chairs, heavy furniture, doors...) reacts to water.
    /// Shared by all objects of the category through <see cref="WaterPushHandler"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "WaterPushProfile", menuName = "Water Chaos/Water Push Profile")]
    public sealed class WaterPushProfile : ScriptableObject
    {
        [Header("Body")]
        [Tooltip("Mass in kilograms (kg).")]
        [SerializeField, Min(0.01f)] private float _mass = 5f;

        [Tooltip("Linear damping (drag); higher = the object stops sooner.")]
        [SerializeField, Min(0f)] private float _linearDamping = 0.2f;

        [Tooltip("Angular damping; higher = the object spins less.")]
        [SerializeField, Min(0f)] private float _angularDamping = 0.5f;

        [Tooltip("Top speed the water can give the object, in meters per second (m/s).")]
        [SerializeField, Min(0.1f)] private float _maxSpeed = 8f;

        [Header("Water")]
        [Tooltip("Off = the water never moves this object (it still blocks the stream).")]
        [SerializeField] private bool _pushable = true;

        [Tooltip("Weakest water impact that moves the object, in newtons (N).")]
        [SerializeField, Min(0f)] private float _minImpactForce;

        [Tooltip("Share of the impact force applied to the object (1 = all of it).")]
        [SerializeField, Min(0f)] private float _forceMultiplier = 1f;

        /// <summary>Mass in kilograms (kg).</summary>
        public float Mass => _mass;
        public float LinearDamping => _linearDamping;
        public float AngularDamping => _angularDamping;
        /// <summary>Top speed in meters per second (m/s).</summary>
        public float MaxSpeed => _maxSpeed;
        public bool Pushable => _pushable;
        /// <summary>Weakest impact in newtons (N) that moves the object.</summary>
        public float MinImpactForce => _minImpactForce;
        public float ForceMultiplier => _forceMultiplier;
    }
}
