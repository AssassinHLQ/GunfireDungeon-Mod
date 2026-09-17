using Config;
using Godot;

using DsUi;

namespace UI.game.Setting;

public partial class SettingPanel : Setting
{
    public override void OnCreateUi()
    {
        //返回按钮: 详细页 -> 回四个入口; 入口页 -> 关闭设置界面
        S_Back.Instance.Pressed += OnBackButtonPressed;
        
        //声音设置BGM
        var save = GameApplication.Instance.GameSave;
        S_BGM.Instance.ValueChanged += (double v) =>
        {
            var value = (float)v;
            save.BgmVolume = value;
            //注意: 用 ApplyAllBusVolume 而不是 SetBusValue ——
            //主音量会同时缩放到两条总线上, 单独 SetBusValue 会把主音量覆盖掉
            SoundManager.ApplyAllBusVolume();
        };
        //声音设置SFX
        S_SFX.Instance.ValueChanged += (double v) =>
        {
            var value = (float)v;
            save.SfxVolume = value;
            SoundManager.ApplyAllBusVolume();
        };
        //鼠标跟随进度
        S_FollowsMouseAmount.Instance.ValueChanged += (double v) =>
        {
            save.FollowsMouseAmount = (float)v;
            if (GameCamera.Main != null)
            {
                GameCamera.Main.FollowsMouseAmount = (float)v;
            }
        };
        //声音设置设置BGM SFX的值
        S_SFX.Instance.VisibilityChanged += () =>
        {
            S_BGM.Instance.Value = save.BgmVolume;
            S_SFX.Instance.Value = save.SfxVolume;
            S_FollowsMouseAmount.Instance.Value = save.FollowsMouseAmount;
        };


        //---------------------- 视频设置 -----------------------------
        //全屏属性
        S_FullScreen.Instance.ButtonPressed = save.FullScreen;
        S_FullScreen.Instance.Pressed += OnChangeFullScreen;
        
        //垂直同步
        S_VerticalSync.Instance.ButtonPressed = save.VerticalSync;
        S_VerticalSync.Instance.Pressed += OnChangeVerticalSync;
        
        //完美像素
        S_PerfectPixel.Instance.ButtonPressed = save.PerfectPixel;
        S_PerfectPixel.Instance.Pressed += () =>
        {
            save.PerfectPixel = S_PerfectPixel.Instance.ButtonPressed;
            GameApplication.Instance.SetPerfectPixel(save.PerfectPixel);
        };
        
        //----------------------- 自动索敌 -----------------------------
        // 说明: ds_ui 的 S_XXX 包装类是扫描场景自动生成的, 这个复选框是后加的节点,
        // 所以直接用节点路径取, 不等生成器重跑。
        // 该值的读取在 Player.CalcMousePosition 里, 打开后立即生效, 不需要额外应用。
        var autoTargetBox = S_SettingMenu.Instance.GetNodeOrNull<Godot.CheckBox>("BoxContainer9/AutoTarget");
        if (autoTargetBox != null)
        {
            autoTargetBox.ButtonPressed = save.AutoTarget;
            autoTargetBox.Pressed += () =>
            {
                save.AutoTarget = autoTargetBox.ButtonPressed;
            };
        }

        //----------------------- 自动换弹 -----------------------------
        // 该值的读取在 Weapon.GlobalAutoReload 里, 打开后立即生效。
        var autoReloadBox = S_SettingMenu.Instance.GetNodeOrNull<Godot.CheckBox>("BoxContainer10/AutoReload");
        if (autoReloadBox != null)
        {
            autoReloadBox.ButtonPressed = save.AutoReload;
            autoReloadBox.Pressed += () =>
            {
                save.AutoReload = autoReloadBox.ButtonPressed;
            };
        }

        //----------------------- 手柄设置 -----------------------------

        S_LockAiming.Instance.ButtonPressed = save.JoystickAimAssist;
        S_LockAiming.Instance.Pressed += () =>
        {
            save.JoystickAimAssist = S_LockAiming.Instance.ButtonPressed;
            GameApplication.Instance.SetJoystickAimAssist(save.JoystickAimAssist);
        };
        
        S_AimStrength.Instance.Value = save.JoystickAimAssistStrength;
        S_AimStrength.Instance.ValueChanged += (double v) =>
        {
            save.JoystickAimAssistStrength = (float)v;
            GameApplication.Instance.SetJoystickAimAssistStrength(save.JoystickAimAssistStrength);
        };

        //======================= 两级分区 =========================
        // 把原本一长条的设置分成"四个入口 -> 详细设置"两级, 避免翻页翻很久。
        // 做法刻意选成【不搬动任何原有节点】: 只往 SettingMenu 里插入 MainPage / DetailPage
        // 两个新容器, 原有设置行原地不动, 只切换 Visible。
        // 原因: ds_ui 生成的 Setting.cs 里 S_XXX 访问器写死了
        //   "SettingMenu/BoxContainer/FullScreen" 这类相对路径, 一旦搬动节点这些路径就全废了。
        // 只新增 + 改 Visible 的话, 节点路径和父子关系完全不变, 自动生成的代码一行都不用动。
        BuildTabs();

        //----------------------- 操作设置 -----------------------------
        // 放在 BuildTabs 之后: 它会把键位区建到「操作设置」分区里，并位于基础操作选项下方
        CreateKeyBindingSection();

        //统一放大设置面板的字号(4倍)
        // 必须最后调用, 这样新建的分区标题和新增控件也能一起被放大
        ApplyMenuFontSize();
    }

