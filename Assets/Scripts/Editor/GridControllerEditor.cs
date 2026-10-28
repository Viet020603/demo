#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Demo.Gameplay;

namespace Demo.EditorTools
{
    [CustomEditor(typeof(GridController))]
    public class GridControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Build Scene Objects creates the camera, grid, pool objects and selection marker directly in the scene " +
                "so you can tweak them before Play. Press it again after changing grid size / cell size / zoom.",
                MessageType.Info);

            if (GUILayout.Button("Build Scene Objects", GUILayout.Height(30)))
                ((GridController)target).BuildSceneObjects();

            if (GUILayout.Button("Clear Pool Objects"))
                ((GridController)target).ClearSceneObjects();
        }
    }

    public static class GridMenu
    {
        [MenuItem("Tools/Grid Demo/Build Scene Objects")]
        public static void BuildFromMenu()
        {
            var controller = Object.FindFirstObjectByType<GridController>();
            if (controller == null)
            {
                var go = new GameObject("GameBootstrap");
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
                controller = go.AddComponent<GridController>();
            }

            controller.BuildSceneObjects();
            Selection.activeObject = controller.gameObject;
        }
    }
}
#endif
