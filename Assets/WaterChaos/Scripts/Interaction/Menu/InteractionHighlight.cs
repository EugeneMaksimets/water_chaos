using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Tints the interactive object under the crosshair slightly towards white.
    /// Uses per-renderer property blocks on the material's base colour, so no material is copied or changed;
    /// the object looks as before as soon as the aim moves on. Renderers that already use their own
    /// property blocks (fire, hoses) and effect renderers are left alone.
    /// </summary>
    public sealed class InteractionHighlight
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly float _amount;
        private readonly List<Renderer> _tinted = new List<Renderer>();
        private readonly List<Renderer> _found = new List<Renderer>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Transform _current;

        /// <summary>Object highlighted now; null when none.</summary>
        public Transform Current => _current;

        /// <param name="amount">How far colours move towards white: 0 = no highlight, 1 = white.</param>
        public InteractionHighlight(float amount)
        {
            _amount = amount;
        }

        /// <summary>Colour moved towards white by <paramref name="amount"/> (clamped to 0..1); alpha is kept.</summary>
        public static Color Tint(Color color, float amount)
        {
            float t = float.IsNaN(amount) ? 0f : Mathf.Clamp01(amount);
            Color tinted = Color.Lerp(color, Color.white, t);
            tinted.a = color.a;
            return tinted;
        }

        /// <summary>
        /// Highlights <paramref name="target"/>, or the nearest parent with visible parts up to
        /// <paramref name="focus"/> (e.g. a hose outlet without its own mesh lights up its truck). Null clears.
        /// </summary>
        public void Show(Transform target, Transform focus)
        {
            Transform shown = FindVisible(target, focus);
            if (shown == _current)
                return;

            Clear();
            _current = shown;
            if (shown == null || _amount <= 0f)
                return;

            shown.GetComponentsInChildren(_found);
            foreach (Renderer renderer in _found)
            {
                if (!CanTint(renderer))
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty(BaseColor))
                        continue;

                    _block.Clear();
                    _block.SetColor(BaseColor, Tint(materials[i].GetColor(BaseColor), _amount));
                    renderer.SetPropertyBlock(_block, i);
                }

                _tinted.Add(renderer);
            }
        }

        public void Clear()
        {
            foreach (Renderer renderer in _tinted)
            {
                if (renderer == null)
                    continue;

                int count = renderer.sharedMaterials.Length;
                for (int i = 0; i < count; i++)
                    renderer.SetPropertyBlock(null, i);
            }

            _tinted.Clear();
            _current = null;
        }

        // A property block we set ourselves does not make a renderer "someone else's":
        // without this check the highlighted object would drop out next frame and the highlight would jump
        // to its parent and back (blinking, and a whole truck lit up for one door).
        private bool CanTint(Renderer renderer)
        {
            return renderer.enabled
                && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                && (!renderer.HasPropertyBlock() || _tinted.Contains(renderer));
        }

        private Transform FindVisible(Transform target, Transform focus)
        {
            for (Transform t = target; t != null; t = t.parent)
            {
                t.GetComponentsInChildren(_found);
                foreach (Renderer renderer in _found)
                {
                    if (CanTint(renderer))
                        return t;
                }

                if (t == focus)
                    break;
            }

            return null;
        }
    }
}
