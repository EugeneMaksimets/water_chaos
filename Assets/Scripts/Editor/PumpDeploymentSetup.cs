using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WaterChaos.Player;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of the throwable pump in the open scene: intake point, deployment check, status light,
    /// a test lake next to the pump, and the hose ray mask that must skip water volumes.
    /// Safe to run again: existing parts are reused. Undo-able; save the scene afterwards.
    /// </summary>
    public static class PumpDeploymentSetup
    {
        private const string UndoName = "Setup Pump Deployment";
        private const string WaterLayer = "Water";
        private const string IntakeName = "Intake";
        private const string LightName = "Status Light";
        private const string LakeName = "Lake";
        private const string WaterMaterialPath = "Assets/WaterChaos/Materials/LakeWater.mat";

        private static readonly Vector3 LakeSize = new Vector3(6f, 0.8f, 6f);
        private const float LakeOffset = 6f;
        private const float LakeVolume = 20_000f;

        [MenuItem("Water Chaos/Setup Pump Deployment")]
        private static void Setup()
        {
            PumpController pump = Object.FindAnyObjectByType<PumpController>(FindObjectsInactive.Include);
            if (pump == null)
            {
                Debug.LogError("Pump setup: the scene has no PumpController.");
                return;
            }

            int waterLayer = LayerMask.NameToLayer(WaterLayer);
            if (waterLayer < 0)
            {
                Debug.LogError("Pump setup: layer \"Water\" is missing.");
                return;
            }

            SetupPump(pump, waterLayer);
            RemoveOldLake();
            WaterBody lake = CreateLake(pump.transform.position, waterLayer);

            ExcludeWaterFromHoseRay(waterLayer);

            EditorSceneManager.MarkSceneDirty(pump.gameObject.scene);
            Selection.activeGameObject = pump.gameObject;
            Debug.Log($"Pump setup: done. Lake \"{lake.name}\" is ready. Save the scene (Cmd+S).");
        }

        private static void SetupPump(PumpController pump, int waterLayer)
        {
            GameObject go = pump.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(go, UndoName);

            // The source now comes from the water the pump is thrown into, not from a fixed tank.
            SerializedObject pumpData = new SerializedObject(pump);
            pumpData.FindProperty("_reservoirSource").objectReferenceValue = null;
            pumpData.ApplyModifiedProperties();

            Bounds local = GetLocalBounds(go);
            Transform intake = FindOrCreateChild(go.transform, IntakeName);
            intake.localPosition = new Vector3(local.center.x, local.min.y + 0.05f, local.center.z);

            PumpDeployment deployment = GetOrAdd<PumpDeployment>(go);
            SerializedObject deploymentData = new SerializedObject(deployment);
            deploymentData.FindProperty("_intake").objectReferenceValue = intake;
            deploymentData.FindProperty("_waterMask").intValue = 1 << waterLayer;
            deploymentData.FindProperty("_maxTiltAngle").floatValue = 180f;
            deploymentData.FindProperty("_bedMask").intValue = ~((1 << waterLayer) | PlayerLayerMask());
            deploymentData.ApplyModifiedProperties();

            Renderer light = CreateStatusLight(go.transform, local);

            PumpView view = GetOrAdd<PumpView>(go);
            SerializedObject viewData = new SerializedObject(view);
            viewData.FindProperty("_pump").objectReferenceValue = pump;
            viewData.FindProperty("_statusLight").objectReferenceValue = light;
            viewData.ApplyModifiedProperties();

            PumpDebugView debugView = GetOrAdd<PumpDebugView>(go);
            SerializedObject debugData = new SerializedObject(debugView);
            debugData.FindProperty("_pump").objectReferenceValue = pump;
            debugData.FindProperty("_reservoir").objectReferenceValue = null;
            debugData.FindProperty("_deployment").objectReferenceValue = deployment;
            debugData.ApplyModifiedProperties();
        }

        private static Renderer CreateStatusLight(Transform pump, Bounds local)
        {
            Transform existing = pump.Find(LightName);
            if (existing != null)
                return existing.GetComponent<Renderer>();

            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = LightName;
            Object.DestroyImmediate(light.GetComponent<Collider>());
            light.transform.SetParent(pump, false);
            light.transform.localPosition = new Vector3(local.center.x, local.max.y + 0.05f, local.center.z);
            light.transform.localScale = Vector3.one * 0.15f;
            Undo.RegisterCreatedObjectUndo(light, UndoName);
            return light.GetComponent<Renderer>();
        }

        /// <summary>The lake made by an earlier run; rebuilt so it gets the current settings.</summary>
        private static void RemoveOldLake()
        {
            GameObject old = GameObject.Find(LakeName);
            if (old != null && old.GetComponentInChildren<WaterBody>() != null)
                Undo.DestroyObjectImmediate(old);
        }

        /// <summary>
        /// A square pool on the ground without walls: the ground is the bed, the trigger box is the water volume.
        /// The player can wade in to reach a pump thrown into the middle.
        /// </summary>
        private static WaterBody CreateLake(Vector3 pumpPosition, int waterLayer)
        {
            Vector3 center = pumpPosition + Vector3.right * LakeOffset;
            if (Physics.Raycast(center + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                center.y = hit.point.y;

            GameObject root = new GameObject(LakeName);
            root.transform.position = center;
            Undo.RegisterCreatedObjectUndo(root, UndoName);

            GameObject water = new GameObject("Water Volume");
            water.layer = waterLayer;
            water.transform.SetParent(root.transform, false);

            BoxCollider volume = water.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = LakeSize;
            volume.center = new Vector3(0f, LakeSize.y * 0.5f, 0f);

            WaterReservoirComponent reservoir = water.AddComponent<WaterReservoirComponent>();
            SerializedObject reservoirData = new SerializedObject(reservoir);
            reservoirData.FindProperty("_capacity").floatValue = LakeVolume;
            reservoirData.FindProperty("_initialVolume").floatValue = LakeVolume;
            reservoirData.FindProperty("_lowLevelRatio").floatValue = 0.05f;
            reservoirData.ApplyModifiedProperties();

            WaterBody body = water.AddComponent<WaterBody>();

            CreateSurface(root.transform, waterLayer);
            return body;
        }

        private static void CreateSurface(Transform lake, int waterLayer)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            surface.layer = waterLayer;
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(lake, false);
            surface.transform.localPosition = new Vector3(0f, LakeSize.y - 0.01f, 0f);
            surface.transform.localScale = new Vector3(LakeSize.x, 0.02f, LakeSize.z);
            surface.GetComponent<Renderer>().sharedMaterial = GetWaterMaterial();
        }

        private static Material GetWaterMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader);

            // URP Lit, transparent alpha blend: the submerged pump stays visible.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.15f, 0.45f, 0.8f, 0.45f));
            material.SetFloat("_Smoothness", 0.9f);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(WaterMaterialPath));
            AssetDatabase.CreateAsset(material, WaterMaterialPath);
            return material;
        }

        /// <summary>The hose ray also hits triggers; without this a lake volume would hide the pump inside it.</summary>
        private static void ExcludeWaterFromHoseRay(int waterLayer)
        {
            PlayerHoseConnector connector = Object.FindAnyObjectByType<PlayerHoseConnector>();
            if (connector == null)
                return;

            SerializedObject data = new SerializedObject(connector);
            SerializedProperty mask = data.FindProperty("_interactMask");
            mask.intValue &= ~(1 << waterLayer);
            data.ApplyModifiedProperties();
        }

        private static int PlayerLayerMask()
        {
            int player = LayerMask.NameToLayer("Player");
            return player >= 0 ? 1 << player : 0;
        }

        /// <summary>Bounds of all colliders, in the object's local space (assumes no rotation between them).</summary>
        private static Bounds GetLocalBounds(GameObject go)
        {
            Collider[] colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds world = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                world.Encapsulate(colliders[i].bounds);

            Transform t = go.transform;
            Bounds local = new Bounds(t.InverseTransformPoint(world.center), Vector3.zero);
            local.Encapsulate(t.InverseTransformPoint(world.min));
            local.Encapsulate(t.InverseTransformPoint(world.max));
            return local;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            return go.transform;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
    }
}
