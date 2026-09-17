
using System;
using System.Collections;
using System.Linq;
using Config;
using DsUi;
using Godot;
using UI.game.BottomTips;

/// <summary>
/// 地牢管理器
/// </summary>
public partial class DungeonManager : Node2D
{
    /// <summary>
    /// 起始房间
    /// </summary>
    public RoomInfo StartRoomInfo => _dungeonGenerator?.StartRoomInfo;
    
    /// <summary>
    /// 当前玩家所在的房间
    /// </summary>
    public RoomInfo ActiveRoomInfo => CurrWorld.Player?.AffiliationArea?.RoomInfo;
    
    /// <summary>
    /// 当前玩家所在的区域
    /// </summary>
    public AffiliationArea ActiveAffiliationArea => CurrWorld.Player?.AffiliationArea;

    /// <summary>
    /// 是否在地牢里
    /// </summary>
    public bool IsInDungeon { get; private set; }

    /// <summary>
    /// 是否是编辑器模式
    /// </summary>
    public bool IsEditorMode { get; private set; }
    
    /// <summary>
    /// 当前使用的配置
    /// </summary>
    public DungeonConfig CurrConfig { get; private set; }
    
    /// <summary>
    /// 当前玩家所在游戏世界对象
    /// </summary>
    public World CurrWorld { get; private set; }

    /// <summary>
    /// 自动图块配置
    /// </summary>
    public AutoTileConfig AutoTileConfig { get; private set; }
    
    /// <summary>
    /// 加载角色ID
    /// </summary>
    public string LoadRoleId { get; set; }

    /// <summary>
    /// 楼层计划, 定义本局依次经过哪些楼层。
    /// 顺序来自 resource/config/FloorPlan.json, 因此同一楼层可以出现多次,
    /// 也可以插入隐藏楼层。改楼层结构只需要改那个 JSON。
    /// </summary>
    public FloorPlan Plan { get; } = FloorPlan.Load();

    /// <summary>
    /// 当前所在层数, 从 1 开始
    /// </summary>
    public int CurrentFloor => Plan.CurrentNumber;

    /// <summary>
    /// 当前楼层定义(名称/副标题/强度系数)
    /// </summary>
    public FloorPlan.FloorDef CurrentFloorDef => Plan.Current;

    /// <summary>
    /// 当前楼层名, 例如「五楼 · 505」
    /// </summary>
    public string CurrentFloorName => Plan.CurrentName;

    /// <summary>
    /// 本局总层数(隐藏层插入后会增加)
    /// </summary>
    public int TotalFloors => Plan.Count;

    /// <summary>
    /// 当前是否已经是最后一层
    /// </summary>
    public bool IsLastFloor => Plan.IsLast;

    /// <summary>
    /// 每局开始时重置楼层
    /// </summary>
    public void ResetFloor()
    {
        Plan.Reset();
    }

    /// <summary>
    /// 是否已经打完最后一层的 BOSS (打完最后一层出口即通关)
    /// </summary>
    public bool IsFinalFloorCleared => Plan.IsLast;

    /// <summary>
    /// 解锁并插入隐藏楼层(例如负一楼)。
    /// 已经插入过、或配置里没有隐藏层时返回 false。
    /// </summary>
    public bool UnlockHiddenFloor()
    {
        var ok = Plan.TryInsertHidden();
        if (ok)
        {
            Debug.Log($"已解锁隐藏楼层: {Plan.Hidden?.Name}, 总层数变为 {Plan.Count}");
        }

        return ok;
    }

    /// <summary>
    /// 魔王模式下敌人血量的倍率。
    /// 魔王模式每个房间基本都有 Boss, 按原始 1200 血打下来节奏太拖, 所以砍半。
    /// </summary>
    public const float ErlkoenigHpMultiplier = 0.5f;

    /// <summary>
    /// 按当前楼层 + 当前地牢模式调整敌人强度。
    /// </summary>
    public void ApplyFloorDifficulty(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        //楼层倍率(来自 FloorPlan.json)
        var multiplier = Plan.CurrentHpMultiplier;

        //模式倍率。
        //魔王模式要连续打 Boss, 所以额外砍血量。
        //注意不能直接改 RoleBase.json 里 daju0001 的 Hp —— 普通模式最后那个
        //boss/Boss1 房用的是同一个配置, 改了会连带影响普通模式。
        if (CurrConfig != null && CurrConfig.Mode == DungeonMode.Erlkoenig)
        {
            multiplier *= ErlkoenigHpMultiplier;
        }

        if (Mathf.IsEqualApprox(multiplier, 1f))
        {
            return;
        }

        enemy.MaxHp = Mathf.Max(1, Mathf.RoundToInt(enemy.MaxHp * multiplier));
        enemy.Hp = enemy.MaxHp;
    }
    
    private UiBase _prevUi;
    private DungeonTileMap _dungeonTileMap;
    private DungeonGenerator _dungeonGenerator;
    //防止重复触发进入下一层
    private bool _isAdvancingFloor;
    
    //用于检查房间敌人的计时器
    private float _checkEnemyTimer = 0;
    //用于记录玩家上一个所在区域
    private AffiliationArea _affiliationAreaFlag;
    private Role _cachePlayer;

    /// <summary>
    /// Boss 房间使用的 BGM (Sound.json 里的 Id)。
    /// 原来游戏只播"地牢组"的 BGM(DungeonRoomGroup.SoundId), 进 Boss 房不会换曲子。
    /// 这里加一层按房间类型覆盖: 进 Boss 房换成这首, 出去换回地牢组的。
    ///
    /// 如果当前 DungeonConfig 指定了 BossBgmId(比如魔王模式), 优先生效。
    ///
    /// 注: AI 生成的 Boss.ogg / Boss_Full.ogg 已按用户要求移除, 现在只剩这一首 Boss 曲,
    /// 所以普通模式和魔王模式的 Boss 房都用它。
    /// </summary>
    public const string BossRoomBgmId = "bgm_boss";

    /// <summary>
    /// 大厅使用的 BGM (Sound.json 里的 Id)。
    /// 大厅不属于任何地牢组, 所以不能靠 DungeonRoomGroup.SoundId, 只能单独指定。
    /// 想换曲子: 改这里, 或者改 Sound.json 里 bgm_hall 指向的文件。
    /// </summary>
    public const string HallBgmId = "bgm_hall";

