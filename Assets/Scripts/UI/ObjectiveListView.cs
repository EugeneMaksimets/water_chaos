using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterChaos.UI
{
    /// <summary>
    /// The list of current mission objectives on the left of the screen, in the cartoon card look of the HUD
    /// (rounded card, colored outline, dark inner panel, outlined text). Objectives add lines and keep their
    /// <see cref="ObjectiveLine"/> handles; this view only lays the lines out and animates them.
    /// Builds its own overlay canvas; runs on real time, so it keeps working while the game is paused.
    /// </summary>
    public sealed class ObjectiveListView : MonoBehaviour
    {
        private const float CheckSize = 30f;
        private const float BaseHeight = 56f;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        [Header("Layout")]
        [Tooltip("Top-left corner of the list from the screen's top-left corner, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 _offset = new Vector2(40f, -40f);

        [Tooltip("Card width, in reference pixels.")]
        [SerializeField, Min(200f)] private float _width = 430f;

        [Tooltip("Gap between cards, in reference pixels.")]
        [SerializeField, Min(0f)] private float _spacing = 10f;

        [Tooltip("Draw order of the list canvas.")]
        [SerializeField] private int _sortingOrder = 10;

        [Header("Timing")]
        [Tooltip("How long a completed objective stays with its check mark, in seconds (s).")]
        [SerializeField, Min(0f)] private float _completedHold = 2f;

        [Tooltip("Slide-in, fade-out and close-up time, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.35f;

        [Tooltip("How fast cards move to their new place when one leaves (1/s).")]
        [SerializeField, Min(0.1f)] private float _layoutSpeed = 10f;

        private readonly List<ObjectiveLine> _lines = new List<ObjectiveLine>();
        private RectTransform _root;
        private Sprite _rounded;
        private Sprite _disc;
        private Sprite _ring;
        private Sprite _tick;

        private void Awake()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Objective List Canvas", _sortingOrder);
            _root = CreateRect("Objectives", canvas.transform, _offset, new Vector2(_width, 0f));

            _rounded = HudWidgets.CreateRoundedSprite(64, 18);
            _disc = HudWidgets.CreateCircleSprite(64, 0f);
            _ring = HudWidgets.CreateCircleSprite(64, 0.78f);
            _tick = HudWidgets.CreateCheckSprite(64);
        }

        /// <summary>Adds an objective line at the bottom of the list.</summary>
        public ObjectiveLine Add(string title)
        {
            RectTransform root = CreateRect("Objective", _root, Vector2.zero, new Vector2(_width, BaseHeight));
            RectTransform card = CreateRect("Card", root, Vector2.zero, new Vector2(_width, BaseHeight));
            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Sliced(CreateImage("Shadow", card, new Vector2(5f, -5f), HudWidgets.CardShadowColor, _rounded));
            Image outline = Sliced(CreateImage("Outline", card, Vector2.zero, HudWidgets.AccentColor, _rounded));
            Image inner = Sliced(CreateImage("Inner", card, Vector2.zero, HudWidgets.CardInnerColor, _rounded));
            Stretch(inner.rectTransform, 5f);

            // Check box: a ring, filled with a green disc and a tick on completion.
            RectTransform box = HudWidgets.CreateRect("Check Box", card, Vector2.zero, new Vector2(CheckSize, CheckSize));
            box.anchorMin = box.anchorMax = new Vector2(0f, 1f);
            box.anchoredPosition = new Vector2(30f, -BaseHeight * 0.5f);
            HudWidgets.CreateImage("Ring", box, Vector2.zero, box.sizeDelta, _ring, new Color(1f, 1f, 1f, 0.55f));
            RectTransform check = HudWidgets.CreateRect("Check", box, Vector2.zero, box.sizeDelta);
            HudWidgets.CreateImage("Fill", check, Vector2.zero, box.sizeDelta, _disc, HudWidgets.DoneColor);
            HudWidgets.CreateImage("Tick", check, Vector2.zero, box.sizeDelta * 0.8f, _tick, Color.white);

            Text titleText = CreateLabel("Title", card, new Vector2(56f, -12f), 20, FontStyle.Bold, Color.white);
            titleText.text = title;
            Text progress = CreateLabel("Progress", card, new Vector2(56f, -12f), 17, FontStyle.Bold, HudWidgets.AccentColor);
            // Below the title: placed once the title height is known.
            progress.rectTransform.anchoredPosition = new Vector2(56f, -12f - Mathf.Max(24f, titleText.preferredHeight));

            ObjectiveLine line = new ObjectiveLine(root, card, group, outline, check, titleText, progress, BaseHeight);
            _lines.Add(line);
            return line;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            float y = 0f;

            for (int i = 0; i < _lines.Count; i++)
            {
                ObjectiveLine line = _lines[i];
                line.Animate(deltaTime, _fadeTime, _completedHold);

                if (line.IsGone)
                {
                    Destroy(line.Root.gameObject);
                    _lines.RemoveAt(i);
                    i--;
                    continue;
                }

                // A new card appears in place; existing cards glide up when one above them leaves.
                line.LayoutY = float.IsNaN(line.LayoutY) ? -y : Mathf.Lerp(line.LayoutY, -y, 1f - Mathf.Exp(-_layoutSpeed * deltaTime));
                line.Root.anchoredPosition = new Vector2(0f, line.LayoutY);

                if (line.LayoutHeight > 0f)
                    y += line.LayoutHeight + _spacing * Mathf.Clamp01(line.LayoutHeight / BaseHeight);
            }
        }

        private Text CreateLabel(string name, Transform parent, Vector2 position, int fontSize, FontStyle style, Color color)
        {
            Text text = HudWidgets.CreateText(name, parent, Vector2.zero, new Vector2(_width - 76f, 24f), fontSize, TextAnchor.UpperLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontStyle = style;
            text.color = color;
            Object.Destroy(text.GetComponent<Shadow>());
            HudWidgets.AddOutline(text);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = HudWidgets.CreateRect(name, parent, position, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = TopLeft;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform card, Vector2 offset, Color color, Sprite sprite)
        {
            Image image = HudWidgets.CreateImage(name, card, Vector2.zero, Vector2.zero, sprite, color);
            Stretch(image.rectTransform, 0f);
            image.rectTransform.anchoredPosition = offset;
            return image;
        }

        // Fills the card, minus an inset on every side; follows the card when it grows.
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image Sliced(Image image)
        {
            image.type = Image.Type.Sliced;
            return image;
        }
    }
}
