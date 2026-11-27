using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Soap;
using WaterChaos.Water;
using WaterChaos.Sounds;

namespace WaterChaos.Characters
{
    /// <summary>
    /// A strong stream of plain or toxic water, or of gasoline that does not burn, knocks the character down; soapy water
    /// never does (it lifts in a bubble). A teammate tips over around the feet in the stream's direction and goes limp
    /// (DOTween on the body and a few bones); the player falls with the camera. They get up after at most a second on the
    /// ground (sooner once the stream stops) and cannot be knocked down again for a moment; a teammate turns to the
    /// nearest fire and carries on.
    /// Sits next to the character's collider, like every impact receiver.
    /// </summary>
    public sealed class CharacterKnockdown : MonoBehaviour, IWaterImpactReceiver, IFlameHitReceiver
    {
        private enum FallStyle
        {
            /// <summary>The model tips over around its feet (teammates).</summary>
            TipOver,

            /// <summary>The first-person camera drops and looks up (the player).</summary>
            Camera,
        }

        // Bones that go limp while a teammate lies on the ground, with how far each may sag, in degrees (°):
        // the trunk and legs only a little, so the body stays flat on the ground.
        private static readonly string[] LimpBones = { "Spine", "Neck", "UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R", "UpperLeg.L", "UpperLeg.R", "LowerLeg.L", "LowerLeg.R" };
        private static readonly float[] LimpAngles = { 8f, 15f, 35f, 35f, 30f, 30f, 10f, 10f, 20f, 20f };

        // Height of the hips above the ground while lying, in meters (m).
        private const float LyingHipsHeight = 0.2f;

        [Header("Knockdown")]
        [SerializeField] private FallStyle _style = FallStyle.TipOver;

        [Tooltip("Impact force that counts as pushing, in newtons (N).")]
        [SerializeField, Min(0f)] private float _minForce = 40f;

        [Tooltip("How long the stream must keep pushing before the character falls, in seconds (s).")]
        [SerializeField, Min(0f)] private float _pushTime = 0.3f;

        [Tooltip("The character gets up this long after the stream stops pressing, in seconds (s).")]
        [SerializeField, Min(0f)] private float _recoverDelay = 1f;

        [Tooltip("The character gets up after lying this long even if the stream keeps pressing, in seconds (s).")]
        [SerializeField, Min(0f)] private float _maxLieTime = 1f;

        [Tooltip("After getting up the character cannot be knocked down for this long, in seconds (s).")]
        [SerializeField, Min(0f)] private float _immunityTime = 1.5f;

        [Header("Tip Over")]
        [Tooltip("The model that tips over (its pivot at the feet).")]
        [SerializeField] private Transform _model;

        [Tooltip("The model's animator, paused while lying.")]
        [SerializeField] private Animator _animator;

        [Header("Camera")]
        [SerializeField] private Transform _camera;

        [Tooltip("How far the camera drops when the player falls, in meters (m).")]
        [SerializeField, Min(0f)] private float _cameraDrop = 1.2f;

        [Header("While Down")]
        [Tooltip("Switched off while lying (movement, look, the teammate's spray animation...).")]
        [SerializeField] private Behaviour[] _suspend = new Behaviour[0];

        [Header("Get Up")]
        [Tooltip("A teammate turns to the nearest burning fire within this distance, in meters (m); 0 = never turns.")]
        [SerializeField, Min(0f)] private float _fireSearchRadius = 30f;

        private readonly List<Transform> _limbs = new List<Transform>();
        private readonly List<float> _limbSag = new List<float>();
        private Transform _hips;
        private readonly List<Quaternion> _limbRest = new List<Quaternion>();
        private bool[] _wasEnabled;
        private SoapBubbleController _bubble;
        private Quaternion _modelRest;
        private Vector3 _modelRestPosition;
        private Vector3 _cameraRestPosition;
        private Quaternion _cameraRestRotation;
        private Sequence _sequence;

