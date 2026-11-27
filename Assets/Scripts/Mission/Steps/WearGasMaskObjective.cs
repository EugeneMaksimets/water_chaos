using UnityEngine;
using WaterChaos.Player;

namespace WaterChaos.Mission
{
    /// <summary>Put the gas mask on; done once the player wears it.</summary>
    public sealed class WearGasMaskObjective : ObjectiveStep
    {
        [Header("Gas Mask")]
        [SerializeField] private PlayerGasMask _mask;

        protected override void OnBegin()
        {
            if (_mask == null)
                Debug.LogError($"{name}: WearGasMaskObjective needs the player's PlayerGasMask.", this);

            base.OnBegin();
        }

        protected override bool Evaluate() => _mask != null && _mask.IsWorn;
    }
}
