using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageEditorSceneBuilder
{
    [MenuItem("Tools/Rebellion/Create Stage Editor Scene")]
    public static void CreateStageEditorScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "StageEditor";

        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        CreateStageCamera();

        var controllerObject = new GameObject("StageEditorController");
        var controller = controllerObject.AddComponent<StageEditorController>();
        var previewRoot = new GameObject("StageEditorPreviewRoot");
        previewRoot.transform.SetParent(controllerObject.transform, false);

        var serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("stageFolder").stringValue = "Stages";
        serializedObject.FindProperty("mapPrefabFolder").stringValue = "Assets/03_Prefabs/Maps";
        serializedObject.FindProperty("enemyPrefabFolder").stringValue = "Assets/03_Prefabs/Pieces/Enemy";
        serializedObject.FindProperty("civilianPrefabFolder").stringValue = "Assets/03_Prefabs/Pieces/Civilian";
        serializedObject.FindProperty("cellSize").floatValue = 1.3f;
        serializedObject.FindProperty("entityYOffset").floatValue = 0.12f;
        serializedObject.FindProperty("previewRoot").objectReferenceValue = previewRoot.transform;

        AssignArray(serializedObject, "mapPrefabs", new[]
        {
            "Assets/03_Prefabs/Maps/Map_Bar.prefab",
            "Assets/03_Prefabs/Maps/Map_Museum.prefab",
            "Assets/03_Prefabs/Maps/Map_Warehouse.prefab",
            "Assets/03_Prefabs/Maps/Map_Table.prefab",
        });

        AssignArray(serializedObject, "enemyPiecePrefabs", new[]
        {
            "Assets/03_Prefabs/Pieces/Enemy/Enemy_Brawler.prefab",
            "Assets/03_Prefabs/Pieces/Enemy/Enemy_Boss.prefab",
            "Assets/03_Prefabs/Pieces/Enemy/Enemy_Gunman.prefab",
        });

        AssignArray(serializedObject, "civilianPiecePrefabs", new[]
        {
            "Assets/03_Prefabs/Pieces/Civilian/Civilian_01.prefab",
            "Assets/03_Prefabs/Pieces/Civilian/Civilian_Eliza.prefab",
        });

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, "Assets/01_Scenes/StageEditor.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Created Assets/01_Scenes/StageEditor.unity");
    }

    private static void AssignArray(SerializedObject serializedObject, string propertyName, string[] assetPaths)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = assetPaths.Length;
        for (int i = 0; i < assetPaths.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[i]);
        }
    }

    private static void CreateStageCamera()
    {
        GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Camera/OrbitCamera.prefab");
        if (cameraPrefab == null)
        {
            Debug.LogWarning("Stage editor could not find OrbitCamera prefab.");
            return;
        }

        var cameraObject = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab);
        cameraObject.name = "CameraOrbitPoint";
        cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Camera stageCamera = cameraObject.GetComponentInChildren<Camera>(true);
        if (stageCamera != null)
        {
            stageCamera.tag = "MainCamera";
        }
    }
}