    /// <summary>
    /// 设置面板普通文字的字号, 原来是 16
    /// 取值必须是像素字体基准字号(12)的整数倍, 否则放大后笔画会糊
    /// </summary>
    private const int MenuFontSize = 60;

    /// <summary>
    /// 把设置菜单里所有控件字号放大, 只改字号不动布局属性
    /// </summary>
    private void ApplyMenuFontSize()
    {
        ApplyFontSizeRecursive(S_ScrollContainer.Instance);
    }

    private static void ApplyFontSizeRecursive(Node node)
    {
        if (node is Control control)
        {
            //标题自己有更大的字号, 不要覆盖
            if (!control.HasThemeFontSizeOverride("font_size"))
            {
                control.AddThemeFontSizeOverride("font_size", MenuFontSize);
            }
        }

        foreach (var child in node.GetChildren())
        {
            ApplyFontSizeRecursive(child);
        }
    }

    //======================= 分区实现 =========================

    /// <summary>分区页面。顺序就是入口按钮从上到下的顺序。</summary>
    private readonly VBoxContainer[] _tabPages = new VBoxContainer[4];

    private const int TabImage = 0;
    private const int TabKey = 1;
    private const int TabVolume = 2;
    private const int TabDeveloper = 3;

    /// <summary>四个分区的中文名</summary>
    private static readonly string[] TabNames = { "图像设置", "操作设置", "音量设置", "开发者设置" };

    /// <summary>当前是否停在"四个入口"那一页</summary>
    private bool _inMenuPage = true;

    /// <summary>当前分区里所有可聚焦控件的顺序(用于手柄/键盘的焦点链)</summary>
    private readonly System.Collections.Generic.List<Control> _tabFocusables = new();

    /// <summary>主菜单页(四个入口按钮)</summary>
    private VBoxContainer _mainPage;

    /// <summary>详细设置页(各分区新建的控件)</summary>
    private VBoxContainer _detailPage;

