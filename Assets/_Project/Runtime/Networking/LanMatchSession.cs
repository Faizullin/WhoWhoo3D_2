using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Gameplay.Turn;

namespace WhoWhoo3D.Networking
{
    public sealed class LanMatchSession : MonoBehaviour, IMatchConnection
    {
        public const int DefaultPort = 47771;
        internal const int ProtocolVersion = 1;

        public LanState State { get; private set; }
        public bool IsHost => _listener != null;
        public int LocalSeat { get; private set; }
        public int Port { get; private set; }
        public string Address { get; private set; }
        public string Status { get; private set; }
        public int Revision { get; private set; }
        public bool CanReconnect => State == LanState.Disconnected && _token != null;

        public bool IsBrowsing => _browser != null;
        public bool DiscoveryAvailable => _browser?.Available == true;
        public IReadOnlyList<LanRoomListing> NearbyRooms => _browser?.Rooms ?? Array.Empty<LanRoomListing>();
        public string RoomName { get; private set; }

        private LanRoomBrowser _browser;
        private LanRoomAdvertiser _advertiser;
        private GameController _view, _authority;
        private TcpListener _listener;
        private TcpClient _dial;
        private Task _connecting;
        private LanPeer _server;
        private readonly List<LanPeer> _peers = new();
        private readonly Queue<LanMessage> _deliveries = new();
        private string[] _tokens;
        private string _token, _digest, _expectedRoomId;
        private MatchSetup _setup;
        private int _round, _hostRevision;
        private bool _pending;
        private float _deadline, _nextPing;

        public void Bind(GameController view) => _view = view;

        public void BrowseRooms()
        {
            if (State == LanState.Closed) _browser ??= new LanRoomBrowser();
        }

        public void StopBrowsing()
        {
            _browser?.Dispose();
            _browser = null;
        }

        public void Host(string map, int players, int port = DefaultPort, string roomName = null)
        {
            if (State != LanState.Closed) throw new InvalidOperationException("Leave the current room first.");
            roomName = string.IsNullOrWhiteSpace(roomName) ? "Game room" : roomName.Trim();
            if (!RoomAnnouncement.ValidLabel(roomName, RoomAnnouncement.MaxNameLength))
                throw new ArgumentException("Enter a room name of up to 32 characters.");
            var setup = new MatchSetup(map, new SeatControl[players]);
            string digest = MapDigest(map);
            var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start(8);
                _listener = listener;
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Address = LocalAddresses();
                StopBrowsing();
                RoomName = roomName;
                _advertiser = new LanRoomAdvertiser(roomName, map, players, Port);
                _setup = setup;
                _digest = digest;
                _tokens = new string[players + 1];
                LocalSeat = 1;
                _view.Connection = this;
                UpdateLobby();
            }
            catch { listener.Stop(); Close(); throw; }
        }

        public void Join(LanRoomListing room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            Join(room.Address, room.Port, room.Id);
        }

        public void Join(string address, int port = DefaultPort) => Join(address, port, null);

