using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Teammate;
using WaterChaos.UI;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Waits until the teammates really spray the expected liquid (gasoline, toxic liquid) before the story talks
    /// about it. Supplied already: done at once, nothing shown. Otherwise, after a short moment, an objective line and a
    /// marker show what to fix: the pump that feeds the tee is off, or the hose is not on the tee.
    /// </summary>
    public sealed class TeammateSupplyStep : MissionStep
    {
        // Below this the teammates only trickle, in liters per second (L/s).
        private const float MinFlow = 0.1f;

        [Header("Supply")]
        [SerializeField] private TeammateNozzle[] _teammates = new TeammateNozzle[0];

        [Tooltip("The liquid the teammates must be spraying.")]
        [SerializeField] private LiquidType _liquid = LiquidType.Gasoline;

        [Tooltip("The pump that feeds the tee.")]
        [SerializeField] private PumpController _pump;

        [Header("When It Is Missing")]
        [SerializeField] private MissionMarker _pumpMarker;
        [SerializeField] private MissionMarker _teeMarker;

        [SerializeField, TextArea(1, 3)] private string _pumpTitle = "Увімкни насос у підземному резервуарі — напарники без подачі";
        [SerializeField, TextArea(1, 3)] private string _hoseTitle = "Під'єднай шланг від підземного резервуара до трійника";

        [Tooltip("Seconds (s) to wait before the problem is shown (the liquid may be on its way).")]
        [SerializeField, Min(0f)] private float _grace = 1f;

        private ObjectiveLine _line;
        private MissionMarker _marker;
        private string _title;
        private float _waiting;

        protected override void OnBegin()
        {
            _waiting = 0f;
        }

        protected override void OnTick()
        {
            if (IsSupplied())
            {
                Complete();
                return;
            }

            _waiting += Time.deltaTime;
            if (_waiting < _grace)
                return;

            bool pumpOff = _pump != null && !_pump.isTurnOn;
            Show(pumpOff ? _pumpTitle : _hoseTitle, pumpOff ? _pumpMarker : _teeMarker);
        }

        protected override void OnEnd(bool completed)
        {
            if (_marker != null)
                _marker.Hide();
            _marker = null;

            if (_line != null)
            {
                if (completed)
                    _line.Complete();
                else
                    _line.Dismiss();
                _line = null;
            }
        }

        private bool IsSupplied()
        {
            foreach (TeammateNozzle teammate in _teammates)
            {
                if (teammate == null)
                    continue;

                WaterFlowData flow = teammate.Flow;
                if (flow.FlowRate > MinFlow && flow.Liquid == _liquid)
                    return true;
            }

            return false;
        }

        private void Show(string title, MissionMarker marker)
        {
            if (title != _title)
            {
                if (_line != null)
                    _line.Dismiss();
                _line = Runner.Objectives.Add(title);
                _title = title;
            }

            if (marker == _marker)
                return;

            if (_marker != null)
                _marker.Hide();
            _marker = marker;
            if (_marker != null)
                _marker.Show();
        }
    }
}
