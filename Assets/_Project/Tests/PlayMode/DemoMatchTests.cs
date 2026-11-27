#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Networking;

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

    [UnityTest]
    public IEnumerator KeyboardConfiguresSoloAndReachesLiveInventory()
    {
        yield return OpenDemo();
        var game = Find<GameController>();
        var keys = InputSystem.AddDevice<Keyboard>();
        string Focus() => EventSystem.current.currentSelectedGameObject?.name;
        try
        {
            yield return null;
            Assert.That(Focus(), Is.EqualTo("Hot-seat"));
            yield return Press(keys, Key.Tab);
            Assert.That(Focus(), Is.EqualTo("Solo"));
            yield return Press(keys, Key.Enter);
            yield return CompactHudSequence.ClickControl(game, _mouse, "Next map");
            yield return CompactHudSequence.ClickControl(game, _mouse, "4 players");
            yield return CompactHudSequence.ClickControl(game, _mouse, "Supply after 4 claims");
            yield return CompactHudSequence.ClickControl(game, _mouse, "Solo");
            yield return Press(keys, Key.LeftShift, Key.Tab);
            Assert.That(Focus(), Is.EqualTo("Hot-seat"));
            yield return Press(keys, Key.LeftShift, Key.Tab);
            Assert.That(Focus(), Is.EqualTo("Start match"));
            yield return Press(keys, Key.Enter);
            Assert.That(game.InSetup, Is.False);
            Assert.That(game.MapName, Is.EqualTo("sample"));
            Assert.That(game.Setup.Supply, Is.True);
            CollectionAssert.AreEqual(new[] { false, true, true, true }, game.Roster.Players.Select(p => p.IsAi));
            yield return Press(keys, Key.Tab);
            Assert.That(Focus(), Is.EqualTo("Relay"));
            yield return Press(keys, Key.Enter);
            Assert.That(Focus(), Is.EqualTo("Cancel"));
            yield return Press(keys, Key.Escape);
            Assert.That(game.SelectedAction, Is.EqualTo(MatchAction.Claim));

            game.DeclareGameOver(PlayerId.None);
            yield return null;
            yield return null;
            Assert.That(Focus(), Is.EqualTo("Play again"));
            yield return Press(keys, Key.Enter);
            Assert.That(game.IsGameOver || game.InSetup, Is.False);
            Assert.That(game.MapName, Is.EqualTo("sample"));
            Assert.That(game.PlayerCount, Is.EqualTo(4));
            game.DeclareGameOver(PlayerId.None);
            yield return null;
            yield return null;
            yield return Press(keys, Key.Tab);
            Assert.That(Focus(), Is.EqualTo("Match setup"));
            yield return Press(keys, Key.Enter);
            Assert.That(game.InSetup, Is.True);
            Assert.That(Focus(), Is.EqualTo("Solo"));
        }
        finally { InputSystem.RemoveDevice(keys); }
    }

    [UnityTest]
    public IEnumerator BrowserJoinsAdvertisedRoomAndReturnsToSetup()
    {
        yield return OpenDemo();
        var game = Find<GameController>();
        var guest = game.GetComponent<LanMatchSession>();
        var hostObject = new GameObject("Discovery host");
        var hostGame = hostObject.AddComponent<GameController>();
        hostGame.InitializeWithBoard(MapLoader.Load("sample"));
        var host = hostObject.AddComponent<LanMatchSession>();
        host.Bind(hostGame);
        var keys = InputSystem.AddDevice<Keyboard>();
        try
        {
            yield return CompactHudSequence.ClickControl(game, _mouse, "Join LAN");
            Assert.That(guest.IsBrowsing, Is.True);
            var field = game.GetComponentsInChildren<TMP_InputField>().Single();
            field.text = "invalid";
            yield return CompactHudSequence.ClickControl(game, _mouse, "Join by address");
            var status = game.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Status");
            StringAssert.Contains("IPv4", status.text);
            yield return Press(keys, Key.LeftShift, Key.Tab);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(field.gameObject));
            yield return Press(keys, Key.Tab);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Join by address"));

            host.Host("sample", 3, 0, "Test <room>");
            float deadline = Time.realtimeSinceStartup + 8;
            while (!game.GetComponentsInChildren<TMP_Text>().Any(t => t.text.StartsWith("Test <room>")))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Room never appeared in the browser");
                yield return null;
            }
            var listing = game.GetComponentsInChildren<TMP_Text>().Single(t => t.text.StartsWith("Test <room>"));
            Assert.That(listing.richText, Is.False);
            yield return CompactHudSequence.ClickControl(game, _mouse, listing.transform.parent.name);
            while (guest.State != LanState.Lobby || guest.LocalSeat != 2)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), guest.Status);
                yield return null;
            }
            yield return CompactHudSequence.ClickControl(game, _mouse, "Leave room");
            Assert.That(guest.State, Is.EqualTo(LanState.Closed));
            Assert.That(game.InSetup && guest.IsBrowsing, Is.True);
            yield return CompactHudSequence.ClickControl(game, _mouse, "Hot-seat");
            Assert.That(guest.IsBrowsing, Is.False);
        }
        finally
        {
            InputSystem.RemoveDevice(keys);
            host.Leave();
            Object.Destroy(hostObject);
        }
    }

    private static IEnumerator Press(Keyboard keys, params Key[] pressed)
    {
        InputSystem.QueueStateEvent(keys, new KeyboardState(pressed));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keys, new KeyboardState());
        yield return null;
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
