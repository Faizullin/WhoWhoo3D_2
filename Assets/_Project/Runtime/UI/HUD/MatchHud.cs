using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Abilities;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;
using WhoWhoo3D.Networking;
using static WhoWhoo3D.UI.HUD.HudControls;

namespace WhoWhoo3D.UI.HUD;

internal sealed class MatchHud
{
    private static readonly MatchAction[] Cards = { MatchAction.Relay, MatchAction.Freeze, MatchAction.Beacon };
    private readonly GameController _game;
    private readonly LanMatchSession _session;
    private readonly Canvas _canvas;
    private readonly RectTransform _safe, _header, _tray;
    private readonly TextMeshProUGUI _turn, _status, _title, _detail;
    private readonly Image[] _seats = new Image[4];
    private readonly TextMeshProUGUI[] _scores = new TextMeshProUGUI[4];
    private readonly Control[] _cards = new Control[3], _refill = new Control[3];
    private readonly Control _supply, _confirm, _cancel, _leave;
    private readonly StringBuilder _effects = new();
    private bool _collecting;
    private int _lastTurn;
    private BoardModel _lastBoard;

    public MatchHud(Transform owner, GameController game, LanMatchSession session)
    {
        _game = game;
        _session = session;
        var root = Node(owner, "Match HUD");
        _canvas = root.gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 12;
        root.gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            var events = new GameObject("HUD input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(owner, false);
        }

        _safe = Node(root, "Safe area");
        _safe.pivot = new Vector2(.5f, .5f);
        _header = Panel(_safe, "Turn and scores", Surface).rectTransform;
        _turn = Label(_header, "Turn", 12, Mint, true);
        _turn.characterSpacing = 6;
        _status = Label(_header, "Active seat", 19, White, true);
        for (int i = 0; i < _seats.Length; i++)
        {
            _seats[i] = Panel(_header, $"Seat {i + 1}", Tile);
            var stripe = Panel(_seats[i].transform, "Seat color", SlotView.ColorFor(new PlayerId(i + 1)));
            stripe.rectTransform.anchorMin = Vector2.zero;
            stripe.rectTransform.anchorMax = new Vector2(0, 1);
            stripe.rectTransform.offsetMin = Vector2.zero;
            stripe.rectTransform.offsetMax = new Vector2(4, 0);
            _scores[i] = Label(_seats[i].transform, "Score and charges", 15, White);
        }
        _leave = new Control(_header, "End room", () => _session.Leave());
        _tray = Panel(_safe, "Ability tray", Surface).rectTransform;
        _title = Label(_tray, "Inventory", 16, Mint, true);
        _detail = Label(_tray, "Instruction", 14, White);
        _detail.textWrappingMode = TextWrappingModes.Normal;
        for (int i = 0; i < Cards.Length; i++)
        {
            var card = Cards[i];
            _cards[i] = new Control(_tray, card.ToString(), () => _game.SelectAbility(card));
            _refill[i] = new Control(_tray, "Refill " + card, () =>
            {
                if (_game.TakeSupply(card)) _collecting = false;
            });
        }
        _supply = new Control(_tray, "Collect supply", () => _collecting = true, true);
        _confirm = new Control(_tray, "Confirm", () => _game.ConfirmAbility(), true);
        _cancel = new Control(_tray, "Cancel", Cancel);
        root.gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (_canvas.gameObject.activeSelf == visible) return;
        _canvas.gameObject.SetActive(visible);
        if (!visible) _collecting = false;
    }

