
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Config;
using DsUi;
using Godot;
using UI.game.BottomTips;
using UI.game.RoomUI;

public partial class GameApplication : Node2D, ICoroutine
{
    public static GameApplication Instance { get; private set; }

    /// <summary>
    /// 场景根节点
    /// </summary>
    [Export]
    public Node2D SceneRoot;
    
    /// <summary>
    /// 全局根节点
    /// </summary>
    [Export]
    public Node2D GlobalNodeRoot;
    
    /// <summary>
    /// 游戏渲染视口
    /// </summary>
    [Export]
    private SubViewport SubViewport;

    /// <summary>
    /// SubViewportContainer 组件
    /// </summary>
    [Export]
    private SubViewportContainer SubViewportContainer;

    [Export]
    private CanvasLayer ViewCanvas;

    [Export]
    private Node NoPerfectPixelRoot;

    /// <summary>
    /// 是否使用完美像素
    /// </summary>
    public bool PerfectPixel { get; private set; } = true;
    
    /// <summary>
    /// 游戏目标帧率
    /// </summary>
    public int TargetFps { get; private set; }
    
    /// <summary>
    /// 鼠标指针
    /// </summary>
    public Cursor Cursor { get; private set; }

    /// <summary>
    /// 地牢管理器
    /// </summary>
    public DungeonManager DungeonManager { get; private set; }
    
    /// <summary>
    /// 房间配置
    /// </summary>
    public Dictionary<string, DungeonRoomGroup> RoomConfig { get; private set; }
    
    /// <summary>
    /// TileSet配置
    /// </summary>
    public Dictionary<string, TileSetSplit> TileSetConfig { get; private set; }
    
    // /// <summary>
    // /// 房间配置数据, key: 模板房间资源路径
    // /// </summary>
    // public Dictionary<string, DungeonRoomSplit> RoomConfigMap { get; private set; }

    /// <summary>
    /// 游戏视图大小
    /// </summary>
    public Vector2 ViewportSize { get; private set; } = new Vector2(480, 270);
    
    /// <summary>
    /// 像素缩放
    /// </summary>
    public int PixelScale { get; private set; } = 4;
    
    /// <summary>
    /// 第一层地牢配置信息
    /// </summary>
    public DungeonConfig FirstDungeonConfig { get; private set; }

    /// <summary>
    /// 地牢组加载顺序
    /// </summary>
    public List<string> DungeonGroupList { get; } = new List<string>();

    /// <summary>
    /// 游戏存档
    /// </summary>
    public GameSave GameSave { get; private set; }
    
    /// <summary>
    /// 默认相机缩放
    /// </summary>
    public Vector2 DefaultCameraZoom { get; private set; } = Vector2.One;
    
    /// <summary>
    /// 游戏中房间Ui
    /// </summary>
    public RoomUIPanel RoomUIPanel { get; set; }
    
    //开启的协程
    private List<CoroutineData> _coroutineList;
    
    public GameApplication()
    {
        Instance = this;
        // TargetFps = Mathf.RoundToInt(DisplayServer.ScreenGetRefreshRate());
        
        Utils.InitRandom();

        //初始化配置表
        ExcelConfig.Init();
        PropFragmentRegister.Init();
        //初始化房间配置数据
        InitRoomConfig();
        //初始化TileSet配置数据
        InitTileSetConfig();
        //初始化武器数据
        Weapon.InitWeaponAttribute();
        //初始化敌人数据
        Enemy.InitRoleAttribute();
        //初始化buff数据
        BuffProp.InitBuffAttribute();
        //初始化主动道具数据
        ActiveProp.InitActiveAttribute();
        //初始化零件数据
        PartProp.InitPartAttribute();
        
        PreinstallMarkManager.Init();
        
        foreach (var dungeonRoomGroup in RoomConfig)
        {
            DungeonGroupList.Add(dungeonRoomGroup.Key);
        }

        FirstDungeonConfig = GetDungeonConfig(DungeonGroupList[0], 1);
        
        //原本这里把地牢组的 SoundId 强制置空(注释还写着 "level1_bgm"),
        //导致 DungeonManager 播放 BGM 的分支永远进不去。
        //现在 Sound.json 里已经配好 bgm_* 条目, 这里改为真正的地牢 BGM。
        //想换曲子只改 GroupConfig.json 里的 SoundId 即可, 不用动代码。
        RoomConfig[DungeonGroupList[0]].SoundId = "bgm_battle";
    }

