using UnityEngine;
using WaterChaos.Soap;

namespace WaterChaos.Mission
{
    /// <summary>Lift a teammate in a soap bubble (spray them with soapy water); done once any of them rises.</summary>
    public sealed class LiftTeammateObjective : ObjectiveStep
    {
        [Header("Teammates")]
        [SerializeField] private SoapBubbleController[] _teammates = new SoapBubbleController[0];

        protected override bool Evaluate()
        {
            foreach (SoapBubbleController teammate in _teammates)
            {
                if (teammate != null && teammate.Phase == SoapBubblePhase.Rising)
                    return true;
            }

            return false;
        }
    }
}
