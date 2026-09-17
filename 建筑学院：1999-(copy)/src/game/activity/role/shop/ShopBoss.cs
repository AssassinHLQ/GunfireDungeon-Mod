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
    /// SlotMarkerList 为空时, 自动生成几个货架
    /// </summary>
    [Export]
    public int DefaultSlotCount = 3;

    /// <summary>
    /// 自动排布时货架的水平间距(像素)。房间宽 21 格 x 16px = 336px, 3 个货架间距 28px 不会出格。
    /// </summary>
    [Export]
    public float DefaultSlotSpacing = 28f;

    /// <summary>
    /// 自动排布时货架相对 NPC 的垂直偏移(像素)。正数在下(房间原点在左上)。
    /// </summary>
    [Export]
    public float DefaultSlotOffsetY = 34f;

    private List<ShopItemSlot> _slot = new List<ShopItemSlot>();
    
    public override void OnInit()
    {
        base.OnInit();
        SetAttackDesire(false); //默认不攻击
        SetMoveDesire(false); //默认不移动
    }

    public override void OnCreateWithMark(RoomPreinstall roomPreinstall, ActivityMark activityMark)
    {
        base.OnCreateWithMark(roomPreinstall, activityMark);

        var layer = World.GetRoomLayer(RoomLayerEnum.NormalLayer);

        //【踩坑记录】原来这里是把货架摆在 (0, 34) —— 那是【房间原点】的坐标,
        //而房间原点是房间左上角, 通常正好在墙里, 所以玩家在商店房里看不到任何商品图标。
        //正确的参照物是 NPC 自己的位置, 货架应当摆在 NPC 身前的空地上。
        var npcPos = GlobalPosition;

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
                marker2D.Reparent(layer, false);
                marker2D.Position = position;
                CreateSlotAt(marker2D, layer);
            }

            GD.Print($"[商店] NPC={npcPos} 使用编辑器摆的 {_slot.Count} 个货架");
            return;
        }

        //兜底: 场景里没摆货架, 自动排布
        if (DefaultSlotCount <= 0)
        {
            GD.Print($"[商店] NPC={npcPos} DefaultSlotCount={DefaultSlotCount}, 不摆货架");
            return;
        }

        var count = DefaultSlotCount;
        //居中排布: 3 个时是 -spacing, 0, +spacing。
        //坐标是【相对 NPC】的, 不是相对房间原点。
        var startX = -(count - 1) * DefaultSlotSpacing * 0.5f;

        for (var i = 0; i < count; i++)
        {
            var marker = new Marker2D
            {
                Name = $"AutoSlotMarker{i}",
                Position = npcPos + new Vector2(startX + i * DefaultSlotSpacing, DefaultSlotOffsetY),
            };
            layer.AddChild(marker);
            CreateSlotAt(marker, layer);
        }

        GD.Print($"[商店] NPC={npcPos} 自动摆了 {_slot.Count} 个货架 " +
                 $"(间距 {DefaultSlotSpacing} 下移 {DefaultSlotOffsetY})");
    }

    /// <summary>
    /// 在一个位置上放一个货架(商品槽)
    /// </summary>
    private void CreateSlotAt(Node parent, Node layer)
    {
        var config = World.RandomPool.GetRandomProp();
        if (config == null)
        {
            return;
        }

        ShopItemSlot slot;
        if (SlotPrefab != null)
        {
            //编辑器里指定了预制体, 用它
            slot = SlotPrefab.Instantiate<ShopItemSlot>();
        }
        else
        {
            //没有预制体: 代码构建
            slot = ShopItemSlot.Create(config, 14f, ResourceManager.DefaultFont12Px);
        }

        _slot.Add(slot);
        parent.AddChild(slot);

        //临时诊断: 确认货架真的被加进场景树、落在哪个坐标
        TempDebug.LogShop(
            $"放置货架 #{_slot.Count} 父节点={parent.Name} " +
            $"父位置={(parent as Node2D)?.GlobalPosition} " +
            $"货架位置={slot.GlobalPosition} 在树中={slot.IsInsideTree()}");

        if (SlotPrefab != null)
        {
            //预制体走原来的初始化路径
            slot.InitItem(config);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_slot != null)
        {
            foreach (var shopItemSlot in _slot)
            {
                shopItemSlot?.Destroy();
            }

            _slot.Clear();
            _slot = null;
        }
    }
}