    public bool Covers(Vector2 point) => _canvas.isActiveAndEnabled &&
        (RectTransformUtility.RectangleContainsScreenPoint(_header, point) ||
         (_tray.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_tray, point)));

    private void Cancel()
    {
        _collecting = false;
        _game.SelectAbility(MatchAction.Claim);
    }

    public void Refresh(float uiScale, bool solo, bool timelineOpen)
    {
        if (_lastTurn != _game.TurnNumber || _lastBoard != _game.Board) _collecting = false;
        _lastTurn = _game.TurnNumber;
        _lastBoard = _game.Board;
        var area = Screen.safeArea;
        var viewport = new HudViewport(area, Screen.height, uiScale);
        _canvas.scaleFactor = viewport.Scale;
        _safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        _safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        float width = Mathf.Min(viewport.Size.x - 20, 796);
        float left = (viewport.Size.x - width) / 2;
        DrawHeader(left, width);
        _tray.gameObject.SetActive(!timelineOpen);
        if (timelineOpen) return;
        DrawTray(left, width, viewport.Size.y, solo);
        Navigate();
    }

    private void DrawHeader(float left, float width)
    {
        bool online = _session.State == LanState.Playing;
        bool narrow = width < 610;
        int count = _game.Roster.Players.Count;
        float y = narrow ? 40 : 7;
        float x = narrow ? 8 : 155;
        float end = online && !narrow ? 107 : 8;
        float seatWidth = (width - x - end) / count;
        Place(_header, left, 8, width, y + 49);
        Place(_turn.rectTransform, 14, narrow ? 4 : 7, 135, 16);
        Place(_status.rectTransform, narrow ? 112 : 14, narrow ? 4 : 23, narrow ? 140 : 137, 26);
        _turn.text = $"TURN  {_game.TurnNumber:00}";
        var active = _game.ActivePlayer;
        _status.text = _game.IsReviewingHistory ? "Reviewing" : _game.IsPaused ? "Paused" :
            _game.CurrentPhase is BoardWavePhase ? "Wave in motion" : $"{active.Id} to play";
        for (int i = 0; i < _seats.Length; i++)
        {
            _seats[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            var seat = _game.Roster.Players[i];
            Place(_seats[i].rectTransform, x + seatWidth * i, y, seatWidth - 4, 42);
            Place(_scores[i].rectTransform, 9, 1, seatWidth - 14, 40);
            bool outOfPlay = _game.DisplayIsOut(seat);
            string score = outOfPlay ? "out" : _game.DisplayScoreFor(seat.Id).ToString();
            string label = $"{seat.Id}   <b>{score}</b>";
            var inv = seat.Inventory;
            string stash = $"R{(inv.relay ? 1 : 0)}  F{(inv.freeze ? 1 : 0)}";
            if (_game.Setup.Supply) stash += $"  B{(inv.beacon ? 1 : 0)}";
            _scores[i].text = label + "\n<size=12><color=#C5D2D8>" + stash + "</color></size>";
            _seats[i].color = seat == active ? new Color32(52, 77, 80, 255) : Tile;
        }
        _leave.Show(online, true);
        if (online)
        {
            Place(_leave.Rect, width - 104, 8, 96, narrow ? 27 : 40);
            _leave.Caption.text = _session.IsHost ? "End room" : "Leave room";
        }
    }

    private void DrawTray(float left, float width, float screenHeight, bool solo)
    {
        var seat = _session.LocalSeat > 0 ? _game.Roster.Players[_session.LocalSeat - 1] :
            solo ? _game.Roster.Players[0] : _game.ActivePlayer;
        bool canPlay = seat == _game.ActivePlayer && _game.AcceptsBoardInput;
        bool supplyMode = _game.Setup.Supply;
        bool supplyReady = supplyMode && seat.Inventory.SupplyReady && canPlay;
        if (!supplyReady) _collecting = false;
        var action = _game.SelectedAction;
        bool aiming = action != MatchAction.Claim;
        if ((aiming || _collecting) && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            Cancel();
            action = MatchAction.Claim;
            aiming = false;
        }
        int cardCount = supplyMode ? 3 : 2;
        bool narrow = width < 610;
        _effects.Clear();
        foreach (var player in _game.Roster.Players)
        {
            var inv = player.Inventory;
            if (!string.IsNullOrEmpty(inv.frozenCell)) _effects.Append($"   •   Ice ends T{inv.thawAfterTurn}");
            if (inv.HasBeacon) _effects.Append($"   •   {player.Id} Beacon pending");
        }
        _title.text = aiming ? $"{action}  /  {seat.Id}" : _collecting ? "Choose a free charge" : $"{seat.Id} abilities";
        string prompt;
        if (aiming) prompt = TargetPrompt(action);
        else if (_collecting) prompt = "Refill one empty card. Your action stays available.";
        else if (!canPlay) prompt = _game.CurrentPhase is BoardWavePhase ? "Resolving the wave" : $"Waiting for {_game.ActivePlayer.Id}";
        else if (supplyMode) prompt = seat.Inventory.supplyUsed ? "Supply collected" : supplyReady ? "Supply ready" :
            $"Supply  {seat.Inventory.ordinaryClaims} / {AbilityInventory.SupplyThreshold} claims";
        else prompt = "Choose a mint tile or an ability.";
        _detail.text = prompt + _effects;
        float controlWidth = narrow ? width - 20 : aiming ? 214 : _collecting || supplyReady ? 375 : cardCount * 106;
        float copyWidth = narrow ? width - 28 : width - controlWidth - 36;
        float detailHeight = Mathf.Max(20, Mathf.Ceil(_detail.GetPreferredValues(_detail.text, copyWidth, 1000).y) + 3);
        float height = Mathf.Max(66, 34 + detailHeight) + (narrow ? 40 : 0);
        Place(_tray, left, screenHeight - height - 8, width, height);
        Place(_title.rectTransform, 14, 6, copyWidth, 21);
        Place(_detail.rectTransform, 14, 27, copyWidth, detailHeight);
        float x = narrow ? 10 : width - controlWidth - 10;
        float y = narrow ? height - 42 : (height - 38) / 2;
        int controls = aiming ? 2 : cardCount + (_collecting || supplyReady ? 1 : 0);
        float step = controlWidth / controls;
        for (int i = 0; i < Cards.Length; i++)
        {
            var card = Cards[i];
            bool showCard = !aiming && !_collecting && i < cardCount;
            bool charged = seat.Inventory.Has(card);
            _cards[i].Show(showCard, canPlay && charged);
            _cards[i].Caption.text = $"{card}  ×{(charged ? 1 : 0)}";
            _refill[i].Show(_collecting, canPlay && _game.Abilities.CanRefill(card));
            _refill[i].Caption.text = "+ " + card;
            Place(_cards[i].Rect, x + i * step, y, step - 4, 38);
            Place(_refill[i].Rect, x + i * step, y, step - 4, 38);
        }
        _confirm.Show(aiming, _game.CanConfirm);
        _cancel.Show(aiming || _collecting, true);
        _supply.Show(supplyReady && !aiming && !_collecting, canPlay);
        Place(_cancel.Rect, x + (_collecting ? cardCount : 0) * step, y, step - 4, 38);
        Place(_confirm.Rect, x + step, y, step - 4, 38);
        Place(_supply.Rect, x + cardCount * step, y, step - 4, 38);
        _confirm.Caption.text = "Confirm";
        _supply.Caption.text = "Collect";
    }

    private string TargetPrompt(MatchAction action)
    {
        if (_game.RenderedBoard != null && !_game.RenderedBoard.HasHighlightedTargets)
            return "No legal target. Cancel to choose another action.";
        bool picked = _game.Target.HasValue;
        switch (action)
        {
            case MatchAction.Freeze:
                if (!picked) return "Pick a tile on the cube to freeze.";
                var cube = _game.Board.GetCell(_game.Target.Value.Side.Cell);
                return $"Cube {cube.Position} · thaws after {_game.Abilities.NextOpponent}’s turn. Waves pass through.";
            case MatchAction.Beacon:
                return _game.SecondTarget.HasValue ? "Both marks must stay open until your next turn." :
                    picked ? "Pick another white tile on this face." : "Pick two white tiles on one face.";
            default:
                return picked ? "Your boxed tile empties. The two marked tiles gain your color." :
                    "Pick a gold-rimmed tile to move across its edge.";
        }
    }

    private void Navigate()
    {
        var keys = Keyboard.current;
        if (keys == null || !keys.tabKey.wasPressedThisFrame || EventSystem.current == null) return;
        var list = _canvas.GetComponentsInChildren<Button>();
        var selected = EventSystem.current.currentSelectedGameObject;
        int index = System.Array.FindIndex(list, b => b.gameObject == selected);
        int direction = keys.shiftKey.isPressed ? -1 : 1;
        if (index < 0 && direction < 0) index = 0;
        for (int i = 0; i < list.Length; i++)
        {
            index = (index + direction + list.Length) % list.Length;
            if (list[index].IsInteractable()) { list[index].Select(); break; }
        }
    }
}
