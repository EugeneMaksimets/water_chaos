using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Items;

namespace WaterChaos.UI
{
    /// <summary>One inventory slot on screen: item icon, empty look, selection frame and slot number.</summary>
    public sealed class InventorySlotView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Slot background; its color shows whether the slot is empty.")]
        [SerializeField] private Image _background;

        [Tooltip("Shows the stored item's icon; hidden when the slot is empty or the item has no icon.")]
        [SerializeField] private Image _icon;

        [Tooltip("Highlight frame, active only on the selected slot.")]
        [SerializeField] private GameObject _selectionFrame;

        [Tooltip("Slot number label: 1, 2, 3.")]
        [SerializeField] private Text _number;

        [Header("Colors")]
        [SerializeField] private Color _filledColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color _emptyColor = new Color(0f, 0f, 0f, 0.3f);

        /// <param name="number">Number shown to the player, starting from 1.</param>
        public void SetNumber(int number)
        {
            if (_number != null)
                _number.text = number.ToString();
        }

        /// <summary>Shows the item, or the empty look for null.</summary>
        public void SetItem(IPickableItem item)
        {
            if (_background != null)
                _background.color = item != null ? _filledColor : _emptyColor;

            if (_icon == null)
                return;

            Sprite sprite = item != null ? item.Icon : null;
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        public void SetSelected(bool selected)
        {
            if (_selectionFrame != null)
                _selectionFrame.SetActive(selected);
        }
    }
}
