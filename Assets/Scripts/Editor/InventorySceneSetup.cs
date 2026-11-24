using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;
using WaterChaos.UI;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of the inventory UI in the open scene: slots, notice and throw hint, all wired to the player.
    /// Running it again rebuilds the UI. Undo-able; save the scene afterwards.
    /// </summary>
    public static class InventorySceneSetup
    {
        private const string RootName = "Inventory UI";
        private const string PlayerLayer = "Player";
        private const int SlotCount = 3;
        private const float SlotSize = 96f;
        private const float SlotSpacing = 12f;

        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Color FrameColor = new Color(1f, 0.8f, 0.2f);

        [MenuItem("Water Chaos/Setup Inventory UI")]
        private static void Setup()
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>();
            PlayerItemInteractor interactor = Object.FindAnyObjectByType<PlayerItemInteractor>();
            PlayerItemThrower thrower = Object.FindAnyObjectByType<PlayerItemThrower>();
            if (inventory == null || interactor == null || thrower == null)
            {
                Debug.LogError("Inventory setup: the scene needs PlayerInventory, PlayerItemInteractor and PlayerItemThrower.");
                return;
            }

            Canvas canvas = FindOrCreateCanvas();
            RemoveOld(canvas.transform);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            RectTransform root = CreateRect(RootName, canvas.transform);
            Stretch(root, 0f);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Setup Inventory UI");

            InventorySlotView[] slots = BuildSlots(root, sprite, font);
            AddInventoryView(root.gameObject, inventory, slots);
            BuildMessages(root, font, interactor, thrower);

            FixThrower(thrower);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Inventory setup: done. Save the scene (Cmd+S).");
        }

        private static Canvas FindOrCreateCanvas()
        {
            Canvas canvas = null;
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (candidate.isRootCanvas && candidate.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = candidate;
                    break;
                }
            }

            if (canvas == null)
            {
                GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.layer = LayerMask.NameToLayer("UI");
                Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);

            Undo.RecordObject(scaler, "Setup Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void RemoveOld(Transform canvas)
        {
            Transform old = canvas.Find(RootName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
        }

        private static InventorySlotView[] BuildSlots(Transform root, Sprite sprite, Font font)
        {
            RectTransform bar = CreateRect("Slots", root);
            bar.anchorMin = BottomCenter;
            bar.anchorMax = BottomCenter;
            bar.pivot = BottomCenter;
            bar.anchoredPosition = new Vector2(0f, 30f);
            bar.sizeDelta = new Vector2(SlotCount * SlotSize + (SlotCount - 1) * SlotSpacing, SlotSize);

            HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            InventorySlotView[] slots = new InventorySlotView[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                slots[i] = BuildSlot(bar, i + 1, sprite, font);

            return slots;
        }

        private static InventorySlotView BuildSlot(Transform parent, int number, Sprite sprite, Font font)
        {
            RectTransform slot = CreateRect($"Slot {number}", parent);
            slot.sizeDelta = new Vector2(SlotSize, SlotSize);

            Image background = slot.gameObject.AddComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            RectTransform iconRect = CreateRect("Icon", slot);
            Stretch(iconRect, 14f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            RectTransform frameRect = CreateRect("Selection Frame", slot);
            Stretch(frameRect, -4f);
            Image frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.color = FrameColor;
            frame.raycastTarget = false;
            Outline thickness = frameRect.gameObject.AddComponent<Outline>();
            thickness.effectColor = FrameColor;
            thickness.effectDistance = new Vector2(2f, 2f);

            RectTransform numberRect = CreateRect("Number", slot);
            numberRect.anchorMin = TopLeft;
            numberRect.anchorMax = TopLeft;
            numberRect.pivot = TopLeft;
            numberRect.anchoredPosition = new Vector2(8f, -4f);
            numberRect.sizeDelta = new Vector2(30f, 26f);
            Text label = CreateText(numberRect, font, 20, TextAnchor.UpperLeft, Color.white);
            label.text = number.ToString();

            InventorySlotView view = slot.gameObject.AddComponent<InventorySlotView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_selectionFrame").objectReferenceValue = frameRect.gameObject;
            so.FindProperty("_number").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static void AddInventoryView(GameObject root, PlayerInventory inventory, InventorySlotView[] slots)
        {
            InventoryView view = root.AddComponent<InventoryView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_inventory").objectReferenceValue = inventory;

            SerializedProperty array = so.FindProperty("_slots");
            array.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildMessages(Transform root, Font font, PlayerItemInteractor interactor, PlayerItemThrower thrower)
        {
            Text notice = CreateBottomText("Notice", root, font, 190f, 30, new Color(1f, 0.35f, 0.3f));
            notice.fontStyle = FontStyle.Bold;
            Text hint = CreateBottomText("Throw Hint", root, font, 140f, 20, Color.white);

            InventoryMessageView view = root.gameObject.AddComponent<InventoryMessageView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_interactor").objectReferenceValue = interactor;
            so.FindProperty("_thrower").objectReferenceValue = thrower;
            so.FindProperty("_notice").objectReferenceValue = notice;
            so.FindProperty("_hint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The throw must not collide with the player's own capsule; the trajectory line must be thin.</summary>
        private static void FixThrower(PlayerItemThrower thrower)
        {
            SerializedObject so = new SerializedObject(thrower);
            int player = LayerMask.NameToLayer(PlayerLayer);
            if (player >= 0)
            {
                SerializedProperty mask = so.FindProperty("_collisionMask");
                mask.intValue &= ~(1 << player);
            }
            so.ApplyModifiedProperties();

            LineRenderer line = (LineRenderer)so.FindProperty("_trajectoryLine").objectReferenceValue;
            if (line == null)
                return;

            Undo.RecordObject(line, "Setup Trajectory Line");
            line.useWorldSpace = true;
            line.widthMultiplier = 1f;
            line.startWidth = 0.03f;
            line.endWidth = 0.03f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private static Text CreateBottomText(string name, Transform parent, Font font, float y, int size, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = BottomCenter;
            rect.anchorMax = BottomCenter;
            rect.pivot = BottomCenter;
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(600f, 40f);
            return CreateText(rect, font, size, TextAnchor.MiddleCenter, color);
        }

        private static Text CreateText(RectTransform rect, Font font, int size, TextAnchor alignment, Color color)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            rect.gameObject.AddComponent<Shadow>();
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Fills the parent, inset by padding on every side (negative = sticks out).</summary>
        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
