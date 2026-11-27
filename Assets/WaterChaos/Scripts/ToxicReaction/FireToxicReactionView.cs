using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.ToxicReaction
{
    /// <summary>
    /// Shows a fire's toxic reaction: the flames turn green (particle colors swapped, original ones restored after),
    /// flare up with a burst of extra flames, the light goes green and the fire bubbles.
    /// Display only: everything comes from <see cref="FireToxicReaction"/>.
    /// </summary>
    [RequireComponent(typeof(FireToxicReaction))]
    public sealed class FireToxicReactionView : MonoBehaviour
    {
        private static readonly Color ToxicLight = new Color(0.45f, 1f, 0.2f);

        [Header("Parts")]
        [Tooltip("Bubbling fumes played while the fire reacts (not tinted).")]
        [SerializeField] private ParticleSystem _bubbles;

        [Header("Look")]
        [SerializeField] private Color _toxicFlame = new Color(0.4f, 1f, 0.12f, 1f);
        [SerializeField] private Color _toxicFlameTip = new Color(0.8f, 1f, 0.25f, 1f);

        [Tooltip("Extra flames emitted by each fire particle system when the reaction starts (the flare).")]
        [SerializeField, Range(0, 50)] private int _flareBurst = 12;

        private struct Original
        {
            public ParticleSystem Particles;
            public ParticleSystem.MinMaxGradient StartColor;
            public ParticleSystem.MinMaxGradient LifetimeColor;
        }

        private readonly List<Original> _flames = new List<Original>();
        private FireToxicReaction _reaction;
        private Light _light;
        private Color _lightColor;
        private bool _toxic;

        private void Awake()
        {
            _reaction = GetComponent<FireToxicReaction>();
            _light = GetComponentInChildren<Light>(true);
            if (_light != null)
                _lightColor = _light.color;

            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (_bubbles != null && particles.transform.IsChildOf(_bubbles.transform))
                    continue;

                _flames.Add(new Original
                {
                    Particles = particles,
                    // Deep copies: the gradients Unity returns may still change with the module.
                    StartColor = Copy(particles.main.startColor),
                    LifetimeColor = Copy(particles.colorOverLifetime.color),
                });
            }
        }

        private void OnEnable()
        {
            _reaction.ReactionStarted += OnStarted;
            _reaction.ReactionEnded += OnEnded;
        }

        private void OnDisable()
        {
            _reaction.ReactionStarted -= OnStarted;
            _reaction.ReactionEnded -= OnEnded;
            SetToxic(false);
        }

        private void OnStarted(int generation)
        {
            SetToxic(true);
            foreach (Original flame in _flames)
            {
                if (flame.Particles != null && flame.Particles.isPlaying)
                    flame.Particles.Emit(_flareBurst);
            }
        }

        private void OnEnded()
        {
            SetToxic(false);
        }

        private void SetToxic(bool toxic)
        {
            if (_toxic == toxic)
                return;

            _toxic = toxic;
            foreach (Original flame in _flames)
            {
                if (flame.Particles == null)
                    continue;

                ParticleSystem.MainModule main = flame.Particles.main;
                ParticleSystem.ColorOverLifetimeModule lifetime = flame.Particles.colorOverLifetime;
                if (toxic)
                {
                    main.startColor = new ParticleSystem.MinMaxGradient(_toxicFlame, _toxicFlameTip);
                    // Keep the fade (alpha) of the flames, drop their own colors.
                    if (lifetime.enabled)
                        lifetime.color = WhiteWithAlphaOf(flame.LifetimeColor);
                }
                else
                {
                    main.startColor = flame.StartColor;
                    lifetime.color = flame.LifetimeColor;
                }
            }

            if (_light != null)
                _light.color = toxic ? ToxicLight : _lightColor;

            if (_bubbles != null)
            {
                if (toxic)
                    _bubbles.Play();
                else
                    _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static ParticleSystem.MinMaxGradient Copy(ParticleSystem.MinMaxGradient source)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Copy(source.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Copy(source.gradientMin), Copy(source.gradientMax));
                case ParticleSystemGradientMode.RandomColor:
                    return new ParticleSystem.MinMaxGradient(Copy(source.gradient)) { mode = ParticleSystemGradientMode.RandomColor };
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(source.colorMin, source.colorMax);
                default:
                    return new ParticleSystem.MinMaxGradient(source.color);
            }
        }

        private static Gradient Copy(Gradient source)
        {
            if (source == null)
                return null;

            var copy = new Gradient { mode = source.mode };
            copy.SetKeys((GradientColorKey[])source.colorKeys.Clone(), (GradientAlphaKey[])source.alphaKeys.Clone());
            return copy;
        }

        private static ParticleSystem.MinMaxGradient WhiteWithAlphaOf(ParticleSystem.MinMaxGradient source)
        {
            Gradient original = source.mode == ParticleSystemGradientMode.TwoGradients ? source.gradientMax : source.gradient;
            var gradient = new Gradient();
            GradientAlphaKey[] alpha = original != null
                ? original.alphaKeys
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            return new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
