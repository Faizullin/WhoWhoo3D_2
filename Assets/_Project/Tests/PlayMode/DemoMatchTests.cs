#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Tests.PlayMode;

public class DemoMatchTests
{
    private InputSettings.BackgroundBehavior _previousBackground;
    private Mouse _mouse;
    private Scene _scene;
    private InputSettings.EditorInputBehaviorInPlayMode _previousRouting;

    [UnityTest]
    public IEnumerator SavedDemoAcceptsClicksThroughVictoryAndRestart()
    {
        yield return OpenDemo();
        yield return DemoMatchSequence.Run(Find<GameController>(), Find<BoardView>(), Find<Camera>(), _mouse, _ => { }, false);
    }

    [UnityTest]
    public IEnumerator FourSeatFreezeUsesHudAndBoardInput()
    {
        yield return OpenDemo();
        yield return CompactHudSequence.Run(Find<GameController>(), Find<BoardView>(), Find<Camera>(), _mouse, null);
    }

    private T Find<T>() where T : Component =>
        System.Array.Find(_scene.GetRootGameObjects(), go => go.GetComponent<T>() != null).GetComponent<T>();

    private IEnumerator OpenDemo()
    {
        _previousBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        _previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        _mouse = InputSystem.AddDevice<Mouse>();
        const string path = "Assets/_Project/Scenes/Demo4x4.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        _scene = SceneManager.GetSceneByPath(path);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        InputSystem.settings.backgroundBehavior = _previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = _previousRouting;
        if (_mouse != null) InputSystem.RemoveDevice(_mouse);
        if (_scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
    }
}
#endif
