using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Mission;
using WaterChaos.Player;
using WaterChaos.Rooms;
using WaterChaos.Soap;
using WaterChaos.Surfaces;
using WaterChaos.Teammate;
using WaterChaos.Water;
using WaterChaos.Tools;
using WaterChaos.UI;
using Object = UnityEngine.Object;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// The cat rescue and the roof fire, built into the existing mission (Mini Village):
    /// a cartoon cat (meows) and a gas mask with their icons, the teammates' house filling with toxic air after the
    /// gasoline is neutralized, steps "take the gas mask, put it on, carry the cat out", the soap as its own step,
    /// then fires on the roof and "lift a teammate in a bubble, put the roof out" before the finale.
    /// Assets are created only if missing; the scene part runs once (it looks for the "Rescue · Cat" step).
    /// </summary>
    public static class RescueMissionSetup
    {
        private const string ModelFolder = "Assets/WaterChaos/Prefabs/Rescue";
        private const string MaterialFolder = "Assets/WaterChaos/Materials/Rescue";
        private const string IconFolder = "Assets/WaterChaos/Materials/Icon";
        private const string AudioFolder = "Assets/WaterChaos/Audio/Generated";
        private const string CatPath = ModelFolder + "/Cat.prefab";
        private const string MaskPath = ModelFolder + "/GasMask.prefab";
        private const string CatIconPath = IconFolder + "/icon_cat.png";
        private const string MaskIconPath = IconFolder + "/icon_gas_mask.png";
        private const string SoundSettingsPath = "Assets/Resources/SoundSettings.asset";
        private const string FirePath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string FirstHouseRoomName = "Beige House · Room";
        private const string RescueStepMark = "Rescue · Cat";
        private const int SampleRate = 44100;
        private const int RoofFireCount = 4;

        private static readonly Regex StepNumber = new Regex(@"^\d{2} · ");

        // ───────────── Entry points ─────────────

        [MenuItem("Water Chaos/Setup Cat Rescue and Roof Fire (Mini Village)")]
        private static void RunFromMenu()
        {
            var report = new List<string>();
            MissionRunner runner = Object.FindAnyObjectByType<MissionRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                Debug.LogError("Rescue setup: no MissionRunner in the open scene; run Setup Mission first.");
                return;
            }

            Apply(runner.transform, report);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            Debug.Log("Cat rescue and roof fire set up. Save the scene (Cmd+S).\n- " + string.Join("\n- ", report));
        }

        /// <summary>Batch mode: opens the Mini Village, builds everything into its mission and saves.</summary>
        public static void RunAndSave()
        {
            Scene scene = EditorSceneManager.OpenScene(MiniVillage.MiniVillageBuilder.ScenePath, OpenSceneMode.Single);
            var report = new List<string>();
            MissionRunner runner = Object.FindAnyObjectByType<MissionRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                Debug.LogError("Rescue setup: no MissionRunner in the Mini Village.");
                return;
            }

            Apply(runner.transform, report);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Cat rescue and roof fire set up.\n- " + string.Join("\n- ", report));
        }

        /// <summary>Builds the assets and inserts the rescue and roof steps into the mission under <paramref name="root"/>.</summary>
        public static void Apply(Transform root, List<string> report)
        {
            EnsureAssets(report);
            if (FindStep(root, RescueStepMark) == null)
                BuildScene(root, report);
            else
                report.Add("Rescue steps already in the mission: only updating them.");

            UpdateRescue(root, report);
        }

        /// <summary>
        /// Keeps the built rescue up to date: the cat appears only when its part of the mission starts,
        /// and the gas mask hint says it can be taken off again.
        /// </summary>
        private static void UpdateRescue(Transform root, List<string> report)
        {
            MissionStep story = FindStep(root, "Story · Cat");
            CatItem cat = Object.FindObjectsByType<CatItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene == root.gameObject.scene);
            if (story != null && cat != null && FindStep(root, "Event · Cat Appears") == null)
            {
                SetActiveStep appear = MissionSetup.Add<SetActiveStep>(root, "Event · Cat Appears", null);
                MissionSetup.SetArray(appear, "_objects", cat.gameObject);
                MissionSetup.Set(appear, "_active", true);
                appear.transform.SetSiblingIndex(story.transform.GetSiblingIndex());
                report.Add("The cat appears only when its part of the mission starts.");
            }

            if (cat != null && cat.gameObject.activeSelf)
                cat.gameObject.SetActive(false);

            MissionStep rescue = FindStep(root, RescueStepMark);
            if (rescue is ObjectiveStep rescueStep)
            {
                MissionSetup.Set(rescueStep, "_title", "Винеси котика на вулицю й постав його");
                MissionSetup.Hint(rescueStep, "Винеси котика з будинку й постав на землю (F — покласти).", null, null);
            }

            // The interaction prompt already shows E: mission hints keep only their text, never a second E.
            foreach (ObjectiveStep step in root.GetComponentsInChildren<ObjectiveStep>(true))
            {
                var data = new SerializedObject(step);
                if (data.FindProperty("_showHintKey").boolValue && data.FindProperty("_hintKey").enumValueIndex == (int)PlayerControl.Interact)
                    MissionSetup.Set(step, "_showHintKey", false);
            }

            MissionStep wear = FindStep(root, "Wear · Gas Mask");
            if (wear is ObjectiveStep wearStep)
                MissionSetup.Hint(wearStep, "Вибери протигаз в інвентарі та натисни ЛКМ, щоб надіти або зняти його.", PlayerControl.Primary, null);

            AddSupplyChecks(root, report);
            AddTeammateAim(root.gameObject.scene, report);
            AddCoolingStage(root, report);
            AddBubbleSteering(root, report);

            Renumber(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        /// <summary>
        /// Before the story talks about gasoline in the hoses or the toxic house, the teammates must really be spraying it:
        /// a supply check waits for it and shows what to reconnect if they are not.
        /// </summary>
        private static void AddSupplyChecks(Transform root, List<string> report)
        {
            Scene scene = root.gameObject.scene;
            TeammateNozzle[] teammates = Object.FindObjectsByType<TeammateNozzle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene == scene).ToArray();
            MissionStep startPump = FindStep(root, "Pump · Start Tank Pump");
            MissionStep carryToTee = FindStep(root, "Hose · Tank End to Destination");
            Object pump = startPump != null ? FirstElement(startPump, "_pumps") : null;
            Object pumpMarker = startPump != null ? FirstElement(startPump, "_markers") : null;
            Object teeMarker = carryToTee != null ? FirstElement(carryToTee, "_markers") : null;

            AddSupplyCheck(root, "See · Flamethrowers", "Check · Gasoline Reaches Teammates", LiquidType.Gasoline,
                teammates, pump, pumpMarker, teeMarker, report);
            AddSupplyCheck(root, "Event · House Turns Toxic", "Check · Toxic Reaches Teammates", LiquidType.Toxic,
                teammates, pump, pumpMarker, teeMarker, report);
        }

        private static void AddSupplyCheck(Transform root, string before, string name, LiquidType liquid,
            TeammateNozzle[] teammates, Object pump, Object pumpMarker, Object teeMarker, List<string> report)
        {
            MissionStep next = FindStep(root, before);
            if (next == null || FindStep(root, name) != null)
                return;

            TeammateSupplyStep check = MissionSetup.Add<TeammateSupplyStep>(root, name, null);
            MissionSetup.SetArray(check, "_teammates", teammates.Cast<Object>().ToArray());
            MissionSetup.SetEnum(check, "_liquid", (int)liquid);
            MissionSetup.Set(check, "_pump", pump);
            MissionSetup.Set(check, "_pumpMarker", pumpMarker);
            MissionSetup.Set(check, "_teeMarker", teeMarker);
            check.transform.SetSiblingIndex(next.transform.GetSiblingIndex());
            report.Add($"{name}: before '{next.name}' (pump: {(pump != null ? pump.name : "NOT FOUND")}).");
        }

        private static Object FirstElement(Object target, string field)
        {
            SerializedProperty list = new SerializedObject(target).FindProperty(field);
            return list != null && list.isArray && list.arraySize > 0 ? list.GetArrayElementAtIndex(0).objectReferenceValue : null;
        }

        /// <summary>
        /// At the teammates' house: an objective to switch on its fridge and air conditioner (with markers), coolers strong
        /// enough to bring the room below zero quickly (to show snow and ice), at most 20 fires there, and a hint about
        /// snow and ice once the room reaches 0 °C.
        /// </summary>
        private static void AddCoolingStage(Transform root, List<string> report)
        {
            MissionZone crewZone = FindZone(root, "Zone · Teammates (reached)");
            MissionStep after = FindStep(root, "Story · Out of Water");
            if (crewZone == null || after == null)
            {
                report.Add("Cooling stage: teammates zone or \"Story · Out of Water\" not found.");
                return;
            }

            List<RoomTemperature> rooms = TeammateHouseRooms(crewZone.transform.position);
            if (rooms.Count == 0)
                return;

            RoomTemperature room = rooms.OrderBy(r => r.GetComponent<BoxCollider>().bounds.min.y).First();
            Bounds box = room.GetComponent<BoxCollider>().bounds;
            CoolingAppliance[] coolers = Object.FindObjectsByType<CoolingAppliance>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene == root.gameObject.scene && box.Contains(c.transform.position)).ToArray();

            // Fast enough to reach 0 °C within seconds, but no colder than -15 °C.
            foreach (CoolingAppliance cooler in coolers)
                MissionSetup.Set(cooler, "_coolingPerSecond", 4f);
            MissionSetup.Set(room, "_minTemperature", -15f);
            MissionSetup.Set(room, "_maxFires", 20f);

            if (FindStep(root, "Cooling · Teammates House") == null && coolers.Length > 0)
            {
                MissionMarker[] markers = coolers.Select(c => MissionSetup.Marker(c, 1.2f)).ToArray();
                CoolingObjective cooling = MissionSetup.Add<CoolingObjective>(root, "Cooling · Teammates House",
                    "Увімкни холодильник і кондиціонер у будинку напарників", markers);
                MissionSetup.SetArray(cooling, "_appliances", coolers.Cast<Object>().ToArray());
                MissionSetup.Hint(cooling, "Холодильник і кондиціонер швидко знизять температуру в будинку — вогонь перестане поширюватися.", null, null);
                cooling.transform.SetSiblingIndex(after.transform.GetSiblingIndex() + 1);
                report.Add($"Cooling objective after '{after.name}': {coolers.Length} cooler(s) in {room.name}, 4 °C/s each, down to -15 °C, at most 20 fires.");
            }

            if (Object.FindObjectsByType<RoomFreezeHint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                var hintObject = new GameObject("Freeze Hint · Teammates House");
                SceneManager.MoveGameObjectToScene(hintObject, root.gameObject.scene);
                RoomFreezeHint hint = hintObject.AddComponent<RoomFreezeHint>();
                MissionSetup.Set(hint, "_runner", root.GetComponent<MissionRunner>());
                MissionSetup.Set(hint, "_room", room);
                report.Add("Snow and ice hint at 0 °C in the teammates' house.");
            }
        }

        /// <summary>The player steers their soap bubble with WASD (e.g. onto a roof).</summary>
        private static void AddBubbleSteering(Transform root, List<string> report)
        {
            MissionRunner runner = root.GetComponent<MissionRunner>();
            Transform player = runner != null ? runner.Player : null;
            if (player == null || player.GetComponent<PlayerBubbleSteering>() != null)
                return;

            SoapBubbleController bubble = player.GetComponent<SoapBubbleController>();
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (bubble == null || movement == null)
            {
                report.Add("Bubble steering: the player has no SoapBubbleController or PlayerMovement.");
                return;
            }

            var movementData = new SerializedObject(movement);
            PlayerBubbleSteering steering = player.gameObject.AddComponent<PlayerBubbleSteering>();
            MissionSetup.Set(steering, "_bubble", bubble);
            MissionSetup.Set(steering, "_camera", movementData.FindProperty("_cameraTransform").objectReferenceValue);
            MissionSetup.Set(steering, "_hoseConnector", movementData.FindProperty("_hoseConnector").objectReferenceValue);
            report.Add("Player: WASD steers the soap bubble.");
        }

        /// <summary>Every teammate aims at the fires (the roof ones when lifted in a bubble).</summary>
        private static void AddTeammateAim(Scene scene, List<string> report)
        {
            int added = 0;
            foreach (TeammateNozzle nozzle in Object.FindObjectsByType<TeammateNozzle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (nozzle.gameObject.scene != scene || nozzle.GetComponent<TeammateAim>() != null)
                    continue;

                CharacterController body = nozzle.GetComponentInParent<CharacterController>(true);
                TeammateAim aim = nozzle.gameObject.AddComponent<TeammateAim>();
                MissionSetup.Set(aim, "_body", body != null ? body.transform : nozzle.transform.root);
                added++;
            }

            if (added > 0)
                report.Add($"Teammates aim at the fires: {added}.");
        }

        // ───────────── Assets ─────────────

        private static void EnsureAssets(List<string> report)
        {
            foreach (string folder in new[] { ModelFolder, MaterialFolder, IconFolder, AudioFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            EnsureMeows(report);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(CatPath) == null)
            {
                Sprite icon = LoadOrCreateIcon(CatIconPath, CatIconPixel);
                SavePrefab(BuildCat(icon), CatPath, report);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(MaskPath) == null)
            {
                Sprite icon = LoadOrCreateIcon(MaskIconPath, MaskIconPixel);
                SavePrefab(BuildGasMask(icon), MaskPath, report);
            }
        }

        private static void SavePrefab(GameObject root, string path, List<string> report)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.Add($"Prefab: {path}");
        }

        private static Material Lit(string name, Color color, float smoothness, Color emission = default)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A primitive part without a collider (the item has one box collider on its root).</summary>
        private static Transform Part(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 position, Vector3 scale, Vector3 rotation = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(rotation);
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        /// <summary>Cartoon ginger cat sitting on the floor (pivot at its paws, facing +Z), about 0.5 m long.</summary>
        private static GameObject BuildCat(Sprite icon)
        {
            Material fur = Lit("M_CatFur", new Color(1f, 0.58f, 0.18f), 0.2f, new Color(0.12f, 0.05f, 0f));
            Material stripe = Lit("M_CatStripe", new Color(0.85f, 0.36f, 0.06f), 0.2f);
            Material cream = Lit("M_CatCream", new Color(1f, 0.93f, 0.8f), 0.2f, new Color(0.08f, 0.07f, 0.05f));
            Material pink = Lit("M_CatPink", new Color(1f, 0.55f, 0.65f), 0.4f);
            Material eye = Lit("M_CatEye", new Color(0.08f, 0.08f, 0.1f), 0.9f);
            Material glint = Lit("M_CatGlint", Color.white, 0.9f, new Color(0.6f, 0.6f, 0.6f));

            var root = new GameObject("Cat");
            Transform t = root.transform;
            Part(PrimitiveType.Sphere, "Body", t, fur, new Vector3(0f, 0.17f, -0.02f), new Vector3(0.28f, 0.24f, 0.42f));
            Part(PrimitiveType.Sphere, "Belly", t, cream, new Vector3(0f, 0.14f, 0.04f), new Vector3(0.2f, 0.17f, 0.3f));
            Part(PrimitiveType.Sphere, "Stripe A", t, stripe, new Vector3(0f, 0.28f, -0.06f), new Vector3(0.22f, 0.04f, 0.07f));
            Part(PrimitiveType.Sphere, "Stripe B", t, stripe, new Vector3(0f, 0.27f, -0.16f), new Vector3(0.2f, 0.04f, 0.06f));

            Transform head = new GameObject("Head").transform;
            head.SetParent(t, false);
            head.localPosition = new Vector3(0f, 0.33f, 0.19f);
            Part(PrimitiveType.Sphere, "Skull", head, fur, Vector3.zero, new Vector3(0.26f, 0.23f, 0.23f));
            Part(PrimitiveType.Sphere, "Muzzle", head, cream, new Vector3(0f, -0.04f, 0.1f), new Vector3(0.13f, 0.08f, 0.07f));
            Part(PrimitiveType.Sphere, "Nose", head, pink, new Vector3(0f, -0.01f, 0.135f), new Vector3(0.035f, 0.025f, 0.025f));
            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, "Eye", head, eye, new Vector3(0.058f * side, 0.025f, 0.1f), new Vector3(0.055f, 0.065f, 0.03f));
                Part(PrimitiveType.Sphere, "Glint", head, glint, new Vector3(0.05f * side, 0.04f, 0.113f), new Vector3(0.017f, 0.017f, 0.01f));
                Part(PrimitiveType.Cube, "Ear", head, fur, new Vector3(0.07f * side, 0.11f, -0.01f), new Vector3(0.08f, 0.08f, 0.03f), new Vector3(0f, 0f, 45f));
                Part(PrimitiveType.Cube, "Ear Inner", head, pink, new Vector3(0.07f * side, 0.105f, 0.006f), new Vector3(0.045f, 0.045f, 0.01f), new Vector3(0f, 0f, 45f));
                Part(PrimitiveType.Cylinder, "Front Paw", t, cream, new Vector3(0.07f * side, 0.05f, 0.12f), new Vector3(0.06f, 0.05f, 0.06f));
                Part(PrimitiveType.Cylinder, "Back Paw", t, fur, new Vector3(0.08f * side, 0.05f, -0.14f), new Vector3(0.07f, 0.05f, 0.07f));
            }

            // The tail hangs from a pivot the cat swings (CatItem).
            Transform tailPivot = new GameObject("Tail Pivot").transform;
            tailPivot.SetParent(t, false);
            tailPivot.localPosition = new Vector3(0f, 0.16f, -0.21f);
            Part(PrimitiveType.Capsule, "Tail", tailPivot, fur, new Vector3(0f, 0.1f, -0.06f), new Vector3(0.06f, 0.13f, 0.06f), new Vector3(-30f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Tail Tip", tailPivot, stripe, new Vector3(0f, 0.21f, -0.12f), new Vector3(0.065f, 0.065f, 0.065f));

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.23f, 0.02f);
            box.size = new Vector3(0.3f, 0.46f, 0.56f);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var item = root.AddComponent<PickableItem>();
            var itemSo = new SerializedObject(item);
            itemSo.FindProperty("_icon").objectReferenceValue = icon;
            itemSo.FindProperty("_heldPosition").vector3Value = new Vector3(0f, -0.25f, 0.15f);
            itemSo.FindProperty("_heldRotation").vector3Value = new Vector3(0f, 160f, 0f);
            itemSo.FindProperty("_keepUpright").boolValue = true;
            itemSo.ApplyModifiedPropertiesWithoutUndo();

            var cat = root.AddComponent<CatItem>();
            var catSo = new SerializedObject(cat);
            catSo.FindProperty("_tail").objectReferenceValue = tailPivot;
            catSo.ApplyModifiedPropertiesWithoutUndo();

            AddDisplayName(root, "котика");
            return root;
        }

        /// <summary>Cartoon gas mask (pivot at the bottom, facing +Z), about 0.3 m tall.</summary>
        private static GameObject BuildGasMask(Sprite icon)
        {
            Material rubber = Lit("M_MaskRubber", new Color(0.22f, 0.26f, 0.22f), 0.35f);
            Material rim = Lit("M_MaskRim", new Color(0.12f, 0.13f, 0.12f), 0.5f);
            Material glass = Lit("M_MaskLens", new Color(0.55f, 0.85f, 1f), 0.95f, new Color(0.08f, 0.16f, 0.22f));
            Material filter = Lit("M_MaskFilter", new Color(0.45f, 0.5f, 0.2f), 0.4f);
            Material metal = Lit("M_MaskMetal", new Color(0.7f, 0.72f, 0.75f), 0.8f);

            var root = new GameObject("Gas Mask");
            Transform t = root.transform;
            Part(PrimitiveType.Sphere, "Face", t, rubber, new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.27f, 0.16f));
            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cylinder, "Lens Rim", t, rim, new Vector3(0.052f * side, 0.18f, 0.06f), new Vector3(0.085f, 0.012f, 0.085f), new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, "Lens", t, glass, new Vector3(0.052f * side, 0.18f, 0.068f), new Vector3(0.068f, 0.008f, 0.068f), new Vector3(90f, 0f, 0f));
            }

            Part(PrimitiveType.Cylinder, "Filter", t, filter, new Vector3(0f, 0.07f, 0.1f), new Vector3(0.09f, 0.05f, 0.09f), new Vector3(70f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Filter Cap", t, metal, new Vector3(0f, 0.055f, 0.145f), new Vector3(0.07f, 0.01f, 0.07f), new Vector3(70f, 0f, 0f));
            Part(PrimitiveType.Cube, "Strap", t, rim, new Vector3(0f, 0.17f, -0.07f), new Vector3(0.23f, 0.03f, 0.02f));

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.15f, 0.03f);
            box.size = new Vector3(0.24f, 0.29f, 0.24f);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var item = root.AddComponent<PickableItem>();
            var itemSo = new SerializedObject(item);
            itemSo.FindProperty("_icon").objectReferenceValue = icon;
            itemSo.FindProperty("_heldPosition").vector3Value = new Vector3(0f, -0.1f, 0.1f);
            itemSo.FindProperty("_heldRotation").vector3Value = new Vector3(0f, 180f, 0f);
            itemSo.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<GasMaskItem>();

            AddDisplayName(root, "протигаз");
            return root;
        }

        /// <summary>The name the interaction menu shows for the item.</summary>
        private static void AddDisplayName(GameObject root, string displayName)
        {
            var name = root.AddComponent<MenuName>();
            var so = new SerializedObject(name);
            so.FindProperty("_name").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────── Icons (drawn in code, like the soap) ─────────────

        private static Sprite LoadOrCreateIcon(string path, Func<Vector2, Color> pixel)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.one);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static readonly Color Outline = new Color(0.18f, 0.1f, 0.05f, 1f);

        /// <summary>Ginger cat face: round head, two ears, big eyes with glints, pink nose, whiskers.</summary>
        private static Color CatIconPixel(Vector2 p)
        {
            var fur = new Color(1f, 0.6f, 0.2f);
            var furShade = new Color(0.9f, 0.42f, 0.1f);
            var cream = new Color(1f, 0.93f, 0.8f);
            var pink = new Color(1f, 0.55f, 0.65f);

            Vector2 head = p - new Vector2(0f, -0.1f);
            float headDist = Ellipse(head, new Vector2(0.72f, 0.62f));
            float ear = Mathf.Min(Triangle(p, new Vector2(-0.62f, 0.12f), new Vector2(-0.52f, 0.82f), new Vector2(-0.12f, 0.42f)),
                                  Triangle(p, new Vector2(0.62f, 0.12f), new Vector2(0.52f, 0.82f), new Vector2(0.12f, 0.42f)));
            float innerEar = Mathf.Min(Triangle(p, new Vector2(-0.5f, 0.28f), new Vector2(-0.48f, 0.66f), new Vector2(-0.24f, 0.44f)),
                                       Triangle(p, new Vector2(0.5f, 0.28f), new Vector2(0.48f, 0.66f), new Vector2(0.24f, 0.44f)));
            float shape = Mathf.Min(headDist, ear);

            Color color = Color.clear;
            if (shape < 0.06f)
                color = Outline;
            if (shape < 0f)
                color = Color.Lerp(fur, furShade, Mathf.Clamp01(-head.y * 0.8f + 0.1f));
            if (innerEar < 0f)
                color = pink;
            if (Ellipse(head - new Vector2(0f, -0.25f), new Vector2(0.34f, 0.22f)) < 0f)
                color = cream;

            // Forehead stripes.
            for (int i = -1; i <= 1; i++)
            {
                if (Mathf.Abs(p.x - i * 0.14f) < 0.035f && p.y > 0.22f && p.y < 0.4f - Mathf.Abs(i) * 0.06f && headDist < 0f)
                    color = furShade;
            }

            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 eye = p - new Vector2(0.26f * side, 0.02f);
                if (Ellipse(eye, new Vector2(0.13f, 0.16f)) < 0f)
                    color = new Color(0.1f, 0.1f, 0.12f);
                if (Ellipse(eye - new Vector2(-0.04f, 0.06f), new Vector2(0.045f, 0.05f)) < 0f)
                    color = Color.white;

                // Whiskers: three short lines on each side.
                for (int w = -1; w <= 1; w++)
                {
                    Vector2 from = new Vector2(0.28f * side, -0.28f + w * 0.07f);
                    Vector2 to = new Vector2(0.85f * side, -0.22f + w * 0.12f);
                    if (DistanceToSegment(p, from, to) < 0.018f)
                        color = Outline;
                }
            }

            if (Triangle(p, new Vector2(-0.08f, -0.14f), new Vector2(0.08f, -0.14f), new Vector2(0f, -0.24f)) < 0f)
                color = pink;
            return color;
        }

        /// <summary>Gas mask: dark rubber face, two round blue lenses with rims, olive filter at the bottom.</summary>
        private static Color MaskIconPixel(Vector2 p)
        {
            var rubber = new Color(0.25f, 0.3f, 0.25f);
            var rubberLight = new Color(0.36f, 0.42f, 0.35f);
            var lens = new Color(0.55f, 0.85f, 1f);
            var rim = new Color(0.12f, 0.13f, 0.12f);
            var filter = new Color(0.55f, 0.6f, 0.25f);

            float face = Ellipse(p - new Vector2(0f, 0.08f), new Vector2(0.62f, 0.78f));
            float can = Ellipse(p - new Vector2(0f, -0.62f), new Vector2(0.3f, 0.24f));
            float shape = Mathf.Min(face, can);

            Color color = Color.clear;
            if (shape < 0.06f)
                color = Outline;
            if (face < 0f)
                color = Color.Lerp(rubberLight, rubber, Mathf.Clamp01((p.x + 0.6f) * 0.8f));
            if (can < 0f)
            {
                color = filter;
                if (Mathf.Abs(Mathf.Sin((p.x) * 30f)) > 0.85f)
                    color = Color.Lerp(filter, Outline, 0.5f);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 eye = p - new Vector2(0.27f * side, 0.2f);
                float rimDist = eye.magnitude - 0.25f;
                if (rimDist < 0f)
                    color = rim;
                if (eye.magnitude < 0.19f)
                {
                    color = lens;
                    if ((eye - new Vector2(-0.06f, 0.07f)).magnitude < 0.05f)
                        color = Color.white;
                }
            }

            return color;
        }

        private static float Ellipse(Vector2 p, Vector2 radius)
        {
            return new Vector2(p.x / radius.x, p.y / radius.y).magnitude - 1f;
        }

        /// <summary>Negative inside the triangle (a, b, c), roughly the distance to its edge outside.</summary>
        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = Mathf.Min(DistanceToSegment(p, a, b), Mathf.Min(DistanceToSegment(p, b, c), DistanceToSegment(p, c, a)));
            float s1 = Cross(b - a, p - a);
            float s2 = Cross(c - b, p - b);
            float s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
            return inside ? -d : d;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * t);
        }

        // ───────────── Meow ─────────────

        private static void EnsureMeows(List<string> report)
        {
            bool added = false;
            added |= LoadOrCreateWav("Cat_Meow_1", () => Meow(0.62f, 520f, 820f, 430f, 1));
            added |= LoadOrCreateWav("Cat_Meow_2", () => Meow(0.48f, 600f, 900f, 520f, 2));
            AddSoundEntry(report);
            if (added)
                report.Add("Meow sounds: Cat_Meow_1, Cat_Meow_2 in " + AudioFolder);
        }

        private static bool LoadOrCreateWav(string name, Func<float[]> build)
        {
            string path = $"{AudioFolder}/{name}.wav";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
                return false;

            WriteWav(path, build());
            AssetDatabase.ImportAsset(path);
            return true;
        }

        /// <summary>
        /// A cartoon "m-e-o-w": the pitch rises from <paramref name="start"/> to <paramref name="peak"/> Hz and falls to
        /// <paramref name="end"/>; the sound opens from a closed "m" (few harmonics) to a bright "e-a" and closes to "ow".
        /// </summary>
        private static float[] Meow(float seconds, float start, float peak, float end, int seed)
        {
            int count = (int)(SampleRate * seconds);
            var samples = new float[count];
            var random = new System.Random(seed);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float pitch = t < 0.35f ? Mathf.Lerp(start, peak, Mathf.SmoothStep(0f, 1f, t / 0.35f))
                                        : Mathf.Lerp(peak, end, Mathf.SmoothStep(0f, 1f, (t - 0.35f) / 0.65f));
                pitch *= 1f + 0.015f * Mathf.Sin(2f * Mathf.PI * 6f * t * seconds);
                phase += 2f * Mathf.PI * pitch / SampleRate;

                // Mouth opening: dark at the start and the end, bright in the middle.
                float open = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.05f) / 0.85f));
                float sample = 0f;
                for (int h = 1; h <= 6; h++)
                {
                    float weight = h == 1 ? 1f : Mathf.Lerp(0.05f, 0.75f / h, open);
                    sample += Mathf.Sin(phase * h) * weight;
                }

                float breath = (float)(random.NextDouble() * 2.0 - 1.0) * 0.03f * open;
                float envelope = Mathf.Clamp01(t / 0.06f) * Mathf.Clamp01((1f - t) / 0.2f);
                samples[i] = (sample * 0.28f + breath) * envelope;
            }

            return samples;
        }

        private static void WriteWav(string path, float[] samples)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int dataSize = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                foreach (float sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        /// <summary>Adds the meow to the sound settings (the game plays sounds only from there).</summary>
        private static void AddSoundEntry(List<string> report)
        {
            var settings = AssetDatabase.LoadAssetAtPath<Sounds.SoundSettings>(SoundSettingsPath);
            if (settings == null)
            {
                report.Add("NO SoundSettings: run Water Chaos sound setup, the cat stays silent.");
                return;
            }

            var so = new SerializedObject(settings);
            SerializedProperty sounds = so.FindProperty("_sounds");
            for (int i = 0; i < sounds.arraySize; i++)
            {
                if (sounds.GetArrayElementAtIndex(i).FindPropertyRelative("Id").enumValueIndex == (int)Sounds.SoundId.CatMeow)
                    return;
            }

            sounds.arraySize++;
            SerializedProperty entry = sounds.GetArrayElementAtIndex(sounds.arraySize - 1);
            entry.FindPropertyRelative("Id").enumValueIndex = (int)Sounds.SoundId.CatMeow;
            entry.FindPropertyRelative("Volume").floatValue = 0.85f;
            entry.FindPropertyRelative("Spatial").boolValue = true;
            entry.FindPropertyRelative("MinDistance").floatValue = 2f;
            entry.FindPropertyRelative("MaxDistance").floatValue = 22f;
            entry.FindPropertyRelative("Pitch").vector2Value = new Vector2(0.92f, 1.12f);
            SerializedProperty clips = entry.FindPropertyRelative("Clips");
            clips.arraySize = 2;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Cat_Meow_1.wav");
            clips.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Cat_Meow_2.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            report.Add("Sound settings: CatMeow added.");
        }

        // ───────────── Scene and mission ─────────────

        private static void BuildScene(Transform root, List<string> report)
        {
            Scene scene = root.gameObject.scene;
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            MissionRunner runner = root.GetComponent<MissionRunner>();
            Transform player = runner != null ? runner.Player : null;
            MissionStep neutralize = FindStep(root, "Neutralize · Gasoline");
            MissionStep soapyTee = FindStep(root, "Connect · Nozzle to Tee");
            MissionZone crewZone = FindZone(root, "Zone · Teammates (reached)");
            if (inventory == null || player == null || neutralize == null || soapyTee == null || crewZone == null)
            {
                report.Add("ERROR: inventory, player, the \"Neutralize · Gasoline\" / \"Connect · Nozzle to Tee\" steps or the teammates zone not found.");
                return;
            }

            Vector3 crew = crewZone.transform.position;
            List<RoomTemperature> rooms = TeammateHouseRooms(crew);
            if (rooms.Count == 0)
            {
                report.Add("ERROR: no temperature room found for the teammates' house.");
                return;
            }

            Bounds house = rooms[0].GetComponent<BoxCollider>().bounds;
            foreach (RoomTemperature room in rooms)
                house.Encapsulate(room.GetComponent<BoxCollider>().bounds);
            report.Add($"Teammates' house: {string.Join(", ", rooms.Select(r => r.name))} ({house.size.x:0.#} x {house.size.z:0.#} m).");

            Transform zonesParent = root.Find("Zones") ?? root;
            MissionZone houseZone = MissionSetup.CreateZone("Zone · Teammates House (toxic)", zonesParent, house);

            // Toxic air, with the exit between the house and the crew.
            var airObject = new GameObject("Toxic Air · Teammates House");
            SceneManager.MoveGameObjectToScene(airObject, scene);
            Vector3 exit = Outside(house, crew, 2f);
            Transform exitPoint = new GameObject("Exit").transform;
            exitPoint.SetParent(airObject.transform, false);
            exitPoint.position = exit;
            ToxicAirZone air = airObject.AddComponent<ToxicAirZone>();
            MissionSetup.SetArray(air, "_areas", rooms.Select(r => (Object)r.GetComponent<BoxCollider>()).ToArray());
            MissionSetup.Set(air, "_player", player);
            MissionSetup.Set(air, "_exit", exitPoint);

            // The player wears and sees the gas mask.
            if (player.GetComponent<ToxicExposure>() == null)
                player.gameObject.AddComponent<ToxicExposure>();
            PlayerGasMask wearer = player.GetComponent<PlayerGasMask>() ?? player.gameObject.AddComponent<PlayerGasMask>();
            if (player.GetComponent<PlayerGasMaskOverlay>() == null)
                player.gameObject.AddComponent<PlayerGasMaskOverlay>();

            // The cat on the lowest floor, on the far side from the exit; the gas mask by the crew.
            RoomTemperature ground = rooms.OrderBy(r => r.GetComponent<BoxCollider>().bounds.min.y).First();
            Bounds groundBox = ground.GetComponent<BoxCollider>().bounds;
            Vector3 away = groundBox.center - exit;
            away.y = 0f;
            Vector3 catSpot = groundBox.center + away.normalized * Mathf.Min(groundBox.extents.x, groundBox.extents.z) * 0.4f;
            PickableItem cat = SpawnItem(CatPath, "Cat", scene, OnFloor(catSpot, groundBox.max.y - 0.1f, groundBox.min.y - 0.5f));
            Vector3 maskSpot = Vector3.MoveTowards(crew, exit, 2f);
            PickableItem mask = SpawnItem(MaskPath, "Gas Mask", scene, OnFloor(maskSpot + Vector3.up * 3f, maskSpot.y + 6f, maskSpot.y - 10f));

            MissionMarker catMarker = MissionSetup.Marker(cat, 0.9f);
            MissionMarker maskMarker = MissionSetup.Marker(mask, 0.8f);

            // Roof fires (hidden until the soap step is done) and their zone.
            GameObject roofFires = BuildRoofFires(house, scene, out Bounds roof, report);
            MissionZone roofZone = MissionSetup.CreateZone("Zone · Roof Fires", zonesParent, roof);

            // ── Steps after the toxin: the house turns toxic, the cat is rescued, then the soap ──
            SerializedObject oldNeutralize = new SerializedObject(neutralize);
            Object tank = oldNeutralize.FindProperty("_tank").objectReferenceValue;
            Object soap = oldNeutralize.FindProperty("_soap").objectReferenceValue;
            Object soapMarker = oldNeutralize.FindProperty("_soapMarker").objectReferenceValue;
            MissionSetup.Set(neutralize, "_needSoap", false);
            MissionSetup.Set(neutralize, "_soapMarker", null);
            MissionSetup.Set(neutralize, "_title", "Нейтралізуй бензин токсичною водою");
            MissionSetup.Set(neutralize, "_hint", "Закачай токсичну воду в підземний резервуар — вона нейтралізує бензин.");

            var inserted = new List<Transform>();
            Transform Step(Component step) { inserted.Add(step.transform); return step.transform; }

            ToxicAirStep toxic = MissionSetup.Add<ToxicAirStep>(root, "Event · House Turns Toxic", null);
            MissionSetup.Set(toxic, "_zone", air);
            MissionSetup.Set(toxic, "_active", true);
            Step(toxic);

            MissionSetup.Story(root, "Story · Cat", MissionSetup.Teammate,
                "Стривай! Від реакції токсичної води з бензином наш будинок наповнився отруйними випарами… А всередині залишився котик!\n\n"
                + "Без протигазу туди не можна — випари одразу виштовхнуть тебе назовні. Протигаз лежить біля нас: візьми його, надінь, "
                + "знайди котика й винеси на вулицю. Прислухайся — він нявкає!");
            Step(root.GetChild(root.childCount - 1));

            MissionSetup.PickUp(root, "Pick Up · Gas Mask", "Візьми протигаз", inventory, mask);
            Step(root.GetChild(root.childCount - 1));

            WearGasMaskObjective wear = MissionSetup.Add<WearGasMaskObjective>(root, "Wear · Gas Mask", "Надінь протигаз");
            MissionSetup.Set(wear, "_mask", wearer);
            MissionSetup.Hint(wear, "Вибери протигаз в інвентарі та натисни ЛКМ, щоб надіти його.", PlayerControl.Primary, null);
            Step(wear);

            PickUpObjective findCat = MissionSetup.Add<PickUpObjective>(root, "Pick Up · Cat", "Знайди котика в будинку", catMarker);
            MissionSetup.Set(findCat, "_inventory", inventory);
            MissionSetup.SetArray(findCat, "_items", cat);
            Step(findCat);

            RescueObjective rescue = MissionSetup.Add<RescueObjective>(root, RescueStepMark, "Винеси котика на вулицю");
            MissionSetup.Set(rescue, "_target", cat);
            MissionSetup.Set(rescue, "_inventory", inventory);
            MissionSetup.Set(rescue, "_zone", houseZone);
            Step(rescue);

            ToxicAirStep clear = MissionSetup.Add<ToxicAirStep>(root, "Event · Toxic Air Clears", null);
            MissionSetup.Set(clear, "_zone", air);
            MissionSetup.Set(clear, "_active", false);
            Step(clear);

            MissionSetup.Story(root, "Story · Cat Saved", MissionSetup.Teammate,
                "Ти врятував котика! Він трохи наляканий, але живий і цілий.\n\n"
                + "Випари вже розвіюються. Тепер кинь мило в підземний резервуар — мильна вода нам ще знадобиться.");
            Step(root.GetChild(root.childCount - 1));

            NeutralizeObjective soapStep = MissionSetup.Add<NeutralizeObjective>(root, "Neutralize · Soap", "Кинь мило в підземний резервуар");
            MissionSetup.Set(soapStep, "_tank", tank);
            MissionSetup.Set(soapStep, "_soap", soap);
            MissionSetup.Set(soapStep, "_needToxin", false);
            MissionSetup.Set(soapStep, "_needSoap", true);
            MissionSetup.Set(soapStep, "_soapMarker", soapMarker);
            MissionSetup.Hint(soapStep, "Кинь мило в підземний резервуар — вода в ньому стане мильною.", null, null);
            Step(soapStep);

            SetActiveStep fires = MissionSetup.Add<SetActiveStep>(root, "Event · Roof Fires", null);
            MissionSetup.SetArray(fires, "_objects", roofFires);
            MissionSetup.Set(fires, "_active", true);
            Step(fires);

            MissionSetup.Story(root, "Story · Roof", MissionSetup.Teammate,
                "Ой, ні! Вогонь перекинувся на дах, а знизу нам туди не дістати!\n\n"
                + "Під'єднай брандспойт до трійника з мильною водою та облий мене — бульбашка підніме мене на дах, і я загашу вогонь згори!");
            Step(root.GetChild(root.childCount - 1));

            Place(inserted, neutralize.transform.GetSiblingIndex() + 1);

            // ── After the soapy-water nozzle: lift a teammate, put the roof out ──
            inserted.Clear();
            LiftTeammateObjective lift = MissionSetup.Add<LiftTeammateObjective>(root, "Lift · Teammate in Bubble",
                "Облий напарника мильною водою, щоб підняти його на дах");
            MissionSetup.SetArray(lift, "_teammates", Object.FindObjectsByType<SoapBubbleController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene == scene).Cast<Object>().ToArray());
            MissionSetup.Hint(lift, "Мильна вода огортає напарника бульбашкою й піднімає його вгору.", null, null);
            Step(lift);

            ExtinguishFiresObjective roofOut = MissionSetup.Add<ExtinguishFiresObjective>(root, "Extinguish · Roof", "Загаси вогонь на даху");
            MissionSetup.SetArray(roofOut, "_zones", roofZone);
            Step(roofOut);
            Place(inserted, soapyTee.transform.GetSiblingIndex() + 1);

            Renumber(root);
            report.Add($"Mission: rescue steps after \"{neutralize.name}\", roof steps after \"{soapyTee.name}\".");
            report.Add($"Cat at {cat.transform.position}, gas mask at {mask.transform.position}, exit at {exit}. CHECK they are reachable.");
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Rooms of the teammates' house: the room closest to the crew and every room stacked above or below it.</summary>
        private static List<RoomTemperature> TeammateHouseRooms(Vector3 crew)
        {
            List<RoomTemperature> rooms = Object.FindObjectsByType<RoomTemperature>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.name != FirstHouseRoomName && r.GetComponent<BoxCollider>() != null).ToList();
            if (rooms.Count == 0)
                return rooms;

            RoomTemperature nearest = rooms.OrderBy(r => Flat(r.GetComponent<BoxCollider>().bounds.center - crew).sqrMagnitude).First();
            Bounds box = nearest.GetComponent<BoxCollider>().bounds;
            float reach = Mathf.Max(box.extents.x, box.extents.z);
            return rooms.Where(r => Flat(r.GetComponent<BoxCollider>().bounds.center - box.center).magnitude <= reach).ToList();
        }

        /// <summary>A point on the ground just outside the house, on the side facing <paramref name="toward"/>.</summary>
        private static Vector3 Outside(Bounds house, Vector3 toward, float margin)
        {
            Vector3 direction = Flat(toward - house.center);
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.forward;
            direction.Normalize();

            float distance = Mathf.Abs(direction.x) > 0.001f ? house.extents.x / Mathf.Abs(direction.x) : float.MaxValue;
            distance = Mathf.Min(distance, Mathf.Abs(direction.z) > 0.001f ? house.extents.z / Mathf.Abs(direction.z) : float.MaxValue);
            Vector3 point = house.center + direction * (distance + margin);
            return OnFloor(new Vector3(point.x, house.max.y, point.z), house.max.y + 5f, house.min.y - 10f) + Vector3.up * 0.1f;
        }

        /// <summary>The first solid surface below <paramref name="point"/> between the two heights (the point itself if none).</summary>
        private static Vector3 OnFloor(Vector3 point, float fromHeight, float toHeight)
        {
            int mask = ~((1 << 2) | LayerBit("Room") | LayerBit("Fire") | LayerBit("Player") | LayerBit("Water"));
            Vector3 from = new Vector3(point.x, fromHeight, point.z);
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, Mathf.Max(0.1f, fromHeight - toHeight), mask, QueryTriggerInteraction.Ignore)
                         .OrderBy(h => h.distance))
            {
                if (hit.collider.attachedRigidbody == null && hit.normal.y > 0.6f)
                    return hit.point;
            }

            return new Vector3(point.x, toHeight + 0.5f, point.z);
        }

        private static int LayerBit(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? 1 << layer : 0;
        }

        private static PickableItem SpawnItem(string prefabPath, string name, Scene scene, Vector3 floor)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.position = floor + Vector3.up * 0.05f;
            return instance.GetComponent<PickableItem>();
        }

        /// <summary>Fire prefabs on the roof (the first hit from above over the house), hidden until the mission lights them.</summary>
        private static GameObject BuildRoofFires(Bounds house, Scene scene, out Bounds roof, List<string> report)
        {
            var parent = new GameObject("Roof Fires (Mission)");
            SceneManager.MoveGameObjectToScene(parent, scene);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePath);

            var spots = new[] { new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.25f), new Vector2(-0.25f, 0.3f), new Vector2(0.3f, 0.3f) };
            float top = house.max.y;
            roof = new Bounds(new Vector3(house.center.x, top, house.center.z), new Vector3(house.size.x, 0.1f, house.size.z));
            for (int i = 0; i < RoofFireCount && i < spots.Length; i++)
            {
                Vector3 above = house.center + new Vector3(spots[i].x * house.size.x, 0f, spots[i].y * house.size.z);
                Vector3 point = OnFloor(above, house.max.y + 15f, house.max.y - 2.5f);
                top = Mathf.Max(top, point.y);
                var fire = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                fire.name = $"Roof Fire {i + 1}";
                fire.transform.position = point + Vector3.up * 0.1f;
                roof.Encapsulate(point);
            }

            roof.Encapsulate(new Vector3(roof.center.x, top + 4f, roof.center.z));
            roof.Encapsulate(new Vector3(roof.center.x, roof.min.y - 1.5f, roof.center.z));
            parent.SetActive(false);
            report.Add($"Roof fires: {RoofFireCount} under \"{parent.name}\" (hidden), roof at about {top:0.#} m. CHECK they sit on the roof.");
            return parent;
        }

        private static void Place(List<Transform> steps, int index)
        {
            for (int i = 0; i < steps.Count; i++)
                steps[i].SetSiblingIndex(index + i);
        }

        /// <summary>Numbers the mission steps again in their order ("01 · ...").</summary>
        private static void Renumber(Transform root)
        {
            int n = 0;
            foreach (Transform child in root)
            {
                if (child.GetComponent<MissionStep>() == null)
                    continue;

                string title = StepNumber.Replace(child.name, string.Empty);
                child.name = $"{++n:00} · {title}";
            }
        }

        private static MissionStep FindStep(Transform root, string part)
        {
            foreach (Transform child in root)
            {
                if (child.name.Contains(part) && child.TryGetComponent(out MissionStep step))
                    return step;
            }

            return null;
        }

        private static MissionZone FindZone(Transform root, string name)
        {
            return root.GetComponentsInChildren<MissionZone>(true).FirstOrDefault(z => z.name == name);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
