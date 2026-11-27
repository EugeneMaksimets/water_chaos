using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>
    /// What the player sees through the gas mask while it is on: a dark rubber frame around the screen with faint
    /// edges of the two lenses, faded in and out. Kept light so the view stays clear. Display only, on the player next to <see cref="PlayerGasMask"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerGasMask))]
    public sealed class PlayerGasMaskOverlay : MonoBehaviour
    {
        private static readonly Color Rubber = new Color(0.1f, 0.12f, 0.1f, 0.85f);
        private static readonly Color Rim = new Color(0.25f, 0.3f, 0.24f, 0.35f);

        [SerializeField, Min(0.01f)] private float _fadeTime = 0.4f;
        [SerializeField] private int _sortingOrder = 28;

        private PlayerGasMask _mask;
        private CanvasGroup _group;

        private void Awake()
        {
            _mask = GetComponent<PlayerGasMask>();
            Build();
        }

        private void OnEnable()
        {
            _mask.WornChanged += Show;
        }

        private void OnDisable()
        {
            _mask.WornChanged -= Show;
        }

        private void Show(bool worn)
        {
            _group.DOKill();
            _group.DOFade(worn ? 1f : 0f, _fadeTime).SetUpdate(true).SetLink(gameObject);
        }

        private void Build()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Gas Mask Canvas", _sortingOrder);
            var root = (RectTransform)canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image frame = HudWidgets.CreateImage("Frame", root, Vector2.zero, Vector2.zero, HudWidgets.CreateVignetteSprite(128), Rubber);
            Stretch(frame.rectTransform);

            // Two lens rims, wide apart, so the middle of the view stays clear.
            Sprite ring = HudWidgets.CreateCircleSprite(256, 0.975f);
            AddLens(root, ring, new Vector2(0.2f, 0.5f));
            AddLens(root, ring, new Vector2(0.8f, 0.5f));
        }

        private static void AddLens(RectTransform root, Sprite ring, Vector2 anchor)
        {
            Image lens = HudWidgets.CreateImage("Lens", root, Vector2.zero, new Vector2(1500f, 1500f), ring, Rim);
            lens.rectTransform.anchorMin = lens.rectTransform.anchorMax = anchor;
            lens.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
