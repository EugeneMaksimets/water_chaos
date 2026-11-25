using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;
using WaterChaos.WaterTransfer;

namespace WaterChaos.UI
{
    /// <summary>
    /// Transfer hose prompts in the PlayerHud key-prompt style:
    /// E (pump menu / pull hose), "Press F to Place Hose", "Return to the Pump and Start It",
    /// and a status line that only shows problems (no flow or liters).
    /// </summary>
    public sealed class TransferHoseHud : MonoBehaviour
    {
        private const float PromptY = -90f;
        private const float InstructionY = -215f;
        private const float StatusY = -300f;

        /// <summary>Shift sideways when the regular HUD shows its pick up prompt at the center, in canvas pixels.</summary>
        private const float SideOffset = 170f;

        private const float PlaceFailedShowTime = 2f;

        private const string MenuText = "Pump Menu";
        private const string PullText = "Pull Hose";
        private const string PlaceCarriedFirstText = "Place the carried hose first";
        private const string PlaceText = "Press F to Place Hose";
        private const string ReturnText = "Return to the Pump and Start It";
        private const string PlaceFailedText = "No ground here - cannot place the hose";

        [Header("References")]
        [SerializeField] private PlayerTransferHoseInteractor _interactor;

        [Tooltip("Pump menu; prompts hide while it is open. Optional.")]
        [SerializeField] private PumpRadialMenu _menu;

        [Tooltip("Regular pick up target; the E prompt moves aside when the Q prompt is shown. Optional.")]
        [SerializeField] private PlayerItemInteractor _itemInteractor;

        private KeyPrompt _interactPrompt;
        private KeyPrompt _placePrompt;
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
            _interactPrompt = HudWidgets.CreateKeyPrompt("Transfer Interact Prompt", canvas, "E", new Vector2(0f, PromptY));
            _placePrompt = HudWidgets.CreateKeyPrompt("Transfer Place Prompt", canvas, "F", new Vector2(0f, PromptY));
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
            float x = _itemInteractor != null && _itemInteractor.Target != null ? -SideOffset : 0f;

            UpdateInteractPrompt(menuOpen, x);
            UpdatePlacePrompt(menuOpen, x);

            if (_interactor.IsAwaitingPumpStart && !menuOpen)
                _instruction.Show(ReturnText, 0f, 1f);
            else
                _instruction.Hide();

            UpdateStatus();
        }

        private void UpdateInteractPrompt(bool menuOpen, float x)
        {
            if (menuOpen)
                _interactPrompt.Hide();
            else if (_interactor.CanOpenMenu)
                _interactPrompt.Show(MenuText, x, 1f);
            else if (_interactor.TargetAction == TransferHoseAction.PullHose)
                _interactPrompt.Show(PullText, x, _interactor.HoldProgress);
            else if (_interactor.TargetAction == TransferHoseAction.PlaceCarriedFirst)
                _interactPrompt.Show(PlaceCarriedFirstText, x, 0f, true);
            else
                _interactPrompt.Hide();
        }

        private void UpdatePlacePrompt(bool menuOpen, float x)
        {
            bool placeFailed = _placeFailedTime >= 0f && Time.time - _placeFailedTime < PlaceFailedShowTime;

            // Next to the E prompt when both are shown.
            float placeX = _interactor.CanOpenMenu || _interactor.TargetAction != TransferHoseAction.None ? x + SideOffset : x;

            if (_interactor.CanPlace && !menuOpen)
                _placePrompt.Show(placeFailed ? PlaceFailedText : PlaceText, placeX, 1f, placeFailed);
            else
                _placePrompt.Hide();
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

            TransferStatus status = _transfer != null ? _transfer.Status : TransferStatus.NotConnected;
            string text = GetStatusText(status);
            if (text == null)
            {
                if (_status.gameObject.activeSelf)
                    _status.gameObject.SetActive(false);
                _shownStatusKey = int.MinValue;
                return;
            }

            if ((int)status == _shownStatusKey)
                return;

            _shownStatusKey = (int)status;
            _status.gameObject.SetActive(true);
            _status.text = text;
            _status.color = HudWidgets.ProblemColor;
        }

        /// <summary>Problem text; null hides the line (normal work and steps the prompts already explain).</summary>
        private static string GetStatusText(TransferStatus status)
        {
            switch (status)
            {
                case TransferStatus.OutletBusy: return "Pump output is used by the nozzle hose";
                case TransferStatus.SourceEmpty: return "No water: the source reservoir is empty";
                case TransferStatus.NoWater: return "No water source: put the pump intake into water";
                case TransferStatus.DestinationFull: return "Destination reservoir full";
                case TransferStatus.Interrupted: return "Pump running - flow interrupted";
                default: return null;
            }
        }
    }
}