    /// <summary>
    /// 获取地牢配置数据
    /// </summary>
    /// <param name="groupName">地牢组名称</param>
    /// <param name="layer">层级</param>
    /// <param name="mode">地牢模式, 默认 Normal</param>
    public DungeonConfig GetDungeonConfig(string groupName, int layer, DungeonMode mode = DungeonMode.Normal)
    {
        var config = new DungeonConfig();
        config.DungeonLayer = layer;
        config.GroupName = groupName;
        config.RandomSeed = null;
        config.Mode = mode;

        config.BattleRoomCount = 12;
        config.RewardRoomCount = 2;
        config.BossRoomCount = 0;

        config.ShopRoomCount = 1;
        config.EnableLimitRange = false;
        config.AllowedCornerAisles = false;

        //魔王模式: 把战斗房按比例换成 Boss 房。
        //Boss 满血不削弱(用户明确要求), 所以这里不改任何敌人属性, 只改房间类型。
        //保留 2 个奖励房 + 1 个商店作为喘息, 由 DefaultDungeonRule 按生成顺序插入。
        if (mode == DungeonMode.Erlkoenig)
        {
            config.BossRoomRatio = 70;      //70% 的战斗格变 Boss 房

            //------------- 降低生成失败率 -------------
            // 生成失败(报"尝试次数过多")的直接原因是【门连不上】:
            // DungeonGenerator.GenerateRoom 里每个房间最多随机试 maxTryCount 次
            // (Boss 房 = _maxTryCount * 2), 每次随机 方向 + 间隔 + 偏移,
            // 然后要求 (a) 不与已有房间碰撞 (b) ConnectDoor 成功。
            // 而 ConnectDoor 要求两个房间在垂直于走廊的轴上【至少重叠 6 格】。
            //
            // 关键是【偏移范围】: GetNextRoomOffset 用 RoomHorizontal/VerticalDispersion,
            // 默认 ±0.6, 即偏移可达房间尺寸的 60%。两个房间的偏移都是独立随机的,
            // 相对位移最大能到 1.2 倍房间宽(约 21 格) —— 远超过"重叠 6 格"的要求,
            // 所以经常怎么试都连不上。
            //
            // 这里把偏移收到 ±0.25, 让新房间大概率正对着上一个房间的方向, 重叠就够 6 格了。
            // 副作用是布局会比原来规整一些(不那么"散"), 但换来的是生成稳定。
            const float dispersion = 0.25f;
            config.RoomHorizontalMaxDispersion = dispersion;
            config.RoomHorizontalMinDispersion = -dispersion;
            config.RoomVerticalMaxDispersion = dispersion;
            config.RoomVerticalMinDispersion = -dispersion;

            //房间数也收一点, 地图越满越难塞
            config.BattleRoomCount = 10;
            config.RoomMaxInterval = 3;
            config.AllowedCornerAisles = true;

            //Boss 房用的曲子。
            //AI 生成的 Boss.ogg / Boss_Full.ogg 已移除, 现在只剩 Scherzo 这一首 Boss 曲,
            //所以普通模式和魔王模式用的是同一首。以后渲染出《魔王》再改这一行。
            config.BossBgmId = "bgm_boss";
        }

        // config.RoomMaxInterval = 30;
        // config.RoomMinInterval = 10;
        // config.RoomHorizontalMaxDispersion = 2f;
        // config.RoomHorizontalMinDispersion = -2f;
        // config.RoomVerticalMaxDispersion = 2f;
        // config.RoomVerticalMinDispersion = -2f;
        return config;
    }

    /// <summary>
    /// 获取下一个地牢组名称
    /// </summary>
    /// <param name="currGroupName">当前地牢组名称</param>
    public string GetNextDungeonGroup(string currGroupName)
    {
        var index = DungeonGroupList.IndexOf(currGroupName);
        if (index == -1 || index == DungeonGroupList.Count - 1)
        {
            return null;
        }
        return DungeonGroupList[index + 1];
    }

