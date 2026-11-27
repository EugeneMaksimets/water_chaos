using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Pump;
using WaterChaos.Soap;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Get rid of the gasoline in a tank with toxic liquid and soap.
    /// Toxin counts once the gasoline is gone (toxic liquid wins over gasoline, see <see cref="LiquidTypeExtensions.MixWith"/>);
    /// it is latched.
    /// Soap counts once the bar lies in the tank or has dissolved there.
    /// When both are done the listed pumps stop and the tank holds soapy water: the teammates' streams follow it,
    /// so the flamethrowers go out by themselves.
    /// Toxin and soap can also be two separate steps (<c>Need Toxin</c> / <c>Need Soap</c>): the toxin step stops the
    /// pumps and leaves plain water; the soap step turns it into soapy water.
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

        [Header("What Counts")]
        [Tooltip("The toxin has to neutralize the gasoline in this step.")]
        [SerializeField] private bool _needToxin = true;

        [Tooltip("The soap has to go into the tank in this step.")]
        [SerializeField] private bool _needSoap = true;

        [Header("Markers")]
        [Tooltip("Shown until the toxin is in (e.g. on the neutralizing pump).")]
        [SerializeField] private MissionMarker _toxinMarker;

        [Tooltip("Shown until the soap is in the tank (on the soap).")]
        [SerializeField] private MissionMarker _soapMarker;

        private WaterBody _body;
        private bool _toxinDone;
        private bool _soapDone;

        protected override string Progress =>
            _needToxin && _needSoap ? $"{(_toxinDone ? 1 : 0) + (_soapDone ? 1 : 0)}/2" : null;

        protected override void OnBegin()
        {
            if (_tank == null || (_needSoap && _soap == null))
                Debug.LogError($"{name}: NeutralizeObjective needs Tank (and Soap, if it needs soap) references.", this);

            _body = _tank != null ? _tank.GetComponent<WaterBody>() : null;
            base.OnBegin();
        }

        protected override bool Evaluate()
        {
            WaterReservoir tank = _tank != null ? _tank.Reservoir : null;
            if (tank == null)
                return false;

            _toxinDone |= !_needToxin || (!tank.IsEmpty && !tank.Liquid.IsFlammable());
            _soapDone |= !_needSoap || IsSoapInTank(tank);

            SetMarkerShown(_toxinMarker, _needToxin && !_toxinDone);
            SetMarkerShown(_soapMarker, _needSoap && !_soapDone);
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

            // A toxin-only step leaves the toxic liquid as it is; the soap comes in a later step.
            if (!_needSoap)
                return;

            // Toxin and soap together leave soapy water, whatever was pumped in after the gasoline was gone;
            // from now on nothing pumped in can spoil it (see WaterReservoir.Add).
            WaterReservoir tank = _tank.Reservoir;
            float volume = tank.Volume;
            tank.Empty();
            tank.Add(volume, LiquidType.Water, true);

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
