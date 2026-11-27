using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Characters
{
    /// <summary>
    /// A teammate catches fire from a flamethrower stream or from standing in a fire: flames burn on them and they
    /// panic, waving both arms over the head (DOTween on the arm bones, the animator paused), until water hits them or
    /// the fire burns out by itself. A knocked-down teammate burns without the dance.
    /// Sits next to the teammate's collider, like every impact receiver.
    /// </summary>
    public sealed class TeammateBurning : MonoBehaviour, IFlameHitReceiver, IWaterImpactReceiver
    {
        private static readonly string[] Arms = { "UpperArm.L", "UpperArm.R" };

        [Header("References")]
        [Tooltip("Fire prefab used as the flames on the body (a look-only copy is made).")]
        [SerializeField] private GameObject _firePrefab;

        [Tooltip("Where the flames sit (e.g. the chest bone).")]
        [SerializeField] private Transform _fireAnchor;

        [SerializeField] private Animator _animator;

        [Tooltip("Optional: a lying teammate burns without waving.")]
        [SerializeField] private CharacterKnockdown _knockdown;

        [Tooltip("Switched off while panicking (the spray animation...).")]
        [SerializeField] private Behaviour[] _suspend = new Behaviour[0];

        [Header("Burning")]
        [Tooltip("The fire on the body goes out by itself after this long, in seconds (s).")]
        [SerializeField, Min(0.5f)] private float _burnTime = 5f;

        [Tooltip("Standing this close to a burning fire sets the teammate alight, in meters (m); 0 = never.")]
        [SerializeField, Min(0f)] private float _nearFireRadius = 0.9f;

        [Tooltip("Size of the flames on the body relative to the fire prefab.")]
        [SerializeField, Min(0.05f)] private float _fireScale = 0.6f;

        private readonly List<Transform> _arms = new List<Transform>();
        private readonly List<Quaternion> _armRest = new List<Quaternion>();
        private GameObject _flames;
        private readonly List<Tween> _panic = new List<Tween>();
        private bool _panicking;
        private bool[] _wasEnabled;
        private float _burnLeft;
        private float _nextNearCheck;

        public bool IsBurning { get; private set; }

        private void Awake()
        {
            if (_animator != null)
            {
                foreach (Transform bone in _animator.GetComponentsInChildren<Transform>(true))
                {
                    if (System.Array.IndexOf(Arms, bone.name) >= 0)
                        _arms.Add(bone);
                }
            }
        }

        private void OnDestroy()
        {
            KillPanic();
        }

        private void KillPanic()
        {
            foreach (Tween tween in _panic)
                tween.Kill();
            _panic.Clear();
        }

        public void ReceiveFlame(Vector3 point)
        {
            Ignite();
        }

        public void ReceiveImpact(in WaterImpact impact)
        {
            if (IsBurning && impact.Liquid.ExtinguishingPower() > 0f)
                Extinguish();
        }

        private void Update()
        {
            if (!IsBurning)
            {
                if (_nearFireRadius > 0f && Time.time >= _nextNearCheck)
                {
                    _nextNearCheck = Time.time + 0.25f;
                    if (IsNearFire())
                        Ignite();
                }

                return;
            }

            _burnLeft -= Time.deltaTime;
            if (_burnLeft <= 0f)
            {
                Extinguish();
                return;
            }

            bool lying = _knockdown != null && _knockdown.IsDown;
            if (lying && _panicking)
                StopPanic();
            else if (!lying && !_panicking)
                StartPanic();
        }

        private void Ignite()
        {
            _burnLeft = _burnTime;
            if (IsBurning)
                return;

            IsBurning = true;
            Flames().SetActive(true);
            if (_knockdown == null || !_knockdown.IsDown)
                StartPanic();
        }

        private void Extinguish()
        {
            IsBurning = false;
            if (_flames != null)
                _flames.SetActive(false);
            StopPanic();
        }

        private void StartPanic()
        {
            if (_animator == null || _arms.Count == 0)
                return;

            SetSuspended(true);
            _animator.enabled = false;
            _armRest.Clear();
            foreach (Transform arm in _arms)
                _armRest.Add(arm.localRotation);

            _panicking = true;
            for (int i = 0; i < _arms.Count; i++)
            {
                Transform arm = _arms[i];
                Transform child = arm.childCount > 0 ? arm.GetChild(0) : null;
                if (child == null)
                    continue;

                // Raise the arm straight up (world space, whatever the bone axes), then wave it side to side.
                float side = arm.name.EndsWith(".L") ? -1f : 1f;
                Vector3 along = (child.position - arm.position).normalized;
                Vector3 sideways = transform.right * side;
                Quaternion up = Quaternion.FromToRotation(along, (Vector3.up + sideways * 0.35f).normalized) * arm.rotation;
                Quaternion wave = Quaternion.FromToRotation(along, (Vector3.up - sideways * 0.15f).normalized) * arm.rotation;

                // Up first, then waving forever (separate tweens: endless loops cannot live in a sequence).
                _panic.Add(arm.DORotateQuaternion(up, 0.2f).SetLink(gameObject));
                _panic.Add(arm.DORotateQuaternion(wave, 0.22f).SetDelay(0.2f + i * 0.11f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject));
            }
        }

        private void StopPanic()
        {
            if (!_panicking)
                return;

            _panicking = false;
            KillPanic();
            for (int i = 0; i < _arms.Count && i < _armRest.Count; i++)
                _arms[i].localRotation = _armRest[i];

            if (_animator != null && (_knockdown == null || !_knockdown.IsDown))
                _animator.enabled = true;
            SetSuspended(false);
        }

        private bool IsNearFire()
        {
            foreach (FireExtinguishHandler fire in FireExtinguishHandler.All)
            {
                if (fire.IsBurning && Vector3.Distance(fire.transform.position, transform.position) <= _nearFireRadius)
                    return true;
            }

            return false;
        }

        /// <summary>The flames on the body: a copy of the fire prefab with every gameplay part removed, made once.</summary>
        private GameObject Flames()
        {
            if (_flames != null)
                return _flames;

            Transform anchor = _fireAnchor != null ? _fireAnchor : transform;
            _flames = _firePrefab != null ? Instantiate(_firePrefab, anchor) : new GameObject("Flames");
            _flames.name = "Body Flames";
            _flames.transform.localPosition = Vector3.zero;
            _flames.transform.localScale = Vector3.one * _fireScale;

            DestroyAll<FireSpreader>(_flames);
            DestroyAll<FireFuelHandler>(_flames);
            DestroyAll<FireSteamEffect>(_flames);
            DestroyAll<FireView>(_flames);
            DestroyAll<FireExtinguishHandler>(_flames);
            DestroyAll<Collider>(_flames);
            DestroyAll<Rigidbody>(_flames);
            return _flames;
        }

        private static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
                DestroyImmediate(component);
        }

        private void SetSuspended(bool suspend)
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
