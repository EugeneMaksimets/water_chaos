using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WaterChaos.UI
{
    /// <summary>
    /// Main menu: the game title and the Start button over the 3D background scene.
    /// Start fades the screen to black and loads the game scene.
    /// The UI is built in code (like the other HUD views), so the scene only needs this component and an EventSystem.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Flow")]
        [Tooltip("Scene loaded by Start; must be in Build Profiles / Build Settings.")]
        [SerializeField] private string _gameScene = "WaterChaos_MiniVillage";

        [Tooltip("Fade to black before loading, in seconds.")]
        [SerializeField, Min(0f)] private float _fadeOutTime = 0.6f;

        [Header("Text")]
        [SerializeField] private string _title = "WATER CHAOS";
        [SerializeField] private string _subtitle = "Пожежна команда 67";
        [SerializeField] private string _startLabel = "СТАРТ";

        private static readonly Color ButtonColor = new Color(0.78f, 0.12f, 0.08f);

        private Image _fade;
        private bool _starting;

        private void Awake()
        {
            BuildUi();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            StartCoroutine(Fade(1f, 0f, 0.8f));
        }

        public void StartGame()
        {
            if (_starting)
                return;

            _starting = true;
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            yield return Fade(0f, 1f, _fadeOutTime);
            SceneManager.LoadScene(_gameScene);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            _fade.raycastTarget = true;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }

            SetFade(to);
            _fade.raycastTarget = to > 0f;
        }

        private void SetFade(float alpha)
        {
            _fade.color = new Color(0f, 0f, 0f, alpha);
        }

        private void BuildUi()
        {
            Canvas canvas = HudWidgets.CreateCanvas(transform, "Main Menu Canvas", 0);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            Transform root = canvas.transform;

            // Darkens the left side, so the text stays readable over the fire.
            Image shade = HudWidgets.CreateImage("Shade", root, new Vector2(-620f, 0f), new Vector2(900f, 1080f), null, new Color(0f, 0f, 0f, 0.45f));
            shade.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            shade.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            shade.rectTransform.sizeDelta = new Vector2(900f, 0f);

            Text title = HudWidgets.CreateText("Title", root, new Vector2(-560f, 220f), new Vector2(800f, 140f), 104, TextAnchor.MiddleCenter);
            title.text = _title;
            title.fontStyle = FontStyle.Bold;
            title.color = HudWidgets.AccentColor;
            HudWidgets.AddOutline(title);

            Text subtitle = HudWidgets.CreateText("Subtitle", root, new Vector2(-560f, 130f), new Vector2(800f, 60f), 36, TextAnchor.MiddleCenter);
            subtitle.text = _subtitle;

            CreateButton(root, _startLabel, new Vector2(-560f, -60f), StartGame);

            _fade = HudWidgets.CreateImage("Fade", root, Vector2.zero, Vector2.zero, null, Color.black);
            _fade.rectTransform.anchorMin = Vector2.zero;
            _fade.rectTransform.anchorMax = Vector2.one;
        }

        private static void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            Image background = HudWidgets.CreateImage(label + " Button", parent, position, new Vector2(380f, 100f),
                HudWidgets.CreateRoundedSprite(64, 20), Color.white);
            background.type = Image.Type.Sliced;
            background.raycastTarget = true;

            Button button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = new Color(0.95f, 0.3f, 0.12f);
            colors.pressedColor = new Color(0.55f, 0.08f, 0.05f);
            colors.selectedColor = ButtonColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            Text text = HudWidgets.CreateText("Label", background.transform, Vector2.zero, new Vector2(380f, 100f), 48, TextAnchor.MiddleCenter);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
        }
    }
}