    public override void _EnterTree()
    {
        //编辑器弹窗设置为使用windows弹窗
        GetViewport().GuiEmbedSubwindows = false;
        //背景颜色
        RenderingServer.SetDefaultClearColor(new Color(0, 0, 0, 1));
        //随机化种子
        GD.Randomize();
        //界面音效: 挂在节点添加事件上, 之后创建的所有按钮都会自动带音效
        //必须放在创建任何界面之前
        UiSound.Install(GetTree());
        //画质系统的全局钩子。地牢是逐层重建的, 靠 NodeAdded 才能覆盖到之后新建的
        //粒子/环境/相机节点; 存档里的具体数值在 LoadGameSave 之后由 GraphicsQuality.Apply 套用。
        GraphicsQuality.Install(GetTree());
        //帧率上限由存档决定(GameSave.Init 里应用), 这里不再硬编码。
        //【原来这里有个 bug】先写 Engine.MaxFps = TargetFps, 紧接着又写 = 300,
        //第二行把第一行覆盖掉了, 所以引擎实际一直跑在 300 fps。
        //Engine.TimeScale = 0.2f;
        
        //调整窗口分辨率
        CallDeferred(nameof(OnWindowSizeChanged));
        //窗体大小改变
        //GetWindow().SizeChanged += OnWindowSizeChanged;

        ImageCanvas.Init(GetTree().CurrentScene);
        
        //加载存档
        LoadGameSave(this);
        
        //调试Ui(开发者工具)。
        //这是内置的"外挂"(日志 / 工具 / 作弊按钮 / 节点检查器), 默认必须关闭,
        //只有玩家在【设置 - 开发者设置】里手动打开才显示。
        //注意顺序: 必须在 LoadGameSave 之后调用, 否则读不到开关状态。
        ApplyDevToolsVisible();
        
        // 初始化鼠标
        InitCursor();
        var backpackOverlay = new BackpackOverlay
        {
            Name = "BackpackOverlay"
        };
        AddChild(backpackOverlay);
        //地牢管理器
        DungeonManager = new DungeonManager(ActivityObject.Ids.Id_role0001);
        DungeonManager.Name = "DungeonManager";
        SceneRoot.AddChild(DungeonManager);

        MapProjectManager.Init();
        EditorTileSetManager.Init();
        BottomTipsPanel.Init();
        // 房间清空 / 楼层切换 / BOSS 预警与血条
        GameNotificationOverlay.Init(this);

        this.CallDelay(0, () =>
        {
            //打开主菜单Ui
            UiManager.Open_Game_Main();
        });
    }

    public override void _Process(double delta)
    {
        var newDelta = (float)delta;
        InputManager.Update(newDelta);
        SoundManager.Update(newDelta);
        GameSave.Tick(newDelta);
        
        //协程更新
        ProxyCoroutineHandler.ProxyUpdateCoroutine(ref _coroutineList, newDelta);
    }

    public override void _Input(InputEvent @event)
    {
        InputManager.GlobalInputHandler(@event);
    }

    /// <summary>
    /// 将Ui坐标转换为游戏中的世界坐标
    /// </summary>
    public Vector2 UiToWorldPosition(Vector2 uiPos)
    {
        if (PerfectPixel)
        {
            return (uiPos / PixelScale - ViewportSize / 2) / GameCamera.Main.Zoom - GameCamera.Main.PixelOffset + GameCamera.Main.GlobalPosition + GameCamera.Main.Offset;
        }

        return (uiPos - GetViewportRect().Size / 2) / GameCamera.Main.Zoom + GameCamera.Main.GlobalPosition + GameCamera.Main.Offset;
    }

    /// <summary>
    /// 将游戏中的世界坐标转换为Ui坐标
    /// </summary>
    public Vector2 WorldToUiPosition(Vector2 worldPos)
    {
        if (PerfectPixel)
        {
            return ((worldPos + GameCamera.Main.PixelOffset - GameCamera.Main.GlobalPosition - GameCamera.Main.Offset) * GameCamera.Main.Zoom + ViewportSize / 2) * PixelScale;
        }

        return (worldPos - GameCamera.Main.GlobalPosition - GameCamera.Main.Offset) * GameCamera.Main.Zoom + GetViewportRect().Size / 2;
    }

