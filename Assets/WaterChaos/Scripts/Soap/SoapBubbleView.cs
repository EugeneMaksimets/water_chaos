using UnityEngine;

namespace WaterChaos.Soap
{
    /// <summary>
    /// The big transparent soap bubble around a character: a separate shell object that only draws itself.
    /// It never touches the character's model, materials or scale; <see cref="SoapBubbleController"/> sizes it,
    /// drives its growth and tells it to pop. No colliders, so it never takes part in physics.
    /// </summary>
    public sealed class SoapBubbleView : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("The bubble shell (a unit sphere, 1 m across, with the soap bubble material).")]
        [SerializeField] private Transform _shell;

        [Tooltip("Optional small bubbles drifting around the big one.")]
        [SerializeField] private ParticleSystem _sparkles;

        [Header("Form")]
        [Tooltip("Shell size over form progress (0..1); overshoot above 1 gives a soft pop-in.")]
        [SerializeField] private AnimationCurve _formCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.7f, 1.08f), new Keyframe(1f, 1f));

        [Header("Wobble")]
        [Tooltip("How much the shell squashes and stretches, as a share of its size (0.03 = 3 %).")]
        [SerializeField, Range(0f, 0.2f)] private float _wobbleAmount = 0.035f;

        [Tooltip("Wobble speed, in radians per second (rad/s).")]
        [SerializeField, Min(0f)] private float _wobbleSpeed = 3.5f;

        [Header("Burst")]
        [Tooltip("One-shot effect spawned where the bubble pops; scaled to the bubble size.")]
        [SerializeField] private ParticleSystem _burstEffect;

        [Header("Audio")]
        [SerializeField] private AudioClip _formSound;
        [SerializeField] private AudioClip _burstSound;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        private float _formProgress;
        private float _wobblePhase;

        /// <summary>Radius of the fully formed bubble, in meters (m).</summary>
        public float Radius { get; private set; } = 1f;

        /// <summary>The visible shell; it is never part of the character's hierarchy.</summary>
        public Transform Shell => _shell != null ? _shell : transform;

        private void Start()
        {
            PlaySound(_formSound);
        }

        private void Update()
        {
            _wobblePhase += _wobbleSpeed * Time.deltaTime;
            ApplyScale();
        }

        /// <param name="radius">Radius of the fully formed bubble, in meters (m).</param>
        public void SetRadius(float radius)
        {
            Radius = Mathf.Max(0.01f, radius);

            if (_sparkles != null)
            {
                ParticleSystem.ShapeModule shape = _sparkles.shape;
                shape.radius = Radius;
            }

            ApplyScale();
        }

        /// <param name="progress">Growth from 0 (just appeared) to 1 (fully around the character).</param>
        public void SetFormProgress(float progress)
        {
            _formProgress = Mathf.Clamp01(progress);
            ApplyScale();
        }

        /// <summary>Pops: spawns the burst effect and sound where the bubble is, then removes the bubble.</summary>
        public void Burst()
        {
            if (_burstEffect != null)
            {
                ParticleSystem burst = Instantiate(_burstEffect, Shell.position, Quaternion.identity);
                burst.transform.localScale = Vector3.one * Radius;
                burst.Play(true);
            }

            PlaySound(_burstSound);
            DestroySelf();
        }

        /// <summary>Removes the bubble without popping (effect cancelled).</summary>
        public void Remove()
        {
            DestroySelf();
        }

        private void ApplyScale()
        {
            float size = Radius * 2f * Mathf.Max(0f, _formCurve.Evaluate(_formProgress));
            float wobble = _wobbleAmount * Mathf.Sin(_wobblePhase);
            float side = _wobbleAmount * Mathf.Sin(_wobblePhase * 1.3f + 1f);
            Shell.localScale = new Vector3(size * (1f + side), size * (1f + wobble), size * (1f - side));
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && Application.isPlaying)
                AudioSource.PlayClipAtPoint(clip, Shell.position, _volume);
        }

        private void DestroySelf()
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
