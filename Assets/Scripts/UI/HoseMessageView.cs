using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>Shows "Hose length reached!" while the hose holds the player back.</summary>
    public sealed class HoseMessageView : MonoBehaviour
    {
        private const string LimitText = "Hose length reached!";

        [Header("References")]
        [SerializeField] private PlayerHoseConnector _hoseConnector;

        [Tooltip("Text shown while the hose is at full length; hidden otherwise.")]
        [SerializeField] private Text _message;

        private void Awake()
        {
            if (_hoseConnector == null || _message == null)
            {
                Debug.LogError($"{name}: HoseMessageView needs Hose Connector and Message references.", this);
                enabled = false;
                return;
            }

            _message.text = LimitText;
            _message.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_hoseConnector == null)
                return;

            _hoseConnector.LimitReachedChanged += OnLimitReachedChanged;
            OnLimitReachedChanged(_hoseConnector.IsLimitReached);
        }

        private void OnDisable()
        {
            if (_hoseConnector != null)
                _hoseConnector.LimitReachedChanged -= OnLimitReachedChanged;
        }

        private void OnLimitReachedChanged(bool reached)
        {
            if (_message != null)
                _message.gameObject.SetActive(reached);
        }
    }
}
