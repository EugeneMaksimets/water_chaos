using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Soap
{
    /// <summary>
    /// Marks a character as a teammate that soapy water can lift in a bubble, and passes soapy hits to its
    /// <see cref="SoapBubbleController"/>. Plain water, gasoline and toxic liquid are ignored here, and objects
    /// without this component are never affected. Must sit on the GameObject with the character's collider:
    /// <see cref="WaterStream"/> looks for receivers on the collider it hits.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoapBubbleReceiver : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("References")]
        [Tooltip("Controller that runs the bubble; found on this object or a parent if empty.")]
        [SerializeField] private SoapBubbleController _controller;

        private SoapBubbleController Controller =>
            _controller != null ? _controller : _controller = GetComponentInParent<SoapBubbleController>();

        public void ReceiveImpact(in WaterImpact impact)
        {
            if (!enabled || !impact.IsSoapy || !(impact.Volume > 0f))
                return;

            // Repeated hits while a bubble is active (or right after landing) are ignored by the controller.
            if (Controller != null)
                Controller.TryCapture();
        }
    }
}
