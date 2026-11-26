using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;
using WaterChaos.WaterTransfer;

namespace WaterChaos.UI
{
    /// <summary>
    /// Transfer hose guidance in the PlayerHud style: "Повернись до насоса й увімкни його" after a hose is placed,
    /// and a status line that only shows problems (no flow or liters), including a failed placement.
    /// Actions themselves (pump menu, pull, place) are in the interaction menu.
    /// </summary>
    public sealed class TransferHoseHud : MonoBehaviour
    {
        private const float InstructionY = -215f;
        private const float StatusY = -300f;
        private const float PlaceFailedShowTime = 2f;

        private const string ReturnText = "Повернись до насоса й увімкни його";
        private const string PlaceFailedText = "Тут немає землі — шланг не покласти";

        [Header("References")]
        [SerializeField] private PlayerTransferHoseInteractor _interactor;

        [Tooltip("Interaction menu; the instruction hides while it is open. Optional.")]
        [SerializeField] private RadialInteractionMenu _menu;

        private KeyPrompt _instruction;
        private Text _status;

        private TransferHose _statusHose;
        private WaterTransferController _transfer;
        private int _shownStatusKey = int.MinValue;
        private float _placeFailedTime = -1f;

        private void Awake()
        {
            if (_interactor == null)
            {
                Debug.LogError($"{name}: TransferHoseHud needs an Interactor reference.", this);
                enabled = false;
                return;
            }

            Transform canvas = HudWidgets.CreateCanvas(transform, "Transfer HUD Canvas", 5).transform;
            _instruction = HudWidgets.CreateKeyPrompt("Transfer Instruction", canvas, "!", new Vector2(0f, InstructionY));

            _status = HudWidgets.CreateText("Transfer Status", canvas, new Vector2(0f, StatusY), new Vector2(1000f, 32f), 20, TextAnchor.MiddleCenter);
            _status.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_interactor != null)
            {
                _interactor.HosePlaced += OnHosePlaced;
                _interactor.PlaceFailed += OnPlaceFailed;
            }
        }

        private void OnDisable()
        {
            if (_interactor != null)
            {
                _interactor.HosePlaced -= OnHosePlaced;
                _interactor.PlaceFailed -= OnPlaceFailed;
            }
        }

        private void OnHosePlaced() => _placeFailedTime = -1f;

        private void OnPlaceFailed() => _placeFailedTime = Time.time;

        private void Update()
        {
            bool menuOpen = _menu != null && _menu.IsOpen;

            if (_interactor.IsAwaitingPumpStart && !menuOpen)
                _instruction.Show(ReturnText, 0f, 1f);
            else
                _instruction.Hide();

            UpdateStatus();
        }

        /// <summary>Flow and problems of the active transfer hose; rebuilt only when what it shows changes.</summary>
        private void UpdateStatus()
        {
            TransferHose hose = _interactor.ActiveHose;
            if (hose != _statusHose)
            {
                _statusHose = hose;
                _transfer = hose != null ? hose.GetComponent<WaterTransferController>() : null;
                _shownStatusKey = int.MinValue;
            }

            bool placeFailed = _placeFailedTime >= 0f && Time.time - _placeFailedTime < PlaceFailedShowTime;
            TransferStatus status = _transfer != null ? _transfer.Status : TransferStatus.NotConnected;
            string text = placeFailed ? PlaceFailedText : GetStatusText(status);
            if (text == null)
            {
                if (_status.gameObject.activeSelf)
                    _status.gameObject.SetActive(false);
                _shownStatusKey = int.MinValue;
                return;
            }

            int key = placeFailed ? -1 : (int)status;
            if (key == _shownStatusKey)
                return;

            _shownStatusKey = key;
            _status.gameObject.SetActive(true);
            _status.text = text;
            _status.color = HudWidgets.ProblemColor;
        }

        /// <summary>Problem text; null hides the line (normal work and steps the prompts already explain).</summary>
        private static string GetStatusText(TransferStatus status)
        {
            switch (status)
            {
                case TransferStatus.OutletBusy: return "Вихід насоса зайнятий шлангом ствола";
                case TransferStatus.SourceEmpty: return "Немає води: резервуар-джерело порожній";
                case TransferStatus.NoWater: return "Немає джерела води: опусти насос у воду";
                case TransferStatus.DestinationFull: return "Резервуар, куди качаєш, повний";
                case TransferStatus.Interrupted: return "Насос працює — потік перервано";
                default: return null;
            }
        }
    }
}
