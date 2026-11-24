using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Pump;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click swap of the pump's placeholder cube for the WaterPump model in the open scene.
    /// The pump object keeps all its scripts and every reference to them; the model becomes its visual child.
    /// Undo-able; save the scene afterwards.
    /// </summary>
    public static class PumpModelSetup
    {
        private const string UndoName = "Setup Pump Model";
        private const string ModelName = "WaterPump";
        private const string IntakeName = "Intake";
        private const string LightName = "Status Light";

        [MenuItem("Water Chaos/Setup Pump Model")]
        private static void Setup()
        {
            PumpController pump = Object.FindAnyObjectByType<PumpController>(FindObjectsInactive.Include);
            if (pump == null)
            {
                Debug.LogError("Pump model setup: the scene has no PumpController.");
                return;
            }

            GameObject model = GameObject.Find(ModelName);
            if (model == null)
            {
                Debug.LogError($"Pump model setup: the scene has no active \"{ModelName}\" object.");
                return;
            }

            Transform root = pump.transform;
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, UndoName);
            Undo.RegisterFullObjectHierarchyUndo(model, UndoName);

            AttachModel(root, model.transform);
            RemovePlaceholderMesh(root.gameObject);

            Bounds local = GetLocalRendererBounds(root, model);
            FitCollider(root.gameObject, local);
            PlaceChild(root, IntakeName, new Vector3(local.center.x, local.min.y + 0.05f, local.center.z));
            PlaceChild(root, LightName, new Vector3(local.center.x, local.max.y + 0.05f, local.center.z));
            SetupView(root, pump, model.transform);
            SetupHeldParts(root.gameObject);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Pump model setup: done. Check the held scale on PickableItem, then save the scene (Cmd+S).");
        }

        /// <summary>The pump moves to where the model was placed; the model sits at the pump's origin.</summary>
        private static void AttachModel(Transform root, Transform model)
        {
            root.SetPositionAndRotation(model.position, model.rotation);
            Undo.SetTransformParent(model, root, UndoName);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
        }

        private static void RemovePlaceholderMesh(GameObject root)
        {
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
                Undo.DestroyObjectImmediate(renderer);

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter != null)
                Undo.DestroyObjectImmediate(filter);
        }

        private static void FitCollider(GameObject root, Bounds local)
        {
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(root);

            box.center = local.center;
            box.size = local.size;
        }

        private static void PlaceChild(Transform root, string childName, Vector3 localPosition)
        {
            Transform child = root.Find(childName);
            if (child == null)
            {
                Debug.LogWarning($"Pump model setup: child \"{childName}\" not found, run Setup Pump Deployment first.");
                return;
            }

            child.localPosition = localPosition;
        }

        /// <summary>
        /// Adds PumpView if it is missing and wires it: the status light shows the pump state,
        /// only the model shakes, so the physics body and the hose attachment stay still.
        /// </summary>
        private static void SetupView(Transform root, PumpController pump, Transform model)
        {
            PumpView view = root.GetComponent<PumpView>();
            if (view == null)
                view = Undo.AddComponent<PumpView>(root.gameObject);

            Transform light = root.Find(LightName);

            SerializedObject data = new SerializedObject(view);
            data.FindProperty("_pump").objectReferenceValue = pump;
            data.FindProperty("_statusLight").objectReferenceValue = light != null ? light.GetComponent<Renderer>() : null;
            data.FindProperty("_vibrationTarget").objectReferenceValue = model;
            data.ApplyModifiedProperties();
        }

        /// <summary>The pump's world-only parts are switched off while the player holds it.</summary>
        private static void SetupHeldParts(GameObject root)
        {
            PickableItem item = root.GetComponent<PickableItem>();
            if (item == null)
                return;

            Behaviour[] parts =
            {
                root.GetComponent<PumpController>(),
                root.GetComponent<PumpDebugView>(),
                root.GetComponent<PumpDeployment>(),
            };

            SerializedObject data = new SerializedObject(item);
            SerializedProperty list = data.FindProperty("_disableWhileHeld");
            list.arraySize = 0;
            foreach (Behaviour part in parts)
            {
                if (part == null)
                    continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = part;
            }

            data.ApplyModifiedProperties();
        }

        /// <summary>Bounds of the model's renderers, in the root's local space.</summary>
        private static Bounds GetLocalRendererBounds(Transform root, GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds local = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    local.Encapsulate(root.InverseTransformPoint(corner));
                }
            }

            return local;
        }
    }
}
