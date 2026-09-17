
using System.Collections.Generic;
using Godot;

/// <summary>
/// 默认实现地牢房间规则
/// </summary>
public class DefaultDungeonRule : DungeonRule
{
    //用于排除上一级房间
    private List<RoomInfo> excludePrevRoom = new List<RoomInfo>();
    
    //战斗房间尝试链接次数
    private int battleTryCount = 0;
    private int battleMaxTryCount = 3;

    //结束房间尝试链接次数
    private int outletTryCount = 0;
    //奖励房间绑定的上一个房间
    private List<RoomInfo> rewardBindRoom = new List<RoomInfo>();
    
    private readonly RoomDirection[] _roomDirections = new []{ RoomDirection.Up, RoomDirection.Down, RoomDirection.Left, RoomDirection.Right };
    
    public DefaultDungeonRule(DungeonGenerator generator) : base(generator)
    {
    }

    public override bool CanOverGenerator()
    {
        if (Config.IsBossRatioMode)
        {
            //按比例刷 Boss 时, Boss 房是【替换】战斗房, 它加到的是 BossRoomInfos 而不是 BattleRoomInfos。
            //如果这里只数 BattleRoomInfos, 那么每刷一个 Boss 房, 战斗房计数就不动,
            //CanOverGenerator() 会一直是 false, 生成循环(DungeonGenerator.Generate 里的 while)
            //会一直跑到 1000 次尝试上限, 结果是地牢残缺或者卡住。
            //所以必须把两者加起来算"已完成的战斗类房间"。
            var combatDone = Generator.BattleRoomInfos.Count + Generator.BossRoomInfos.Count;

            //除了战斗房够了, 房间总数也不能超 —— 否则如果奖励房/商店因为模板缺失被跳过,
            //生成器会一路补战斗房补到 1000 次上限。
            var totalRooms = 1 + Config.BattleRoomCount + Config.RewardRoomCount + Config.ShopRoomCount + 1;
            var totalDone = Generator.RoomInfos.Count >= totalRooms;

            return (combatDone >= Config.BattleRoomCount || totalDone)
                   && Generator.EndRoomInfos.Count >= Config.OutRoomCount;
        }

        return Generator.BattleRoomInfos.Count >= Config.BattleRoomCount && Generator.EndRoomInfos.Count >= Config.OutRoomCount;
    }

    /// <summary>
    /// 当前是否应该把下一个战斗房换成 Boss 房(按比例模式)。
    ///
    /// 【踩坑记录】一开始用 Generator.RoomInfos.Count(绝对房间号)取模, 结果是 33% 而不是 70%:
    /// 因为房间序号里夹着奖励房和商店, 拿它取模会打乱相位。
    /// 比如 every=2 时会得到 Battle,Boss,Battle,Boss... 再被奖励/商店错开, 实际只有 1/3 是 Boss。
    /// 正确做法是用【已生成的战斗类房间数】当序号, 这样 12 个战斗格里刚好 70% 是 Boss。
    /// </summary>
    private bool ShouldPlaceBossAsCombatRoom()
    {
        if (!Config.IsBossRatioMode)
        {
            return false;
        }

        if (RoomGroup.BossList.Count == 0)
        {
            //这一组没有配置 Boss 房模板, 放不了
            return false;
        }

        var ratio = Mathf.Clamp(Config.BossRoomRatio, 1, 100);
        // 100% 就是每个战斗格都放 Boss
        if (ratio >= 100)
        {
            return true;
        }

        //已生成的战斗类房间数(含已经放下的 Boss 房) —— 这就是"第几个战斗格"
        var combatIndex = Generator.BattleRoomInfos.Count + Generator.BossRoomInfos.Count;

        //每 floor(100/ratio) 个战斗格放一个 Boss, 剩下的按相位补齐, 使总数贴近目标比例。
        //例: ratio=70 -> every=1, phase 需要让 10 个里出 7 个 -> 用 10 格周期。
        //这里用更直白的做法: 把比例换算成"每 100 个战斗格放 ratio 个",
        //用 (combatIndex * ratio) % 100 落在 [0, ratio) 区间判断, 天然就是给定的比例。
        return (combatIndex * ratio) % 100 < ratio;
    }

    /// <summary>
    /// 按比例模式下的"战斗类房间"类型: Boss 或普通战斗
    /// </summary>
    private DungeonRoomType GetCombatRoomType()
    {
        return ShouldPlaceBossAsCombatRoom() ? DungeonRoomType.Boss : DungeonRoomType.Battle;
    }

