using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WaterChaos.Sounds;

namespace WaterChaos.UI
{
    /// <summary>
    /// Story card at the side of the screen; the game keeps running. The card slides in, its text is typed out,
    /// then it stays for a while (a ring around the Esc key fills meanwhile) and slides away by itself.
    /// Esc closes it early, but only once the text is fully typed. Messages queue up, so two cards never show at once.
    /// </summary>
    public sealed class StoryPanelView : MonoBehaviour
    {
        private const float TextInset = 36f;
        private const float FooterHeight = 70f;
        private const float MinHeight = 150f;

        private enum Phase
        {
            Typing,
            Holding,
            Closing,
        }

        private struct Message
        {
            public string Speaker;
            public string Text;
        }

        [Header("Layout")]
        [Tooltip("Card width, in reference pixels (1920x1080).")]
        [SerializeField, Min(300f)] private float _width = 600f;

        [Tooltip("Card position from the right edge (x) and above the screen middle (y), in reference pixels.")]
        [SerializeField] private Vector2 _position = new Vector2(40f, 120f);

        [Tooltip("Draw order of the card canvas; above the rest of the HUD.")]
        [SerializeField] private int _sortingOrder = 100;

        [Header("Text")]
        [Tooltip("Caption next to the Esc key.")]
        [SerializeField] private string _closeText = "Закрити";

        [Header("Timing")]
        [Tooltip("Letters typed per second (1/s).")]
        [SerializeField, Min(1f)] private float _lettersPerSecond = 45f;

        [Tooltip("How long the typed card stays before it slides away, in seconds (s). The Esc ring fills over this time.")]
        [SerializeField, Min(0.5f)] private float _holdTime = 5f;

        [Tooltip("Slide in/out time, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _slideTime = 0.35f;

        [Tooltip("A waiting card replaces a typed one after it was shown at least this long, in seconds (s).")]
        [SerializeField, Min(0f)] private float _minReadTime = 3f;

        private readonly Queue<Message> _queue = new Queue<Message>();
        private CanvasGroup _group;
        private RectTransform _card;
        private Text _speaker;
        private Text _body;
        private Image _progress;
        private CanvasGroup _footer;

        private bool _hasCurrent;
        private Phase _phase;
        private string _fullText = string.Empty;
        private float _typed;
        private float _held;
        private float _slide;

        /// <summary>A card is shown or waiting.</summary>
        public bool IsOpen => _hasCurrent || _queue.Count > 0;

        /// <summary>A card is still being typed or waits to be shown; the mission waits until it has been read out.</summary>
        public bool IsReading => (_hasCurrent && _phase == Phase.Typing) || _queue.Count > 0;

        private void Awake()
        {
            Build();
            _group.gameObject.SetActive(false);
        }

        /// <summary>Queues a story card; it slides in as soon as no other card is shown.</summary>
        public void Show(string speaker, string text)
        {
            _queue.Enqueue(new Message { Speaker = speaker, Text = text ?? string.Empty });
            if (!_hasCurrent)
                OpenNext();
        }

        private void Update()
        {
            if (!_hasCurrent)
                return;

            float deltaTime = Time.deltaTime;
            switch (_phase)
            {
                case Phase.Typing:
                    Type(deltaTime);
                    break;
                case Phase.Holding:
                    Hold(deltaTime);
                    break;
            }

            Slide(deltaTime);
        }

        private void Type(float deltaTime)
        {
            _typed += _lettersPerSecond * deltaTime;
            int shown = Mathf.Min(_fullText.Length, Mathf.FloorToInt(_typed));
            _body.text = _fullText.Substring(0, shown);

            if (shown < _fullText.Length)
                return;

            _phase = Phase.Holding;
            _held = 0f;
            _footer.alpha = 1f;
        }

        private void Hold(float deltaTime)
        {
            _held += deltaTime;
            _progress.fillAmount = Mathf.Clamp01(_held / _holdTime);

            Keyboard keyboard = Keyboard.current;
            bool closePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool nextWaiting = _queue.Count > 0 && _held >= _minReadTime;

            if (closePressed || nextWaiting || _held >= _holdTime)
                _phase = Phase.Closing;
        }

        // Eases in from beyond the right edge and back out; the next card opens once this one is gone.
        private void Slide(float deltaTime)
        {
            float target = _phase == Phase.Closing ? 0f : 1f;
            _slide = Mathf.MoveTowards(_slide, target, deltaTime / _slideTime);

            float eased = 1f - (1f - _slide) * (1f - _slide);
            float hiddenX = _width + _position.x + 40f;
            _card.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, -_position.x, eased), _position.y);
            _group.alpha = eased;

            if (_phase == Phase.Closing && _slide <= 0f)
                FinishClosing();
        }

