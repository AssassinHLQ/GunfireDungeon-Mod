
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
        //固定帧率
        Engine.MaxFps = TargetFps;
        //Engine.TimeScale = 0.2f;
        Engine.MaxFps = 300;
        
        //调整窗口分辨率
        CallDeferred(nameof(OnWindowSizeChanged));
        //窗体大小改变
        //GetWindow().SizeChanged += OnWindowSizeChanged;

        ImageCanvas.Init(GetTree().CurrentScene);
        
        //加载存档
        LoadGameSave(this);
        
        //调试Ui
        UiManager.Open_Debug_Debugger();
        
        // 初始化鼠标
        InitCursor();
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
