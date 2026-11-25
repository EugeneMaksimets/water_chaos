using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Raises and lowers a water look inside a tank with the reservoir fill level.
    /// Look only: the volume comes from the reservoir.
    /// </summary>
    public sealed class ReservoirLevelView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaterReservoirComponent _reservoir;

        [Tooltip("Water look; scaled along its local Y from the tank bottom.")]
        [SerializeField] private Transform _water;

        [Header("Shape")]
        [Tooltip("Local Y of the tank bottom, in meters (m).")]
        [SerializeField] private float _bottom;

        [Tooltip("Water height when the tank is full, in meters (m).")]
        [SerializeField, Min(0.01f)] private float _fullHeight = 1f;

        [Tooltip("Smallest height shown while any water is left, in meters (m).")]
        [SerializeField, Min(0f)] private float _minHeight = 0.02f;

        private void Awake()
        {
            if (_reservoir == null || _water == null)
            {
                Debug.LogError($"{name}: ReservoirLevelView needs Reservoir and Water references.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            WaterReservoir reservoir = _reservoir.Reservoir;
            if (reservoir == null)
                return;

            bool visible = !reservoir.IsEmpty;
            if (_water.gameObject.activeSelf != visible)
                _water.gameObject.SetActive(visible);

            float height = Mathf.Max(_minHeight, _fullHeight * reservoir.FillRatio);

            Vector3 scale = _water.localScale;
            scale.y = height;
            _water.localScale = scale;

            Vector3 position = _water.localPosition;
            position.y = _bottom + height * 0.5f;
            _water.localPosition = position;
        }
    }
}
