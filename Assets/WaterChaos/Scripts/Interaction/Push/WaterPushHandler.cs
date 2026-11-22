using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pushes the attached Rigidbody along the water flow while the stream hits it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaterPushHandler : MonoBehaviour, IWaterImpactReceiver
    {
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        // Called during FixedUpdate; Rigidbody sums all forces added within the step.
        public void ReceiveImpact(in WaterImpact impact)
        {
            _rigidbody.AddForce(WaterPush.CalculateForce(impact), ForceMode.Force);
        }
    }
}