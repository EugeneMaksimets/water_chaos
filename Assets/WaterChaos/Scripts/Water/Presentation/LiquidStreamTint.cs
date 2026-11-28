using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Colors the stream look by the liquid that flows (e.g. amber for gasoline), so the player sees what they spray.
    /// Liquids without an entry keep the renderers' own material color. Display only.
    /// The particles of a <see cref="WaterStreamParticleView"/> on the same object (splash, nozzle spray, start burst)
    /// are tinted too, keeping their material's transparency.
    /// </summary>
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate
    public sealed class LiquidStreamTint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        [Serializable]
        private struct LiquidColor
        {
            [SerializeField] private LiquidType _liquid;
            [SerializeField] private Color _color;

            public LiquidColor(LiquidType liquid, Color color)
            {
                _liquid = liquid;
                _color = color;
            }

            public LiquidType Liquid => _liquid;
            public Color Color => _color;
        }

        [Header("Source")]
        [SerializeField] private WaterStream _stream;

        [Header("Look")]
        [Tooltip("Renderers of the stream whose material color (_BaseColor) is tinted.")]
        [SerializeField] private Renderer[] _renderers = new Renderer[0];

        [Tooltip("Stream color per liquid; liquids not listed keep the material color.")]
        [SerializeField] private LiquidColor[] _colors =
        {
            new LiquidColor(LiquidType.Gasoline, GasolineStreamColor),
            new LiquidColor(LiquidType.Toxic, ToxicStreamColor),
        };

        /// <summary>Bright, slightly over-saturated green so the toxic stream reads as glowing.</summary>
        public static readonly Color ToxicStreamColor = new Color(0.5f, 1f, 0.12f, 1f);

        /// <summary>Amber of a gasoline stream.</summary>
        public static readonly Color GasolineStreamColor = new Color(1f, 0.7f, 0.15f, 1f);

        private readonly List<Renderer> _targets = new List<Renderer>();

        private MaterialPropertyBlock _block;
        private LiquidType _shown;
        private bool _applied;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_stream == null)
            {
                Debug.LogError($"{name}: LiquidStreamTint needs a Stream reference.", this);
                enabled = false;
                return;
            }

            _targets.AddRange(_renderers);
            WaterStreamParticleView particles = GetComponent<WaterStreamParticleView>();
            if (particles != null)
                _targets.AddRange(particles.Renderers);
        }

        /// <summary>Default stream color of a liquid; false for liquids that keep the material color (water).</summary>
        public static bool TryGetDefaultColor(LiquidType liquid, out Color color)
        {
            switch (liquid)
            {
                case LiquidType.Gasoline: color = GasolineStreamColor; return true;
                case LiquidType.Toxic: color = ToxicStreamColor; return true;
                default: color = default; return false;
            }
        }

        /// <summary>
        /// Sets the material color of <paramref name="target"/> through a property block, or clears it (tinted = false).
        /// Particles keep the alpha of their material, so drops stay see-through. Toon water materials also get their
        /// shadow side (_ShadeColor) and rim (_RimColor) tinted, or the shadow side would keep the water's blue.
        /// </summary>
        public static void Apply(Renderer target, MaterialPropertyBlock block, bool tinted, Color color)
        {
            if (!tinted)
            {
                target.SetPropertyBlock(null);
                return;
            }

            Material material = target.sharedMaterial;
            if (target is ParticleSystemRenderer && material != null && material.HasProperty(BaseColorId))
                color.a = material.GetColor(BaseColorId).a;

            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            if (material != null && material.HasProperty(ShadeColorId))
            {
                Color shade = color * 0.6f;
                shade.a = color.a;
                block.SetColor(ShadeColorId, shade);
            }

            if (material != null && material.HasProperty(RimColorId))
            {
                Color rim = Color.Lerp(color, Color.white, 0.5f);
                rim.a = color.a;
                block.SetColor(RimColorId, rim);
            }

            target.SetPropertyBlock(block);
        }

        private void LateUpdate()
        {
            LiquidType liquid = _stream.Flow.Liquid;
            if (_applied && liquid == _shown)
                return;

            _shown = liquid;
            _applied = true;
            bool tinted = TryGetColor(liquid, out Color color);

            foreach (Renderer target in _targets)
            {
                if (target != null)
                    Apply(target, _block, tinted, color);
            }
        }

        private bool TryGetColor(LiquidType liquid, out Color color)
        {
            foreach (LiquidColor entry in _colors)
            {
                if (entry.Liquid == liquid)
                {
                    color = entry.Color;
                    return true;
                }
            }

            color = default;
            return false;
        }
    }
}
