
using System.Collections.Generic;
using Godot;

/// <summary>
/// 金币类
/// </summary>
public partial class Gold : ActivityObject, IPoolItem
{
    /// <summary>
    /// 金币数量
    /// </summary>
    [Export]
    public int GoldCount { get; set; } = 1;

    public bool IsRecycled { get; set; }
    public string Logotype { get; set; }
    
    private float _maxSpeed = 250;
    private float _speed = 0;
    private Role _moveTarget;
    private bool _networkClaimPending;
    
    public override void OnInit()
    {
        DefaultLayer = RoomLayerEnum.YSortLayer;
    }

    protected override void OnThrowOver()
    {
        var current = World.Player;
        if (current != null)
        {
            this.CallDelay(0.3f, () =>
            {
                _moveTarget = current;
                MoveController.Enable = false;
            });
        }
    }

    protected override void Process(float delta)
    {
        var network = LanNetworkManager.Instance;
        if (network != null && network.IsLanConnected)
        {
            var player = World.Player;
            if (player != null && GlobalPosition.DistanceSquaredTo(player.GlobalPosition) <= 18f * 18f &&
                !_networkClaimPending)
            {
                _networkClaimPending = true;
                network.RequestGoldPickup(this);
            }
            return;
        }

        if (_moveTarget != null && !_moveTarget.IsDestroyed)
        {
            var position = Position;
            var targetPosition = _moveTarget.Position;
            if (position.DistanceSquaredTo(targetPosition) < 3 * 3)
            {
                _moveTarget.AddGold(GoldCount);
                ObjectPool.Reclaim(this);
            }
            else
            {
                _speed = Mathf.MoveToward(_speed, _maxSpeed, _maxSpeed * delta);
                Position = position.MoveToward(targetPosition, _speed * delta);
            }
        }
    }
    
    public void OnReclaim()
    {
        GetParent().RemoveChild(this);
        _moveTarget = null;
    }

    public void OnLeavePool()
    {
        _speed = 0;
        _networkClaimPending = false;
        NetworkId = 0;
        MoveController.Enable = true;
        MoveController.ClearForce();
        MoveController.SetAllVelocity(Vector2.Zero);
    }
    
    /// <summary>
    /// 创建散落的金币
    /// </summary>
    /// <param name="position">位置</param>
    /// <param name="count">金币数量</param>
    /// <param name="force">投抛力度</param>
    public static List<Gold> CreateGold(Vector2 position, int count, int force = 10)
    {
        var list = new List<Gold>();
        var goldList = Utils.GetGoldList(count);
        foreach (var id in goldList)
        {
            var o = ObjectManager.GetActivityObject<Gold>(id);
            o.Position = position;
            var network = LanNetworkManager.Instance;
            if (network != null && network.IsHost && network.IsLanConnected)
            {
                o.InitNetworkDrop(position);
                network.BroadcastNetworkGoldSpawn(o);
            }
            else
            {
                o.Throw(0,
                    Utils.Random.RandomRangeInt(5 * force, 11 * force),
                    new Vector2(Utils.Random.RandomRangeInt(-2 * force, 2 * force), Utils.Random.RandomRangeInt(-2 * force, 2 * force)),
                    0
                );
            }
            list.Add(o);
        }

        return list;
    }

    public void InitNetworkDrop(Vector2 position)
    {
        PutDown(position, RoomLayerEnum.YSortLayer, false);
        _moveTarget = World.Player;
        _networkClaimPending = false;
    }

    public void ReclaimNetworkDrop()
    {
        ObjectPool.Reclaim(this);
    }

    public void ResetNetworkClaimPending()
    {
        _networkClaimPending = false;
    }
}
