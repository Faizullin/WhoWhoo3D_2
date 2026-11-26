using System;
using UnityEngine;
using WhoWhoo3D.Networking;

namespace WhoWhoo3D.UI.HUD;

internal sealed class LanRoomPanel
{
    private readonly LanMatchSession _session;
    private string _address = "";
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
        var selected = (Mode)GUILayout.Toolbar((int)_mode, ModeLabels, GUILayout.Height(28));
        if (selected != _mode) _error = null;
        _mode = selected;
    }

    public void DrawJoin()
    {
        GUILayout.Label("Host IPv4 address");
        _address = GUILayout.TextField(_address, 45);
        GUILayout.FlexibleSpace();
        DrawError();
        if (GUILayout.Button("Join room", GUILayout.Height(35))) Run(() => _session.Join(_address));
    }

    public void DrawHost(string map, int players)
    {
        DrawError();
        if (GUILayout.Button("Host room", GUILayout.Height(35))) Run(() => _session.Host(map, players));
    }

    public void DrawLobby()
    {
        if (_session.IsHost)
        {
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
