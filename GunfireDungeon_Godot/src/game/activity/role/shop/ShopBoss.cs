using System.Collections.Generic;
using Godot;
using Godot.Collections;

/// <summary>
/// 商店老板
///
/// 背景: 这个类原来依赖 ShopBoss0001.tscn 里的 SlotMarkerList 提供货架位置,
/// 但那个场景里 SlotMarkerList = [] (空数组), 于是下面的 foreach 一次都不执行,
/// 结果场上一个商品都没有 —— 玩家靠近 NPC 什么都不弹。
/// 更糟的是 PackedScene 导出也没赋值, 一旦列表非空就会 NRE。
///
/// 现在改成: SlotMarkerList 为空时, 用 DefaultSlotCount / DefaultSlotSpacing 自动排布货架,
/// 并且槽位由 ShopItemSlot.Create() 用代码构建, 不再需要 .tscn 和 PackedScene。
/// 如果以后在编辑器里摆好了 SlotMarkerList, 仍然优先用编辑器摆的位置。
///
/// ── 刷新商品 ──
/// 店主本身是可互动物体(ActivityObject 已经实现了 IInteractive), 这里覆写
/// <see cref="Interactive"/>: 玩家按 E 跟店主对话, 就把所有货架的商品重新抽一遍。
/// 刷新是【原地重建】—— 货架位置不变, 只换内容, 所以不会跑到墙里去。
/// </summary>
public partial class ShopBoss : AiRole
{
    /// <summary>
    /// 货架预制体。留作兼容 —— 不填也能工作(会用代码构建)。
    ///
    /// 注意: 原来这个字段叫 ShopItemSlot, 和类名 ShopItemSlot 同名,
    /// 在类内部写 ShopItemSlot.Create(...) 会被解析成"在这个 PackedScene 字段上找 Create"从而编译失败。
    /// 所以改名成 SlotPrefab。导出名保持 "ShopItemSlot" 不变,
    /// 这样 ShopBoss0001.tscn 里已有的连接(虽然目前是空的)不会失效。
    /// </summary>
    [Export]
    public PackedScene SlotPrefab;

    /// <summary>
    /// 编辑器里摆好的货架位置。为空时走下面的自动排布。
    /// </summary>
    [Export]
    public Array<Marker2D> SlotMarkerList;

    /// <summary>
    /// SlotMarkerList 为空时, 自动生成几个货架。
    /// 2026-09-20 由 3 改成 5(用户要求每轮卖五个)。
    /// </summary>
    [Export]
    public int DefaultSlotCount = 5;

    /// <summary>
    /// 自动排布时货架的水平间距(像素)。
    /// 5 个货架时总宽 = 4 x 28 = 112px, 房间宽 21 格 x 16px = 336px, 不会出格。
    /// </summary>
    [Export]
    public float DefaultSlotSpacing = 28f;

    /// <summary>
    /// 自动排布时货架相对 NPC 的垂直偏移(像素)。正数在下(房间原点在左上)。
    /// </summary>
    [Export]
    public float DefaultSlotOffsetY = 34f;

    /// <summary>
    /// 商品图标显示的边长(像素)。
    /// </summary>
    [Export]
    public float SlotIconSize = 14f;

    /// <summary>
    /// 刷新商品的【基础】价格(金币)。实际价格每刷一次翻一倍:
    /// 5 → 10 → 20 → 40 → 80 ... 所以基础价就是第一次刷新的价格。
    /// 设成 0 表示免费(不再翻倍)。
    ///
    /// 2026-09-20 由用户要求定为 5 金币起、逐次翻倍。
    /// 想调价直接改这个字段(或在 ShopBoss0001.tscn 里覆盖), 不用动代码。
    /// </summary>
    [Export]
    public int RefreshCostGold = 5;

    /// <summary>
    /// 本次进商店已经刷新过几次。每创建一次商店 NPC 从 0 开始。
    /// </summary>
    private int _refreshCount;

    /// <summary>
    /// 【下一次】刷新要花多少金币 = 基础价 x 2^已刷新次数。
    ///
    /// 5 → 10 → 20 → 40 → 80 → ...
    /// 用 long 算再夹到 int 上界 —— 玩家一直刷的话 5 &lt;&lt; 31 会溢出成负数,
    /// 那样反而变成"刷新给钱"。
    /// </summary>
    public int CurrentRefreshCost
    {
        get
        {
            if (RefreshCostGold <= 0)
            {
                return 0;
            }

            // 移位上限 20 次(5 * 2^20 ≈ 524 万), 早就远超玩家能付得起的量
            var shift = Mathf.Min(_refreshCount, 20);
            var cost = (long)RefreshCostGold << shift;
            return cost > int.MaxValue ? int.MaxValue : (int)cost;
        }
    }

