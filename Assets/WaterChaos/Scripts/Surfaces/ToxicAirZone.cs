using System;
using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// A building full of toxic air while active: characters inside are exposed (<see cref="ToxicHazards"/>), and the
    /// player without a gas mask is pushed out to the exit after a few seconds (<see cref="ToxicEjectionModel"/>).
    /// The volume is made of boxes (e.g. the rooms of the house). Switched on and off by the mission.
    /// </summary>
    public sealed class ToxicAirZone : MonoBehaviour, IToxicHazardArea
    {
        [Header("Volume")]
        [Tooltip("Boxes that make up the toxic volume (e.g. the house's room triggers).")]
        [SerializeField] private BoxCollider[] _areas = new BoxCollider[0];

        [SerializeField] private bool _activeOnStart;

        [Header("Player")]
        [Tooltip("The player (has a CharacterController and a ToxicExposure).")]
        [SerializeField] private Transform _player;

        [Tooltip("Where the player ends up when pushed out (outside the building, on the ground).")]
        [SerializeField] private Transform _exit;

        [Tooltip("Seconds (s) the player can stay inside without a gas mask before being pushed out.")]
        [SerializeField, Range(0.5f, 20f)] private float _tolerance = 3f;

        private ToxicEjectionModel _ejection;
        private ToxicExposure _exposure;
        private CharacterController _body;
        private bool _registered;

        public bool IsActive { get; private set; }

        /// <summary>Raised when the player was pushed out (e.g. for a hint).</summary>
        public event Action Ejected;

        private void Awake()
        {
            _ejection = new ToxicEjectionModel(_tolerance);
            if (_player != null)
            {
                _exposure = _player.GetComponent<ToxicExposure>();
                _body = _player.GetComponent<CharacterController>();
            }
        }

        private void Start()
        {
            if (_activeOnStart)
                SetActive(true);
        }

        private void OnDisable()
        {
            Unregister();
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (active && isActiveAndEnabled && !_registered)
            {
                ToxicHazards.Register(this);
                _registered = true;
            }
            else if (!active)
            {
                Unregister();
            }
        }

        public bool Contains(Vector3 position)
        {
            foreach (BoxCollider area in _areas)
            {
                if (area == null)
                    continue;

                Vector3 local = area.transform.InverseTransformPoint(position) - area.center;
                Vector3 half = area.size * 0.5f;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z)
                    return true;
            }

            return false;
        }

        private void Update()
        {
            if (!IsActive || _player == null)
                return;

            Vector3 body = _body != null ? _player.TransformPoint(_body.center) : _player.position + Vector3.up;
            bool isProtected = _exposure != null && _exposure.IsProtected;
            if (_ejection.Tick(Contains(body), isProtected, Time.deltaTime))
                Eject();
        }

        private void Eject()
        {
            if (_exit == null)
                return;

            // A CharacterController overrides a moved transform unless it is off for the move.
            bool wasEnabled = _body != null && _body.enabled;
            if (_body != null)
                _body.enabled = false;
            _player.position = _exit.position;
            if (_body != null)
                _body.enabled = wasEnabled;

            Ejected?.Invoke();
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            _registered = false;
            ToxicHazards.Unregister(this);
        }
    }
}
