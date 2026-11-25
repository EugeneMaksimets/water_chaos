using UnityEngine;
using WaterChaos.Player;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tools
{
    /// <summary>
    /// Transfer hose coil as an item: while in the hands it tells the player's transfer interactor,
    /// so interact on a pump transfer outlet connects this hose there.
    /// </summary>
    [RequireComponent(typeof(TransferHose))]
    public sealed class TransferHoseItem : MonoBehaviour, IEquippable
    {
        private TransferHose _hose;
        private PlayerTransferHoseInteractor _interactor;

        private void Awake()
        {
            _hose = GetComponent<TransferHose>();
        }

        public void Equip(PlayerEquipment owner)
        {
            Unequip();

            _interactor = owner.GetComponent<PlayerTransferHoseInteractor>();
            if (_interactor == null)
            {
                Debug.LogError($"{name}: the player has no PlayerTransferHoseInteractor, the transfer hose cannot be connected.", this);
                return;
            }

            _interactor.HoldHose(_hose);
        }

        public void Unequip()
        {
            if (_interactor == null)
                return;

            _interactor.ReleaseHose(_hose);
            _interactor = null;
        }
    }
}