    /// <summary>
    /// 互动提示条上显示的文案。把"下次刷新多少钱"直接写进去,
    /// 否则玩家不知道按 E 会扣钱(见 InteractiveTipBarHandler.GetDisplayName)。
    /// </summary>
    public string InteractiveTipText
    {
        get
        {
            var name = ActivityBase?.Name ?? "商店";
            if (RefreshCostGold <= 0)
            {
                return $"{name}（按 E 刷新商品 · 免费）";
            }

            return $"{name}（按 E 刷新商品 · {CurrentRefreshCost} 金币）";
        }
    }

    private readonly List<ShopItemSlot> _slot = new List<ShopItemSlot>();

    /// <summary>
    /// 货架的位置(相对 world 的全局坐标)。刷新时按这些位置原地重建。
    /// </summary>
    private readonly List<Vector2> _slotPositions = new List<Vector2>();

    /// <summary>
    /// 货架挂到哪个节点下(房间的 NormalLayer)。
    /// </summary>
    private Node _slotLayer;

    /// <summary>
    /// 是否已经摆过货架。刷新只在摆过之后才有意义。
    /// </summary>
    private bool _built;

    public override void OnInit()
    {
        base.OnInit();
        SetAttackDesire(false); //默认不攻击
        SetMoveDesire(false); //默认不移动

        // ── 让玩家的交互区域能探测到店主 ──
        //
        // 【踩坑记录】玩家 RoleTemplate.tscn 里那个 InteractiveArea 的
        //   collision_layer = 0, collision_mask = 4(Prop)
        // 而 ShopBoss0001.tscn 把本节点根部的 collision_layer 覆盖成了
        //   1024(HurtArea)
        // 两个值按位与是 0 —— 所以玩家走近店主【根本不会弹交互提示】,
        // 也就没法按 E 对话刷新。之前"商店老板无法交互"就是这个原因。
        //
        // 把 Prop 位并进去(1024 | 4 = 1028)即可。
        //
        // 【为什么安全】店主 Camp = Peace(1)。Role.IsEnemy() 里有
        //   if (other.Camp == Camp || other.Camp == Peace || Camp == Peace) return false;
        // 所以哪怕房间归属区域(AffiliationArea 的 mask 含 Prop)探测到了店主,
        // DungeonManager.OnCheckEnemy 的 IsEnemyWithPlayer() 也是 false,
        // 不会把它当成"房间还没清空"而卡住门。
        CollisionLayer |= PhysicsLayer.Prop;

        Debug.Log($"[商店] 店主交互层已修正: CollisionLayer={CollisionLayer} " +
                  $"(Prop={PhysicsLayer.Prop}, HurtArea={PhysicsLayer.HurtArea})");
    }

    public override void OnCreateWithMark(RoomPreinstall roomPreinstall, ActivityMark activityMark)
    {
        base.OnCreateWithMark(roomPreinstall, activityMark);

        _slotLayer = World.GetRoomLayer(RoomLayerEnum.NormalLayer);

        //【踩坑记录】原来这里是把货架摆在 (0, 34) —— 那是【房间原点】的坐标,
        //而房间原点是房间左上角, 通常正好在墙里, 所以玩家在商店房里看不到任何商品图标。
        //正确的参照物是 NPC 自己的位置, 货架应当摆在 NPC 身前的空地上。
        var npcPos = GlobalPosition;

        _slotPositions.Clear();

        //优先用编辑器摆好的位置
        if (SlotMarkerList != null && SlotMarkerList.Count > 0)
        {
            foreach (var marker2D in SlotMarkerList)
            {
                if (marker2D == null)
                {
                    continue;
                }

                var position = marker2D.GlobalPosition;
                marker2D.Reparent(_slotLayer, false);
                marker2D.Position = position;
                _slotPositions.Add(position);
            }

            GD.Print($"[商店] NPC={npcPos} 使用编辑器摆的 {_slotPositions.Count} 个货架");
        }
        else
        {
            //兜底: 场景里没摆货架, 自动排布
            if (DefaultSlotCount <= 0)
            {
                GD.Print($"[商店] NPC={npcPos} DefaultSlotCount={DefaultSlotCount}, 不摆货架");
                return;
            }

            //居中排布: 5 个时是 -2s, -s, 0, +s, +2s。
            //坐标是【相对 NPC】的, 不是相对房间原点。
            var startX = -(DefaultSlotCount - 1) * DefaultSlotSpacing * 0.5f;

            for (var i = 0; i < DefaultSlotCount; i++)
            {
                _slotPositions.Add(npcPos + new Vector2(startX + i * DefaultSlotSpacing, DefaultSlotOffsetY));
            }

            GD.Print($"[商店] NPC={npcPos} 自动摆 {_slotPositions.Count} 个货架 " +
                     $"(间距 {DefaultSlotSpacing} 下移 {DefaultSlotOffsetY})");
        }

        BuildSlots();
    }

