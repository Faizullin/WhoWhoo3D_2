using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Networking;
using static WhoWhoo3D.UI.HUD.HudControls;

namespace WhoWhoo3D.UI.HUD;

/// <summary>Setup, lobby and result views share the scene's controller and LAN session.</summary>
internal sealed class MatchScreens
{
    private enum ScreenKind { Hidden, Setup, Lobby, Result }
    private enum Mode { HotSeat, Solo, Host, Join }

    private readonly GameController _game;
    private readonly LanMatchSession _session;
    private readonly Canvas _canvas;
    private readonly RectTransform _safe, _panel, _setup, _options, _result;
    private readonly TextMeshProUGUI _title, _mapLabel, _mapName, _rulesLabel, _seatLabel, _status;
    private readonly TextMeshProUGUI[] _score = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] _charges = new TextMeshProUGUI[4];
    private readonly Control[] _modes = new Control[4], _rules = new Control[2], _seats = new Control[3];
    private readonly Control _previous, _next, _start, _replay, _exit;
    private readonly LanRoomPanel _lan;
    private string[] _maps = Array.Empty<string>();
    private int _map, _players;
    private bool _supply;
    private string _error;
    private ScreenKind _kind;
    private Mode _mode;

    public MatchScreens(Transform owner, GameController game, LanMatchSession session)
    {
        _game = game;
        _session = session;
        _canvas = CreateCanvas(owner, "Match screens", out _safe);
        _panel = Panel(_safe, "Match menu", Surface).rectTransform;
        _title = Label(_panel, "Heading", 26, White, true);
        _setup = Node(_panel, "Setup");
        string[] modes = { "Hot-seat", "Solo", "Host LAN", "Join LAN" };
        for (int i = 0; i < modes.Length; i++)
        {
            var mode = (Mode)i;
            _modes[i] = new Control(_setup, modes[i], () => SelectMode(mode));
        }
        _options = Node(_setup, "Match options");
        _mapLabel = Label(_options, "Map", 14, Soft);
        _mapLabel.text = "Map";
        _previous = new Control(_options, "Previous map", () => StepMap(-1));
        _previous.Caption.text = "‹";
        _mapName = Label(_options, "Selected map", 18, White, true);
        _mapName.alignment = TextAlignmentOptions.Center;
        _mapName.richText = false;
        _mapName.overflowMode = TextOverflowModes.Ellipsis;
        _next = new Control(_options, "Next map", () => StepMap(1));
        _next.Caption.text = "›";
        _rulesLabel = Label(_options, "Ability rules", 14, Soft);
        _rulesLabel.text = "Ability rules";
        _rules[0] = new Control(_options, "Fixed charges", () => _supply = false);
        _rules[1] = new Control(_options, "Supply after 4 claims", () => _supply = true);
        _seatLabel = Label(_options, "Seats", 14, Soft);
        for (int i = 0; i < _seats.Length; i++)
        {
            int count = i + GameController.MinPlayers;
            _seats[i] = new Control(_options, count + " players", () => _players = count);
            _seats[i].Caption.text = count.ToString();
        }
        _lan = new LanRoomPanel(_panel, _setup, _session, Run);
        _start = new Control(_setup, "Start match", StartMatch, true);
        _status = Label(_panel, "Status", 14, Soft);
        _status.richText = false;
        _status.textWrappingMode = TextWrappingModes.Normal;
        _result = Node(_panel, "Results");
        for (int i = 0; i < _score.Length; i++)
        {
            var row = Panel(_result, $"Seat {i + 1}", Tile);
            var stripe = Panel(row.transform, "Seat color", SlotView.ColorFor(new PlayerId(i + 1)));
            Place(stripe.rectTransform, 0, 0, 4, 40);
            _score[i] = Label(row.transform, "Final score", 16, White);
            _charges[i] = Label(row.transform, "Remaining charges", 15, Soft);
            _charges[i].alignment = TextAlignmentOptions.MidlineRight;
        }
        _replay = new Control(_result, "Play again", () =>
        {
            if (_session.IsHost) _session.Rematch();
            else _game.RestartMatch();
        }, true);
        _exit = new Control(_result, "Match setup", () =>
        {
            if (_session.State == LanState.Playing) _session.Leave();
            else _game.OpenSetup();
        });
        Hide();
    }

    public void Hide()
    {
        _kind = ScreenKind.Hidden;
        _canvas.gameObject.SetActive(false);
    }

    public bool Covers(Vector2 point) => _canvas.isActiveAndEnabled &&
        RectTransformUtility.RectangleContainsScreenPoint(_panel, point);

    public void Refresh(HudViewport viewport, Vector2 pixels, bool lobby)
    {
        var kind = lobby ? ScreenKind.Lobby : _game.InSetup ? ScreenKind.Setup : ScreenKind.Result;
        bool entering = kind != _kind;
        var previousKind = _kind;
        _kind = kind;
        _canvas.gameObject.SetActive(true);
        if (entering)
        {
            _error = null;
            if (kind == ScreenKind.Setup && (previousKind != ScreenKind.Lobby || _maps.Length == 0))
            {
                Run(() => _maps = MapLoader.ListMaps());
                _map = Mathf.Max(0, Array.IndexOf(_maps, _game.MapName));
                _players = _game.PlayerCount;
                _supply = _game.Setup.Supply;
            }
            if (kind == ScreenKind.Setup && (_mode == Mode.Join || _session.IsBrowsing)) SelectMode(Mode.Join);
        }
        if (kind == ScreenKind.Setup && _session.IsBrowsing && _mode != Mode.Join) SelectMode(Mode.Join);

        var size = Fit(_canvas, _safe, viewport, pixels);
        float w = Mathf.Min(size.x - 20, 500);
        float h = kind == ScreenKind.Result ? 146 + _game.PlayerCount * 46 :
            kind == ScreenKind.Lobby ? 275 : 386;
        Place(_panel, (size.x - w) / 2, (size.y - h) / 2, w, h);
        float inner = w - 32;
        Place(_title.rectTransform, 16, 10, inner, 36);
        _setup.gameObject.SetActive(kind == ScreenKind.Setup);
        _result.gameObject.SetActive(kind == ScreenKind.Result);
        _lan.Show(kind == ScreenKind.Setup && _mode == Mode.Host,
            kind == ScreenKind.Setup && _mode == Mode.Join, kind == ScreenKind.Lobby);
        if (kind == ScreenKind.Setup) DrawSetup(inner);
        else if (kind == ScreenKind.Lobby)
        {
            _title.text = "LAN room";
            _lan.LayoutLobby(inner);
            _status.text = _error ?? _session.Status ?? "";
            Place(_status.rectTransform, 16, 161, inner, 55);
        }
        else DrawResult(inner, h);
        if (entering && EventSystem.current != null)
        {
            if (kind == ScreenKind.Setup) _modes[(int)_mode].Button.Select();
            else if (kind == ScreenKind.Result) (_replay.Button.isActiveAndEnabled ? _replay : _exit).Button.Select();
            else _lan.FocusLobby();
        }
        Navigate(_canvas);
    }

    private void SelectMode(Mode mode)
    {
        _mode = mode;
        _error = null;
        if (mode == Mode.Join) _session.BrowseRooms();
        else _session.StopBrowsing();
    }

    private void StepMap(int step) => _map = (_map + step + _maps.Length) % _maps.Length;

    private void StartMatch()
    {
        Run(() =>
        {
            if (_mode == Mode.Host)
                _session.Host(_maps[_map], _players, roomName: _lan.RoomName, supply: _supply);
            else
            {
                var seats = new SeatControl[_players];
                if (_mode == Mode.Solo)
                    for (int i = 1; i < seats.Length; i++) seats[i] = SeatControl.Computer;
                _game.StartMatch(new MatchSetup(_maps[_map], _supply, seats));
            }
        });
    }

    private void Run(Action action)
    {
        try { _error = null; action(); }
        catch (Exception ex) { _error = ex.Message; }
    }

    private void DrawSetup(float inner)
    {
        _title.text = "Match setup";
        Place(_setup, 16, 53, inner, 323);
        Choices(_modes, (int)_mode, inner, 0);
        bool join = _mode == Mode.Join, host = _mode == Mode.Host;
        _options.gameObject.SetActive(!join);
        _start.Show(!join, _maps.Length > 0);
        if (join) _lan.LayoutBrowser(inner);
        else
        {
            Place(_options, 0, 44, inner, 177);
            Place(_mapLabel.rectTransform, 0, 0, inner, 18);
            Place(_previous.Rect, 0, 21, 44, 34);
            Place(_mapName.rectTransform, 49, 21, inner - 98, 34);
            Place(_next.Rect, inner - 44, 21, 44, 34);
            _previous.Show(true, _maps.Length > 1);
            _next.Show(true, _maps.Length > 1);
            _mapName.text = _maps.Length > 0 ? _maps[_map] : "No maps available";
            Place(_rulesLabel.rectTransform, 0, 61, inner, 18);
            Choices(_rules, _supply ? 1 : 0, inner, 82);
            Place(_seatLabel.rectTransform, 0, 122, inner, 18);
            _seatLabel.text = _mode == Mode.Solo
                ? $"Players   ·   P1 + {_players - 1} {(_players == 2 ? "computer" : "computers")}"
                : "Players";
            Choices(_seats, _players - GameController.MinPlayers, inner, 143);
            if (host) _lan.LayoutHost(inner);
            _start.Caption.text = host ? "Host room" : "Start match";
            Place(_start.Rect, 0, 283, inner, 38);
        }
        _status.text = _error ?? (host || join ? _session.Status : null) ??
            (host || join ? "" : "Right-drag to orbit  ·  Middle-drag to pan  ·  Scroll to zoom");
        Place(_status.rectTransform, 16, host || join ? 301 : 282, inner, host || join ? 34 : 50);
    }

    private static void Choices(Control[] choices, int selected, float width, float y)
    {
        float step = (width + 4) / choices.Length;
        for (int i = 0; i < choices.Length; i++)
        {
            Place(choices[i].Rect, i * step, y, step - 4, 34);
            choices[i].MarkSelected(i == selected);
        }
    }

    private void DrawResult(float inner, float height)
    {
        bool online = _session.State == LanState.Playing;
        _title.text = _game.Winner.IsNone ? "Draw" : $"{_game.Winner} wins";
        _status.text = "Final scores   ·   Remaining charges";
        Place(_status.rectTransform, 16, 48, inner, 22);
        Place(_result, 16, 77, inner, height - 77);
        for (int i = 0; i < _score.Length; i++)
        {
            var row = (RectTransform)_score[i].transform.parent;
            row.gameObject.SetActive(i < _game.PlayerCount);
            if (i >= _game.PlayerCount) continue;
            var player = _game.Roster.Players[i];
            var inv = player.Inventory;
            string cards = $"R{(inv.relay ? 1 : 0)}  F{(inv.freeze ? 1 : 0)}";
            if (_game.Setup.Supply) cards += $"  B{(inv.beacon ? 1 : 0)}";
            string state = _game.DisplayIsOut(player) ? "  (out)" : "";
            Place(row, 0, 46 * i, inner, 40);
            Place(_score[i].rectTransform, 12, 0, inner - 132, 40);
            Place(_charges[i].rectTransform, inner - 116, 0, 104, 40);
            _charges[i].text = cards;
            _score[i].text = $"<b>{player.Id}{state}</b>   {_game.DisplayScoreFor(player.Id)} tiles";
        }
        bool canReplay = !online || _session.IsHost;
        _replay.Show(canReplay, true);
        float half = (inner - 6) / 2;
        Place(_replay.Rect, 0, height - 131, half, 38);
        Place(_exit.Rect, canReplay ? half + 6 : 0, height - 131, canReplay ? half : inner, 38);
        _exit.Caption.text = online ? _session.IsHost ? "End room" : "Leave room" : "Match setup";
    }
}
