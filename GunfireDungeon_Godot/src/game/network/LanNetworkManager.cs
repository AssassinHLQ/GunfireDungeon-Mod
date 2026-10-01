using DsUi;
using Config;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Godot;

/// <summary>
/// 局域网联机管理器。
///
/// 这是一个轻量的 LAN 协作层: 使用 Godot 内置 ENet 建立连接,
/// 房主负责同步大厅/地牢状态, 每个客户端同步自己的玩家位置和朝向。
/// 游戏原有的敌人、掉落和伤害系统仍由各实例本地运行, 不会改变单机逻辑。
/// </summary>
public partial class LanNetworkManager : Node
{
    public const int DefaultPort = 24567;
    private const int DiscoveryPort = 24568;
    public const int MaxPlayers = 4;
    private const string DiscoveryRequest = "GFDISCOVER1";
    private const string DiscoveryResponse = "GFDROOM1";

    public static LanNetworkManager Instance { get; private set; }

    public bool IsHost { get; private set; }
    public bool IsLanConnected => _peer != null && Multiplayer.MultiplayerPeer != null &&
                                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    public long LocalPeerId => IsLanConnected ? Multiplayer.GetUniqueId() : 1;
    public string LastStatus { get; private set; } = "未连接";

    /// <summary>状态文字发生变化。</summary>
    public event Action<string> StatusChanged;

    /// <summary>本机已经完成大厅/地牢加载。</summary>
    public event Action<string> LocalWorldReady;

    /// <summary>自动搜索结束。address 为空表示没有找到房间。</summary>
    public event Action<string> HostSearchCompleted;

    private ENetMultiplayerPeer _peer;
    private UdpClient _discoveryListener;
    private UdpClient _discoveryClient;
    private double _hostSearchRemaining;
    private readonly Dictionary<long, RemotePlayerState> _remotePlayers = new();
    private double _snapshotTimer;
    private double _worldSnapshotTimer;
    private ulong _lastSnapshotSentAt;
    private World _lastLocalWorld;
    private string _lastLocalWorldKind = string.Empty;
    private string _lastBroadcastStateKey = string.Empty;
    private string _lastRequestedStateKey = string.Empty;
    private int _sessionRevision;
    private bool _applyingRemoteState;
    private Variant[] _pendingRemoteState;
    private long _nextDynamicNetworkId = 1;

    private sealed class RemotePlayerState
    {
        public long PeerId;
        public Vector2 TargetPosition;
        public FaceDirection Face;
        public float AimRotationDegrees;
        public Player Player;
        public bool IsMoving;
        public bool IsRolling;
        public int RoomId = -1;
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
        Multiplayer.ConnectionFailed -= OnConnectionFailed;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Process(double delta)
    {
        PollHostDiscovery();
        PollHostSearch(delta);

        if (!IsLanConnected)
        {
            return;
        }

        if (GameApplication.Instance == null || GameApplication.Instance.DungeonManager == null)
        {
            return;
        }

        TrackLocalWorld();
        if (IsHost)
        {
            // 不只监听 world 类型, 还要检测楼层/种子变化。
            BroadcastSessionState();

            _worldSnapshotTimer -= delta;
            if (_worldSnapshotTimer <= 0)
            {
                _worldSnapshotTimer = 0.05;
                BroadcastEnemySnapshots();
                BroadcastPlayerVitals();
                CheckRemoteRoomWaves();
            }
        }
        UpdateRemotePlayers((float)delta);

        _snapshotTimer -= delta;
        if (_snapshotTimer <= 0)
        {
            _snapshotTimer = 0.05;
            SendLocalPlayerSnapshot();
        }
    }

    /// <summary>
    /// 开一个局域网房间。其他电脑连接本机局域网 IP 和同一个端口即可。
    /// </summary>
    public bool StartHost(int port = DefaultPort)
    {
        Disconnect();

        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, MaxPlayers - 1);
        if (error != Error.Ok)
        {
            SetStatus($"创建房间失败: {error}");
            return false;
        }

        _peer = peer;
        Multiplayer.MultiplayerPeer = _peer;
        IsHost = true;
        _sessionRevision = 0;
        StartHostDiscovery();
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _pendingRemoteState = null;
        _lastSnapshotSentAt = 0;
        _worldSnapshotTimer = 0;
        SetStatus($"房主已启动，端口 {port}。本机 IP: {GetLanAddressText()}");
        return true;
    }

    /// <summary>
    /// 连接到房主。address 可以是 192.168.x.x、局域网主机名或 localhost。
    /// </summary>
    public bool JoinHost(string address, int port = DefaultPort)
    {
        Disconnect();

        address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(address, port);
        if (error != Error.Ok)
        {
            SetStatus($"连接失败: {error}");
            return false;
        }

        _peer = peer;
        Multiplayer.MultiplayerPeer = _peer;
        IsHost = false;
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _pendingRemoteState = null;
        _lastSnapshotSentAt = 0;
        _worldSnapshotTimer = 0;
        SetStatus($"正在连接 {address}:{port}...");
        return true;
    }