        private void Join(string address, int port, string expectedRoomId)
        {
            if (State != LanState.Closed) throw new InvalidOperationException("Leave the current room first.");
            if (!IPAddress.TryParse(address?.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Enter the host’s IPv4 address.");
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            StopBrowsing();
            _expectedRoomId = expectedRoomId;
            Address = ip.ToString();
            Port = port;
            Dial();
        }

        public void Reconnect()
        {
            if (CanReconnect) Dial();
        }

        private void Dial()
        {
            _dial = new TcpClient(AddressFamily.InterNetwork);
            _connecting = _dial.ConnectAsync(IPAddress.Parse(Address), Port);
            _deadline = Time.realtimeSinceStartup + 8f;
            _view.Connection = this;
            SetState(LanState.Connecting, "Connecting to " + Address + "…");
        }

        public bool CanPlay(PlayerId active) => State == LanState.Playing && !_pending &&
            _deliveries.Count == 0 && active.Value == LocalSeat;

        public bool Submit(ClaimSlotCommand claim)
        {
            if (!CanPlay(claim.Actor)) return false;
            if (IsHost)
            {
                if (!Accept(LocalSeat, claim, _round)) return false;
                _view.ShowTurnHints(false);
                return true;
            }
            if (_server == null) return false;
            _server.Send(LanMessage.Move("claim", _round, 0, claim));
            _pending = true;
            _view.ShowTurnHints(false);
            return true;
        }

        private void Update()
        {
            if (State == LanState.Closed)
            {
                _browser?.Tick(Time.realtimeSinceStartup);
                return;
            }
            if (State == LanState.Disconnected) return;
            try
            {
                if (IsHost) PumpHost();
                else PumpClient();
                if (State == LanState.Playing && _deliveries.Count > 0 && _view.CurrentPhase is PlayerTurnPhase)
                {
                    var result = _deliveries.Dequeue();
                    if (result.sequence != Revision + 1 || !_view.TrySubmitClaim(result.Claim))
                        throw new IOException("Match state differed from the host. Reconnect to restore it.");
                    Revision = result.sequence;
                    _pending = false;
                    _view.ShowTurnHints(_view.AcceptsBoardInput);
                }
            }
            catch (Exception ex)
            {
                if (IsHost) Close(ex.Message);
                else LostServer(ex.Message);
            }
        }

        private void PumpHost()
        {
            if (_round == 0)
                _advertiser.Tick(Time.realtimeSinceStartup, _setup.Seats.Count - 1 - _peers.Count(p => p.Seat > 0));
            if (_listener.Pending())
            {
                var tcp = _listener.AcceptTcpClient();
                if (_peers.Count >= GameController.MaxPlayers + 4) tcp.Close();
                else _peers.Add(new LanPeer(tcp));
            }

            bool ping = PingDue();
            foreach (var peer in _peers.ToArray())
            {
                if (!IsHost) return;
                if (!_peers.Contains(peer)) continue;
                try
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var message = peer.Read();
                        if (message == null) break;
                        FromGuest(peer, message);
                        if (!IsHost) return;
                    }
                    CheckTimeout(peer);
                    if (ping) peer.Send(new LanMessage { kind = "ping" });
                    peer.Flush();
                }
                catch (Exception ex)
                {
                    try { peer.Send(new LanMessage { kind = "error", text = ex.Message }); peer.Flush(); }
                    catch (Exception) { /* The socket may already be closed. */ }
                    if (RemovePeer(peer)) UpdateLobby();
                }
            }
        }

        private void PumpClient()
        {
            if (_connecting != null)
            {
                if (Time.realtimeSinceStartup > _deadline) throw new IOException("Connection timed out.");
                if (!_connecting.IsCompleted) return;
                _connecting.GetAwaiter().GetResult();
                _connecting = null;
                _server = new LanPeer(_dial);
                _dial = null;
                _server.Send(new LanMessage { kind = "hello", version = ProtocolVersion, token = _token, roomId = _expectedRoomId });
            }
            if (_server == null) return;
            for (int i = 0; i < 16; i++)
            {
                var message = _server.Read();
                if (message == null) break;
                FromHost(message);
                if (_server == null) return;
            }
            CheckTimeout(_server);
            if (PingDue()) _server.Send(new LanMessage { kind = "ping" });
            _server.Flush();
        }

