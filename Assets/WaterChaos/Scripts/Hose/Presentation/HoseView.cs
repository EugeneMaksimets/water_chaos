using UnityEngine;
using UnityEngine.Rendering;
using WaterChaos.Water;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Draws the hose path with a LineRenderer and shows the water inside it.
    /// Only reads the hose and its water source; owns no gameplay rules and moves no water.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(HoseController.ExecutionOrder + 1)]
    public sealed class HoseView : MonoBehaviour
    {
        /// <summary>Extra ribbon width the shader may use to swell the hose when it is charged (fraction of the width).</summary>
        private const float SwellHeadroom = 0.2f;

        /// <summary>Scroll offset wraps here, in meters (m), so it never loses float precision.</summary>
        private const float OffsetWrap = 1000f;

        private static readonly int WidthId = Shader.PropertyToID("_HoseWidth");
        private static readonly int HeadroomId = Shader.PropertyToID("_HoseHeadroom");
        private static readonly int FlowOffsetId = Shader.PropertyToID("_HoseFlowOffset");
        private static readonly int FullnessId = Shader.PropertyToID("_HoseFullness");

        [Header("References")]
        [SerializeField] private HoseController _hose;

        [Header("Look")]
        [Tooltip("Hose diameter, in meters (m). Applies along the whole hose, also during Play Mode.")]
        [SerializeField, Range(0.02f, 0.15f)] private float _width = 0.05f;

        [Tooltip("Fire hose material (WaterChaos/FireHose shader). Empty keeps the LineRenderer's material.")]
        [SerializeField] private Material _material;

        [Tooltip("Extra vertices at bends and ends, so bends look rounded instead of sharp.")]
        [SerializeField, Range(0, 16)] private int _roundness = 6;

        [Header("Water Inside")]
        [Tooltip("Show water moving inside the hose while the source gives flow.")]
        [SerializeField] private bool _showWater = true;

        [Tooltip("Multiplies the real water speed for the moving pulses. 1 = physical speed.")]
        [SerializeField, Range(0f, 3f)] private float _speedMultiplier = 1f;

        [Tooltip("How strong the water effect looks (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;

        [Tooltip("Pressure at which the hose looks fully charged, in pascals (Pa).")]
        [SerializeField, Min(1f)] private float _fullPressure = 200_000f;

        [Tooltip("Time for the water effect to fade in or out, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.4f;

        private LineRenderer _line;
        private MaterialPropertyBlock _block;
        private float _flowOffset;
        private float _shownFullness;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _block = new MaterialPropertyBlock();

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

        private void OnValidate()
        {
            // Lets the width and roundness be tuned live in Play Mode.
            if (_line != null)
                ApplyLook();
        }

        private void LateUpdate()
        {
            HoseModel model = _hose.Model;
            bool visible = _hose.IsConnected && model != null;
            if (_line.enabled != visible)
                _line.enabled = visible;

            if (!visible)
            {
                _shownFullness = 0f;
                return;
            }

            int count = model.PointCount;
            if (_line.positionCount != count)
                _line.positionCount = count;

            for (int i = 0; i < count; i++)
                _line.SetPosition(i, model.GetPoint(i));

            UpdateWater(Time.deltaTime);
        }

        private void UpdateWater(float deltaTime)
        {
            IWaterFlowSource source = _hose.Source;
            WaterFlowData flow = _showWater && source != null ? source.Output : default;

            float speed = HoseWaterVisual.WaterSpeed(flow, _width) * _speedMultiplier;
            float targetFullness = HoseWaterVisual.Fullness(flow, _fullPressure) * _intensity;

            _flowOffset = Mathf.Repeat(_flowOffset + speed * deltaTime, OffsetWrap);
            _shownFullness = Mathf.MoveTowards(_shownFullness, targetFullness, deltaTime / _fadeTime);

            _line.GetPropertyBlock(_block);
            _block.SetFloat(WidthId, _width);
            _block.SetFloat(HeadroomId, SwellHeadroom);
            _block.SetFloat(FlowOffsetId, _flowOffset);
            _block.SetFloat(FullnessId, _shownFullness);
            _line.SetPropertyBlock(_block);
        }

        private void ApplyLook()
        {
            _line.useWorldSpace = true;
            // The ribbon is a bit wider than the hose; the shader cuts it back to the real width
            // and uses the spare room to swell the hose when water flows.
            _line.widthMultiplier = _width * (1f + SwellHeadroom);
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.numCornerVertices = _roundness;
            _line.numCapVertices = _roundness;
            _line.alignment = LineAlignment.View;
            // Tile: U = distance along the hose in meters, from the source (0) to the nozzle.
            _line.textureMode = LineTextureMode.Tile;
            _line.textureScale = Vector2.one;
            _line.shadowCastingMode = ShadowCastingMode.On;

            if (_material != null)
                _line.sharedMaterial = _material;
        }
    }
}
