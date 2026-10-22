using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Creates both scenes from scratch. The UI itself comes from WallUIBuilder / DrawingUIBuilder.</summary>
public static class PledgeWallSceneSetup
{
    private const string WallScenePath = "Assets/Scenes/WallScene.unity";
    private const string DrawingScenePath = "Assets/Scenes/DrawingScene.unity";
    private const string ReceiverPrefabPath = "Assets/Prefabs/WallReceiver.prefab";
    private const string NetworkPrefabPath = "Assets/Prefabs/NetworkManager.prefab";

    [MenuItem("Cyber Pledge Wall/Build Scenes And Prefabs")]
    public static void BuildEverything()
    {
        EnsureFolders();
        GameObject receiverPrefab = CreateWallReceiverPrefab();
        WallUIBuilder.CreatePledgeCardPrefab();
        CreateNetworkManagerPrefab(receiverPrefab);
        CreateWallScene(receiverPrefab);
        CreateDrawingScene();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Cyber Pledge Wall scenes, prefabs, and build settings are ready.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");
    }

    private static GameObject CreateWallReceiverPrefab()
    {
        var go = new GameObject("WallReceiver");
        go.AddComponent<NetworkObject>();
        go.AddComponent<WallReceiver>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, ReceiverPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void CreateNetworkManagerPrefab(GameObject receiverPrefab)
    {
        var go = new GameObject("NetworkManager");
        var nm = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();
        var session = go.AddComponent<NetworkSession>();

        nm.NetworkConfig.NetworkTransport = transport;
        nm.NetworkConfig.EnableSceneManagement = false;
        nm.NetworkConfig.PlayerPrefab = null;
        transport.MaxPayloadSize = NetworkSession.DefaultMaxPayloadSize;

        var so = new SerializedObject(session);
        so.FindProperty("wallReceiverPrefab").objectReferenceValue = receiverPrefab;
        so.FindProperty("port").intValue = NetworkSession.DefaultPort;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(go, NetworkPrefabPath);
        Object.DestroyImmediate(go);
    }

    private static void CreateWallScene(GameObject receiverPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "WallScene";

        CreateEventSystem();
        GameObject networkGo = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath)) as GameObject;
        networkGo.name = "NetworkManager";

        Canvas canvas = CreateCanvas("WallCanvas");

        var wallManagerGo = new GameObject("WallManager");
        var wallManager = wallManagerGo.AddComponent<WallManager>();
        var wallSo = new SerializedObject(wallManager);
        wallSo.FindProperty("wallReceiverPrefab").objectReferenceValue = receiverPrefab;
        wallSo.ApplyModifiedPropertiesWithoutUndo();

        WallUIBuilder.Build(canvas);
        EditorSceneManager.SaveScene(scene, WallScenePath);
    }

    private static void CreateDrawingScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "DrawingScene";

        CreateEventSystem();
        GameObject networkGo = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath)) as GameObject;
        networkGo.name = "NetworkManager";

        Canvas canvas = CreateCanvas("DrawingCanvas");
        new GameObject("DrawingAppController").AddComponent<DrawingAppController>();

        DrawingUIBuilder.Build(canvas);
        EditorSceneManager.SaveScene(scene, DrawingScenePath);
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(WallScenePath, true),
            new EditorBuildSettingsScene(DrawingScenePath, true)
        };
    }

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return canvas;
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<EventSystem>().sendNavigationEvents = true;
    }
}
