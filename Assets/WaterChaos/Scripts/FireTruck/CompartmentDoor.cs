using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.FireTruck
{
    /// <summary>
    /// Equipment compartment door on a fire truck: the interaction menu opens and closes it,
    /// and it swings around its pivot (the hinge) by the open angle.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CompartmentDoor : MonoBehaviour, IInteractionProvider, IDisplayName
    {
        [Header("Swing")]
        [Tooltip("Rotation around the hinge (local Y) when fully open, in degrees; the sign picks the side.")]
        [SerializeField] private float _openAngle = 100f;

        [Tooltip("Open-ratio per second (1/s): 2 = opens fully in 0.5 s.")]
        [SerializeField, Min(0.01f)] private float _speed = 2f;

        private CompartmentDoorModel _model;
        private Quaternion _closedRotation;
        private Action _toggle;

        public string DisplayName => "Compartment";

        private void Awake()
        {
            _model = new CompartmentDoorModel(_speed);
            _closedRotation = transform.localRotation;
            _toggle = _model.Toggle;
        }

        private void Update()
        {
            _model.Tick(Time.deltaTime);
            transform.localRotation = _closedRotation * Quaternion.Euler(0f, _openAngle * _model.OpenRatio, 0f);
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            actions.Add(new InteractionAction(_model.IsOpen ? "Close" : "Open", this, _toggle));
        }
    }
}
