using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Moves a water surface (e.g. a lake plane) between its empty and full heights with the reservoir fill level.
    /// Look only: the volume comes from the reservoir.
    /// </summary>
    public sealed class ReservoirSurfaceView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaterReservoirComponent _reservoir;

        [Tooltip("Water surface object; moved along its parent's Y.")]
        [SerializeField] private Transform _surface;

        [Header("Heights")]
        [Tooltip("Local Y of the surface when the reservoir is full, in meters (m).")]
        [SerializeField] private float _fullY = 0.8f;

        [Tooltip("Local Y of the surface when the reservoir is empty, in meters (m).")]
        [SerializeField] private float _emptyY;

        private void Awake()
        {
            if (_reservoir == null || _surface == null)
            {
                Debug.LogError($"{name}: ReservoirSurfaceView needs Reservoir and Surface references.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            WaterReservoir reservoir = _reservoir.Reservoir;
            if (reservoir == null)
                return;

            bool visible = !reservoir.IsEmpty;
            if (_surface.gameObject.activeSelf != visible)
                _surface.gameObject.SetActive(visible);

            Vector3 position = _surface.localPosition;
            position.y = Mathf.Lerp(_emptyY, _fullY, reservoir.FillRatio);
            _surface.localPosition = position;
        }
    }
}