    /// <summary>
    /// 当前正在播放的 BGM Id, 用于避免重复播放同一首(重复调用会打断循环)
    /// </summary>
    private string _currBgmId;

    public DungeonManager(string loadRoleId)
    {
        LoadRoleId = loadRoleId;
        //绑定事件
        EventManager.AddEventListener(EventEnum.OnPlayerFirstEnterRoom, OnPlayerFirstEnterRoom);
        EventManager.AddEventListener(EventEnum.OnPlayerEnterRoom, OnPlayerEnterRoom);
    }
    
    /// <summary>
    /// 创建新的 World 对象, 相当于清理房间
    /// </summary>
    public World CreateNewWorld(SeedRandom random, string scenePath)
    {
        if (CurrWorld != null)
        {
            ClearWorld();
            CurrWorld.QueueFree();
        }
        CurrWorld = ResourceManager.LoadAndInstantiate<World>(scenePath);
        GameApplication.Instance.SceneRoot.AddChild(CurrWorld);
        CurrWorld.InitRandomPool(random);
        return CurrWorld;
    }

    /// <summary>
    /// 销毁 World 对象, 相当于清理房间
    /// </summary>
    public void DestroyWorld()
    {
        //销毁所有物体
        if (CurrWorld != null)
        {
            ClearWorld();
            CurrWorld.Destroy();
        }
        
        //销毁池中所有物体
        ObjectPool.DisposeAllItem();

        CurrWorld = null;
    }
    
    
    //清理世界
    private void ClearWorld()
    {
        var childCount = CurrWorld.NormalLayer.GetChildCount();
        for (var i = 0; i < childCount; i++)
        {
            var c = CurrWorld.NormalLayer.GetChild(i);
            if (c is IDestroy destroy)
            {
                destroy.Destroy();
            }
        }
        childCount = CurrWorld.YSortLayer.GetChildCount();
        for (var i = 0; i < childCount; i++)
        {
            var c = CurrWorld.YSortLayer.GetChild(i);
            if (c is IDestroy destroy)
            {
                destroy.Destroy();
            }
        }
    }

    /// <summary>
    /// 进入大厅
    /// </summary>
    public void LoadHall(Action finish = null)
    {
        GameApplication.Instance.StartCoroutine(RunLoadHallCoroutine(finish));
    }

    /// <summary>
    /// 退出大厅
    /// </summary>
    public void ExitHall(bool keepPlayer, Action finish = null)
    {
        GameApplication.Instance.StartCoroutine(RunExitHallCoroutine(keepPlayer, finish));
    }
    
    /// <summary>
    /// 加载地牢
    /// </summary>
    public void LoadDungeon(DungeonConfig config, Action finish = null)
    {
        IsEditorMode = false;
        CurrConfig = config;
        GameApplication.Instance.StartCoroutine(RunLoadDungeonCoroutine(finish));
    }
    
    /// <summary>
    /// 重启地牢
    /// </summary>
    public void RestartDungeon(bool keepPlayer, DungeonConfig config, Action finish = null)
    {
        IsEditorMode = false;
        CurrConfig = config;
        ExitDungeon(keepPlayer, () =>
        {
            LoadDungeon(CurrConfig, finish);
        });
    }

    /// <summary>
    /// 退出地牢
    /// </summary>
    public void ExitDungeon(bool keepPlayer, Action finish = null)
    {
        IsInDungeon = false;
        GameApplication.Instance.StartCoroutine(RunExitDungeonCoroutine(keepPlayer, finish));
    }

    /// <summary>
    /// 保留玩家、武器和道具, 进入下一层
    /// </summary>
    public void AdvanceToNextFloor()
    {
        if (_isAdvancingFloor || !IsInDungeon || CurrConfig == null)
        {
            return;
        }

        if (Plan.IsLast)
        {
            //最后一层应该走通关流程, 由 RoomExit 处理
            return;
        }

        _isAdvancingFloor = true;
        if (!Plan.MoveNext())
        {
            _isAdvancingFloor = false;
            return;
        }

        Debug.Log($"进入 {Plan.CurrentName}");
        UiManager.Open_Game_Loading();
        RestartDungeon(true, CurrConfig, () =>
        {
            UiManager.Destroy_Game_Loading();
            _isAdvancingFloor = false;
            //跨层时保留角色与道具，但护盾应从最大值重新开始。
            CurrWorld?.Player?.RestoreShieldToMax();
            //进入新层后弹出提示
            ShowFloorNotification();
        });
    }

    /// <summary>
    /// 弹出当前楼层提示
    /// </summary>
    public void ShowFloorNotification()
    {
        if (!IsInDungeon)
        {
            return;
        }

        var floor = Plan.Current;
        var subtitle = floor != null && !string.IsNullOrEmpty(floor.Subtitle)
            ? $"\n{floor.Subtitle}"
            : string.Empty;

        //剧情探索层明确告诉玩家这里没有敌人, 让他敢停下来搜查
        var hint = Plan.CurrentIsExplore
            ? "\n这里没有敌人，可以安心搜查"
            : string.Empty;

        BottomTipsPanel.ShowTips(
            ResourcePath.resource_sprite_box_TreasureBox0001_icon_png,
            $"进入 {Plan.CurrentName}{subtitle}{hint}"
        );
    }
    
    //-------------------------------------------------------------------------------------

    /// <summary>
    /// 在编辑器模式下进入地牢
    /// </summary>
    /// <param name="config">地牢配置</param>
    /// <param name="finish">执行完成回调</param>
    public void EditorPlayDungeon(DungeonConfig config, Action finish = null)
    {
        IsEditorMode = true;
        CurrConfig = config;
        if (_prevUi != null)
        {
            _prevUi.HideUi();
        }
        GameApplication.Instance.StartCoroutine(RunLoadDungeonCoroutine(finish));
    }