    /// <summary>
    /// 构建"四个入口 -> 详细设置"两级结构。
    ///
    /// 布局(全部在原来的 SettingMenu 里面, 不搬动 SettingMenu 自身):
    ///   ScrollContainer
    ///     └ SettingMenu(VBoxContainer)
    ///         ├ Title          "游戏设置" 大字
    ///         ├ MainPage       四个入口按钮
    ///         ├ DetailPage     各分区的新建控件
    ///         ├ BoxContainer…  原有的设置行(原地不动, 只切 Visible)
    ///         └ Back           返回 / 关闭
    ///
    /// 【为什么不能在外面再套一层 Root】
    /// ds_ui 生成的 Setting.cs 里写死了
    ///     Instance.GetNode<Godot.VBoxContainer>("SettingMenu")
    /// 这个路径是相对 ScrollContainer 的。之前我在 ScrollContainer 下加了一层
    /// Root 并把 SettingMenu 挪进去, 于是路径必须改成 "Root/SettingMenu";
    /// 但 Root 是【运行时】才创建的, 而 OnCreateUi 开头第 15 行就访问了 S_Back,
    /// 那时 Root 还不存在 —— GetNode 返回 null, Setting.cs:626 直接
    /// NullReferenceException, 整个设置界面打不开。
    ///
    /// 所以这里改成【只新增兄弟节点, 不改变任何已有节点的父子关系】:
    /// 路径不用改, 也没有任何时序依赖。Setting.cs 保持原样。
    /// </summary>
    private void BuildTabs()
    {
        var menu = S_SettingMenu.Instance;

        //1. 主菜单页: 四个入口按钮, 插在 Title(下标 0) 后面
        _mainPage = new VBoxContainer { Name = "MainPage" };
        _mainPage.AddThemeConstantOverride("separation", 20);
        menu.AddChild(_mainPage);
        menu.MoveChild(_mainPage, 1);

        for (var i = 0; i < TabNames.Length; i++)
        {
            var index = i;
            var button = new Button
            {
                Text = TabNames[i],
                CustomMinimumSize = new Vector2(600, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            button.Pressed += () => EnterTab(index);
            _mainPage.AddChild(button);
        }

        //2. 详细页容器: 每个分区一个 VBoxContainer
        _detailPage = new VBoxContainer { Name = "DetailPage" };
        _detailPage.AddThemeConstantOverride("separation", 12);
        menu.AddChild(_detailPage);
        menu.MoveChild(_detailPage, 2);

        for (var i = 0; i < _tabPages.Length; i++)
        {
            var page = new VBoxContainer { Name = TabNames[i] };
            page.AddThemeConstantOverride("separation", 12);
            _detailPage.AddChild(page);
            _tabPages[i] = page;
        }

        //3. 按分区登记原有的设置行(它们仍然留在 SettingMenu 里, 只控制 Visible)
        Register(TabImage, "BoxContainer", "BoxContainer6", "BoxContainer2");
        Register(TabKey, "BoxContainer5", "BoxContainer9", "BoxContainer10", "BoxContainer7", "BoxContainer8");
        Register(TabVolume, "BoxContainer3", "BoxContainer4");
        //开发者分区全部是新建控件, 没有原有设置行

        //4. 新建各分区独有的设置项
        BuildImageTabExtras();
        BuildVolumeTabExtras();
        BuildDeveloperTabExtras();

        //动态详情页放到原有操作选项之后，让鼠标跟随、自动索敌等基础设置优先显示。
        menu.MoveChild(_detailPage, menu.GetChildCount() - 2);

        //5. 初始停在入口页
        ShowMenuPage();
    }

    /// <summary>进入某个分区的详细设置</summary>
    private void EnterTab(int index)
    {
        _inMenuPage = false;
        _currentTab = index;
        RefreshTabVisibility();
    }

    /// <summary>
    /// 返回按钮。语义随层级变化:
    ///   详细页 -> 退回四个入口
    ///   入口页 -> 关闭设置界面(有上一级 UI 就回上一级)
    /// </summary>
    private void OnBackButtonPressed()
    {
        if (!_inMenuPage)
        {
            ShowMenuPage();
        }
        else if (PrevUi != null)
        {
            OpenPrevUi();
        }
        else
        {
            Destroy();
        }
    }

    /// <summary>回到四个入口页</summary>
    private void ShowMenuPage()
    {
        _inMenuPage = true;
        RefreshTabVisibility();
    }

    /// <summary>当前分区名 -> 该分区下注册的 HBoxContainer 名字</summary>
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<string>> _tabRows = new();

    /// <summary>当前所在的详细分区下标</summary>
    private int _currentTab = TabImage;

    /// <summary>
    /// 把一个原有的设置行登记到某个分区
    /// </summary>
    private void Register(int tab, params string[] rowNames)
    {
        if (!_tabRows.TryGetValue(tab, out var list))
        {
            list = new System.Collections.Generic.List<string>();
            _tabRows[tab] = list;
        }

        list.AddRange(rowNames);
    }

    /// <summary>
    /// 按当前状态刷新可见性。
    ///
    /// 两级结构:
    ///   入口页(_inMenuPage = true)  -> 只显示 MainPage 的四个按钮, 隐藏所有设置行与详细内容
    ///   详细页(_inMenuPage = false) -> 只显示 _currentTab 那一分区的设置行与内容
    ///
    /// 【注意】原有设置行不能靠 page.Visible 隐藏 —— 它们并不在 page 下
    /// (为了保住 ds_ui 写死的节点路径, 它们仍然是 SettingMenu 的子节点),
    /// 所以必须按登记表逐个设 Visible。
    /// </summary>
    private void RefreshTabVisibility()
    {
        var menu = S_SettingMenu.Instance;

        //主菜单页只在入口状态显示
        if (_mainPage != null)
        {
            _mainPage.Visible = _inMenuPage;
        }

        //详细页整体: 入口状态就藏起来
        if (_detailPage != null)
        {
            _detailPage.Visible = !_inMenuPage;
        }

        //各分区内容
        for (var i = 0; i < _tabPages.Length; i++)
        {
            _tabPages[i].Visible = !_inMenuPage && i == _currentTab;
        }

        //原有设置行: 只在对应分区显示
        foreach (var kv in _tabRows)
        {
            var visible = !_inMenuPage && kv.Key == _currentTab;
            foreach (var rowName in kv.Value)
            {
                var row = menu.GetNodeOrNull<Control>(rowName);
                if (row != null)
                {
                    row.Visible = visible;
                }
            }
        }

        //返回按钮: 入口页显示"返回"(关闭设置), 详细页显示"返回上一级"
        S_Back.Instance.Visible = true;
        S_Back.Instance.Text = _inMenuPage ? "返回" : "返回上一级";

        HandlerFocusList();
    }

    /// <summary>
    /// 新建一个"标签 + 复选框"的设置行, 样式沿用场景里既有的
    /// (HBoxContainer 最小宽 1400 / size_flags 4, Label size_flags 3, CheckBox size_flags 10)
    /// </summary>
    private static HBoxContainer MakeCheckRow(string labelText, Godot.CheckBox box)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(1400, 0) };
        row.AddThemeConstantOverride("separation", 20);
        row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        row.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        var label = new Godot.Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        box.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        row.AddChild(label);
        row.AddChild(box);
        return row;
    }

