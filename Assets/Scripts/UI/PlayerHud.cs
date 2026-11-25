using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.UI
{
    /// <summary>
    /// Player HUD: crosshair dot and the pressure and water bars of the connected source.
    /// Interaction prompts live in <see cref="RadialInteractionMenu"/>.
    /// Builds its own overlay canvas on start, so it only needs references.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        private const float BarWidth = 320f;
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomLeft = Vector2.zero;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("References")]
        [SerializeField] private PlayerHoseConnector _hose;

        [Tooltip("Its reference pressure fills the pressure bar, so the bar matches the stream range.")]
        [SerializeField] private WaterFlowTuning _tuning;

        [Header("Crosshair")]
        [Tooltip("Diameter of the center dot, in reference pixels (1920x1080).")]
        [SerializeField, Min(1f)] private float _crosshairSize = 6f;
        [SerializeField] private Color _crosshairColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("Water Bar")]
        [SerializeField] private Color _waterColor = new Color(0.25f, 0.6f, 1f);
        [SerializeField] private Color _lowWaterColor = new Color(0.95f, 0.3f, 0.2f);

        [Tooltip("Fill ratio (0..1) below which the water bar turns to the low color.")]
        [SerializeField, Range(0f, 1f)] private float _lowWaterRatio = 0.2f;

        private Font _font;
        private Sprite _solidSprite;

        private Image _pressureFill;
        private Image _waterFill;

        private void Awake()
        {
            if (_hose == null || _tuning == null)
            {
                Debug.LogError($"{name}: PlayerHud needs Hose and Tuning references.", this);
                enabled = false;
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _solidSprite = CreateCircleSprite(4, 0f, false);

            Transform canvas = CreateCanvas();
            BuildCrosshair(canvas);
            BuildBars(canvas);
        }

        private void Update()
        {
            UpdateBars();
        }

        private void UpdateBars()
        {
            WaterConnectionPoint connected = _hose.Connected;

            // Load: 0 = no pressure (green), 1 = reference pressure (red).
            float load = connected != null
                ? Mathf.Clamp01(connected.Source.Output.Pressure / _tuning.ReferencePressure)
                : 0f;
            _pressureFill.fillAmount = load;
            _pressureFill.color = Color.HSVToRGB(Mathf.Lerp(0.33f, 0f, load), 0.75f, 0.95f);

            float water = GetWaterRatio(connected);
            _waterFill.fillAmount = water;
            _waterFill.color = water < _lowWaterRatio ? _lowWaterColor : _waterColor;
        }

        /// <summary>
        /// 0 when detached; the tank level for a pump (0 without a tank);
        /// 1 for the hydrant, whose network never runs dry.
        /// </summary>
        private static float GetWaterRatio(WaterConnectionPoint connected)
        {
            if (connected == null)
                return 0f;

            if (connected.Source is PumpController pump)
                return pump.Reservoir != null ? pump.Reservoir.FillRatio : 0f;

            return 1f;
        }

        private Transform CreateCanvas()
        {
            GameObject go = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);

            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return go.transform;
        }

        private void BuildCrosshair(Transform canvas)
        {
            Vector2 size = new Vector2(_crosshairSize, _crosshairSize);
            Image dot = CreateImage("Crosshair", canvas, Center, Vector2.zero, size, CreateCircleSprite(32, 0f, true), _crosshairColor);
            dot.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
        }

        private void BuildBars(Transform canvas)
        {
            RectTransform root = CreateRect("Source Bars", canvas, BottomLeft, new Vector2(40f, 40f), new Vector2(BarWidth, 110f));

            _waterFill = CreateBar("Water", root, 0f);
            _pressureFill = CreateBar("Pressure", root, 60f);
        }

        private Image CreateBar(string title, Transform parent, float y)
        {
            RectTransform bar = CreateRect(title, parent, BottomLeft, new Vector2(0f, y), new Vector2(BarWidth, 46f));

            Text label = CreateText("Title", bar, TopLeft, Vector2.zero, new Vector2(BarWidth, 24f), 18, TextAnchor.LowerLeft);
            label.text = title;

            Image background = CreateImage("Background", bar, BottomLeft, Vector2.zero, new Vector2(BarWidth, 18f), _solidSprite, PanelColor);

            Image fill = CreateImage("Fill", background.transform, Center, Vector2.zero, new Vector2(BarWidth - 4f, 14f), _solidSprite, Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;

            return fill;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Image image = CreateRect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Text text = CreateRect(name, parent, anchor, position, size).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Shadow>();
            return text;
        }

        /// <summary>
        /// White sprite: a disc (innerRatio = 0) or a ring; round = false gives a plain square.
        /// Filled images need a sprite, so even the bars use one.
        /// </summary>
        private static Sprite CreateCircleSprite(int size, float innerRatio, bool round)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            float half = size * 0.5f;
            float edge = 1.5f / half; // anti-aliasing width, as a share of the radius
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    if (round)
                    {
                        float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                        float outer = Mathf.Clamp01((1f - radius) / edge);
                        float inner = innerRatio > 0f ? Mathf.Clamp01((radius - innerRatio) / edge) : 1f;
                        alpha = Mathf.Min(outer, inner);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
