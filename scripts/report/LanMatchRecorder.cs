using System;
using System.IO;
using System.Linq;
using UnityEngine;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Networking;

// Runs in two standalone processes. The files coordinate capture and shutdown only.
public sealed class LanMatchRecorder : MonoBehaviour
{
    private GameController _game;
    private LanMatchSession _session;
    private string _directory, _role;
    private bool _capturedTurn, _finished, _foundRoom;
    private string _roomName;
    private float _deadline;

    private void Start()
    {
        _role = Environment.GetEnvironmentVariable("WHOWHOO_LAN_ROLE");
        _directory = Environment.GetEnvironmentVariable("WHOWHOO_FRAMES");
        _game = FindAnyObjectByType<GameController>();
        _session = FindAnyObjectByType<LanMatchSession>();
        _deadline = Time.realtimeSinceStartup + 100;
        _game.WaveAnimator.StepDuration = 0.05f;
        _roomName = Environment.GetEnvironmentVariable("WHOWHOO_ROOM_NAME");
        if (_role == "host")
        {
            _session.Host("demo4x4", 2, 0, _roomName);
        }
        else _session.BrowseRooms();
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup > _deadline || (!_finished &&
            (_session.State == LanState.Disconnected || (_session.State == LanState.Closed && !_session.IsBrowsing))))
        {
            Debug.LogError("LAN capture stopped: " + _session.Status);
            Application.Quit(1);
            return;
        }
        if (_finished)
        {
            if (File.Exists(Path.Combine(_directory, "complete"))) Application.Quit(0);
            return;
        }
        if (_session.IsBrowsing)
        {
            var room = _session.NearbyRooms.FirstOrDefault(r => r.Name == _roomName);
            if (room == null) return;
            if (!_foundRoom)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "lan-room-browser.png"));
                _foundRoom = true;
                return;
            }
            _session.Join(room);
            return;
        }
        if (_session.State != LanState.Playing) return;
        if (_game.IsGameOver)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "lan-" + _role + "-result.png"));
            Write(_role + ".json", JsonUtility.ToJson(MatchSnapshot.Capture(_game)));
            _finished = true;
            return;
        }
        if (!_game.AcceptsBoardInput) return;
        if (_role == "guest" && !_capturedTurn)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "lan-guest-turn.png"));
            _capturedTurn = true;
            return;
        }
        var rng = new System.Random(42 + _game.TurnNumber);
        var slot = BoardActions.LegalMoves(_game.Board, _game.ActivePlayer.Id).OrderBy(_ => rng.Next()).First();
        _game.TryPlayerAction(slot);
    }

    private void Write(string name, string contents)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path + ".tmp", contents);
        File.Move(path + ".tmp", path);
    }
}
