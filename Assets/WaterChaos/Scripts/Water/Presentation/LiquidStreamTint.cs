using System;
using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Colors the stream look by the liquid that flows (e.g. amber for gasoline), so the player sees what they spray.
    /// Liquids without an entry keep the renderers' own material color. Display only.
    /// </summary>
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate
    public sealed class LiquidStreamTint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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
            new LiquidColor(LiquidType.Gasoline, new Color(1f, 0.7f, 0.15f, 1f)),
            new LiquidColor(LiquidType.Toxic, ToxicStreamColor),
        };

        /// <summary>Bright, slightly over-saturated green so the toxic stream reads as glowing.</summary>
        public static readonly Color ToxicStreamColor = new Color(0.5f, 1f, 0.12f, 1f);

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
            }
        }

        private void LateUpdate()
        {
            LiquidType liquid = _stream.Flow.Liquid;
            if (_applied && liquid == _shown)
                return;

            _shown = liquid;
            _applied = true;
            bool tinted = TryGetColor(liquid, out Color color);

            foreach (Renderer target in _renderers)
            {
                if (target == null)
                    continue;

                if (!tinted)
                {
                    target.SetPropertyBlock(null);
                    continue;
                }

                target.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                target.SetPropertyBlock(_block);
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
