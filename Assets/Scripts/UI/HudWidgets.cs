using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>
    /// Builders for HUD pieces in the same look as the PlayerHud key prompt
    /// (dark round panel, white ring, bold key letter, label underneath, built-in font with shadow).
    /// </summary>
    public static class HudWidgets
    {
        public const float PromptSize = 72f;
        private const float MouseIconWidth = 28f;
        private const float MouseIconHeight = 40f;
        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.2f);
        public static readonly Color ProblemColor = new Color(1f, 0.55f, 0.45f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Font s_font;

        public static Font Font => s_font != null ? s_font : (s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Screen-space overlay canvas scaled for 1920x1080, child of <paramref name="parent"/>.</summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// Round prompt for a player control: a mouse icon with the bound button lit for mouse bindings,
        /// otherwise the key name from the binding (e.g. "F").
        /// </summary>
        public static KeyPrompt CreateKeyPrompt(string name, Transform parent, PlayerControl control, Vector2 position)
        {
            if (!PlayerControls.IsMouseButton(control, out bool isLeft, out bool isRight))
                return CreateKeyPrompt(name, parent, PlayerControls.GetDisplayName(control), position);

            KeyPrompt prompt = CreateKeyPrompt(name, parent, string.Empty, position, out RectTransform root);
            Sprite mouse = CreateMouseSprite(isLeft, isRight);
            CreateImage("Mouse Icon", root, Vector2.zero, new Vector2(MouseIconWidth, MouseIconHeight), mouse, Color.white);
            return prompt;
        }

        /// <summary>Round key prompt: panel, ring track, progress ring, key letter and a label under it.</summary>
        public static KeyPrompt CreateKeyPrompt(string name, Transform parent, string key, Vector2 position)
        {
            return CreateKeyPrompt(name, parent, key, position, out _);
        }

        private static KeyPrompt CreateKeyPrompt(string name, Transform parent, string key, Vector2 position, out RectTransform root)
        {
            Sprite disc = CreateCircleSprite(128, 0f);
            Sprite ring = CreateCircleSprite(128, 0.82f);
            Vector2 size = new Vector2(PromptSize, PromptSize);

            root = CreateRect(name, parent, position, size);
            CreateImage("Background", root, Vector2.zero, size, disc, PanelColor);
            CreateImage("Track", root, Vector2.zero, size, ring, TrackColor);

            Image progress = CreateImage("Progress", root, Vector2.zero, size, ring, Color.white);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Radial360;
            progress.fillOrigin = (int)Image.Origin360.Top;
            progress.fillClockwise = true;
            progress.fillAmount = 1f;

            Text keyText = CreateText("Key", root, Vector2.zero, size, 30, TextAnchor.MiddleCenter);
            keyText.text = key;
            keyText.fontStyle = FontStyle.Bold;

            Text label = CreateText("Label", root, new Vector2(0f, -56f), new Vector2(520f, 30f), 20, TextAnchor.MiddleCenter);

            root.gameObject.SetActive(false);
            return new KeyPrompt(root, progress, label);
        }

        public static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Center;
            rect.anchorMax = Center;
            rect.pivot = Center;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Image image = CreateRect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Text text = CreateRect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Shadow>();
            return text;
        }

        /// <summary>
        /// White mouse outline: an oval body split into two buttons; the lit button is filled.
        /// Drawn in code like the other prompt sprites, so no texture asset is needed.
        /// </summary>
        public static Sprite CreateMouseSprite(bool leftLit, bool rightLit)
        {
            const int width = 56;
            const int height = 80;
            const float line = 4f;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float rx = cx - 1f;
            float ry = cy - 1f;
            float split = cy + ry * 0.15f; // buttons are above this line (texture y goes up)
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = (px - cx) / rx;
                    float dy = (py - cy) / ry;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 0f;

                    if (radius <= 1f)
                    {
                        // Distance to the edge in pixels, approximated along the smaller radius.
                        float edge = (1f - radius) * Mathf.Min(rx, ry);
                        bool outline = edge < line;
                        bool buttonArea = py > split;
                        bool divider = buttonArea && Mathf.Abs(px - cx) < line * 0.5f;
                        bool splitLine = Mathf.Abs(py - split) < line * 0.5f;
                        bool lit = buttonArea && ((leftLit && px < cx) || (rightLit && px > cx));

                        if (outline || divider || splitLine)
                            alpha = 1f;
                        else if (lit)
                            alpha = 0.95f;
                        else
                            alpha = 0.15f;

                        alpha *= Mathf.Clamp01(edge + 0.5f); // soft outer edge
                    }

                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>White anti-aliased disc (innerRatio = 0) or ring sprite.</summary>
        public static Sprite CreateCircleSprite(int size, float innerRatio)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            float half = size * 0.5f;
            float edge = 1.5f / half;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float outer = Mathf.Clamp01((1f - radius) / edge);
                    float inner = innerRatio > 0f ? Mathf.Clamp01((radius - innerRatio) / edge) : 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(outer, inner) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
