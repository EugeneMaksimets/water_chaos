using System;
using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Colors the liquid look of a reservoir by what it holds (greenish for toxic liquid), from the first frame on,
    /// and follows the liquid when it changes (water turned toxic by a transfer, toxic neutralized back to water).
    /// Every color of the material (base, shade, rim, stripe) is multiplied by the tint, so shading and highlights stay.
    /// Liquids without an entry keep the material's own colors. Display only.
    /// </summary>
    public sealed class ReservoirLiquidTint : MonoBehaviour
    {
        // Color properties of the water materials (WaterToon, URP Lit); missing ones are skipped.
        private static readonly int[] ColorIds =
        {
            Shader.PropertyToID("_BaseColor"),
            Shader.PropertyToID("_ShadeColor"),
            Shader.PropertyToID("_RimColor"),
            Shader.PropertyToID("_StripeColor"),
        };

        [Serializable]
        private struct LiquidTint
        {
            [SerializeField] private LiquidType _liquid;
            [SerializeField] private Color _tint;

            public LiquidTint(LiquidType liquid, Color tint)
            {
                _liquid = liquid;
                _tint = tint;
            }

            public LiquidType Liquid => _liquid;
            public Color Tint => _tint;
        }

        [Header("Source")]
        [SerializeField] private WaterReservoirComponent _reservoir;

        [Header("Look")]
        [Tooltip("Renderers that show the liquid (water surface, tank fill).")]
        [SerializeField] private Renderer[] _renderers = new Renderer[0];

        [Tooltip("Multiplier of the material colors per liquid; liquids not listed keep the material colors.")]
        [SerializeField] private LiquidTint[] _tints =
        {
            new LiquidTint(LiquidType.Toxic, new Color(0.55f, 1f, 0.25f, 1f)),
        };

        private MaterialPropertyBlock _block;
        private LiquidType _shown;
        private bool _applied;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_reservoir == null)
            {
                Debug.LogError($"{name}: ReservoirLiquidTint needs a Reservoir reference.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            WaterReservoir reservoir = _reservoir.Reservoir;
            if (reservoir == null)
                return;

            LiquidType liquid = reservoir.Liquid;
            if (_applied && liquid == _shown)
                return;

            _shown = liquid;
            _applied = true;
            Apply(TryGetTint(liquid, out Color tint), tint);
        }

        private void Apply(bool tinted, Color tint)
        {
            foreach (Renderer target in _renderers)
            {
                if (target == null)
                    continue;

                Material[] materials = target.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    _block.Clear();
                    Material material = materials[i];
                    if (tinted && material != null)
                    {
                        foreach (int id in ColorIds)
                        {
                            if (!material.HasProperty(id))
                                continue;

                            Color original = material.GetColor(id);
                            Color shown = original * tint;
                            shown.a = original.a;
                            _block.SetColor(id, shown);
                        }
                    }

                    // An empty block removes the tint again.
                    target.SetPropertyBlock(_block, i);
                }
            }
        }

        private bool TryGetTint(LiquidType liquid, out Color tint)
        {
            foreach (LiquidTint entry in _tints)
            {
                if (entry.Liquid == liquid)
                {
                    tint = entry.Tint;
                    return true;
                }
            }

            tint = Color.white;
            return false;
        }
    }
}
