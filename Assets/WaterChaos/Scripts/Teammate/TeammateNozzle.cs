using UnityEngine;
using WaterChaos.Hose;
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

        /// <summary>The teammate's stream currently carries some flow.</summary>
        public bool IsSpraying => _stream != null && _stream.Flow.FlowRate > 0f;

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
            _stream.SetSource(_outlet);
            if (_hose != null)
                _hose.Connect(_point);

            if (_openOnStart && !_outlet.IsOpen)
                _outlet.Toggle();
        }
    }
}
