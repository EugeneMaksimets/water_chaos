using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Test target: tints the object while the water stream hits it.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class DebugImpactReceiver : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Colors")]
        [SerializeField]
        private Color _idleColor = Color.white;
        [SerializeField]
        private Color _hitColor = Color.cyan;

        private Material _material;
        private float _lastHitTime = float.NegativeInfinity;

        private void Awake()
        {
            _material = GetComponent<Renderer>().material;
        }

        public void ReceiveImpact(in WaterImpact impact)
        {
            _lastHitTime = Time.time;
        }

        private void Update()
        {
            bool isHit = Time.time - _lastHitTime <= Time.fixedDeltaTime * 2f;
            _material.color = isHit ? _hitColor : _idleColor;
        }
    }
}