    public override RoomInfo GetConnectPrevRoom(RoomInfo prevRoom, DungeonRoomType nextRoomType)
    {
        if (nextRoomType == DungeonRoomType.Inlet)
        {
            return null;
        }
        else if (nextRoomType == DungeonRoomType.Boss)
        {
            //【踩坑记录】原来是这么写的:
            //    return Generator.FindMaxLayerRoom(DungeonRoomType.Battle | DungeonRoomType.Shop, excludePrevRoom);
            // 它假设 Boss 房是"终结点", 只可能挂在战斗房/商店后面。
            // 但魔王模式要一连串 Boss 房, 第二个 Boss 房的父节点是上一个 Boss 房,
            // FindMaxLayerRoom 在 Battle|Shop 里找不到可用房间就返回 null, 生成直接失败,
            // 重试到上限就报"生成房间尝试次数过多"。
            //
            // 所以按比例模式下, Boss 房要能和普通战斗房用同一套父节点选择逻辑。
            if (Config.IsBossRatioMode)
            {
                if (battleTryCount < battleMaxTryCount)
                {
                    if (prevRoom == null || prevRoom.Layer >= Config.MaxLayer - 1)
                    {
                        return Generator.RandomRoomLessThanLayer(
                            DungeonRoomType.Battle | DungeonRoomType.Boss | DungeonRoomType.Shop | DungeonRoomType.Event | DungeonRoomType.Inlet,
                            Mathf.Max(1, Config.MaxLayer / 2));
                    }

                    return prevRoom;
                }

                return Generator.GetRandomRoom(
                    DungeonRoomType.Battle | DungeonRoomType.Boss | DungeonRoomType.Shop | DungeonRoomType.Event | DungeonRoomType.Inlet);
            }

            return Generator.FindMaxLayerRoom(DungeonRoomType.Battle | DungeonRoomType.Shop, excludePrevRoom);
        }
        else if (nextRoomType == DungeonRoomType.Outlet || nextRoomType == DungeonRoomType.Shop || nextRoomType == DungeonRoomType.Event)
        {
            return prevRoom;
        }
        else if (nextRoomType == DungeonRoomType.Reward)
        {
            if (Generator.BattleRoomInfos.Count == 0)
            {
                return prevRoom;
            }
            
            foreach (var temp in rewardBindRoom)
            {
                if (!excludePrevRoom.Contains(temp))
                {
                    excludePrevRoom.Add(temp);
                }
            }

            return Generator.FindMaxLayerRoom(DungeonRoomType.Battle | DungeonRoomType.Shop | DungeonRoomType.Event, excludePrevRoom);
        }
        else if (nextRoomType == DungeonRoomType.Battle)
        {
            if (battleTryCount < battleMaxTryCount)
            {
                if (prevRoom == null || prevRoom.Layer >= Config.MaxLayer - 1) //层数太高, 下一个房间生成在低层级
                {
                    return Generator.RandomRoomLessThanLayer(DungeonRoomType.Battle | DungeonRoomType.Shop | DungeonRoomType.Event | DungeonRoomType.Inlet, Mathf.Max(1, Config.MaxLayer / 2));
                }

                return prevRoom;
            }
            return Generator.GetRandomRoom(DungeonRoomType.Battle | DungeonRoomType.Shop | DungeonRoomType.Event | DungeonRoomType.Inlet);
        }
        
        return Generator.GetRandomRoom(DungeonRoomType.None);
    }

