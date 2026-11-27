using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Draws one puddle: a flat disc sized by the puddle, with a material for its state
    /// (water, ice, toxic, gasoline) and bubbling fumes over toxic liquid. Display only, no colliders.
    /// </summary>
    public sealed class PuddleView : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("Scaled to the puddle in X and Z: holds a flat disc of diameter 1 m lying on the floor.")]
        [SerializeField] private Transform _disc;

        [Tooltip("Renderer of the disc; its material shows the puddle's state.")]
        [SerializeField] private Renderer _surface;

        [Tooltip("Optional: bubbles over a toxic puddle (shape radius follows the puddle).")]
        [SerializeField] private ParticleSystem _toxicBubbles;

        [Header("Materials")]
        [SerializeField] private Material _water;
        [SerializeField] private Material _ice;
        [SerializeField] private Material _toxic;
        [SerializeField] private Material _gasoline;

        [Header("Look")]
        [Tooltip("How fast the disc grows to the puddle's size, in meters per second (m/s).")]
        [SerializeField, Min(0.01f)] private float _growSpeed = 1.5f;

        private PuddleModel _model;
        private float _shownRadius;

        public void Bind(PuddleModel model)
        {
            _model = model;
            _model.Changed += OnChanged;
            _shownRadius = model.Radius * 0.5f;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            OnChanged(model);
            ApplySize();
        }

        private void OnDestroy()
        {
            if (_model != null)
                _model.Changed -= OnChanged;
        }

        private void Update()
        {
            if (_model == null || Mathf.Approximately(_shownRadius, _model.Radius))
                return;

            _shownRadius = Mathf.MoveTowards(_shownRadius, _model.Radius, _growSpeed * Time.deltaTime);
            ApplySize();
        }

        private void ApplySize()
        {
            if (_disc != null)
                _disc.localScale = new Vector3(_shownRadius * 2f, 1f, _shownRadius * 2f);

            if (_toxicBubbles != null)
            {
                ParticleSystem.ShapeModule shape = _toxicBubbles.shape;
                shape.radius = _shownRadius * 0.8f;
            }
        }

        private void OnChanged(PuddleModel model)
        {
            if (_surface != null)
            {
                Material material = MaterialFor(model);
                if (material != null && _surface.sharedMaterial != material)
                    _surface.sharedMaterial = material;
            }

            if (_toxicBubbles != null)
            {
                bool bubbling = model.Liquid.Corrodes() && !model.IsFrozen;
                if (bubbling && !_toxicBubbles.isPlaying)
                    _toxicBubbles.Play();
                else if (!bubbling && _toxicBubbles.isPlaying)
                    _toxicBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private Material MaterialFor(PuddleModel model)
        {
            if (model.IsFrozen)
                return _ice;

            switch (model.Liquid)
            {
                case LiquidType.Toxic: return _toxic;
                case LiquidType.Gasoline: return _gasoline;
                default: return _water;
            }
        }
    }
}
