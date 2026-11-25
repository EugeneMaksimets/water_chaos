using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Player;
using WaterChaos.Water;

namespace WaterChaos.Tools
{
    /// <summary>
    /// Nozzle in the hands: hands its stream and hose to the player's hose connector,
    /// so interact connects it to a source; "Water On/Off" (primary button or menu) and the mouse wheel control it.
    /// Unequipping takes the hose off and stops the source, so no water flows after switching.
    /// </summary>
    public sealed class NozzleController : MonoBehaviour, IEquippable
    {
        [Header("References")]
        [SerializeField] private WaterStream _stream;

        [Tooltip("Optional: the physical hose from the connected source to this nozzle.")]
        [SerializeField] private HoseController _hose;

        private PlayerHoseConnector _connector;

        private void Awake()
        {
            if (_stream == null)
                Debug.LogError($"{name}: NozzleController needs a Stream reference.", this);
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

            _connector.AttachNozzle(_stream, _hose);
        }

        public void Unequip()
        {
            if (_connector == null)
                return;

            _connector.DetachNozzle();
            _connector = null;
        }
    }
}
