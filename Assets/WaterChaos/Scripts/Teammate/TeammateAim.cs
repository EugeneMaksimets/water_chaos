using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Teammate
{
    /// <summary>
    /// A teammate aims their stream at a burning fire (<see cref="FireTargeting"/>): lifted to the roof in a bubble they
    /// pick the roof fires, back on the ground the fires around them. The body turns toward the fire and the nozzle
    /// follows on top of the animation; the aim is raised or lowered by where the stream actually lands.
    /// Without a fire in range they spray where they stood facing at the start (the house).
    /// Sits on the teammate's nozzle object (in the hand).
    /// </summary>
    [DefaultExecutionOrder(-10)] // before WaterStream.LateUpdate draws the arc
    [RequireComponent(typeof(TeammateNozzle))]
    public sealed class TeammateAim : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The teammate's body, turned toward the fire.")]
        [SerializeField] private Transform _body;

        [Header("Targets")]
        [Tooltip("Fires farther than this (height counted double) are ignored, in meters (m).")]
        [SerializeField, Min(1f)] private float _range = 18f;

        [Tooltip("How much more a difference in height counts than the same distance sideways.")]
        [SerializeField, Min(1f)] private float _heightWeight = 2f;

        [Tooltip("Seconds (s) between choosing a fire.")]
        [SerializeField, Min(0.05f)] private float _retargetInterval = 0.5f;

        [Header("Aim")]
        [Tooltip("How fast the body turns, in degrees per second (°/s).")]
        [SerializeField, Min(0f)] private float _turnSpeed = 120f;

        [Tooltip("How fast the nozzle follows the aim (1/s).")]
        [SerializeField, Min(0.1f)] private float _aimSpeed = 4f;

        [Tooltip("Most the nozzle turns away from the hands, in degrees (°).")]
        [SerializeField, Range(0f, 90f)] private float _maxNozzleAngle = 55f;

        [Tooltip("How fast the aim point moves up or down to make the stream land on the fire, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _correctionSpeed = 3f;

        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<FireExtinguishHandler> _fires = new List<FireExtinguishHandler>();
        private TeammateNozzle _nozzle;
        private Quaternion _restBody;
        private Quaternion _restLocal;
        private Quaternion _aim = Quaternion.identity;
        private FireExtinguishHandler _target;
        private float _nextRetarget;
        private float _raise;

        /// <summary>The fire aimed at now; null when none is in range.</summary>
        public FireExtinguishHandler Target => _target;

        private void Awake()
        {
            _nozzle = GetComponent<TeammateNozzle>();
            _restLocal = transform.localRotation;
            if (_body != null)
                _restBody = _body.rotation;
        }

        private void Update()
        {
            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + _retargetInterval;
                Retarget();
            }

            if (_target != null && !_target.IsBurning)
                _target = null;

            TurnBody();
        }

        private void LateUpdate()
        {
            // The animation leaves the nozzle in the hands; the aim goes on top of it every frame.
            transform.localRotation = _restLocal;
            WaterStream stream = _nozzle.Stream;
            Transform spout = stream != null ? stream.Nozzle : null;
            if (spout == null)
                return;

            Quaternion wanted = Quaternion.identity;
            if (_target != null && _nozzle.IsSpraying)
            {
                CorrectHeight(stream, spout);
                Vector3 aimPoint = _target.transform.position + Vector3.up * _raise;
                wanted = Quaternion.FromToRotation(spout.forward, aimPoint - spout.position);
                wanted = Quaternion.RotateTowards(Quaternion.identity, wanted, _maxNozzleAngle);
            }
            else
            {
                _raise = Mathf.MoveTowards(_raise, 0f, _correctionSpeed * Time.deltaTime);
            }

            _aim = Quaternion.Slerp(_aim, wanted, 1f - Mathf.Exp(-_aimSpeed * Time.deltaTime));
            transform.rotation = _aim * transform.rotation;
        }

        private void Retarget()
        {
            _fires.Clear();
            _positions.Clear();
            foreach (FireExtinguishHandler fire in FireExtinguishHandler.All)
            {
                if (fire != null && fire.IsBurning)
                {
                    _fires.Add(fire);
                    _positions.Add(fire.transform.position);
                }
            }

            Vector3 from = _nozzle.Stream != null && _nozzle.Stream.Nozzle != null ? _nozzle.Stream.Nozzle.position : transform.position;
            int index = FireTargeting.Pick(from, _positions, _range, _heightWeight);
            FireExtinguishHandler target = index >= 0 ? _fires[index] : null;
            if (target != _target)
                _raise = 0f;
            _target = target;
        }

        /// <summary>Turns the body toward the fire around the vertical axis, or back to its first facing.</summary>
        private void TurnBody()
        {
            if (_body == null)
                return;

            Quaternion wanted = _restBody;
            if (_target != null)
            {
                Vector3 toFire = _target.transform.position - _body.position;
                toFire.y = 0f;
                if (toFire.sqrMagnitude > 0.01f)
                    wanted = Quaternion.LookRotation(toFire, Vector3.up);
            }

            _body.rotation = Quaternion.RotateTowards(_body.rotation, wanted, _turnSpeed * Time.deltaTime);
        }

        /// <summary>Raises the aim while the stream falls short of the fire, lowers it while it lands high above.</summary>
        private void CorrectHeight(WaterStream stream, Transform spout)
        {
            Vector3 fire = _target.transform.position;
            float step = _correctionSpeed * Time.deltaTime;
            if (!stream.HasHit)
            {
                _raise -= step;
            }
            else
            {
                Vector3 hit = stream.HitPoint;
                float toFire = Flat(fire - spout.position);
                float toHit = Flat(hit - spout.position);
                if (toHit < toFire - 1f)
                    _raise += step;
                else if (hit.y > fire.y + 1f)
                    _raise -= step;
            }

            _raise = Mathf.Clamp(_raise, -2f, 8f);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
