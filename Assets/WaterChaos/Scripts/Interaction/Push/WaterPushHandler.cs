using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pushes the attached Rigidbody along the water flow while the stream hits it.
    /// With a <see cref="WaterPushProfile"/> the body takes the profile's mass and damping, ignores water
    /// weaker than its minimum, and is pushed at the contact point (so tall objects tip and round ones roll).
    /// Without a profile the full impact force is applied to the centre of mass.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaterPushHandler : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Reaction")]
        [Tooltip("Category settings (mass, threshold, force share). Empty = full force, Rigidbody settings as is.")]
        [SerializeField] private WaterPushProfile _profile;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (_profile != null)
                ApplyProfile();
        }

        // Called during FixedUpdate; Rigidbody sums all forces added within the step.
        public void ReceiveImpact(in WaterImpact impact)
        {
            if (_profile == null)
            {
                _rigidbody.AddForce(WaterPush.CalculateForce(impact), ForceMode.Force);
                return;
            }

            if (!_profile.Pushable || _rigidbody.isKinematic)
                return;

            Vector3 force = WaterPush.CalculateForce(impact, _profile.MinImpactForce, _profile.ForceMultiplier);
            if (force != Vector3.zero)
                _rigidbody.AddForceAtPosition(force, impact.ContactPoint, ForceMode.Force);
        }

        private void ApplyProfile()
        {
            _rigidbody.mass = _profile.Mass;
            _rigidbody.linearDamping = _profile.LinearDamping;
            _rigidbody.angularDamping = _profile.AngularDamping;
            _rigidbody.maxLinearVelocity = _profile.MaxSpeed;
        }
    }
}
