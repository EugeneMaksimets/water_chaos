using System;
using UnityEngine;
using WaterChaos.Surfaces;

namespace WaterChaos.Player
{
    /// <summary>
    /// Whether the player wears the gas mask (put on and taken off with <see cref="GasMaskItem"/>): while it is on,
    /// toxic air, puddles and sprays do not affect them (<see cref="ToxicExposure.SetProtected"/>).
    /// Sits on the player next to their ToxicExposure.
    /// </summary>
    [RequireComponent(typeof(ToxicExposure))]
    public sealed class PlayerGasMask : MonoBehaviour
    {
        private ToxicExposure _exposure;

        public bool IsWorn { get; private set; }

        /// <summary>Raised when the mask is put on (true) or taken off (false).</summary>
        public event Action<bool> WornChanged;

        private void Awake()
        {
            _exposure = GetComponent<ToxicExposure>();
        }

        public void SetWorn(bool worn)
        {
            if (IsWorn == worn)
                return;

            IsWorn = worn;
            _exposure.SetProtected(worn);
            WornChanged?.Invoke(worn);
        }
    }
}
