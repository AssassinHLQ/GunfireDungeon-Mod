
using System.Collections.Generic;

/// <summary>
/// 地牢模式。
/// 不同模式共用同一套房间模板, 区别只在生成规则和 BGM。
/// </summary>
public enum DungeonMode
{
    /// <summary>
    /// 默认模式: 普通战斗房为主, 每层最后一个房间是 Boss 房
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 魔王模式: 战斗房按 BossRoomRatio 替换成 Boss 房(Boss 满血, 不削弱),
    /// 中间保留少量奖励房和商店作为喘息, 生成顺序与默认模式一致。
    /// </summary>
    Erlkoenig = 1,
}

/// <summary>
/// 生成地牢的配置
/// </summary>
public class DungeonConfig
{
    /// <summary>
    /// 地牢使用的随机种子
    /// </summary>
    public int? RandomSeed = null;

    /// <summary>
    /// 当前地牢层级
    /// </summary>
    public int DungeonLayer;
    
    /// <summary>
    /// 地牢组名称
    /// </summary>
    public string GroupName;

    /// <summary>
    /// 地牢模式
    /// </summary>
    public DungeonMode Mode = DungeonMode.Normal;

    /// <summary>
    /// 把多少个战斗房替换成 Boss 房 (百分比, 0~100)。
    ///
    /// 0 = 关闭, 走原来的 BossRoomCount 逻辑(每层最后一个房间是 Boss)。
    /// 大于 0 = 进入"按比例刷 Boss"模式, 此时 BossRoomCount 不再使用。
    ///
    /// 注意: 这个模式下 Boss 房是【替换】战斗房, 不额外增加房间总数,
    /// 所以地牢总长度和默认模式一致, 生成顺序(第几个房间放奖励/商店)也一致,
    /// 只是把"战斗房"那几格换成了"Boss 房"。
    /// </summary>
    public int BossRoomRatio = 0;

    /// <summary>
    /// 是否处于"按比例刷 Boss"模式
    /// </summary>
    public bool IsBossRatioMode => BossRoomRatio > 0;

    /// <summary>
    /// 本模式 Boss 房使用的 BGM (Sound.json 里的 Id)。
    /// 空 = 跟随地牢组的 SoundId。
    /// </summary>
    public string BossBgmId;

    /// <summary>
    /// 战斗房间数量
    /// </summary>
    public int BattleRoomCount = 15;

    /// <summary>
    /// 奖励房间数量
    /// </summary>
    public int RewardRoomCount = 2;

    /// <summary>
    /// 商店数量
    /// </summary>
    public int ShopRoomCount = 1;

    /// <summary>
    /// 出口房间数量
    /// </summary>
    public int OutRoomCount = 1;

    /// <summary>
    /// Boss房间数量
    /// </summary>
    public int BossRoomCount = 1;
    
    /// <summary>
    /// 房间数量
    /// </summary>
    public int RoomCount => BattleRoomCount + RewardRoomCount + ShopRoomCount + OutRoomCount + BossRoomCount;
    
    /// <summary>
    /// 房间最大层级
    /// </summary>
    public int MaxLayer = 5;
    
    /// <summary>
    /// 房间最小间隔
    /// </summary>
    public int RoomMinInterval = 2;
    
    /// <summary>
    /// 房间最大间隔
    /// </summary>
    public int RoomMaxInterval = 5;
    
    /// <summary>
    /// 房间横轴最小分散程度
    /// </summary>
    public float RoomHorizontalMinDispersion = -0.6f;
    
    /// <summary>
    /// 房间横轴最大分散程度
    /// </summary>
    public float RoomHorizontalMaxDispersion = 0.6f;

    /// <summary>
    /// 房间纵轴最小分散程度
    /// </summary>
    public float RoomVerticalMinDispersion = -0.6f;
    
    /// <summary>
    /// 房间纵轴最大分散程度
    /// </summary>
    public float RoomVerticalMaxDispersion = 0.6f;
    
    /// <summary>
    /// 是否启用区域限制
    /// </summary>
    public bool EnableLimitRange = true;
    
    /// <summary>
    /// 横轴范围
    /// </summary>
    public int RangeX = 120;
    
    /// <summary>
    /// 纵轴范围
    /// </summary>
    public int RangeY = 120;

    /// <summary>
    /// 是否允许包含拐角的过道
    /// </summary>
    public bool AllowedCornerAisles = false;
    
    //----------------------- 地牢编辑使用 -------------------------
    /// <summary>
    /// 是否指定了房间
    /// </summary>
    public bool HasDesignatedRoom => DesignatedRoom != null && DesignatedRoom.Count > 0;

    /// <summary>
    /// 指定预设的房间类型
    /// </summary>
    public DungeonRoomType DesignatedType;
    /// <summary>
    /// 指定房间列表
    /// </summary>
    public List<DungeonRoomSplit> DesignatedRoom;
}