    /// <summary>
    /// 按 <see cref="_slotPositions"/> 重新摆一遍商品。会先清掉旧的。
    /// 刷新商品(Interactive)也走这里, 所以位置不会变。
    /// </summary>
    private void BuildSlots()
    {
        ClearSlots();

        if (_slotLayer == null || _slotPositions.Count == 0)
        {
            return;
        }

        foreach (var position in _slotPositions)
        {
            var config = World.RandomPool.GetRandomProp();
            if (config == null)
            {
                continue;
            }

            ShopItemSlot slot;
            if (SlotPrefab != null)
            {
                //编辑器里指定了预制体, 用它
                slot = SlotPrefab.Instantiate<ShopItemSlot>();
                _slotLayer.AddChild(slot);
                slot.GlobalPosition = position;
                slot.InitItem(config);
            }
            else
            {
                //没有预制体: 代码构建
                slot = ShopItemSlot.Create(config, SlotIconSize, ResourceManager.DefaultFont12Px);
                _slotLayer.AddChild(slot);
                slot.GlobalPosition = position;
            }

            _slot.Add(slot);
        }

        _built = true;

        TempDebug.LogShop($"[商店] 摆好 {_slot.Count} 个商品");
    }

    /// <summary>
    /// 清掉当前所有货架。刷新和销毁都用它。
    /// </summary>
    private void ClearSlots()
    {
        foreach (var shopItemSlot in _slot)
        {
            shopItemSlot?.Destroy();
        }

        _slot.Clear();
    }

    // ────────────────────────── 和店主对话: 刷新商品 ──────────────────────────

    /// <summary>
    /// 玩家靠近店主时提示条上显示的行不行。
    /// 只要是个 Role(玩家)就能对话 —— 店主不挑时间。
    /// </summary>
    public override CheckInteractiveResult CheckInteractive(ActivityObject master)
    {
        return new CheckInteractiveResult(this, master is Role);
    }

    /// <summary>
    /// 按下互动键(默认 E) → 重新抽一遍商品。
    ///
    /// 价格逐次翻倍: 第 1 次 5、第 2 次 10、第 3 次 20 …… (见 <see cref="CurrentRefreshCost"/>)。
    /// 每次都先扣钱再重抽, 金币不够就直接取消, 不扣钱也不刷新。
    /// </summary>
    public override void Interactive(ActivityObject master)
    {
        if (!_built || master is not Role role)
        {
            return;
        }

        var cost = CurrentRefreshCost;
        if (cost > 0)
        {
            if (role.RoleState.Gold < cost)
            {
                TempDebug.LogShop($"[商店] 第 {_refreshCount + 1} 次刷新需要 {cost} 金币, " +
                                  $"玩家只有 {role.RoleState.Gold}, 已取消");
                return;
            }

            role.UseGold(cost);
        }

        BuildSlots();
        _refreshCount++;

        //提示条上的价格要跟着变成下一次的价(5 → 10 → 20)。
        //走的是玩家切换互动对象时用的同一个事件, 见 InteractiveTipBarHandler.OnPlayerChangeInteractiveItem。
        EventManager.EmitEvent(EventEnum.OnPlayerChangeInteractiveItem,
            new CheckInteractiveResult(this, true));

        TempDebug.LogShop($"[商店] 第 {_refreshCount} 次刷新完成, 花费 {cost} 金币, " +
                          $"新上架 {_slot.Count} 个商品, 下次刷新 {CurrentRefreshCost} 金币");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_slot != null)
        {
            ClearSlots();
            _slot.Clear();
        }

        _slotPositions.Clear();
        _slotLayer = null;
        _built = false;
    }
}
