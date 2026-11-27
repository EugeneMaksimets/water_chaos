using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Soap;
using WaterChaos.Splitter;
using WaterChaos.Water;

namespace WaterChaos.Teammate
{
    /// <summary>
    /// Nozzle in a teammate's hands, connected from the start to one outlet of a <see cref="HoseSplitter"/>.
    /// The stream gets whatever the splitter carries: water from the hydrant, nothing when the hydrant runs dry,
    /// gasoline (and so a flame stream) once the player feeds the splitter from the gasoline tank.
    /// The stream object must not be pickable: use the spray part of a nozzle without PickableItem and NozzleController.
    /// </summary>
    public sealed class TeammateNozzle : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Stream of this teammate's nozzle.")]
        [SerializeField] private WaterStream _stream;

        [Tooltip("Splitter outlet the nozzle hose is connected to (it has a WaterConnectionPoint).")]
        [SerializeField] private SplitterOutlet _outlet;

        [Tooltip("Optional: the hose from the outlet to the nozzle, for the look.")]
        [SerializeField] private HoseController _hose;

        [Header("Settings")]
        [Tooltip("Opens the outlet valve on start, so the teammate sprays right away.")]
        [SerializeField] private bool _openOnStart = true;

        private WaterConnectionPoint _point;
        private SoapBubbleController _bubble;
        private SoapBubblePhase _bubblePhase;

        // Below this the stream is only a trickle (a hydrant running dry), in liters per second (L/s).
        private const float MinSprayFlow = 0.1f;

        /// <summary>The teammate's stream; null if not set up.</summary>
        public WaterStream Stream => _stream;

        /// <summary>What the teammate's stream carries now (no flow without a stream).</summary>
        public WaterFlowData Flow => _stream != null ? _stream.Flow : default;

        /// <summary>The teammate's stream carries more than a trickle.</summary>
        public bool IsSpraying => _stream != null && _stream.Flow.FlowRate > MinSprayFlow;

        // Start: connection points find their source in Awake.
        private void Start()
        {
            if (_stream == null || _outlet == null)
            {
                Debug.LogError($"{name}: TeammateNozzle needs Stream and Outlet references.", this);
                enabled = false;
                return;
            }

            _point = _outlet.GetComponent<WaterConnectionPoint>();
            if (_point == null)
            {
                Debug.LogError($"{name}: the outlet {_outlet.name} has no WaterConnectionPoint.", this);
                enabled = false;
                return;
            }

            if (!_point.IsFreeFor(null))
                Debug.LogWarning($"{name}: outlet {_outlet.name} is already taken by another hose.", this);

            _point.Attach();
            _bubble = GetComponentInParent<SoapBubbleController>();
            _stream.SetSource(_outlet);
            if (_hose != null)
                _hose.Connect(_point);

            if (_openOnStart && !_outlet.IsOpen)
                _outlet.Toggle();
        }

        // Lifted over a roof in a bubble, the hose wraps around its edges; back on the ground it is laid straight again.
        private void Update()
        {
            if (_bubble == null)
                return;

            SoapBubblePhase phase = _bubble.Phase;
            if (phase == _bubblePhase)
                return;

            if (phase == SoapBubblePhase.Normal && _hose != null)
                _hose.ResetPath();
            _bubblePhase = phase;
        }
    }
}
