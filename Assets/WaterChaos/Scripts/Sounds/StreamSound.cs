using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Sounds
{
    /// <summary>
    /// Sound of a nozzle stream: water rushing at the nozzle and splashing where it hits, louder with more flow;
    /// a roar instead while it burns as a flamethrower. Silent while nothing flows.
    /// </summary>
    public sealed class StreamSound : MonoBehaviour
    {
        // Flow that sounds at full volume, in liters per second (L/s).
        private const float FullFlow = 10f;

        [SerializeField] private WaterStream _stream;

        [Tooltip("Optional: the flamethrower of this stream; found next to the stream if empty.")]
        [SerializeField] private FuelStreamIgnition _ignition;

        private SoundLoop _spray;
        private SoundLoop _flame;
        private SoundLoop _splash;
        private Transform _hitPoint;

        private void Awake()
        {
            if (_stream == null)
                _stream = GetComponent<WaterStream>();
            if (_ignition == null)
                _ignition = GetComponent<FuelStreamIgnition>();

            _hitPoint = new GameObject("Splash Sound").transform;
            _hitPoint.SetParent(transform, false);

            _spray = new SoundLoop(gameObject, SoundId.WaterSpray, 0.2f);
            _flame = new SoundLoop(gameObject, SoundId.Flamethrower, 0.2f);
            _splash = new SoundLoop(_hitPoint.gameObject, SoundId.WaterSplash, 0.15f);
        }

        private void OnDisable()
        {
            _spray.Stop();
            _flame.Stop();
            _splash.Stop();
        }

        private void Update()
        {
            if (_stream == null)
                return;

            float flow = _stream.Flow.FlowRate;
            bool flowing = flow > 0.05f && _stream.VisiblePointCount >= 2;
            bool lit = _ignition != null && _ignition.IsLit;
            float strength = 0.35f + 0.65f * Mathf.Clamp01(flow / FullFlow);

            _spray.Update(flowing && !lit, strength);
            _flame.Update(lit);

            bool splashing = flowing && !lit && _stream.HasHit;
            if (splashing)
                _hitPoint.position = _stream.HitPoint;
            _splash.Update(splashing, strength);
        }
    }
}
