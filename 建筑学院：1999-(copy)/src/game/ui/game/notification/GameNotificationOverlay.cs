using Godot;

/// <summary>
/// 游戏过程中的醒目提示：楼层切换、房间清空、BOSS 预警与 BOSS 血条。
///
/// 移植自 4.2 项目，三处改动：
///   1. 字体换成当前项目的【方舟像素】(4.2 用的是 VonwaonBitmap)
///   2. BOSS 查找用当前项目的 <see cref="Boss"/> 基类，而不是 4.2 的 BelialEnemy
///   3. BOSS 血条改用 Kenney Sci-fi UI (CC0) 的九宫格血条素材
///
/// 存在的意义（用户反馈）：
///   房间清空后如果没有提示，玩家不知道是否打完，会继续浪费子弹。
///   所以每次房间清空都必须弹一次「房间已清理！」。
/// </summary>
public partial class GameNotificationOverlay : CanvasLayer
{
    private static GameNotificationOverlay _instance;

    private const string BarTexture =
        "res://resource/sprite/ui/spaceHud/Blue/bar_round_large.png";
    private const string FontPath = "res://resource/font/GameFont.tres";

    // 主题色：与游戏 UI 的暖金/暗紫体系一致
    private static readonly Color Gold = new Color("#ffe082");
    private static readonly Color PanelBg = new Color(0.055f, 0.045f, 0.10f, 0.94f);

    private Label _floorLabel;
    private PanelContainer _messagePanel;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Tween _messageTween;
    private PanelContainer _bossPanel;
    private Label _bossNameLabel;
    private ProgressBar _bossHealthBar;
    private Label _bossHealthLabel;
    private Font _font;

    public static void Init(Node parent)
    {
        if (_instance != null)
        {
            return;
        }

        _instance = new GameNotificationOverlay
        {
            Name = "GameNotificationOverlay",
            Layer = 500
        };
        parent.AddChild(_instance);
        _instance.BuildUi();
    }

    public static void ShowFloor(int floor, bool entered = false)
    {
        if (_instance == null)
        {
            return;
        }

        _instance._floorLabel.Text = $"第 {floor} 层";
        _instance._floorLabel.Visible = true;
        if (entered)
        {
            _instance.ShowMessage($"进入第 {floor} 层", "这里没有敌人，可以安心搜查", 2.2f);
        }
    }

    public static void HideFloor()
    {
        if (_instance != null)
        {
            _instance._floorLabel.Visible = false;
        }
    }

    public static void ShowBossWarning(int floor)
    {
        _instance?.ShowMessage(
            $"第 {floor} 层 BOSS 战",
            "击败守关者后，楼梯传送门才会开启",
            3.0f
        );
    }

    public static void ShowTrialGround(int floor)
    {
        _instance?.ShowMessage(
            "试炼之地",
            $"第 {floor} 层 · 每个战斗房均为 BOSS",
            2.6f
        );
    }

    /// <summary>
    /// 房间清空提示。玩家靠这个才知道不用再浪费子弹。
    /// </summary>
    public static void ShowRoomCleared(int floor, bool isExitRoom)
    {
        if (_instance == null)
        {
            return;
        }

        var subtitle = isExitRoom
            ? $"第 {floor} 层已完成 · 前往楼梯进入下一层"
            : "房门已经开启 · 可以继续探索";
        _instance.ShowMessage("房间已清理！", subtitle, 2.0f);
    }

    private void BuildUi()
    {
        _font = GD.Load<Font>(FontPath);

        _floorLabel = new Label
        {
            Text = "第 1 层",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _floorLabel.AddThemeFontOverride("font", _font);
        _floorLabel.AddThemeFontSizeOverride("font_size", 36);
        _floorLabel.AddThemeColorOverride("font_color", Gold);
        _floorLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _floorLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _floorLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        _floorLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _floorLabel.Position = new Vector2(-140, 14);
        _floorLabel.Size = new Vector2(280, 48);
        AddChild(_floorLabel);

        var center = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.OffsetTop = -96;
        center.OffsetBottom = -96;
        AddChild(center);

        _messagePanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(560, 168),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            PivotOffset = new Vector2(280, 84)
        };
        _messagePanel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        center.AddChild(_messagePanel);

        var labels = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        labels.AddThemeConstantOverride("separation", 12);
        _messagePanel.AddChild(labels);

        _titleLabel = new Label
        {
            Text = "房间已清理！",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _titleLabel.AddThemeFontOverride("font", _font);
        _titleLabel.AddThemeFontSizeOverride("font_size", 48);
        _titleLabel.AddThemeColorOverride("font_color", Gold);
        labels.AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = "房门已经开启",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _subtitleLabel.AddThemeFontOverride("font", _font);
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 24);
        _subtitleLabel.AddThemeColorOverride("font_color", Colors.White);
        labels.AddChild(_subtitleLabel);

        BuildBossBar();
    }

