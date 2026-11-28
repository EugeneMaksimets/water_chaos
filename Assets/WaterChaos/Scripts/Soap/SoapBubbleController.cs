using UnityEngine;
using WaterChaos.Common;

namespace WaterChaos.Soap
{
    /// <summary>
    /// Runs the soap bubble effect on one character (<see cref="SoapBubbleModel"/>): spawns a separate
    /// <see cref="SoapBubbleView"/> around it, lifts the character and the bubble together, pops the bubble
    /// and gives the character back to its own movement and gravity.
    /// The character itself is never hidden, scaled or re-skinned; only its movement components are paused.
    /// Moves through the <see cref="CharacterController"/>, so it never pushes the character through solid geometry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class SoapBubbleController : MonoBehaviour
    {
        private const int MaxHits = 8;
        private static readonly RaycastHit[] s_hits = new RaycastHit[MaxHits];

        [Header("Bubble")]
        [Tooltip("Bubble shell spawned around the character; a separate object, never a replacement model.")]
        [SerializeField] private SoapBubbleView _bubblePrefab;

        [Tooltip("Child of the character the bubble is attached to; moved to the character's visual center. " +
                 "Created at runtime if empty.")]
        [SerializeField] private Transform _anchor;

        [Tooltip("Gap between the character and the bubble wall, in meters (m).")]
        [SerializeField, Min(0f)] private float _clearance = 0.25f;

        [Tooltip("Time for the bubble to grow around the character, in seconds (s).")]
        [SerializeField, Min(0f)] private float _formDuration = 0.6f;

        [Tooltip("Pause in the closed bubble before lift-off, in seconds (s).")]
        [SerializeField, Min(0f)] private float _encapsulatedDuration = 0.35f;

        [Header("Rise")]
        [Tooltip("Top lift speed, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _riseSpeed = 2.2f;

        [Tooltip("Height of one building floor, in meters (m).")]
        [SerializeField, Min(0f)] private float _floorHeight = 3f;

        [Tooltip("Floor to lift the character to; 1 = ground floor, 3 = third floor (two floors up).")]
        [SerializeField, Min(1)] private int _targetFloor = 3;

        [Tooltip("Extra lift above the target floor level, in meters (m).")]
        [SerializeField] private float _extraHeight = 0.5f;

        [Tooltip("Time at the top before the bubble pops, in seconds (s).")]
        [SerializeField, Min(0f)] private float _hangDuration = 0.8f;

        [Header("Burst")]
        [Tooltip("Length of the pop before the character is released, in seconds (s).")]
        [SerializeField, Min(0f)] private float _burstDuration = 0.1f;

        [Header("Obstacles")]
        [Tooltip("Layers that stop the bubble going up (ceilings, roofs, balconies).")]
        [SerializeField] private LayerMask _obstacleMask = ~0;

        [Tooltip("Distance kept from a ceiling, in meters (m).")]
        [SerializeField, Min(0f)] private float _skin = 0.05f;

        [Header("Control")]
        [Tooltip("Components that move the character (input, AI, gravity); paused while the bubble carries it.")]
        [SerializeField] private Behaviour[] _suspendWhileCarried = new Behaviour[0];

        [Tooltip("After landing, time before a new bubble can form, in seconds (s).")]
        [SerializeField, Min(0f)] private float _recaptureCooldown = 1.5f;

        [Tooltip("Falling longer than this counts as landed anyway, in seconds (s).")]
        [SerializeField, Min(0.1f)] private float _maxFallTime = 8f;

        private CharacterController _character;
        private Rigidbody _body;
        private SoapBubbleModel _model;
        private SoapBubbleView _bubble;

        private bool _savedKinematic;
        private bool _savedGravity;
        private bool _isSuspended;
        private bool _blockedAbove;
        private float _fallTime;
        // While the bubble forms, the character is raised this much (m) so the bubble does not sink into the ground.
        private float _groundLift;
        private float _groundLifted;

        public SoapBubblePhase Phase => Model.Phase;

        /// <summary>The bubble holds the character now (forming, closed, rising or popping).</summary>
        public bool IsCarrying => Model.IsCarrying;

        /// <summary>The current bubble; null when there is none.</summary>
        public SoapBubbleView ActiveBubble => _bubble;

        /// <summary>Planned lift above the start, in meters (m): the target floor level plus the extra height.</summary>
        public float RiseHeight => Mathf.Max(0f, _floorHeight * (_targetFloor - 1) + _extraHeight);

        // Built on first use, so it also works before Awake (and in edit mode tests, where Awake never runs).
        private SoapBubbleModel Model
        {
            get
            {
                if (_model == null)
                {
                    _model = new SoapBubbleModel(_formDuration, _encapsulatedDuration, _riseSpeed, RiseHeight,
                        _hangDuration, _burstDuration, _recaptureCooldown);
                    _model.PhaseChanged += OnPhaseChanged;
                }

                return _model;
            }
        }

        // Room around the body (m) for the arms and the nozzle when measuring the bubble.
        private const float BodyMargin = 0.5f;

        private CharacterController Character => _character != null ? _character : _character = GetComponent<CharacterController>();

        private void OnDisable()
        {
            Cancel();
        }

        private void OnDestroy()
        {
            Cancel();
        }

        private void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Starts a bubble around the character. Ignored (false) while a bubble is already active,
        /// during the cooldown after landing, or while this component is off.
        /// </summary>
        public bool TryCapture()
        {
            if (!enabled || !gameObject.activeInHierarchy)
                return false;

            if (_bubblePrefab == null)
            {
                Debug.LogError($"{name}: SoapBubbleController needs a Bubble Prefab.", this);
                return false;
            }

            return Model.TryCapture();
        }

        /// <summary>Ends the effect at once: removes the bubble and restores the character's movement and physics.</summary>
        public void Cancel()
        {
            if (_model == null || _model.Phase == SoapBubblePhase.Normal)
                return;

            _model.Cancel();
        }

        private void Step(float deltaTime)
        {
            SoapBubblePhase phase = Model.Phase;
            float freeSpace = phase == SoapBubblePhase.Rising ? MeasureFreeSpace(deltaTime) : float.PositiveInfinity;
            float lift = Model.Tick(deltaTime, freeSpace);

            if (Model.Phase == SoapBubblePhase.BubbleForming || phase == SoapBubblePhase.BubbleForming)
                lift += RaiseOffGround(Model.FormProgress);

            if (lift > 0f)
                Lift(lift);

            if (_bubble != null)
                _bubble.SetFormProgress(Model.FormProgress);

            if (Model.Phase == SoapBubblePhase.Falling)
            {
                _fallTime += deltaTime;
                if (_fallTime >= _maxFallTime || Character.isGrounded)
                    Model.Land();
            }
        }

        private void OnPhaseChanged(SoapBubblePhase phase)
        {
            switch (phase)
            {
                case SoapBubblePhase.BubbleForming:
                    Suspend();
                    SpawnBubble();
                    break;

                case SoapBubblePhase.Bursting:
                    if (_bubble != null)
                        _bubble.Burst();
                    _bubble = null;
                    break;

                case SoapBubblePhase.Falling:
                    _fallTime = 0f;
                    Restore();
                    break;

                case SoapBubblePhase.Normal:
                    // Landed normally (already restored), or cancelled mid-effect.
                    if (_bubble != null)
                        _bubble.Remove();
                    _bubble = null;
                    Restore();
                    break;
            }
        }

        // ───────────── Character ─────────────

        private void Suspend()
        {
            if (_isSuspended)
                return;

            _isSuspended = true;
            _blockedAbove = false;

            // Locks, not plain switching: a knockdown or a pause may hold the same parts at the same time.
            foreach (Behaviour part in _suspendWhileCarried)
                BehaviourLocks.Lock(part, this);

            _body = GetComponent<Rigidbody>();
            if (_body != null)
            {
                _savedKinematic = _body.isKinematic;
                _savedGravity = _body.useGravity;
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }

                _body.useGravity = false;
                _body.isKinematic = true;
            }
        }

        private void Restore()
        {
            if (!_isSuspended)
                return;

            _isSuspended = false;

            foreach (Behaviour part in _suspendWhileCarried)
                BehaviourLocks.Unlock(part, this);

            if (_body != null)
            {
                _body.isKinematic = _savedKinematic;
                _body.useGravity = _savedGravity;
            }
        }

        /// <returns>How much more to raise the character so the forming bubble clears the ground, in meters (m).</returns>
        private float RaiseOffGround(float formProgress)
        {
            float target = _groundLift * Mathf.Clamp01(formProgress);
            float step = Mathf.Max(0f, target - _groundLifted);
            _groundLifted += step;
            return step;
        }

        private void Lift(float distance)
        {
            CharacterController character = Character;
            if (!character.enabled)
                return;

            CollisionFlags flags = character.Move(Vector3.up * distance);
            // A ceiling the cast missed (e.g. a thin edge) still stops the rise on the next step.
            _blockedAbove = (flags & CollisionFlags.Above) != 0;
        }

        // ───────────── Bubble ─────────────

        private void SpawnBubble()
        {
            Bounds bounds = MeasureCharacter();
            Transform anchor = EnsureAnchor();
            anchor.position = bounds.center;

            _bubble = Instantiate(_bubblePrefab, anchor, false);
            _bubble.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            float radius = bounds.extents.magnitude + _clearance;
            _bubble.SetRadius(radius);
            _bubble.SetFormProgress(0f);

            // The bubble is wider than the character is tall from its center down: lift it a little off the ground.
            _groundLift = Mathf.Max(0f, radius - (bounds.center.y - bounds.min.y));
            _groundLifted = 0f;
        }

        private Transform EnsureAnchor()
        {
            if (_anchor != null)
                return _anchor;

            _anchor = new GameObject("BubbleEffectAnchor").transform;
            _anchor.SetParent(transform, false);
            return _anchor;
        }

        /// <summary>World bounds of the character: its visible meshes, else its capsule.</summary>
        private Bounds MeasureCharacter()
        {
            bool found = false;
            var bounds = new Bounds(transform.position, Vector3.zero);

            foreach (Renderer part in GetComponentsInChildren<Renderer>())
            {
                if (!(part is MeshRenderer || part is SkinnedMeshRenderer))
                    continue;
                if (_bubble != null && part.transform.IsChildOf(_bubble.transform))
                    continue;
                // Only the character: never the water stream (its tube mesh reaches far), the hose or flames on the body.
                if (part.GetComponentInParent<WaterChaos.Water.WaterStream>() != null
                    || part.GetComponentInParent<WaterChaos.Hose.HoseController>() != null
                    || part.GetComponentInParent<WaterChaos.Fire.FireSway>() != null)
                    continue;

                if (!found)
                    bounds = part.bounds;
                else
                    bounds.Encapsulate(part.bounds);
                found = true;
            }

            return found ? ClipToBody(bounds) : Character.bounds;
        }

        // Anything else long hanging off the character (a hose, a rope) must not blow the bubble up:
        // the measured size never goes past the body plus a margin for the arms and the nozzle.
        private Bounds ClipToBody(Bounds bounds)
        {
            // A switched-off controller reports empty bounds: nothing to clip against.
            if (!Character.enabled)
                return bounds;

            Bounds body = Character.bounds;
            body.Expand(BodyMargin * 2f);

            Vector3 min = Vector3.Max(bounds.min, body.min);
            Vector3 max = Vector3.Max(min, Vector3.Min(bounds.max, body.max));
            bounds.SetMinMax(min, max);
            return bounds;
        }

        /// <summary>
        /// How far the top of the bubble can still go up, in meters (m): a sphere as wide as the capsule
        /// is cast up from the head, ignoring the character itself.
        /// </summary>
        private float MeasureFreeSpace(float deltaTime)
        {
            if (_blockedAbove)
                return 0f;

            CharacterController character = Character;
            float scale = transform.lossyScale.y;
            float radius = character.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 0.95f;
            Vector3 center = transform.TransformPoint(character.center);
            float headOffset = Mathf.Max(0f, character.height * scale * 0.5f - radius);
            Vector3 origin = center + Vector3.up * headOffset;
            float headTop = origin.y + radius;

            float bubbleTop = _anchor != null && _bubble != null ? _anchor.position.y + _bubble.Radius : headTop;
            float headroom = Mathf.Max(0f, bubbleTop - headTop);
            float distance = headroom + _riseSpeed * deltaTime + _skin;

            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.up, s_hits, distance, _obstacleMask,
                QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    continue;

                // Already touching something above the head: no room at all.
                nearest = Mathf.Min(nearest, Mathf.Max(0f, hit.distance));
            }

            return float.IsPositiveInfinity(nearest) ? nearest : Mathf.Max(0f, nearest - headroom - _skin);
        }

        private void OnValidate()
        {
            // New inspector values take effect with the next bubble; a running one keeps its timeline.
            if (_model == null || _model.Phase != SoapBubblePhase.Normal)
                return;

            _model.PhaseChanged -= OnPhaseChanged;
            _model = null;
        }
    }
}
