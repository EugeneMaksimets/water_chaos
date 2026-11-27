using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Environment;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// One per scene. Turns liquid landing on floors into puddles and snow into snowdrifts, freezes and melts them
    /// by the temperature where they lie (rooms only; outdoors nothing freezes), and answers what lies under a point
    /// (ice for the player's movement, toxic liquid for the toxic exposure). Snowdrifts stop new fires through
    /// <see cref="FireIgnitionRules"/>. Listens to every stream's impacts, so no stream or target knows about it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiquidSurfaceController : MonoBehaviour, IFireIgnitionBlocker
    {
        private const int MaxFloorHits = 8;
        // Directions and step used to measure the free floor around a new puddle.
        private const int SpaceDirections = 8;
        private const float SpaceStep = 0.25f;
        // Floor this much higher or lower than the puddle counts as an edge (a step, a curb, the end of a table), in meters (m).
        private const float EdgeHeight = 0.06f;
        // The floor ray starts this far off the hit surface, so it never starts inside it, in meters (m).
        private const float RayLift = 0.05f;

        private static readonly RaycastHit[] s_floorHits = new RaycastHit[MaxFloorHits];

        [Header("Setup")]
        [SerializeField] private LiquidSurfaceSettings _settings;
        [SerializeField] private PuddleView _puddlePrefab;
        [SerializeField] private SnowdriftView _snowdriftPrefab;

        private readonly Dictionary<WaterStream, FuelStreamIgnition> _flames = new Dictionary<WaterStream, FuelStreamIgnition>();
        private readonly Dictionary<SnowdriftModel, SnowdriftView> _driftViews = new Dictionary<SnowdriftModel, SnowdriftView>();
        private readonly Dictionary<PuddleModel, PuddleView> _puddleViews = new Dictionary<PuddleModel, PuddleView>();
        private PuddleField _puddles;
        private SnowdriftField _drifts;
        private float _tickTimer;

        /// <summary>The scene's controller; null when the scene has none (then nothing forms and nothing is slippery).</summary>
        public static LiquidSurfaceController Instance { get; private set; }

        public PuddleField Puddles => _puddles;
        public SnowdriftField Snowdrifts => _drifts;

        /// <summary>Ice under the point and its grip (0..1); false without a controller.</summary>
        public static bool TryGetIceAt(Vector3 point, out float friction)
        {
            friction = 1f;
            return Instance != null && Instance._puddles.TryGetIceAt(point, out friction);
        }

        /// <summary>True if the point stands in a (liquid) toxic puddle.</summary>
        public static bool IsInToxicPuddle(Vector3 point)
        {
            return Instance != null && Instance._puddles.IsInLiquid(point, liquid => liquid.Corrodes());
        }

        // Domain reload on entering Play Mode is off in this project.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError($"{name}: LiquidSurfaceController needs Liquid Surface Settings.", this);
                enabled = false;
                return;
            }

            if (_puddlePrefab == null || _snowdriftPrefab == null)
                Debug.LogWarning($"{name}: Puddle or Snowdrift prefab is not set: puddles and snowdrifts still work but are invisible.", this);

            _puddles = new PuddleField(_settings.ToPuddleConfig(), _settings.MaxPuddles);
            _drifts = new SnowdriftField(_settings.ToSnowdriftConfig(), _settings.MaxSnowdrifts);
            _puddles.Created += OnPuddleCreated;
            _puddles.Removed += OnPuddleRemoved;
            _drifts.Created += OnDriftCreated;
            _drifts.Removed += OnDriftRemoved;
        }

        private void OnEnable()
        {
            if (_puddles == null)
                return;

            if (Instance != null && Instance != this)
                Debug.LogWarning($"{name}: a second LiquidSurfaceController in the scene; the newest one is used.", this);

            Instance = this;
            WaterStream.ImpactDelivered += OnImpact;
            FireIgnitionRules.Register(this);
        }

        private void OnDisable()
        {
            WaterStream.ImpactDelivered -= OnImpact;
            FireIgnitionRules.Unregister(this);
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < _settings.TickInterval)
                return;

            ITemperatureProvider temperatures = TemperatureZones.Provider;
            _puddles.Tick(_tickTimer, temperatures);
            _drifts.Tick(_tickTimer, temperatures);
            _tickTimer = 0f;
        }

        /// <summary>
        /// Pours liquid that lands near <paramref name="point"/> (e.g. splashes from a fire): it joins the puddle on the
        /// floor below or starts one. Returns that puddle, or null if there is no floor below.
        /// </summary>
        public PuddleModel AddLiquidAt(Vector3 point, LiquidType liquid, float liters)
        {
            return _puddles != null ? AddLiquidBelow(point, Vector3.up, liquid, liters) : null;
        }

        public bool BlocksIgnitionAt(Vector3 firePosition)
        {
            return _drifts != null && _drifts.BlocksIgnitionAt(firePosition);
        }

        private void OnImpact(WaterStream stream, in WaterImpact impact, Collider surface)
        {
            if (!(impact.Volume > 0f) || IsBurning(stream))
                return;

            // Characters and moving bodies take the hit themselves (e.g. a toxic spray on the player):
            // only liquid hitting the static world settles into puddles.
            if (surface == null || surface is CharacterController || surface.attachedRigidbody != null)
                return;

            if (impact.IsSnow)
            {
                if (TryFindFloor(impact.ContactPoint, impact.Normal, out Vector3 floor))
                    _drifts.AddSnow(floor, impact.Volume);
                return;
            }

            AddLiquidBelow(impact.ContactPoint, impact.Normal, impact.Liquid, impact.Volume);
        }

        /// <summary>
        /// Liquid lands on the first surface below the point with room for a puddle; a surface too small for one
        /// (a table, a shelf, the back of a sofa) lets it run off onto the next surface below.
        /// </summary>
        private PuddleModel AddLiquidBelow(Vector3 point, Vector3 normal, LiquidType liquid, float liters)
        {
            int count = CastDown(point, normal);
            SortByDistance(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_floorHits[i];
                // Distance 0: the ray started inside the collider, so the hit point is not valid.
                if (hit.distance <= 0f || !IsStaticSurface(hit.collider) || hit.normal.y < _settings.FloorMinUp)
                    continue;

                // Joining a puddle needs no measuring: it already has its room.
                if (_puddles.FindNearby(hit.point) != null)
                    return _puddles.AddLiquid(hit.point, liquid, liters);

                float space = MeasureSpace(hit.point);
                PuddleModel puddle = _puddles.AddLiquid(hit.point, liquid, liters, space);
                if (puddle != null)
                    return puddle;
            }

            return null;
        }

        /// <summary>
        /// Free floor around a point, in meters (m): how far the floor goes on at the same height in every direction
        /// before an edge or a wall. Stops early once it is too small for a puddle.
        /// </summary>
        private float MeasureSpace(Vector3 point)
        {
            PuddleConfig config = _settings.ToPuddleConfig();
            int mask = _settings.SurfaceMask;
            float space = config.MaxRadius;
            Vector3 lifted = point + Vector3.up * 0.1f;

            for (int i = 0; i < SpaceDirections && space >= config.MinRadius; i++)
            {
                float angle = i * Mathf.PI * 2f / SpaceDirections;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                if (Physics.Raycast(lifted, direction, out RaycastHit wall, space, mask, QueryTriggerInteraction.Ignore))
                    space = Mathf.Min(space, wall.distance);

                for (float r = SpaceStep; r <= space; r += SpaceStep)
                {
                    Vector3 probe = point + direction * r + Vector3.up * 0.25f;
                    bool floor = Physics.Raycast(probe, Vector3.down, out RaycastHit below, 0.5f, mask, QueryTriggerInteraction.Ignore)
                        && Mathf.Abs(below.point.y - point.y) <= EdgeHeight;
                    if (!floor)
                    {
                        space = r - SpaceStep;
                        break;
                    }
                }
            }

            return space;
        }

        private int CastDown(Vector3 point, Vector3 normal)
        {
            Vector3 from = point + normal * RayLift + Vector3.up * RayLift;
            return Physics.RaycastNonAlloc(from, Vector3.down, s_floorHits, _settings.MaxDropToFloor + RayLift * 2f,
                _settings.SurfaceMask, QueryTriggerInteraction.Ignore);
        }

        private static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = s_floorHits[i];
                int j = i - 1;
                while (j >= 0 && s_floorHits[j].distance > hit.distance)
                {
                    s_floorHits[j + 1] = s_floorHits[j];
                    j--;
                }

                s_floorHits[j + 1] = hit;
            }
        }

        /// <summary>Moving bodies (barrels, items) and characters are not floors.</summary>
        private static bool IsStaticSurface(Collider collider)
        {
            return collider != null && collider.attachedRigidbody == null && !(collider is CharacterController);
        }

        /// <summary>A lit gasoline stream is a flame: it burns, it leaves no puddle.</summary>
        private bool IsBurning(WaterStream stream)
        {
            if (!_flames.TryGetValue(stream, out FuelStreamIgnition flame))
            {
                flame = stream.GetComponentInParent<FuelStreamIgnition>();
                if (flame == null)
                    flame = stream.GetComponentInChildren<FuelStreamIgnition>();
                _flames[stream] = flame;
            }

            return flame != null && flame.IsLit;
        }

        /// <summary>
        /// Where liquid hitting the point ends up: the floor right there, or the floor below
        /// (liquid runs down walls and off objects). Moving bodies (barrels, items), characters and triggers are not floors.
        /// </summary>
        private bool TryFindFloor(Vector3 point, Vector3 normal, out Vector3 floor)
        {
            Vector3 from = point + normal * RayLift + Vector3.up * RayLift;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, s_floorHits, _settings.MaxDropToFloor + RayLift * 2f,
                _settings.SurfaceMask, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            floor = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_floorHits[i];
                if (hit.distance >= best || hit.distance <= 0f || hit.collider.attachedRigidbody != null
                    || hit.collider is CharacterController)
                    continue;

                best = hit.distance;
                found = hit.normal.y >= _settings.FloorMinUp;
                floor = hit.point;
            }

            return found;
        }

        private void OnPuddleCreated(PuddleModel puddle)
        {
            if (_puddlePrefab == null)
                return;

            PuddleView view = Instantiate(_puddlePrefab, puddle.Center + Vector3.up * _settings.SurfaceOffset,
                Quaternion.identity, transform);
            view.Bind(puddle);
            _puddleViews[puddle] = view;
        }

        private void OnPuddleRemoved(PuddleModel puddle)
        {
            if (_puddleViews.TryGetValue(puddle, out PuddleView view))
            {
                _puddleViews.Remove(puddle);
                if (view != null)
                    view.Sink();
            }
        }

        private void OnDriftCreated(SnowdriftModel drift)
        {
            if (_snowdriftPrefab == null)
                return;

            SnowdriftView view = Instantiate(_snowdriftPrefab, drift.Center + Vector3.up * _settings.SurfaceOffset,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            view.Bind(drift);
            _driftViews[drift] = view;
        }

        private void OnDriftRemoved(SnowdriftModel drift)
        {
            if (_driftViews.TryGetValue(drift, out SnowdriftView view))
            {
                _driftViews.Remove(drift);
                if (view != null)
                    Destroy(view.gameObject);
            }
        }
    }
}
