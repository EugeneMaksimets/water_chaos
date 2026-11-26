using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Environment;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Rooms;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Ukrainian names in the open scene: renames the temperature rooms and lists interactive objects
    /// whose menu name is still English (not in <see cref="ObjectMenuNames"/> and without a <see cref="MenuName"/>).
    /// Object names themselves are translated at runtime by <see cref="ObjectMenuNames"/>. Save the scene afterwards.
    /// </summary>
    public static class UkrainianNamesSetup
    {
        private static readonly Dictionary<string, string> RoomNames = new Dictionary<string, string>
        {
            { "Room", "Кімната" },
            { "Floor One", "Перший поверх" },
            { "Toxic Works · Hall", "Токсичний завод · Цех" },
            { "Quick Mart", "Мінімаркет" },
        };

        [MenuItem("Water Chaos/Setup Ukrainian Names")]
        private static void Run()
        {
            int rooms = 0;
            foreach (RoomTemperature room in Object.FindObjectsByType<RoomTemperature>(FindObjectsInactive.Include))
            {
                SerializedObject data = new SerializedObject(room);
                SerializedProperty name = data.FindProperty("_displayName");
                if (name != null && RoomNames.TryGetValue(name.stringValue, out string ukrainian))
                {
                    name.stringValue = ukrainian;
                    data.ApplyModifiedProperties();
                    rooms++;
                }
            }

            SortedSet<string> english = new SortedSet<string>(FindInteractiveRoots()
                .Where(go => go.GetComponentInParent<IDisplayName>(true) == null)
                .Select(go => PlayerInteraction.CleanName(go.name))
                .Where(name => !ObjectMenuNames.IsKnown(name)));

            EditorSceneManager.MarkAllScenesDirty();
            string missing = english.Count > 0
                ? "\nStill English in the menu (add them to ObjectMenuNames or put a MenuName on them): " + string.Join(", ", english)
                : "\nEvery interactive object has a Ukrainian name.";
            Debug.Log($"Ukrainian names: {rooms} room(s) renamed. Save the scene (Cmd+S).{missing}");
        }

        /// <summary>The whole object (as the menu sees it) of everything the player can aim at and act on.</summary>
        private static IEnumerable<GameObject> FindInteractiveRoots()
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c is IInteractionProvider || c is PickableItem || c is WaterConnectionPoint
                    || c is TransferConnectionPoint || c is HoseEndSocket || c is Ladder)
                .Where(c => c.gameObject.scene.IsValid())
                .Select(c => PlayerInteraction.FocusOf(c.transform).gameObject)
                .Where(go => go.GetComponent<TransferHose>() == null)
                .Distinct();
        }
    }
}
