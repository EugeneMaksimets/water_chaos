using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Rooms;

namespace WaterChaos.UI
{
    /// <summary>
    /// Cartoon panel with the temperature of the room the player is in: thermometer, big value,
    /// fire and cooler counts and a status pill. One panel for all rooms; hidden outside every room.
    /// Only reads the room; builds its own overlay canvas on start, so it only needs the visitor.
    /// </summary>
    public sealed class RoomTemperatureUI : MonoBehaviour
    {
        private enum State
        {
            Normal,
            High,
            Critical,
            Cooling,
            Freezing
        }

        private static readonly Vector2 PanelSize = new Vector2(360f, 220f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Color InnerColor = new Color(0.1f, 0.09f, 0.14f, 0.9f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color TubeColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("References")]
        [Tooltip("Player's room visitor; found on this object or its parents if empty.")]
        [SerializeField] private RoomVisitor _visitor;

        [Header("Layout")]
        [Tooltip("Panel offset from the top-right screen corner, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 _offset = new Vector2(-40f, -40f);

        [Header("Thresholds")]
        [Tooltip("From this temperature (°C) the panel shows a warning.")]
        [SerializeField] private float _highTemperature = 60f;

        [Tooltip("From this temperature (°C) the panel shows a critical warning.")]
        [SerializeField] private float _criticalTemperature = 100f;

        [Tooltip("Thermometer range, in degrees Celsius (°C).")]
        [SerializeField] private Vector2 _thermometerRange = new Vector2(-100f, 150f);

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.45f, 0.9f, 0.4f);
        [SerializeField] private Color _highColor = new Color(1f, 0.72f, 0.15f);
        [SerializeField] private Color _criticalColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color _coolingColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color _freezingColor = new Color(0.45f, 0.95f, 1f);

        [Header("Animation")]
        [Tooltip("How fast the shown value follows the real one (1/s).")]
        [SerializeField, Min(0.1f)] private float _valueSmoothing = 6f;

        private GameObject _root;
        private RectTransform _panel;
        private Image _outline;
        private Image _thermoFill;
        private Image _bulb;
        private Text _roomName;
        private Text _value;
        private Text _fires;
        private Text _coolers;
        private Image _statusPill;
        private Text _status;
        private RectTransform _badge;
        private Image _badgeFlame;
        private Image _badgeFlameCore;
        private Image _badgeSnow;
        private RectTransform _fireIcon;
        private RectTransform _snowIcon;

        private RoomTemperature _shownRoom;
        private float _shownTemperature;
        private int _shownValue = int.MinValue;
        private int _shownFires = -1;
        private int _shownCoolers = -1;
        private string _shownName;
        private State _shownState = (State)(-1);

        private void Awake()
        {
            if (_visitor == null)
                _visitor = GetComponentInParent<RoomVisitor>();

            if (_visitor == null)
            {
                Debug.LogError($"{name}: RoomTemperatureUI needs a RoomVisitor reference.", this);
                enabled = false;
                return;
            }

            Build(HudWidgets.CreateCanvas(transform, "Room Temperature Canvas", 3).transform);
            _root.SetActive(false);
        }

        private void OnDisable()
        {
            if (_root != null)
                _root.SetActive(false);
            _shownRoom = null;
        }

        private void LateUpdate()
        {
            RoomTemperature room = _visitor.CurrentRoom;
            if (room == null)
            {
                if (_root.activeSelf)
                    _root.SetActive(false);
                _shownRoom = null;
                return;
            }

            if (room != _shownRoom)
            {
                // New room: show its value at once instead of sliding from the previous room.
                _shownRoom = room;
                _shownTemperature = room.Temperature;
                _root.SetActive(true);
            }

            float dt = Time.unscaledDeltaTime;
            _shownTemperature = Mathf.Lerp(_shownTemperature, room.Temperature, 1f - Mathf.Exp(-_valueSmoothing * dt));
            if (Mathf.Abs(_shownTemperature - room.Temperature) < 0.05f)
                _shownTemperature = room.Temperature;

            UpdateTexts(room);
            State state = GetState(room);
            if (state != _shownState)
                ApplyState(state);
            Animate(state, Time.unscaledTime);
        }

        private State GetState(RoomTemperature room)
        {
            float temperature = room.Temperature;
            if (temperature < 0f)
                return State.Freezing;
            if (room.IsCoolingActive)
                return State.Cooling;
            if (temperature >= _criticalTemperature)
                return State.Critical;
            if (temperature >= _highTemperature)
                return State.High;
            return State.Normal;
        }

        // Strings are rebuilt only when a shown number changes, so the panel does not allocate every frame.
        private void UpdateTexts(RoomTemperature room)
        {
            if (!ReferenceEquals(_shownName, room.DisplayName))
            {
                _shownName = room.DisplayName;
                _roomName.text = _shownName.ToUpperInvariant();
            }

            int value = Mathf.RoundToInt(_shownTemperature);
            if (value != _shownValue)
            {
                _shownValue = value;
                _value.text = value + "°C";
            }

            if (room.ActiveFireCount != _shownFires)
            {
                _shownFires = room.ActiveFireCount;
                _fires.text = "Fires: " + _shownFires;
            }

            if (room.ActiveCoolerCount != _shownCoolers)
            {
                _shownCoolers = room.ActiveCoolerCount;
                _coolers.text = "Cooling: " + _shownCoolers;
            }

            float range = Mathf.Max(_thermometerRange.y - _thermometerRange.x, 1f);
            _thermoFill.fillAmount = Mathf.Clamp01((_shownTemperature - _thermometerRange.x) / range);
        }

        private void ApplyState(State state)
        {
            _shownState = state;
            Color color = ColorOf(state);

            _outline.color = color;
            _thermoFill.color = color;
            _bulb.color = color;
            _value.color = color;
            _statusPill.color = color;
            _status.text = StatusText(state);

            bool hot = state == State.High || state == State.Critical;
            bool cold = state == State.Cooling || state == State.Freezing;
            _badge.gameObject.SetActive(hot || cold);
            _badgeFlame.gameObject.SetActive(hot);
            _badgeFlameCore.gameObject.SetActive(hot);
            _badgeSnow.gameObject.SetActive(cold);
            _badgeFlame.color = color;
            _badgeSnow.color = color;
        }

        private void Animate(State state, float time)
        {
            Vector2 shake = Vector2.zero;
            float valueScale = 1f;
            float badgeScale = 1f;
            float badgeAngle = 0f;

            switch (state)
            {
                case State.High:
                    valueScale = 1f + 0.04f * Mathf.Sin(time * 4f);
                    badgeScale = 1f + 0.08f * Mathf.Sin(time * 6f);
                    break;

                case State.Critical:
                    valueScale = 1f + 0.1f * Mathf.Abs(Mathf.Sin(time * 7f));
                    badgeScale = 1f + 0.18f * Mathf.PerlinNoise(time * 9f, 0.3f);
                    badgeAngle = 10f * (Mathf.PerlinNoise(time * 6f, 0.7f) * 2f - 1f);
                    shake = new Vector2(Mathf.Sin(time * 53f), Mathf.Sin(time * 47f)) * 2.5f;
                    break;

                case State.Cooling:
                    badgeAngle = -time * 60f;
                    valueScale = 1f + 0.03f * Mathf.Sin(time * 2.5f);
                    break;

                case State.Freezing:
                    badgeAngle = -time * 25f;
                    badgeScale = 1f + 0.06f * Mathf.Sin(time * 3f);
                    break;
            }

            _panel.anchoredPosition = _offset + shake;
            _value.rectTransform.localScale = new Vector3(valueScale, valueScale, 1f);
            _badge.localScale = new Vector3(badgeScale, badgeScale, 1f);
            _badge.localRotation = Quaternion.Euler(0f, 0f, badgeAngle);

            // Row icons: the flame flickers while fires burn, the snowflake spins while coolers work.
            float flicker = _shownFires > 0 ? 1f + 0.12f * Mathf.PerlinNoise(time * 10f, 1.3f) : 1f;
            _fireIcon.localScale = new Vector3(flicker, flicker, 1f);
            _snowIcon.localRotation = Quaternion.Euler(0f, 0f, _shownCoolers > 0 ? -time * 90f : 0f);
        }

        private Color ColorOf(State state)
        {
            switch (state)
            {
                case State.High: return _highColor;
                case State.Critical: return _criticalColor;
                case State.Cooling: return _coolingColor;
                case State.Freezing: return _freezingColor;
                default: return _normalColor;
            }
        }

        private static string StatusText(State state)
        {
            switch (state)
            {
                case State.High: return "HEATING UP!";
                case State.Critical: return "CRITICAL HEAT!";
                case State.Cooling: return "COOLING ACTIVE";
                case State.Freezing: return "FREEZING";
                default: return "NORMAL";
            }
        }

        // ───────────── Build ─────────────

        private void Build(Transform canvas)
        {
            Sprite rounded = CreateRoundedSprite(64, 22);
            Sprite disc = HudWidgets.CreateCircleSprite(64, 0f);
            Sprite flame = CreateFlameSprite(64);
            Sprite snow = CreateSnowflakeSprite(64);

            _root = new GameObject("Room Temperature", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            RectTransform rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _panel = HudWidgets.CreateRect("Panel", _root.transform, Vector2.zero, PanelSize);
            _panel.anchorMin = TopRight;
            _panel.anchorMax = TopRight;
            _panel.pivot = TopRight;
            _panel.anchoredPosition = _offset;

            Sliced(HudWidgets.CreateImage("Shadow", _panel, new Vector2(7f, -7f), PanelSize, rounded, ShadowColor));
            _outline = Sliced(HudWidgets.CreateImage("Outline", _panel, Vector2.zero, PanelSize, rounded, _normalColor));
            Sliced(HudWidgets.CreateImage("Inner", _panel, Vector2.zero, PanelSize - new Vector2(12f, 12f), rounded, InnerColor));

            BuildThermometer(_panel, rounded, disc);

            Text title = Label("Title", new Vector2(30f, 84f), new Vector2(280f, 26f), 19, TextAnchor.MiddleCenter);
            title.text = "ROOM TEMPERATURE";
            title.fontStyle = FontStyle.Bold;

            _roomName = Label("Room", new Vector2(30f, 62f), new Vector2(280f, 22f), 15, TextAnchor.MiddleCenter);
            _roomName.color = new Color(1f, 1f, 1f, 0.7f);

            _value = Label("Value", new Vector2(30f, 18f), new Vector2(280f, 70f), 60, TextAnchor.MiddleCenter);
            _value.fontStyle = FontStyle.Bold;

            _fireIcon = Icon("Fire Icon", flame, new Vector2(-60f, -36f), new Color(1f, 0.5f, 0.15f));
            Image core = HudWidgets.CreateImage("Core", _fireIcon, new Vector2(0f, -3f), new Vector2(12f, 14f), flame, new Color(1f, 0.9f, 0.35f));
            core.preserveAspect = true;
            _fires = Label("Fires", new Vector2(2f, -36f), new Vector2(100f, 26f), 18, TextAnchor.MiddleLeft);

            _snowIcon = Icon("Snow Icon", snow, new Vector2(60f, -36f), new Color(0.55f, 0.85f, 1f));
            _coolers = Label("Coolers", new Vector2(126f, -36f), new Vector2(100f, 26f), 18, TextAnchor.MiddleLeft);

            _statusPill = Sliced(HudWidgets.CreateImage("Status", _panel, new Vector2(30f, -76f), new Vector2(240f, 34f), rounded, _normalColor));
            _status = HudWidgets.CreateText("Text", _statusPill.transform, Vector2.zero, new Vector2(240f, 34f), 17, TextAnchor.MiddleCenter);
            _status.fontStyle = FontStyle.Bold;
            _status.color = new Color(0.08f, 0.08f, 0.12f);
            Destroy(_status.GetComponent<Shadow>());

            // Corner badge: a flame when hot, a snowflake when cold.
            _badge = HudWidgets.CreateRect("Badge", _panel, new Vector2(PanelSize.x * 0.5f - 6f, PanelSize.y * 0.5f - 6f), new Vector2(56f, 56f));
            _badgeFlame = HudWidgets.CreateImage("Flame", _badge, Vector2.zero, new Vector2(56f, 56f), flame, _criticalColor);
            _badgeFlameCore = HudWidgets.CreateImage("Flame Core", _badge, new Vector2(0f, -9f), new Vector2(28f, 30f), flame, new Color(1f, 0.92f, 0.4f));
            _badgeSnow = HudWidgets.CreateImage("Snow", _badge, Vector2.zero, new Vector2(56f, 56f), snow, _coolingColor);
            AddOutline(_badgeFlame);
            AddOutline(_badgeSnow);
            _badge.gameObject.SetActive(false);
        }

        private void BuildThermometer(RectTransform panel, Sprite rounded, Sprite disc)
        {
            Vector2 tubeSize = new Vector2(24f, 120f);
            Vector2 tubePosition = new Vector2(-138f, 14f);

            Image tube = Sliced(HudWidgets.CreateImage("Thermometer", panel, Vector2.zero, tubeSize, rounded, TubeColor));
            tube.rectTransform.anchoredPosition = tubePosition;
            AddOutline(tube);

            _thermoFill = HudWidgets.CreateImage("Fill", tube.transform, Vector2.zero, tubeSize - new Vector2(10f, 10f), CreateSolidSprite(), _normalColor);
            _thermoFill.type = Image.Type.Filled;
            _thermoFill.fillMethod = Image.FillMethod.Vertical;
            _thermoFill.fillOrigin = (int)Image.OriginVertical.Bottom;

            Image bulbBack = HudWidgets.CreateImage("Bulb Back", panel, Vector2.zero, new Vector2(46f, 46f), disc, TubeColor);
            bulbBack.rectTransform.anchoredPosition = tubePosition + new Vector2(0f, -tubeSize.y * 0.5f - 12f);
            AddOutline(bulbBack);
            _bulb = HudWidgets.CreateImage("Bulb", bulbBack.transform, Vector2.zero, new Vector2(34f, 34f), disc, _normalColor);
        }

        private Text Label(string labelName, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Text text = HudWidgets.CreateText(labelName, _panel, position, size, fontSize, alignment);
            AddOutline(text);
            return text;
        }

        private RectTransform Icon(string iconName, Sprite sprite, Vector2 position, Color color)
        {
            Image image = HudWidgets.CreateImage(iconName, _panel, position, new Vector2(26f, 26f), sprite, color);
            image.preserveAspect = true;
            AddOutline(image);
            return image.rectTransform;
        }

        private static Image Sliced(Image image)
        {
            image.type = Image.Type.Sliced;
            return image;
        }

        // Thick dark outline: the cartoon look of the game.
        private static void AddOutline(Graphic graphic)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        // ───────────── Sprites (drawn in code like the other HUD sprites, no texture assets) ─────────────

        /// <summary>White rounded square, 9-sliced so it stretches to any panel size.</summary>
        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                    float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            Texture2D texture = CreateTexture(size, pixels);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>White teardrop flame: a round base narrowing to a tip.</summary>
        private static Sprite CreateFlameSprite(int size)
        {
            const float baseY = 0.35f;
            const float radius = 0.3f;
            const float tipY = 0.97f;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float edge;

                    if (v <= baseY)
                    {
                        edge = radius - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, baseY));
                    }
                    else
                    {
                        float t = Mathf.Clamp01((v - baseY) / (tipY - baseY));
                        // Slight lean to the right makes it read as a flickering flame.
                        float center = 0.5f + 0.06f * t * t;
                        float halfWidth = radius * Mathf.Sqrt(1f - t);
                        edge = halfWidth - Mathf.Abs(u - center);
                    }

                    float alpha = Mathf.Clamp01(edge * size + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Sprite.Create(CreateTexture(size, pixels), new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>White six-armed snowflake with a pair of small branches on every arm.</summary>
        private static Sprite CreateSnowflakeSprite(int size)
        {
            const float arm = 0.44f;
            const float branch = 0.14f;
            const float thickness = 0.045f;
            Vector2 center = new Vector2(0.5f, 0.5f);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float distance = float.MaxValue;

                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 60f * Mathf.Deg2Rad;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector2 tip = center + direction * arm;
                        distance = Mathf.Min(distance, DistanceToSegment(p, center, tip));

                        Vector2 joint = center + direction * (arm * 0.6f);
                        Vector2 left = new Vector2(Mathf.Cos(angle + 0.8f), Mathf.Sin(angle + 0.8f)) * branch;
                        Vector2 right = new Vector2(Mathf.Cos(angle - 0.8f), Mathf.Sin(angle - 0.8f)) * branch;
                        distance = Mathf.Min(distance, DistanceToSegment(p, joint, joint + left));
                        distance = Mathf.Min(distance, DistanceToSegment(p, joint, joint + right));
                    }

                    float alpha = Mathf.Clamp01((thickness - distance) * size + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Sprite.Create(CreateTexture(size, pixels), new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        private static Sprite CreateSolidSprite()
        {
            const int size = 4;
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);

            return Sprite.Create(CreateTexture(size, pixels), new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Texture2D CreateTexture(int size, Color32[] pixels)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
