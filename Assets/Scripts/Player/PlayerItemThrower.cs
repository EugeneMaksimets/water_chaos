using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Player
{
    /// <summary>
    /// Holding throw aims the selected item and shows its predicted path;
    /// releasing it throws the item along the camera forward.
    /// </summary>
    public sealed class PlayerItemThrower : MonoBehaviour
    {
        private const int TrajectoryPointCount = 60;

        /// <summary>Distance kept between a spawned item and a wall in front of the camera, in meters (m).</summary>
        private const float WallGap = 0.3f;

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("The throw goes along its forward.")]
        [SerializeField] private Transform _camera;

        [Tooltip("Draws the predicted path while aiming. Optional.")]
        [SerializeField] private LineRenderer _trajectoryLine;

        [Header("Throw")]
        [Tooltip("Impulse given to the item, in newton-seconds (N·s). Launch speed = impulse / item mass.")]
        [SerializeField, Min(0f)] private float _throwImpulse = 10f;

        [Tooltip("How far in front of the camera the item appears, in meters (m).")]
        [SerializeField, Min(0f)] private float _spawnDistance = 0.8f;

        [Tooltip("Layers that block the item: the spawn point and the predicted path stop on them. Exclude the Player layer.")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Header("Trajectory")]
        [Tooltip("Time between predicted path points, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _trajectoryTimeStep = 0.05f;

        private readonly Vector3[] _points = new Vector3[TrajectoryPointCount];

        /// <summary>The selected slot holds an item.</summary>
        public bool CanThrow { get; private set; }

        /// <summary>Throw is held and there is an item to throw.</summary>
        public bool IsAiming { get; private set; }

        private void Awake()
        {
            if (_input == null || _inventory == null || _camera == null)
            {
                Debug.LogError($"{name}: PlayerItemThrower needs Input, Inventory and Camera references.", this);
                enabled = false;
                return;
            }

            HideTrajectory();
        }

        private void OnEnable()
        {
            _input.ThrowReleased += OnThrowReleased;
        }

        private void OnDisable()
        {
            _input.ThrowReleased -= OnThrowReleased;
            CanThrow = false;
            IsAiming = false;
            HideTrajectory();
        }

        private void Update()
        {
            IPickableItem item = _inventory.Model.SelectedItem;
            CanThrow = item != null;
            IsAiming = CanThrow && _input.ThrowHeld;

            if (IsAiming)
                DrawTrajectory(item);
            else
                HideTrajectory();
        }

        private void OnThrowReleased()
        {
            IPickableItem item = _inventory.Model.RemoveSelected();
            if (item == null)
                return;

            item.Release(GetSpawnPosition(), _camera.forward * _throwImpulse);
        }

        /// <summary>A point in front of the camera, pulled back if a wall is closer, so the item is not spawned inside it.</summary>
        private Vector3 GetSpawnPosition()
        {
            Vector3 forward = _camera.forward;
            float distance = _spawnDistance;

            if (Physics.Raycast(_camera.position, forward, out RaycastHit hit, _spawnDistance + WallGap, _collisionMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - WallGap));

            return _camera.position + forward * distance;
        }

        private void DrawTrajectory(IPickableItem item)
        {
            if (_trajectoryLine == null)
                return;

            float mass = Mathf.Max(item.Mass, 0.001f);
            Vector3 velocity = _camera.forward * (_throwImpulse / mass);
            ThrowTrajectory.Fill(_points, GetSpawnPosition(), velocity, Physics.gravity, _trajectoryTimeStep);

            int count = CutAtFirstHit();
            _trajectoryLine.positionCount = count;
            for (int i = 0; i < count; i++)
                _trajectoryLine.SetPosition(i, _points[i]);

            _trajectoryLine.enabled = true;
        }

        /// <summary>Ends the path where it first hits something; returns the number of points to draw.</summary>
        private int CutAtFirstHit()
        {
            for (int i = 1; i < _points.Length; i++)
            {
                if (Physics.Linecast(_points[i - 1], _points[i], out RaycastHit hit, _collisionMask, QueryTriggerInteraction.Ignore))
                {
                    _points[i] = hit.point;
                    return i + 1;
                }
            }

            return _points.Length;
        }

        private void HideTrajectory()
        {
            if (_trajectoryLine != null)
                _trajectoryLine.enabled = false;
        }
    }
}
