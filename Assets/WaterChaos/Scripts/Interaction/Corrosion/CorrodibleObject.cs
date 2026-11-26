using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Lets corrosive liquid eat this object away (rules in <see cref="CorrosionModel"/>, strength in <see cref="CorrosionProfile"/>).
    /// Lives on the object's collider, because the stream only notifies the collider it hits.
    /// Destroyed once: its colliders stop blocking (water, the player and the hose pass through) and its renderers hide;
    /// an optional effect is spawned and <see cref="Destroyed"/> is raised. Objects without this component never corrode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorrodibleObject : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Strength")]
        [Tooltip("Corrosion rules of this material (fence, interior wall, reinforced wall).")]
        [SerializeField] private CorrosionProfile _profile;

        [Header("Destruction")]
        [Tooltip("Colliders switched off when destroyed. Empty = all colliders on this object and its children.")]
        [SerializeField] private Collider[] _colliders = new Collider[0];

        [Tooltip("Renderers hidden when destroyed. Empty = all renderers on this object and its children.")]
        [SerializeField] private Renderer[] _renderers = new Renderer[0];

        [Tooltip("Optional one-shot effect (debris) spawned at the object's center when it is destroyed; it removes itself.")]
        [SerializeField] private GameObject _destroyEffect;

        private CorrosionModel _model;
        private bool _missingProfileReported;

        /// <summary>Raised whenever corrosion grows; the argument is <see cref="Progress"/>.</summary>
        public event Action<float> CorrosionChanged;

        /// <summary>Raised once, when the object is destroyed.</summary>
        public event Action<CorrodibleObject> Destroyed;

        /// <summary>0 = intact, 1 = destroyed.</summary>
        public float Progress => Model != null ? Model.Progress : 0f;

        public bool IsDestroyed => Model != null && Model.IsDestroyed;

        /// <summary>Corrosive liquid is still on the surface and keeps eating it.</summary>
        public bool IsWet => Model != null && Model.IsWet;

        /// <summary>Where corrosive liquid last hit, in world space.</summary>
        public Vector3 LastHitPoint { get; private set; }

        /// <summary>Surface normal at <see cref="LastHitPoint"/>.</summary>
        public Vector3 LastHitNormal { get; private set; } = Vector3.up;

        public Renderer[] Renderers
        {
            get
            {
                if (_renderers == null || _renderers.Length == 0)
                    _renderers = GetComponentsInChildren<Renderer>(true);
                return _renderers;
            }
        }

        private Collider[] Colliders
        {
            get
            {
                if (_colliders == null || _colliders.Length == 0)
                    _colliders = GetComponentsInChildren<Collider>(true);
                return _colliders;
            }
        }

        // Built on first use, so it also works before Awake (edit mode tests).
        private CorrosionModel Model
        {
            get
            {
                if (_model == null && _profile != null)
                    _model = _profile.CreateModel();
                return _model;
            }
        }

        private void Awake()
        {
            if (_profile == null)
                ReportMissingProfile();
        }

        private void FixedUpdate()
        {
            Advance(Time.fixedDeltaTime);
        }

        /// <summary>Lets a wet surface keep corroding for <paramref name="deltaTime"/> seconds (s). Called every physics step.</summary>
        public void Advance(float deltaTime)
        {
            if (!CanCorrode())
                return;

            float before = _model.Corrosion;
            bool destroyed = _model.Tick(deltaTime);
            Notify(before, destroyed);
        }

        // Called during FixedUpdate by WaterStream.
        public void ReceiveImpact(in WaterImpact impact)
        {
            if (!CanCorrode())
                return;

            float power = impact.Liquid.CorrosionPower();
            if (power > 0f)
            {
                LastHitPoint = impact.ContactPoint;
                LastHitNormal = impact.Normal.sqrMagnitude > 0f ? impact.Normal.normalized : Vector3.up;
            }

            float before = _model.Corrosion;
            bool destroyed = _model.ApplyLiquid(impact.Volume, power);
            Notify(before, destroyed);
        }

        private bool CanCorrode()
        {
            if (this == null || !enabled || !gameObject.activeInHierarchy)
                return false;

            if (Model == null)
            {
                ReportMissingProfile();
                return false;
            }

            return !_model.IsDestroyed;
        }

        private void Notify(float corrosionBefore, bool destroyed)
        {
            if (_model.Corrosion > corrosionBefore)
                CorrosionChanged?.Invoke(_model.Progress);

            if (destroyed)
                Break();
        }

        // Runs once: the model reports destruction a single time.
        private void Break()
        {
            Vector3 center = GetCenter();

            foreach (Collider part in Colliders)
            {
                if (part != null)
                    part.enabled = false;
            }

            foreach (Renderer part in Renderers)
            {
                if (part != null)
                    part.enabled = false;
            }

            if (_destroyEffect != null)
                Instantiate(_destroyEffect, center, transform.rotation);

            Destroyed?.Invoke(this);
        }

        private Vector3 GetCenter()
        {
            foreach (Renderer part in Renderers)
            {
                if (part != null)
                    return part.bounds.center;
            }

            return transform.position;
        }

        private void ReportMissingProfile()
        {
            if (_missingProfileReported)
                return;

            _missingProfileReported = true;
            Debug.LogError($"{name}: CorrodibleObject needs a Corrosion Profile; it will not corrode.", this);
        }
    }
}
