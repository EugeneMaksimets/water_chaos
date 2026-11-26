using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Mission;

namespace WaterChaos.UI
{
    /// <summary>
    /// Draws every shown <see cref="MissionMarker"/> as a round "!" badge over its object, with the distance under it.
    /// Markers outside the view stick to the screen edge, so the player can find a far object (the factory, the pool).
    /// One look for every stage; markers whose object is out of the world (an item in the inventory) are skipped.
    /// </summary>
    public sealed class MissionMarkerView : MonoBehaviour
    {
        private sealed class Badge
        {
            public RectTransform Root;
            public RectTransform Body;
            public Text Distance;
        }

        [Header("References")]
        [Tooltip("Camera the markers are projected with; empty = Camera.main.")]
        [SerializeField] private Camera _camera;

        [Header("Visibility")]
        [Tooltip("Markers farther than this are hidden, in meters (m); 0 = any distance.")]
        [SerializeField, Min(0f)] private float _maxDistance;

        [Tooltip("Markers outside the view stick to the screen edge instead of disappearing.")]
        [SerializeField] private bool _showOffscreen = true;

        [Tooltip("Distance of an edge marker from the screen border, in reference pixels.")]
        [SerializeField, Min(0f)] private float _edgeMargin = 70f;

        [Header("Look")]
        [Tooltip("Badge diameter, in reference pixels (1920x1080).")]
        [SerializeField, Min(16f)] private float _size = 54f;

        [Tooltip("Draw order of the marker canvas; below the other HUD canvases.")]
        [SerializeField] private int _sortingOrder = -10;

        private readonly List<Badge> _badges = new List<Badge>();
        private RectTransform _canvasRect;
        private Sprite _disc;
        private Sprite _ring;

        private void Awake()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Mission Marker Canvas", _sortingOrder);
            _canvasRect = (RectTransform)canvas.transform;
            _disc = HudWidgets.CreateCircleSprite(128, 0f);
            _ring = HudWidgets.CreateCircleSprite(128, 0.8f);
        }

        private void Start()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                Debug.LogError($"{name}: MissionMarkerView found no camera.", this);
        }

        private void LateUpdate()
        {
            int used = 0;
            if (_camera != null)
            {
                IReadOnlyList<MissionMarker> markers = MissionMarker.Shown;
                for (int i = 0; i < markers.Count; i++)
                {
                    if (TryPlace(markers[i], used))
                        used++;
                }
            }

            for (int i = used; i < _badges.Count; i++)
            {
                if (_badges[i].Root.gameObject.activeSelf)
                    _badges[i].Root.gameObject.SetActive(false);
            }
        }

        private bool TryPlace(MissionMarker marker, int index)
        {
            if (marker == null || !marker.isActiveAndEnabled)
                return false;

            Vector3 world = marker.WorldPosition;
            float distance = Vector3.Distance(_camera.transform.position, world);
            if (_maxDistance > 0f && distance > _maxDistance)
                return false;

            Vector3 screen = _camera.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            if (behind)
                screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f);

            bool onScreen = !behind && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            if (!onScreen && !_showOffscreen)
                return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
            if (!onScreen || behind)
                local = ClampToEdge(local, behind);

            Badge badge = GetBadge(index);
            if (!badge.Root.gameObject.activeSelf)
                badge.Root.gameObject.SetActive(true);

            float time = Time.unscaledTime;
            float bob = onScreen ? Mathf.Sin(time * 3f) * 5f : 0f;
            badge.Root.anchoredPosition = local;
            badge.Body.anchoredPosition = new Vector2(0f, bob);
            badge.Body.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(time * 5f));
            badge.Distance.text = $"{Mathf.RoundToInt(distance)} м";
            return true;
        }

        private Vector2 ClampToEdge(Vector2 local, bool behind)
        {
            Vector2 half = _canvasRect.rect.size * 0.5f - new Vector2(_edgeMargin, _edgeMargin);

            // Behind the camera: push it to the edge along its direction so it never sits mid-screen.
            if (behind && local.sqrMagnitude > 0f)
                local = local.normalized * (half.magnitude * 2f);

            return new Vector2(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y));
        }

        private Badge GetBadge(int index)
        {
            while (_badges.Count <= index)
                _badges.Add(CreateBadge(_badges.Count));

            return _badges[index];
        }

        private Badge CreateBadge(int number)
        {
            Vector2 size = new Vector2(_size, _size);
            RectTransform root = HudWidgets.CreateRect($"Marker {number}", _canvasRect, Vector2.zero, size);
            RectTransform body = HudWidgets.CreateRect("Body", root, Vector2.zero, size);

            HudWidgets.CreateImage("Shadow", body, new Vector2(3f, -3f), size, _disc, HudWidgets.CardShadowColor);
            HudWidgets.CreateImage("Outline", body, Vector2.zero, size, _disc, HudWidgets.AccentColor);
            HudWidgets.CreateImage("Inner", body, Vector2.zero, size * 0.8f, _disc, HudWidgets.CardInnerColor);
            HudWidgets.CreateImage("Ring", body, Vector2.zero, size * 0.8f, _ring, new Color(1f, 1f, 1f, 0.25f));

            Text mark = HudWidgets.CreateText("Mark", body, new Vector2(0f, 1f), size, Mathf.RoundToInt(_size * 0.55f), TextAnchor.MiddleCenter);
            mark.text = "!";
            mark.fontStyle = FontStyle.Bold;
            mark.color = HudWidgets.AccentColor;

            Text distance = HudWidgets.CreateText("Distance", root, new Vector2(0f, -_size * 0.5f - 14f), new Vector2(120f, 24f), 17, TextAnchor.MiddleCenter);
            distance.fontStyle = FontStyle.Bold;
            Object.Destroy(distance.GetComponent<Shadow>());
            HudWidgets.AddOutline(distance);

            return new Badge { Root = root, Body = body, Distance = distance };
        }
    }
}
