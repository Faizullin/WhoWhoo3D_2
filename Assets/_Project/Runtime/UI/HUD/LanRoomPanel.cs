using System;
using UnityEngine;
using WhoWhoo3D.Networking;

namespace WhoWhoo3D.UI.HUD;

internal sealed class LanRoomPanel
{
    private readonly LanMatchSession _session;
    private string _address = "";
    private string _roomName = "Game room";
    private Vector2 _scroll;
    private string _error;
    private enum Mode { HotSeat, Host, Join, Solo }
    private static readonly string[] ModeLabels = { "Hot-seat", "Host LAN", "Join LAN", "Solo" };
    private Mode _mode;
    public bool IsHosting => _mode == Mode.Host;
    public bool IsSolo => _mode == Mode.Solo;
    public bool IsJoining => _mode == Mode.Join;
    public bool InLobby => _session.State != LanState.Closed && _session.State != LanState.Playing;

    public LanRoomPanel(LanMatchSession session) => _session = session;

    public void DrawModes()
    {
        var current = _session.IsBrowsing ? Mode.Join : _mode;
        var selected = (Mode)GUILayout.Toolbar((int)current, ModeLabels, GUILayout.Height(28));
        if (selected != _mode) _error = null;
        _mode = selected;
        if (IsJoining) _session.BrowseRooms();
        else if (_session.IsBrowsing) _session.StopBrowsing();
    }

    public void DrawJoin()
    {
        GUILayout.Label("Nearby rooms");
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(56), GUILayout.ExpandHeight(true));
        if (_session.NearbyRooms.Count == 0)
            GUILayout.Label(_session.DiscoveryAvailable ? "Looking for open rooms…" : "Nearby search unavailable. Enter the host address below.");
        foreach (var room in _session.NearbyRooms)
        {
            string seats = room.OpenSeats == 1 ? "1 seat open" : $"{room.OpenSeats} seats open";
            if (GUILayout.Button($"{room.Name}\n{room.Map} · {seats}"))
            {
                Run(() => _session.Join(room));
                break;
            }
        }
        GUILayout.EndScrollView();
        GUILayout.Label("Join by IPv4 address");
        _address = GUILayout.TextField(_address, 45);
        DrawError();
        if (GUILayout.Button("Join by address", GUILayout.Height(35))) Run(() => _session.Join(_address));
    }

    public void DrawHost(string map, int players, bool supply)
    {
        GUILayout.Label("Room name");
        _roomName = GUILayout.TextField(_roomName, 32);
        DrawError();
        if (GUILayout.Button("Host room", GUILayout.Height(35))) Run(() => _session.Host(map, players, roomName: _roomName, supply: supply));
    }

    public void DrawLobby()
    {
        if (_session.IsHost)
        {
            GUILayout.Label(_session.RoomName);
            GUILayout.Label("Share this IPv4 address");
            GUILayout.TextField(_session.Address);
        }
        if (_session.LocalSeat > 0) GUILayout.Label($"Your seat · P{_session.LocalSeat}");
        GUILayout.Label(_session.Status);
        GUILayout.FlexibleSpace();
        if (_session.CanReconnect && GUILayout.Button("Reconnect", GUILayout.Height(32))) Run(_session.Reconnect);
        if (GUILayout.Button("Leave room", GUILayout.Height(32))) _session.Leave();
        DrawError();
    }

    public void DrawError()
    {
        string text = _error ?? _session.Status;
        if (!string.IsNullOrEmpty(text) && (_error != null || !InLobby)) GUILayout.Label(text);
    }

    private void Run(Action action)
    {
        try { _error = null; action(); }
        catch (Exception ex) { _error = ex.Message; }
    }
}
