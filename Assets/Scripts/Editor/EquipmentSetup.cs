using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Items;
using WaterChaos.Player;
using WaterChaos.Pump;
using WaterChaos.Tools;
using WaterChaos.UI;
using WaterChaos.Water;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of equipped items:
    /// makes the Nozzle prefab a pickable item with nozzle controls,
    /// adds an Item Holder at the place the nozzle is shown now, puts the scene nozzle on the ground,
    /// sizes every other pickable item for the hands and turns off a pump's world-only parts while held,
    /// and wires PlayerEquipment and the hose consumers on the player.
    /// Safe to run again: existing parts are reused. Save the scene afterwards.
    /// </summary>
    public static class EquipmentSetup
    {
        private const string UndoName = "Setup Equipped Items";
        private const string NozzlePrefabPath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";
        private const string ItemHolderName = "ItemHolder";
        private const string IconPath = "UI/Skin/Knob.psd";
        private const float NozzleMass = 2f;

        /// <summary>Largest size of an item in the hands, in meters (m); bigger items are scaled down.</summary>
        private const float MaxHeldSize = 0.35f;

        /// <summary>Where the nozzle was shown relative to the camera before this setup, in m and degrees.</summary>
        private static readonly Vector3 DefaultHolderPosition = new Vector3(0.627f, -0.31f, 0.767f);
        private static readonly Vector3 DefaultHolderRotation = new Vector3(0f, 90f, 0f);

        private const float DropDistance = 2f;
        private const float DropLift = 0.3f;

        [MenuItem("Water Chaos/Setup Equipped Items")]
        private static void Setup()
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null)
            {
                Debug.LogError("Equipment setup: the scene has no PlayerInventory.");
                return;
            }

            GameObject player = inventory.gameObject;
            Camera camera = player.GetComponentInChildren<Camera>(true);
            PlayerInputHandler input = player.GetComponent<PlayerInputHandler>();
            PlayerHoseConnector connector = player.GetComponent<PlayerHoseConnector>();
            if (camera == null || input == null || connector == null)
            {
                Debug.LogError("Equipment setup: the player needs a child Camera, PlayerInputHandler and PlayerHoseConnector.");
                return;
            }

            if (!SetupNozzlePrefab())
                return;

            Transform holder = SetupHolderAndNozzle(player.transform, camera.transform);
            SetupEquipment(player, input, inventory, camera.transform, holder);
            SetupSceneItems();
            WireHoseConsumers(player, connector);

            EditorSceneManager.MarkSceneDirty(player.scene);
            Selection.activeGameObject = holder.gameObject;
            Debug.Log("Equipment setup: done. Save the scene (Cmd+S).");
        }

        private static bool SetupNozzlePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NozzlePrefabPath);
            if (root == null)
            {
                Debug.LogError($"Equipment setup: no prefab at {NozzlePrefabPath}.");
                return false;
            }

            try
            {
                WaterStream stream = root.GetComponentInChildren<WaterStream>(true);
                if (stream == null)
                {
                    Debug.LogError("Equipment setup: the Nozzle prefab has no WaterStream.");
                    return false;
                }

                if (!root.TryGetComponent(out Rigidbody body))
                    body = root.AddComponent<Rigidbody>();
                body.mass = NozzleMass;

                if (root.GetComponentInChildren<Collider>(true) == null)
                    AddBoxCollider(root);

                if (!root.TryGetComponent(out PickableItem item))
                    item = root.AddComponent<PickableItem>();

                SerializedObject itemData = new SerializedObject(item);
                SerializedProperty icon = itemData.FindProperty("_icon");
                if (icon.objectReferenceValue == null)
                    icon.objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>(IconPath);
                itemData.ApplyModifiedPropertiesWithoutUndo();

                if (!root.TryGetComponent(out NozzleController nozzle))
                    nozzle = root.AddComponent<NozzleController>();

                SerializedObject nozzleData = new SerializedObject(nozzle);
                nozzleData.FindProperty("_stream").objectReferenceValue = stream;
                nozzleData.FindProperty("_hose").objectReferenceValue = root.GetComponentInChildren<HoseController>(true);
                nozzleData.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, NozzlePrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Box around the nozzle model meshes, in the root's local space.</summary>
        private static void AddBoxCollider(GameObject root)
        {
            Transform rootTransform = root.transform;
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !AssetDatabase.Contains(mesh))
                    continue;

                Bounds meshBounds = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = rootTransform.InverseTransformPoint(filter.transform.TransformPoint(corner));

                    if (hasBounds)
                        bounds.Encapsulate(local);
                    else
                        bounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
            }

            BoxCollider box = root.AddComponent<BoxCollider>();
            if (hasBounds)
            {
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }

        /// <summary>
        /// Creates the Item Holder under the camera. If the nozzle is still shown under the camera,
        /// the holder takes its exact pose and the nozzle is put on the ground in front of the player.
        /// </summary>
        private static Transform SetupHolderAndNozzle(Transform player, Transform camera)
        {
            Transform holder = camera.Find(ItemHolderName);
            if (holder == null)
            {
                GameObject holderObject = new GameObject(ItemHolderName);
                Undo.RegisterCreatedObjectUndo(holderObject, UndoName);
                holder = holderObject.transform;
                holder.SetParent(camera, false);
                holder.SetLocalPositionAndRotation(DefaultHolderPosition, Quaternion.Euler(DefaultHolderRotation));
            }

            NozzleController nozzle = Object.FindAnyObjectByType<NozzleController>(FindObjectsInactive.Include);
            if (nozzle == null || nozzle.transform.parent != camera)
                return holder;

            Transform nozzleTransform = nozzle.transform;
            Undo.RecordObject(holder, UndoName);
            holder.SetLocalPositionAndRotation(nozzleTransform.localPosition, nozzleTransform.localRotation);

            Undo.SetTransformParent(nozzleTransform, null, UndoName);
            Undo.RecordObject(nozzleTransform, UndoName);
            nozzleTransform.SetPositionAndRotation(GetDropPoint(player), Quaternion.Euler(0f, player.eulerAngles.y, 0f));
            return holder;
        }

        private static Vector3 GetDropPoint(Transform player)
        {
            Vector3 point = player.position + player.forward * DropDistance;
            Physics.SyncTransforms();
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                point = hit.point;

            return point + Vector3.up * DropLift;
        }

        private static void SetupEquipment(GameObject player, PlayerInputHandler input, PlayerInventory inventory, Transform camera, Transform holder)
        {
            if (!player.TryGetComponent(out PlayerEquipment equipment))
                equipment = Undo.AddComponent<PlayerEquipment>(player);

            SerializedObject data = new SerializedObject(equipment);
            data.FindProperty("_input").objectReferenceValue = input;
            data.FindProperty("_inventory").objectReferenceValue = inventory;
            data.FindProperty("_camera").objectReferenceValue = camera;
            data.FindProperty("_itemHolder").objectReferenceValue = holder;
            data.ApplyModifiedProperties();
        }

        /// <summary>Every pickable item except the nozzle: fit it into the hands and list its world-only parts.</summary>
        private static void SetupSceneItems()
        {
            PickableItem[] items = Object.FindObjectsByType<PickableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PickableItem item in items)
            {
                if (item.GetComponent<NozzleController>() != null)
                    continue;

                SerializedObject data = new SerializedObject(item);
                data.FindProperty("_heldScale").floatValue = GetHeldScale(item.gameObject);

                List<Behaviour> worldParts = GetWorldOnlyParts(item.gameObject);
                SerializedProperty list = data.FindProperty("_disableWhileHeld");
                list.arraySize = worldParts.Count;
                for (int i = 0; i < worldParts.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = worldParts[i];

                data.ApplyModifiedProperties();
            }
        }

        /// <summary>Scale that makes the item's largest side at most <see cref="MaxHeldSize"/>; 1 if it already fits.</summary>
        private static float GetHeldScale(GameObject item)
        {
            float size = 0f;
            foreach (MeshRenderer meshRenderer in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                Vector3 extents = meshRenderer.bounds.size;
                size = Mathf.Max(size, extents.x, extents.y, extents.z);
            }

            if (size <= MaxHeldSize)
                return 1f;

            return Mathf.Round(MaxHeldSize / size * 100f) / 100f;
        }

        /// <summary>Parts that must not work in the hands: water sources, connection points, pump deployment.</summary>
        private static List<Behaviour> GetWorldOnlyParts(GameObject item)
        {
            List<Behaviour> parts = new List<Behaviour>();
            foreach (Behaviour behaviour in item.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour is IWaterFlowSource || behaviour is WaterConnectionPoint || behaviour is PumpDeployment)
                    parts.Add(behaviour);
            }

            return parts;
        }

        private static void WireHoseConsumers(GameObject player, PlayerHoseConnector connector)
        {
            if (player.TryGetComponent(out PlayerMovement movement))
                SetReference(movement, "_hoseConnector", connector);

            HoseMessageView message = Object.FindAnyObjectByType<HoseMessageView>(FindObjectsInactive.Include);
            if (message != null)
                SetReference(message, "_hoseConnector", connector);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedProperties();
        }
    }
}