        private float _pushed;
        private float _downSince;
        private float _immuneUntil;
        private float _lastFlame = float.NegativeInfinity;
        private Vector3 _feet;
        private Vector3 _fallAxis;
        private Vector3 _fallStartPosition;
        private Quaternion _fallStartRotation;
        private float _tilt;
        private float _lastPushTime = float.NegativeInfinity;
        private float _lastPushStep = -1f;
        private bool _gettingUp;

        /// <summary>Lying on the ground (falling, down or getting up).</summary>
        public bool IsDown { get; private set; }

        private void Awake()
        {
            _bubble = GetComponent<SoapBubbleController>();

            if (_animator != null)
            {
                foreach (Transform bone in _animator.GetComponentsInChildren<Transform>(true))
                {
                    int index = System.Array.IndexOf(LimpBones, bone.name);
                    if (index >= 0)
                    {
                        _limbs.Add(bone);
                        _limbSag.Add(LimpAngles[index]);
                    }

                    if (bone.name == "Hips")
                        _hips = bone;
                }
            }
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        // A burning stream hits too: its gasoline burns instead of pushing.
        public void ReceiveFlame(Vector3 point)
        {
            _lastFlame = Time.time;
        }

        public void ReceiveImpact(in WaterImpact impact)
        {
            // Soapy water lifts in a bubble instead; lit gasoline burns instead of pushing.
            if (impact.IsSoapy || impact.Force < _minForce || Time.time < _immuneUntil)
                return;
            if (impact.Liquid.IsFlammable() && Time.time - _lastFlame < 0.3f)
                return;
            if (_bubble != null && _bubble.IsCarrying)
                return;

            // Several hits in one physics step count once.
            if (_lastPushStep != Time.fixedTime)
            {
                _lastPushStep = Time.fixedTime;
                _pushed += Time.fixedDeltaTime;
            }

            _lastPushTime = Time.time;
            if (!IsDown && _pushed >= _pushTime)
                KnockDown(impact.Direction);
        }

        private void Update()
        {
            if (!IsDown)
            {
                // The push has to be continuous: a short gap starts it over.
                if (Time.time - _lastPushTime > 0.2f)
                    _pushed = 0f;
                return;
            }

            if (!_gettingUp && (Time.time - _lastPushTime > _recoverDelay || Time.time - _downSince >= _maxLieTime))
                GetUp();
        }

        private void KnockDown(Vector3 direction)
        {
            IsDown = true;
            _gettingUp = false;
            _downSince = Time.time;
            Sound.Play(SoundId.Knockdown, transform.position);
            Suspend(true);
            _sequence?.Kill();

            if (_style == FallStyle.Camera)
                FallCamera();
            else
                TipOver(direction);
        }

        private void TipOver(Vector3 direction)
        {
            if (_model == null)
                return;

            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude < 0.001f)
                flat = -transform.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);

            _modelRest = _model.localRotation;
            _modelRestPosition = _model.localPosition;
            if (_animator != null)
                _animator.enabled = false;

            _limbRest.Clear();
            foreach (Transform limb in _limbs)
                _limbRest.Add(limb.localRotation);

            // Around the feet (the character's root stands on the ground), whatever the model's own pivot.
            _feet = transform.position;
            _fallAxis = axis;
            _fallStartPosition = _model.position;
            _fallStartRotation = _model.rotation;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Append(DOVirtual.Float(0f, 85f, 0.5f, ApplyTilt).SetEase(Ease.OutBounce));

            // Limp: every listed bone sags a little, each its own way.
            for (int i = 0; i < _limbs.Count; i++)
            {
                float limit = _limbSag[i];
                Quaternion sag = Quaternion.Euler(Random.Range(-limit, limit), Random.Range(-limit * 0.5f, limit * 0.5f), Random.Range(-limit, limit));
                _sequence.Join(_limbs[i].DOLocalRotateQuaternion(_limbRest[i] * sag, 0.6f).SetEase(Ease.OutBack));
            }
        }