    public override DungeonRoomType GetNextRoomType(RoomInfo prevRoom)
    {
        if (Generator.StartRoomInfo == null) //生成第一个房间
        {
            return DungeonRoomType.Inlet;
        }

        //========================= 按比例刷 Boss 模式(魔王模式) =========================
        //不走下面那套"战斗房满了才放一个 Boss"的逻辑, 而是按生成顺序按比例把战斗格换成 Boss。
        //
        //【踩坑记录】第一版是这么写的:
        //    var rewardInterval = Config.BattleRoomCount / (Config.RewardRoomCount + 1);   // 12/3 = 4
        //    if (Generator.RoomInfos.Count % (rewardInterval + 1) == rewardInterval)        // % 5 == 4
        // 结果奖励房和商店【永远不生成】: RoomInfos.Count 从 1 开始递增, %5 只会得到 1,2,3,4,0,
        // 第一次能等于 4 的时候 Count 已经是 4 了, 而那个位置早就过去了。
        // 危害不只是"没有奖励房" —— 不走奖励房/商店, 那些格子就全变成 Battle 房,
        // 而 BattleRoomInfos 增长慢(Boss 房不计数), 生成循环会一直补房间,
        // 补到 1000 次尝试上限, 地牢里就冒出一堆普通敌人房。
        //
        // 现在改成: 先把房间总数定下来, 按房间序号均匀插入奖励房/商店, 其余是 Boss 房。
        if (Config.IsBossRatioMode)
        {
            //战斗类房间(包含 Boss 房)的目标数量
            var combatTarget = Config.BattleRoomCount;

            //总房间数 = 起始房 1 + 战斗类 combatTarget + 奖励 + 商店 + 结束房 1
            var totalRooms = 1 + combatTarget + Config.RewardRoomCount + Config.ShopRoomCount + 1;

            //用【绝对房间序号】判断, 避免上面那个取模相位错误。
            //RoomInfos.Count 就是"下一个要生成的房间"的序号(起始房已经生成时为 1)。
            var roomNo = Generator.RoomInfos.Count;

            //出口房恰好放在最后一个位置, 整个流程只会有一次 roomNo == outletAt。
            //
            //【踩坑记录】原来这里写的是:
            //    var combatDone = BattleRoomInfos.Count + BossRoomInfos.Count;
            //    if (combatDone >= combatTarget || roomNo >= totalRooms - 1) return Outlet;
            // 看上去没问题, 实际会死循环: 战斗房凑够之后这里【永远】返回 Outlet,
            // 而出口只能生成一次; 第二次返回 Outlet 时生成器发现没有可用父节点/位置就失败,
            // 于是反复重试同一个 Outlet 直到 1000 次上限 -> 报"生成房间尝试次数过多"。
            // 而且它和 CanOverGenerator 的条件不一致, 那边要求 EndRoomInfos>=1 才收手。
            var outletAt = totalRooms - 1;
            if (roomNo >= outletAt)
            {
                return DungeonRoomType.Outlet;
            }

            if (prevRoom != null)
            {
                //奖励房: 把总长三等分(按"奖励+商店"的总数均分), 但至少间隔 2 个房间。
                //两个奖励房分别落在约 1/3、2/3 处, 中间自然夹着商店/Boss。
                var nonCombatTotal = Config.RewardRoomCount + Config.ShopRoomCount;
                var step = Mathf.Max(2, totalRooms / (nonCombatTotal + 1));

                if (Config.RewardRoomCount > 0
                    && Generator.RewardRoomInfos.Count < Config.RewardRoomCount
                    && RoomGroup.RewardList.Count > 0)
                {
                    //第 n 个奖励房放在 step * (2n-1) 处: n=1 -> step, n=2 -> 3*step
                    var nextRewardAt = step * (2 * Generator.RewardRoomInfos.Count + 1);
                    if (roomNo == nextRewardAt)
                    {
                        return DungeonRoomType.Reward;
                    }
                }

                if (Config.ShopRoomCount > 0
                    && Generator.ShopRoomInfos.Count < Config.ShopRoomCount
                    && RoomGroup.ShopList.Count > 0)
                {
                    //商店放在两个奖励房中间: 2*step
                    var nextShopAt = step * (2 * Generator.ShopRoomInfos.Count + 2);
                    if (roomNo == nextShopAt)
                    {
                        return DungeonRoomType.Shop;
                    }
                }
            }

            return GetCombatRoomType();
        }
        //========================= 以下是默认模式的原有逻辑 =========================

        if (prevRoom != null)
        {
            if (prevRoom.RoomType == DungeonRoomType.Boss) //boss房间后生成结束房间
            {
                return DungeonRoomType.Outlet;
            }

            if (Generator.RewardRoomInfos.Count < Config.RewardRoomCount)
            {
                if (Generator.BattleRoomInfos.Count == Config.BattleRoomCount / (Config.RewardRoomCount + 1) * (Generator.RewardRoomInfos.Count + 1)) //奖励房间
                {
                    if (RoomGroup.RewardList.Count > 0)
                    {
                        return DungeonRoomType.Reward;
                    }

                    return DungeonRoomType.Battle;
                }
            }

            if (Generator.ShopRoomInfos.Count < Config.ShopRoomCount)
            {
                //原本条件
                if (Generator.BattleRoomInfos.Count == Config.BattleRoomCount / (Config.ShopRoomCount + 1) * (Generator.ShopRoomInfos.Count + 1)) //商店
                {
                    if (RoomGroup.ShopList.Count > 0)
                    {
                        return DungeonRoomType.Shop;
                    }
                
                    return DungeonRoomType.Battle;
                }
                else if (Generator.BattleRoomInfos.Count == Config.BattleRoomCount) //商店
                {
                    if (RoomGroup.ShopList.Count > 0)
                    {
                        return DungeonRoomType.Shop;
                    }

                    return DungeonRoomType.Battle;
                }
            }
        }

        if (Generator.BattleRoomInfos.Count >= Config.BattleRoomCount) //战斗房间已满
        {
            if ((Generator.BattleRoomInfos.Count >= Config.BattleRoomCount + 1 && RoomGroup.BossList.Count == 0) ||
                Config.BossRoomCount == 0)
            {
                return DungeonRoomType.Outlet;
            }
            else if (Generator.BossRoomInfos.Count < Config.BossRoomCount) //最后一个房间是boss房间
            {
                if (RoomGroup.BossList.Count == 0) //没有预设boss房间
                {
                    return DungeonRoomType.Battle;
                }

                //生成boss房间
                return DungeonRoomType.Boss;
            }
        }
        return DungeonRoomType.Battle;
    }