    public long StartCoroutine(IEnumerator able)
    {
        return ProxyCoroutineHandler.ProxyStartCoroutine(ref _coroutineList, able);
    }
    
    public void StopCoroutine(long coroutineId)
    {
        ProxyCoroutineHandler.ProxyStopCoroutine(ref _coroutineList, coroutineId);
    }

    public bool IsCoroutineOver(long coroutineId)
    {
        return ProxyCoroutineHandler.ProxyIsCoroutineOver(ref _coroutineList, coroutineId);
    }

    public void StopAllCoroutine()
    {
        ProxyCoroutineHandler.ProxyStopAllCoroutine(ref _coroutineList);
    }

    public void SetRoomConfig(Dictionary<string,DungeonRoomGroup> roomConfig)
    {
        foreach (var dungeonRoomGroup in roomConfig)
        {
            if (RoomConfig.TryGetValue(dungeonRoomGroup.Key, out var temp))
            {
                dungeonRoomGroup.Value.BgColor = temp.BgColor;
                dungeonRoomGroup.Value.SoundId = temp.SoundId;
            }
        }
        RoomConfig = roomConfig;
        InitReadyRoom();
    }

    /// <summary>
    /// 获取子视图容器的材质，该材质用于完美像素
    /// </summary>
    /// <returns></returns>
    public ShaderMaterial GetSubViewportContainerMaterial()
    {
        return (ShaderMaterial)SubViewportContainer.Material;
    }

    /// <summary>
    /// 设置是否启用完美像素
    /// </summary>
    public void SetPerfectPixel(bool v)
    {
        ViewCanvas.Visible = v;
        if (PerfectPixel == v) return;
        PerfectPixel = v;

        SceneRoot.Owner = null;
        GameCamera.Main.Owner = null;
        if (v) //完美像素
        {
            DefaultCameraZoom = Vector2.One;
            GameCamera.Main.Zoom = DefaultCameraZoom;
            SceneRoot.Reparent(SubViewport);
            GameCamera.Main.Reparent(SubViewport);
        }
        else
        {
            DefaultCameraZoom = new Vector2(PixelScale, PixelScale);
            GameCamera.Main.Zoom = DefaultCameraZoom;
            SceneRoot.Reparent(NoPerfectPixelRoot);
            GameCamera.Main.Reparent(NoPerfectPixelRoot);
        }
    }

    /// <summary>
    /// 按存档开关显示/隐藏两个开发者工具。
    ///
    /// 这两个工具是内置的"外挂", 普通玩家不应该看到:
    ///   1. 调试器悬浮图标 —— 左上角可拖动的小图标, 点开是日志/工具面板, 也显示 FPS,
    ///      里面还有"秒杀全部敌人"这类作弊按钮
    ///   2. 节点检查器 DsInspector —— 扫描整个场景树, 也提供 add_cheat_button 接口
    ///
    /// 两个开关互相独立, 分别由 GameSave.Debug.ShowDebuggerIcon / ShowInspector 控制。
    /// 默认都是 false, 也就是默认完全关闭。
    /// </summary>
    public void ApplyDevToolsVisible()
    {
        var save = GameSave;
        if (save?.Debug == null)
        {
            return;
        }

        //节点树还没搭完, 延后一帧再应用, 否则取不到刚创建的调试 Ui
        //每次调用都重置重试计数, 这样玩家在设置里开关后能重新走一遍等待流程
        _devToolRetry = 0;
        CallDeferred(nameof(ApplyDevToolsVisibleDeferred));
    }

