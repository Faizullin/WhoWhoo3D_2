using System;
using TMPro;
using UnityEngine;
using WhoWhoo3D.Networking;
using static WhoWhoo3D.UI.HUD.HudControls;

namespace WhoWhoo3D.UI.HUD;

/// <summary>Editable host/join fields, paged discovery and the waiting room.</summary>
internal sealed class LanRoomPanel
{
    private readonly LanMatchSession _session;
    private readonly RectTransform _host, _browser, _lobby;
    private readonly TMP_InputField _name, _address, _sharedAddress;
    private readonly TextMeshProUGUI _hostLabel, _nearby, _empty, _pageLabel, _directLabel, _room, _share, _seat;
    private readonly Control[] _rows = new Control[2];
    private readonly LanRoomListing[] _displayed = new LanRoomListing[2];
    private readonly Control _previous, _next, _join, _reconnect, _leave;
    private int _page;
    public string RoomName => _name.text;

    public LanRoomPanel(RectTransform panel, RectTransform setup, LanMatchSession session, Action<Action> run)
    {
        _session = session;
        _host = Node(setup, "Host options");
        _hostLabel = Label(_host, "Room name label", 14, Soft);
        _hostLabel.text = "Room name";
        _name = Field(_host, "Room name", 32);
        _name.SetTextWithoutNotify("Game room");

        _browser = Node(setup, "Room browser");
        _nearby = Label(_browser, "Nearby rooms", 16, Mint, true);
        _nearby.text = "Nearby rooms";
        _empty = Label(_browser, "Search status", 15, Soft);
        _empty.textWrappingMode = TextWrappingModes.Normal;
        for (int i = 0; i < _rows.Length; i++)
        {
            int row = i;
            _rows[i] = new Control(_browser, $"Join listing {i + 1}", () => run(() => _session.Join(_displayed[row])));
            var text = _rows[i].Caption;
            text.richText = false;
            text.fontSize = 14;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(8, 0, 8, 0);
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        _previous = new Control(_browser, "Previous rooms", () => _page--);
        _previous.Caption.text = "Previous";
        _pageLabel = Label(_browser, "Page", 14, Soft);
        _pageLabel.alignment = TextAlignmentOptions.Center;
        _next = new Control(_browser, "Next rooms", () => _page++);
        _next.Caption.text = "Next";
        _directLabel = Label(_browser, "Address label", 14, Soft);
        _directLabel.text = "Host IPv4 address";
        _address = Field(_browser, "Host address", 45);
        _join = new Control(_browser, "Join by address", () => run(() => _session.Join(_address.text)), true);

        _lobby = Node(panel, "Lobby");
        _room = Label(_lobby, "Room name", 19, Mint, true);
        _room.richText = false;
        _room.overflowMode = TextOverflowModes.Ellipsis;
        _share = Label(_lobby, "Share address", 14, Soft);
        _share.text = "Share this IPv4 address";
        _sharedAddress = Field(_lobby, "Shared address", 0);
        _sharedAddress.readOnly = true;
        _seat = Label(_lobby, "Local seat", 16, White);
        _reconnect = new Control(_lobby, "Reconnect", () => run(_session.Reconnect), true);
        _leave = new Control(_lobby, "Leave room", _session.Leave);
    }

    public void Show(bool host, bool browser, bool lobby)
    {
        if (!browser) _page = 0;
        _host.gameObject.SetActive(host);
        _browser.gameObject.SetActive(browser);
        _lobby.gameObject.SetActive(lobby);
    }

    public void LayoutHost(float width)
    {
        Place(_host, 0, 224, width, 30);
        Place(_hostLabel.rectTransform, 0, 0, 85, 30);
        Place((RectTransform)_name.transform, 88, 0, width - 88, 30);
    }

    public void LayoutBrowser(float width)
    {
        Place(_browser, 0, 42, width, 279);
        Place(_nearby.rectTransform, 0, 0, width, 22);
        var rooms = _session.NearbyRooms;
        int pages = Mathf.Max(1, Mathf.CeilToInt((float)rooms.Count / _rows.Length));
        _page = Mathf.Clamp(_page, 0, pages - 1);
        _empty.gameObject.SetActive(rooms.Count == 0);
        _empty.text = _session.DiscoveryAvailable ? "Looking for open rooms…" : "Nearby search unavailable. Enter the host address below.";
        Place(_empty.rectTransform, 0, 25, width, 78);
        for (int i = 0; i < _rows.Length; i++)
        {
            int index = _page * _rows.Length + i;
            var listing = index < rooms.Count ? rooms[index] : null;
            _displayed[i] = listing;
            _rows[i].Show(listing != null, true);
            if (listing == null) continue;
            _rows[i].Caption.text = $"{listing.Name}\n{listing.Map}  ·  {listing.OpenSeats} {(listing.OpenSeats == 1 ? "seat" : "seats")} open";
            Place(_rows[i].Rect, 0, 25 + i * 44, width, 40);
        }
        _previous.Show(pages > 1, _page > 0);
        _next.Show(pages > 1, _page < pages - 1);
        _pageLabel.gameObject.SetActive(pages > 1);
        _pageLabel.text = $"{_page + 1} / {pages}";
        Place(_previous.Rect, 0, 114, 86, 29);
        Place(_pageLabel.rectTransform, 90, 114, width - 180, 29);
        Place(_next.Rect, width - 86, 114, 86, 29);
        Place(_directLabel.rectTransform, 0, 149, width, 18);
        Place((RectTransform)_address.transform, 0, 171, width, 34);
        Place(_join.Rect, 0, 241, width, 38);
    }

    public void FocusLobby()
    {
        if (_session.CanReconnect) _reconnect.Button.Select();
        else if (_session.IsHost) _sharedAddress.Select();
        else _leave.Button.Select();
    }

    public void LayoutLobby(float width)
    {
        Place(_lobby, 16, 52, width, 208);
        _room.text = _session.RoomName ?? "";
        _seat.text = _session.LocalSeat > 0 ? $"Your seat   ·   P{_session.LocalSeat}" : "";
        _share.gameObject.SetActive(_session.IsHost);
        _sharedAddress.gameObject.SetActive(_session.IsHost);
        if (_session.IsHost && _sharedAddress.text != _session.Address)
            _sharedAddress.SetTextWithoutNotify(_session.Address);
        Place(_room.rectTransform, 0, 0, width, 23);
        Place(_share.rectTransform, 0, 27, width, 19);
        Place((RectTransform)_sharedAddress.transform, 0, 50, width, 31);
        Place(_seat.rectTransform, 0, _session.IsHost ? 83 : 26, width, 23);
        bool retry = _session.CanReconnect;
        _reconnect.Show(retry, true);
        float half = (width - 6) / 2;
        Place(_reconnect.Rect, 0, 171, half, 38);
        Place(_leave.Rect, retry ? half + 6 : 0, 171, retry ? half : width, 38);
    }
}
