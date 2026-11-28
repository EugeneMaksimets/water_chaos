using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Shows how far a <see cref="CorrodibleObject"/> is eaten away: its colors stain toward a corroded tint,
    /// fumes rise from the last hit while the surface is wet, and toxic drips run down over the whole object,
    /// more of them the deeper it is eaten; the drips vanish when the object is destroyed.
    /// Display only: reads the target's state, never changes it. Cleans up after itself when the object is destroyed.
    /// </summary>
    [RequireComponent(typeof(CorrodibleObject))]
    public sealed class CorrosionView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        [Header("Stain")]
        [Tooltip("Color the surface turns toward as it corrodes.")]
        [SerializeField] private Color _corrodedColor = new Color(0.32f, 0.36f, 0.12f, 1f);

        [Tooltip("0..1: how far the surface is stained at full corrosion. 0 = no stain.")]
        [SerializeField, Range(0f, 1f)] private float _visualIntensity = 0.8f;

        [Header("Drips")]
        [Tooltip("Particle material of the toxic drips (same as the hose drips). Empty = no drips.")]
        [SerializeField] private Material _dripMaterial;

        [Tooltip("Color of the drips.")]
        [SerializeField] private Color _dripColor = LiquidStreamTint.ToxicStreamColor;

        [Tooltip("Drop size range, in meters (m).")]
        [SerializeField] private Vector2 _dripSize = new Vector2(0.04f, 0.09f);

        [Tooltip("Drips per second per square meter of the object's face (1/(s*m2)) at full corrosion; scales with progress.")]
        [SerializeField, Min(0f)] private float _maxDripsPerSquareMeter = 4f;

        [Tooltip("Upper limit of drips per second (1/s) for one part of the object, so big walls stay cheap.")]
        [SerializeField, Min(0f)] private float _maxDripsPerPart = 60f;

        [Header("Fumes")]
        [Tooltip("Optional looping fumes effect; one copy is made on the first hit and follows the last hit point while the surface is wet.")]
        [SerializeField] private ParticleSystem _fumesPrefab;

        private CorrodibleObject _target;
        private MaterialPropertyBlock _block;
        private Color[][] _baseColors;
        private ParticleSystem _fumes;
        // One emitter per mesh part of the object, with that part's face area (m2).
        private readonly List<ParticleSystem> _drips = new List<ParticleSystem>();
        private readonly List<float> _dripAreas = new List<float>();
        private float _shownProgress = -1f;

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
            StopDrips();
        }

        private void OnDestroy()
        {
            if (_fumes != null)
                Destroy(_fumes.gameObject);

            foreach (ParticleSystem drips in _drips)
            {
                if (drips != null)
                    Destroy(drips.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_target.IsDestroyed)
                return;

            UpdateFumes();

            float progress = _target.Progress;
            if (Mathf.Approximately(progress, _shownProgress))
                return;

            _shownProgress = progress;
            ApplyColors(progress * _visualIntensity);
            UpdateDrips(progress);
        }

        // Called only when progress changes: more damage = more drips, spread over the whole object.
        private void UpdateDrips(float progress)
        {
            if (progress <= 0f || _dripMaterial == null)
                return;

            if (_drips.Count == 0)
                CreateDrips();

            for (int i = 0; i < _drips.Count; i++)
            {
                ParticleSystem drips = _drips[i];
                ParticleSystem.EmissionModule emission = drips.emission;
                emission.rateOverTime = Mathf.Min(_maxDripsPerPart, progress * _maxDripsPerSquareMeter * _dripAreas[i]);

                if (!drips.isPlaying)
                    drips.Play();
            }
        }

        private void CreateDrips()
        {
            foreach (Renderer part in _target.Renderers)
            {
                if (part is MeshRenderer && part != null)
                    AddDrips(part);
            }
        }

        // Built in code like the hose drips: stretched green drops that run down and shrink.
        // Born on the shell of the part's own (rotated) bounds box, so they cover the whole part.
        private void AddDrips(Renderer part)
        {
            Bounds bounds = part.localBounds;
            Vector3 worldSize = Vector3.Scale(bounds.size, part.transform.lossyScale);

            GameObject go = new GameObject("ToxicDrips");
            go.transform.SetParent(part.transform, false);
            go.transform.localPosition = bounds.center;

            ParticleSystem drips = go.AddComponent<ParticleSystem>();
            drips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = drips.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(_dripSize.x, _dripSize.y);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            main.startColor = Color.white;

            ParticleSystem.EmissionModule emission = drips.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = drips.shape;
            shape.shapeType = ParticleSystemShapeType.BoxShell;
            shape.scale = bounds.size;

            ParticleSystem.SizeOverLifetimeModule size = drips.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _dripMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 2f;
            TintDrips(renderer);

            _drips.Add(drips);
            _dripAreas.Add(FaceArea(worldSize));
        }

        // The toon water shader colors the lit side (_BaseColor), the shadow side (_ShadeColor) and the rim (_RimColor),
        // so all three are tinted: otherwise the shadow side keeps the material's blue.
        private void TintDrips(ParticleSystemRenderer renderer)
        {
            LiquidStreamTint.Apply(renderer, _block, true, _dripColor);

            renderer.GetPropertyBlock(_block);
            Color shade = _dripColor * 0.6f;
            Color rim = Color.Lerp(_dripColor, Color.white, 0.5f);
            shade.a = rim.a = _dripColor.a;
            _block.SetColor(ShadeColorId, shade);
            _block.SetColor(RimColorId, rim);
            renderer.SetPropertyBlock(_block);
            _block.Clear();
        }

        // Area (m2) of the biggest face of a box: the two largest sides.
        private static float FaceArea(Vector3 size)
        {
            float x = Mathf.Abs(size.x), y = Mathf.Abs(size.y), z = Mathf.Abs(size.z);
            float smallest = Mathf.Min(x, Mathf.Min(y, z));
            return x * y * z > 0f ? x * y * z / smallest : Mathf.Max(x * y, Mathf.Max(y * z, x * z));
        }

        private void StopDrips()
        {
            foreach (ParticleSystem drips in _drips)
            {
                if (drips != null)
                    drips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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
            StopDrips();
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
        private void ApplyColors(float stain)
        {
            Renderer[] renderers = _target.Renderers;
            for (int r = 0; r < renderers.Length && r < _baseColors.Length; r++)
            {
                Renderer part = renderers[r];
                if (part == null)
                    continue;

                for (int m = 0; m < _baseColors[r].Length; m++)
                {
                    Color shown = Color.Lerp(_baseColors[r][m], _corrodedColor, stain);
                    shown.a = _baseColors[r][m].a;

                    part.GetPropertyBlock(_block, m);
                    _block.SetColor(BaseColorId, shown);
                    part.SetPropertyBlock(_block, m);
                }
            }
        }
    }
}
