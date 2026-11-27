using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Interaction;

namespace WaterChaos.UI
{
    /// <summary>
    /// Cartoon fire around the screen while the player burns: a friendly flamethrower hits them, or they stand in a fire.
    /// An orange-red glow on the edges and flickering flames along the bottom and the sides (DOTween), fading in and out.
    /// Display only. Sits on the player's object, next to their collider, so flamethrower hits find it.
    /// </summary>
    public sealed class PlayerBurnOverlay : MonoBehaviour, IFlameHitReceiver
    {
        private static readonly Color Outer = new Color(1f, 0.42f, 0.08f, 0.95f);
        private static readonly Color Inner = new Color(1f, 0.86f, 0.25f, 0.95f);
        private static readonly Color Glow = new Color(1f, 0.3f, 0.05f, 0.75f);

        [Header("When")]
        [Tooltip("Standing this close to a burning fire burns, in meters (m); 0 = only flamethrowers.")]
        [SerializeField, Min(0f)] private float _nearFireRadius = 1.3f;

        [Tooltip("The screen keeps burning this long after the last flame hit, in seconds (s).")]
        [SerializeField, Min(0f)] private float _holdTime = 0.6f;

        [Header("Look")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.3f;
        [SerializeField, Min(1)] private int _flamesAlongBottom = 9;
        [SerializeField, Min(0)] private int _flamesPerSide = 4;
        [SerializeField] private int _sortingOrder = 30;

        private readonly List<Tween> _tweens = new List<Tween>();
        private CanvasGroup _group;
        private float _lastFlame = float.NegativeInfinity;
        private float _nextNearCheck;
        private bool _nearFire;
        private bool _shown;

        public void ReceiveFlame(Vector3 point)
        {
            _lastFlame = Time.time;
        }

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            foreach (Tween tween in _tweens)
                tween.Kill();
        }

        private void Update()
        {
            if (_nearFireRadius > 0f && Time.time >= _nextNearCheck)
            {
                _nextNearCheck = Time.time + 0.2f;
                _nearFire = IsNearFire();
            }

            bool burning = _nearFire || Time.time - _lastFlame <= _holdTime;
            if (burning == _shown)
                return;

            _shown = burning;
            _group.DOKill();
            _group.DOFade(burning ? 1f : 0f, _fadeTime).SetUpdate(true).SetLink(gameObject);
        }

        private bool IsNearFire()
        {
            foreach (FireExtinguishHandler fire in FireExtinguishHandler.All)
            {
                if (fire.IsBurning && Vector3.Distance(fire.transform.position, transform.position) <= _nearFireRadius)
                    return true;
            }

            return false;
        }

        private void Build()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Burn Overlay Canvas", _sortingOrder);
            RectTransform root = (RectTransform)canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image glow = HudWidgets.CreateImage("Glow", root, Vector2.zero, Vector2.zero, HudWidgets.CreateVignetteSprite(128), Glow);
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = glow.rectTransform.offsetMax = Vector2.zero;

            Sprite flame = HudWidgets.CreateFlameSprite(128);
            for (int i = 0; i < _flamesAlongBottom; i++)
            {
                float u = (i + 0.5f) / _flamesAlongBottom;
                AddFlame(root, flame, new Vector2(u, 0f), 0f, Random.Range(260f, 420f));
            }

            for (int i = 0; i < _flamesPerSide; i++)
            {
                float v = (i + 0.5f) / _flamesPerSide * 0.8f;
                AddFlame(root, flame, new Vector2(0f, v), -70f, Random.Range(180f, 280f));
                AddFlame(root, flame, new Vector2(1f, v), 70f, Random.Range(180f, 280f));
            }
        }

        /// <summary>A two-tone flame (orange with a yellow core) standing on a screen edge and flickering.</summary>
        private void AddFlame(RectTransform root, Sprite sprite, Vector2 anchor, float tilt, float height)
        {
            RectTransform pivot = HudWidgets.CreateRect("Flame", root, Vector2.zero, new Vector2(height * 0.7f, height));
            pivot.anchorMin = pivot.anchorMax = anchor;
            pivot.pivot = new Vector2(0.5f, 0.05f);
            pivot.anchoredPosition = Vector2.zero;
            pivot.localRotation = Quaternion.Euler(0f, 0f, tilt);

            Image outer = HudWidgets.CreateImage("Outer", pivot, Vector2.zero, pivot.sizeDelta, sprite, Outer);
            outer.rectTransform.pivot = new Vector2(0.5f, 0.05f);
            outer.rectTransform.anchorMin = outer.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            Image inner = HudWidgets.CreateImage("Core", pivot, Vector2.zero, pivot.sizeDelta * 0.55f, sprite, Inner);
            inner.rectTransform.pivot = new Vector2(0.5f, 0.05f);
            inner.rectTransform.anchorMin = inner.rectTransform.anchorMax = new Vector2(0.5f, 0f);

            // Flicker: each flame stretches and sways at its own pace, on real time.
            float speed = Random.Range(0.18f, 0.32f);
            _tweens.Add(pivot.DOScaleY(Random.Range(0.75f, 0.9f), speed).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetDelay(Random.Range(0f, 0.3f)).SetUpdate(true).SetLink(gameObject));
            _tweens.Add(pivot.DOLocalRotate(new Vector3(0f, 0f, tilt + Random.Range(-8f, 8f)), speed * 1.7f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject));
        }
    }
}
