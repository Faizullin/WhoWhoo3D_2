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
    private float _deadline, _searchUntil;
    private bool _probe, _capturedLobby;
    private string _directAddress;

#if UNITY_STANDALONE_LINUX
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallProbe()
    {
        if (Environment.GetEnvironmentVariable("WHOWHOO_LAN_PROBE") == "1")
            new GameObject("LAN verification").AddComponent<LanMatchRecorder>();
    }
#endif

    private void Start()
    {
        _probe = Environment.GetEnvironmentVariable("WHOWHOO_LAN_PROBE") == "1";
        _directAddress = Environment.GetEnvironmentVariable("WHOWHOO_DIRECT_HOST");
        _searchUntil = Time.realtimeSinceStartup + 5;
        if (_probe) Application.targetFrameRate = 60;
        _role = Environment.GetEnvironmentVariable("WHOWHOO_LAN_ROLE");
        _directory = Environment.GetEnvironmentVariable("WHOWHOO_FRAMES");
        _game = FindAnyObjectByType<GameController>();
        _session = FindAnyObjectByType<LanMatchSession>();
        _deadline = Time.realtimeSinceStartup + 100;
        _game.WaveAnimator.StepDuration = 0.05f;
        _roomName = Environment.GetEnvironmentVariable("WHOWHOO_ROOM_NAME");
        if (_role == "host")
        {
            _session.Host("demo4x4", 2, _probe ? LanMatchSession.DefaultPort : 0, _roomName);
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
            if (_directAddress != null)
            {
                if (room != null)
                {
                    Debug.LogError("Room visible during the blocked-discovery test.");
                    Application.Quit(1);
                    return;
                }
                if (Time.realtimeSinceStartup >= _searchUntil) _session.Join(_directAddress);
                return;
            }
            if (room == null) return;
            if (!_probe && !_foundRoom)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "lan-room-browser.png"));
                _foundRoom = true;
                return;
            }
            _session.Join(room);
            return;
        }
        if (!_probe && _role == "host" && !_capturedLobby && _session.State == LanState.Lobby)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "lan-lobby.png"));
            _capturedLobby = true;
        }
        if (_session.State != LanState.Playing) return;
        if (_game.IsGameOver)
        {
            if (!_probe) ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "lan-" + _role + "-result.png"));
            Write(_role + ".json", JsonUtility.ToJson(MatchSnapshot.Capture(_game)));
            _finished = true;
            return;
        }
        if (!_game.AcceptsBoardInput) return;
        if (!_probe && _role == "guest" && !_capturedTurn)
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
