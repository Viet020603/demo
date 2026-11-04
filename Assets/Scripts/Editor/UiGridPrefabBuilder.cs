using Demo.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Demo.Editor
{
    public static class UiGridPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string CellPrefabPath = PrefabFolder + "/UICell.prefab";
        private const string BackgroundSpritePath = "Assets/Resources/Sprite/dat.png";
        private const string PopupFolder = "Assets/Resources/UI/Popup/Grid";
        private const string PopupPrefabPath = PopupFolder + "/PopupGrid.prefab";

        [MenuItem("Tools/Grid Demo/Create Popup Prefabs")]
        public static void CreatePopupPrefabs()
        {
            EnsureFolders();
            GameObject cellPrefab = CreateCellPrefab();
            GameObject popupPrefab = CreatePopupPrefab(cellPrefab);
            Selection.activeObject = popupPrefab;
            Debug.Log($"Created '{PopupPrefabPath}' and '{CellPrefabPath}'. UiManager loads the popup from Resources/{UiManager.PopupGridResourcePath} and parents it under your existing Canvas.");
        }

        private static GameObject CreateCellPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("UICell", typeof(RectTransform), typeof(Image), typeof(CellView));
            var image = root.GetComponent<Image>();
            image.raycastTarget = true;
            image.color = Color.white;
            var cellButton = root.AddComponent<Button>();
            cellButton.targetGraphic = image;
            cellButton.navigation = new Navigation { mode = Navigation.Mode.None };

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePopupPrefab(GameObject cellPrefab)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("PopupGrid", typeof(RectTransform), typeof(UiGridController));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            var viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(RectMask2D),
                typeof(Image),
                typeof(ScrollRect));
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(rootRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var background = viewportObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.2f, 0.12f, 1f);
            background.raycastTarget = true;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            var content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = Vector2.zero;
            content.pivot = Vector2.zero;

            var backgroundObject = new GameObject("MapBackground", typeof(RectTransform), typeof(Image));
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.SetParent(content, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            backgroundImage.raycastTarget = false;

            var frameObject = new GameObject("MapFrame", typeof(RectTransform), typeof(Image));
            var frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.SetParent(content, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            var frameImage = frameObject.GetComponent<Image>();
            frameImage.raycastTarget = false;
            frameImage.enabled = false;

            var gridLinesObject = new GameObject("GridLines", typeof(RectTransform), typeof(CanvasRenderer), typeof(GridLinesGraphic));
            var gridLines = gridLinesObject.GetComponent<GridLinesGraphic>();
            gridLines.rectTransform.SetParent(content, false);
            gridLines.color = new Color(0.1f, 0.3f, 0.05f, 0.75f);
            gridLines.raycastTarget = false;

            var markerObject = new GameObject("SelectionMarker", typeof(RectTransform), typeof(Image));
            var marker = markerObject.GetComponent<RectTransform>();
            marker.SetParent(content, false);
            marker.anchorMin = marker.anchorMax = Vector2.zero;
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(64f, 64f);
            var markerImage = markerObject.GetComponent<Image>();
            markerImage.color = new Color(0.85f, 0.42f, 0.12f, 0.9f);
            markerImage.raycastTarget = false;
            markerObject.SetActive(false);

            var closeObject = new GameObject("BtnClose", typeof(RectTransform), typeof(Image), typeof(Button));
            var closeRect = closeObject.GetComponent<RectTransform>();
            closeRect.SetParent(rootRect, false);
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20f, -20f);
            closeRect.sizeDelta = new Vector2(90f, 90f);
            closeObject.GetComponent<Image>().color = new Color(0.75f, 0.2f, 0.2f, 1f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(closeRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.text = "X";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 48;
            label.color = Color.white;
            label.raycastTarget = false;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var controller = root.GetComponent<UiGridController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("btnClose").objectReferenceValue = closeObject.GetComponent<Button>();
            serializedController.FindProperty("scrollRect").objectReferenceValue = viewportObject.GetComponent<ScrollRect>();
            serializedController.FindProperty("viewport").objectReferenceValue = viewport;
            serializedController.FindProperty("content").objectReferenceValue = content;
            serializedController.FindProperty("mapBackground").objectReferenceValue = backgroundImage;
            serializedController.FindProperty("backgroundSprite").objectReferenceValue = backgroundImage.sprite;
            serializedController.FindProperty("mapFrame").objectReferenceValue = frameImage;
            serializedController.FindProperty("gridLines").objectReferenceValue = gridLines;
            serializedController.FindProperty("selectionMarker").objectReferenceValue = marker;
            serializedController.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PopupPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI/Popup")) AssetDatabase.CreateFolder("Assets/Resources/UI", "Popup");
            if (!AssetDatabase.IsValidFolder(PopupFolder)) AssetDatabase.CreateFolder("Assets/Resources/UI/Popup", "Grid");
        }
    }
}
