using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>
    /// A point above a mission object (axe, pump, door, tank) where the HUD draws the objective marker.
    /// Holds no logic: objectives ask it to show or hide, the marker view draws every shown marker.
    /// Several objectives may ask for the same marker; it stays until the last one lets go.
    /// While its object is out of the world (e.g. an item in the inventory) it is not drawn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionMarker : MonoBehaviour
    {
        private static readonly List<MissionMarker> s_shown = new List<MissionMarker>();

        [Header("Placement")]
        [Tooltip("Optional: the marker follows this point instead of this object.")]
        [SerializeField] private Transform _anchor;

        [Tooltip("Height of the marker above the anchor, in meters (m).")]
        [SerializeField] private float _height = 1f;

        private int _requests;

        /// <summary>Markers that are asked for now; the view skips those whose object is out of the world.</summary>
        public static IReadOnlyList<MissionMarker> Shown => s_shown;

        public bool IsShown => _requests > 0;

        /// <summary>Where the marker is drawn, in world space.</summary>
        public Vector3 WorldPosition => (_anchor != null ? _anchor : transform).position + Vector3.up * _height;

        // Domain reload on entering Play Mode is off in this project: drop markers left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_shown.Clear();
        }

        public void Show()
        {
            _requests++;
            if (_requests == 1)
                s_shown.Add(this);
        }

        public void Hide()
        {
            if (_requests == 0)
                return;

            _requests--;
            if (_requests == 0)
                s_shown.Remove(this);
        }

        private void OnDestroy()
        {
            _requests = 0;
            s_shown.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.72f, 0.15f);
            Gizmos.DrawWireSphere(WorldPosition, 0.2f);
        }
    }
}
