using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Draws the hose as a round 3D tube (<see cref="HoseTubeMesh"/>) and shows the water inside it.
    /// Its shape is either laid on the ground along the gameplay path (<see cref="HoseGroundDrape"/>) or simulated
    /// as a physical rope (<see cref="HoseVerletSimulation"/>) whose length follows the gameplay path.
    /// Only reads the hose and its water source; owns no gameplay rules and moves no water.
    /// </summary>
    [DefaultExecutionOrder(HoseController.ExecutionOrder + 1)]
    public sealed class HoseView : MonoBehaviour
    {

        /// <summary>Scroll offset wraps here, in meters (m), so it never loses float precision.</summary>
        private const float OffsetWrap = 1000f;

        private static readonly int WidthId = Shader.PropertyToID("_HoseWidth");
        private static readonly int FlowOffsetId = Shader.PropertyToID("_HoseFlowOffset");
        private static readonly int FullnessId = Shader.PropertyToID("_HoseFullness");

        [Header("References")]
        [SerializeField] private HoseController _hose;

        [Header("Look")]
        [Tooltip("Hose diameter, in meters (m). Applies along the whole hose, also during Play Mode.")]
        [SerializeField, Range(0.02f, 0.25f)] private float _width = 0.1f;

        [Tooltip("Fire hose material (WaterChaos/FireHose shader). Empty uses the old LineRenderer's material, if any.")]
        [SerializeField] private Material _material;

        [Tooltip("Vertices around the hose. 10-12 looks round up close.")]
        [SerializeField, Range(HoseTubeMesh.MinSides, 24)] private int _sides = 12;

        [Header("Shape")]
        [Tooltip("Drape = laid along the gameplay path; Physics = a simulated rope (more alive, a bit more expensive).")]
        [SerializeField] private HoseShapeMode _shape = HoseShapeMode.Drape;

        [Tooltip("Layers the hose rests on. Leave out Water and Player.")]
        [SerializeField] private LayerMask _groundMask = 1;

        [Tooltip("Distance between drawn points, in meters (m). Smaller = smoother bends.")]
        [SerializeField, Range(0.05f, 1f)] private float _pointSpacing = 0.15f;

        [Tooltip("How far below its path the hose may hang or drop to reach the ground, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxSag = 2.5f;

        [Tooltip("How far from a corner its rounding starts, in meters (m).")]
        [SerializeField, Range(0f, 1.5f)] private float _cornerRadius = 0.4f;

        [Tooltip("How sharply a hanging hose bends down under its weight, in 1/m. Lower = stiffer hose, wider arcs.")]
        [SerializeField, Range(0.3f, 6f)] private float _bendCurvature = 1.5f;

        [Header("Physics Shape")]
        [Tooltip("Used when Shape is Physics.")]
        [SerializeField] private HoseRope _rope = new HoseRope();

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

        private readonly HoseGroundDrape _drape = new HoseGroundDrape();
        private HoseTubeMesh _tube;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _flowOffset;
        private float _shownFullness;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _tube = new HoseTubeMesh();
            _renderer = _tube.CreateRenderer(transform, _material);

            if (_hose == null)
            {
                Debug.LogError($"{name}: HoseView needs a Hose reference.", this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_tube != null)
                Destroy(_tube.Mesh);
            _rope.Dispose();
        }

        private void FixedUpdate()
        {
            HoseModel model = _hose.Model;
            if (_shape != HoseShapeMode.Physics || !_hose.IsConnected || model == null)
            {
                _rope.Clear();
                return;
            }

            _rope.Step(transform, model, _hose.StartPosition, _hose.EndPosition, _groundMask, _hose.Ignores, _width * 0.5f);
        }

        private void LateUpdate()
        {
            HoseModel model = _hose.Model;
            bool visible = _hose.IsConnected && model != null;
            if (_renderer.enabled != visible)
                _renderer.enabled = visible;

            if (!visible)
            {
                _shownFullness = 0f;
                return;
            }

            float radius = _width * 0.5f;
            if (_shape == HoseShapeMode.Physics && _rope.IsLaid)
            {
                int count = _rope.BuildPoints(_hose.StartPosition, _hose.EndPosition, out Vector3[] points);
                _tube.Build(points, count, radius, _sides, _renderer.transform.worldToLocalMatrix);
            }
            else
            {
                int count = _drape.Build(model, _hose.StartPosition, _hose.EndPosition, _groundMask, _pointSpacing, _maxSag, radius, _cornerRadius, _bendCurvature);
                _tube.Build(_drape.Points, count, radius, _sides, _renderer.transform.worldToLocalMatrix);
            }

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

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(WidthId, _width);
            _block.SetFloat(FlowOffsetId, _flowOffset);
            _block.SetFloat(FullnessId, _shownFullness);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