    /// <summary>
    /// 在编辑器模式下进入地牢
    /// </summary>
    /// <param name="prevUi">记录上一个Ui</param>
    /// <param name="config">地牢配置</param>
    /// <param name="finish">执行完成回调</param>
    public void EditorPlayDungeon(UiBase prevUi, DungeonConfig config, Action finish = null)
    {
        IsEditorMode = true;
        CurrConfig = config;
        _prevUi = prevUi;
        if (_prevUi != null)
        {
            _prevUi.HideUi();
        }
        GameApplication.Instance.StartCoroutine(RunLoadDungeonCoroutine(finish));
    }

    /// <summary>
    /// 在编辑器模式下退出地牢, 并且打开上一个Ui
    /// </summary>
    public void EditorExitDungeon(bool keepPlayer, Action finish = null)
    {
        IsInDungeon = false;
        GameApplication.Instance.StartCoroutine(RunExitDungeonCoroutine(keepPlayer, () =>
        {
            IsEditorMode = false;
            //显示上一个Ui
            if (_prevUi != null)
            {
                _prevUi.ShowUi();
            }
            GameApplication.Instance.Cursor.RefreshCursor();
            if (finish != null)
            {
                finish();
            }
        }));
    }
    
    //-------------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (IsInDungeon)
        {
            if (CurrWorld.Pause) //已经暂停
            {
                return;
            }
            
            //暂停游戏
            if (InputManager.Menu)
            {
                CurrWorld.Pause = true;
                //打开暂停Ui
                UiManager.Open_Game_PauseMenu();
            }
            
            //更新迷雾
            FogMaskHandler.Update();
            
            _checkEnemyTimer += (float)delta;
            if (_checkEnemyTimer >= 1)
            {
                _checkEnemyTimer %= 1;
                //检查房间内的敌人存活状况
                OnCheckEnemy();
            }
            
            if (ActivityObject.IsDebug)
            {
                QueueRedraw();
            }
        }
    }

    //执行加载大厅流程
    private IEnumerator RunLoadHallCoroutine(Action finish)
    {
        yield return 0;
        var hall = (Hall)CreateNewWorld(Utils.Random, ResourcePath.scene_Hall_tscn);
        IsInDungeon = true;
        yield return 0;

        //创建房间数据
        var roomInfo = new RoomInfo(0, DungeonRoomType.None, null);
        roomInfo.World = CurrWorld;
        var usedRect = hall.GetUsedRect();
        roomInfo.Size = usedRect.Size;
        roomInfo.Position = usedRect.Position;
        hall.RoomInfo = roomInfo;
        yield return 0;
        
        //创建归属区域
        var affiliation = new AffiliationArea();
        affiliation.Name = "AffiliationArea_Hall";
        affiliation.Init(roomInfo, new Rect2I(roomInfo.Position, roomInfo.Size * GameConfig.TileCellSize));
        roomInfo.AffiliationArea = affiliation;
        hall.AffiliationAreaRoot.AddChild(affiliation);
        yield return 0;
        
        //静态渲染精灵根节点, 用于放置sprite
        var spriteRoot = new RoomStaticSprite(roomInfo);
        roomInfo.StaticSprite = spriteRoot;
        hall.StaticSpriteRoot.AddChild(spriteRoot);
        yield return 0;
        
        //静态精灵画布
        var canvasSprite = new ImageCanvas(roomInfo.Size.X * GameConfig.TileCellSize, roomInfo.Size.Y * GameConfig.TileCellSize);
        canvasSprite.Position = roomInfo.Position - (roomInfo.Size - new Vector2(canvasSprite.Width, canvasSprite.Height)) / 2;
        roomInfo.StaticImageCanvas = canvasSprite;
        roomInfo.StaticSprite.AddChild(canvasSprite);
        yield return 0;
        
        //液体画布
        var liquidCanvas = new LiquidCanvas(roomInfo);
        roomInfo.LiquidCanvas = liquidCanvas;
        roomInfo.StaticSprite.AddChild(liquidCanvas);
        yield return 0;
        
        //打开游戏中的ui
        UiManager.Open_Game_RoomUI();
        yield return 0;
        
        //创建玩家
        var player = CurrWorld.Player;
        if (player == null)
        {
            player = ActivityObject.Create<Player>(ActivityObject.Ids.Id_role0001);
            player.Name = "Player";
        }
        player.World = CurrWorld;
        player.Position = hall.BirthMark.Position;
        player.PutDown(RoomLayerEnum.YSortLayer);
        CurrWorld.SetCurrentPlayer(player);
        affiliation.InsertItem(player);
        //player.WeaponPack.PickupItem(ActivityObject.Create<Weapon>(ActivityObject.Ids.Id_weapon0001));
        
        yield return 0;
        player.Collision.Disabled = false;
        GameCamera.Main.Zoom = GameApplication.Instance.DefaultCameraZoom;
        
        yield return 0;
        GameApplication.Instance.Cursor.RefreshCursor();

        //大厅背景音乐。
        //
        //【为什么原来完全没有音乐】PlayDungeonBgm 只在两个地方被调用:
        //   · 地牢加载完成后(LoadDungeon 协程里)
        //   · 玩家进入房间时(OnPlayerEnterRoom, 且只在地牢里)
        // 大厅走的是这条 RunLoadHallCoroutine, 从来没有人播过 BGM,
        // 所以 Sound.json 里就算配了 bgm_hall 也永远听不到。
        _currBgmId = null;                  //清掉上一局的记录, 否则会被"已在播放同一首"挡住
        PlayDungeonBgm(HallBgmId);

        if (finish != null)
        {
            finish();
        }
    }
    
    //执行退出大厅流程
    private IEnumerator RunExitHallCoroutine(bool keepPlayer, Action finish)
    {
        IsInDungeon = false;
        yield return 0;
        
        CurrWorld.Pause = true;
        yield return 0;

        var hall = (Hall)CurrWorld;
        hall.RoomInfo.Destroy();
        yield return 0;
        
        UiManager.Destroy_Game_RoomUI();
        yield return 0;
        if (!keepPlayer)
        {
            CurrWorld.SetCurrentPlayer(null);
        }
        else
        {
            var player = CurrWorld.Player;
            player.AffiliationArea?.RemoveItem(player);
            player.GetParent().RemoveChild(player);
            player.World = null;
            player.Collision.Disabled = true;
        }

        CurrWorld.OnUnloadSuccess();
        DestroyWorld();
        yield return 0;
        FogMaskHandler.ClearRecordRoom();
        QueueRedraw();
        
        yield return 0;
        GameApplication.Instance.Cursor.RefreshCursor();
        if (finish != null)
        {
            finish();
        }
    }
    

    //执行加载地牢协程
    private IEnumerator RunLoadDungeonCoroutine(Action finish)
    {
        yield return 0;
        //生成地牢房间
        
        //最多尝试 20 次。
        //原来是 10 次。每次是一整套独立随机的地牢生成, 理论上重试一次就该成功,
        //但魔王模式房间多、又要连 Boss 房, 单次成功率没那么高, 翻倍留些余量。
        //重试只在真正失败时才发生, 不影响正常情况下的耗时。
        const int maxCount = 20;
        for (var i = 0; i < maxCount; i++)
        {
            SeedRandom random;
            if (CurrConfig.RandomSeed != null)
            {
                random = new SeedRandom(CurrConfig.RandomSeed.Value);
            }
            else
            {
                random = new SeedRandom();
            }
       
            var dungeonGenerator = new DungeonGenerator(CurrConfig, random);
            var rule = new DefaultDungeonRule(dungeonGenerator);
            if (!dungeonGenerator.Generate(rule)) //生成房间失败
            {
                dungeonGenerator.EachRoom(DisposeRoomInfo);
                if (i == maxCount - 1)
                {
                    if (IsEditorMode) //在编辑器模式下打开的Ui
                    {
                        EditorPlayManager.IsPlay = false;
                        IsEditorMode = false;
                        //显示上一个Ui
                        if (_prevUi != null)
                        {
                            _prevUi.ShowUi();
                            //尝试关闭加载Ui
                            UiManager.Destroy_Game_Loading();
                        }
                    }
                    else //正常关闭Ui
                    {
                        UiManager.Open_Game_Main();
                        //尝试关闭加载Ui
                        UiManager.Destroy_Game_Loading();
                    }
                    EditorWindowManager.ShowTips("错误", "生成房间尝试次数过多，生成地牢房间失败，请加大房间门连接区域，或者修改地牢生成规则！");
                    yield break;
                }
                else
                {
                    yield return 0;
                }
            }
            else //生成成功!
            {
                _dungeonGenerator = dungeonGenerator;
                break;
            }
        }
        
        yield return 0;
        //创建世界场景
        var dungeon = (Dungeon)CreateNewWorld(_dungeonGenerator.Random, ResourcePath.scene_Dungeon_tscn);
        dungeon.DungeonConfig = _dungeonGenerator.Config;
        dungeon.RoomGroup = _dungeonGenerator.RoomGroup;
        dungeon.InitLayer();
        //初始化房间 World 字段
        foreach (var roomInfo in _dungeonGenerator.RoomInfos)
        {
            roomInfo.World = dungeon;
        }
        IsInDungeon = true;
        yield return 0;
        var group = GameApplication.Instance.RoomConfig[CurrConfig.GroupName];
        var tileSetSplit = GameApplication.Instance.TileSetConfig[group.TileSet];
        CurrWorld.SetTileSet(tileSetSplit.GetTileSet());
        //填充地牢
        AutoTileConfig = new AutoTileConfig(0, tileSetSplit.TileSetInfo.Sources[0].Terrain[0]);
        _dungeonTileMap = new DungeonTileMap(CurrWorld);
        yield return _dungeonTileMap.AutoFillRoomTile(AutoTileConfig, _dungeonGenerator.StartRoomInfo, CurrWorld);
        yield return _dungeonTileMap.AutoFillAisleTile(AutoTileConfig, _dungeonGenerator.StartRoomInfo, CurrWorld);
        //yield return _dungeonTileMap.AddOutlineTile(AutoTileConfig.WALL_BLOCK);
        yield return 0;
        //生成墙壁, 生成导航网格
        _dungeonTileMap.GenerateWallAndNavigation(CurrWorld, AutoTileConfig);
        yield return 0;
        //初始化所有房间
        yield return _dungeonGenerator.EachRoomCoroutine(InitRoom);
        
        //Debug.Log("[临时处理]: 关闭房间迷雾");
        //CurrWorld.FogMaskRoot.Visible = false;

        //房间背景颜色
        RenderingServer.SetDefaultClearColor(_dungeonGenerator.RoomGroup.BgColor.AsColor());
        
        //播放bgm
        //先清掉上一层的记录: World.OnUnloadSuccess 会 StopBgm, 但 _currBgmId 是
        //DungeonManager 的字段、跨层保留。不清的话, 如果新一层的 BGM Id 和上一层相同,
        //PlayDungeonBgm 会以为"已经在放同一首"而直接 return, 结果新层没有音乐。
        _currBgmId = null;
        PlayDungeonBgm(_dungeonGenerator.RoomGroup.SoundId);
        
        GameCamera.Main.FollowsMouseAmount = GameApplication.Instance.GameSave.FollowsMouseAmount;

        //地牢加载即将完成
        yield return _dungeonGenerator.EachRoomCoroutine(info => info.OnReady());
        
        //打开游戏中的ui
        UiManager.Open_Game_RoomUI();
        yield return 0;
        
        //创建自定义物体
        _dungeonGenerator.EachRoom(OnInitCustomObject);
        yield return 0;
        
        //初始房间创建玩家标记
        var playerBirthMark = StartRoomInfo.RoomPreinstall.GetSpecialMark(SpecialMarkType.BirthPoint);
        
        //创建玩家
        var player = CurrWorld.Player ?? _cachePlayer;
        if (player == null)
        {
            player = ActivityObject.Create<Role>(LoadRoleId);
            player.Name = "Player";
        }
        if (playerBirthMark != null)
        {
            player.Position = playerBirthMark.Position;
        }

        _cachePlayer = null;

        player.World = CurrWorld;
        player.PutDown(RoomLayerEnum.YSortLayer);
        CurrWorld.SetCurrentPlayer(player);
        StartRoomInfo.AffiliationArea.InsertItem(player);
        yield return 0;
        player.Collision.Disabled = false;
        
        GameCamera.Main.Zoom = GameApplication.Instance.DefaultCameraZoom;
        //派发进入地牢事件
        EventManager.EmitEvent(EventEnum.OnEnterDungeon);
        
        QueueRedraw();
        yield return 0;
        
        CurrWorld.OnLoadSuccess();
        GameApplication.Instance.Cursor.RefreshCursor();
        if (finish != null)
        {
            finish();
        }
    }

    //执行退出地牢流程
    private IEnumerator RunExitDungeonCoroutine(bool keepPlayer, Action finish)
    {
        yield return 0;
        CurrWorld.Pause = true;
        yield return 0;
        _dungeonGenerator?.EachRoom(DisposeRoomInfo);
        yield return 0;
        _dungeonTileMap = null;
        AutoTileConfig = null;
        _dungeonGenerator = null;
        
        UiManager.Destroy_Game_RoomUI();
        yield return 0;
        if (!keepPlayer)
        {
            CurrWorld.SetCurrentPlayer(null);
            if (_cachePlayer != null)
            {
                _cachePlayer.Destroy();
                _cachePlayer = null;
            }
        }
        else
        {
            var player = CurrWorld.Player;
            player.AffiliationArea?.RemoveItem(player);
            player.GetParent().RemoveChild(player);
            player.World = null;
            player.Collision.Disabled = true;
            _cachePlayer = player;
        }

        yield return 0;

        CurrWorld.OnUnloadSuccess();
        DestroyWorld();
        yield return 0;
        //还原房间背景颜色
        RenderingServer.SetDefaultClearColor(Colors.Black);
        
        FogMaskHandler.ClearRecordRoom();
        QueueRedraw();
        //派发退出地牢事件
        EventManager.EmitEvent(EventEnum.OnExitDungeon);
        yield return 0;
        if (finish != null)
        {
            finish();
        }
    }

    //初始化自定义物体
    private void OnInitCustomObject(RoomInfo info)
    {
        var tileInfo = info.RoomSplit.TileInfo;
        if (tileInfo.NormalLayerObjects != null)
        {
            foreach (var objectInfo in tileInfo.NormalLayerObjects)
            {
                CreateCustomObject(info, objectInfo, RoomLayerEnum.NormalLayer);
            }
        }

        if (tileInfo.YSortLayerObjects != null)
        {
            foreach (var objectInfo in tileInfo.YSortLayerObjects)
            {
                CreateCustomObject(info, objectInfo, RoomLayerEnum.YSortLayer);
            }
        }
    }

    private void CreateCustomObject(RoomInfo roomInfo, RoomObjectInfo info, RoomLayerEnum layer)
    {
        if (ExcelConfig.EditorObject_Map.TryGetValue(info.Id, out var editorObject))
        {
            if (editorObject.IsActivity())
            {
                var activityObject = ActivityObject.Create(editorObject.Prefab);
                activityObject.Position = roomInfo.ToGlobalPosition(new Vector2(info.X, info.Y));
                activityObject.PutDown(layer);
            }
            else
            {
                var node = ResourceManager.LoadAndInstantiate<Node2D>(editorObject.Prefab);
                node.Position = roomInfo.ToGlobalPosition(new Vector2(info.X, info.Y));
                node.AddToActivityRoot(layer);
            }
        }
        else
        {
            Debug.LogError($"创建CustomObject没有找到物体配置: {info.Id}!");
        }
    }
    
    // 初始化房间
    private void InitRoom(RoomInfo roomInfo)
    {
        roomInfo.CalcRange();
        //创建门
        CreateDoor(roomInfo);
        //创建房间归属区域
        CreateRoomAffiliation(roomInfo);
        //创建 RoomStaticSprite
        CreateRoomStaticSprite(roomInfo);
        //创建静态精灵画布
        CreateRoomStaticImageCanvas(roomInfo);
        //创建液体区域
        CreateRoomLiquidCanvas(roomInfo);
        //创建迷雾遮罩
        CreateRoomFogMask(roomInfo);
        //创建房间/过道预览sprite
        CreatePreviewSprite(roomInfo);
    }

    //创建门
    private void CreateDoor(RoomInfo roomInfo)
    {
        foreach (var doorInfo in roomInfo.Doors)
        {
            RoomDoor door;
            switch (doorInfo.Direction)
            {
                case DoorDirection.E:
                    door = ActivityObject.Create<RoomDoor>(ActivityObject.Ids.Id_other_door_e);
                    door.Position = (doorInfo.OriginPosition + new Vector2(0.5f, 2)) * GameConfig.TileCellSize;
                    break;
                case DoorDirection.W:
                    door = ActivityObject.Create<RoomDoor>(ActivityObject.Ids.Id_other_door_w);
                    door.Position = (doorInfo.OriginPosition + new Vector2(-0.5f, 2)) * GameConfig.TileCellSize;
                    break;
                case DoorDirection.S:
                    door = ActivityObject.Create<RoomDoor>(ActivityObject.Ids.Id_other_door_s);
                    door.Position = (doorInfo.OriginPosition + new Vector2(2f, 1.5f)) * GameConfig.TileCellSize;
                    break;
                case DoorDirection.N:
                    door = ActivityObject.Create<RoomDoor>(ActivityObject.Ids.Id_other_door_n);
                    door.Position = (doorInfo.OriginPosition + new Vector2(2f, -0.5f)) * GameConfig.TileCellSize;
                    break;
                default:
                    return;
            }
            doorInfo.Door = door;
            door.Init(doorInfo);
            door.PutDown(RoomLayerEnum.YSortLayer, false);
        }
    }

    //创建房间归属区域
    private void CreateRoomAffiliation(RoomInfo roomInfo)
    {
        var affiliation = new AffiliationArea();
        affiliation.Name = "AffiliationArea" + roomInfo.Id;
        affiliation.Init(roomInfo, new Rect2I(
            roomInfo.GetWorldPosition() + new Vector2I(GameConfig.TileCellSize * 2, GameConfig.TileCellSize * 3),
            (roomInfo.Size - new Vector2I(4, 5)) * GameConfig.TileCellSize));
        
        roomInfo.AffiliationArea = affiliation;
        CurrWorld.AffiliationAreaRoot.AddChild(affiliation);
    }

    //创建 RoomStaticSprite
    private void CreateRoomStaticSprite(RoomInfo roomInfo)
    {
        var spriteRoot = new RoomStaticSprite(roomInfo);
        World.Current.StaticSpriteRoot.AddChild(spriteRoot);
        roomInfo.StaticSprite = spriteRoot;
    }
    
    
    //创建静态图像画布
    private void CreateRoomStaticImageCanvas(RoomInfo roomInfo)
    {
        var rect = roomInfo.CanvasRect;

        var canvasSprite = new ImageCanvas(rect.Size.X, rect.Size.Y);
        canvasSprite.Position = rect.Position;
        roomInfo.StaticImageCanvas = canvasSprite;
        roomInfo.StaticSprite.AddChild(canvasSprite);
    }
    
        
    //创建液体画布
    private void CreateRoomLiquidCanvas(RoomInfo roomInfo)
    {
        // var rect = roomInfo.CanvasRect;
        // var liquidCanvas = new LiquidCanvas(roomInfo, rect.Size.X, rect.Size.Y);
        // liquidCanvas.Position = rect.Position;
        
        var liquidCanvas = new LiquidCanvas(roomInfo);
        roomInfo.LiquidCanvas = liquidCanvas;
        roomInfo.StaticSprite.AddChild(liquidCanvas);
    }

    //创建迷雾遮罩
    private void CreateRoomFogMask(RoomInfo roomInfo)
    {
        var roomFog = new FogMask();
        roomFog.Name = "FogMask" + roomFog.IsDestroyed;
        roomFog.InitFog(roomInfo.Position + new Vector2I(1, 0), roomInfo.Size - new Vector2I(2, 1));
        //roomFog.InitFog(roomInfo.Position + new Vector2I(1, 1), roomInfo.Size - new Vector2I(2, 2));

        CurrWorld.FogMaskRoot.AddChild(roomFog);
        roomInfo.RoomFogMask = roomFog;
        
        //生成通道迷雾
        foreach (var roomDoorInfo in roomInfo.Doors)
        {
            //必须是正向门
            if (roomDoorInfo.IsForward)
            {
                Rect2I calcRect;
                Rect2I fogAreaRect;
                if (!roomDoorInfo.HasCross)
                {
                    calcRect = roomDoorInfo.GetAisleRect();
                    fogAreaRect = calcRect;
                    if (roomDoorInfo.Direction == DoorDirection.E || roomDoorInfo.Direction == DoorDirection.W)
                    {
                        calcRect.Position += new Vector2I(2, 0);
                        calcRect.Size -= new Vector2I(4, 0);
                    }
                    else
                    {
                        calcRect.Position += new Vector2I(0, 2);
                        calcRect.Size -= new Vector2I(0, 5);
                    }
                }
                else
                {
                    var aisleRect = roomDoorInfo.GetCrossAisleRect();
                    calcRect = aisleRect.CalcAisleRect();
                    fogAreaRect = calcRect;

                    if (roomDoorInfo.Direction == DoorDirection.E)
                    {
                        if (roomDoorInfo.ConnectDoor.Direction == DoorDirection.N) //→↑
                        {
                            calcRect.Position += new Vector2I(2, 0);
                            calcRect.Size -= new Vector2I(2, 4);
                        }
                        else //→↓
                        {
                            calcRect.Position += new Vector2I(2, 3);
                            calcRect.Size -= new Vector2I(2, 3);
                        }
                    }
                    else if (roomDoorInfo.Direction == DoorDirection.W)
                    {
                        if (roomDoorInfo.ConnectDoor.Direction == DoorDirection.N) //←↑
                        {
                            calcRect.Size -= new Vector2I(2, 4);
                        }
                        else //←↓
                        {
                            calcRect.Position += new Vector2I(0, 3);
                            calcRect.Size -= new Vector2I(2, 3);
                        }
                    }
                    else if (roomDoorInfo.Direction == DoorDirection.N)
                    {
                        if (roomDoorInfo.ConnectDoor.Direction == DoorDirection.E) //↑→
                        {
                            calcRect.Position += new Vector2I(2, -1);
                            calcRect.Size -= new Vector2I(2, 2);
                        }
                        else //↑←
                        {
                            calcRect.Position += new Vector2I(0, -1);
                            calcRect.Size -= new Vector2I(2, 2);
                        }
                    }
                    else if (roomDoorInfo.Direction == DoorDirection.S)
                    {
                        if (roomDoorInfo.ConnectDoor.Direction == DoorDirection.E) //↓→
                        {
                            calcRect.Position += new Vector2I(2, 2);
                            calcRect.Size -= new Vector2I(2, 1);
                        }
                        else //↓←
                        {
                            calcRect.Position += new Vector2I(0, 2);
                            calcRect.Size -= new Vector2I(2, 1);
                        }
                    }
                }

                //过道迷雾遮罩
                var aisleFog = new FogMask();
                var calcRectSize = calcRect.Size;
                var calcRectPosition = calcRect.Position;
                if (roomDoorInfo.Direction == DoorDirection.N || roomDoorInfo.Direction == DoorDirection.S)
                {
                    calcRectSize.Y -= 1;
                }
                else
                {
                    calcRectPosition.Y -= 1;
                    calcRectSize.Y += 1;
                }

                aisleFog.InitFog(calcRectPosition, calcRectSize);
                CurrWorld.FogMaskRoot.AddChild(aisleFog);
                roomDoorInfo.AisleFogMask = aisleFog;
                roomDoorInfo.ConnectDoor.AisleFogMask = aisleFog;

                //过道迷雾区域
                var fogArea = new AisleFogArea();
                fogArea.Init(roomDoorInfo,
                    new Rect2I(
                        fogAreaRect.Position * GameConfig.TileCellSize,
                        fogAreaRect.Size * GameConfig.TileCellSize
                    )
                );
                roomDoorInfo.AisleFogArea = fogArea;
                roomDoorInfo.ConnectDoor.AisleFogArea = fogArea;
                CurrWorld.AffiliationAreaRoot.AddChild(fogArea);
            }

            //预览迷雾区域
            var previewRoomFog = new PreviewFogMask();
            roomDoorInfo.PreviewRoomFogMask = previewRoomFog;
            previewRoomFog.Init(roomDoorInfo, PreviewFogMask.PreviewFogType.Room);
            previewRoomFog.SetActive(false);
            CurrWorld.FogMaskRoot.AddChild(previewRoomFog);
            
            var previewAisleFog = new PreviewFogMask();
            roomDoorInfo.PreviewAisleFogMask = previewAisleFog;
            previewAisleFog.Init(roomDoorInfo, PreviewFogMask.PreviewFogType.Aisle);
            previewAisleFog.SetActive(false);
            CurrWorld.FogMaskRoot.AddChild(previewAisleFog);
        }
    }

    private void CreatePreviewSprite(RoomInfo roomInfo)
    {
        //房间区域
        var sprite = new TextureRect();
        //sprite.Centered = false;
        sprite.Texture = roomInfo.PreviewTexture;
        sprite.Position = roomInfo.Position + new Vector2I(1, 3);
        var material = ResourceManager.Load<ShaderMaterial>(ResourcePath.resource_material_Outline2_tres, false);
        material.SetShaderParameter("outline_color", new Color(1, 1, 1, 0.9f));
        material.SetShaderParameter("scale", 0.5f);
        sprite.Material = material;
        roomInfo.PreviewSprite = sprite;
        
        //过道
        if (roomInfo.Doors != null)
        {
            foreach (var doorInfo in roomInfo.Doors)
            {
                if (doorInfo.IsForward)
                {
                    var aisleSprite = new TextureRect();
                    //aisleSprite.Centered = false;
                    aisleSprite.Texture = doorInfo.AislePreviewTexture;
                    //调整过道预览位置
                    if (doorInfo.Direction == DoorDirection.N || doorInfo.Direction == DoorDirection.S ||
                        doorInfo.ConnectDoor.Direction == DoorDirection.N || doorInfo.ConnectDoor.Direction == DoorDirection.S)
                    {
                        aisleSprite.Position = doorInfo.AisleFloorRect.Position + new Vector2I(0, 1);
                    }
                    else
                    {
                        aisleSprite.Position = doorInfo.AisleFloorRect.Position;
                    }

                    // var aisleSpriteMaterial = ResourceManager.Load<ShaderMaterial>(ResourcePath.resource_material_Outline2_tres, false);
                    // aisleSpriteMaterial.SetShaderParameter("outline_color", new Color(1, 1, 1, 0.9f));
                    // aisleSpriteMaterial.SetShaderParameter("scale", 0.5f);
                    // aisleSprite.Material = aisleSpriteMaterial;
                    doorInfo.AislePreviewSprite = aisleSprite;
                    doorInfo.ConnectDoor.AislePreviewSprite = aisleSprite;
                }
            }
        }
    }
    
    /// <summary>
    /// 玩家第一次进入某个房间回调
    /// </summary>
    private void OnPlayerFirstEnterRoom(object o)
    {
        _checkEnemyTimer = 0;
        var room = (RoomInfo)o;
        room.OnFirstEnter();
        //如果关门了, 那么房间外的敌人就会丢失目标
        if (room.IsSeclusion)
        {
            var playerAffiliationArea = CurrWorld.Player.AffiliationArea;
            foreach (var role in CurrWorld.Role_InstanceList)
            {
                //不与玩家处于同一个房间
                if (role is AiRole enemy && !enemy.IsDestroyed && enemy.AffiliationArea != playerAffiliationArea)
                {
                    if (enemy.StateController.CurrState != AIStateEnum.AiNormal)
                    {
                        enemy.StateController.ChangeState(AIStateEnum.AiNormal);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 玩家进入某个房间回调
    /// </summary>
    private void OnPlayerEnterRoom(object o)
    {
        var roomInfo = (RoomInfo)o;
        if (_affiliationAreaFlag != roomInfo.AffiliationArea)
        {
            if (!roomInfo.AffiliationArea.IsDestroyed)
            {
                //刷新迷雾
                FogMaskHandler.RefreshRoomFog(roomInfo);
            }

            _affiliationAreaFlag = roomInfo.AffiliationArea;

            //按房间类型切 BGM:
            //原版游戏只会播地牢组的 BGM, 进 Boss 房不换曲子。
            //这里在 Boss 房换成 Boss 曲, 其他房间用回地牢组的。
            //PlayBgm 内部会先 StopBgm 再淡入, 所以用 _currBgmId 挡住重复调用,
            //否则同一首会被反复重播, 循环永远从开头开始。
            var bossBgm = _dungeonGenerator?.Config?.BossBgmId;
            if (string.IsNullOrEmpty(bossBgm))
            {
                bossBgm = BossRoomBgmId;
            }

            var wantBgm = roomInfo.RoomType == DungeonRoomType.Boss
                ? bossBgm
                : _dungeonGenerator?.RoomGroup?.SoundId;

            PlayDungeonBgm(wantBgm);
        }
    }

    /// <summary>
    /// 播放地牢 BGM。
    /// 会先查 Sound.json 里有没有这个 Id, 有才播 —— 避免像原版那样
    /// 传了不存在的 Id 时静默失败(SoundManager 里 TryGetValue 失败只 return null, 不报错)。
    /// </summary>
    private void PlayDungeonBgm(string soundId)
    {
        if (string.IsNullOrEmpty(soundId))
        {
            return;
        }

        //已经在放同一首, 不要重播
        if (_currBgmId == soundId)
        {
            return;
        }

        if (!ExcelConfig.Sound_Map.ContainsKey(soundId))
        {
            GD.PushWarning($"BGM Id 在 Sound.json 里不存在, 已跳过: {soundId}");
            return;
        }

        _currBgmId = soundId;
        CurrWorld?.PlayBgm(soundId);
    }
    
    /// <summary>
    /// 检测当前房间敌人是否已经消灭干净, 应当每秒执行一次
    /// </summary>
    private void OnCheckEnemy()
    {
        var activeRoom = ActiveRoomInfo;
        if (activeRoom != null && activeRoom.RoomPreinstall != null)
        {
            if (activeRoom.RoomPreinstall.IsRunWave) //正在生成标记
            {
                if (activeRoom.RoomPreinstall.IsCurrWaveOver()) //所有标记执行完成
                {
                    //房间内是否有存活的敌人
                    var flag = ActiveAffiliationArea.ExistEnterItem(
                        activityObject => activityObject is Role role && role.IsEnemyWithPlayer()
                    );
                    //Debug.Log("当前房间存活数量: " + count);
                    if (!flag)
                    {
                        var isFinalWave = activeRoom.RoomPreinstall.IsLastWave;
                        activeRoom.OnClearRoom();
                        // 提示玩家房间已经清空。
                        // 没有这个提示, 玩家不知道是否打完, 会继续浪费子弹。
                        if (isFinalWave && activeRoom.RoomPreinstall.HasEnemy())
                        {
                            GameNotificationOverlay.ShowRoomCleared(
                                CurrentFloor,
                                activeRoom.RoomType == DungeonRoomType.Outlet
                            );
                        }
                    }
                }
            }
        }
    }

    private void DisposeRoomInfo(RoomInfo roomInfo)
    {
        roomInfo.Destroy();
    }
    
    public override void _Draw()
    {
        if (ActivityObject.IsDebug)
        {
            StartRoomInfo?.EachRoom(info =>
            {
                DrawRect(new Rect2(info.Waypoints * GameConfig.TileCellSize, new Vector2(16, 16)), Colors.Red);
            });
            //绘制房间区域
            if (_dungeonGenerator != null)
            {
                DrawRoomInfo(StartRoomInfo);
            }
            //绘制边缘线

        }
    }
    
    //绘制房间区域, debug 用
    private void DrawRoomInfo(RoomInfo roomInfo)
    {
        var cellSize = GameConfig.TileCellSize;
        var pos1 = (roomInfo.Position + roomInfo.Size / 2) * cellSize;
        
        //绘制下一个房间
        foreach (var nextRoom in roomInfo.Next)
        {
            var pos2 = (nextRoom.Position + nextRoom.Size / 2) * cellSize;
            DrawLine(pos1, pos2, Colors.Red);
            DrawRoomInfo(nextRoom);
        }

        DrawString(ResourceManager.DefaultFont16Px, pos1 - new Vector2I(0, 10), "Id: " + roomInfo.Id.ToString());
        DrawString(ResourceManager.DefaultFont16Px, pos1 + new Vector2I(0, 10), "Layer: " + roomInfo.Layer.ToString());

        //绘制房间区域
        DrawRect(new Rect2(roomInfo.Position * cellSize, roomInfo.Size * cellSize), Colors.Blue, false);
        
        //绘制门
        foreach (var roomDoor in roomInfo.Doors)
        {
            var originPos = roomDoor.OriginPosition * cellSize;
            switch (roomDoor.Direction)
            {
                case DoorDirection.E:
                    DrawLine(originPos, originPos + new Vector2(3, 0) * cellSize, Colors.Yellow);
                    DrawLine(originPos + new Vector2(0, 4) * cellSize, originPos + new Vector2(3, 4) * cellSize,
                        Colors.Yellow);
                    break;
                case DoorDirection.W:
                    DrawLine(originPos, originPos - new Vector2(3, 0) * cellSize, Colors.Yellow);
                    DrawLine(originPos + new Vector2(0, 4) * cellSize, originPos - new Vector2(3, -4) * cellSize,
                        Colors.Yellow);
                    break;
                case DoorDirection.S:
                    DrawLine(originPos, originPos + new Vector2(0, 3) * cellSize, Colors.Yellow);
                    DrawLine(originPos + new Vector2(4, 0) * cellSize, originPos + new Vector2(4, 3) * cellSize,
                        Colors.Yellow);
                    break;
                case DoorDirection.N:
                    DrawLine(originPos, originPos - new Vector2(0, 3) * cellSize, Colors.Yellow);
                    DrawLine(originPos + new Vector2(4, 0) * cellSize, originPos - new Vector2(-4, 3) * cellSize,
                        Colors.Yellow);
                    break;
            }
            
            if (roomDoor.HasCross && roomDoor.RoomInfo.Id < roomDoor.ConnectRoom.Id)
            {
                DrawRect(new Rect2(roomDoor.Cross * cellSize, new Vector2(cellSize * 4, cellSize * 4)), Colors.Yellow, false);
            }
        }
    }
    
    /// <summary>
    /// 将房间类型枚举转为字符串
    /// </summary>
    public static string DungeonRoomTypeToString(DungeonRoomType roomType)
    {
        switch (roomType)
        {
            case DungeonRoomType.Battle: return "battle";
            case DungeonRoomType.Inlet: return "inlet";
            case DungeonRoomType.Outlet: return "outlet";
            case DungeonRoomType.Boss: return "boss";
            case DungeonRoomType.Reward: return "reward";
            case DungeonRoomType.Shop: return "shop";
            case DungeonRoomType.Event: return "event";
        }

        return "battle";
    }
    
        
    /// <summary>
    /// 将房间类型枚举转为描述字符串
    /// </summary>
    public static string DungeonRoomTypeToDescribeString(DungeonRoomType roomType)
    {
        switch (roomType)
        {
            case DungeonRoomType.Battle: return "战斗房间";
            case DungeonRoomType.Inlet: return "起始房间";
            case DungeonRoomType.Outlet: return "结束房间";
            case DungeonRoomType.Boss: return "Boss房间";
            case DungeonRoomType.Reward: return "宝箱房间";
            case DungeonRoomType.Shop: return "商店房间";
            case DungeonRoomType.Event: return "事件房间";
        }

        return "战斗房间";
    }

    /// <summary>
    /// 检测地牢是否可以执行生成
    /// </summary>
    /// <param name="groupName">组名称</param>
    public static DungeonCheckState CheckDungeon(string groupName)
    {
        if (GameApplication.Instance.RoomConfig.TryGetValue(groupName, out var group))
        {
            //验证该组是否满足生成地牢的条件
            if (group.InletList.Count == 0)
            {
                return new DungeonCheckState(true, "当没有可用的起始房间!");
            }
            else if (group.OutletList.Count == 0)
            {
                return new DungeonCheckState(true, "没有可用的结束房间!");
            }
            else if (group.BattleList.Count == 0)
            {
                return new DungeonCheckState(true, "没有可用的战斗房间!");
            }

            return new DungeonCheckState(false, null);
        }

        return new DungeonCheckState(true, "未找到地牢组");
    }
}
