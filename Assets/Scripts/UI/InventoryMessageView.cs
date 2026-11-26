using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>Inventory texts: the short "backpack is full" notice and the drop hint (keys from the bindings).</summary>
    public sealed class InventoryMessageView : MonoBehaviour
    {
        private const string FullText = "Рюкзак заповнений!";
        private string _dropText;

        [Header("References")]
        [SerializeField] private PlayerItemInteractor _interactor;
        [SerializeField] private PlayerItemThrower _thrower;

        [Tooltip("Shows the notice for a moment, then hides.")]
        [SerializeField] private Text _notice;

        [Tooltip("Drop hint; hidden when the selected slot is empty.")]
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

            _dropText = $"{PlayerControls.GetDisplayName(PlayerControl.Drop)} — покласти";

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
            bool visible = _thrower.CanDrop;
            _hint.gameObject.SetActive(visible);
            if (visible)
                _hint.text = _dropText;
        }
    }
}
