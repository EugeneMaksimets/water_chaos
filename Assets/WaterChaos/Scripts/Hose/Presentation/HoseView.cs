using UnityEngine;
using UnityEngine.Rendering;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Draws the hose path with a LineRenderer. Only reads the hose; owns no gameplay rules.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(HoseController.ExecutionOrder + 1)]
    public sealed class HoseView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HoseController _hose;

        [Header("Look")]
        [Tooltip("Hose diameter, in meters (m).")]
        [SerializeField, Min(0.005f)] private float _width = 0.05f;

        [Tooltip("Rubber material, e.g. URP Lit, dark and matte. Empty keeps the LineRenderer's material.")]
        [SerializeField] private Material _material;

        [Tooltip("Extra vertices at bends and ends, so bends look rounded instead of sharp.")]
        [SerializeField, Range(0, 16)] private int _roundness = 6;

        private LineRenderer _line;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();

            if (_hose == null)
            {
                Debug.LogError($"{name}: HoseView needs a Hose reference.", this);
                _line.enabled = false;
                enabled = false;
                return;
            }

            ApplyLook();
            _line.enabled = false;
        }

        private void LateUpdate()
        {
            HoseModel model = _hose.Model;
            bool visible = _hose.IsConnected && model != null;
            if (_line.enabled != visible)
                _line.enabled = visible;

            if (!visible)
                return;

            int count = model.PointCount;
            if (_line.positionCount != count)
                _line.positionCount = count;

            for (int i = 0; i < count; i++)
                _line.SetPosition(i, model.GetPoint(i));
        }

        private void ApplyLook()
        {
            _line.useWorldSpace = true;
            _line.widthMultiplier = _width;
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.numCornerVertices = _roundness;
            _line.numCapVertices = _roundness;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Tile;
            _line.shadowCastingMode = ShadowCastingMode.On;

            if (_material != null)
                _line.sharedMaterial = _material;
        }
    }
}
