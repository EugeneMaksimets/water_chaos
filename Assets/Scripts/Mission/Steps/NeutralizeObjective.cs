using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Pump;
using WaterChaos.Soap;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Neutralize the gasoline in a tank with toxic liquid and soap together.
    /// Toxin counts once the gasoline has been neutralized at least once (gasoline + toxic turn into water,
    /// see <see cref="LiquidTypeExtensions.MixWith"/>); pumping on would turn the water toxic, so it is latched.
    /// Soap counts once the bar lies in the tank or has dissolved there.
    /// When both are done the listed pumps stop and the tank holds clean water: the teammates' streams follow it,
    /// so the flamethrowers go out by themselves.
    /// </summary>
    public sealed class NeutralizeObjective : ObjectiveStep
    {
        [Header("Tank")]
        [Tooltip("The tank with gasoline that feeds the teammates (on its WaterBody object).")]
        [SerializeField] private WaterReservoirComponent _tank;

        [Tooltip("The soap the player throws into the tank.")]
        [SerializeField] private SoapItem _soap;

        [Tooltip("Pumps stopped when the gasoline is neutralized (the one pumping toxic liquid in).")]
        [SerializeField] private PumpController[] _stopPumps = new PumpController[0];

        [Header("Markers")]
        [Tooltip("Shown until the toxin is in (e.g. on the neutralizing pump).")]
        [SerializeField] private MissionMarker _toxinMarker;

        [Tooltip("Shown until the soap is in the tank (on the soap).")]
        [SerializeField] private MissionMarker _soapMarker;

        private WaterBody _body;
        private bool _toxinDone;
        private bool _soapDone;

        protected override string Progress => $"{(_toxinDone ? 1 : 0) + (_soapDone ? 1 : 0)}/2";

        protected override void OnBegin()
        {
            if (_tank == null || _soap == null)
                Debug.LogError($"{name}: NeutralizeObjective needs Tank and Soap references.", this);

            _body = _tank != null ? _tank.GetComponent<WaterBody>() : null;
            base.OnBegin();
        }

        protected override bool Evaluate()
        {
            WaterReservoir tank = _tank != null ? _tank.Reservoir : null;
            if (tank == null)
                return false;

            _toxinDone |= !tank.IsEmpty && !tank.Liquid.IsFlammable();
            _soapDone |= IsSoapInTank(tank);

            SetMarkerShown(_toxinMarker, !_toxinDone);
            SetMarkerShown(_soapMarker, !_soapDone);
            return _toxinDone && _soapDone;
        }

        protected override void OnEnd(bool completed)
        {
            base.OnEnd(completed);
            if (!completed || _tank == null || _tank.Reservoir == null)
                return;

            foreach (PumpController pump in _stopPumps)
            {
                if (pump != null)
                    pump.Stop();
            }

            // Toxin and soap together leave clean water, whatever was pumped in after the gasoline was gone.
            WaterReservoir tank = _tank.Reservoir;
            float volume = tank.Volume;
            tank.Empty();
            tank.Add(volume, LiquidType.Water);

            if (_soap != null && !_soap.GetComponent<PickableItem>().IsHeld && _soap.gameObject.activeInHierarchy)
                Destroy(_soap.gameObject);
        }

        // Dissolved (destroyed) in the water, or lying in the tank; not while in the inventory or in the hands.
        private bool IsSoapInTank(WaterReservoir tank)
        {
            if (_soap == null)
                return tank.IsSoapy;

            PickableItem item = _soap.GetComponent<PickableItem>();
            return _soap.gameObject.activeInHierarchy && (item == null || !item.IsHeld)
                && _body != null && _body.Contains(_soap.transform.position);
        }
    }
}