        private void FallCamera()
        {
            if (_camera == null)
                return;

            _cameraRestPosition = _camera.localPosition;
            _cameraRestRotation = _camera.localRotation;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Append(_camera.DOLocalMove(_cameraRestPosition + Vector3.down * _cameraDrop, 0.45f).SetEase(Ease.OutBounce));
            _sequence.Join(_camera.DOLocalRotateQuaternion(Quaternion.Euler(-65f, 0f, 25f), 0.45f).SetEase(Ease.OutQuad));
        }

        private void GetUp()
        {
            _gettingUp = true;
            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetLink(gameObject);

            if (_style == FallStyle.Camera && _camera != null)
            {
                _sequence.Append(_camera.DOLocalMove(_cameraRestPosition, 0.5f).SetEase(Ease.OutBack));
                _sequence.Join(_camera.DOLocalRotateQuaternion(_cameraRestRotation, 0.5f));
            }
            else if (_model != null)
            {
                for (int i = 0; i < _limbs.Count; i++)
                    _sequence.Join(_limbs[i].DOLocalRotateQuaternion(_limbRest[i], 0.3f));
                _sequence.Append(DOVirtual.Float(_tilt, 0f, 0.5f, ApplyTilt).SetEase(Ease.OutQuad));
                _sequence.AppendCallback(() =>
                {
                    _model.localPosition = _modelRestPosition;
                    _model.localRotation = _modelRest;
                });

                Transform fire = FindNearestFire();
                if (fire != null)
                {
                    Vector3 look = Vector3.ProjectOnPlane(fire.position - transform.position, Vector3.up);
                    if (look.sqrMagnitude > 0.01f)
                        _sequence.Append(transform.DORotateQuaternion(Quaternion.LookRotation(look), 0.4f));
                }
            }

            _sequence.OnComplete(Recovered);
        }

        /// <summary>Tips the model by <paramref name="angle"/> degrees around the feet, lifting it a little so it lies on the ground.</summary>
        private void ApplyTilt(float angle)
        {
            _tilt = angle;
            Quaternion tilt = Quaternion.AngleAxis(angle, _fallAxis);
            _model.SetPositionAndRotation(_feet + tilt * (_fallStartPosition - _feet), tilt * _fallStartRotation);

            // Whatever the model's own pivot: lying, the hips rest just above the ground the character stood on.
            if (_hips != null)
            {
                float share = Mathf.Clamp01(angle / 85f);
                float correction = (_feet.y + LyingHipsHeight - _hips.position.y) * share;
                _model.position += Vector3.up * correction;
            }
        }

        private void Recovered()
        {
            _immuneUntil = Time.time + _immunityTime;
            if (_animator != null)
                _animator.enabled = true;
            Suspend(false);
            IsDown = false;
            _gettingUp = false;
            _pushed = 0f;
        }

        private Transform FindNearestFire()
        {
            if (_fireSearchRadius <= 0f)
                return null;

            Transform best = null;
            float bestDistance = _fireSearchRadius;
            foreach (FireExtinguishHandler fire in FireExtinguishHandler.All)
            {
                if (!fire.IsBurning)
                    continue;

                float distance = Vector3.Distance(fire.transform.position, transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = fire.transform;
                }
            }

            return best;
        }

        private void Suspend(bool suspend)
        {
            if (suspend)
                _wasEnabled = new bool[_suspend.Length];

            for (int i = 0; i < _suspend.Length; i++)
            {
                Behaviour part = _suspend[i];
                if (part == null)
                    continue;

                if (suspend)
                {
                    _wasEnabled[i] = part.enabled;
                    part.enabled = false;
                }
                else if (_wasEnabled != null && _wasEnabled[i])
                {
                    part.enabled = true;
                }
            }
        }
    }
}
