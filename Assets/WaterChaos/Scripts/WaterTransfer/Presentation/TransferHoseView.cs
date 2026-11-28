using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Draws the transfer hose as a round 3D tube, either lying on the ground between its path points or as a
    /// physical rope (<see cref="HoseRope"/>), and shows the water moving inside it (same shapes and shader as the fire hose).
    /// Look only: reads the hose and the transfer, moves no water.
    /// </summary>
    [DefaultExecutionOrder(TransferHose.ExecutionOrder + 20)]
    public sealed class TransferHoseView : MonoBehaviour
    {
        private const float OffsetWrap = 1000f;

        private static readonly int WidthId = Shader.PropertyToID("_HoseWidth");
        private static readonly int FlowOffsetId = Shader.PropertyToID("_HoseFlowOffset");
        private static readonly int FullnessId = Shader.PropertyToID("_HoseFullness");

        [Header("References")]
        [SerializeField] private TransferHose _hose;
        [SerializeField] private WaterTransferController _transfer;

        [Header("Look")]
        [Tooltip("Hose diameter, in meters (m).")]
        [SerializeField, Range(0.02f, 0.25f)] private float _width = 0.1f;

        [Tooltip("Fire hose material (WaterChaos/FireHose shader). Empty uses the old LineRenderer's material, if any.")]
        [SerializeField] private Material _material;

        [Tooltip("Vertices around the hose.")]
        [SerializeField, Range(HoseTubeMesh.MinSides, 24)] private int _sides = 10;

        [Header("Shape")]
        [Tooltip("Drape = laid along the gameplay path; Physics = a simulated rope that falls, slides and collides with fences and walls.")]
        [SerializeField] private HoseShapeMode _shape = HoseShapeMode.Drape;

        [Tooltip("Layers the hose rests on and (Physics) collides with. Leave out Water and Player.")]
        [SerializeField] private LayerMask _groundMask = 1;

        [Tooltip("Distance between drawn points, in meters (m). Smaller = smoother bends.")]
        [SerializeField, Range(0.05f, 1f)] private float _pointSpacing = 0.2f;

        [Tooltip("How far below its path the hose may hang or drop to reach the ground, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxSag = 2.5f;

        [Tooltip("How far from a corner its rounding starts, in meters (m).")]
        [SerializeField, Range(0f, 1.5f)] private float _cornerRadius = 0.4f;

        [Tooltip("How sharply a hanging hose bends down under its weight, in 1/m. Lower = stiffer hose, wider arcs.")]
        [SerializeField, Range(0.3f, 6f)] private float _bendCurvature = 1.5f;

        [Header("Physics Shape")]
        [Tooltip("Used when Shape is Physics.")]
        [SerializeField] private HoseRope _rope = new HoseRope();

        [Header("End Coupling")]
        [Tooltip("Optional coupling head (a child of the hose end); kept on the tube's last point, pointing along the tube.")]
        [SerializeField] private Transform _coupling;

        [Header("Water Inside")]
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

            if (_hose == null || _transfer == null)
            {
                Debug.LogError($"{name}: TransferHoseView needs Hose and Transfer references.", this);
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
            HoseModel path = _hose.Path;
            if (_shape != HoseShapeMode.Physics || path == null || !_hose.Model.IsConnected)
            {
                _rope.Clear();
                return;
            }

            _rope.Step(transform, path, path.Anchor, _hose.End.position, _groundMask, _hose.Ignores, _width * 0.5f);
        }

        private void LateUpdate()
        {
            HoseModel path = _hose.Path;
            bool visible = _hose.Model.IsConnected && path != null;
            if (_renderer.enabled != visible)
                _renderer.enabled = visible;

            if (!visible)
            {
                _shownFullness = 0f;
                return;
            }

            float radius = _width * 0.5f;
            Vector3[] points;
            int count;
            if (_shape == HoseShapeMode.Physics && _rope.IsLaid)
            {
                count = _rope.BuildPoints(out points);
            }
            else
            {
                // The path end runs low to find obstacles; the drawn hose ends at the real end (in the hands, on the ground).
                count = _drape.Build(path, path.Anchor, _hose.End.position, _groundMask, _pointSpacing, _maxSag, radius, _cornerRadius, _bendCurvature);
                points = _drape.Points;
            }

            _tube.Build(points, count, radius, _sides, _renderer.transform.worldToLocalMatrix);
            AlignCoupling(points, count);

            UpdateWater(Time.deltaTime);
        }

        // The end itself faces where the player looks (or the discharge direction); the coupling follows the tube instead.
        private void AlignCoupling(Vector3[] points, int count)
        {
            if (_coupling == null || count < 2)
                return;

            Vector3 end = points[count - 1];
            Vector3 direction = end - points[count - 2];
            if (direction.sqrMagnitude < 1e-8f)
                return;

            _coupling.SetPositionAndRotation(end, Quaternion.LookRotation(direction));
        }

        private void UpdateWater(float deltaTime)
        {
            PumpFlow(out WaterFlowData flow);

            float speed = HoseWaterVisual.WaterSpeed(flow, _width);
            float targetFullness = HoseWaterVisual.Fullness(flow, _fullPressure);

            _flowOffset = Mathf.Repeat(_flowOffset + speed * deltaTime, OffsetWrap);
            _shownFullness = Mathf.MoveTowards(_shownFullness, targetFullness, deltaTime / _fadeTime);

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(WidthId, _width);
            _block.SetFloat(FlowOffsetId, _flowOffset);
            _block.SetFloat(FullnessId, _shownFullness);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>Pump output while the hose takes it; no flow otherwise.</summary>
        private void PumpFlow(out WaterFlowData flow)
        {
            flow = _transfer.IsDelivering && _transfer.Source != null ? _transfer.Source.Output : default;
        }
    }
}
