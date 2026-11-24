using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Hose;

namespace WaterChaos.UI
{
    /// <summary>Shows "Hose length reached!" while the hose holds the player back.</summary>
    public sealed class HoseMessageView : MonoBehaviour
    {
        private const string LimitText = "Hose length reached!";

        [Header("References")]
        [SerializeField] private HoseController _hose;

        [Tooltip("Text shown while the hose is at full length; hidden otherwise.")]
        [SerializeField] private Text _message;

        private void Awake()
        {
            if (_hose == null || _message == null)
            {
                Debug.LogError($"{name}: HoseMessageView needs Hose and Message references.", this);
                enabled = false;
                return;
            }

            _message.text = LimitText;
            _message.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_hose == null)
                return;

            _hose.LimitReachedChanged += OnLimitReachedChanged;
            OnLimitReachedChanged(_hose.IsLimitReached);
        }

        private void OnDisable()
        {
            if (_hose != null)
                _hose.LimitReachedChanged -= OnLimitReachedChanged;
        }

        private void OnLimitReachedChanged(bool reached)
        {
            if (_message != null)
                _message.gameObject.SetActive(reached);
        }
    }
}