        private void OpenNext()
        {
            Message message = _queue.Dequeue();
            _hasCurrent = true;
            _phase = Phase.Typing;
            _typed = 0f;
            _held = 0f;
            _slide = 0f;

            bool hasSpeaker = !string.IsNullOrWhiteSpace(message.Speaker);
            _speaker.gameObject.SetActive(hasSpeaker);
            _speaker.text = hasSpeaker ? message.Speaker : string.Empty;

            // Sized for the whole text up front, so the card does not grow while typing.
            _fullText = message.Text;
            _body.text = _fullText;
            Layout(hasSpeaker);
            _body.text = string.Empty;

            _progress.fillAmount = 0f;
            _footer.alpha = 0.35f;
            _group.gameObject.SetActive(true);
            Sound.PlayUI(SoundId.StoryOpen);
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
        }

        // The card grows with the text: speaker on top, body, then the Esc prompt at the bottom.
        private void Layout(bool hasSpeaker)
        {
            float top = hasSpeaker ? 64f : 28f;
            float bodyHeight = Mathf.Max(30f, _body.preferredHeight);
            float height = Mathf.Max(MinHeight, top + bodyHeight + FooterHeight);

            _card.sizeDelta = new Vector2(_width, height);
            _speaker.rectTransform.anchoredPosition = new Vector2(TextInset, -24f);
            _body.rectTransform.anchoredPosition = new Vector2(TextInset, -top);
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

            Sprite rounded = HudWidgets.CreateRoundedSprite(64, 22);
            _card = HudWidgets.CreateRect("Card", root, Vector2.zero, new Vector2(_width, MinHeight));
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(1f, 0.5f);
            Fill(HudWidgets.CreateImage("Shadow", _card, Vector2.zero, Vector2.zero, rounded, HudWidgets.CardShadowColor), 0f, new Vector2(8f, -8f));
            Fill(HudWidgets.CreateImage("Outline", _card, Vector2.zero, Vector2.zero, rounded, HudWidgets.AccentColor), 0f, Vector2.zero);
            Fill(HudWidgets.CreateImage("Inner", _card, Vector2.zero, Vector2.zero, rounded, HudWidgets.CardInnerColor), 6f, Vector2.zero);

            _speaker = CreateTopText("Speaker", 26, HudWidgets.AccentColor);
            _body = CreateTopText("Body", 23, Color.white);
            _body.fontStyle = FontStyle.Normal;
            _body.lineSpacing = 1.1f;

            BuildFooter();
        }

        // Esc key in the bottom right corner with a ring around it that fills while the card stays.
        private void BuildFooter()
        {
            RectTransform footer = HudWidgets.CreateRect("Footer", _card, Vector2.zero, new Vector2(240f, 56f));
            footer.anchorMin = footer.anchorMax = footer.pivot = new Vector2(1f, 0f);
            footer.anchoredPosition = new Vector2(-24f, 16f);
            _footer = footer.gameObject.AddComponent<CanvasGroup>();

            Sprite disc = HudWidgets.CreateCircleSprite(128, 0f);
            Sprite ring = HudWidgets.CreateCircleSprite(128, 0.8f);
            Vector2 keySize = new Vector2(52f, 52f);
            Vector2 keyPosition = new Vector2(120f - keySize.x * 0.5f, 0f);

            HudWidgets.CreateImage("Key Background", footer, keyPosition, keySize, disc, HudWidgets.PanelColor);
            HudWidgets.CreateImage("Key Track", footer, keyPosition, keySize, ring, HudWidgets.TrackColor);

            _progress = HudWidgets.CreateImage("Key Progress", footer, keyPosition, keySize, ring, HudWidgets.AccentColor);
            _progress.type = Image.Type.Filled;
            _progress.fillMethod = Image.FillMethod.Radial360;
            _progress.fillOrigin = (int)Image.Origin360.Top;
            _progress.fillClockwise = true;
            _progress.fillAmount = 0f;

            Text key = HudWidgets.CreateText("Key", footer, keyPosition, keySize, 15, TextAnchor.MiddleCenter);
            key.text = "Esc";
            key.fontStyle = FontStyle.Bold;

            Text caption = HudWidgets.CreateText("Caption", footer, new Vector2(keyPosition.x - keySize.x * 0.5f - 70f, 0f),
                new Vector2(130f, 30f), 20, TextAnchor.MiddleRight);
            caption.text = _closeText;
            caption.fontStyle = FontStyle.Bold;
            Object.Destroy(caption.GetComponent<Shadow>());
            HudWidgets.AddOutline(caption);
        }

        private Text CreateTopText(string name, int fontSize, Color color)
        {
            Text text = HudWidgets.CreateText(name, _card, Vector2.zero, new Vector2(_width - TextInset * 2f, 30f), fontSize, TextAnchor.UpperLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
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
