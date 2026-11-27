using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Surfaces;

namespace WaterChaos.UI
{
    /// <summary>
    /// Cartoon toxins around the screen while the player is exposed to toxic liquid (standing in a toxic puddle or
    /// hit by a toxic stream): a wobbling green glow on the edges, goo dripping from the top and bubbles floating
    /// up from the bottom (DOTween), fading in and out. Display only: whether the player is exposed is decided by the
    /// one <see cref="ToxicExposure"/> on the player, so overlapping sources never flicker or double up.
    /// Same setup as <see cref="PlayerBurnOverlay"/>: sits on the player's object.
    /// </summary>
    [RequireComponent(typeof(ToxicExposure))]
    public sealed class PlayerToxicOverlay : MonoBehaviour
    {
        private static readonly Color Glow = new Color(0.35f, 0.95f, 0.1f, 0.7f);
        private static readonly Color Goo = new Color(0.55f, 1f, 0.15f, 0.95f);
        private static readonly Color GooDark = new Color(0.22f, 0.6f, 0.05f, 0.95f);
        private static readonly Color Bubble = new Color(0.75f, 1f, 0.3f, 0.85f);

        [Header("Look")]
        [Tooltip("How strong the effect is at full exposure (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;

        [Tooltip("Fade in when exposure starts, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeInTime = 0.25f;

        [Tooltip("Fade out after exposure ends, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _fadeOutTime = 0.8f;

        [SerializeField, Min(0)] private int _drips = 8;
        [SerializeField, Min(0)] private int _bubbles = 14;
        [SerializeField] private int _sortingOrder = 29;

        private readonly List<Tween> _tweens = new List<Tween>();
        private ToxicExposure _exposure;
        private CanvasGroup _group;
        private RectTransform _root;
        private bool _shown;

        private void Awake()
        {
            _exposure = GetComponent<ToxicExposure>();
            Build();
        }

        private void OnDestroy()
        {
            foreach (Tween tween in _tweens)
                tween.Kill();
        }

        private void Update()
        {
            bool exposed = _exposure.IsExposed;
            if (exposed == _shown)
                return;

            _shown = exposed;
            _group.DOKill();
            _group.DOFade(exposed ? _intensity : 0f, exposed ? _fadeInTime : _fadeOutTime).SetUpdate(true).SetLink(gameObject);
        }

        private void Build()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Toxic Overlay Canvas", _sortingOrder);
            _root = (RectTransform)canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image glow = HudWidgets.CreateImage("Glow", _root, Vector2.zero, Vector2.zero, HudWidgets.CreateVignetteSprite(128), Glow);
            Stretch(glow.rectTransform, 0f);
            // The glow breathes, like woozy vision.
            _tweens.Add(glow.rectTransform.DOScale(1.06f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetUpdate(true).SetLink(gameObject));
            _tweens.Add(glow.DOColor(new Color(0.6f, 1f, 0.1f, 0.85f), 1.3f).SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject));

            Sprite disc = HudWidgets.CreateCircleSprite(64, 0f);
            Sprite ring = HudWidgets.CreateCircleSprite(64, 0.72f);

            for (int i = 0; i < _drips; i++)
                AddDrip(disc, (i + Random.Range(0.2f, 0.8f)) / _drips);

            for (int i = 0; i < _bubbles; i++)
                AddBubble(disc, ring, Random.value);
        }

        /// <summary>A blob of goo hanging from the top edge that stretches down and snaps back.</summary>
        private void AddDrip(Sprite disc, float u)
        {
            float width = Random.Range(40f, 80f);
            RectTransform drip = HudWidgets.CreateRect("Drip", _root, Vector2.zero, new Vector2(width, width * 1.6f));
            drip.anchorMin = drip.anchorMax = new Vector2(u, 1f);
            drip.pivot = new Vector2(0.5f, 1f);
            drip.anchoredPosition = new Vector2(0f, width * 0.4f);

            Image body = HudWidgets.CreateImage("Goo", drip, Vector2.zero, drip.sizeDelta, disc, GooDark);
            Stretch(body.rectTransform, 0f);
            Image shine = HudWidgets.CreateImage("Shine", drip, Vector2.zero, drip.sizeDelta * 0.75f, disc, Goo);
            shine.rectTransform.anchorMin = shine.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);

            float time = Random.Range(0.9f, 1.6f);
            _tweens.Add(drip.DOScaleY(Random.Range(1.4f, 2.2f), time).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutQuad)
                .SetDelay(Random.Range(0f, 1f)).SetUpdate(true).SetLink(gameObject));
        }

        /// <summary>A toxic bubble rising from the bottom edge, wobbling, popping at the top of its path and repeating.</summary>
        private void AddBubble(Sprite disc, Sprite ring, float u)
        {
            float size = Random.Range(24f, 64f);
            RectTransform bubble = HudWidgets.CreateRect("Bubble", _root, Vector2.zero, new Vector2(size, size));
            // Bubbles hug the left and right thirds, leaving the middle readable.
            float x = u < 0.5f ? u * 0.6f : 0.7f + (u - 0.5f) * 0.6f;
            bubble.anchorMin = bubble.anchorMax = new Vector2(x, 0f);
            bubble.anchoredPosition = new Vector2(0f, -size);

            Image fill = HudWidgets.CreateImage("Fill", bubble, Vector2.zero, bubble.sizeDelta, disc, new Color(Bubble.r, Bubble.g, Bubble.b, 0.3f));
            Stretch(fill.rectTransform, 0f);
            Image rim = HudWidgets.CreateImage("Rim", bubble, Vector2.zero, bubble.sizeDelta, ring, Bubble);
            Stretch(rim.rectTransform, 0f);
            Image glint = HudWidgets.CreateImage("Glint", bubble, Vector2.zero, bubble.sizeDelta * 0.22f, disc, Color.white);
            glint.rectTransform.anchorMin = glint.rectTransform.anchorMax = new Vector2(0.32f, 0.68f);

            float rise = Random.Range(220f, 520f);
            float time = Random.Range(1.6f, 3f);
            float delay = Random.Range(0f, 2.5f);
            _tweens.Add(bubble.DOAnchorPosY(rise, time).SetEase(Ease.OutSine).SetLoops(-1, LoopType.Restart)
                .SetDelay(delay).SetUpdate(true).SetLink(gameObject));
            _tweens.Add(bubble.DOScale(1.25f, time).SetEase(Ease.InQuad).SetLoops(-1, LoopType.Restart)
                .SetDelay(delay).SetUpdate(true).SetLink(gameObject));
            _tweens.Add(bubble.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-25f, 25f)), time * 0.3f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject));
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