    private void ApplyDevToolsVisibleDeferred()
    {
        var save = GameSave;
        if (save?.Debug == null)
        {
            return;
        }

        var showDebugger = save.Debug.ShowDebuggerIcon;
        var showInspector = save.Debug.ShowInspector;

        //---- 1. 调试器悬浮图标 ----
        //【加固】开关关闭时不只是"藏起来", 而是把面板从树里销毁。
        // 原因: HoverButton 在 Debugger.tscn 里默认是 visible = true,
        // 只要面板存在, 那个小图标(commonIcon/Debug.png)就会出现在左上角。
        // 光设 Visible = false 太脆弱 —— 任何一次重新 Open 都会让它复活,
        // 而且面板常驻内存也没必要。彻底销毁最干净。
        var panels = UiManager.GetUiInstance<UI.debug.Debugger.DebuggerPanel>(UiManager.UiName.Debug_Debugger);
        var exists = panels != null && panels.Length > 0;

        if (showDebugger)
        {
            var debuggerPanel = exists ? panels[0] : UiManager.Open_Debug_Debugger();
            if (debuggerPanel != null)
            {
                debuggerPanel.S_HoverButton.Instance.Visible = true;
            }
        }
        else
        {
            if (exists)
            {
                //先收起面板内容(它会顺带解除输入屏蔽), 再整体销毁
                panels[0].OnClose();
                UiManager.Destroy_Debug_Debugger();
            }
        }

        //---- 2. 节点检查器 DsInspector ----
        //左上角那个"扳手"图标就是这里来的: addons/ds_inspector/icon/Icon.png,
        //挂在 DsInspectorTool 的 HoverIcon(TextureButton) 上。
        //
        //【之前为什么关不掉】两个原因, 都踩中了:
        //  1. DsInspectorTool 的根节点是 CanvasLayer, 不是 CanvasItem ——
        //     CanvasLayer 根本没有 Visible 属性, 所以 "tool is CanvasItem" 判断
        //     永远为 false, 那句 Visible = false 从来没执行过。
        //     真正要藏的是它下面的 HoverIcon, 那才是个 CanvasItem。
        //  2. DsInspector 是 autoload, 它的 _deff_init 会把自己 reparent 到新建的
        //     DsInspectorTool 下面, 于是 "/root/DsInspector" 这个路径初始化之后就没了。
        //     所以不能死认路径, 改成按特征在 /root 下找。
        var tool = FindDsInspectorTool();
        if (tool != null)
        {
            ApplyInspectorVisible(tool, showInspector);
        }
        else if (_devToolRetry < DevToolRetryLimit)
        {
            //DsInspector 自己也是 call_deferred("_deff_init") 才把工具节点挂进树的,
            //谁先谁后不确定。没找到就再等一帧, 最多重试 DevToolRetryLimit 次。
            //(导出后 OS.has_feature("editor") 为假, 这个工具根本不会创建, 重试几次就会放弃。)
            _devToolRetry++;
            CallDeferred(nameof(ApplyDevToolsVisibleDeferred));
        }
    }

    /// <summary>DsInspector 工具节点延迟创建的等待上限(帧)</summary>
    private const int DevToolRetryLimit = 10;

    /// <summary>已经为找 DsInspector 工具节点重试了多少帧</summary>
    private int _devToolRetry;

    /// <summary>
    /// 按开关设置 DsInspector 那个"扳手"图标和面板的显示状态。
    /// 注意 HoverIcon 才是左上角看到的图标, 工具节点本身是 CanvasLayer, 没有 Visible。
    /// </summary>
    private static void ApplyInspectorVisible(Node tool, bool showInspector)
    {
        if (tool.GetNodeOrNull<CanvasItem>("HoverIcon") is { } hoverIcon)
        {
            hoverIcon.Visible = showInspector;
        }

        //关闭时顺手把已经弹出的面板收掉, 免得图标没了面板还开着
        if (!showInspector && tool.GetNodeOrNull<Window>("WindowDialog") is { } dialog)
        {
            dialog.Hide();
        }
    }