    public void Disconnect()
    {
        StopHostSearch();
        StopHostDiscovery();
        ClearRemotePlayers();
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _pendingRemoteState = null;
        _applyingRemoteState = false;
        _lastSnapshotSentAt = 0;
        _worldSnapshotTimer = 0;

        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }

        _peer = null;
        IsHost = false;
        SetStatus("未连接");
    }

    /// <summary>
    /// 房主从大厅进入地牢。客户端不应自己触发入口，避免各自生成不同地图。
    /// </summary>
    public void HostStartDungeon(DungeonConfig config)
    {
        if (!IsHost || !IsLanConnected || config == null)
        {
            return;
        }

        var check = DungeonManager.CheckDungeon(config.GroupName);
        if (check.HasError)
        {
            EditorWindowManager.ShowTips("警告", "当前组'" + config.GroupName + "'" + check.ErrorMessage + "，不能生成地牢!");
            return;
        }

        // 固定本层种子，让所有客户端生成相同的房间布局。
        config.RandomSeed ??= MakeDungeonSeed();

        var dungeonManager = GameApplication.Instance.DungeonManager;
        dungeonManager.ResetFloor();
        UiManager.Open_Game_Loading();
        dungeonManager.ExitHall(true, () =>
        {
            dungeonManager.LoadDungeon(config, () => UiManager.Destroy_Game_Loading());
        });
    }

    /// <summary>
    /// 每次房主准备加载下一层时可使用的新种子。
    /// 目前保留为公开 API，方便后续把更多楼层/房间操作纳入主机权威同步。
    /// </summary>
    public int MakeDungeonSeed()
    {
        return unchecked((int)GD.Randi());
    }

    public long AllocateDynamicNetworkId()
    {
        return 0x7000000000000000L | _nextDynamicNetworkId++;
    }

    public void ForcePartyIntoRoom(int roomId, Vector2 entrantPosition, long entrantPeerId)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager == null || !dungeonManager.ForcePartyIntoRoom(roomId, entrantPosition))
        {
            return;
        }

        foreach (var state in _remotePlayers.Values)
        {
            if (state.PeerId == entrantPeerId)
            {
                state.RoomId = roomId;
                state.TargetPosition = entrantPosition;
                dungeonManager.OnRemotePlayerEnterRoom(roomId, state.Player);
                continue;
            }

            state.RoomId = roomId;
            state.TargetPosition = entrantPosition;
            if (state.Player != null && GodotObject.IsInstanceValid(state.Player))
            {
                state.Player.PutDown(entrantPosition, RoomLayerEnum.YSortLayer, false);
                dungeonManager.OnRemotePlayerEnterRoom(roomId, state.Player);
            }

            RpcId(state.PeerId, nameof(ReceiveForceRoomEntry), roomId, entrantPosition.X, entrantPosition.Y);
        }
    }

    public void RequestSharedPickup(ActivityObject item)
    {
        if (item == null || item.IsDestroyed || item.NetworkId == 0)
        {
            return;
        }

        if (IsHost)
        {
            ResolveSharedPickup(LocalPeerId, item.NetworkId);
        }
        else
        {
            RpcId(1, nameof(ReceiveSharedPickupRequest), item.NetworkId);
        }
    }

    public void BroadcastNetworkPickupSpawn(ActivityObject item)
    {
        if (!IsHost || !IsLanConnected || item == null || item.IsDestroyed)
        {
            return;
        }

        item.NetworkId = AllocateDynamicNetworkId();

        Rpc(nameof(ReceiveNetworkPickupSpawn), item.NetworkId, item.ActivityBase?.Id ?? string.Empty,
            item.GlobalPosition.X, item.GlobalPosition.Y);
    }

    public void RequestGoldPickup(Gold gold)
    {
        if (!IsLanConnected || gold == null || gold.IsDestroyed || gold.NetworkId == 0)
        {
            return;
        }

        if (IsHost)
        {
            ResolveGoldPickup(LocalPeerId, gold.NetworkId);
        }
        else
        {
            RpcId(1, nameof(ReceiveGoldPickupRequest), gold.NetworkId);
        }
    }

    public void BroadcastNetworkGoldSpawn(Gold gold)
    {
        if (!IsHost || !IsLanConnected || gold == null)
        {
            return;
        }

        gold.NetworkId = AllocateDynamicNetworkId();
        Rpc(nameof(ReceiveNetworkGoldSpawn), gold.NetworkId, gold.ActivityBase?.Id ?? string.Empty,
            gold.GoldCount, gold.GlobalPosition.X, gold.GlobalPosition.Y);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkGoldSpawn(long networkId, string activityId, int count, float x, float y)
    {
        if (IsHost || networkId == 0 || string.IsNullOrEmpty(activityId) || FindActivityObject(networkId) != null)
        {
            return;
        }

        var gold = ObjectManager.GetActivityObject<Gold>(activityId);
        gold.NetworkId = networkId;
        gold.GoldCount = count;
        gold.InitNetworkDrop(new Vector2(x, y));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGoldPickupRequest(long networkId)
    {
        if (IsHost)
        {
            ResolveGoldPickup(Multiplayer.GetRemoteSenderId(), networkId);
        }
    }

    private void ResolveGoldPickup(long peerId, long networkId)
    {
        var gold = FindActivityObject(networkId) as Gold;
        var player = peerId == LocalPeerId
            ? GameApplication.Instance?.DungeonManager?.CurrWorld?.Player
            : _remotePlayers.TryGetValue(peerId, out var state) ? state.Player : null;
        if (!IsHost || gold == null)
        {
            return;
        }

        if (player == null || player.IsDestroyed || gold.GlobalPosition.DistanceTo(player.GlobalPosition) > 24f)
        {
            if (peerId == LocalPeerId)
            {
                gold.ResetNetworkClaimPending();
            }
            else if (peerId > 1)
            {
                RpcId(peerId, nameof(ReceiveGoldPickupResult), networkId, -1L, 0);
            }
            return;
        }

        player.AddGold(gold.GoldCount);
        var count = gold.GoldCount;
        gold.ReclaimNetworkDrop();
        Rpc(nameof(ReceiveGoldPickupResult), networkId, peerId, count);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGoldPickupResult(long networkId, long winnerPeerId, int count)
    {
        if (!IsHost)
        {
            ApplyGoldPickupResult(networkId, winnerPeerId, count);
        }
    }

    private void ApplyGoldPickupResult(long networkId, long winnerPeerId, int count)
    {
        var gold = FindActivityObject(networkId) as Gold;
        if (winnerPeerId < 0)
        {
            gold?.ResetNetworkClaimPending();
            return;
        }

        if (winnerPeerId == LocalPeerId)
        {
            GameApplication.Instance?.DungeonManager?.CurrWorld?.Player?.AddGold(count);
        }

        gold?.ReclaimNetworkDrop();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkPickupSpawn(long networkId, string activityId, float x, float y)
    {
        if (IsHost || networkId == 0 || string.IsNullOrEmpty(activityId) || FindActivityObject(networkId) != null)
        {
            return;
        }

        var item = ActivityObject.Create(activityId);
        if (item == null)
        {
            return;
        }

        item.NetworkId = networkId;
        item.PutDown(new Vector2(x, y), RoomLayerEnum.YSortLayer, false);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedPickupRequest(long networkId)
    {
        if (!IsHost)
        {
            return;
        }

        ResolveSharedPickup(Multiplayer.GetRemoteSenderId(), networkId);
    }

    private void ResolveSharedPickup(long peerId, long networkId)
    {
        if (!IsHost || !IsLanConnected || networkId == 0)
        {
            return;
        }

        var item = FindActivityObject(networkId);
        var player = peerId == LocalPeerId
            ? GameApplication.Instance?.DungeonManager?.CurrWorld?.Player
            : _remotePlayers.TryGetValue(peerId, out var state) ? state.Player : null;
        if (item == null || item.IsDestroyed || player == null || player.IsDestroyed ||
            item.GlobalPosition.DistanceTo(player.GlobalPosition) > 56f ||
            !item.CheckInteractive(player).CanInteractive)
        {
            return;
        }

        item.Interactive(player);
        if (peerId == LocalPeerId)
        {
            Rpc(nameof(ReceiveSharedPickupResult), networkId, peerId);
            ApplySharedPickupResult(networkId, peerId);
        }
        else
        {
            Rpc(nameof(ReceiveSharedPickupResult), networkId, peerId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedPickupResult(long networkId, long winnerPeerId)
    {
        if (!IsHost)
        {
            ApplySharedPickupResult(networkId, winnerPeerId);
        }
    }

    private void ApplySharedPickupResult(long networkId, long winnerPeerId)
    {
        var item = FindActivityObject(networkId);
        if (item == null || item.IsDestroyed)
        {
            return;
        }

        if (winnerPeerId == LocalPeerId && GameApplication.Instance?.DungeonManager?.CurrWorld?.Player is Role player &&
            item.CheckInteractive(player).CanInteractive)
        {
            item.Interactive(player);
        }
        else
        {
            item.Destroy();
        }
    }

    private ActivityObject FindActivityObject(long networkId)
    {
        var root = GetTree()?.Root;
        if (root == null)
        {
            return null;
        }

        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is ActivityObject activity && activity.NetworkId == networkId && !activity.IsDestroyed)
            {
                return activity;
            }

            foreach (var child in node.GetChildren())
            {
                pending.Push((Node)child);
            }
        }

        return null;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveForceRoomEntry(int roomId, float x, float y)
    {
        if (IsHost)
        {
            return;
        }

        GameApplication.Instance?.DungeonManager?.ForceLocalPlayerIntoRoom(
            roomId, new Vector2(x, y));
    }

    /// <summary>确保相同地图参数的重开也会作为新会话同步给客户端。</summary>
    public void MarkSessionRestarted()
    {
        if (IsHost && IsLanConnected)
        {
            ClearRemotePlayers();
            _lastBroadcastStateKey = string.Empty;
            _sessionRevision++;
        }
    }

    /// <summary>向局域网广播搜索请求，并在短时间内等待房主回应。</summary>
    public bool FindHostOnLan()
    {
        StopHostSearch();
        try
        {
            _discoveryClient = new UdpClient(0);
            _discoveryClient.EnableBroadcast = true;
            var payload = Encoding.ASCII.GetBytes(DiscoveryRequest);
            _discoveryClient.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            _hostSearchRemaining = 2.0;
            return true;
        }
        catch (SocketException)
        {
            StopHostSearch();
            return false;
        }
    }

    private void StartHostDiscovery()
    {
        StopHostDiscovery();
        try
        {
            _discoveryListener = new UdpClient(DiscoveryPort);
        }
        catch (SocketException)
        {
            _discoveryListener = null;
        }
    }

    private void PollHostDiscovery()
    {
        if (!IsHost || _discoveryListener == null)
        {
            return;
        }

        try
        {
            while (_discoveryListener.Available > 0)
            {
                IPEndPoint sender = new(IPAddress.Any, 0);
                var request = _discoveryListener.Receive(ref sender);
                if (Encoding.ASCII.GetString(request) != DiscoveryRequest)
                {
                    continue;
                }

                var response = Encoding.ASCII.GetBytes(DiscoveryResponse);
                _discoveryListener.Send(response, response.Length, sender);
            }
        }
        catch (SocketException)
        {
            StopHostDiscovery();
        }
        catch (ObjectDisposedException)
        {
            StopHostDiscovery();
        }
    }

    private void PollHostSearch(double delta)
    {
        if (_discoveryClient == null)
        {
            return;
        }

        try
        {
            if (_discoveryClient.Available > 0)
            {
                IPEndPoint sender = new(IPAddress.Any, 0);
                var response = _discoveryClient.Receive(ref sender);
                if (Encoding.ASCII.GetString(response) == DiscoveryResponse)
                {
                    var address = sender.Address.ToString();
                    StopHostSearch();
                    HostSearchCompleted?.Invoke(address);
                    return;
                }
            }
        }
        catch (SocketException)
        {
            StopHostSearch();
            HostSearchCompleted?.Invoke(string.Empty);
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _hostSearchRemaining -= delta;
        if (_hostSearchRemaining <= 0)
        {
            StopHostSearch();
            HostSearchCompleted?.Invoke(string.Empty);
        }
    }

    private void StopHostSearch()
    {
        _hostSearchRemaining = 0;
        _discoveryClient?.Close();
        _discoveryClient = null;
    }

    private void StopHostDiscovery()
    {
        _discoveryListener?.Close();
        _discoveryListener = null;
    }

    private static string GetLanAddressText()
    {
        var addresses = new List<string>();
        foreach (var address in IP.GetLocalAddresses())
        {
            if (address.Contains(".") && !address.StartsWith("127."))
            {
                addresses.Add(address);
            }
        }

        return addresses.Count == 0 ? "请运行 ipconfig 查看 IPv4" : string.Join(", ", addresses);
    }
    private void OnPeerConnected(long peerId)
    {
        if (IsHost)
        {
            // PeerConnected 回调发生在连接建立的边界时刻, 延后一帧再发状态,
            // 避免对端的 RPC 节点还没有完成注册。
            CallDeferred(nameof(SendSessionStateToPeerDeferred), peerId);
            SetStatus($"玩家 {peerId} 已加入");
        }
    }

    private void OnPeerDisconnected(long peerId)
    {
        RemoveRemotePlayer(peerId);
        SetStatus($"玩家 {peerId} 已离开");
    }

    private void OnConnectedToServer()
    {
        SetStatus("已连接房主，等待房主开始游戏...");
        _lastRequestedStateKey = string.Empty;
        // 连接信号触发时网络对象刚切换完成, 延迟请求一次初始状态更稳。
        CallDeferred(nameof(RequestSessionStateDeferred));
    }

    private void OnConnectionFailed()
    {
        SetStatus("连接房主失败，请检查 IP、端口和防火墙");
        DisconnectPeerOnly();
    }

    private void OnServerDisconnected()
    {
        SetStatus("房主已断开连接");
        DisconnectPeerOnly();
    }

    private void DisconnectPeerOnly()
    {
        ClearRemotePlayers();
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }

        _peer = null;
        IsHost = false;
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _pendingRemoteState = null;
        _applyingRemoteState = false;
        _lastSnapshotSentAt = 0;
    }

    private void RequestSessionStateDeferred()
    {
        if (!IsHost && IsLanConnected)
        {
            RpcId(1, nameof(RequestSessionState));
        }
    }

    private void SendSessionStateToPeerDeferred(long peerId)
    {
        SendSessionStateToPeer(peerId);
    }

    private void SetStatus(string status)
    {
        LastStatus = status ?? string.Empty;
        StatusChanged?.Invoke(LastStatus);
    }

    private void TrackLocalWorld()
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        var worldKind = GetWorldKind();
        if (world == _lastLocalWorld && worldKind == _lastLocalWorldKind)
        {
            return;
        }

        _lastLocalWorld = world;
        _lastLocalWorldKind = worldKind;
        ClearRemotePlayers();

        if (!string.IsNullOrEmpty(worldKind))
        {
            LocalWorldReady?.Invoke(worldKind);
        }

        if (IsHost && !string.IsNullOrEmpty(worldKind))
        {
            BroadcastSessionState();
        }
    }
    private string GetWorldKind()
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (world is Hall)
        {
            return "hall";
        }

        if (world is Dungeon)
        {
            return "dungeon";
        }

        return string.Empty;
    }

    private void SendLocalPlayerSnapshot()
    {
        var player = GameApplication.Instance?.DungeonManager?.CurrWorld?.Player as Player;
        if (player == null || player.IsDestroyed || string.IsNullOrEmpty(_lastLocalWorldKind))
        {
            return;
        }

        var now = Time.GetTicksMsec();
        var elapsed = _lastSnapshotSentAt == 0 ? 0.05f : Mathf.Max(0.001f, (float)(now - _lastSnapshotSentAt) / 1000f);
        _lastSnapshotSentAt = now;
        var args = new Variant[]
        {
            LocalPeerId,
            player.GlobalPosition.X,
            player.GlobalPosition.Y,
            (int)player.Face,
            player.MountPoint?.RealRotationDegrees ?? 0f,
            elapsed,
            player.StateController?.CurrState == PlayerStateEnum.Roll,
            player.AffiliationArea?.RoomInfo?.Id ?? -1,
        };
        Rpc(nameof(ReceivePlayerSnapshot), args);
    }

    private void BroadcastEnemySnapshots()
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (!IsHost || world == null)
        {
            return;
        }

        foreach (var role in world.Role_InstanceList)
        {
            if (role == null || role.IsDestroyed || !role.IsAi || role.NetworkId == 0 ||
                role.AnimatedSprite == null)
            {
                continue;
            }

            Rpc(nameof(ReceiveEnemySnapshot), new Variant[]
            {
                role.NetworkId,
                role.ActivityBase?.Id ?? string.Empty,
                role.GlobalPosition.X,
                role.GlobalPosition.Y,
                (int)role.Face,
                role.Hp,
                role.MaxHp,
                role.RoleState.Gold,
                role.IsDie,
                role.AnimatedSprite.Animation.ToString(),
                role.AnimatedSprite.Frame,
            });
        }
    }

    private void BroadcastPlayerVitals()
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (!IsHost || world?.Player is not Player hostPlayer)
        {
            return;
        }

        BroadcastVitalsForPeer(1, hostPlayer);
        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player != null && GodotObject.IsInstanceValid(state.Player))
            {
                BroadcastVitalsForPeer(state.PeerId, state.Player);
            }
        }
    }

    private void CheckRemoteRoomWaves()
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (!IsHost || dungeonManager == null)
        {
            return;
        }

        foreach (var state in _remotePlayers.Values)
        {
            if (state.RoomId >= 0)
            {
                dungeonManager.CheckRemoteRoomWave(state.RoomId);
            }
        }
    }

    private void BroadcastVitalsForPeer(long peerId, Player player)
    {
        Rpc(nameof(ReceivePlayerVitals), peerId, player.Hp, player.Shield, player.Armor);
    }

    public void RequestEnemyDamage(long networkId, List<AttackStats> damages,
        List<AbnormalData> abnormals, float angle)
    {
        if (!IsLanConnected || IsHost || networkId == 0)
        {
            return;
        }

        var damagePacket = new Godot.Collections.Array<Variant>();
        if (damages != null)
        {
            foreach (var damage in damages)
            {
                damagePacket.Add(damage.BaseDamage);
                damagePacket.Add((int)damage.Type);
                damagePacket.Add(damage.CritRate);
                damagePacket.Add(damage.CritBonus);
                damagePacket.Add(damage.CritArmorPenetration);
            }
        }

        var abnormalPacket = new Godot.Collections.Array<Variant>();
        if (abnormals != null)
        {
            foreach (var abnormal in abnormals)
            {
                abnormalPacket.Add((int)abnormal.Type);
                abnormalPacket.Add(abnormal.Value);
            }
        }

        RpcId(1, nameof(ReceiveEnemyDamageRequest), networkId, damagePacket, abnormalPacket, angle);
    }

    public void OnPlayerBulletFired(Role shooter, IBullet bullet)
    {
        var localPlayer = GameApplication.Instance?.DungeonManager?.CurrWorld?.Player;
        if (!IsLanConnected || shooter != localPlayer || bullet?.BulletData?.BulletBase == null ||
            bullet.BulletData.BulletBase.Type != 1)
        {
            return;
        }

        var data = bullet.BulletData;
        if (IsHost)
        {
            Rpc(nameof(ReceivePlayerBulletVisual), LocalPeerId, data.BulletBase.Id,
                data.Position.X, data.Position.Y, data.Rotation, data.Altitude, data.FlySpeed,
                data.VerticalSpeed, data.MaxDistance, data.LifeTime);
        }
        else
        {
            RpcId(1, nameof(RequestPlayerBulletVisual), data.BulletBase.Id,
                data.Position.X, data.Position.Y, data.Rotation, data.Altitude, data.FlySpeed,
                data.VerticalSpeed, data.MaxDistance, data.LifeTime);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestPlayerBulletVisual(string bulletId, float x, float y, float rotation,
        float altitude, float flySpeed, float verticalSpeed, float maxDistance, float lifeTime)
    {
        if (!IsHost || !ExcelConfig.BulletBase_Map.TryGetValue(bulletId, out var bulletBase) || bulletBase.Type != 1)
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId <= 1 || !_remotePlayers.TryGetValue(senderId, out var state) ||
            state.Player == null || !GodotObject.IsInstanceValid(state.Player))
        {
            return;
        }

        SpawnNetworkVisualBullet(senderId, bulletBase, new Vector2(x, y), rotation,
            altitude, flySpeed, verticalSpeed, maxDistance, lifeTime);
        Rpc(nameof(ReceivePlayerBulletVisual), senderId, bulletId, x, y, rotation,
            altitude, flySpeed, verticalSpeed, maxDistance, lifeTime);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceivePlayerBulletVisual(long sourcePeerId, string bulletId, float x, float y,
        float rotation, float altitude, float flySpeed, float verticalSpeed, float maxDistance, float lifeTime)
    {
        if (IsHost || sourcePeerId == LocalPeerId ||
            !ExcelConfig.BulletBase_Map.TryGetValue(bulletId, out var bulletBase) || bulletBase.Type != 1)
        {
            return;
        }

        SpawnNetworkVisualBullet(sourcePeerId, bulletBase, new Vector2(x, y), rotation,
            altitude, flySpeed, verticalSpeed, maxDistance, lifeTime);
    }

    private void SpawnNetworkVisualBullet(long sourcePeerId, ExcelConfig.BulletBase bulletBase,
        Vector2 position, float rotation, float altitude, float flySpeed, float verticalSpeed,
        float maxDistance, float lifeTime)
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        Role shooter = null;
        if (sourcePeerId == LocalPeerId)
        {
            shooter = world?.Player;
        }
        else
        {
            if (!_remotePlayers.TryGetValue(sourcePeerId, out var state))
            {
                state = new RemotePlayerState { PeerId = sourcePeerId };
                _remotePlayers.Add(sourcePeerId, state);
            }

            EnsureRemotePlayer(state);
            shooter = state.Player;
        }

        if (world == null || shooter == null || shooter.IsDestroyed || string.IsNullOrEmpty(bulletBase.Prefab))
        {
            return;
        }

        var visualBullet = ObjectManager.GetBullet(bulletBase.Prefab);
        visualBullet.NetworkVisualOnly = true;
        var data = new BulletData(world)
        {
            BulletBase = bulletBase,
            TriggerRole = shooter,
            Damages = new List<AttackStats>(),
            Abnormals = new List<AbnormalData>(),
            Position = position,
            Rotation = rotation,
            Altitude = altitude,
            FlySpeed = flySpeed,
            VerticalSpeed = verticalSpeed,
            MaxDistance = maxDistance,
            LifeTime = lifeTime,
        };
        visualBullet.InitData(data, shooter.Camp);
    }

    private void UpdateRemotePlayers(float delta)
    {
        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player == null || !GodotObject.IsInstanceValid(state.Player))
            {
                state.Player = null;
                continue;
            }

            var blend = Mathf.Clamp(delta * 18f, 0f, 1f);
            state.Player.GlobalPosition = state.Player.GlobalPosition.Lerp(state.TargetPosition, blend);
            state.Player.Face = state.Face;
            state.Player.MountPoint?.ApplyNetworkRotation(state.AimRotationDegrees);

            if (state.Player.AnimatedSprite != null)
            {
                var animation = state.IsRolling
                    ? AnimatorNames.Roll
                    : state.IsMoving ? AnimatorNames.Run : AnimatorNames.Idle;
                if (state.Player.AnimatedSprite.Animation != animation)
                {
                    state.Player.AnimatedSprite.Play(animation);
                }
            }
        }
    }

    private void EnsureRemotePlayer(RemotePlayerState state)
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (world == null || world.Player == null || state.Player != null)
        {
            return;
        }

        var remote = ActivityObject.Create<Player>(ActivityObject.Ids.Id_role0001);
        if (remote == null)
        {
            return;
        }

        remote.Name = $"RemotePlayer_{state.PeerId}";
        remote.World = world;
        remote.Camp = world.Player.Camp;
        remote.PutDown(state.TargetPosition, RoomLayerEnum.YSortLayer, false);
        remote.Collision.Disabled = true;
        remote.EnableBehavior = false;
        remote.EnableCustomBehavior = false;
        remote.Modulate = new Color(0.65f, 0.9f, 1f, 1f);
        remote.Face = state.Face;
        state.Player = remote;
    }

    private void ClearRemotePlayers()
    {
        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player != null && GodotObject.IsInstanceValid(state.Player))
            {
                state.Player.Destroy();
            }
        }

        _remotePlayers.Clear();
    }

    private void RemoveRemotePlayer(long peerId)
    {
        if (!_remotePlayers.TryGetValue(peerId, out var state))
        {
            return;
        }

        if (state.Player != null && GodotObject.IsInstanceValid(state.Player))
        {
            state.Player.Destroy();
        }

        _remotePlayers.Remove(peerId);
    }

    private void BroadcastSessionState()
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        var state = GetSessionState();
        var key = MakeStateKey(state);
        if (key == _lastBroadcastStateKey)
        {
            return;
        }

        _lastBroadcastStateKey = key;
        Rpc(nameof(ReceiveSessionState), state);
    }

    private void SendSessionStateToPeer(long peerId)
    {
        if (!IsHost || !IsLanConnected || peerId <= 1)
        {
            return;
        }

        RpcId(peerId, nameof(ReceiveSessionState), GetSessionState());
    }

    private Variant[] GetSessionState()
    {
        var kind = GetWorldKind();
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var config = dungeonManager?.CurrConfig;
        var floor = dungeonManager?.CurrentFloor ?? 1;
        var mode = config == null ? (int)DungeonMode.Normal : (int)config.Mode;
        var groupName = config?.GroupName ?? GameApplication.Instance?.FirstDungeonConfig?.GroupName ?? string.Empty;
        var seed = config?.RandomSeed ?? 0;
        var hasSeed = config?.RandomSeed != null;

        return new Variant[]
        {
            kind,
            groupName,
            floor,
            mode,
            seed,
            hasSeed,
            _sessionRevision,
        };
    }

    private static string MakeStateKey(Variant[] state)
    {
        return $"{state[0]}|{state[1]}|{state[2]}|{state[3]}|{state[4]}|{state[5]}|{state[6]}";
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSessionState()
    {
        if (!IsHost)
        {
            return;
        }

        var peerId = Multiplayer.GetRemoteSenderId();
        if (peerId > 1)
        {
            SendSessionStateToPeer(peerId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSessionState(string worldKind, string groupName, int floor, int modeValue, int seed,
        bool hasSeed, int sessionRevision)
    {
        if (IsHost)
        {
            return;
        }

        var state = new Variant[]
        {
            worldKind,
            groupName,
            floor,
            modeValue,
            seed,
            hasSeed,
            sessionRevision,
        };
        var key = MakeStateKey(state);
        if (key == _lastRequestedStateKey && _pendingRemoteState == null)
        {
            return;
        }

        // 加载场景期间只保留房主最新状态, 当前加载完成后再补应用,
        // 避免房主快速切层时客户端卡在旧楼层。
        _pendingRemoteState = state;
        if (!_applyingRemoteState)
        {
            ApplyPendingRemoteState();
        }
    }

    private void ApplyPendingRemoteState()
    {
        if (IsHost || _applyingRemoteState || _pendingRemoteState == null)
        {
            return;
        }

        var state = _pendingRemoteState;
        _pendingRemoteState = null;

        var worldKind = (string)state[0];
        var groupName = (string)state[1];
        var floor = (int)state[2];
        var modeValue = (int)state[3];
        var seed = (int)state[4];
        var hasSeed = (bool)state[5];
        var key = MakeStateKey(state);
        if (key == _lastRequestedStateKey)
        {
            return;
        }

        _lastRequestedStateKey = key;
        _applyingRemoteState = true;

        if (worldKind == "hall")
        {
            ApplyRemoteHall();
        }
        else if (worldKind == "dungeon")
        {
            ApplyRemoteDungeon(groupName, floor, modeValue, seed, hasSeed);
        }
        else
        {
            _applyingRemoteState = false;
            CallDeferred(nameof(ApplyPendingRemoteState));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void ReceivePlayerSnapshot(long peerId, float x, float y, int faceValue, float aimRotationDegrees, float elapsed,
        bool isRolling, int roomId)
    {
        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId > 1)
        {
            peerId = senderId;
        }

        if (peerId <= 0 || peerId == LocalPeerId)
        {
            return;
        }

        if (!_remotePlayers.TryGetValue(peerId, out var state))
        {
            state = new RemotePlayerState { PeerId = peerId };
            _remotePlayers.Add(peerId, state);
        }

        var newPosition = new Vector2(x, y);
        state.IsMoving = state.TargetPosition.DistanceTo(newPosition) > Mathf.Max(0.25f, 4f * elapsed);
        state.IsRolling = isRolling;
        state.TargetPosition = newPosition;
        state.Face = (FaceDirection)Mathf.Clamp(faceValue, (int)FaceDirection.Left, (int)FaceDirection.Right);
        state.AimRotationDegrees = aimRotationDegrees;
        EnsureRemotePlayer(state);

        if (IsHost && senderId > 1 && roomId >= 0 && roomId != state.RoomId)
        {
            GameApplication.Instance?.DungeonManager?.OnRemotePlayerEnterRoom(roomId, state.Player);
            ForcePartyIntoRoom(roomId, newPosition, senderId);
        }
        else if (IsHost && senderId > 1 && roomId < 0 && state.RoomId >= 0)
        {
            GameApplication.Instance?.DungeonManager?.OnRemotePlayerLeaveRoom(state.Player);
        }
        state.RoomId = roomId;

        // ENet 的快照先发到房主, 再由房主转发给其他客户端。
        if (IsHost && senderId > 1)
        {
            Rpc(nameof(ReceivePlayerSnapshot), new Variant[]
            {
                peerId, x, y, faceValue, aimRotationDegrees, elapsed, isRolling, roomId,
            });
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void ReceiveEnemySnapshot(long networkId, string activityId, float x, float y, int faceValue, int hp,
        int maxHp, int gold, bool isDead, string animation, int frame)
    {
        if (IsHost)
        {
            return;
        }

        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        var role = world?.Role_InstanceList.Find(item => item.NetworkId == networkId);
        if ((role == null || role.IsDestroyed) && !string.IsNullOrEmpty(activityId))
        {
            role = ActivityObject.Create<Role>(activityId);
            if (role != null)
            {
                role.NetworkId = networkId;
                role.EnableCustomBehavior = false;
                role.PutDown(new Vector2(x, y), RoomLayerEnum.YSortLayer, false);
            }
        }

        if (role == null || role.IsDestroyed)
        {
            return;
        }

        role.GlobalPosition = new Vector2(x, y);
        role.Face = (FaceDirection)Mathf.Clamp(faceValue, (int)FaceDirection.Left, (int)FaceDirection.Right);
        role.ApplyReplicatedEnemyState(hp, maxHp, gold, isDead, animation, frame);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveEnemyDamageRequest(long networkId, Godot.Collections.Array<Variant> damagePacket,
        Godot.Collections.Array<Variant> abnormalPacket, float angle)
    {
        if (!IsHost || damagePacket == null || abnormalPacket == null)
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId <= 1 || !_remotePlayers.TryGetValue(senderId, out var playerState) ||
            playerState.Player == null || !GodotObject.IsInstanceValid(playerState.Player))
        {
            return;
        }

        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        var target = world?.Role_InstanceList.Find(item => item.NetworkId == networkId);
        if (target == null || target.IsDestroyed || !target.IsAi || target.IsDie || target.HurtArea == null ||
            !target.HurtArea.CanHurt(playerState.Player.Camp))
        {
            return;
        }

        var damages = new List<AttackStats>();
        for (var i = 0; i + 4 < damagePacket.Count; i += 5)
        {
            damages.Add(new AttackStats(
                (int)damagePacket[i],
                (DamageType)(int)damagePacket[i + 1],
                (float)damagePacket[i + 2],
                (float)damagePacket[i + 3],
                (float)damagePacket[i + 4]));
        }

        var abnormals = new List<AbnormalData>();
        for (var i = 0; i + 1 < abnormalPacket.Count; i += 2)
        {
            abnormals.Add(new AbnormalData(
                (AbnormalStateType)(int)abnormalPacket[i],
                (int)abnormalPacket[i + 1]));
        }

        target.HurtArea.Hurt(playerState.Player, damages, abnormals, angle);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void ReceivePlayerVitals(long peerId, int hp, int shield, int armor)
    {
        if (IsHost || peerId != LocalPeerId)
        {
            return;
        }

        GameApplication.Instance?.DungeonManager?.CurrWorld?.Player?.ApplyNetworkVitals(hp, shield, armor);
    }

    private void ApplyRemoteHall()
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager == null)
        {
            _applyingRemoteState = false;
            return;
        }

        UiManager.Open_Game_Loading();
        Action loadHall = () => dungeonManager.LoadHall(() =>
        {
            _applyingRemoteState = false;
            UiManager.Destroy_Game_Loading();
            CallDeferred(nameof(ApplyPendingRemoteState));
        });

        if (dungeonManager.CurrWorld is Hall)
        {
            _applyingRemoteState = false;
            UiManager.Destroy_Game_Loading();
            CallDeferred(nameof(ApplyPendingRemoteState));
            return;
        }

        if (dungeonManager.CurrWorld is Dungeon)
        {
            dungeonManager.ExitDungeon(false, loadHall);
        }
        else
        {
            loadHall();
        }
    }

    private void ApplyRemoteDungeon(string groupName, int floor, int modeValue, int seed, bool hasSeed)
    {
        var app = GameApplication.Instance;
        var dungeonManager = app?.DungeonManager;
        if (app == null || dungeonManager == null)
        {
            _applyingRemoteState = false;
            return;
        }

        if (string.IsNullOrEmpty(groupName))
        {
            groupName = app.FirstDungeonConfig?.GroupName ?? string.Empty;
        }

        SyncPlanFloor(dungeonManager, floor);
        var config = app.GetDungeonConfig(groupName, Mathf.Max(1, floor), (DungeonMode)modeValue);
        if (hasSeed)
        {
            config.RandomSeed = seed;
        }

        UiManager.Open_Game_Loading();
        UiManager.Destroy_Game_Settlement();
        UiManager.Destroy_Game_PauseMenu();
        Action loadDungeon = () => dungeonManager.LoadDungeon(config, () =>
        {
            _applyingRemoteState = false;
            UiManager.Destroy_Game_Loading();
            CallDeferred(nameof(ApplyPendingRemoteState));
        });

        if (dungeonManager.CurrWorld is Hall)
        {
            dungeonManager.ExitHall(true, loadDungeon);
        }
        else if (dungeonManager.CurrWorld is Dungeon)
        {
            // 房主切换楼层时, 客户端重建自己的本地世界。
            dungeonManager.ExitDungeon(false, loadDungeon);
        }
        else
        {
            loadDungeon();
        }
    }

    private static void SyncPlanFloor(DungeonManager dungeonManager, int floor)
    {
        if (floor <= 1)
        {
            dungeonManager.ResetFloor();
            return;
        }

        dungeonManager.ResetFloor();
        for (var i = 1; i < floor; i++)
        {
            if (!dungeonManager.Plan.MoveNext())
            {
                break;
            }
        }
    }
}
