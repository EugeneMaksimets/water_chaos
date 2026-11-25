using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Lets a fire ignite its surroundings while it burns strongly enough: flammable objects in range
    /// and random points of flammable ground in every direction. Never in water, never next to another fire.
    /// Timing rules live in FireSpreadModel.
    /// </summary>
    [RequireComponent(typeof(FireExtinguishHandler))]
    public sealed class FireSpreader : MonoBehaviour
    {
        private const int MaxColliders = 64;
        // How far above and below the fire the ground is searched, in meters (m).
        private const float GroundProbeHeight = 2f;
        // Radius of the water check around a fire position, in meters (m).
        private const float WaterProbeRadius = 0.3f;
        // Radius of the room check around a fire position, in meters (m).
        private const float RoomProbeRadius = 0.05f;

        private static readonly Collider[] s_targetBuffer = new Collider[MaxColliders];
        private static readonly Collider[] s_fireBuffer = new Collider[MaxColliders];
        private static readonly List<Candidate> s_candidates = new List<Candidate>();
        // Fire positions promised to fires that are still igniting.
        private static readonly List<Vector3> s_reserved = new List<Vector3>();

        [Header("Settings")]
        [SerializeField] private FireSpreadSettings _settings;

        private readonly List<Vector3> _ownReserved = new List<Vector3>();
        private FireExtinguishHandler _fire;
        private FireSpreadModel _model;

        /// <summary>Fires with an enabled spreader in the scene, burning or waiting to reignite.</summary>
        public static int ActiveCount { get; private set; }

        private struct Candidate
        {
            public FlammableObject Target;
            public Vector3 FirePosition;
        }

        // Domain reload on entering Play Mode is off in this project, so statics survive between runs:
        // a stale fire count or reserved spot from the last run would block spreading from the start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveCount = 0;
            s_reserved.Clear();
            s_candidates.Clear();
        }

        private void Awake()
        {
            _fire = GetComponent<FireExtinguishHandler>();

            if (_settings == null)
            {
                Debug.LogError($"{name}: FireSpreader needs FireSpreadSettings.", this);
                enabled = false;
                return;
            }

            // A random first wait, so fires placed in the scene do not all spread at the same moment.
            _model = new FireSpreadModel(_settings.SpreadInterval, _settings.MinIntensity, _settings.IgnitionChance,
                _settings.IntervalJitter, Random.value);
        }

        private void OnEnable()
        {
            ActiveCount++;
        }

        private void OnDisable()
        {
            ActiveCount--;

            // Disabling stops the igniting coroutines; free their spots.
            foreach (Vector3 position in _ownReserved)
                Release(position);
            _ownReserved.Clear();
        }

        private void Update()
        {
            if (_model.Tick(Time.deltaTime, _fire.Intensity, Random.value, Random.value) && CanSpreadNow())
                TrySpread();
        }

        /// <summary>
        /// True if a new fire may appear at <paramref name="firePosition"/>: under the fire limit,
        /// not in water, and no fire (existing or igniting) within the exclusion radius.
        /// </summary>
        public static bool CanBurnAt(Vector3 firePosition, FireSpreadSettings settings)
        {
            if (ActiveCount + s_reserved.Count >= settings.MaxActiveFires)
                return false;

            if (Physics.CheckSphere(firePosition, WaterProbeRadius, settings.WaterMask, QueryTriggerInteraction.Collide))
                return false;

            return !IsFireNear(firePosition, settings.ExclusionRadius, settings.FireMask);
        }

        /// <summary>Holds a spot for a fire that is igniting, so no other fire takes it meanwhile.</summary>
        public static void Reserve(Vector3 firePosition)
        {
            s_reserved.Add(firePosition);
        }

        public static void Release(Vector3 firePosition)
        {
            s_reserved.Remove(firePosition);
        }

        private static bool IsFireNear(Vector3 position, float radius, LayerMask fireMask)
        {
            float radiusSqr = radius * radius;
            foreach (Vector3 reserved in s_reserved)
            {
                if ((reserved - position).sqrMagnitude < radiusSqr)
                    return true;
            }

            int count = Physics.OverlapSphereNonAlloc(
                position, radius, s_fireBuffer, fireMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (s_fireBuffer[i].GetComponentInParent<FireExtinguishHandler>() != null)
                    return true;
            }

            return false;
        }

        /// <summary>A room under cooling protection stops its fires from spreading.</summary>
        private bool CanSpreadNow()
        {
            IFireArea area = _fire.Area;
            return area == null || area.AllowsSpread;
        }

        /// <summary>
        /// A fire spreads only within its own area: inside a room to the same room (never through walls),
        /// outdoors to places outside every room.
        /// </summary>
        private bool IsInOwnArea(Vector3 firePosition)
        {
            IFireArea area = _fire.Area;
            if (area != null)
                return area.Contains(firePosition);

            return !Physics.CheckSphere(firePosition, RoomProbeRadius, _settings.RoomMask, QueryTriggerInteraction.Collide);
        }

        private void TrySpread()
        {
            s_candidates.Clear();
            AddObjectCandidates();
            AddGroundCandidates();

            if (s_candidates.Count == 0)
                return;

            Candidate chosen = s_candidates[Random.Range(0, s_candidates.Count)];
            if (chosen.Target != null)
                chosen.Target.TryIgnite(_settings);
            else
                StartCoroutine(IgniteGround(chosen.FirePosition));
        }

        private void AddObjectCandidates()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _settings.SpreadRadius, s_targetBuffer, _settings.TargetMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                FlammableObject target = s_targetBuffer[i].GetComponentInParent<FlammableObject>();
                if (target == null || !target.CanIgnite || ContainsTarget(target))
                    continue;

                Vector3 firePosition = _settings.GetFirePosition(target.IgnitionPosition);
                if (IsInOwnArea(firePosition) && CanBurnAt(firePosition, _settings))
                    s_candidates.Add(new Candidate { Target = target, FirePosition = firePosition });
            }
        }

        /// <summary>Random points around the fire, in every direction, that lie on flammable ground.</summary>
        private void AddGroundCandidates()
        {
            float minDistance = Mathf.Min(_settings.ExclusionRadius, _settings.SpreadRadius);

            for (int i = 0; i < _settings.SurfaceSamples; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction == Vector2.zero)
                    direction = Vector2.right;

                float distance = Random.Range(minDistance, _settings.SpreadRadius);
                Vector3 from = transform.position
                    + new Vector3(direction.x, 0f, direction.y) * distance
                    + Vector3.up * GroundProbeHeight;

                if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, GroundProbeHeight * 2f,
                        _settings.TargetMask, QueryTriggerInteraction.Ignore))
                    continue;

                if (hit.collider.GetComponentInParent<FlammableSurface>() == null)
                    continue;

                Vector3 firePosition = _settings.GetFirePosition(hit.point);
                if (IsInOwnArea(firePosition) && CanBurnAt(firePosition, _settings))
                    s_candidates.Add(new Candidate { Target = null, FirePosition = firePosition });
            }
        }

        private IEnumerator IgniteGround(Vector3 firePosition)
        {
            Reserve(firePosition);
            _ownReserved.Add(firePosition);

            if (_settings.IgnitionDelay > 0f)
                yield return new WaitForSeconds(_settings.IgnitionDelay);

            Release(firePosition);
            _ownReserved.Remove(firePosition);

            // Checked again: something may have changed during the delay.
            if (CanBurnAt(firePosition, _settings))
                Instantiate(_settings.FirePrefab, firePosition, Quaternion.identity);
        }

        private static bool ContainsTarget(FlammableObject target)
        {
            foreach (Candidate candidate in s_candidates)
            {
                if (candidate.Target == target)
                    return true;
            }

            return false;
        }
    }
}
