using Demo.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Demo.Editor
{
    public static class UiGridPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string CellPrefabPath = PrefabFolder + "/UICell.prefab";
        private const string BackgroundSpritePath = "Assets/Resources/Sprite/dat.png";
        private const string CanvasPrefabPath = PrefabFolder + "/GridCanvas.prefab";

        [MenuItem("Tools/Grid Demo/Create Canvas Prefabs and Scene Instance")]
        public static void CreateCanvasPrefabsAndSceneInstance()
        {
            EnsurePrefabFolder();
            GameObject cellPrefab = CreateCellPrefab();
            GameObject canvasPrefab = CreateCanvasPrefab(cellPrefab);

            var existing = Object.FindFirstObjectByType<UiGridController>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Canvas prefabs were created. A UiGridController already exists in this scene, so no duplicate instance was added.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
            var controller = instance.GetComponent<UiGridController>();
            var camera = Camera.main;
            if (camera != null)
            {
                var canvas = instance.GetComponent<Canvas>();
                canvas.worldCamera = camera;

                var serializedController = new SerializedObject(controller);
                serializedController.FindProperty("canvasCamera").objectReferenceValue = camera;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(instance, "Create Canvas Grid");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log($"Created prefab assets at '{CanvasPrefabPath}' and '{CellPrefabPath}', and added GridCanvas to the open scene.");
        }

        private static GameObject CreateCellPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("UICell", typeof(RectTransform), typeof(Image), typeof(CellView));
            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.white;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCanvasPrefab(GameObject cellPrefab)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject(
                "GridCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(UiGridController));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(RectMask2D),
                typeof(Image));
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var background = viewportObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.2f, 0.12f, 1f);
            background.raycastTarget = false;

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

            var markerObject = new GameObject("SelectionMarker", typeof(RectTransform), typeof(Image));
            var marker = markerObject.GetComponent<RectTransform>();
            marker.SetParent(content, false);
            marker.anchorMin = marker.anchorMax = Vector2.zero;
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(64f, 64f);
            var markerImage = markerObject.GetComponent<Image>();
            markerImage.color = new Color(1f, 1f, 1f, 0.45f);
            markerImage.raycastTarget = false;
            markerObject.SetActive(false);

            var controller = root.GetComponent<UiGridController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("canvas").objectReferenceValue = canvas;
            serializedController.FindProperty("viewport").objectReferenceValue = viewport;
            serializedController.FindProperty("content").objectReferenceValue = content;
            serializedController.FindProperty("mapBackground").objectReferenceValue = backgroundImage;
            serializedController.FindProperty("backgroundSprite").objectReferenceValue = backgroundImage.sprite;
            serializedController.FindProperty("selectionMarker").objectReferenceValue = marker;
            serializedController.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }
}
