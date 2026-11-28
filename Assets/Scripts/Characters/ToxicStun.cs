using System;
using UnityEngine;
using WaterChaos.Common;
using WaterChaos.Interaction;
using WaterChaos.Player;
using WaterChaos.Surfaces;
using WaterChaos.Teammate;
using WaterChaos.Water;

namespace WaterChaos.Characters
{
    /// <summary>
    /// A toxic stream from someone else's nozzle stuns the character for a while (<see cref="ToxicStunModel"/>):
    /// its controls are switched off, it keeps turning to whoever hit it (the player's view, a teammate's nozzle),
    /// and green fumes show above it (<see cref="ToxicExposure.ExposeFor"/>). A gas mask protects from it.
    /// Sits next to the character's collider, like every impact receiver.
    /// </summary>
    public sealed class ToxicStun : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Stun")]
        [Tooltip("How long a toxic hit stuns, in seconds (s).")]
        [SerializeField, Min(0f)] private float _duration = 5f;

        [Tooltip("After a stun, no new hit stuns for this long, in seconds (s).")]
        [SerializeField, Min(0f)] private float _immunity = 3f;

        [Tooltip("Switched off while stunned (the player's input, movement and look).")]
        [SerializeField] private Behaviour[] _suspend = new Behaviour[0];

        [Header("Turn To The Shooter")]
        [Tooltip("Player: the view that turns to whoever hit them.")]
        [SerializeField] private PlayerLook _look;

        [Tooltip("Teammate: the nozzle that aims at whoever hit them.")]
        [SerializeField] private TeammateAim _aim;

        [Tooltip("How fast the player's view turns to the shooter, in degrees per second (°/s).")]
        [SerializeField, Min(0f)] private float _turnSpeed = 240f;

        [Header("Look")]
        [Tooltip("Shows the green fumes / toxic screen while stunned; the one on this object otherwise.")]
        [SerializeField] private ToxicExposure _exposure;

        private ToxicStunModel _model;
        private Transform _shooter;
        private bool _locked;

        /// <summary>Raised when any character gets stunned (e.g. to show a tip once).</summary>
        public static event Action<ToxicStun> AnyStunned;

        public bool IsStunned => _model != null && _model.IsStunned(Time.time);

        private void Awake()
        {
            _model = new ToxicStunModel(_duration, _immunity);
            if (_exposure == null)
                _exposure = GetComponent<ToxicExposure>();
            if (_look == null)
                _look = GetComponent<PlayerLook>();
            if (_aim == null)
                _aim = GetComponentInChildren<TeammateAim>();
        }

        private void OnDisable()
        {
            Release();
        }

        // Called during FixedUpdate by WaterStream.
        public void ReceiveImpact(in WaterImpact impact)
        {
            Transform shooter = impact.Source;
            if (impact.Liquid != LiquidType.Toxic || shooter == null || shooter.IsChildOf(transform))
                return;
            if (_exposure != null && _exposure.IsProtected)
                return;
            if (!_model.TryStun(Time.time))
                return;

            _shooter = shooter;
            Lock();
            if (_aim != null)
                _aim.ForceTarget(shooter, _duration);
            if (_exposure != null)
                _exposure.ExposeFor(_duration);

            AnyStunned?.Invoke(this);
        }

        private void Update()
        {
            if (!_locked)
                return;

            if (!IsStunned)
            {
                Release();
                return;
            }

            if (_look != null && _shooter != null)
                _look.TurnTowards(_shooter.position, _turnSpeed * Time.deltaTime);
        }

        private void Lock()
        {
            _locked = true;
            foreach (Behaviour part in _suspend)
                BehaviourLocks.Lock(part, this);
        }

        private void Release()
        {
            if (!_locked)
                return;

            _locked = false;
            _shooter = null;
            foreach (Behaviour part in _suspend)
                BehaviourLocks.Unlock(part, this);
        }
    }
}