        private void FromGuest(LanPeer peer, LanMessage message)
        {
            if (message.kind == "ping") return;
            if (message.kind == "hello" && peer.Seat == 0)
            {
                if (message.version != ProtocolVersion) throw new IOException("Incompatible game version.");
                if (!string.IsNullOrEmpty(message.roomId) && message.roomId != _advertiser.Id)
                    throw new IOException("That room has closed. Choose a room again.");
                int seat = 0;
                for (int i = 2; i <= _setup.Seats.Count; i++)
                {
                    bool eligible = _round == 0 ? _tokens[i] == null :
                        !string.IsNullOrEmpty(message.token) && _tokens[i] == message.token;
                    if (eligible && !_peers.Any(p => p.Seat == i)) { seat = i; break; }
                }
                if (seat == 0) throw new IOException("Room full or seat unavailable.");
                peer.Seat = seat;
                _tokens[seat] ??= Guid.NewGuid().ToString("N");
                peer.Send(new LanMessage { kind = "welcome", seat = seat, token = _tokens[seat],
                    map = _setup.MapName, digest = _digest, players = _setup.Seats.Count });
                return;
            }
            if (peer.Seat == 0) throw new IOException("Join the room first.");
            switch (message.kind)
            {
                case "ready" when !peer.Ready:
                    peer.Ready = true;
                    UpdateLobby();
                    break;
                case "claim" when peer.Ready:
                    if (!Accept(peer.Seat, message.Claim, message.round))
                        peer.Send(new LanMessage { kind = "rejected" });
                    break;
                case "leave":
                    if (_round > 0 && peer.Ready) Close($"P{peer.Seat} left the match.");
                    else throw new IOException("Left room.");
                    break;
                default: throw new IOException("Unexpected room message.");
            }
        }

        private void FromHost(LanMessage message)
        {
            switch (message.kind)
            {
                case "ping": break;
                case "welcome" when State == LanState.Connecting:
                    if (message.players < 2 || message.players > 4 || message.seat < 2 || message.seat > message.players ||
                        string.IsNullOrEmpty(message.token) || MapDigest(message.map) != message.digest)
                        throw new IOException("The selected map or room settings differ. Use the same game and map.");
                    _setup = new MatchSetup(message.map, new SeatControl[message.players]);
                    LocalSeat = message.seat;
                    _token = message.token;
                    SetState(LanState.Lobby, "Waiting for players…");
                    _server.Send(new LanMessage { kind = "ready" });
                    break;
                case "wait" when _setup != null:
                    _pending = false;
                    SetState(LanState.Lobby, message.text);
                    break;
                case "snapshot" when _setup != null:
                    ApplySnapshot(message);
                    break;
                case "accepted" when State == LanState.Playing:
                    if (message.round != _round || message.sequence <= Revision) break;
                    if (_deliveries.Count >= 512 || message.sequence != Revision + _deliveries.Count + 1)
                        throw new IOException("Missing turn result.");
                    _deliveries.Enqueue(message);
                    break;
                case "rejected":
                    _pending = false;
                    _view.ShowTurnHints(_view.AcceptsBoardInput);
                    break;
                case "ended": Close(message.text); break;
                case "error": throw new IOException(message.text);
                default: throw new IOException("Unexpected host message.");
            }
        }

        private bool Accept(int seat, ClaimSlotCommand claim, int round)
        {
            if (State != LanState.Playing || round != _round || seat != claim.Actor.Value ||
                !_authority.TrySubmitClaim(claim)) return false;
            var result = LanMessage.Move("accepted", _round, ++_hostRevision, claim);
            _deliveries.Enqueue(result);
            Broadcast(result);
            return true;
        }

        private void UpdateLobby()
        {
            int present = 1 + _peers.Count(p => p.Ready);
            if (present < _setup.Seats.Count)
            {
                string text = $"Waiting for players ({present}/{_setup.Seats.Count})";
                SetState(LanState.Lobby, text);
                Broadcast(new LanMessage { kind = "wait", text = text });
                return;
            }
            if (_round == 0) StartRound();
            else SyncRoom();
        }

        private void StartRound()
        {
            if (_authority == null)
            {
                var obj = new GameObject("LAN match authority");
                obj.transform.SetParent(transform, false);
                _authority = obj.AddComponent<GameController>();
                _authority.enabled = false;
            }
            _authority.StartMatchWithoutPresentation(_setup);
            _round++;
            _hostRevision = 0;
            SyncRoom();
        }

        public void Rematch()
        {
            if (IsHost && State == LanState.Playing && _authority.IsGameOver && _view.IsGameOver) StartRound();
        }

