using UnityEngine;
using UnityEngine.Rendering;
using WaterChaos.Hose;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Draws the transfer hose with a LineRenderer, lying on the ground between its path points,
    /// and shows the water moving inside it (same shader properties as the fire hose).
    /// Look only: reads the hose and the transfer, moves no water.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(TransferHose.ExecutionOrder + 20)]
    public sealed class TransferHoseView : MonoBehaviour
    {
        private const float SwellHeadroom = 0.2f;
        private const float OffsetWrap = 1000f;

        private static readonly int WidthId = Shader.PropertyToID("_HoseWidth");
        private static readonly int HeadroomId = Shader.PropertyToID("_HoseHeadroom");
        private static readonly int FlowOffsetId = Shader.PropertyToID("_HoseFlowOffset");
        private static readonly int FullnessId = Shader.PropertyToID("_HoseFullness");

        [Header("References")]
        [SerializeField] private TransferHose _hose;
        [SerializeField] private WaterTransferController _transfer;

        [Header("Look")]
        [Tooltip("Hose diameter, in meters (m).")]
        [SerializeField, Range(0.02f, 0.25f)] private float _width = 0.1f;

        [Tooltip("Fire hose material (WaterChaos/FireHose shader). Empty keeps the LineRenderer's material.")]
        [SerializeField] private Material _material;

        [Tooltip("Extra vertices at bends and ends.")]
        [SerializeField, Range(0, 16)] private int _roundness = 4;

        [Header("Ground")]
        [Tooltip("Layers the hose rests on. Leave out Water and Player.")]
        [SerializeField] private LayerMask _groundMask = 1;

        [Tooltip("Distance between ground samples, in meters (m).")]
        [SerializeField, Range(0.1f, 2f)] private float _sampleSpacing = 0.4f;

        [Tooltip("How far below the straight path the hose may sag to reach the ground, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxSag = 2.5f;

        [Header("Water Inside")]
        [Tooltip("Pressure at which the hose looks fully charged, in pascals (Pa).")]
        [SerializeField, Min(1f)] private float _fullPressure = 200_000f;

        [Tooltip("Time for the water effect to fade in or out, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.4f;

        private readonly HoseGroundDrape _drape = new HoseGroundDrape();
        private LineRenderer _line;
        private MaterialPropertyBlock _block;
        private float _flowOffset;
        private float _shownFullness;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _block = new MaterialPropertyBlock();

            if (_hose == null || _transfer == null)
            {
                Debug.LogError($"{name}: TransferHoseView needs Hose and Transfer references.", this);
                _line.enabled = false;
                enabled = false;
                return;
            }

            ApplyLook();
            _line.enabled = false;
        }

        private void LateUpdate()
        {
            HoseModel path = _hose.Path;
            bool visible = _hose.Model.IsConnected && path != null;
            if (_line.enabled != visible)
                _line.enabled = visible;

            if (!visible)
            {
                _shownFullness = 0f;
                return;
            }

            int count = _drape.Build(path, _groundMask, _sampleSpacing, _maxSag, _width * 0.5f);
            if (_line.positionCount != count)
                _line.positionCount = count;
            _line.SetPositions(_drape.Points);

            UpdateWater(Time.deltaTime);
        }

        private void UpdateWater(float deltaTime)
        {
            PumpFlow(out WaterFlowData flow);

            float speed = HoseWaterVisual.WaterSpeed(flow, _width);
            float targetFullness = HoseWaterVisual.Fullness(flow, _fullPressure);

            _flowOffset = Mathf.Repeat(_flowOffset + speed * deltaTime, OffsetWrap);
            _shownFullness = Mathf.MoveTowards(_shownFullness, targetFullness, deltaTime / _fadeTime);

            _line.GetPropertyBlock(_block);
            _block.SetFloat(WidthId, _width);
            _block.SetFloat(HeadroomId, SwellHeadroom);
            _block.SetFloat(FlowOffsetId, _flowOffset);
            _block.SetFloat(FullnessId, _shownFullness);
            _line.SetPropertyBlock(_block);
        }

        /// <summary>Pump output while the hose takes it; no flow otherwise.</summary>
        private void PumpFlow(out WaterFlowData flow)
        {
            flow = _transfer.IsDelivering && _transfer.Source != null ? _transfer.Source.Output : default;
        }

        private void ApplyLook()
        {
            _line.useWorldSpace = true;
            _line.widthMultiplier = _width * (1f + SwellHeadroom);
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.numCornerVertices = _roundness;
            _line.numCapVertices = _roundness;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Tile;
            _line.textureScale = Vector2.one;
            _line.shadowCastingMode = ShadowCastingMode.On;

            if (_material != null)
                _line.sharedMaterial = _material;
        }
    }
}
