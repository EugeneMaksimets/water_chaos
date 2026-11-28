using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Flamethrower look, driven only by <see cref="FuelStreamIgnition.LitChanged"/>:
    /// while the stream is lit the liquid look is hidden and copies of the existing fire prefab burn along the arc,
    /// following the nozzle; when it goes out the flames hide and the liquid look returns.
    /// The copies are made once, with every gameplay part removed (no colliders, spreading or extinguishing),
    /// so they are look only. Never decides whether anything ignites.
    /// </summary>
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the flames follow this frame's arc
    public sealed class FlameStreamView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private WaterStream _stream;
        [SerializeField] private FuelStreamIgnition _ignition;

        [Header("Flames")]
        [Tooltip("Existing fire prefab reused as the flame look (e.g. Cartoon Fire); its gameplay parts are removed from the copies.")]
        [SerializeField] private GameObject _firePrefab;

        [Tooltip("Fire copies spread along the stream.")]
        [SerializeField, Range(1, 8)] private int _flameCount = 4;

        [Tooltip("World scale of the flame nearest the nozzle (x) and at the stream end (y).")]
        [SerializeField] private Vector2 _scale = new Vector2(0.3f, 1f);

        [Header("Liquid Look")]
        [Tooltip("Views of the liquid stream (tube, spray); disabled while the stream burns.")]
        [SerializeField] private Behaviour[] _liquidViews = new Behaviour[0];

        private GameObject _flameRoot;
        private Transform[] _flames;
        private bool _flamesShown;

        // Switched off while burning: the liquid stays hidden until the left-over flow has run out.
        private bool _holdLiquidHidden;

        private void Awake()
        {
            if (_stream == null || _ignition == null || _firePrefab == null)
            {
                Debug.LogError($"{name}: FlameStreamView needs Stream, Ignition and Fire Prefab references.", this);
                enabled = false;
                return;
            }

            CreateFlames();
        }

        private void OnEnable()
        {
            if (_flames == null)
                return;

            _ignition.LitChanged += OnLitChanged;
            Apply(_ignition.IsLit);
        }

        private void OnDisable()
        {
            if (_flames == null)
                return;

            _ignition.LitChanged -= OnLitChanged;
            _holdLiquidHidden = false;
            Apply(false);
        }

        private void OnDestroy()
        {
            if (_flameRoot != null)
                Destroy(_flameRoot);
        }

        private void LateUpdate()
        {
            if (_holdLiquidHidden && (_stream.VisiblePointCount < 2 || IsSourceActive()))
            {
                _holdLiquidHidden = false;
                Apply(_ignition.IsLit);
            }

            if (_flamesShown)
                PlaceFlames();
        }

        private void OnLitChanged(bool lit)
        {
            if (!lit)
                _holdLiquidHidden = !IsSourceActive();

            Apply(lit);
        }

        /// <summary>Exactly one look at a time: flames while lit, the liquid otherwise.</summary>
        private void Apply(bool lit)
        {
            _flamesShown = lit;
            if (_flameRoot == null)
                return;

            if (_flameRoot.activeSelf != lit)
                _flameRoot.SetActive(lit);
            if (lit)
                PlaceFlames();

            bool liquidVisible = !lit && !_holdLiquidHidden;
            foreach (Behaviour view in _liquidViews)
            {
                if (view != null && view.enabled != liquidVisible)
                    view.enabled = liquidVisible;
            }
        }

        // A source destroyed with the scene (leaving Play Mode) counts as off.
        private bool IsSourceActive()
        {
            if (_stream == null || (_stream.Source is Object source && source == null))
                return false;

            return !(_stream.Source is IWaterSourceControl control) || control.IsActive;
        }

        /// <summary>Spreads the flames along the current arc, each pointing along the stream, bigger toward the end.</summary>
        private void PlaceFlames()
        {
            Vector3[] points = _stream.Points;
            int count = _stream.VisiblePointCount;
            if (points == null || count < 2)
                return;

            float length = 0f;
            for (int i = 0; i < count - 1; i++)
                length += Vector3.Distance(points[i], points[i + 1]);

            float parentScale = Mathf.Max(1e-4f, transform.lossyScale.x);
            for (int i = 0; i < _flames.Length; i++)
            {
                float share = (i + 0.5f) / _flames.Length;
                GetPointAlong(points, count, share * length, out Vector3 position, out Vector3 direction);

                Transform flame = _flames[i];
                flame.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, direction));
                flame.localScale = Vector3.one * (Mathf.Lerp(_scale.x, _scale.y, share) / parentScale);
            }
        }

        private static void GetPointAlong(Vector3[] points, int count, float distance, out Vector3 position, out Vector3 direction)
        {
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[i + 1];
                float segment = Vector3.Distance(from, to);
                if (distance > segment && i < count - 2)
                {
                    distance -= segment;
                    continue;
                }

                direction = segment > 0f ? (to - from) / segment : Vector3.up;
                position = Vector3.Lerp(from, to, segment > 0f ? Mathf.Clamp01(distance / segment) : 0f);
                return;
            }

            position = points[0];
            direction = Vector3.up;
        }

        /// <summary>
        /// Copies the fire prefab under an inactive root, so none of its scripts wake up,
        /// and strips the gameplay parts before it is ever shown. Made once; only shown and hidden afterwards.
        /// </summary>
        private void CreateFlames()
        {
            _flameRoot = new GameObject("Flamethrower Flames");
            _flameRoot.SetActive(false);
            _flameRoot.transform.SetParent(transform, false);

            _flames = new Transform[_flameCount];
            for (int i = 0; i < _flameCount; i++)
            {
                GameObject flame = Instantiate(_firePrefab, _flameRoot.transform);
                flame.name = "Flame " + (i + 1);
                StripGameplay(flame);
                _flames[i] = flame.transform;
            }
        }

        // Dependents first: FireSpreader, FireFuelHandler and the toxic reaction require FireExtinguishHandler,
        // and the reaction's view requires the reaction.
        private static void StripGameplay(GameObject flame)
        {
            DestroyAll<WaterChaos.ToxicReaction.FireToxicReactionView>(flame);
            DestroyAll<WaterChaos.ToxicReaction.FireToxicReaction>(flame);
            DestroyAll<FireSpreader>(flame);
            DestroyAll<FireSteamEffect>(flame);
            DestroyAll<WaterChaos.Sounds.FireSound>(flame);
            DestroyAll<FireFuelHandler>(flame);
            DestroyAll<FireView>(flame);
            DestroyAll<FireExtinguishHandler>(flame);
            DestroyAll<Collider>(flame);
            DestroyAll<Rigidbody>(flame);
        }

        private static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
                DestroyImmediate(component);
        }
    }
}
