using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>Shows "Hose length reached!" while a hose (the nozzle hose or a carried transfer hose) holds the player back.</summary>
    public sealed class HoseMessageView : MonoBehaviour
    {
        private const string LimitText = "Шланг натягнуто до кінця!";

        [Header("References")]
        [SerializeField] private PlayerHoseConnector _hoseConnector;

        [Tooltip("Transfer hose side of the player. Empty = taken from the Hose Connector's object.")]
        [SerializeField] private PlayerTransferHoseInteractor _transferHose;

        [Tooltip("Text shown while a hose is at full length; hidden otherwise.")]
        [SerializeField] private Text _message;

        // This view put the label on screen (and so may take it off).
        private bool _shown;

        private void Awake()
        {
            if (_hoseConnector == null || _message == null)
            {
                Debug.LogError($"{name}: HoseMessageView needs Hose Connector and Message references.", this);
                enabled = false;
                return;
            }

            if (_transferHose == null)
                _transferHose = _hoseConnector.GetComponent<PlayerTransferHoseInteractor>();

            _message.text = LimitText;
            _message.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_message != null && _shown)
                _message.gameObject.SetActive(false);
            _shown = false;
        }

        // Polled: the transfer hose has no change event, and two flags per frame cost nothing.
        private void LateUpdate()
        {
            bool reached = _hoseConnector.IsLimitReached
                || (_transferHose != null && _transferHose.IsLimitReached);

            // The label is shared with other notices (the inventory's "backpack is full"):
            // the text is set every time it shows, and only a label this view showed is hidden again.
            if (reached)
            {
                if (!ReferenceEquals(_message.text, LimitText))
                    _message.text = LimitText;
                if (!_message.gameObject.activeSelf)
                    _message.gameObject.SetActive(true);
            }
            else if (_shown)
            {
                _message.gameObject.SetActive(false);
            }

            _shown = reached;
        }
    }
}