    /// <summary>
    /// 找到 DsInspector 的调试工具节点(DsInspectorTool, 一个 CanvasLayer)。
    ///
    /// 不能写死节点路径: DsInspector.gd 的 _deff_init 里执行了
    ///     get_parent().add_child(debug_tool)
    ///     reparent(debug_tool)
    /// 也就是把 autoload 自己搬到了新建的工具节点下面, "/root/DsInspector" 随后失效。
    /// 这里改成在 /root 的直接子节点里按特征找:
    /// "一个 CanvasLayer, 且带名为 HoverIcon 的子节点" 就是它。
    /// </summary>
    private Node FindDsInspectorTool()
    {
        var root = GetTree()?.Root;
        if (root == null)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is CanvasLayer layer && layer.GetNodeOrNull("HoverIcon") != null)
            {
                return layer;
            }
        }

        return null;
    }

    //初始化房间配置
    private void InitRoomConfig()
    {
        //加载房间配置信息
        var asText = ResourceManager.LoadText("res://" + GameConfig.RoomTileDir + GameConfig.RoomGroupConfigFile);
        RoomConfig = JsonSerializer.Deserialize<Dictionary<string, DungeonRoomGroup>>(asText);

        InitReadyRoom();
    }
    
    //初始化房间数据
    private void InitReadyRoom()
    {
        foreach (var dungeonRoomGroup in RoomConfig)
        {
            RemoveUnreadyRooms(dungeonRoomGroup.Value.BattleList);
            RemoveUnreadyRooms(dungeonRoomGroup.Value.InletList);
            RemoveUnreadyRooms(dungeonRoomGroup.Value.OutletList);
            RemoveUnreadyRooms(dungeonRoomGroup.Value.BossList);
            RemoveUnreadyRooms(dungeonRoomGroup.Value.ShopList);
            RemoveUnreadyRooms(dungeonRoomGroup.Value.RewardList);
            RemoveUnreadyRooms(dungeonRoomGroup.Value.EventList);
        }
    }
    
    //移除未准备好的房间
    private void RemoveUnreadyRooms(List<DungeonRoomSplit> roomInfos)
    {
        for (var i = 0; i < roomInfos.Count; i++)
        {
            if (roomInfos[i].ErrorType != RoomErrorType.None) //存在错误
            {
                roomInfos.RemoveAt(i);
                i--;
            }
        }
    }

    //初始化TileSet配置
    private void InitTileSetConfig()
    {
        //加载房间配置信息
        var asText = ResourceManager.LoadText("res://" + GameConfig.RoomTileSetDir + GameConfig.TileSetConfigFile);
        TileSetConfig = JsonSerializer.Deserialize<Dictionary<string, TileSetSplit>>(asText);
        
        //加载所有数据
        foreach (var tileSetSplit in TileSetConfig)
        {
            tileSetSplit.Value.ReloadTileSetInfo();
        }
    }

    //窗体大小改变
    private void OnWindowSizeChanged()
    {
        // var size = GetWindow().Size;
        // ViewportSize = size / PixelScale;
        RefreshSubViewportSize();
    }
    
    //刷新视窗大小
    private void RefreshSubViewportSize()
    {
        var s = new Vector2I((int)ViewportSize.X, (int)ViewportSize.Y);
        s.X = s.X / 2 * 2 + 2;
        s.Y = s.Y / 2 * 2 + 2;
        SubViewport.Size = s;
        SubViewportContainer.Scale = new Vector2(PixelScale, PixelScale);
        SubViewportContainer.SetDeferred(Control.PropertyName.Size, s);
        // SubViewportContainer.Size = s;
        SubViewportContainer.Position = new Vector2(-PixelScale, -PixelScale);
    }

    //初始化鼠标
    private void InitCursor()
    {
        Cursor = ResourceManager.LoadAndInstantiate<Cursor>(ResourcePath.prefab_Cursor_tscn);
        var cursorLayer = new CanvasLayer();
        cursorLayer.Name = "CursorLayer";
        cursorLayer.Layer = UiManager.GetUiLayer(UiLayer.Pop).Layer + 10;
        AddChild(cursorLayer);
        cursorLayer.AddChild(Cursor);
    }

    private void LoadGameSave(GameApplication app)
    {
        GameSave = GameSave.Load();
        GameSave.Init(app);
        //应用玩家自定义的键位
        KeyBindingManager.ApplyAll();
    }

    /// <summary>
    /// 设置手柄是否锁定瞄准
    /// </summary>
    public void SetJoystickAimAssist(bool flag)
    {
        
    }

    /// <summary>
    /// 设置手柄辅助瞄准强度
    /// </summary>
    public void SetJoystickAimAssistStrength(float value)
    {
        
    }
}
