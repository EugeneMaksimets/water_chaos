using UnityEngine;
using WaterChaos.Teammate;

namespace WaterChaos.Player
{
    /// <summary>
    /// A teammate stands idle while nothing comes out of their nozzle (the hydrant ran dry) and goes back
    /// to extinguishing as soon as it sprays again.
    /// </summary>
    public sealed class TeammateSprayAnimation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TeammateNozzle _nozzle;
        [SerializeField] private FirefighterAnimationSwitcher _animation;

        private void Update()
        {
            if (_nozzle == null || _animation == null)
                return;

            _animation.Animation = _nozzle.IsSpraying ? FirefighterAnimation.Extinguish : FirefighterAnimation.Idle;
        }
    }
}
