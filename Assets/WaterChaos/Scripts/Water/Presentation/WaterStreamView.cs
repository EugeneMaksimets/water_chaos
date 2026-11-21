using UnityEngine;

namespace WaterChaos.Water
{
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the line uses this frame's arc
    /// <summary>
    /// Draws the water stream with a LineRenderer. Display only: all data comes from WaterStream.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class WaterStreamView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private WaterStream _stream;
        private LineRenderer _line;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            // Arc points are in world space
            _line.useWorldSpace = true;
        }

        private void LateUpdate()
        {
            int count = _stream != null ? _stream.VisiblePointCount : 0;

            // A line needs at least 2 points; fewer means no water is flowing
            bool visible = count >= 2;
            _line.enabled = visible;

            if (!visible)
                return;

            _line.positionCount = count;
            // Copies only positionCount elements: points after the hit are ignored
            _line.SetPositions(_stream.Points);
        }
    }
}