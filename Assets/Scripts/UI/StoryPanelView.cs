using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WaterChaos.Mission;

namespace WaterChaos.UI
{
    /// <summary>
    /// Story panel in the middle of the screen: the game pauses while it is open and goes on when the player
    /// closes it with Esc. Messages queue up, so two panels never show at once. Same cartoon card look as the HUD;
    /// animates on real time, since the game time is frozen while it is open.
    /// </summary>
    public sealed class StoryPanelView : MonoBehaviour
    {
        private const float BodyWidthInset = 90f;
        private const float MinHeight = 200f;

        private struct Message
        {
            public string Speaker;
            public string Text;
        }

        [Header("References")]
        [Tooltip("Pauses the game while a panel is open.")]
        [SerializeField] private GamePause _pause;

        [Header("Layout")]
        [Tooltip("Card width, in reference pixels (1920x1080).")]
        [SerializeField, Min(300f)] private float _width = 880f;

        [Tooltip("Darkening of the game behind the panel (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.45f;

        [Tooltip("Draw order of the panel canvas; above the rest of the HUD.")]
        [SerializeField] private int _sortingOrder = 100;

        [Header("Text")]
        [Tooltip("Caption next to the Esc key.")]
        [SerializeField] private string _continueText = "Продовжити";

        [Header("Timing")]
        [Tooltip("Fade in/out time, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.25f;

        [Tooltip("Esc is ignored for this long after a panel opens, so it is not skipped by accident, in seconds (s).")]
        [SerializeField, Min(0f)] private float _minShowTime = 0.6f;

        private readonly Queue<Message> _queue = new Queue<Message>();
        private CanvasGroup _group;
        private RectTransform _card;
        private Text _speaker;
        private Text _body;
        private RectTransform _footer;

        private bool _hasCurrent;
        private bool _closing;
        private float _shownTime;

        /// <summary>A panel is shown or waiting; the mission does not advance meanwhile.</summary>
        public bool IsOpen => _hasCurrent || _queue.Count > 0;

        private void Awake()
        {
            if (_pause == null)
                Debug.LogError($"{name}: StoryPanelView needs a Pause reference; the game will not stop under the panel.", this);

            Build();
            _group.gameObject.SetActive(false);
        }

        /// <summary>Queues a story panel; it opens (and the game pauses) as soon as no other panel is open.</summary>
        public void Show(string speaker, string text)
        {
            _queue.Enqueue(new Message { Speaker = speaker, Text = text });
            if (!_hasCurrent)
                OpenNext();
        }

        private void Update()
        {
            if (!_hasCurrent)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            _shownTime += deltaTime;

            float target = _closing ? 0f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, deltaTime / _fadeTime);
            _card.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, _group.alpha);

            if (_closing)
            {
                if (_group.alpha <= 0f)
                    FinishClosing();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && _shownTime >= _minShowTime)
                _closing = true;
        }

        private void OpenNext()
        {
            Message message = _queue.Dequeue();
            _hasCurrent = true;
            _closing = false;
            _shownTime = 0f;

            bool hasSpeaker = !string.IsNullOrWhiteSpace(message.Speaker);
            _speaker.gameObject.SetActive(hasSpeaker);
            _speaker.text = hasSpeaker ? message.Speaker : string.Empty;
            _body.text = message.Text;
            Layout(hasSpeaker);

            _group.alpha = 0f;
            _group.gameObject.SetActive(true);

            if (_pause != null)
                _pause.Pause();
        }

        private void FinishClosing()
        {
            _hasCurrent = false;

            if (_queue.Count > 0)
            {
                OpenNext();
                return;
            }

            _group.gameObject.SetActive(false);
            if (_pause != null)
                _pause.Resume();
        }

        // The card grows with the text: speaker on top, body, then the Esc prompt at the bottom.
        private void Layout(bool hasSpeaker)
        {
            float top = hasSpeaker ? 82f : 40f;
            float bodyHeight = Mathf.Max(30f, _body.preferredHeight);
            float height = Mathf.Max(MinHeight, top + bodyHeight + 100f);

            _card.sizeDelta = new Vector2(_width, height);
            _speaker.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            _body.rectTransform.anchoredPosition = new Vector2(0f, -top);
            _footer.anchoredPosition = new Vector2(0f, 46f);
        }

        private void Build()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Story Panel Canvas", _sortingOrder);

            RectTransform root = HudWidgets.CreateRect("Story Panel", canvas.transform, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, _dimAlpha);
            dim.raycastTarget = false;

            Sprite rounded = HudWidgets.CreateRoundedSprite(64, 22);
            _card = HudWidgets.CreateRect("Card", root, Vector2.zero, new Vector2(_width, MinHeight));
            Fill(HudWidgets.CreateImage("Shadow", _card, Vector2.zero, Vector2.zero, rounded, HudWidgets.CardShadowColor), 0f, new Vector2(8f, -8f));
            Fill(HudWidgets.CreateImage("Outline", _card, Vector2.zero, Vector2.zero, rounded, HudWidgets.AccentColor), 0f, Vector2.zero);
            Fill(HudWidgets.CreateImage("Inner", _card, Vector2.zero, Vector2.zero, rounded, HudWidgets.CardInnerColor), 6f, Vector2.zero);

            _speaker = CreateTopText("Speaker", 28, HudWidgets.AccentColor, TextAnchor.UpperCenter);
            _body = CreateTopText("Body", 25, Color.white, TextAnchor.UpperCenter);
            _body.fontStyle = FontStyle.Normal;
            _body.lineSpacing = 1.1f;

            _footer = HudWidgets.CreateRect("Footer", _card, Vector2.zero, new Vector2(260f, 44f));
            _footer.anchorMin = _footer.anchorMax = new Vector2(0.5f, 0f);
            Sprite disc = HudWidgets.CreateCircleSprite(128, 0f);
            Sprite ring = HudWidgets.CreateCircleSprite(128, 0.84f);
            Vector2 keySize = new Vector2(44f, 44f);
            Vector2 keyPosition = new Vector2(-70f, 0f);
            HudWidgets.CreateImage("Key Background", _footer, keyPosition, keySize, disc, HudWidgets.PanelColor);
            HudWidgets.CreateImage("Key Ring", _footer, keyPosition, keySize, ring, Color.white);
            Text key = HudWidgets.CreateText("Key", _footer, keyPosition, keySize, 15, TextAnchor.MiddleCenter);
            key.text = "Esc";
            key.fontStyle = FontStyle.Bold;

            Text caption = HudWidgets.CreateText("Caption", _footer, new Vector2(30f, 0f), new Vector2(180f, 30f), 20, TextAnchor.MiddleLeft);
            caption.text = _continueText;
            caption.fontStyle = FontStyle.Bold;
            Object.Destroy(caption.GetComponent<Shadow>());
            HudWidgets.AddOutline(caption);
        }

        private Text CreateTopText(string name, int fontSize, Color color, TextAnchor alignment)
        {
            Text text = HudWidgets.CreateText(name, _card, Vector2.zero, new Vector2(_width - BodyWidthInset, 30f), fontSize, alignment);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            Object.Destroy(text.GetComponent<Shadow>());
            HudWidgets.AddOutline(text);
            return text;
        }

        private static void Fill(Image image, float inset, Vector2 offset)
        {
            image.type = Image.Type.Sliced;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset) + offset;
            rect.offsetMax = new Vector2(-inset, -inset) + offset;
        }
    }
}
