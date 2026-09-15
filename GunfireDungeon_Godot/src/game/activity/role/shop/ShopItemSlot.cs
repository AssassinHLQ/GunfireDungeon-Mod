
using System;
using Config;
using Godot;

/// <summary>
/// 商店货架上的一个商品槽。
///
/// 这是一个 Area2D(不是 ActivityObject), 玩家 Role.InteractiveArea 的 collision_mask = Prop(4),
/// 所以本节点的 CollisionLayer 必须含 Prop 位, 否则玩家永远探测不到。
/// 又因为不是 ActivityObject, InteractiveTipBarHandler 里原来的
/// <c>is ActivityObject</c> 判断会失败, 那个判断已经改成按接口取, 详见该文件。
///
/// 节点结构是【代码构建】的(见 Create()), 不再依赖 ShopItemSlot.tscn。
/// 原因: 新建 .tscn 需要 Godot 生成 .import/uid, 而 uid 由引擎内部哈希算出, 无法手写;
///       代码构建则零新增资源文件。
/// </summary>
public partial class ShopItemSlot : Area2D, IInteractive, IOutline
{
    private Label _name;
    private Label _price;
    private Sprite2D _icon;
    private ExcelConfig.ActivityBase _config;

    private uint _finalPrice;
    //是否买得起
    private bool _flag = true;

    /// <summary>
    /// 互动提示条上显示的名字。
    /// 提示条不能再用 ActivityObject.ActivityBase 取名(本类不是 ActivityObject), 改走这个属性。
    /// </summary>
    public string DisplayName => _config?.Name ?? "";

    public bool ShowOutline { get; set; } = true;
    public Color OutlineColor
    {
        get => _blendShaderMaterial == null ? Colors.Black : _blendShaderMaterial.GetShaderParameter(ShaderParamNames.OutlineColor).AsColor();
        set => _blendShaderMaterial?.SetShaderParameter(ShaderParamNames.OutlineColor, value);
    }

    
    public bool IsDestroyed { get; private set; }

    private ShaderMaterial _blendShaderMaterial;

    /// <summary>
    /// 用代码搭建一个商品槽。
    /// 不依赖 .tscn, 所以也不需要 PackedScene 导出。
    /// </summary>
    /// <param name="config">商品配置</param>
    /// <param name="iconSize">图标显示边长(像素)</param>
    /// <param name="font">价格/名字用的字体, 可为 null</param>
    public static ShopItemSlot Create(ExcelConfig.ActivityBase config, float iconSize = 14f, Font font = null)
    {
        var slot = new ShopItemSlot
        {
            Name = "ShopItemSlot",
            //关键: 玩家 InteractiveArea 的 mask 是 Prop(4), 本节点必须落在 Prop 层
            CollisionLayer = PhysicsLayer.Prop,
            CollisionMask = 0,
            Monitorable = true,
            Monitoring = true,
        };

        //碰撞形状 (略大于图标, 便于玩家靠近就能选中)
        var shape = new CollisionShape2D { Name = "Collision" };
        var rect = new RectangleShape2D { Size = new Vector2(iconSize + 6f, iconSize + 6f) };
        shape.Shape = rect;
        slot.AddChild(shape);

        //图标
        var icon = new Sprite2D
        {
            Name = "Icon",
            Centered = true,
            //图标原始尺寸各异, 缩放到统一大小
            Scale = Vector2.One,
        };
        var tex = LoadIcon(config);
        if (tex != null)
        {
            icon.Texture = tex;
            var s = tex.GetSize();
            if (s.X > 0 && s.Y > 0)
            {
                //保持比例缩放到 iconSize 以内
                var k = iconSize / Mathf.Max(s.X, s.Y);
                icon.Scale = new Vector2(k, k);
            }
        }
        slot.AddChild(icon);

        //名字标签不再创建。
        //玩家靠近时上方的互动提示条已经会显示物品名(见 InteractiveTipBarHandler),
        //槽位自己再显示一次就会变成两行重复文字, 用户要求只保留上面那行。
        //保留字段 _name 只是为了兼容以前的 InitItem(走 .tscn 那条路), 代码构建时不赋值。
        var nameLabel = MakeLabel("NameLabel", font);
        nameLabel.Text = config?.Name ?? "";
        nameLabel.Visible = false;
        //【不 AddChild】—— 不加入场景树就不会渲染。
        // 留个注释说明为什么这里"建了又不加": 是为了让 MakeLabel 的调用结构不变,
        // 以后要恢复槽位自显名字, 只要把下面这行 AddChild 取消注释即可。
        // slot.AddChild(nameLabel);

        //价格
        var priceLabel = MakeLabel("Price", font);
        priceLabel.Text = (config?.Price ?? 0).ToString();
        //放在图标下方, 避免和图标重叠
        priceLabel.Position = new Vector2(0, iconSize * 0.5f + 8f);
        slot.AddChild(priceLabel);

        slot.BindNodes(priceLabel, nameLabel, icon, config);
        return slot;
    }

    /// <summary>
    /// 加载图标。config.Icon 可能为空或指向不存在的文件(实测 56 条里有 25 条是空的),
    /// 这里容错: 失败就返回 null, 不抛异常也不写错误日志刷屏。
    /// </summary>
    private static Texture2D LoadIcon(ExcelConfig.ActivityBase config)
    {
        if (config == null || string.IsNullOrEmpty(config.Icon))
        {
            return null;
        }

        try
        {
            return ResourceManager.Load<Texture2D>(config.Icon);
        }
        catch
        {
            return null;
        }
    }

