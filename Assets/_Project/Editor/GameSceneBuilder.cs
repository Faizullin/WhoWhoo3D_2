using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Wave;
using WhoWhoo3D.Input;
using WhoWhoo3D.MapEditor;
using WhoWhoo3D.UI.Debugging;
using WhoWhoo3D.UI.HUD;

namespace WhoWhoo3D.EditorTools;

/// <summary>
/// Builds a playable scene from code.
///
/// Scenes are wiring, and wiring done by hand drifts: a missing reference or an
/// unticked box costs a confusing play session to find. Generating it means every
/// scene is assembled the same way, and the setup is reviewable as a diff.
///
/// This is also the seam the future SingleplayerGame and MultiplayerGame scenes should
/// come through — same layout, same components, different map and player count.
/// </summary>
public static class GameSceneBuilder
{
    private const string CubePrefabPath = "Assets/_Project/Prefabs/Board/Cube.prefab";
    private const string SceneFolder = "Assets/_Project/Scenes";

    [MenuItem("Tools/WhoWhoo3D/Build Demo4x4 Scene")]
    public static void BuildDemo4x4() =>
        Build(new SceneSpec
        {
            SceneName = "Demo4x4",
            MapFileName = "demo4x4",
            PlayerCount = 3,
            BoardSize = 4,
        });

    [MenuItem("Tools/WhoWhoo3D/Build MapEditor Demo Scene")]
    public static void BuildMapEditorDemo()
    {
        var cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
        if (cubePrefab == null)
        {
            Debug.LogError($"Cube prefab not found at {CubePrefabPath}. Scene not built.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateLighting();
        var camera = CreateCamera(6);
        var boardView = CreateBoard(cubePrefab);

        var go = new GameObject("Editor");
        var controller = go.AddComponent<MapEditorController>();
        go.AddComponent<MapEditorHud>();

        var so = new SerializedObject(controller);
        so.FindProperty("boardView").objectReferenceValue = boardView;
        so.FindProperty("editorCamera").objectReferenceValue = camera;
        so.FindProperty("mapFileName").stringValue = "custom";
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(SceneFolder);
        string path = $"{SceneFolder}/DemoMapEditor.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();

        Debug.Log($"Built {path}. Place/erase cubes on the grid, paint tiles, save to StreamingAssets/Maps.");
        EditorUtility.DisplayDialog("WhoWhoo3D", "Built DemoMapEditor.unity", "OK");
    }

    public struct SceneSpec
    {
        public string SceneName;
        public string MapFileName;
        public int PlayerCount;

        /// <summary>Cells across, used only to frame the camera.</summary>
        public int BoardSize;
    }

    public static void Build(SceneSpec spec)
    {
        var cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
        if (cubePrefab == null)
        {
            Debug.LogError($"Cube prefab not found at {CubePrefabPath}. Scene not built.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateLighting();
        var camera = CreateCamera(spec.BoardSize);
        camera.fieldOfView = 44f;
        var boardView = CreateBoard(cubePrefab);
        CreateGameObjects(boardView, camera, spec);

        Directory.CreateDirectory(SceneFolder);
        string path = $"{SceneFolder}/{spec.SceneName}.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();

        Debug.Log($"Built {path} — {spec.PlayerCount} players on map '{spec.MapFileName}'. " +
                  "Add it to Build Settings if you want to ship it.");
        EditorUtility.DisplayDialog("WhoWhoo3D",
            $"Built {spec.SceneName}.unity\n\n{spec.PlayerCount} players, map '{spec.MapFileName}'.", "OK");
    }

    private static void CreateLighting()
    {
        var go = new GameObject("Directional Light");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static Camera CreateCamera(int boardSize)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();

        // Frame the slab from above and in front. Cells sit on integer coordinates
        // from 0..boardSize-1, so the centre is (boardSize-1)/2. The floors keep a
        // small board from putting the camera inside it.
        float centre = (boardSize - 1) * 0.5f;
        float height = Mathf.Max(6f, boardSize * 1.15f);
        float back = Mathf.Max(5f, boardSize * 0.85f);

        go.transform.position = new Vector3(centre, height, centre - back);
        go.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        camera.backgroundColor = new Color(0.29f, 0.28f, 0.27f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        return camera;
    }

    private static BoardView CreateBoard(GameObject cubePrefab)
    {
        var go = new GameObject("Board");
        var boardView = go.AddComponent<BoardView>();

        // BoardView is only a renderer; GameController owns the map choice.
        var so = new SerializedObject(boardView);
        so.FindProperty("cubePrefab").objectReferenceValue = cubePrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        return boardView;
    }

    private static void CreateGameObjects(BoardView boardView, Camera camera, SceneSpec spec)
    {
        var go = new GameObject("Game");

        var controller = go.AddComponent<GameController>();
        var animator = go.AddComponent<WaveAnimator>();
        var input = go.AddComponent<PlayerInputHandler>();
        go.AddComponent<ScoreboardView>();
        var timeline = new SerializedObject(go.AddComponent<TimelineView>());
        timeline.FindProperty("visible").boolValue = false;
        timeline.ApplyModifiedPropertiesWithoutUndo();

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("boardView").objectReferenceValue = boardView;
        controllerSo.FindProperty("waveAnimator").objectReferenceValue = animator;
        controllerSo.FindProperty("showSetupOnStart").boolValue = true;
        controllerSo.FindProperty("mapFileName").stringValue = spec.MapFileName;
        controllerSo.FindProperty("playerCount").intValue = spec.PlayerCount;
        controllerSo.FindProperty("enableAi").boolValue = false;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        var animatorSo = new SerializedObject(animator);
        animatorSo.FindProperty("boardView").objectReferenceValue = boardView;
        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        // clickAction is deliberately left unset: an inline action with no bindings
        // would suppress every click, and unset means the handler reads the pointer.
        var inputSo = new SerializedObject(input);
        inputSo.FindProperty("gameController").objectReferenceValue = controller;
        inputSo.FindProperty("inputCamera").objectReferenceValue = camera;
        inputSo.ApplyModifiedPropertiesWithoutUndo();
    }
}

