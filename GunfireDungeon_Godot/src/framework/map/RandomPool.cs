
using System;
using System.Collections.Generic;
using Config;
using Godot;

public class RandomPool
{
    /// <summary>
    /// 随机数生成器
    /// </summary>
    public SeedRandom Random { get; }
    
    /// <summary>
    /// 所属世界
    /// </summary>
    public World World { get; }

    private readonly List<ExcelConfig.ActivityBase> _lootPropList;

    public RandomPool(World world)
    {
        World = world;
        Random = world.Random;

        _lootPropList = new List<ExcelConfig.ActivityBase>();
        foreach (var prop in PreinstallMarkManager.GetMarkConfigsByType(ActivityType.Prop))
        {
            if (prop != null && !IsPartDropId(prop.Id))
            {
                _lootPropList.Add(prop);
            }
        }
    }

    /// <summary>
    /// 判断一个道具标记是否是已经从普通掉落移除的武器零件。
    /// </summary>
    public static bool IsPartDropId(string id)
    {
        return string.Equals(id, ActivityObject.Ids.Id_part_comm0001, StringComparison.Ordinal) ||
               (id != null && id.StartsWith("partProp", StringComparison.Ordinal));
    }

    /// <summary>
    /// 获取随机武器
    /// </summary>
    public ExcelConfig.ActivityBase GetRandomWeapon()
    {
        return Random.RandomChoose(PreinstallMarkManager.GetMarkConfigsByType(ActivityType.Weapon));
    }

    /// <summary>
    /// 精英怪的抽取权重。没在表里的敌人一律按 <see cref="NormalEnemyWeight"/> 算,
    /// 所以以后再加普通小怪不用动这里。
    /// </summary>
    private static readonly Dictionary<string, int> EnemyWeights = new()
    {
        { "wizard0001", EliteEnemyWeight },
        { "executioner0001", EliteEnemyWeight },
    };

    private const int NormalEnemyWeight = 10;

    /// <summary>
    /// 精英怪权重。10 : 1 → 单只精英约占刷怪量的 1/11(两族合计约 15%)。
    /// </summary>
    private const int EliteEnemyWeight = 1;

    /// <summary>
    /// 获取随机敌人
    ///
    /// 【为什么改成加权】(2026-09-24 加入第二个精英怪「不死刽子手」时)
    /// 原来是无脑 <c>RandomChoose</c> —— 池子里 4 种就是各 25%,
    /// 两只精英怪加起来要占掉**一半**的刷怪量, 战斗房会直接变成"精英房"。
    /// 精英怪是"这一房有硬骨头"的节奏点, 不该跟小怪一个密度。
    /// </summary>
    public ExcelConfig.ActivityBase GetRandomEnemy()
    {
        var pool = PreinstallMarkManager.GetMarkConfigsByType(ActivityType.Enemy);
        if (pool.Count == 0)
        {
            return null;
        }

        var weights = new int[pool.Count];
        for (var i = 0; i < pool.Count; i++)
        {
            weights[i] = EnemyWeights.TryGetValue(pool[i].Id, out var w) ? w : NormalEnemyWeight;
        }

        return pool[World.Random.RandomWeight(weights)];
    }

    /// <summary>
    /// 获取随机道具
    /// </summary>
    public ExcelConfig.ActivityBase GetRandomProp()
    {
        return Random.RandomChoose(_lootPropList);
    }

    /// <summary>
    /// 填充自动波次数据
    /// </summary>
    public void FillAutoWave(RoomPreinstall preinstall)
    {
        if (preinstall.RoomInfo.RoomType == DungeonRoomType.Battle)
        {
            FillBattleRoom(preinstall);
        }
    }

    //填充战斗房间
    private void FillBattleRoom(RoomPreinstall preinstall)
    {
        var count = World.Random.RandomRangeInt(3, 10);
        var tileInfo = preinstall.RoomInfo.RoomSplit.TileInfo;
        var serializeVector2s = tileInfo.NavigationVertices;
        var vertices = new List<Vector2>();
        foreach (var sv2 in serializeVector2s)
        {
            vertices.Add(sv2.AsVector2());
        }
        var positionArray = World.Random.GetRandomPositionInPolygon(vertices, tileInfo.NavigationPolygon, count);
        var arr = new ActivityType[] { ActivityType.Enemy, ActivityType.Weapon, ActivityType.Prop };
        var weight = new int[] { 15, 2, 1 };
        for (var i = 0; i < count; i++)
        {
            var tempWave = preinstall.GetOrCreateWave(World.Random.RandomRangeInt(0, 2));
            var index = World.Random.RandomWeight(weight);
            var activityType = arr[index];
    
            //创建标记
            var mark = ActivityMark.CreateMark(activityType, i * 0.3f, preinstall.RoomInfo.ToGlobalPosition(positionArray[i]));
            
            if (activityType == ActivityType.Enemy) //敌人
            {
                mark.Id = GetRandomEnemy().Id;
                mark.Attr.Add("Face", "0");
                mark.DerivedAttr = new Dictionary<string, string>();
                mark.DerivedAttr.Add("Face", World.Random.RandomChoose((int)FaceDirection.Left, (int)FaceDirection.Right).ToString()); //链朝向
                // 小怪一律手持武器。
                // 原来这里是 `if (World.Random.RandomBoolean(0.8f))`, 也就是有 20% 的小怪光着手出生 ——
                // 空手的小怪既打不到人、死了也掉不出武器, 实测看上去就是"这个小怪出生没有武器"。
                var weapon = GetRandomWeapon();
                var weaponAttribute = Weapon.GetWeaponAttribute(weapon.Id);
                mark.Attr.Add("Weapon", weapon.Id); //武器id
                mark.Attr.Add("CurrAmmon", weaponAttribute.AmmoCapacity.ToString()); //弹夹弹药量
                // 键名沿用旧的 "ResidueMana", 含义已经改成【备用弹药】(见 RoomPreinstall)
                mark.Attr.Add("ResidueMana",
                    (weaponAttribute.AmmoCapacity * Weapon.ReserveMagazineCount).ToString());
            }
            else if (activityType == ActivityType.Weapon) //武器
            {
                mark.Id = GetRandomWeapon().Id;
            }
            else if (activityType == ActivityType.Prop) //道具
            {
                var prop = GetRandomProp();
                if (prop == null)
                {
                    continue;
                }

                mark.Id = prop.Id;
            }

            tempWave.Add(mark);
        }
    }
}
