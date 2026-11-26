using UnityEngine;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Lay a transfer hose: its start on one of the listed pump outlets (empty = any outlet)
    /// and its free end in one of the listed water bodies or plugged into one of the listed sockets
    /// (both lists empty = only the start matters).
    /// </summary>
    public sealed class TransferHoseObjective : ObjectiveStep
    {
        [Header("Hose")]
        [Tooltip("Any of these hoses counts.")]
        [SerializeField] private TransferHose[] _hoses = new TransferHose[0];

        [Tooltip("Pump outlets the hose start must be on; empty = any.")]
        [SerializeField] private TransferConnectionPoint[] _startPoints = new TransferConnectionPoint[0];

        [Header("Free End")]
        [Tooltip("Water bodies the free end may lie in (e.g. the underground tank).")]
        [SerializeField] private WaterBody[] _endBodies = new WaterBody[0];

        [Tooltip("Sockets the free end may be plugged into (e.g. the splitter inlet).")]
        [SerializeField] private HoseEndSocket[] _endSockets = new HoseEndSocket[0];

        private WaterTransferController[] _transfers;

        protected override void OnBegin()
        {
            _transfers = new WaterTransferController[_hoses.Length];
            for (int i = 0; i < _hoses.Length; i++)
                _transfers[i] = _hoses[i] != null ? _hoses[i].GetComponent<WaterTransferController>() : null;

            base.OnBegin();
        }

        protected override bool Evaluate()
        {
            for (int i = 0; i < _hoses.Length; i++)
            {
                if (_hoses[i] != null && IsStartOk(_hoses[i]) && IsEndOk(_hoses[i], _transfers[i]))
                    return true;
            }

            return false;
        }

        private bool IsStartOk(TransferHose hose)
        {
            if (hose.ConnectedPoint == null)
                return false;

            return _startPoints.Length == 0 || System.Array.IndexOf(_startPoints, hose.ConnectedPoint) >= 0;
        }

        private bool IsEndOk(TransferHose hose, WaterTransferController transfer)
        {
            if (_endBodies.Length == 0 && _endSockets.Length == 0)
                return true;

            if (hose.ConnectedSocket != null && System.Array.IndexOf(_endSockets, hose.ConnectedSocket) >= 0)
                return true;

            return transfer != null && transfer.DestinationBody != null
                && System.Array.IndexOf(_endBodies, transfer.DestinationBody) >= 0;
        }
    }
}
