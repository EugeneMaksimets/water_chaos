using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Sounds;

namespace WaterChaos.UI
{
    /// <summary>
    /// One objective in the <see cref="ObjectiveListView"/>: the handle an objective keeps to update its line.
    /// Slides in, shows progress, gets a check mark when completed, stays for a while and fades out;
    /// a dismissed (dropped) objective just fades out. All animation is in real time, so it runs while paused.
    /// </summary>
    public sealed class ObjectiveLine
    {
        private const float SlideDistance = 40f;
        private const float PopTime = 0.3f;
        private const float LineHeight = 24f;
        private const float Padding = 15f;

        private enum Phase { Active, Completed, Leaving, Collapsing, Gone }

        private readonly RectTransform _card;
        private readonly CanvasGroup _group;
        private readonly Image _outline;
        private readonly RectTransform _check;
        private readonly Text _title;
        private readonly Text _progress;
        private readonly float _baseHeight;

        private Phase _phase = Phase.Active;
        private float _phaseTime;
        private float _appear;
        private float _collapse = 1f;
        private bool _hasProgress;

        internal ObjectiveLine(RectTransform root, RectTransform card, CanvasGroup group, Image outline, RectTransform check,
            Text title, Text progress, float baseHeight)
        {
            Root = root;
            _card = card;
            _group = group;
            _outline = outline;
            _check = check;
            _title = title;
            _progress = progress;
            _baseHeight = baseHeight;
            _check.localScale = Vector3.zero;
            _progress.gameObject.SetActive(false);
            Resize();
        }

        internal RectTransform Root { get; }

        internal bool IsGone => _phase == Phase.Gone;

        /// <summary>Height taken in the list now, in reference pixels; shrinks to 0 while leaving.</summary>
        internal float LayoutHeight => Height * _collapse;

        /// <summary>Current vertical position in the list; eased toward its slot by the view.</summary>
        internal float LayoutY { get; set; } = float.NaN;

        private float Height { get; set; }

        /// <summary>Progress after the title (e.g. "1/2"); null hides it.</summary>
        public void SetProgress(string text)
        {
            bool has = !string.IsNullOrEmpty(text);
            _progress.text = has ? text : string.Empty;

            if (has != _hasProgress)
            {
                _hasProgress = has;
                _progress.gameObject.SetActive(has);
                Resize();
            }
        }

        /// <summary>Shows the check mark; the line leaves after the hold time.</summary>
        public void Complete()
        {
            if (_phase != Phase.Active)
                return;

            SetPhase(Phase.Completed);
            Sound.PlayUI(SoundId.ObjectiveComplete);
            _outline.color = HudWidgets.DoneColor;
            _progress.gameObject.SetActive(false);
            _title.color = new Color(1f, 1f, 1f, 0.8f);
        }

        /// <summary>Removes the line without a check mark (the objective was dropped).</summary>
        public void Dismiss()
        {
            if (_phase == Phase.Active || _phase == Phase.Completed)
                SetPhase(Phase.Leaving);
        }

        internal void Animate(float deltaTime, float fadeTime, float completedHold)
        {
            _phaseTime += deltaTime;
            _appear = Mathf.MoveTowards(_appear, 1f, deltaTime / fadeTime);

            switch (_phase)
            {
                case Phase.Completed:
                    _check.localScale = Vector3.one * Pop(_phaseTime / PopTime);
                    _card.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Mathf.Clamp01(_phaseTime / PopTime) * Mathf.PI));
                    if (_phaseTime >= completedHold)
                        SetPhase(Phase.Leaving);
                    break;

                case Phase.Leaving:
                    if (_phaseTime >= fadeTime)
                        SetPhase(Phase.Collapsing);
                    break;

                case Phase.Collapsing:
                    _collapse = Mathf.MoveTowards(_collapse, 0f, deltaTime / fadeTime);
                    if (_collapse <= 0f)
                        SetPhase(Phase.Gone);
                    break;
            }

            float visible = _phase == Phase.Leaving ? 1f - Mathf.Clamp01(_phaseTime / fadeTime)
                : _phase == Phase.Collapsing || _phase == Phase.Gone ? 0f
                : EaseOut(_appear);

            _group.alpha = visible;
            _card.anchoredPosition = new Vector2(-SlideDistance * (1f - visible), 0f);
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        // Title wraps to its width and the card grows with it; the progress sits at the right of the first line.
        private void Resize()
        {
            float titleHeight = Mathf.Max(LineHeight, _title.preferredHeight);
            Height = Mathf.Max(_baseHeight, titleHeight + 2f * Padding);

            float top = -(Height - titleHeight) * 0.5f;
            _title.rectTransform.anchoredPosition = new Vector2(_title.rectTransform.anchoredPosition.x, top);
            _title.rectTransform.sizeDelta = new Vector2(_title.rectTransform.sizeDelta.x, titleHeight);
            _progress.rectTransform.anchoredPosition = new Vector2(_progress.rectTransform.anchoredPosition.x, top);

            _card.sizeDelta = new Vector2(_card.sizeDelta.x, Height);
            Root.sizeDelta = new Vector2(Root.sizeDelta.x, Height);
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // 0 → overshoot 1.25 → settle at 1.
        private static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.6f ? Mathf.Lerp(0f, 1.25f, t / 0.6f) : Mathf.Lerp(1.25f, 1f, (t - 0.6f) / 0.4f);
        }
    }
}