    /// <summary>
    /// 用 Kenney 血条素材做九宫格面板。
    /// StyleBoxTexture 支持九宫格拉伸，所以两端的圆角不会被拉变形。
    /// </summary>
    private StyleBoxTexture MakeBarStyle(Color tint, float margin)
    {
        var sb = new StyleBoxTexture
        {
            Texture = GD.Load<Texture2D>(BarTexture),
            ModulateColor = tint
        };
        sb.SetTextureMarginAll(margin);
        return sb;
    }

    private StyleBoxFlat MakePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = Gold,
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            ContentMarginLeft = 34,
            ContentMarginRight = 34,
            ContentMarginTop = 22,
            ContentMarginBottom = 22
        };
    }

    private void BuildBossBar()
    {
        _bossPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(820, 120),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _bossPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _bossPanel.Position = new Vector2(-410, 56);
        _bossPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.07f, 0.92f),
            BorderColor = new Color("#f2d06b"),
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 10,
            ContentMarginBottom = 12
        });
        AddChild(_bossPanel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        _bossPanel.AddChild(content);

        _bossNameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "BOSS",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _bossNameLabel.AddThemeFontOverride("font", _font);
        _bossNameLabel.AddThemeFontSizeOverride("font_size", 36);
        _bossNameLabel.AddThemeColorOverride("font_color", Gold);
        content.AddChild(_bossNameLabel);

        var barLayer = new Control
        {
            CustomMinimumSize = new Vector2(760, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        content.AddChild(barLayer);

        // Kenney 血条：外层用九宫格贴图当轨道，内层同样是九宫格当填充。
        // bar_round_large.png 是 192x48（Double 版），两端圆角各 24px，
        // 所以九宫格边距给 24，拉伸时圆角不会被拉扁。
        _bossHealthBar = new ProgressBar
        {
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _bossHealthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _bossHealthBar.AddThemeStyleboxOverride(
            "background", MakeBarStyle(new Color(0.10f, 0.08f, 0.12f), 24f));
        _bossHealthBar.AddThemeStyleboxOverride(
            "fill", MakeBarStyle(new Color(0.84f, 0.15f, 0.20f), 24f));
        barLayer.AddChild(_bossHealthBar);

        _bossHealthLabel = new Label
        {
            Text = "100 / 100",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _bossHealthLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _bossHealthLabel.AddThemeFontOverride("font", _font);
        _bossHealthLabel.AddThemeFontSizeOverride("font_size", 24);
        _bossHealthLabel.AddThemeColorOverride("font_color", Colors.White);
        _bossHealthLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _bossHealthLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _bossHealthLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        barLayer.AddChild(_bossHealthLabel);
    }

    public override void _Process(double delta)
    {
        if (_bossPanel == null)
        {
            return;
        }

        // 注意: 不能直接写 DungeonManager?.ActiveAffiliationArea。
        // 那个属性的实现是 CurrWorld.Player?.AffiliationArea, 当 CurrWorld 还是 null 时
        // (主菜单 / 大厅加载中) 会抛 NullReferenceException —— 每帧刷屏。
        // 所以必须先确认已经在地牢里、且 CurrWorld 存在。
        var manager = GameApplication.Instance?.DungeonManager;
        if (manager == null || !manager.IsInDungeon || manager.CurrWorld == null)
        {
            _bossPanel.Visible = false;
            return;
        }

        // 在玩家所在的区域里找还活着的 Boss —— 用当前项目的 Boss 基类，
        // 所以以后新加的 boss（比如大橘）不需要改这里。
        Boss boss = null;
        var area = manager.ActiveAffiliationArea;
        if (area != null)
        {
            var objects = area.FindIncludeItems(
                item => item is Boss candidate && !candidate.IsDie);
            if (objects.Length > 0)
            {
                boss = objects[0] as Boss;
            }
        }

        _bossPanel.Visible = boss != null;
        if (boss == null)
        {
            return;
        }

        _bossNameLabel.Text = boss.BossDisplayName;
        _bossHealthBar.MaxValue = Mathf.Max(1, boss.MaxHp);
        _bossHealthBar.Value = boss.Hp;
        _bossHealthLabel.Text = $"{boss.Hp} / {boss.MaxHp}";
    }

    private void ShowMessage(string title, string subtitle, float holdTime)
    {
        _messageTween?.Kill();
        _titleLabel.Text = title;
        _subtitleLabel.Text = subtitle;
        _messagePanel.Visible = true;
        _messagePanel.Modulate = new Color(1, 1, 1, 0);
        _messagePanel.Scale = new Vector2(0.82f, 0.82f);

        _messageTween = CreateTween();
        _messageTween.SetParallel(true);
        _messageTween.TweenProperty(_messagePanel, "modulate:a", 1.0f, 0.18f);
        _messageTween.TweenProperty(_messagePanel, "scale", Vector2.One, 0.24f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _messageTween.SetParallel(false);
        _messageTween.TweenInterval(holdTime);
        _messageTween.TweenProperty(_messagePanel, "modulate:a", 0.0f, 0.35f);
        _messageTween.TweenCallback(Callable.From(() => _messagePanel.Visible = false));
    }
}