        private void SyncRoom()
        {
            var snapshot = new LanMessage { kind = "snapshot", round = _round, sequence = _hostRevision,
                snapshot = MatchSnapshot.Capture(_authority) };
            ApplySnapshot(snapshot);
            Broadcast(snapshot);
        }

        private void ApplySnapshot(LanMessage message)
        {
            _round = message.round;
            Revision = message.sequence;
            _pending = false;
            _deliveries.Clear();
            SetState(LanState.Playing, null);
            _view.RestoreMatch(_setup, message.snapshot);
        }

        private void Broadcast(LanMessage message)
        {
            bool lostSeat = false;
            foreach (var peer in _peers.ToArray())
            {
                if (!peer.Ready) continue;
                try { peer.Send(message); }
                catch (IOException)
                {
                    lostSeat |= RemovePeer(peer);
                }
            }
            if (lostSeat) UpdateLobby();
        }

        private bool RemovePeer(LanPeer peer)
        {
            if (!_peers.Remove(peer)) return false;
            peer.Dispose();
            if (peer.Seat == 0) return false;
            if (_round == 0) _tokens[peer.Seat] = null;
            return peer.Ready;
        }

        private void LostServer(string reason)
        {
            CloseClientSocket();
            _pending = false;
            _deliveries.Clear();
            SetState(LanState.Disconnected, reason);
        }

        public void Leave()
        {
            if (_server != null)
            {
                try { _server.Send(new LanMessage { kind = "leave" }); _server.Flush(); }
                catch (Exception) { /* Continue local cleanup if the host is gone. */ }
            }
            Close();
        }

        private void Close(string reason = null)
        {
            foreach (var peer in _peers)
            {
                try { peer.Send(new LanMessage { kind = "ended", text = reason ?? "Host closed the room." }); peer.Flush(); }
                catch (Exception) { /* Release every peer even when one has disconnected. */ }
                peer.Dispose();
            }
            _peers.Clear();
            StopBrowsing();
            _advertiser?.Dispose();
            _advertiser = null;
            RoomName = null;
            _listener?.Stop();
            _listener = null;
            CloseClientSocket();
            if (_authority != null) Destroy(_authority.gameObject);
            _authority = null;
            _deliveries.Clear();
            _setup = null;
            _tokens = null;
            _token = _expectedRoomId = null;
            _pending = false;
            LocalSeat = _round = Revision = 0;
            if (_view != null && ReferenceEquals(_view.Connection, this))
            {
                _view.Connection = null;
                _view.OpenSetup();
            }
            SetState(LanState.Closed, reason);
        }

        private void CloseClientSocket()
        {
            _server?.Dispose();
            _server = null;
            _dial?.Close();
            _dial = null;
            // Observe a pending connection failure even when the user cancels.
            _connecting?.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            _connecting = null;
        }

        private void OnDestroy() => Leave();

        private void SetState(LanState value, string status)
        {
            State = value;
            Status = status;
            if (_view != null) _view.ShowTurnHints(_view.AcceptsBoardInput);
        }

        private bool PingDue()
        {
            if (Time.realtimeSinceStartup < _nextPing) return false;
            _nextPing = Time.realtimeSinceStartup + 3;
            return true;
        }

        private static void CheckTimeout(LanPeer peer)
        {
            if (Time.realtimeSinceStartup - peer.LastReceived > 15f) throw new IOException("Connection timed out.");
        }

        private static string MapDigest(string map)
        {
            if (!MapLoader.ListMaps().Contains(map, StringComparer.Ordinal))
                throw new IOException("The host’s map is not installed.");
            using var sha = SHA256.Create();
            var text = File.ReadAllText(MapLoader.PathFor(map)).Replace("\r\n", "\n");
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        private static string LocalAddresses()
        {
            var addresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address).Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString()).Distinct().ToArray();
            return addresses.Length > 0 ? string.Join(" / ", addresses) : "127.0.0.1";
        }
    }
}
