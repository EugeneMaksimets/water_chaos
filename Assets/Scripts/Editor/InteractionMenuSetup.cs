using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Player;
using WaterChaos.UI;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Sets up the unified interaction menu on the player: PlayerInteraction (gathers actions),
    /// RadialInteractionMenu (the only interaction UI) with the behaviours it blocks while open,
    /// and the menu reference of the HUDs that hide under it.
    /// Runs automatically after scripts reload when the open game scene has no PlayerInteraction yet and saves it;
    /// by hand: Water Chaos/Setup Interaction Menu. Safe to run again.
    /// </summary>
    [InitializeOnLoad]
    public static class InteractionMenuSetup
    {
        private const string UndoName = "Setup Interaction Menu";

        static InteractionMenuSetup()
        {
            EditorApplication.delayCall += TryAutoRun;
        }

        [MenuItem("Water Chaos/Setup Interaction Menu")]
        private static void Setup()
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null)
            {
                Debug.LogError("Interaction menu setup: the scene needs a PlayerInventory on the player.");
                return;
            }

            if (SetupPlayer(inventory.gameObject) == null)
                return;

            EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);
            Debug.Log("Interaction menu setup done. Save the scene.");
        }

        private static void TryAutoRun()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null || inventory.GetComponent<PlayerInteraction>() != null)
                return;

            if (SetupPlayer(inventory.gameObject) == null)
                return;

            UnityEngine.SceneManagement.Scene scene = inventory.gameObject.scene;
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Interaction menu: set up automatically and saved \"{scene.name}\".");
        }

        /// <summary>Adds or updates PlayerInteraction and RadialInteractionMenu on the player; null if the player is incomplete.</summary>
        public static RadialInteractionMenu SetupPlayer(GameObject player)
        {
            Camera camera = player.GetComponentInChildren<Camera>(true);
            PlayerInputHandler input = player.GetComponent<PlayerInputHandler>();
            if (camera == null || input == null)
            {
                Debug.LogError("Interaction menu setup: the player needs a child Camera and a PlayerInputHandler.");
                return null;
            }

            if (!player.TryGetComponent(out PlayerInteraction interaction))
                interaction = Undo.AddComponent<PlayerInteraction>(player);

            SerializedObject interactionData = new SerializedObject(interaction);
            interactionData.FindProperty("_camera").objectReferenceValue = camera.transform;
            interactionData.FindProperty("_interactMask").intValue = ~(1 << player.layer);
            interactionData.ApplyModifiedProperties();

            if (!player.TryGetComponent(out RadialInteractionMenu menu))
                menu = Undo.AddComponent<RadialInteractionMenu>(player);

            // Everything that would react to the mouse, the click, the wheel or movement keys while the menu is open.
            // Action providers (item, hose and transfer interactors) stay on, or the menu would lose its actions.
            Behaviour[] blocked =
            {
                player.GetComponentInChildren<PlayerLook>(true),
                player.GetComponentInChildren<PlayerMovement>(true),
                player.GetComponentInChildren<PlayerItemThrower>(true),
                player.GetComponentInChildren<PlayerSourceControl>(true),
                player.GetComponent<PlayerInventory>()
            };

            SerializedObject menuData = new SerializedObject(menu);
            menuData.FindProperty("_input").objectReferenceValue = input;
            menuData.FindProperty("_interaction").objectReferenceValue = interaction;
            SerializedProperty list = menuData.FindProperty("_blockWhileOpen");
            list.arraySize = 0;
            foreach (Behaviour behaviour in blocked)
            {
                if (behaviour == null)
                    continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = behaviour;
            }
            menuData.ApplyModifiedProperties();

            SetMenu(player.GetComponent<TransferHoseHud>(), menu);
            SetMenu(player.GetComponent<WaterReservoirInfoUI>(), menu);

            Undo.SetCurrentGroupName(UndoName);
            return menu;
        }

        private static void SetMenu(Component hud, RadialInteractionMenu menu)
        {
            if (hud == null)
                return;

            SerializedObject data = new SerializedObject(hud);
            data.FindProperty("_menu").objectReferenceValue = menu;
            data.ApplyModifiedProperties();
        }
    }
}
