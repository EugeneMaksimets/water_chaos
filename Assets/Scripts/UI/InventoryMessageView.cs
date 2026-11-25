using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>Inventory texts: the short "backpack is full" notice and the throw / drop hint (keys from the bindings).</summary>
    public sealed class InventoryMessageView : MonoBehaviour
    {
        private const string FullText = "Backpack is full!";
        private string _readyText;
        private string _aimingText;

        [Header("References")]
        [SerializeField] private PlayerItemInteractor _interactor;
        [SerializeField] private PlayerItemThrower _thrower;

        [Tooltip("Shows the notice for a moment, then hides.")]
        [SerializeField] private Text _notice;

        [Tooltip("Throw hint; hidden when the selected slot is empty.")]
        [SerializeField] private Text _hint;

        [Header("Settings")]
        [Tooltip("How long the notice stays on screen, in seconds (s).")]
        [SerializeField, Min(0f)] private float _noticeDuration = 1.5f;

        private float _noticeTimeLeft;

        private void Awake()
        {
            if (_interactor == null || _thrower == null || _notice == null || _hint == null)
            {
                Debug.LogError($"{name}: InventoryMessageView needs Interactor, Thrower, Notice and Hint references.", this);
                enabled = false;
                return;
            }

            string primary = PlayerControls.GetDisplayName(PlayerControl.Primary);
            string drop = PlayerControls.GetDisplayName(PlayerControl.Drop);
            _readyText = $"Hold {primary} to throw · {drop} to drop";
            _aimingText = $"Release {primary} to throw";

            _notice.gameObject.SetActive(false);
            _hint.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _interactor.BackpackFull += OnBackpackFull;
        }

        private void OnDisable()
        {
            _interactor.BackpackFull -= OnBackpackFull;
        }

        private void Update()
        {
            UpdateNotice();
            UpdateHint();
        }

        private void OnBackpackFull()
        {
            _notice.text = FullText;
            _notice.gameObject.SetActive(true);
            _noticeTimeLeft = _noticeDuration;
        }

        private void UpdateNotice()
        {
            if (_noticeTimeLeft <= 0f)
                return;

            _noticeTimeLeft -= Time.deltaTime;
            if (_noticeTimeLeft <= 0f)
                _notice.gameObject.SetActive(false);
        }

        private void UpdateHint()
        {
            bool visible = _thrower.CanThrow;
            _hint.gameObject.SetActive(visible);
            if (visible)
                _hint.text = _thrower.IsAiming ? _aimingText : _readyText;
        }
    }
}