    /// <summary>
    /// 新建一个"标签 + 滑块"的设置行
    /// </summary>
    private static HBoxContainer MakeSliderRow(string labelText, HSlider slider)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(1400, 0) };
        row.AddThemeConstantOverride("separation", 20);
        row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        var label = new Godot.Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.CustomMinimumSize = new Vector2(700, 24);
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        row.AddChild(label);
        row.AddChild(slider);
        return row;
    }

    /// <summary>
    /// 在分区里插一个小标题
    /// </summary>
    private static void AddSectionTitle(Node parent, string text)
    {
        var title = new Godot.Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        parent.AddChild(title);
    }

    //----------------------- 图像设置: 画质 -----------------------------

    /// <summary>画质档位下拉框</summary>
    private OptionButton _qualityOption;

    /// <summary>画质子选项, 切换档位时要把它们刷新成新值</summary>
    private Godot.CheckBox _glowBox;
    private HSlider _particleSlider;
    private Godot.Label _particleValueLabel;
    private Godot.CheckBox _damageNumberBox;
    private Godot.CheckBox _screenShakeBox;

    /// <summary>
    /// 正在用代码刷新子选项的值。
    /// 刷新时不能再把改动当成"玩家手动调过", 否则一切换档位就会立刻掉进"自定义"。
    /// </summary>
    private bool _refreshingQuality;

    /// <summary>
    /// 搭出「画质」这一段: 档位下拉框 + 四个子选项。
    /// 子选项任意改一个就自动切到"自定义", 这是玩家能预期的行为。
    /// </summary>
    private void BuildQualitySection(VBoxContainer page)
    {
        var save = GameApplication.Instance.GameSave;

        //---- 档位 ----
        _qualityOption = new OptionButton();
        _qualityOption.AddItem("低", (int)QualityPreset.Low);
        _qualityOption.AddItem("中", (int)QualityPreset.Medium);
        _qualityOption.AddItem("高", (int)QualityPreset.High);
        _qualityOption.AddItem("自定义", (int)QualityPreset.Custom);
        _qualityOption.Selected = PresetToIndex(save.QualityLevel);
        _qualityOption.ItemSelected += _ => OnQualityPresetChanged();
        page.AddChild(MakeOptionRow("画质", _qualityOption));
        _tabFocusables.Add(_qualityOption);

        AddSectionTitle(page, "低画质会关闭辉光与伤害数字，适合低端机与手机");

        //---- 辉光 ----
        _glowBox = new Godot.CheckBox { ButtonPressed = save.GlowEnabled };
        _glowBox.Pressed += () =>
        {
            save.GlowEnabled = _glowBox.ButtonPressed;
            OnQualitySubOptionChanged();
        };
        page.AddChild(MakeCheckRow("辉光效果", _glowBox));
        _tabFocusables.Add(_glowBox);

        //---- 粒子数量 ----
        _particleSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.05,
            Value = save.ParticleAmount,
        };
        _particleSlider.ValueChanged += v =>
        {
            save.ParticleAmount = (float)v;
            OnQualitySubOptionChanged();
        };
        page.AddChild(MakeSliderRow("粒子数量", _particleSlider));
        _particleValueLabel = new Godot.Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        page.AddChild(_particleValueLabel);
        _tabFocusables.Add(_particleSlider);

        //---- 伤害数字 ----
        _damageNumberBox = new Godot.CheckBox { ButtonPressed = save.DamageNumberEnabled };
        _damageNumberBox.Pressed += () =>
        {
            save.DamageNumberEnabled = _damageNumberBox.ButtonPressed;
            OnQualitySubOptionChanged();
        };
        page.AddChild(MakeCheckRow("伤害数字", _damageNumberBox));
        _tabFocusables.Add(_damageNumberBox);

        //---- 屏幕震动 ----
        _screenShakeBox = new Godot.CheckBox { ButtonPressed = save.ScreenShakeEnabled };
        _screenShakeBox.Pressed += () =>
        {
            save.ScreenShakeEnabled = _screenShakeBox.ButtonPressed;
            OnQualitySubOptionChanged();
        };
        page.AddChild(MakeCheckRow("屏幕震动", _screenShakeBox));
        _tabFocusables.Add(_screenShakeBox);

        RefreshQualityControls(save);
    }

    /// <summary>
    /// 玩家选了某个画质档位。非"自定义"档会把四个子选项覆盖成预设值。
    /// </summary>
    private void OnQualityPresetChanged()
    {
        //代码刷新 Selected 时也可能回调进来, 那种情况必须忽略
        if (_refreshingQuality)
        {
            return;
        }

        var save = GameApplication.Instance.GameSave;
        var index = _qualityOption.Selected;
        save.QualityLevel = (int)IndexToPreset(index);

        GraphicsQuality.ApplyPreset(save);
        GraphicsQuality.Apply(save);

        RefreshQualityControls(save);
        save.LateSave();
    }

    /// <summary>
    /// 玩家手动改了某个子选项 —— 自动切到"自定义", 免得下次启动被预设覆盖掉。
    /// </summary>
    private void OnQualitySubOptionChanged()
    {
        if (_refreshingQuality)
        {
            return;
        }

        var save = GameApplication.Instance.GameSave;
        save.QualityLevel = (int)QualityPreset.Custom;

        GraphicsQuality.Apply(save);
        RefreshQualityControls(save);
        save.LateSave();
    }

    /// <summary>
    /// 把四个子选项的显示值刷成存档里的当前值(不触发回调)。
    /// </summary>
    private void RefreshQualityControls(GameSave save)
    {
        _refreshingQuality = true;

        if (_qualityOption != null)
        {
            _qualityOption.Selected = PresetToIndex(save.QualityLevel);
        }

        if (_glowBox != null)
        {
            //SetPressedNoSignal: 普通赋值会触发 Toggled/Pressed, 会被当成玩家手动改
            _glowBox.SetPressedNoSignal(save.GlowEnabled);
        }

        if (_particleSlider != null)
        {
            //SetValueNoSignal: 直接赋值会触发 ValueChanged, 会被当成玩家手动改
            _particleSlider.SetValueNoSignal(save.ParticleAmount);
        }

        if (_damageNumberBox != null)
        {
            _damageNumberBox.SetPressedNoSignal(save.DamageNumberEnabled);
        }

        if (_screenShakeBox != null)
        {
            _screenShakeBox.SetPressedNoSignal(save.ScreenShakeEnabled);
        }

        if (_particleValueLabel != null)
        {
            _particleValueLabel.Text = $"当前: {Mathf.RoundToInt(save.ParticleAmount * 100)}%";
        }

        _refreshingQuality = false;
    }

    /// <summary>下拉框下标 -> 档位。顺序必须和 AddItem 那里一致。</summary>
    private static QualityPreset IndexToPreset(int index)
    {
        return index switch
        {
            0 => QualityPreset.Low,
            1 => QualityPreset.Medium,
            2 => QualityPreset.High,
            _ => QualityPreset.Custom,
        };
    }

    /// <summary>档位 -> 下拉框下标</summary>
    private static int PresetToIndex(int level)
    {
        return level switch
        {
            (int)QualityPreset.Low => 0,
            (int)QualityPreset.Medium => 1,
            (int)QualityPreset.High => 2,
            _ => 3,
        };
    }

    /// <summary>
    /// 新建一个"标签 + 下拉框"的设置行
    /// </summary>
    private static HBoxContainer MakeOptionRow(string labelText, OptionButton option)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(1400, 0) };
        row.AddThemeConstantOverride("separation", 20);
        row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        var label = new Godot.Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        option.CustomMinimumSize = new Vector2(400, 0);
        option.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        option.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        row.AddChild(label);
        row.AddChild(option);
        return row;
    }

    //----------------------- 图像设置: 帧率上限 -----------------------------
    /// <summary>帧率滑块最大值。超过 FpsUnlimitedAt 视为"不限制"。</summary>
    private const int FpsUnlimitedAt = 245;
    private Godot.Label _fpsValueLabel;

    private void BuildImageTabExtras()
    {
        var page = _tabPages[TabImage];

        //画质档位放在最前面 —— 这是普通玩家唯一需要动的一项
        BuildQualitySection(page);

        var slider = new HSlider
        {
            MinValue = 30,
            MaxValue = FpsUnlimitedAt,
            Step = 5,
            Value = GameApplication.Instance.GameSave.TargetFps <= 0
                ? FpsUnlimitedAt
                : Mathf.Clamp(GameApplication.Instance.GameSave.TargetFps, 30, 240),
        };

        var row = MakeSliderRow("帧率上限", slider);
        page.AddChild(row);

        //数值标签, 跟随滑块实时显示
        _fpsValueLabel = new Godot.Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        page.AddChild(_fpsValueLabel);
        RefreshFpsLabel(slider);

        slider.ValueChanged += (double v) =>
        {
            var save = GameApplication.Instance.GameSave;
            var fps = (int)v >= FpsUnlimitedAt ? 0 : (int)v;
            //吸附到常见刷新率, 免得停在 35/85 这种奇怪的数字上
            if (fps > 0)
            {
                foreach (var common in new[] { 60, 75, 90, 120, 144, 165, 240 })
                {
                    if (Mathf.Abs(fps - common) <= 2)
                    {
                        fps = common;
                        break;
                    }
                }
            }

            save.TargetFps = fps;
            save.ApplyTargetFps();
            RefreshFpsLabel(slider);
        };

        _tabFocusables.Add(slider);
    }

    private void RefreshFpsLabel(HSlider slider)
    {
        if (_fpsValueLabel == null)
        {
            return;
        }

        var save = GameApplication.Instance.GameSave;
        _fpsValueLabel.Text = save.TargetFps <= 0
            ? "当前: 不限制"
            : $"当前: {save.TargetFps} FPS";
    }

    //----------------------- 音量设置: 主音量 -----------------------------
    private void BuildVolumeTabExtras()
    {
        var page = _tabPages[TabVolume];
        var save = GameApplication.Instance.GameSave;

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            Value = save.MasterVolume,
        };

        var row = MakeSliderRow("主音量", slider);
        //插到最前面, 让它显示在 BGM / 音效之上
        page.AddChild(row);
        page.MoveChild(row, 0);

        slider.ValueChanged += (double v) =>
        {
            save.MasterVolume = (float)v;
            //主音量是总推子, 改它要同时重算两条总线
            SoundManager.ApplyAllBusVolume();
        };

        _tabFocusables.Add(slider);
    }

    //----------------------- 开发者设置 -----------------------------
    /// <summary>是否显示调试器悬浮图标(日志/工具/作弊/FPS)</summary>
    private Godot.CheckBox _devDebuggerBox;
    /// <summary>是否显示节点检查器</summary>
    private Godot.CheckBox _devInspectorBox;

    private void BuildDeveloperTabExtras()
    {
        var page = _tabPages[TabDeveloper];
        var save = GameApplication.Instance.GameSave;

        AddSectionTitle(page, "开发者工具");
        AddSectionTitle(page, "（内置调试外挂，普通游玩请保持关闭）");

        //---- 调试器悬浮图标 ----
        _devDebuggerBox = new Godot.CheckBox { ButtonPressed = save.Debug.ShowDebuggerIcon };
        var row1 = MakeCheckRow("显示调试器图标（日志 / 工具 / FPS）", _devDebuggerBox);
        page.AddChild(row1);
        _devDebuggerBox.Pressed += () =>
        {
            save.Debug.ShowDebuggerIcon = _devDebuggerBox.ButtonPressed;
            save.LateSave();
            GameApplication.Instance.ApplyDevToolsVisible();
        };
        _tabFocusables.Add(_devDebuggerBox);

        //---- 节点检查器 ----
        _devInspectorBox = new Godot.CheckBox { ButtonPressed = save.Debug.ShowInspector };
        var row2 = MakeCheckRow("显示节点检查器（含作弊按钮）", _devInspectorBox);
        page.AddChild(row2);
        _devInspectorBox.Pressed += () =>
        {
            save.Debug.ShowInspector = _devInspectorBox.ButtonPressed;
            save.LateSave();
            GameApplication.Instance.ApplyDevToolsVisible();
        };
        _tabFocusables.Add(_devInspectorBox);
    }

    /// <summary>
    /// 当前正在等待玩家按键的动作, 为 null 表示不在改键状态
    /// </summary>
    private KeyBindingManager.BindEntry _waitingEntry;
    //动作对应的按键按钮, 用于刷新显示
    private readonly System.Collections.Generic.Dictionary<string, Button> _keyButtons = new();
    //键位区域里可聚焦的控件, 按顺序排列, 用于拼接焦点链
    private readonly System.Collections.Generic.List<Control> _keyFocusables = new();
    /// <summary>
    /// 构建操作设置中的键位区域
    /// </summary>
    private void CreateKeyBindingSection()
    {
        //放进「操作设置」分区
        var menu = _tabPages[TabKey];

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 40);
        grid.AddThemeConstantOverride("v_separation", 12);
        menu.AddChild(grid);

        foreach (var entry in KeyBindingManager.Entries)
        {
            var nameLabel = new Godot.Label
            {
                Text = entry.DisplayName,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.AddChild(nameLabel);

            var button = new Button
            {
                Text = KeyBindingManager.GetBindText(entry.Action),
                CustomMinimumSize = new Vector2(300, 0)
            };
            var captured = entry;
            button.Pressed += () => StartRebind(captured, button);
            grid.AddChild(button);
            _keyFocusables.Add(button);

            _keyButtons[entry.Action.ToString()] = button;
        }

        var resetButton = new Button
        {
            Text = "恢复默认键位",
            CustomMinimumSize = new Vector2(360, 0)
        };
        resetButton.Pressed += OnResetKeyBindings;
        menu.AddChild(resetButton);
        _keyFocusables.Add(resetButton);
    }

    /// <summary>
    /// 进入等待按键状态
    /// </summary>
    private void StartRebind(KeyBindingManager.BindEntry entry, Button button)
    {
        _waitingEntry = entry;
        button.Text = "请按新键位...";
    }

    /// <summary>
    /// 恢复默认键位
    /// </summary>
    private void OnResetKeyBindings()
    {
        KeyBindingManager.ResetAll();
        _waitingEntry = null;
        RefreshKeyButtons();
    }

    /// <summary>
    /// 刷新所有按键按钮的显示
    /// </summary>
    private void RefreshKeyButtons()
    {
        foreach (var entry in KeyBindingManager.Entries)
        {
            var key = entry.Action.ToString();
            if (_keyButtons.TryGetValue(key, out var button) && GodotObject.IsInstanceValid(button))
            {
                button.Text = KeyBindingManager.GetBindText(entry.Action);
            }
        }
    }

    /// <summary>
    /// 等待玩家按键期间捕获输入
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_waitingEntry == null)
        {
            return;
        }

        //键盘: ESC 取消改键, 其它键作为新键位
        //注意不能用鼠标右键取消, 因为右键本身就是要被绑定的键(例如冲刺默认就是右键)
        if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            var keycode = keyEvent.PhysicalKeycode != Key.None
                ? keyEvent.PhysicalKeycode
                : keyEvent.Keycode;

            if (keycode == Key.Escape)
            {
                _waitingEntry = null;
            }
            else if (keycode != Key.None)
            {
                KeyBindingManager.SetKey(_waitingEntry.Action, keycode);
                _waitingEntry = null;
            }

            RefreshKeyButtons();
            //吃掉这次输入, 避免同一个事件又被界面处理一遍
            GetViewport()?.SetInputAsHandled();
            return;
        }

        //鼠标: 直接作为新键位(开火默认就是鼠标左键)
        if (@event is InputEventMouseButton { Pressed: true } mouseEvent
            && mouseEvent.ButtonIndex != MouseButton.None)
        {
            KeyBindingManager.SetMouseKey(_waitingEntry.Action, mouseEvent.ButtonIndex);
            _waitingEntry = null;
            RefreshKeyButtons();
            GetViewport()?.SetInputAsHandled();
        }
    }
    
    public override void OnShowUi()
    {
        InputManager.AddBlockageMarking(GetInstanceId());
        RefreshTabVisibility();
    }

    public override void OnHideUi()
    {
        InputManager.RemoveBlockageMarking(GetInstanceId());
    }

    public override void OnDestroyUi()
    {
        GameApplication.Instance.GameSave.Save();
    }

    public override void Process(float delta)
    {
        //等待改键时保持单一提示文本，不再追加 ESC 取消说明。
        if (_waitingEntry != null)
        {
            return;
        }

        //ESC: 在详细页先回入口页, 在入口页才关闭设置面板
        //(改键状态下 ESC 已经在 _Input 里被当成"取消改键"用掉了, 不会走到这里)
        if (Input.IsActionJustPressed(InputAction.UiCancel))
        {
            if (!_inMenuPage)
            {
                ShowMenuPage();
            }
            else if (PrevUi != null)
            {
                OpenPrevUi();
            }
            else
            {
                Destroy();
            }
        }
    }

    /// <summary>
    /// 拼接手柄/键盘的焦点链: 只连【当前可见】的控件。
    ///
    /// 入口页 -> 四个入口按钮
    /// 详细页 -> 该分区的设置控件 + 键位按钮 + 返回按钮
    /// </summary>
    private void HandlerFocusList()
    {
        var menu = S_SettingMenu.Instance;
        Control prev = null;

        if (_inMenuPage)
        {
            //入口页: 四个按钮 + 退出
            if (_mainPage != null)
            {
                foreach (var node in _mainPage.GetChildren())
                {
                    if (node is Control c && c.Visible)
                    {
                        LinkFocus(ref prev, c);
                    }
                }
            }
        }
        else
        {
            //详细页焦点顺序跟随视觉顺序。操作设置页先连接鼠标跟随/自动索敌等基础选项，
            //再连接下方的键位表；其他分区仍按新增控件、原有设置行的顺序连接。
            if (_currentTab == TabKey)
            {
                LinkVisibleSettingRows(menu, ref prev);
                LinkKeyBindingControls(ref prev);
            }
            else
            {
                foreach (var control in _tabFocusables)
                {
                    if (GodotObject.IsInstanceValid(control) && control.IsVisibleInTree())
                    {
                        LinkFocus(ref prev, control);
                    }
                }

                LinkKeyBindingControls(ref prev);
                LinkVisibleSettingRows(menu, ref prev);
            }

            //返回 / 关闭
            if (GodotObject.IsInstanceValid(S_Back.Instance))
            {
                LinkFocus(ref prev, S_Back.Instance);
            }
        }

        //收尾: 让焦点链首尾相接, 便于手柄循环
        if (prev != null)
        {
            Control first = prev;
            if (_inMenuPage)
            {
                if (_mainPage != null && _mainPage.GetChildCount() > 0)
                {
                    first = _mainPage.GetChild<Control>(0);
                }
            }
            else if (_currentTab == TabKey)
            {
                first = FindFirstSettingControl();
            }
            else
            {
                foreach (var control in _tabFocusables)
                {
                    if (GodotObject.IsInstanceValid(control) && control.IsVisibleInTree())
                    {
                        first = control;
                        break;
                    }
                }
            }

            if (GodotObject.IsInstanceValid(first))
            {
                prev.FocusNext = prev.GetPathTo(first);
            }
        }
    }

    private void LinkVisibleSettingRows(VBoxContainer menu, ref Control prev)
    {
        if (!_tabRows.TryGetValue(_currentTab, out var rows))
        {
            return;
        }

        foreach (var rowName in rows)
        {
            var box = menu.GetNodeOrNull<HBoxContainer>(rowName);
            if (box == null || !box.Visible)
            {
                continue;
            }

            foreach (var node in box.GetChildren())
            {
                if (node is Control temp && (temp is CheckBox || temp is HSlider))
                {
                    LinkFocus(ref prev, temp);
                    break;
                }
            }
        }
    }

    private void LinkKeyBindingControls(ref Control prev)
    {
        foreach (var control in _keyFocusables)
        {
            if (GodotObject.IsInstanceValid(control) && control.IsVisibleInTree())
            {
                LinkFocus(ref prev, control);
            }
        }
    }

    private Control FindFirstSettingControl()
    {
        var menu = S_SettingMenu.Instance;
        if (_tabRows.TryGetValue(_currentTab, out var rows))
        {
            foreach (var rowName in rows)
            {
                var box = menu.GetNodeOrNull<HBoxContainer>(rowName);
                if (box == null || !box.Visible)
                {
                    continue;
                }

                foreach (var node in box.GetChildren())
                {
                    if (node is Control control && (control is CheckBox || control is HSlider))
                    {
                        return control;
                    }
                }
            }
        }

        foreach (var control in _keyFocusables)
        {
            if (GodotObject.IsInstanceValid(control) && control.IsVisibleInTree())
            {
                return control;
            }
        }

        return S_Back.Instance;
    }

    /// <summary>
    /// 把控件接到焦点链尾部, 并把焦点给第一个控件
    /// </summary>
    private static void LinkFocus(ref Control prev, Control current)
    {
        if (prev == null)
        {
            prev = current;
            current.GrabFocus();
        }
        else
        {
            prev.FocusNext = prev.GetPathTo(current);
            prev = current;
        }
    }

    //切换全屏/非全屏
    private void OnChangeFullScreen()
    {
        var pressed = S_FullScreen.Instance.ButtonPressed;
        GameApplication.Instance.GameSave.FullScreen = pressed;
        DisplayServer.WindowSetMode(pressed ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    //切换垂直同步
    private void OnChangeVerticalSync()
    {
        var pressed = S_VerticalSync.Instance.ButtonPressed;
        GameApplication.Instance.GameSave.VerticalSync = pressed;
        DisplayServer.WindowSetVsyncMode(pressed ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
    }

}
