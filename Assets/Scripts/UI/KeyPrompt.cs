using UnityEngine;
using UnityEngine.UI;

namespace WaterChaos.UI
{
    /// <summary>One round key prompt built by <see cref="HudWidgets.CreateKeyPrompt"/>; changes only what differs.</summary>
    public sealed class KeyPrompt
    {
        private readonly RectTransform _root;
        private readonly Image _progress;
        private readonly Text _label;

        public KeyPrompt(RectTransform root, Image progress, Text label)
        {
            _root = root;
            _progress = progress;
            _label = label;
        }

        public void Hide()
        {
            if (_root.gameObject.activeSelf)
                _root.gameObject.SetActive(false);
        }

        /// <param name="progress">Ring fill 0..1: hold progress, or 1 for a single press.</param>
        public void Show(string label, float x, float progress, bool problem = false)
        {
            if (!_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);

            if (!ReferenceEquals(_label.text, label))
                _label.text = label;

            _label.color = problem ? HudWidgets.ProblemColor : Color.white;
            _progress.fillAmount = progress;

            Vector2 position = _root.anchoredPosition;
            if (position.x != x)
                _root.anchoredPosition = new Vector2(x, position.y);
        }
    }
}
