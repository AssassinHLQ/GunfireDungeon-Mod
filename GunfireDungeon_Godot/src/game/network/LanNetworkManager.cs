using DsUi;
using Config;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public const int DungeonFloorSeedStep = 104729;
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

    /// <summary>
    /// 当前客户端是否已经进入与房主不同的楼层。
    /// 不同楼层时由本机生成敌人和处理房间，避免房主切层把访客拉回去。
    /// </summary>
    public bool IsLocalDungeonAuthority
    {
        get
        {
            if (!IsLanConnected || IsHost)
            {
                return true;
            }

            var dungeonManager = GameApplication.Instance?.DungeonManager;
            if (dungeonManager?.CurrWorld is not Dungeon)
            {
                return false;
            }

            if (_knownHostFloor > 0)
            {
                return _knownHostFloor != dungeonManager.CurrentFloor;
            }

            //刚切层还没收到房主的新快照时，二层及以上默认按本机独立层处理，
            //避免先跳过敌人生成、等快照到达后却没有第一波敌人。
            return _locallyOwnedFloor == dungeonManager.CurrentFloor || dungeonManager.CurrentFloor > 1;
        }
    }

    public void ClaimIndependentFloor(int floor)
    {
        if (!IsLanConnected || IsHost || floor <= 0)
        {
            return;
        }

        if (_knownHostFloor <= 0 || _knownHostFloor != floor)
        {
            _locallyOwnedFloor = floor;
        }
    }

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
    private readonly HashSet<long> _deadEnemyNetworkIds = new();
    private double _snapshotTimer;
    private double _worldSnapshotTimer;
    private double _roomStateTimer;
    private double _mapExplorationTimer;
    private long _localGoldRevision;
    private ulong _lastSnapshotSentAt;
    private World _lastLocalWorld;
    private string _lastLocalWorldKind = string.Empty;
    private string _lastBroadcastStateKey = string.Empty;
    private string _lastRequestedStateKey = string.Empty;
    private string _lastLocalMapKnowledgeKey = string.Empty;
    private int _sessionRevision;
    private int _knownHostFloor;
    private int _locallyOwnedFloor;
    private bool _applyingRemoteState;
    private Variant[] _pendingRemoteState;
    private long _nextDynamicNetworkId = 1;
    private long _nextDamageSequence = 1;
    private long _nextShopTransactionId = 1;
    private long _lastAppliedDamageSequence;
    private readonly HashSet<int> _broadcastClearedRooms = new();
    /// <summary>房主已经清空的房间。重开一局要清掉, 否则同 ID 房间不会再同步开门。</summary>
    private readonly HashSet<int> _hostClearedRooms = new();
    private readonly Dictionary<long, string> _openedTreasureBoxRewards = new();
    private readonly Dictionary<long, SharedShopState> _pendingSharedShopStates = new();
    private readonly Dictionary<long, PendingShopTransaction> _pendingShopTransactions = new();
    private Player _defeatSettlementPlayer;
    private double _coopDefeatTimer = -1;
    private bool _coopDefeatSettlementShown;
    private readonly Dictionary<(int Session, int Floor, int Room), List<NetworkLiquidStroke>> _liquidStrokeHistory = new();
    private readonly HashSet<string> _seenLiquidStrokeIds = new();
    private readonly Queue<string> _liquidStrokeIdOrder = new();
    private readonly HashSet<string> _appliedLiquidStrokeIds = new();
    private readonly Dictionary<string, LiquidStrokeThrottle> _liquidStrokeThrottles = new();
    private long _nextLiquidStrokeSequence = 1;
    private double _liquidReplayTimer;

    private sealed class NetworkLiquidStroke
    {
        public int SessionRevision;
        public int Floor;
        public int RoomId;
        public long SourcePeerId;
        public long Sequence;
        public string BrushId;
        public string LayerId;
        public bool HasPrevious;
        public int PreviousX;
        public int PreviousY;
        public int X;
        public int Y;
        public float Rotation;
        public ulong ReceivedAt;
    }

    private sealed class LiquidStrokeThrottle
    {
        public ulong SentAt;
        public Vector2I Position;
        public bool HasPosition;
    }

    private sealed class RemotePlayerState
    {
        public long PeerId;
        public Vector2 TargetPosition;
        public FaceDirection Face;
        public float AimRotationDegrees;
        public Player Player;
        public bool IsMoving;
        public bool IsRolling;
        public bool IsMeleeAttacking;
        public long MeleeAttackSequence;
        public int RoomId = -1;
        public int Floor;
        public bool IsDead;
        public string DeathAnimation = string.Empty;
        public int DeathFrame;
        public int Gold;
        public long GoldRevision = -1;
        public string WeaponId = string.Empty;
    }

    private sealed class SharedShopState
    {
        public string[] StockIds;
        public int RefreshCount;
        public int Floor;
        public int SessionRevision;
    }

    private sealed class PendingShopTransaction
    {
        public int RefundAmount;
        public string PurchaseActivityId;
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
        UpdateCoopDefeatSettlement(delta);
        _liquidReplayTimer -= delta;
        if (_liquidReplayTimer <= 0)
        {
            _liquidReplayTimer = 0.5;
            ApplyCachedLiquidStrokesToLocalFloor();
        }
        if (IsHost)
        {
            // 不只监听 world 类型, 还要检测楼层/种子变化。
            BroadcastSessionState();

            _roomStateTimer -= delta;
            if (_roomStateTimer <= 0)
            {
                _roomStateTimer = 1.0;
                BroadcastRoomStates();
            }

            _worldSnapshotTimer -= delta;
            if (_worldSnapshotTimer <= 0)
            {
                _worldSnapshotTimer = 0.05;
                BroadcastEnemySnapshots();
                CheckRemoteRoomWaves();
            }
        }
        UpdateRemotePlayers((float)delta);
        ApplyPendingTreasureBoxStates();
        ApplyPendingSharedShopStates();
        SyncLocalMapExploration(delta);

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
        _knownHostFloor = 0;
        _locallyOwnedFloor = 0;
        _deadEnemyNetworkIds.Clear();
        _hostClearedRooms.Clear();
        _openedTreasureBoxRewards.Clear();
        _pendingSharedShopStates.Clear();
        _pendingShopTransactions.Clear();
        _appliedLiquidStrokeIds.Clear();
        _liquidStrokeThrottles.Clear();
        _nextShopTransactionId = 1;
        _localGoldRevision = 0;
        StartHostDiscovery();
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _pendingRemoteState = null;
        _lastSnapshotSentAt = 0;
        _worldSnapshotTimer = 0;
        _nextDamageSequence = 1;
        _lastAppliedDamageSequence = 0;
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
        _deadEnemyNetworkIds.Clear();
        _pendingSharedShopStates.Clear();
        _pendingShopTransactions.Clear();
        _nextShopTransactionId = 1;
        _localGoldRevision = 0;
        _sessionRevision = 0;
        _knownHostFloor = 0;
        _locallyOwnedFloor = 0;
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _pendingRemoteState = null;
        _lastSnapshotSentAt = 0;
        _worldSnapshotTimer = 0;
        _nextDamageSequence = 1;
        _lastAppliedDamageSequence = 0;
        SetStatus($"正在连接 {address}:{port}...");
        return true;
    }

    public void Disconnect()
    {
        StopHostSearch();
        StopHostDiscovery();
        ClearRemotePlayers();
        _deadEnemyNetworkIds.Clear();
        _hostClearedRooms.Clear();
        _openedTreasureBoxRewards.Clear();
        _pendingSharedShopStates.Clear();
        _pendingShopTransactions.Clear();
        _liquidStrokeHistory.Clear();
        _seenLiquidStrokeIds.Clear();
        _liquidStrokeIdOrder.Clear();
        _appliedLiquidStrokeIds.Clear();
        _liquidStrokeThrottles.Clear();
        _localGoldRevision = 0;
        _lastLocalWorld = null;
        _lastLocalWorldKind = string.Empty;
        _lastBroadcastStateKey = string.Empty;
        _lastRequestedStateKey = string.Empty;
        _knownHostFloor = 0;
        _locallyOwnedFloor = 0;
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

        var currentFloor = dungeonManager.CurrentFloor;

        foreach (var state in _remotePlayers.Values)
        {
            if (state.Floor != currentFloor)
            {
                continue;
            }

            if (state.PeerId == entrantPeerId)
            {
                state.RoomId = roomId;
                state.TargetPosition = entrantPosition;
                dungeonManager.OnRemotePlayerEnterRoom(roomId, state.Player);
                SendLiquidHistoryToPeer(state.PeerId, currentFloor, roomId);
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
            SendLiquidHistoryToPeer(state.PeerId, currentFloor, roomId);
        }
    }

    public void NotifyPortalEntered(int currentFloor)
    {
        if (!IsLanConnected)
        {
            return;
        }

        if (IsHost)
        {
            BroadcastPortalEntered(LocalPeerId, currentFloor);
        }
        else
        {
            RpcId(1, nameof(ReceivePortalEnteredRequest), currentFloor);
        }
    }

    private void BroadcastPortalEntered(long peerId, int currentFloor)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        if (peerId != LocalPeerId)
        {
            GameNotificationOverlay.ShowPortalEntered(peerId == 1 ? "房主" : "访客", currentFloor + 1);
        }

        Rpc(nameof(ReceivePortalEntered), peerId, currentFloor);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceivePortalEnteredRequest(int currentFloor)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        var peerId = Multiplayer.GetRemoteSenderId();
        if (peerId > 1)
        {
            BroadcastPortalEntered(peerId, currentFloor);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceivePortalEntered(long peerId, int currentFloor)
    {
        if (IsHost || peerId == LocalPeerId)
        {
            return;
        }

        GameNotificationOverlay.ShowPortalEntered(peerId == 1 ? "房主" : "访客", currentFloor + 1);
    }

    public void RequestSharedPickup(ActivityObject item)
    {
        if (item == null || item.IsDestroyed)
        {
            return;
        }

        if (!IsHost && IsLocalDungeonAuthority)
        {
            var player = GameApplication.Instance?.DungeonManager?.CurrWorld?.Player;
            if (player != null && item.CheckInteractive(player).CanInteractive)
            {
                item.Interactive(player);
            }
            return;
        }

        if (item.NetworkId == 0)
        {
            if (IsHost)
            {
                item.NetworkId = AllocateDynamicNetworkId();
            }
            else
            {
                if (!string.IsNullOrEmpty(item.ActivityBase?.Id))
                {
                    RpcId(1, nameof(ReceiveSharedPickupFallbackRequest), item.ActivityBase.Id,
                        item.GlobalPosition.X, item.GlobalPosition.Y);
                }
                return;
            }
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

        if (item.NetworkId == 0)
        {
            item.NetworkId = AllocateDynamicNetworkId();
        }

        Rpc(nameof(ReceiveNetworkPickupSpawn), item.NetworkId, item.ActivityBase?.Id ?? string.Empty,
            item.GlobalPosition.X, item.GlobalPosition.Y);
    }

    public void RequestNetworkPickupSpawn(ActivityObject item)
    {
        if (!IsLanConnected || item == null || item.IsDestroyed || string.IsNullOrEmpty(item.ActivityBase?.Id))
        {
            return;
        }

        if (IsHost)
        {
            BroadcastNetworkPickupSpawn(item);
            return;
        }

        if (!IsLocalDungeonAuthority)
        {
            RpcId(1, nameof(ReceiveNetworkPickupSpawnRequest), item.ActivityBase.Id,
                item.GlobalPosition.X, item.GlobalPosition.Y);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkPickupSpawnRequest(string activityId, float x, float y)
    {
        if (!IsHost || string.IsNullOrEmpty(activityId) ||
            !ExcelConfig.ActivityBase_Map.TryGetValue(activityId, out var config) ||
            config.Type is not (ActivityType.Weapon or ActivityType.Prop or ActivityType.Enemy or ActivityType.Boss or ActivityType.Treasure))
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        var position = new Vector2(x, y);
        var currentFloor = GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 0;
        if (!_remotePlayers.TryGetValue(senderId, out var state) || state.Player == null ||
            !GodotObject.IsInstanceValid(state.Player) || state.IsDead || state.Floor != currentFloor ||
            state.TargetPosition.DistanceTo(position) > 128f)
        {
            return;
        }

        var item = ActivityObject.Create(activityId);
        if (item == null)
        {
            return;
        }

        item.PutDown(position, RoomLayerEnum.YSortLayer, false);
        BroadcastNetworkPickupSpawn(item);
    }

    public void RequestNetworkPropDrop(PropActivity prop)
    {
        if (!IsLanConnected || prop == null || prop.IsDestroyed || string.IsNullOrEmpty(prop.ActivityBase?.Id))
        {
            return;
        }

        if (IsHost)
        {
            BroadcastNetworkPickupSpawn(prop);
            return;
        }

        if (IsLocalDungeonAuthority)
        {
            return;
        }

        RpcId(1, nameof(ReceiveNetworkPropDropRequest), prop.ActivityBase.Id,
            prop.GlobalPosition.X, prop.GlobalPosition.Y);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkPropDropRequest(string activityId, float x, float y)
    {
        if (!IsHost || string.IsNullOrEmpty(activityId))
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        var position = new Vector2(x, y);
        if (!_remotePlayers.TryGetValue(senderId, out var state) || state.Player == null ||
            !GodotObject.IsInstanceValid(state.Player) || state.Player.IsDie ||
            state.Floor != (GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 0) ||
            state.TargetPosition.DistanceTo(position) > 96f)
        {
            return;
        }

        var item = ActivityObject.Create(activityId);
        if (item is not PropActivity prop)
        {
            item?.Destroy();
            return;
        }

        prop.PutDown(position, RoomLayerEnum.YSortLayer, false);
        BroadcastNetworkPickupSpawn(prop);
    }

    public void BroadcastNetworkWeaponDrop(Weapon weapon)
    {
        if (!IsHost || !IsLanConnected || weapon == null || weapon.IsDestroyed)
        {
            return;
        }

        weapon.NetworkId = AllocateDynamicNetworkId();
        Rpc(nameof(ReceiveNetworkWeaponDrop), weapon.NetworkId, weapon.ActivityBase?.Id ?? string.Empty,
            weapon.GlobalPosition.X, weapon.GlobalPosition.Y);
    }

    public void RequestNetworkWeaponDrop(Weapon weapon)
    {
        if (!IsLanConnected || weapon == null || weapon.IsDestroyed ||
            string.IsNullOrEmpty(weapon.ActivityBase?.Id))
        {
            return;
        }

        if (IsHost)
        {
            BroadcastNetworkWeaponDrop(weapon);
            return;
        }

        if (IsLocalDungeonAuthority)
        {
            return;
        }

        RpcId(1, nameof(ReceiveNetworkWeaponDropRequest), weapon.ActivityBase.Id,
            weapon.GlobalPosition.X, weapon.GlobalPosition.Y);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkWeaponDropRequest(string activityId, float x, float y)
    {
        if (!IsHost || string.IsNullOrEmpty(activityId))
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        var position = new Vector2(x, y);
        if (!_remotePlayers.TryGetValue(senderId, out var state) || state.Player == null ||
            !GodotObject.IsInstanceValid(state.Player) || state.Player.IsDie ||
            state.Floor != (GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 0) ||
            state.TargetPosition.DistanceTo(position) > 96f)
        {
            return;
        }

        var weapon = ActivityObject.Create<Weapon>(activityId);
        if (weapon == null)
        {
            return;
        }

        weapon.PutDown(position, RoomLayerEnum.YSortLayer, false);
        BroadcastNetworkWeaponDrop(weapon);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkWeaponDrop(long networkId, string activityId, float x, float y)
    {
        if (IsHost || IsLocalDungeonAuthority || networkId == 0 || string.IsNullOrEmpty(activityId) ||
            FindActivityObject(networkId) != null)
        {
            return;
        }

        var position = new Vector2(x, y);
        var weapon = FindUnnetworkedWeaponDrop(activityId, position);
        if (weapon == null)
        {
            weapon = ActivityObject.Create<Weapon>(activityId);
        }
        if (weapon == null)
        {
            return;
        }

        if (weapon.GetParentOrNull<Node>() == null)
        {
            weapon.PutDown(position, RoomLayerEnum.YSortLayer, false);
        }
        else
        {
            weapon.GlobalPosition = position;
        }
        weapon.NetworkId = networkId;
    }

    private Weapon FindUnnetworkedWeaponDrop(string activityId, Vector2 position)
    {
        var root = GetTree()?.Root;
        if (root == null)
        {
            return null;
        }

        Weapon nearest = null;
        var nearestDistanceSquared = 96f * 96f;
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is Weapon weapon && !weapon.IsDestroyed && weapon.Master == null &&
                weapon.NetworkId == 0 && weapon.ActivityBase?.Id == activityId)
            {
                var distanceSquared = weapon.GlobalPosition.DistanceSquaredTo(position);
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = weapon;
                }
            }

            foreach (var child in node.GetChildren())
            {
                pending.Push((Node)child);
            }
        }

        return nearest;
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
        if (IsHost || IsLocalDungeonAuthority || networkId == 0 || string.IsNullOrEmpty(activityId) ||
            FindActivityObject(networkId) != null)
        {
            return;
        }

        var gold = ObjectManager.GetActivityObject<Gold>(activityId);
        gold.NetworkId = networkId;
        gold.GoldCount = count;
        gold.InitNetworkDrop(new Vector2(x, y));
    }

    public void OnLocalPlayerGoldChanged(Player player)
    {
        if (IsLanConnected && player == GameApplication.Instance?.DungeonManager?.CurrWorld?.Player)
        {
            _localGoldRevision++;
        }
    }

    public bool ShouldDrawLiquidLocally(World roomWorld, ActivityObject source)
    {
        if (!IsLanConnected)
        {
            return true;
        }

        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (world is not Dungeon || roomWorld != world)
        {
            return false;
        }

        var localPlayer = world.Player;
        if (source == localPlayer ||
            source is Weapon weapon && weapon.Master == localPlayer ||
            source is Bullet bullet && bullet.BulletData?.TriggerRole == localPlayer)
        {
            return true;
        }

        return IsHost || IsLocalDungeonAuthority;
    }

    public void OnLocalLiquidBrushDrawn(RoomInfo room, BrushImageData brush,
        ExcelConfig.LiquidLayer layer, Vector2I? previousPosition, Vector2I position,
        float rotation, ActivityObject source)
    {
        if (!IsLanConnected || room == null || brush?.Brush == null || layer == null || source == null ||
            !ShouldDrawLiquidLocally(room.World, source))
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager?.CurrWorld is not Dungeon || room.LiquidCanvas == null)
        {
            return;
        }

        var floor = dungeonManager.CurrentFloor;
        var throttleKey = $"{LocalPeerId}:{floor}:{room.Id}:{source.GetInstanceId()}:{layer.Id}:{brush.Brush.Id}";
        var now = Time.GetTicksMsec();
        if (!_liquidStrokeThrottles.TryGetValue(throttleKey, out var throttle))
        {
            throttle = new LiquidStrokeThrottle();
            _liquidStrokeThrottles.Add(throttleKey, throttle);
        }

        if (now - throttle.SentAt < 100 && throttle.HasPosition && throttle.Position == position)
        {
            return;
        }

        if (now - throttle.SentAt < 80)
        {
            return;
        }

        var stroke = new NetworkLiquidStroke
        {
            SessionRevision = _sessionRevision,
            Floor = floor,
            RoomId = room.Id,
            SourcePeerId = LocalPeerId,
            Sequence = _nextLiquidStrokeSequence++,
            BrushId = brush.Brush.Id,
            LayerId = layer.Id,
            HasPrevious = throttle.HasPosition,
            PreviousX = throttle.Position.X,
            PreviousY = throttle.Position.Y,
            X = position.X,
            Y = position.Y,
            Rotation = rotation,
            ReceivedAt = now,
        };
        throttle.SentAt = now;
        throttle.Position = position;
        throttle.HasPosition = true;

        var strokeId = GetLiquidStrokeId(stroke);
        if (!RememberLiquidStroke(strokeId))
        {
            return;
        }

        StoreLiquidStroke(stroke);
        _appliedLiquidStrokeIds.Add(strokeId);
        if (IsHost)
        {
            Rpc(nameof(ReceiveNetworkLiquidStroke), stroke.SessionRevision, stroke.Floor, stroke.RoomId,
                stroke.SourcePeerId, stroke.Sequence, stroke.BrushId, stroke.LayerId, stroke.HasPrevious,
                stroke.PreviousX, stroke.PreviousY, stroke.X, stroke.Y, stroke.Rotation);
        }
        else
        {
            RpcId(1, nameof(ReceiveNetworkLiquidStrokeRequest), stroke.SessionRevision, stroke.Floor,
                stroke.RoomId, stroke.Sequence, stroke.BrushId, stroke.LayerId, stroke.HasPrevious,
                stroke.PreviousX, stroke.PreviousY, stroke.X, stroke.Y, stroke.Rotation);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkLiquidStrokeRequest(int sessionRevision, int floor, int roomId, long sequence,
        string brushId, string layerId, bool hasPrevious, int previousX, int previousY, int x, int y, float rotation)
    {
        if (!IsHost || sessionRevision != _sessionRevision || floor <= 0 || roomId < 0 || sequence <= 0 ||
            !ExcelConfig.LiquidBrush_Map.ContainsKey(brushId) || !ExcelConfig.LiquidLayer_Map.ContainsKey(layerId))
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId <= 1 || !_remotePlayers.TryGetValue(senderId, out var state) ||
            state.Floor != floor || state.RoomId != roomId)
        {
            return;
        }

        AcceptAndRelayLiquidStroke(new NetworkLiquidStroke
        {
            SessionRevision = sessionRevision,
            Floor = floor,
            RoomId = roomId,
            SourcePeerId = senderId,
            Sequence = sequence,
            BrushId = brushId,
            LayerId = layerId,
            HasPrevious = hasPrevious,
            PreviousX = previousX,
            PreviousY = previousY,
            X = x,
            Y = y,
            Rotation = rotation,
            ReceivedAt = Time.GetTicksMsec(),
        });
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkLiquidStroke(int sessionRevision, int floor, int roomId, long sourcePeerId,
        long sequence, string brushId, string layerId, bool hasPrevious, int previousX, int previousY,
        int x, int y, float rotation)
    {
        if (IsHost || sessionRevision != _sessionRevision || floor <= 0 || roomId < 0 || sequence <= 0 ||
            !ExcelConfig.LiquidBrush_Map.ContainsKey(brushId) || !ExcelConfig.LiquidLayer_Map.ContainsKey(layerId))
        {
            return;
        }

        AcceptAndApplyLiquidStroke(new NetworkLiquidStroke
        {
            SessionRevision = sessionRevision,
            Floor = floor,
            RoomId = roomId,
            SourcePeerId = sourcePeerId,
            Sequence = sequence,
            BrushId = brushId,
            LayerId = layerId,
            HasPrevious = hasPrevious,
            PreviousX = previousX,
            PreviousY = previousY,
            X = x,
            Y = y,
            Rotation = rotation,
            ReceivedAt = Time.GetTicksMsec(),
        });
    }

    private void AcceptAndRelayLiquidStroke(NetworkLiquidStroke stroke)
    {
        if (!AcceptAndApplyLiquidStroke(stroke))
        {
            return;
        }

        Rpc(nameof(ReceiveNetworkLiquidStroke), stroke.SessionRevision, stroke.Floor, stroke.RoomId,
            stroke.SourcePeerId, stroke.Sequence, stroke.BrushId, stroke.LayerId, stroke.HasPrevious,
            stroke.PreviousX, stroke.PreviousY, stroke.X, stroke.Y, stroke.Rotation);
    }

    private bool AcceptAndApplyLiquidStroke(NetworkLiquidStroke stroke)
    {
        if (!RememberLiquidStroke(GetLiquidStrokeId(stroke)))
        {
            return false;
        }

        StoreLiquidStroke(stroke);
        ApplyLiquidStrokeToLocalWorld(stroke);
        return true;
    }

    private void ApplyLiquidStrokeToLocalWorld(NetworkLiquidStroke stroke)
    {
        var manager = GameApplication.Instance?.DungeonManager;
        if (manager?.CurrWorld is not Dungeon || manager.CurrentFloor != stroke.Floor)
        {
            return;
        }

        var strokeId = GetLiquidStrokeId(stroke);
        if (!_appliedLiquidStrokeIds.Add(strokeId))
        {
            return;
        }

        var room = manager.RoomInfosForNetwork.FirstOrDefault(item => item.Id == stroke.RoomId);
        if (room?.LiquidCanvas == null)
        {
            _appliedLiquidStrokeIds.Remove(strokeId);
            return;
        }

        var brush = LiquidBrushManager.GetBrush(stroke.BrushId);
        var layer = ExcelConfig.LiquidLayer_Map[stroke.LayerId];
        Vector2I? previous = stroke.HasPrevious
            ? new Vector2I(stroke.PreviousX, stroke.PreviousY)
            : null;
        room.LiquidCanvas.DrawBrush(brush, layer, previous,
            new Vector2I(stroke.X, stroke.Y), stroke.Rotation, null, false);
    }

    private void ApplyCachedLiquidStrokesToLocalFloor()
    {
        var manager = GameApplication.Instance?.DungeonManager;
        if (manager?.CurrWorld is not Dungeon)
        {
            return;
        }

        var floor = manager.CurrentFloor;
        var now = Time.GetTicksMsec();
        foreach (var pair in _liquidStrokeHistory)
        {
            if (pair.Key.Session != _sessionRevision || pair.Key.Floor != floor)
            {
                continue;
            }

            foreach (var stroke in pair.Value)
            {
                if (now - stroke.ReceivedAt <= 10000)
                {
                    ApplyLiquidStrokeToLocalWorld(stroke);
                }
            }
        }
    }

    private void SendLiquidHistoryToPeer(long peerId, int floor, int roomId)
    {
        if (!IsHost || peerId <= 1 ||
            !_liquidStrokeHistory.TryGetValue((_sessionRevision, floor, roomId), out var strokes))
        {
            return;
        }

        var now = Time.GetTicksMsec();
        foreach (var stroke in strokes)
        {
            if (now - stroke.ReceivedAt <= 10000)
            {
                RpcId(peerId, nameof(ReceiveNetworkLiquidStroke), stroke.SessionRevision, stroke.Floor,
                    stroke.RoomId, stroke.SourcePeerId, stroke.Sequence, stroke.BrushId, stroke.LayerId,
                    stroke.HasPrevious, stroke.PreviousX, stroke.PreviousY, stroke.X, stroke.Y, stroke.Rotation);
            }
        }
    }

    private void StoreLiquidStroke(NetworkLiquidStroke stroke)
    {
        var key = (stroke.SessionRevision, stroke.Floor, stroke.RoomId);
        if (!_liquidStrokeHistory.TryGetValue(key, out var strokes))
        {
            strokes = new List<NetworkLiquidStroke>();
            _liquidStrokeHistory.Add(key, strokes);
        }

        var now = Time.GetTicksMsec();
        strokes.RemoveAll(item => now - item.ReceivedAt > 10000);
        strokes.Add(stroke);
        if (strokes.Count > 256)
        {
            strokes.RemoveRange(0, strokes.Count - 256);
        }
    }

    private bool RememberLiquidStroke(string strokeId)
    {
        if (!_seenLiquidStrokeIds.Add(strokeId))
        {
            return false;
        }

        _liquidStrokeIdOrder.Enqueue(strokeId);
        while (_liquidStrokeIdOrder.Count > 32768)
        {
            _seenLiquidStrokeIds.Remove(_liquidStrokeIdOrder.Dequeue());
        }

        return true;
    }

    private static string GetLiquidStrokeId(NetworkLiquidStroke stroke)
    {
        return $"{stroke.SessionRevision}:{stroke.Floor}:{stroke.RoomId}:{stroke.SourcePeerId}:{stroke.Sequence}";
    }


    public void BroadcastSharedShopState(ShopBoss shop)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (!IsHost || !IsLanConnected || dungeonManager?.CurrWorld is not Dungeon ||
            shop == null || shop.IsDestroyed || shop.ShopRoomId < 0 ||
            !shop.IsShopStockBuilt)
        {
            return;
        }

        Rpc(nameof(ReceiveSharedShopState), shop.ShopRoomId, dungeonManager.CurrentFloor, _sessionRevision,
            shop.SharedRefreshCount,
            PackShopStock(shop.GetSharedStockIds()));
    }

    public void RequestSharedShopState(ShopBoss shop)
    {
        if (!IsLanConnected || IsHost || shop == null || shop.IsDestroyed || shop.ShopRoomId < 0)
        {
            return;
        }

        RpcId(1, nameof(ReceiveSharedShopStateRequest), shop.ShopRoomId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedShopStateRequest(int shopRoomId)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        var peerId = Multiplayer.GetRemoteSenderId();
        if (peerId <= 1 || FindShopByRoomId(shopRoomId) is not ShopBoss shop || !shop.IsShopStockBuilt)
        {
            return;
        }

        SendSharedShopStateToPeer(peerId, shop);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedShopState(int shopRoomId, int floor, int sessionRevision, int refreshCount,
        Godot.Collections.Array<Variant> stockPacket)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (IsHost || IsLocalDungeonAuthority || shopRoomId < 0 || stockPacket == null ||
            dungeonManager?.CurrWorld is not Dungeon || dungeonManager.CurrentFloor != floor ||
            sessionRevision != _sessionRevision)
        {
            return;
        }

        var stockIds = new string[stockPacket.Count];
        for (var i = 0; i < stockPacket.Count; i++)
        {
            stockIds[i] = stockPacket[i].AsString();
        }

        _pendingSharedShopStates[shopRoomId] = new SharedShopState
        {
            StockIds = stockIds,
            RefreshCount = refreshCount,
            Floor = floor,
            SessionRevision = sessionRevision,
        };
        ApplyPendingSharedShopStates();
    }

    private void ApplyPendingSharedShopStates()
    {
        if (IsHost || _pendingSharedShopStates.Count == 0)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        foreach (var pair in _pendingSharedShopStates.ToArray())
        {
            if (dungeonManager?.CurrWorld is not Dungeon ||
                pair.Value.Floor != dungeonManager.CurrentFloor || pair.Value.SessionRevision != _sessionRevision)
            {
                _pendingSharedShopStates.Remove(pair.Key);
                continue;
            }

            if (FindShopByRoomId((int)pair.Key) is not ShopBoss shop || !shop.IsShopStockBuilt)
            {
                continue;
            }

            shop.ApplySharedShopState(pair.Value.StockIds, pair.Value.RefreshCount);
            _pendingSharedShopStates.Remove(pair.Key);
        }
    }

    private static Godot.Collections.Array<Variant> PackShopStock(string[] stockIds)
    {
        var packet = new Godot.Collections.Array<Variant>();
        foreach (var id in stockIds ?? System.Array.Empty<string>())
        {
            packet.Add(id ?? string.Empty);
        }

        return packet;
    }

    private static ShopBoss FindShopByRoomId(int roomId)
    {
        var roles = GameApplication.Instance?.DungeonManager?.CurrWorld?.Role_InstanceList;
        return roles?.OfType<ShopBoss>().FirstOrDefault(shop =>
            !shop.IsDestroyed && shop.ShopRoomId == roomId);
    }

    private void SendSharedShopStateToPeer(long peerId, ShopBoss shop)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (peerId <= 1 || dungeonManager?.CurrWorld is not Dungeon ||
            shop == null || shop.IsDestroyed || !shop.IsShopStockBuilt)
        {
            return;
        }

        RpcId(peerId, nameof(ReceiveSharedShopState), shop.ShopRoomId, dungeonManager.CurrentFloor,
            _sessionRevision, shop.SharedRefreshCount,
            PackShopStock(shop.GetSharedStockIds()));
    }

    private void BroadcastSharedShopStatesToPeer(long peerId)
    {
        var roles = GameApplication.Instance?.DungeonManager?.CurrWorld?.Role_InstanceList;
        if (!IsHost || !IsLanConnected || peerId <= 1 || roles == null)
        {
            return;
        }

        foreach (var role in roles)
        {
            if (role is ShopBoss shop && !shop.IsDestroyed && shop.ShopRoomId >= 0 && shop.IsShopStockBuilt)
            {
                SendSharedShopStateToPeer(peerId, shop);
            }
        }
    }

    private void BroadcastAllSharedShopStates()
    {
        var roles = GameApplication.Instance?.DungeonManager?.CurrWorld?.Role_InstanceList;
        if (!IsHost || !IsLanConnected || roles == null)
        {
            return;
        }

        foreach (var role in roles)
        {
            if (role is ShopBoss shop && !shop.IsDestroyed && shop.ShopRoomId >= 0 && shop.IsShopStockBuilt)
            {
                BroadcastSharedShopState(shop);
            }
        }
    }

    public void RequestSharedShopPurchase(ShopBoss shop, int slotIndex)
    {
        if (!IsLanConnected || shop == null || shop.IsDestroyed || shop.ShopRoomId < 0 ||
            !shop.TryGetSharedPurchase(slotIndex, out _, out _))
        {
            return;
        }

        if (IsHost)
        {
            ResolveSharedShopPurchase(LocalPeerId, 0, shop.ShopRoomId, slotIndex,
                string.Empty, 0, 0, 0);
            return;
        }

        if (GameApplication.Instance?.DungeonManager?.CurrWorld?.Player is not Player player)
        {
            return;
        }

        if (!shop.TryGetSharedPurchase(slotIndex, out var activityId, out var price) ||
            player.RoleState.Gold < price || !ShopItemSlot.IsSupportedShopItem(activityId))
        {
            return;
        }

        var requestId = _nextShopTransactionId++;
        var goldBeforePurchase = player.RoleState.Gold;
        var revisionBeforePurchase = _localGoldRevision;
        _pendingShopTransactions[requestId] = new PendingShopTransaction
        {
            RefundAmount = price,
            PurchaseActivityId = activityId,
        };
        player.UseGold(price);
        RpcId(1, nameof(ReceiveSharedShopPurchaseRequest), requestId, shop.ShopRoomId, slotIndex,
            activityId, price, goldBeforePurchase, revisionBeforePurchase);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedShopPurchaseRequest(long requestId, int shopRoomId, int slotIndex,
        string expectedActivityId, int expectedPrice, int reportedGoldBeforePurchase, long goldRevisionBeforePurchase)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        ResolveSharedShopPurchase(Multiplayer.GetRemoteSenderId(), requestId, shopRoomId, slotIndex,
            expectedActivityId, expectedPrice, reportedGoldBeforePurchase, goldRevisionBeforePurchase);
    }

    private void ResolveSharedShopPurchase(long peerId, long requestId, int shopRoomId, int slotIndex,
        string expectedActivityId, int expectedPrice, int reportedGoldBeforePurchase,
        long goldRevisionBeforePurchase)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        if (FindShopByRoomId(shopRoomId) is not ShopBoss shop ||
            !shop.TryGetSharedPurchase(slotIndex, out var activityId, out var price) ||
            !ShopItemSlot.IsSupportedShopItem(activityId) ||
            (peerId != LocalPeerId && (activityId != expectedActivityId || price != expectedPrice)))
        {
            SendSharedShopTransactionResult(peerId, requestId, false, string.Empty);
            return;
        }

        if (peerId == LocalPeerId)
        {
            if (GameApplication.Instance?.DungeonManager?.CurrWorld?.Player is Player hostPlayer &&
                IsPlayerAtShop(hostPlayer, shop, hostPlayer.AffiliationArea?.RoomInfo?.Id ?? -1))
            {
                var purchased = shop.TryPurchaseLocally(slotIndex, hostPlayer);
                SendSharedShopTransactionResult(peerId, requestId, purchased, purchased ? activityId : string.Empty);
            }

            return;
        }

        if (peerId <= 1 || !_remotePlayers.TryGetValue(peerId, out var state) ||
            state.Player == null || !GodotObject.IsInstanceValid(state.Player) ||
            !IsPlayerAtShop(state.Player, shop, state.RoomId))
        {
            SendSharedShopTransactionResult(peerId, requestId, false, string.Empty);
            return;
        }

        if (!ApplyRemoteShopCharge(state, reportedGoldBeforePurchase, goldRevisionBeforePurchase, price))
        {
            SendSharedShopTransactionResult(peerId, requestId, false, string.Empty);
            return;
        }

        state.Player.RoleState.Gold = state.Gold;
        shop.MarkSharedSlotSold(slotIndex);
        SendSharedShopTransactionResult(peerId, requestId, true, activityId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedShopTransactionResult(long requestId, bool accepted, string activityId)
    {
        if (IsHost || !_pendingShopTransactions.Remove(requestId, out var pending))
        {
            return;
        }

        var player = GameApplication.Instance?.DungeonManager?.CurrWorld?.Player;
        if (!accepted)
        {
            player?.AddGold(pending.RefundAmount);
            return;
        }

        if (string.IsNullOrEmpty(activityId))
        {
            return;
        }

        if (activityId != pending.PurchaseActivityId || !ShopItemSlot.TryGrantItemToPlayer(player, activityId))
        {
            player?.AddGold(pending.RefundAmount);
            GD.PushWarning($"商店同步购买物品失败，已退款: {activityId}");
        }
    }

    private void SendSharedShopTransactionResult(long peerId, long requestId, bool accepted, string activityId)
    {
        if (peerId > 1 && requestId > 0)
        {
            RpcId(peerId, nameof(ReceiveSharedShopTransactionResult), requestId, accepted, activityId ?? string.Empty);
        }
    }

    private static bool ApplyRemoteShopCharge(RemotePlayerState state, int reportedGoldBeforeCharge,
        long reportedRevisionBeforeCharge, int price)
    {
        if (reportedRevisionBeforeCharge < 0 || reportedGoldBeforeCharge < price)
        {
            return false;
        }

        if (state.GoldRevision <= reportedRevisionBeforeCharge)
        {
            if (state.GoldRevision == reportedRevisionBeforeCharge && state.Gold < price)
            {
                return false;
            }

            state.Gold = Math.Max(0, reportedGoldBeforeCharge - price);
            state.GoldRevision = reportedRevisionBeforeCharge + 1;
        }

        return true;
    }

    public void RequestSharedShopRefresh(ShopBoss shop)
    {
        if (!IsLanConnected || shop == null || shop.IsDestroyed || shop.ShopRoomId < 0 ||
            !shop.IsShopStockBuilt)
        {
            return;
        }

        if (IsHost)
        {
            if (GameApplication.Instance?.DungeonManager?.CurrWorld?.Player is Player hostPlayer &&
                IsPlayerAtShop(hostPlayer, shop, hostPlayer.AffiliationArea?.RoomInfo?.Id ?? -1))
            {
                shop.TryRefreshLocally(hostPlayer);
            }

            return;
        }

        if (GameApplication.Instance?.DungeonManager?.CurrWorld?.Player is not Player player)
        {
            return;
        }

        var cost = shop.CurrentRefreshCost;
        if (player.RoleState.Gold < cost)
        {
            return;
        }

        var requestId = _nextShopTransactionId++;
        var goldBeforeRefresh = player.RoleState.Gold;
        var revisionBeforeRefresh = _localGoldRevision;
        _pendingShopTransactions[requestId] = new PendingShopTransaction
        {
            RefundAmount = cost,
            PurchaseActivityId = string.Empty,
        };
        player.UseGold(cost);
        RpcId(1, nameof(ReceiveSharedShopRefreshRequest), requestId, shop.ShopRoomId,
            cost, goldBeforeRefresh, revisionBeforeRefresh);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedShopRefreshRequest(long requestId, int shopRoomId, int expectedCost,
        int reportedGoldBeforeRefresh, long goldRevisionBeforeRefresh)
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        ResolveSharedShopRefresh(Multiplayer.GetRemoteSenderId(), requestId, shopRoomId,
            expectedCost, reportedGoldBeforeRefresh, goldRevisionBeforeRefresh);
    }

    private void ResolveSharedShopRefresh(long peerId, long requestId, int shopRoomId,
        int expectedCost, int reportedGoldBeforeRefresh, long goldRevisionBeforeRefresh)
    {
        if (!IsHost || !IsLanConnected || peerId <= 1 ||
            !_remotePlayers.TryGetValue(peerId, out var state) ||
            state.Player == null || !GodotObject.IsInstanceValid(state.Player) ||
            FindShopByRoomId(shopRoomId) is not ShopBoss shop || !IsPlayerAtShop(state.Player, shop, state.RoomId))
        {
            SendSharedShopTransactionResult(peerId, requestId, false, string.Empty);
            return;
        }

        var cost = shop.CurrentRefreshCost;
        if (cost != expectedCost ||
            !ApplyRemoteShopCharge(state, reportedGoldBeforeRefresh, goldRevisionBeforeRefresh, cost))
        {
            SendSharedShopTransactionResult(peerId, requestId, false, string.Empty);
            return;
        }

        state.Player.RoleState.Gold = state.Gold;
        shop.RefreshFromNetworkAuthority();
        SendSharedShopTransactionResult(peerId, requestId, true, string.Empty);
    }

    private static bool IsPlayerAtShop(Player player, ShopBoss shop, int playerRoomId)
    {
        if (player == null || player.IsDestroyed || player.IsDie || shop == null || shop.IsDestroyed ||
            shop.ShopRoomId < 0 || playerRoomId != shop.ShopRoomId)
        {
            return false;
        }

        return player.GlobalPosition.DistanceSquaredTo(shop.GlobalPosition) <= 112f * 112f;
    }

    public void RequestTreasureBoxOpen(TreasureBox box)
    {
        if (!IsLanConnected || box == null || box.IsDestroyed || box.NetworkId == 0)
        {
            return;
        }

        if (IsHost)
        {
            ResolveTreasureBoxOpen(LocalPeerId, box.NetworkId);
        }
        else
        {
            RpcId(1, nameof(ReceiveTreasureBoxOpenRequest), box.NetworkId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveTreasureBoxOpenRequest(long networkId)
    {
        if (IsHost)
        {
            ResolveTreasureBoxOpen(Multiplayer.GetRemoteSenderId(), networkId);
        }
    }

    private void ResolveTreasureBoxOpen(long peerId, long networkId)
    {
        if (!IsHost || !IsLanConnected || networkId == 0)
        {
            return;
        }

        if (_openedTreasureBoxRewards.TryGetValue(networkId, out var openedRewardId))
        {
            if (peerId > 1)
            {
                RpcId(peerId, nameof(ReceiveTreasureBoxOpened), networkId, openedRewardId);
            }
            return;
        }

        var box = FindActivityObject(networkId) as TreasureBox;
        var player = peerId == LocalPeerId
            ? GameApplication.Instance?.DungeonManager?.CurrWorld?.Player
            : _remotePlayers.TryGetValue(peerId, out var state) ? state.Player : null;
        if (box == null || player == null || player.IsDestroyed ||
            box.GlobalPosition.DistanceTo(player.GlobalPosition) > 72f ||
            !box.CheckInteractive(player).CanInteractive)
        {
            return;
        }

        var rewardId = GameApplication.Instance?.DungeonManager?.CurrWorld?.RandomPool?.GetRandomProp()?.Id ?? string.Empty;
        _openedTreasureBoxRewards[networkId] = rewardId;
        box.OpenWithReward(rewardId);
        Rpc(nameof(ReceiveTreasureBoxOpened), networkId, rewardId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveTreasureBoxOpened(long networkId, string rewardId)
    {
        if (IsHost || IsLocalDungeonAuthority || networkId == 0)
        {
            return;
        }

        _openedTreasureBoxRewards[networkId] = rewardId ?? string.Empty;
        ApplyPendingTreasureBoxStates();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkPickupSpawn(long networkId, string activityId, float x, float y)
    {
        if (IsHost || IsLocalDungeonAuthority || networkId == 0 || string.IsNullOrEmpty(activityId) ||
            FindActivityObject(networkId) != null)
        {
            return;
        }

        var position = new Vector2(x, y);
        ActivityObject item = FindUnnetworkedActivityDrop(activityId, position);
        if (item == null)
        {
            item = ActivityObject.Create(activityId);
            if (item == null)
            {
                return;
            }

            item.PutDown(position, RoomLayerEnum.YSortLayer, false);
        }
        else
        {
            item.StopThrow();
            item.Altitude = 0;
            item.GlobalPosition = position;
        }

        item.NetworkId = networkId;
    }

    private ActivityObject FindUnnetworkedActivityDrop(string activityId, Vector2 position)
    {
        var root = GetTree()?.Root;
        if (root == null)
        {
            return null;
        }

        ActivityObject nearest = null;
        var nearestDistanceSquared = 96f * 96f;
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is ActivityObject item && !item.IsDestroyed && item.World == World.Current &&
                item.NetworkId == 0 &&
                item.ActivityBase?.Id == activityId &&
                (item is not PropActivity prop || prop.Master == null) &&
                (item is not Weapon weapon || weapon.Master == null))
            {
                var distanceSquared = item.GlobalPosition.DistanceSquaredTo(position);
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = item;
                }
            }

            foreach (var child in node.GetChildren())
            {
                pending.Push((Node)child);
            }
        }

        return nearest;
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

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedPickupFallbackRequest(string activityId, float x, float y)
    {
        if (!IsHost || string.IsNullOrEmpty(activityId))
        {
            return;
        }

        var peerId = Multiplayer.GetRemoteSenderId();
        var position = new Vector2(x, y);
        var currentFloor = GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 0;
        if (!_remotePlayers.TryGetValue(peerId, out var state) || state.Player == null ||
            !GodotObject.IsInstanceValid(state.Player) || state.IsDead || state.Floor != currentFloor ||
            state.TargetPosition.DistanceTo(position) > 96f)
        {
            return;
        }

        var item = FindSharedPickupCandidate(activityId, position, state.Player);
        if (item == null)
        {
            return;
        }

        if (item.NetworkId == 0)
        {
            item.NetworkId = AllocateDynamicNetworkId();
        }

        ResolveSharedPickup(peerId, item.NetworkId);
    }

    private ActivityObject FindSharedPickupCandidate(string activityId, Vector2 position, Role player)
    {
        var root = GetTree()?.Root;
        if (root == null || player == null)
        {
            return null;
        }

        var roomId = player.AffiliationArea?.RoomInfo?.Id ?? -1;
        ActivityObject nearest = null;
        var nearestDistanceSquared = 56f * 56f;
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is ActivityObject item && !item.IsDestroyed && item.World == World.Current &&
                item.ActivityBase?.Id == activityId && !item.IsPerPlayerLoot &&
                item is Weapon or PropActivity &&
                (item is not PropActivity prop || prop.Master == null) &&
                (item is not Weapon weapon || weapon.Master == null) &&
                (roomId < 0 || item.AffiliationArea?.RoomInfo?.Id == roomId))
            {
                var distanceSquared = item.GlobalPosition.DistanceSquaredTo(position);
                if (distanceSquared <= nearestDistanceSquared && item.CheckInteractive(player).CanInteractive)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = item;
                }
            }

            foreach (var child in node.GetChildren())
            {
                pending.Push((Node)child);
            }
        }

        return nearest;
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

        if (peerId == LocalPeerId)
        {
            var activityId = item.ActivityBase?.Id ?? string.Empty;
            var position = item.GlobalPosition;
            item.Interactive(player);
            Rpc(nameof(ReceiveSharedPickupResult), networkId, peerId, activityId, position.X, position.Y);
        }
        else
        {
            // 访客在自己的进程执行拾取效果；房主只负责占用物品，避免代理玩家
            // 再执行一次拾取而重复丢出替换道具。
            var activityId = item.ActivityBase?.Id ?? string.Empty;
            var position = item.GlobalPosition;
            item.Destroy();
            Rpc(nameof(ReceiveSharedPickupResult), networkId, peerId, activityId, position.X, position.Y);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveSharedPickupResult(long networkId, long winnerPeerId, string activityId = "",
        float x = 0, float y = 0)
    {
        if (!IsHost)
        {
            ApplySharedPickupResult(networkId, winnerPeerId, activityId, new Vector2(x, y));
        }
    }

    private void ApplySharedPickupResult(long networkId, long winnerPeerId, string activityId = "",
        Vector2 position = default)
    {
        var item = FindActivityObject(networkId);
        if (item == null && winnerPeerId != LocalPeerId && !string.IsNullOrEmpty(activityId))
        {
            item = FindUnnetworkedActivityDrop(activityId, position);
        }

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

    private void ApplyPendingTreasureBoxStates()
    {
        if (IsHost || _openedTreasureBoxRewards.Count == 0)
        {
            return;
        }

        foreach (var pair in _openedTreasureBoxRewards)
        {
            if (FindActivityObject(pair.Key) is TreasureBox box && !box.IsOpen)
            {
                box.OpenWithReward(pair.Value);
            }
        }
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
            _liquidStrokeHistory.Clear();
            _seenLiquidStrokeIds.Clear();
            _liquidStrokeIdOrder.Clear();
            _appliedLiquidStrokeIds.Clear();
            _liquidStrokeThrottles.Clear();
            _broadcastClearedRooms.Clear();
            _hostClearedRooms.Clear();
            _openedTreasureBoxRewards.Clear();
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
        _knownHostFloor = 0;
        _locallyOwnedFloor = 0;
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
        _openedTreasureBoxRewards.Clear();
        _pendingSharedShopStates.Clear();
        _pendingShopTransactions.Clear();
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
        _knownHostFloor = 0;
        _locallyOwnedFloor = 0;
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
        _lastLocalMapKnowledgeKey = string.Empty;
        ClearRemotePlayers();
        _deadEnemyNetworkIds.Clear();
        //房间 ID 每层都会从 0 开始, 清除上层的"已广播清房"记录,
        //避免新楼层同 ID 房间永远无法再同步开门。
        _broadcastClearedRooms.Clear();
        _hostClearedRooms.Clear();
        _openedTreasureBoxRewards.Clear();
        _pendingSharedShopStates.Clear();
        _pendingShopTransactions.Clear();
        _appliedLiquidStrokeIds.Clear();

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
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var player = dungeonManager?.CurrWorld?.Player as Player;
        if (player == null || player.IsDestroyed || string.IsNullOrEmpty(_lastLocalWorldKind))
        {
            return;
        }

        var floor = dungeonManager.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        var animation = player.AnimatedSprite?.Animation.ToString() ?? string.Empty;
        var frame = player.AnimatedSprite?.Frame ?? 0;
        var weaponId = player.WeaponPack?.ActiveItem?.ActivityBase?.Id ?? string.Empty;

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
            player.MeleeAttackTimer > 0 || player.IsAttack && player.AnimatedSprite?.Animation == AnimatorNames.Attack,
            player.MeleeAttackSequence,
            player.AffiliationArea?.RoomInfo?.Id ?? -1,
            floor,
            player.IsDie,
            animation,
            frame,
            player.RoleState.Gold,
            _localGoldRevision,
            weaponId,
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

        var floor = GameApplication.Instance.DungeonManager.CurrentFloor;

        foreach (var role in world.Role_InstanceList)
        {
            if (role == null || role.IsDestroyed || !role.IsAi || role.NetworkId == 0 ||
                role.AnimatedSprite == null || !GodotObject.IsInstanceValid(role.AnimatedSprite))
            {
                continue;
            }

            Rpc(nameof(ReceiveEnemySnapshot), new Variant[]
            {
                role.NetworkId,
                floor,
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
                role.ReplicatedHitSequence,
                role.WeaponPack?.ActiveItem?.ActivityBase?.Id ?? string.Empty,
            });
        }

    }

    public void BroadcastEnemyDeath(Role role)
    {
        if (!IsHost || !IsLanConnected || role == null || role.IsDestroyed || role.NetworkId == 0 ||
            !role.IsAi || role.AnimatedSprite == null || !GodotObject.IsInstanceValid(role.AnimatedSprite))
        {
            return;
        }

        Rpc(nameof(ReceiveEnemySnapshot), new Variant[]
        {
            role.NetworkId,
            GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 0,
            role.ActivityBase?.Id ?? string.Empty,
            role.GlobalPosition.X,
            role.GlobalPosition.Y,
            (int)role.Face,
            0,
            role.MaxHp,
            role.RoleState.Gold,
            true,
            AnimatorNames.Die.ToString(),
            0,
            role.ReplicatedHitSequence,
            role.WeaponPack?.ActiveItem?.ActivityBase?.Id ?? string.Empty,
        });
    }

    private void BroadcastRoomStates()
    {
        if (!IsHost || !IsLanConnected)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager?.CurrWorld is not Dungeon)
        {
            return;
        }

        foreach (var room in dungeonManager.RoomInfosForNetwork)
        {
            if (room != null)
            {
                var exploredAisles = new Godot.Collections.Array<Variant>();
                foreach (var door in room.Doors)
                {
                    exploredAisles.Add(door.AisleFogMask?.IsExplored == true);
                }

                Rpc(nameof(ReceiveNetworkRoomState), dungeonManager.CurrentFloor, room.Id, room.HasFirstEntered, room.IsSeclusion,
                    room.HasFirstEntered || room.RoomFogMask?.IsExplored == true, exploredAisles);

                //已经清空的房间要把"清空"这件事单独补发一次。
                //
                //【为什么要补发】房主开门的事实由房间状态(isSeclusion=false)表达, 但访客只有在
                //执行过自己的本地清房流程(RoomInfo.OnClearRoom / ForceNetworkClear)之后才认这个状态 ——
                //见 RoomPreinstall.HasLivingEnemy。新加入的访客本地房间还没清过, 会一直把自己当成
                //"战斗中的闭关房间", 于是门不开、地图上也看不到已探索的房间。
                if (room.HasFirstEntered && !room.IsSeclusion && _hostClearedRooms.Contains(room.Id))
                {
                    Rpc(nameof(ReceiveRoomCleared), dungeonManager.CurrentFloor, _sessionRevision, room.Id, false);
                }
            }
        }

        foreach (var pair in _openedTreasureBoxRewards)
        {
            Rpc(nameof(ReceiveTreasureBoxOpened), pair.Key, pair.Value);
        }

        BroadcastAllSharedShopStates();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveNetworkRoomState(int floor, int roomId, bool hasFirstEntered, bool isSeclusion,
        bool roomExplored, Godot.Collections.Array<Variant> exploredAisles)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (!IsHost && dungeonManager?.CurrWorld is Dungeon && dungeonManager.CurrentFloor == floor)
        {
            dungeonManager.ApplyNetworkRoomState(
                roomId, hasFirstEntered, isSeclusion, roomExplored, exploredAisles);
        }
    }

    /// <summary>
    /// 访客把新发现的地图区域发给房主; 房主只合并已探索标记, 再通过房间状态广播给全队。
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveMapExploration(int floor, Godot.Collections.Array<Variant> packet)
    {
        if (!IsHost || !IsLanConnected || packet == null)
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId <= 1)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager?.CurrWorld is not Dungeon || dungeonManager.CurrentFloor != floor)
        {
            return;
        }

        foreach (var rowValue in packet)
        {
            var row = rowValue.AsGodotArray();
            if (row.Count < 3)
            {
                continue;
            }

            var roomId = (int)row[0];
            var roomExplored = row[1].AsBool();
            var doorCount = Math.Clamp((int)row[2], 0, row.Count - 3);
            var exploredAisles = new Godot.Collections.Array<Variant>();
            for (var i = 0; i < doorCount; i++)
            {
                exploredAisles.Add(row[i + 3].AsBool());
            }

            dungeonManager.MergeNetworkMapExploration(roomId, roomExplored, exploredAisles);
        }
    }

    private void SyncLocalMapExploration(double delta)
    {
        if (IsHost || !IsLanConnected ||
            GameApplication.Instance?.DungeonManager is not { CurrWorld: Dungeon } dungeonManager)
        {
            return;
        }

        _mapExplorationTimer -= delta;
        if (_mapExplorationTimer > 0)
        {
            return;
        }

        _mapExplorationTimer = 0.25;
        var key = MakeLocalMapKnowledgeKey(dungeonManager);
        if (key == _lastLocalMapKnowledgeKey)
        {
            return;
        }

        _lastLocalMapKnowledgeKey = key;
        var packet = new Godot.Collections.Array<Variant>();
        foreach (var room in dungeonManager.RoomInfosForNetwork)
        {
            if (room == null)
            {
                continue;
            }

            var row = new Godot.Collections.Array<Variant>
            {
                room.Id,
                room.RoomFogMask?.IsExplored == true,
                room.Doors.Count,
            };
            foreach (var door in room.Doors)
            {
                row.Add(door.AisleFogMask?.IsExplored == true);
            }
            packet.Add(row);
        }

        RpcId(1, nameof(ReceiveMapExploration), dungeonManager.CurrentFloor, packet);
    }

    private static string MakeLocalMapKnowledgeKey(DungeonManager dungeonManager)
    {
        var key = new StringBuilder();
        foreach (var room in dungeonManager.RoomInfosForNetwork)
        {
            if (room == null)
            {
                continue;
            }

            key.Append(room.Id).Append(':')
                .Append(room.RoomFogMask?.IsExplored == true ? '1' : '0');
            foreach (var door in room.Doors)
            {
                key.Append(door.AisleFogMask?.IsExplored == true ? '1' : '0');
            }
            key.Append(';');
        }

        return key.ToString();
    }

    public void BroadcastRoomCleared(int roomId)
    {
        if (!IsHost || !IsLanConnected || !_broadcastClearedRooms.Add(roomId))
        {
            return;
        }

        //记录房主已经清空的房间。后进/重连的访客加入时房间状态里已经是"未闭关",
        //它的本地房间却还停在"有敌人"的状态, 于是永远等不到那条清房 RPC。加入时补发一次。
        _hostClearedRooms.Add(roomId);
        var floor = GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 0;
        Rpc(nameof(ReceiveRoomCleared), floor, _sessionRevision, roomId, true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveRoomCleared(int floor, int sessionRevision, int roomId, bool showNotification)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (IsHost || IsLocalDungeonAuthority || dungeonManager?.CurrWorld is not Dungeon ||
            dungeonManager.CurrentFloor != floor ||
            sessionRevision != _sessionRevision)
        {
            return;
        }

        dungeonManager.ApplyNetworkRoomCleared(roomId, showNotification);
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
            if (state.Floor == dungeonManager.CurrentFloor && state.RoomId >= 0)
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
        if (!IsLanConnected || IsHost || IsLocalDungeonAuthority || networkId == 0)
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

    public bool IsRemotePlayer(Player player)
    {
        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player == player)
            {
                return true;
            }
        }

        return false;
    }

    public Player FindCoopTarget(AffiliationArea area, Vector2 position)
    {
        if (!IsHost || area == null)
        {
            return null;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var currentFloor = dungeonManager?.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        Player result = null;
        var bestDistance = float.MaxValue;
        if (GameApplication.Instance?.DungeonManager?.CurrWorld?.Player is Player host &&
            host.AffiliationArea == area && !host.IsDie)
        {
            result = host;
            bestDistance = position.DistanceSquaredTo(host.GlobalPosition);
        }

        foreach (var state in _remotePlayers.Values)
        {
            if (state.Floor != currentFloor || state.IsDead || state.Player == null ||
                !GodotObject.IsInstanceValid(state.Player) || state.Player.IsDie ||
                state.Player.AffiliationArea != area)
            {
                continue;
            }

            var distance = position.DistanceSquaredTo(state.Player.GlobalPosition);
            if (distance < bestDistance)
            {
                result = state.Player;
                bestDistance = distance;
            }
        }

        return result;
    }

    public bool HasLivingCoopPartner(Player player)
    {
        if (!IsLanConnected || player == null)
        {
            return false;
        }

        var floor = GameApplication.Instance?.DungeonManager?.CurrWorld is Dungeon
            ? GameApplication.Instance.DungeonManager.CurrentFloor
            : 0;
        return _remotePlayers.Values.Any(state => state.Floor == floor && !state.IsDead &&
            state.Player != null && GodotObject.IsInstanceValid(state.Player) && !state.Player.IsDie);
    }

    public Player FindLivingCoopPartner(Player player)
    {
        if (!IsLanConnected || player == null)
        {
            return null;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var floor = dungeonManager?.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        Player nearest = null;
        var nearestDistanceSquared = float.MaxValue;
        if (dungeonManager?.CurrWorld?.Player is Player localPlayer && localPlayer != player &&
            !localPlayer.IsDie && !localPlayer.IsDestroyed &&
            localPlayer.AffiliationArea == player.AffiliationArea)
        {
            nearest = localPlayer;
            nearestDistanceSquared = player.GlobalPosition.DistanceSquaredTo(localPlayer.GlobalPosition);
        }

        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player == null || !GodotObject.IsInstanceValid(state.Player) || state.IsDead ||
                state.Player.IsDie || state.Floor != floor || state.Player.AffiliationArea != player.AffiliationArea)
            {
                continue;
            }

            var distanceSquared = player.GlobalPosition.DistanceSquaredTo(state.Player.GlobalPosition);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = state.Player;
                nearestDistanceSquared = distanceSquared;
            }
        }

        if (nearest != null)
        {
            return nearest;
        }

        foreach (var state in _remotePlayers.Values)
        {
            if (state.Floor != floor || state.IsDead || state.Player == null ||
                !GodotObject.IsInstanceValid(state.Player) || state.Player.IsDie)
            {
                continue;
            }

            var distanceSquared = player.GlobalPosition.DistanceSquaredTo(state.Player.GlobalPosition);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = state.Player;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    private void UpdateCoopDefeatSettlement(double delta)
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        var player = world?.Player as Player;
        if (world is not Dungeon || player == null || !player.IsDie)
        {
            _defeatSettlementPlayer = null;
            _coopDefeatTimer = -1;
            _coopDefeatSettlementShown = false;
            return;
        }

        if (!player.HasCompletedDeathSequence)
        {
            _defeatSettlementPlayer = player;
            _coopDefeatTimer = 0;
            return;
        }

        if (HasLivingCoopPartner(player))
        {
            _defeatSettlementPlayer = player;
            _coopDefeatTimer = 0;
            return;
        }

        if (_defeatSettlementPlayer != player)
        {
            _defeatSettlementPlayer = player;
            _coopDefeatTimer = 0;
        }

        _coopDefeatTimer += delta;
        if (_coopDefeatTimer < 0.5 || _coopDefeatSettlementShown)
        {
            return;
        }

        _coopDefeatSettlementShown = true;
        world.Pause = true;
        UiManager.Open_Game_Settlement();
    }

    public void RequestEnemyDamageFromBullet(IHurt hurt, List<AttackStats> damages,
        List<AbnormalData> abnormals, float angle)
    {
        if (hurt?.GetActivityObject() is not Role enemy || enemy.NetworkId == 0)
        {
            return;
        }

        RequestEnemyDamage(enemy.NetworkId, damages, abnormals, angle);
    }

    public void BroadcastRemotePlayerDamage(Player player, Role source,
        List<AttackStats> damages, List<AbnormalData> abnormals, float angle)
    {
        if (!IsHost || player == null || source == null || source.IsDie)
        {
            return;
        }

        long peerId = 0;
        RemotePlayerState targetState = null;
        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player == player)
            {
                peerId = state.PeerId;
                targetState = state;
                break;
            }
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var sourceFloor = dungeonManager?.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        if (dungeonManager?.CurrWorld is not Dungeon || peerId <= 1 ||
            targetState == null || targetState.Floor != sourceFloor ||
            player.IsDie || player.HurtArea == null)
        {
            return;
        }

        //房主端也要更新访客代理的生命状态, 保持房主的目标选择和死亡判断与访客一致。
        player.HurtArea.Hurt(source, damages, abnormals, angle, true);

        var damagePacket = new Godot.Collections.Array<Variant>();
        foreach (var damage in damages ?? new List<AttackStats>())
        {
            damagePacket.Add(damage.BaseDamage);
            damagePacket.Add((int)damage.Type);
            damagePacket.Add(damage.CritRate);
            damagePacket.Add(damage.CritBonus);
            damagePacket.Add(damage.CritArmorPenetration);
        }

        var abnormalPacket = new Godot.Collections.Array<Variant>();
        foreach (var abnormal in abnormals ?? new List<AbnormalData>())
        {
            abnormalPacket.Add((int)abnormal.Type);
            abnormalPacket.Add(abnormal.Value);
        }

        RpcId(peerId, nameof(ReceiveRemotePlayerDamage), _nextDamageSequence++, sourceFloor,
            source.NetworkId, damagePacket, abnormalPacket, angle);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveRemotePlayerDamage(long damageSequence, int sourceFloor, long sourceNetworkId,
        Godot.Collections.Array<Variant> damagePacket, Godot.Collections.Array<Variant> abnormalPacket, float angle)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var localFloor = dungeonManager?.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        if (IsHost || damagePacket == null || dungeonManager?.CurrWorld is not Dungeon ||
            localFloor != sourceFloor || damageSequence <= _lastAppliedDamageSequence)
        {
            return;
        }

        _lastAppliedDamageSequence = damageSequence;
        if (_deadEnemyNetworkIds.Contains(sourceNetworkId))
        {
            return;
        }

        var player = GameApplication.Instance?.DungeonManager?.CurrWorld?.Player;
        if (player == null || player.IsDestroyed || player.HurtArea == null)
        {
            return;
        }

        var damages = new List<AttackStats>();
        for (var i = 0; i + 4 < damagePacket.Count; i += 5)
        {
            damages.Add(new AttackStats((int)damagePacket[i], (DamageType)(int)damagePacket[i + 1],
                (float)damagePacket[i + 2], (float)damagePacket[i + 3], (float)damagePacket[i + 4]));
        }

        var abnormals = new List<AbnormalData>();
        for (var i = 0; i + 1 < abnormalPacket.Count; i += 2)
        {
            abnormals.Add(new AbnormalData((AbnormalStateType)(int)abnormalPacket[i], (int)abnormalPacket[i + 1]));
        }

        player.HurtArea.Hurt(null, damages, abnormals, angle);
    }

    public void OnPlayerBulletFired(Role shooter, IBullet bullet)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var localPlayer = dungeonManager?.CurrWorld?.Player;
        if (!IsLanConnected || shooter != localPlayer || bullet?.BulletData?.BulletBase == null ||
            !IsNetworkVisualBullet(bullet.BulletData.BulletBase))
        {
            return;
        }

        if (dungeonManager.CurrWorld is not Dungeon)
        {
            return;
        }

        var floor = dungeonManager.CurrentFloor;

        var data = bullet.BulletData;
        if (IsHost)
        {
            Rpc(nameof(ReceivePlayerBulletVisual), LocalPeerId, floor, data.BulletBase.Id,
                data.Position.X, data.Position.Y, data.Rotation, data.Altitude, data.FlySpeed,
                data.VerticalSpeed, data.MaxDistance, data.LifeTime);
        }
        else
        {
            RpcId(1, nameof(RequestPlayerBulletVisual), floor, data.BulletBase.Id,
                data.Position.X, data.Position.Y, data.Rotation, data.Altitude, data.FlySpeed,
                data.VerticalSpeed, data.MaxDistance, data.LifeTime);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestPlayerBulletVisual(int sourceFloor, string bulletId, float x, float y, float rotation,
        float altitude, float flySpeed, float verticalSpeed, float maxDistance, float lifeTime)
    {
        if (!IsHost || !ExcelConfig.BulletBase_Map.TryGetValue(bulletId, out var bulletBase) ||
            !IsNetworkVisualBullet(bulletBase))
        {
            return;
        }

        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId <= 1 || !_remotePlayers.TryGetValue(senderId, out var state) ||
            state.Player == null || !GodotObject.IsInstanceValid(state.Player) ||
            sourceFloor <= 0 || state.Floor != sourceFloor)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager?.CurrWorld is Dungeon && dungeonManager.CurrentFloor == sourceFloor)
        {
            SpawnNetworkVisualBullet(senderId, bulletBase, new Vector2(x, y), rotation,
                altitude, flySpeed, verticalSpeed, maxDistance, lifeTime);
        }

        // 房主即使在其他楼层也要继续做中继, 但是否显示由接收端按楼层过滤。
        Rpc(nameof(ReceivePlayerBulletVisual), senderId, sourceFloor, bulletId, x, y, rotation,
            altitude, flySpeed, verticalSpeed, maxDistance, lifeTime);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceivePlayerBulletVisual(long sourcePeerId, int sourceFloor, string bulletId, float x, float y,
        float rotation, float altitude, float flySpeed, float verticalSpeed, float maxDistance, float lifeTime)
    {
        if (IsHost || sourcePeerId == LocalPeerId ||
            !ExcelConfig.BulletBase_Map.TryGetValue(bulletId, out var bulletBase) ||
            !IsNetworkVisualBullet(bulletBase))
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (sourceFloor <= 0 || dungeonManager?.CurrWorld is not Dungeon ||
            dungeonManager.CurrentFloor != sourceFloor)
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

        if (bulletBase.Type == 2)
        {
            var visualLaser = ObjectManager.GetLaser(bulletBase.Prefab);
            visualLaser.NetworkVisualOnly = true;
            visualLaser.AddToActivityRoot(RoomLayerEnum.YSortLayer);
            visualLaser.InitData(data, shooter.Camp);
        }
        else
        {
            var visualBullet = ObjectManager.GetBullet(bulletBase.Prefab);
            visualBullet.NetworkVisualOnly = true;
            visualBullet.InitData(data, shooter.Camp);
        }
    }

    private static bool IsNetworkVisualBullet(ExcelConfig.BulletBase bulletBase)
    {
        return bulletBase != null && (bulletBase.Type == 1 || bulletBase.Type == 2);
    }

    private void UpdateRemotePlayers(float delta)
    {
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var localFloor = dungeonManager?.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        foreach (var state in _remotePlayers.Values)
        {
            if (state.Player == null || !GodotObject.IsInstanceValid(state.Player))
            {
                state.Player = null;
                continue;
            }

            state.Player.Visible = state.Floor == localFloor;
            if (!state.Player.Visible)
            {
                continue;
            }

            var blend = Mathf.Clamp(delta * 18f, 0f, 1f);
            state.Player.GlobalPosition = state.Player.GlobalPosition.Lerp(state.TargetPosition, blend);
            state.Player.Face = state.Face;
            state.Player.MountPoint?.ApplyNetworkRotation(state.AimRotationDegrees);

            if (state.IsDead)
            {
                state.Player.ApplyReplicatedPlayerState(true, state.DeathAnimation, state.DeathFrame);
                continue;
            }

            if (state.Player.AnimatedSprite != null)
            {
                var animation = state.IsMeleeAttacking
                    ? AnimatorNames.Attack
                    : state.IsRolling
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

    private void SyncRemotePlayerWeapon(RemotePlayerState state)
    {
        if (state.Player == null || !GodotObject.IsInstanceValid(state.Player) ||
            state.Player.WeaponPack == null)
        {
            return;
        }

        var currentId = state.Player.WeaponPack.ActiveItem?.ActivityBase?.Id ?? string.Empty;
        if (currentId == state.WeaponId)
        {
            return;
        }

        var previousWeapons = state.Player.WeaponPack.GetAndClearItem();
        state.Player.WeaponPack.ActiveItem = null;
        foreach (var previousWeapon in previousWeapons)
        {
            previousWeapon.Destroy();
        }

        if (string.IsNullOrEmpty(state.WeaponId))
        {
            return;
        }

        var weapon = ActivityObject.Create<Weapon>(state.WeaponId);
        if (weapon == null)
        {
            GD.PushWarning($"联机远程武器同步失败: {state.WeaponId}");
            return;
        }

        weapon.Collision.Disabled = true;
        state.Player.WeaponPack.PickupItem(weapon, true);
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
        BroadcastSharedShopStatesToPeer(peerId);
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
        //楼层和种子是每个玩家本地推进的状态，不能用它们触发全队换层。
        //世界类型、地牢组、模式和会话版本变化时，才需要重建会话。
        return $"{state[0]}|{state[1]}|{state[3]}|{state[6]}";
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

        var previousSessionRevision = _sessionRevision;
        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var preserveLocalFloor = worldKind == "dungeon" &&
                                 dungeonManager?.CurrWorld is Dungeon &&
                                 _lastRequestedStateKey.Length > 0 &&
                                 previousSessionRevision == sessionRevision &&
                                 dungeonManager.CurrentFloor != floor;

        _sessionRevision = sessionRevision;
        _knownHostFloor = worldKind == "dungeon" ? floor : 0;

        if (preserveLocalFloor)
        {
            // 同一局中房主和访客可以处于不同楼层, 房主的新快照不能把访客拉回去。
            _pendingRemoteState = null;
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
        bool isRolling, bool isMeleeAttacking, long meleeAttackSequence, int roomId, int floor,
        bool isDead, string animation, int frame, int gold, long goldRevision, string weaponId = "")
    {
        var senderId = Multiplayer.GetRemoteSenderId();
        if (senderId > 1)
        {
            peerId = senderId;
        }

        if (!IsHost && peerId == 1)
        {
            _knownHostFloor = floor;
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

        var previousFloor = state.Floor;
        var previousRoomId = state.RoomId;
        var newPosition = new Vector2(x, y);
        state.IsMoving = state.TargetPosition.DistanceTo(newPosition) > Mathf.Max(0.25f, 4f * elapsed);
        state.IsRolling = isRolling;
        state.IsMeleeAttacking = isMeleeAttacking;
        state.Floor = floor;
        state.IsDead = isDead;
        state.DeathAnimation = animation ?? string.Empty;
        state.DeathFrame = frame;
        if (state.MeleeAttackSequence != meleeAttackSequence)
        {
            state.MeleeAttackSequence = meleeAttackSequence;
            state.Player?.PlayReplicatedMeleeAttack();
        }
        state.TargetPosition = newPosition;
        state.Face = (FaceDirection)Mathf.Clamp(faceValue, (int)FaceDirection.Left, (int)FaceDirection.Right);
        state.AimRotationDegrees = aimRotationDegrees;
        state.WeaponId = weaponId ?? string.Empty;
        if (goldRevision >= state.GoldRevision)
        {
            state.Gold = Math.Max(0, gold);
            state.GoldRevision = goldRevision;
        }
        EnsureRemotePlayer(state);
        SyncRemotePlayerWeapon(state);
        if (state.Player != null && state.GoldRevision == goldRevision)
        {
            state.Player.RoleState.Gold = state.Gold;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        var localFloor = dungeonManager?.CurrWorld is Dungeon ? dungeonManager.CurrentFloor : 0;
        if (state.Player != null)
        {
            state.Player.Visible = floor == localFloor;
            if (state.Player.Visible && isDead)
            {
                state.Player.ApplyReplicatedPlayerState(true, state.DeathAnimation, state.DeathFrame);
            }
        }

        if (IsHost && senderId > 1 && dungeonManager?.CurrWorld is Dungeon)
        {
            if (previousRoomId >= 0 &&
                (previousFloor != floor || roomId < 0 ||
                 (floor == dungeonManager.CurrentFloor && previousRoomId != roomId)))
            {
                dungeonManager.OnRemotePlayerLeaveRoom(state.Player);
            }

            if (floor == dungeonManager.CurrentFloor && roomId >= 0 &&
                (previousFloor != floor || previousRoomId != roomId))
            {
                dungeonManager.OnRemotePlayerEnterRoom(roomId, state.Player);
                if (!isDead)
                {
                    ForcePartyIntoRoom(roomId, newPosition, senderId);
                }
            }
        }
        state.RoomId = roomId;

        if (IsHost && senderId > 1 && roomId >= 0 &&
            (previousFloor != floor || previousRoomId != roomId))
        {
            SendLiquidHistoryToPeer(senderId, floor, roomId);
        }

        // ENet 的快照先发到房主, 再由房主转发给其他客户端。
        if (IsHost && senderId > 1)
        {
            Rpc(nameof(ReceivePlayerSnapshot), new Variant[]
            {
                peerId, x, y, faceValue, aimRotationDegrees, elapsed, isRolling, isMeleeAttacking,
                meleeAttackSequence, roomId, floor, isDead, animation ?? string.Empty, frame,
                state.Gold, state.GoldRevision,
                state.WeaponId,
            });
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveEnemySnapshot(long networkId, int floor, string activityId, float x, float y, int faceValue, int hp,
        int maxHp, int gold, bool isDead, string animation, int frame, long hitSequence, string weaponId)
    {
        if (IsHost)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance?.DungeonManager;
        if (dungeonManager?.CurrWorld is not Dungeon || IsLocalDungeonAuthority ||
            dungeonManager.CurrentFloor != floor)
        {
            return;
        }

        if (isDead || hp <= 0)
        {
            //先登记终态, 再处理精灵/死亡动画。后续到达的旧伤害 RPC 不得在尸体消失后继续扣盾。
            _deadEnemyNetworkIds.Add(networkId);
        }

        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        var role = world?.Role_InstanceList.Find(item => item.NetworkId == networkId);
        if ((role == null || role.IsDestroyed) && !string.IsNullOrEmpty(activityId))
        {
            // 兼容修复前已经在访客端本地生成的副本：优先接管同房间、同资源的
            // 无 NetworkId 敌人，避免快照到达后再叠加第二只 Boss。
            var snapshotPosition = new Vector2(x, y);
            var nearestDistanceSquared = 256f * 256f;
            if (world != null)
            {
                foreach (var candidate in world.Role_InstanceList)
                {
                    if (candidate.IsDestroyed || candidate.NetworkId != 0 ||
                        candidate.ActivityBase?.Id != activityId ||
                        candidate.AffiliationArea?.RoomInfo == null)
                    {
                        continue;
                    }

                    var distanceSquared = candidate.GlobalPosition.DistanceSquaredTo(snapshotPosition);
                    if (distanceSquared <= nearestDistanceSquared)
                    {
                        nearestDistanceSquared = distanceSquared;
                        role = candidate;
                    }
                }
            }

            if (role != null)
            {
                role.NetworkId = networkId;
                role.EnableCustomBehavior = false;
            }
        }

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
        SyncReplicatedEnemyWeapon(role, weaponId);
        role.ApplyReplicatedEnemyState(hp, maxHp, gold, isDead, animation, frame, hitSequence);
    }

    private void SyncReplicatedEnemyWeapon(Role role, string weaponId)
    {
        if (role?.IsAi != true || role.WeaponPack == null)
        {
            return;
        }

        weaponId ??= string.Empty;
        var currentWeaponId = role.WeaponPack.ActiveItem?.ActivityBase?.Id ?? string.Empty;
        if (currentWeaponId == weaponId)
        {
            return;
        }

        var previousWeapons = role.WeaponPack.GetAndClearItem();
        role.WeaponPack.ActiveItem = null;
        foreach (var previousWeapon in previousWeapons)
        {
            previousWeapon.Destroy();
        }

        if (string.IsNullOrEmpty(weaponId))
        {
            return;
        }

        var weapon = ActivityObject.Create<Weapon>(weaponId);
        if (weapon == null)
        {
            GD.PushWarning($"联机敌人武器同步失败: {weaponId}");
            return;
        }

        weapon.Collision.Disabled = true;
        if (role.WeaponPack.PickupItem(weapon, true) < 0)
        {
            weapon.Destroy();
            GD.PushWarning($"联机敌人无法装备武器: {weaponId}");
        }
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
            playerState.Floor != GameApplication.Instance?.DungeonManager?.CurrentFloor ||
            playerState.RoomId < 0 || target.AffiliationArea?.RoomInfo?.Id != playerState.RoomId ||
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

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
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

        var showFloorNotification = dungeonManager.CurrWorld is Dungeon && dungeonManager.CurrentFloor != floor;
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
            if (showFloorNotification)
            {
                dungeonManager.ShowFloorNotification();
            }
            CallDeferred(nameof(ApplyPendingRemoteState));
        });

        if (dungeonManager.CurrWorld is Hall)
        {
            dungeonManager.ExitHall(true, loadDungeon);
        }
        else if (dungeonManager.CurrWorld is Dungeon)
        {
            // 房主切换楼层时, 客户端重建自己的本地世界。
            dungeonManager.ExitDungeon(true, loadDungeon);
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