    private static Label MakeLabel(string name, Font font)
    {
        var label = new Label
        {
            Name = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            //不参与鼠标, 免得挡住互动
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (font != null)
        {
            label.AddThemeFontOverride("font", font);
        }
        return label;
    }

    /// <summary>
    /// Create() 内部用, 把建好的节点和配置绑到实例上。
    /// 之所以不直接在 Create 里赋值字段, 是为了让"构造"和"初始化"分开, 便于阅读。
    /// </summary>
    private void BindNodes(Label price, Label nameLabel, Sprite2D icon, ExcelConfig.ActivityBase config)
    {
        _price = price;
        _name = nameLabel;
        _icon = icon;
        _blendShaderMaterial = _icon?.Material as ShaderMaterial;

        _config = config;
        _name.Text = config?.Name ?? "";
        _finalPrice = config?.Price ?? 0;
        _price.Text = _finalPrice.ToString();

        //临时诊断: 打印槽位创建时的关键状态, 确认层/尺寸/图标是否正常
        TempDebug.LogShop(
            $"创建槽位 name={config?.Name} id={config?.Id} price={_finalPrice} " +
            $"layer={CollisionLayer} mask={CollisionMask} " +
            $"图标={(icon?.Texture != null ? "有" : "无")} " +
            $"图标路径={config?.Icon ?? "(空)"}");
    }

    /// <summary>
    /// 兼容旧调用: 如果以后补了 ShopItemSlot.tscn, 仍然可以走这条路。
    /// 节点不存在时会抛异常, 所以只在确认场景里配好了节点的情况下调用。
    /// </summary>
    public void InitItem(ExcelConfig.ActivityBase config)
    {
        _price = GetNode<Label>("Price");
        _name = GetNode<Label>("NameLabel");
        _icon = GetNode<Sprite2D>("Icon");
        _name.Visible = false;
        _blendShaderMaterial = _icon.Material as ShaderMaterial;

        _config = config;
        _name.Text = config.Name;
        _finalPrice = config.Price;
        _price.Text = _finalPrice.ToString();
        var tex = LoadIcon(config);
        if (tex != null)
        {
            _icon.Texture = tex;
        }
    }

    public override void _Process(double delta)
    {
        //代码构建的槽位可能没绑好节点(比如图标加载失败), 这里全部做空判断
        var player = World.Current?.Player;
        if (player != null && _price != null)
        {
            //临时诊断: 第一次发现玩家靠近时打印距离, 判断"离得太远"还是"根本没被探测到"
            if (!_loggedNearby)
            {
                var d = GlobalPosition.DistanceTo(player.GlobalPosition);
                if (d < 120f)
                {
                    _loggedNearby = true;
                    TempDebug.LogShop(
                        $"玩家靠近 name={_config?.Name} 距离={d:F1}px " +
                        $"槽位={GlobalPosition} 玩家={player.GlobalPosition} " +
                        $"父节点={GetParent()?.Name} 父位置={(GetParent() as Node2D)?.GlobalPosition}");
                }
            }

            if (_flag && player.RoleState.Gold < _finalPrice) //买不起
            {
                _flag = false;
                _price.Modulate = Colors.Red;
            }
            else if (!_flag && player.RoleState.Gold >= _finalPrice) //买得起
            {
                _flag = true;
                _price.Modulate = Colors.White;
            }
        }
    }

    /// <summary>
    /// 临时诊断用: 是否已经打印过"玩家靠近"
    /// </summary>
    private bool _loggedNearby;

    public CheckInteractiveResult CheckInteractive(ActivityObject master)
    {
        return new CheckInteractiveResult(this, _config != null && master is Role);
    }

    public void Interactive(ActivityObject master)
    {
        var role = (Role)master;
        TempDebug.LogShop($"按下E交互 name={_config?.Name} 价格={_finalPrice} 玩家金币={role.RoleState.Gold}");

        if (role.RoleState.Gold < _finalPrice)
        {
            TempDebug.LogShop("  金币不足, 已取消");
            return;
        }
        
        Monitorable = false;
        Visible = false;
        role.UseGold((int)_finalPrice);

        var item = ActivityObject.Create(_config);
        if (item is Weapon weapon)
        {
            if (!role.PickUpWeapon(weapon))
            {
                role.ThrowWeapon();
                role.PickUpWeapon(weapon);
            }
        }
        else if (item is ActiveProp activeProp)
        {
            if (!role.PickUpActiveProp(activeProp))
            {
                role.ThrowActiveProp();
                role.PickUpActiveProp(activeProp);
            }
        }
        else if (item is BuffProp buffProp)
        {
            role.PickUpBuffProp(buffProp);
        }
        else
        {
            //原来这里直接 throw, 会把整局游戏打断。
            //改成: 不支持的物品就当买不了, 把金币退回去, 槽位保持可见。
            role.AddGold((int)_finalPrice);
            Monitorable = true;
            Visible = true;
            _config = null;
            GD.PushWarning($"商店: 不支持的物品类型, 已取消购买并退款: {item?.ActivityBase?.Id ?? "null"}");
            return;
        }

        _config = null;
    }
    
    public virtual void OnTargetEnterd(ActivityObject target)
    {
        //临时诊断: 走到这里说明 CheckInteractive 通过了, 问题只可能在提示条那一侧
        TempDebug.LogShop($"被选中(可交互) name={_config?.Name}");

        if (_name != null)
        {
            _name.Visible = true;
        }
        OutlineColor = Colors.White;
    }

    public virtual void OnTargetExitd(ActivityObject target)
    {
        if (_name != null)
        {
            _name.Visible = false;
        }
        OutlineColor = Colors.Black;
    }
    
    public void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        
        QueueFree();
    }
}
