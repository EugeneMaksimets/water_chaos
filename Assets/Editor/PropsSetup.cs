using UnityEditor;
using UnityEngine;
using WaterChaos.Props;
using WaterChaos.Rooms;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Builds the Fridge and AirConditioner prefabs from their models: collider, behaviour component
    /// and references to the moving parts. Runs once automatically if a prefab is missing,
    /// and can be re-run from the menu after the models are re-exported.
    /// </summary>
    [InitializeOnLoad]
    public static class PropsSetup
    {
        private const string FridgeModelPath = "Assets/WaterChaos/Models/FridgeModel.fbx";
        private const string AirConditionerModelPath = "Assets/WaterChaos/Models/AirConditionerModel.fbx";
        private const string FridgePrefabPath = "Assets/WaterChaos/Prefabs/Fridge.prefab";
        private const string AirConditionerPrefabPath = "Assets/WaterChaos/Prefabs/AirConditioner.prefab";

        static PropsSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(FridgePrefabPath) == null
                    || AssetDatabase.LoadAssetAtPath<GameObject>(AirConditionerPrefabPath) == null)
                    Run();
            };
        }

        [MenuItem("Tools/Water Chaos/Setup Fridge And Air Conditioner")]
        public static void Run()
        {
            BuildFridge();
            BuildAirConditioner();
            Debug.Log("[PropsSetup] Done: Fridge and AirConditioner prefabs are ready.");
        }

        private static void BuildFridge()
        {
            GameObject instance = Instantiate(FridgeModelPath, "Fridge");
            if (instance == null)
                return;

            Transform body = FindChild(instance.transform, "Body");
            AddBoxCollider(instance, body);

            // Warm light under the fridge ceiling, enabled by the Fridge component while the door is open
            Bounds bounds = body.GetComponent<Renderer>().bounds;
            var lightObject = new GameObject("InteriorLight");
            lightObject.transform.SetParent(instance.transform, false);
            lightObject.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.2f, bounds.center.z);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.2f;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.93f, 0.75f);
            light.shadows = LightShadows.None;

            var fridge = instance.AddComponent<Fridge>();
            SetReference(fridge, "_door", FindChild(instance.transform, "Door"));
            SetReference(fridge, "_interiorLight", light);
            AddCooler(instance, CoolingApplianceKind.Refrigerator);

            Save(instance, FridgePrefabPath);
        }

        private static void BuildAirConditioner()
        {
            GameObject instance = Instantiate(AirConditionerModelPath, "AirConditioner");
            if (instance == null)
                return;

            AddBoxCollider(instance, FindChild(instance.transform, "Body"));

            var airConditioner = instance.AddComponent<AirConditioner>();
            SetReference(airConditioner, "_louver", FindChild(instance.transform, "Louver"));
            SetReference(airConditioner, "_statusLight", FindChild(instance.transform, "StatusLight").gameObject);
            SetReference(airConditioner, "_airFlow", FindChild(instance.transform, "AirFlow"));
            AddCooler(instance, CoolingApplianceKind.AirConditioner);

            Save(instance, AirConditionerPrefabPath);
        }

        private static GameObject Instantiate(string modelPath, string name)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null)
            {
                Debug.LogWarning($"[PropsSetup] Model not found: {modelPath}");
                return null;
            }

            if (importer.animationType != ModelImporterAnimationType.None || importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return instance;
        }

        /// <summary>Box collider on the root that wraps the static body (moving parts stay closed in the model).</summary>
        private static void AddBoxCollider(GameObject root, Transform body)
        {
            Bounds bounds = body.GetComponent<Renderer>().bounds;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }

        /// <summary>Room cooling: 1 °C/s while the appliance is switched on.</summary>
        private static void AddCooler(GameObject root, CoolingApplianceKind kind)
        {
            var cooler = root.AddComponent<CoolingAppliance>();
            var serialized = new SerializedObject(cooler);
            serialized.FindProperty("_kind").enumValueIndex = (int)kind;
            serialized.FindProperty("_coolingPerSecond").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child;
            }

            throw new System.InvalidOperationException($"[PropsSetup] '{childName}' not found under {parent.name}.");
        }

        private static void SetReference(Component component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(GameObject instance, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
        }
    }
}
