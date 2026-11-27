using UnityEngine;
using UnityEngine.Serialization;
using WaterChaos.Hose;
using WaterChaos.Items;
using WaterChaos.Player;
using WaterChaos.Water;

namespace WaterChaos.Tools
{
    /// <summary>
    /// A hose-end item in the hands (a nozzle, or the input of a hose splitter): hands what its hose feeds
    /// (the nozzle stream, the splitter) and the hose to the player's hose connector, so interact connects it to a source;
    /// "Water On/Off" (primary button) and the mouse wheel control that source.
    /// Storing it in the inventory takes the hose off; putting it down in the world keeps the hose attached
    /// (the source stops), and picking it up again restores the connection. No water flows after either.
    /// </summary>
    public sealed class NozzleController : MonoBehaviour, IEquippable
    {
        [Header("References")]
        [Tooltip("What the hose feeds: the nozzle's WaterStream, or a HoseSplitter (any IWaterFlowReceiver).")]
        [SerializeField, FormerlySerializedAs("_stream")] private MonoBehaviour _receiver;

        [Tooltip("Optional: the physical hose from the connected source to this item.")]
        [SerializeField] private HoseController _hose;

        [Header("Menu")]
        [Tooltip("Name in the menu: \"Connect <name>\" / \"Disconnect <name>\".")]
        [SerializeField] private string _displayName = "брандспойт";

        private IWaterFlowReceiver _stream;

        private PlayerHoseConnector _connector;
        private PickableItem _item;

        // Connection kept while the nozzle lies in the world; handed back to the connector on the next equip.
        private WaterConnectionPoint _keptConnection;
        private System.Action<WaterConnectionPoint> _onKeptLost;

        private void Awake()
        {
            _item = GetComponentInParent<PickableItem>();
            _onKeptLost = OnKeptConnectionLost;

            _stream = _receiver as IWaterFlowReceiver;
            if (_stream == null)
                Debug.LogError($"{name}: NozzleController needs a Receiver that implements IWaterFlowReceiver.", this);
        }

        private void OnDestroy()
        {
            if (_keptConnection != null)
                _keptConnection.Detach();
            SetKeptConnection(null);
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

            WaterConnectionPoint kept = _keptConnection;
            SetKeptConnection(null);
            _connector.AttachNozzle(_stream, _hose, kept, _displayName);
        }

        public void Unequip()
        {
            if (_connector == null)
                return;

            // Thrown or dropped: already released into the world before unequipping.
            bool putDown = _item != null && !_item.IsHeld;
            if (putDown)
                SetKeptConnection(_connector.ReleaseNozzle());
            else
                _connector.DetachNozzle();

            _connector = null;
        }

        private void SetKeptConnection(WaterConnectionPoint point)
        {
            if (_keptConnection != null)
                _keptConnection.AttachmentLost -= _onKeptLost;

            _keptConnection = point;

            if (_keptConnection != null)
                _keptConnection.AttachmentLost += _onKeptLost;
        }

        /// <summary>The source left the world while the nozzle lay there: the stream and hose let go, nothing stays attached.</summary>
        private void OnKeptConnectionLost(WaterConnectionPoint point)
        {
            SetKeptConnection(null);
            if (_stream != null)
                _stream.SetSource(null);
            if (_hose != null)
                _hose.Disconnect();
        }
    }
}