    public override void GenerateRoomSuccess(RoomInfo prevRoom, RoomInfo roomInfo)
    {
        if (roomInfo.RoomType == DungeonRoomType.Boss) //boss房间
        {
            roomInfo.CanRollback = true;
            excludePrevRoom.Clear();
        }
        else if (roomInfo.RoomType == DungeonRoomType.Battle)
        {
            battleTryCount = 0;
            battleMaxTryCount = Random.RandomRangeInt(1, 3);
        }
        else if (roomInfo.RoomType == DungeonRoomType.Outlet)
        {
            outletTryCount = 0;
            Generator.SubmitCanRollbackRoom();
        }
        else if (roomInfo.RoomType == DungeonRoomType.Reward)
        {
            rewardBindRoom.Add(prevRoom);
            excludePrevRoom.Clear();
        }

        if (prevRoom != null && prevRoom.CanRollback)
        {
            roomInfo.CanRollback = true;
        }
    }

    public override void GenerateRoomFail(RoomInfo prevRoom, DungeonRoomType roomType)
    {
        if (roomType == DungeonRoomType.Boss || roomType == DungeonRoomType.Reward)
        {
            //生成房间失败
            excludePrevRoom.Add(prevRoom);
            if (excludePrevRoom.Count >= Generator.RoomInfos.Count)
            {
                //全都没找到合适的, 那就再来一遍
                excludePrevRoom.Clear();
            }
        }
        else if (roomType == DungeonRoomType.Outlet)
        {
            outletTryCount++;
            if (outletTryCount >= 3 && prevRoom != null) //生成结束房间失败, 那么只能回滚boss房间
            {
                outletTryCount = 0;
                Generator.RollbackRoom(prevRoom);
            }
        }
        else if (roomType == DungeonRoomType.Battle)
        {
            battleTryCount++;
        }
    }

    public override RoomDirection GetNextRoomDoorDirection(RoomInfo prevRoom, DungeonRoomType roomType)
    {
        return Random.RandomChoose(_roomDirections);
    }

    public override int GetNextRoomInterval(RoomInfo prevRoom, DungeonRoomType roomType, RoomDirection direction)
    {
        return Random.RandomRangeInt(Config.RoomMinInterval, Config.RoomMaxInterval);
    }

    public override int GetNextRoomOffset(RoomInfo prevRoom, DungeonRoomType roomType, RoomDirection direction)
    {
        //为什么最后的值要减4或者5? 因为这个值是房间地板向外扩充的格子数量
        
        if (roomType == DungeonRoomType.Outlet)
        {
            if (direction == RoomDirection.Up || direction == RoomDirection.Down)
            {
                return (int)(prevRoom.Size.X * 0.5f - 4);
            }
            return (int)(prevRoom.Size.Y * 0.5f - 5);
        }
        if (direction == RoomDirection.Up || direction == RoomDirection.Down)
        {
            return Random.RandomRangeInt((int)(prevRoom.Size.X * Config.RoomVerticalMinDispersion),
                (int)(prevRoom.Size.X * Config.RoomVerticalMaxDispersion)) + (int)(prevRoom.Size.X * 0.5f - 4);
        }
        return Random.RandomRangeInt((int)(prevRoom.Size.Y * Config.RoomHorizontalMinDispersion),
            (int)(prevRoom.Size.Y * Config.RoomHorizontalMaxDispersion)) + (int)(prevRoom.Size.Y * 0.5f - 5);
    }
}