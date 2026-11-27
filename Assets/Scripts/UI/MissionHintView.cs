using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>
    /// Tutorial hint under the crosshair ("Закинь насос у резервуар", with the F key): one at a time,
    /// owned by the objective that showed it. With a key it reuses the round key prompt of the HUD;
    /// without one it is a short outlined caption. Fades on real time.
    /// </summary>
    public sealed class MissionHintView : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Center of the hint from the screen center, in reference pixels (1920x1080); below the HUD prompts.")]
        [SerializeField] private Vector2 _position = new Vector2(0f, -300f);

        [Tooltip("Draw order of the hint canvas.")]
        [SerializeField] private int _sortingOrder = 20;

        [Header("Timing")]
        [Tooltip("Fade in/out time, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.25f;

        private readonly Dictionary<PlayerControl, KeyPrompt> _prompts = new Dictionary<PlayerControl, KeyPrompt>();
        private RectTransform _root;
        private CanvasGroup _group;
        private Text _caption;

        private object _owner;
        private KeyPrompt _shownPrompt;

        // Owner of a hint shown for a while (see ShowFor); any objective's hint replaces it.
        private readonly object _timedOwner = new object();
        private float _timedLeft;

        private void Awake()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Mission Hint Canvas", _sortingOrder);
            _root = HudWidgets.CreateRect("Mission Hint", canvas.transform, _position, new Vector2(700f, 160f));
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Long hints wrap onto a second line instead of running off the screen.
            _caption = HudWidgets.CreateText("Caption", _root, Vector2.zero, new Vector2(900f, 60f), 24, TextAnchor.MiddleCenter);
            _caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            _caption.verticalOverflow = VerticalWrapMode.Overflow;
            _caption.fontStyle = FontStyle.Bold;
            _caption.color = HudWidgets.AccentColor;
            Object.Destroy(_caption.GetComponent<Shadow>());
            HudWidgets.AddOutline(_caption);
            _caption.gameObject.SetActive(false);
        }

        /// <summary>Shows a hint for <paramref name="owner"/>, replacing any other hint.</summary>
        public void Show(object owner, string text, bool showKey, PlayerControl key)
        {
            _owner = owner;

            if (_shownPrompt != null)
                _shownPrompt.Hide();
            _shownPrompt = null;

            if (showKey)
            {
                _shownPrompt = GetPrompt(key);
                _shownPrompt.Show(text, 0f, 1f);
                _caption.gameObject.SetActive(false);
            }
            else
            {
                _caption.text = text;
                _caption.gameObject.SetActive(true);
            }
        }

        /// <summary>Shows a hint without a key for <paramref name="seconds"/> (real time), unless an objective's hint replaces it.</summary>
        public void ShowFor(string text, float seconds)
        {
            Show(_timedOwner, text, false, PlayerControl.Interact);
            _timedLeft = seconds;
        }

        /// <summary>Hides the hint if <paramref name="owner"/> is the one showing it.</summary>
        public void Hide(object owner)
        {
            if (_owner == owner)
                _owner = null;
        }

        private void Update()
        {
            if (_owner == _timedOwner)
            {
                _timedLeft -= Time.unscaledDeltaTime;
                if (_timedLeft <= 0f)
                    _owner = null;
            }

            float target = _owner != null ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / _fadeTime);
        }

        private KeyPrompt GetPrompt(PlayerControl key)
        {
            if (!_prompts.TryGetValue(key, out KeyPrompt prompt))
            {
                prompt = HudWidgets.CreateKeyPrompt($"{key} Prompt", _root, key, new Vector2(0f, 30f));
                _prompts.Add(key, prompt);
            }

            return prompt;
        }
    }
}
