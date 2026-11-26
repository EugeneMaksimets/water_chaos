using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Shows how far a <see cref="CorrodibleObject"/> is eaten away: its colors stain toward a corroded tint,
    /// fumes rise from the last hit while the surface is wet, and close to destruction the stain pulses toxic green.
    /// Display only: reads the target's state, never changes it. Cleans up after itself when the object is destroyed.
    /// </summary>
    [RequireComponent(typeof(CorrodibleObject))]
    public sealed class CorrosionView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Stain")]
        [Tooltip("Color the surface turns toward as it corrodes.")]
        [SerializeField] private Color _corrodedColor = new Color(0.32f, 0.36f, 0.12f, 1f);

        [Tooltip("0..1: how far the surface is stained at full corrosion. 0 = no stain.")]
        [SerializeField, Range(0f, 1f)] private float _visualIntensity = 0.8f;

        [Header("Warning")]
        [Tooltip("Progress (0..1) from which the stain pulses, warning that the object is about to break.")]
        [SerializeField, Range(0f, 1f)] private float _warningProgress = 0.75f;

        [Tooltip("Color of the warning pulse.")]
        [SerializeField] private Color _warningColor = new Color(0.55f, 1f, 0.1f, 1f);

        [Tooltip("Warning pulses per second (Hz).")]
        [SerializeField, Min(0f)] private float _warningFrequency = 2.5f;

        [Header("Fumes")]
        [Tooltip("Optional looping fumes effect; one copy is made on the first hit and follows the last hit point while the surface is wet.")]
        [SerializeField] private ParticleSystem _fumesPrefab;

        private CorrodibleObject _target;
        private MaterialPropertyBlock _block;
        private Color[][] _baseColors;
        private ParticleSystem _fumes;
        private float _shownProgress = -1f;
        private bool _pulsing;

        private void Awake()
        {
            _target = GetComponent<CorrodibleObject>();
            _block = new MaterialPropertyBlock();
            CacheBaseColors();
        }

        private void OnEnable()
        {
            _target.Destroyed += OnDestroyed;
        }

        private void OnDisable()
        {
            _target.Destroyed -= OnDestroyed;
            StopFumes(true);
        }

        private void OnDestroy()
        {
            if (_fumes != null)
                Destroy(_fumes.gameObject);
        }

        private void LateUpdate()
        {
            if (_target.IsDestroyed)
                return;

            UpdateFumes();

            float progress = _target.Progress;
            bool pulsing = progress > 0f && progress >= _warningProgress;
            if (!pulsing && !_pulsing && Mathf.Approximately(progress, _shownProgress))
                return;

            _pulsing = pulsing;
            _shownProgress = progress;
            float stain = progress * _visualIntensity;
            float pulse = pulsing ? (Mathf.Sin(Time.time * _warningFrequency * 2f * Mathf.PI) * 0.5f + 0.5f) * 0.6f : 0f;
            ApplyColors(stain, pulse);
        }

        private void UpdateFumes()
        {
            bool wet = _target.IsWet;
            if (wet && _fumes == null && _fumesPrefab != null)
                _fumes = Instantiate(_fumesPrefab, transform);

            if (_fumes == null)
                return;

            if (!wet)
            {
                StopFumes(false);
                return;
            }

            _fumes.transform.SetPositionAndRotation(_target.LastHitPoint, Quaternion.LookRotation(_target.LastHitNormal));
            if (!_fumes.isPlaying)
                _fumes.Play();
        }

        private void StopFumes(bool clear)
        {
            if (_fumes == null || (!_fumes.isPlaying && !clear))
                return;

            _fumes.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDestroyed(CorrodibleObject target)
        {
            StopFumes(true);
            foreach (Renderer part in _target.Renderers)
            {
                if (part != null)
                    part.SetPropertyBlock(null);
            }

            enabled = false;
        }

        private void CacheBaseColors()
        {
            Renderer[] renderers = _target.Renderers;
            _baseColors = new Color[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] materials = renderers[r] != null ? renderers[r].sharedMaterials : new Material[0];
                _baseColors[r] = new Color[materials.Length];
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    _baseColors[r][m] = material != null && material.HasProperty(BaseColorId)
                        ? material.GetColor(BaseColorId)
                        : Color.white;
                }
            }
        }

        // Per material slot, so a wall with two paint colors keeps both, each stained toward the same tint.
        private void ApplyColors(float stain, float pulse)
        {
            Renderer[] renderers = _target.Renderers;
            for (int r = 0; r < renderers.Length && r < _baseColors.Length; r++)
            {
                Renderer part = renderers[r];
                if (part == null)
                    continue;

                for (int m = 0; m < _baseColors[r].Length; m++)
                {
                    Color stained = Color.Lerp(_baseColors[r][m], _corrodedColor, stain);
                    Color shown = Color.Lerp(stained, _warningColor, pulse);
                    shown.a = _baseColors[r][m].a;

                    part.GetPropertyBlock(_block, m);
                    _block.SetColor(BaseColorId, shown);
                    part.SetPropertyBlock(_block, m);
                }
            }
        }
    }
}
