using System.Collections;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Player;

namespace WaterChaos.Tools
{
    /// <summary>
    /// A fire axe in the hands: the primary button swings it. The blow goes along the player's view up to
    /// <c>Reach</c>; the first thing hit gets a <see cref="ToolHit"/> (doors break after a few blows, glass shatters),
    /// or, if it is a loose physics object, a shove. The axe only delivers the blow; targets decide what happens.
    /// </summary>
    public sealed class AxeTool : MonoBehaviour, IEquippable
    {
        private const int MaxHits = 16;
        // Parts of the swing time: pull back up to WindupEnd, chop up to ChopEnd, then return.
        private const float WindupEnd = 0.2f;
        private const float ChopEnd = 0.4f;

        [Header("Blow")]
        [Tooltip("How far the blow reaches from the player's eyes, in meters (m).")]
        [SerializeField, Min(0.5f)] private float _reach = 2.3f;

        [Tooltip("Damage of one blow (1 = one normal blow; a door takes about 4).")]
        [SerializeField, Min(0f)] private float _damage = 1f;

        [Tooltip("Seconds (s) before the next swing.")]
        [SerializeField, Min(0.05f)] private float _cooldown = 0.5f;

        [Tooltip("Impulse given to a loose physics object that is hit, in newton-seconds (N·s).")]
        [SerializeField, Min(0f)] private float _pushImpulse = 8f;

        [Tooltip("Layers the blow can hit.")]
        [SerializeField] private LayerMask _hitMask = ~(1 << 2);

        [Header("Swing Look")]
        [Tooltip("Part turned during the swing (the axe model); empty = this object.")]
        [SerializeField] private Transform _swingPivot;

        [Tooltip("How far the axe is pulled back before the chop, in degrees (°).")]
        [SerializeField] private float _windupAngle = 30f;

        [Tooltip("How far the axe swings forward from its rest pose, in degrees (°).")]
        [SerializeField] private float _swingAngle = 45f;

        [Tooltip("Where the axe moves at the blow, in view space, in meters (m): x right, y up, z forward. Brings the head to the crosshair.")]
        [SerializeField] private Vector3 _strikeOffset = new Vector3(-0.15f, 0.15f, 0.1f);

        [Tooltip("Axis of the chop in the axe's own space: the head turns from up towards the blade (+X) around it.")]
        [SerializeField] private Vector3 _swingAxis = Vector3.back;

        [Tooltip("Seconds (s) of the whole swing: pull back, chop and return.")]
        [SerializeField, Min(0.05f)] private float _swingTime = 0.35f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private AxeSwingModel _model;
        private PlayerEquipment _owner;
        private Coroutine _swing;
        private Quaternion _restRotation;
        private Vector3 _restPosition;

        private Transform Pivot => _swingPivot != null ? _swingPivot : transform;

        private void Awake()
        {
            _model = new AxeSwingModel(_cooldown);
        }

        public void Equip(PlayerEquipment owner)
        {
            Unequip();
            _owner = owner;
            _restRotation = Pivot.localRotation;
            _restPosition = Pivot.localPosition;
            _owner.Input.PrimaryPressed += OnPrimaryPressed;
        }

        public void Unequip()
        {
            if (_owner == null)
                return;

            _owner.Input.PrimaryPressed -= OnPrimaryPressed;
            _owner = null;
            if (_swing != null)
            {
                StopCoroutine(_swing);
                _swing = null;
                Pivot.SetLocalPositionAndRotation(_restPosition, _restRotation);
            }
        }

        private void OnPrimaryPressed()
        {
            if (_owner == null || !_model.TrySwing(Time.time))
                return;

            Strike();
            if (isActiveAndEnabled)
                _swing = StartCoroutine(Swing());
        }

        private void Strike()
        {
            Transform view = _owner.Camera;
            if (!TryFindTarget(new Ray(view.position, view.forward), out RaycastHit hit))
                return;

            var blow = new ToolHit(hit.point, view.forward, _damage);
            IToolHitReceiver receiver = hit.collider.GetComponentInParent<IToolHitReceiver>();
            if (receiver != null)
            {
                receiver.ReceiveToolHit(blow);
                return;
            }

            Rigidbody body = hit.rigidbody;
            if (body != null && !body.isKinematic)
                body.AddForceAtPosition(blow.Direction * _pushImpulse, hit.point, ForceMode.Impulse);
        }

        // Nearest solid hit that is not the player or the axe itself.
        private bool TryFindTarget(Ray ray, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            int count = Physics.RaycastNonAlloc(ray, _hits, _reach, _hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance >= best || hit.collider.transform.IsChildOf(_owner.transform))
                    continue;

                best = hit.distance;
                nearest = hit;
            }

            return best < float.MaxValue;
        }

        private IEnumerator Swing()
        {
            Transform pivot = Pivot;
            Vector3 viewOffset = _owner.Camera.TransformVector(_strikeOffset);
            Vector3 offset = pivot.parent != null ? pivot.parent.InverseTransformVector(viewOffset) : viewOffset;
            for (float t = 0f; t < _swingTime; t += Time.deltaTime)
            {
                // Short pull back and up, fast chop towards the crosshair, slower return.
                float k = t / _swingTime;
                float angle;
                float lift;
                if (k < WindupEnd)
                {
                    float p = k / WindupEnd;
                    angle = -_windupAngle * p;
                    lift = 0.5f * p;
                }
                else if (k < ChopEnd)
                {
                    float p = (k - WindupEnd) / (ChopEnd - WindupEnd);
                    angle = Mathf.Lerp(-_windupAngle, _swingAngle, p);
                    lift = Mathf.Lerp(0.5f, 1f, p);
                }
                else
                {
                    float p = 1f - (k - ChopEnd) / (1f - ChopEnd);
                    angle = _swingAngle * p;
                    lift = p;
                }

                pivot.SetLocalPositionAndRotation(_restPosition + offset * lift, _restRotation * Quaternion.AngleAxis(angle, _swingAxis));
                yield return null;
            }

            pivot.SetLocalPositionAndRotation(_restPosition, _restRotation);
            _swing = null;
        }
    }
}
