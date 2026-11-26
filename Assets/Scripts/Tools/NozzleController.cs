using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Items;
using WaterChaos.Player;
using WaterChaos.Water;

namespace WaterChaos.Tools
{
    /// <summary>
    /// Nozzle in the hands: hands its stream and hose to the player's hose connector,
    /// so interact connects it to a source; "Water On/Off" (primary button or menu) and the mouse wheel control it.
    /// Storing it in the inventory takes the hose off; putting it down in the world keeps the hose attached
    /// (the source stops), and picking it up again restores the connection. No water flows after either.
    /// </summary>
    public sealed class NozzleController : MonoBehaviour, IEquippable
    {
        [Header("References")]
        [SerializeField] private WaterStream _stream;

        [Tooltip("Optional: the physical hose from the connected source to this nozzle.")]
        [SerializeField] private HoseController _hose;

        private PlayerHoseConnector _connector;
        private PickableItem _item;

        // Connection kept while the nozzle lies in the world; handed back to the connector on the next equip.
        private WaterConnectionPoint _keptConnection;

        private void Awake()
        {
            _item = GetComponentInParent<PickableItem>();

            if (_stream == null)
                Debug.LogError($"{name}: NozzleController needs a Stream reference.", this);
        }

        private void OnDestroy()
        {
            if (_keptConnection != null)
                _keptConnection.Detach();
        }

        public void Equip(PlayerEquipment owner)
        {
            Unequip();

            _connector = owner.GetComponent<PlayerHoseConnector>();
            if (_connector == null)
            {
                Debug.LogError($"{name}: the player has no PlayerHoseConnector, the nozzle cannot be connected.", this);
                return;
            }

            _connector.AttachNozzle(_stream, _hose, _keptConnection);
            _keptConnection = null;
        }

        public void Unequip()
        {
            if (_connector == null)
                return;

            // Thrown or dropped: already released into the world before unequipping.
            bool putDown = _item != null && !_item.IsHeld;
            if (putDown)
                _keptConnection = _connector.ReleaseNozzle();
            else
                _connector.DetachNozzle();

            _connector = null;
        }
    }
}
