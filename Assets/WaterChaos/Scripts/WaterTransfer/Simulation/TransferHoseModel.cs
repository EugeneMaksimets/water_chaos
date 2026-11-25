using System;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Pure rules of the transfer hose cycle: connect to a pump → carry the free end → place it →
    /// (pull it again) → disconnect. Every Try* returns false and changes nothing when the step is not allowed.
    /// </summary>
    public sealed class TransferHoseModel
    {
        /// <summary>Raised after any successful step.</summary>
        public event Action Changed;

        public TransferHoseState State { get; private set; } = TransferHoseState.Stored;

        /// <summary>The hose start is attached to a pump.</summary>
        public bool IsConnected => State != TransferHoseState.Stored;

        public bool CanConnect => State == TransferHoseState.Stored;
        public bool CanPlace => State == TransferHoseState.Carried;
        public bool CanPickUpEnd => State == TransferHoseState.Placed;
        public bool CanDisconnect => IsConnected;

        public bool TryConnect() => TrySet(CanConnect, TransferHoseState.Carried);

        public bool TryPlace() => TrySet(CanPlace, TransferHoseState.Placed);

        public bool TryPickUpEnd() => TrySet(CanPickUpEnd, TransferHoseState.Carried);

        public bool TryDisconnect() => TrySet(CanDisconnect, TransferHoseState.Stored);

        private bool TrySet(bool allowed, TransferHoseState state)
        {
            if (!allowed)
                return false;

            State = state;
            Changed?.Invoke();
            return true;
        }
    }
}
