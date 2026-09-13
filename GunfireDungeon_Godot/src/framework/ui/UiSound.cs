using Godot;

/// <summary>
/// 全局界面音效
/// <para/>
/// 挂到 <see cref="SceneTree.NodeAdded"/> 上, 场景里出现的按钮会自动接上音效,
/// 不需要逐个界面去改。目前覆盖:
/// <list type="bullet">
/// <item>普通按钮按下 -> click_001</item>
/// <item>鼠标悬停 -> select_008 (带最小间隔, 防止快速划过时连成一串)</item>
/// <item>勾选框开关 -> toggle_001</item>
/// <item>下拉框/菜单按钮弹出 -> click_001</item>
/// <item>滑块松手 -> toggle_001</item>
/// </list>
/// 音效素材来自 Kenney Interface Sounds (CC0 1.0), 见 resource/sound/ui/LICENSE.md
/// 通过 <see cref="SoundManager.PlaySoundEffect"/> 播放, 走 SFX 总线,
/// 因此设置面板里的「音效音量」会统一控制它。
/// </summary>
public static class UiSound
{
    /// <summary>按钮按下</summary>
    private const string ClickPath = "res://resource/sound/ui/click_001.ogg";

    /// <summary>鼠标悬停</summary>
    private const string HoverPath = "res://resource/sound/ui/select_008.ogg";

    /// <summary>勾选框 / 滑块</summary>
    private const string TogglePath = "res://resource/sound/ui/toggle_001.ogg";

    private const float ClickVolume = 0.65f;
    private const float HoverVolume = 0.35f;
    private const float ToggleVolume = 0.60f;

    /// <summary>两次悬停音效之间的最小间隔(秒)</summary>
    private const double HoverMinInterval = 0.06;

    /// <summary>标记已经接过音效的节点, 避免重复连接</summary>
    private static readonly StringName WiredMeta = "__ui_sound_wired";

    private static double _lastHoverMs = double.NegativeInfinity;

    /// <summary>
    /// 安装全局界面音效, 必须在创建任何界面之前调用
    /// </summary>
    public static void Install(SceneTree tree)
    {
        if (tree == null)
        {
            return;
        }

        tree.NodeAdded += OnNodeAdded;
    }

    private static void OnNodeAdded(Node node)
    {
        //场景里节点极多, 先做最便宜的过滤
        if (node is not BaseButton && node is not HSlider)
        {
            return;
        }

        if (node.HasMeta(WiredMeta))
        {
            return;
        }

        node.SetMeta(WiredMeta, true);

        switch (node)
        {
            //CheckBox 也会发 Pressed, 只接 Toggled, 否则会响两次
            case CheckBox checkBox:
                checkBox.Toggled += _ => Play(TogglePath, ToggleVolume);
                break;

            //下拉框与菜单按钮点了不触发 Pressed, 改接弹出信号
            case OptionButton optionButton:
                optionButton.GetPopup().AboutToPopup += () => Play(ClickPath, ClickVolume);
                break;
            case MenuButton menuButton:
                menuButton.GetPopup().AboutToPopup += () => Play(ClickPath, ClickVolume);
                break;

            case BaseButton baseButton:
                baseButton.Pressed += () => Play(ClickPath, ClickVolume);
                break;
        }

        if (node is BaseButton hoverTarget)
        {
            hoverTarget.MouseEntered += () =>
            {
                if (!hoverTarget.Disabled && hoverTarget.Visible)
                {
                    PlayHover();
                }
            };
        }

        if (node is HSlider slider)
        {
            //拖动时 ValueChanged 会连续触发, 只在松手时响一声
            slider.DragEnded += _ => Play(TogglePath, ToggleVolume * 0.8f);
        }
    }

    /// <summary>
    /// 播放悬停音效, 带间隔限制
    /// </summary>
    private static void PlayHover()
    {
        var now = (double)Time.GetTicksMsec();
        if (now - _lastHoverMs < HoverMinInterval * 1000.0)
        {
            return;
        }

        _lastHoverMs = now;
        Play(HoverPath, HoverVolume);
    }

    private static void Play(string path, float volume)
    {
        if (GameApplication.Instance == null)
        {
            return;
        }

        SoundManager.PlaySoundEffect(path, volume);
    }
}
