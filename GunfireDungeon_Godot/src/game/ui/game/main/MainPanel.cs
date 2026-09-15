using Godot;

using DsUi;

namespace UI.game.Main;

/// <summary>
/// 主菜单
/// </summary>
public partial class MainPanel : Main
{
    /// <summary>
    /// 修改说明浮层
    /// </summary>
    private ChangelogOverlay _changelog;

    /// <summary>
    /// 主菜单背景音乐播放器。
    /// 注意: PlayMusic 返回的节点挂在 GameApplication.GlobalNodeRoot 下, 不是本 UI 的子节点,
    /// 所以主菜单隐藏/销毁时它不会自动停, 必须自己 stop。
    /// </summary>
    private SoundManager.GameAudioPlayer _bgm;

    public override void OnCreateUi()
    {
        //视差背景(石墙大厅 + 拱窗外的黄昏天空), 必须排在最底层
        var background = new MainBackground { Name = "MainBackground" };
        AddChild(background);
        MoveChild(background, 0);

        //原本那块纯色底板已经被背景取代, 隐藏掉
        //注意: 泛型要写全 Godot.ColorRect, 避免解析到 Main 里的同名嵌套类
        var colorRect = GetNodeOrNull<Godot.ColorRect>("ColorRect");
        if (colorRect != null)
        {
            colorRect.Visible = false;
        }

        S_Start.Instance.Pressed += OnStartGameClick;
        S_Tools.Instance.Pressed += OnToolsClick;
        S_Setting.Instance.Pressed += OnSettingClick;
        S_Exit.Instance.Pressed += OnExitClick;

        //「修改说明」按钮与浮层
        //注意: Main 里有同名嵌套类 LinkButton, 泛型必须写 Godot.LinkButton, 否则解析到嵌套类会取不到节点
        var changelogButton = GetNodeOrNull<Godot.LinkButton>("ChangelogButton");
        if (changelogButton != null)
        {
            _changelog = ChangelogOverlay.Create();
            AddChild(_changelog);
            changelogButton.Pressed += () => _changelog.ShowOverlay();
        }

#if !TOOLS
        S_Tools.Instance.Visible = false;
#endif
        
        // var osName = OS.GetName();
        // if (osName == "Android")
        // {
        //     S_Tools.Instance.Visible = false;
        // }
    }

    public override void OnShowUi()
    {
        Utils.HandlerFocusList(S_ButtonList.Instance);

        //主菜单 BGM(淡入 1 秒)
        PlayMenuBgm();
    }

    /// <summary>
    /// 播主菜单 BGM。重复调用是安全的: 已经在放就不再重开。
    /// </summary>
    private void PlayMenuBgm()
    {
        if (_bgm != null && _bgm.Playing)
        {
            return;
        }

        _bgm = SoundManager.PlayTransitionMusic("bgm_menu", 1);
    }

    /// <summary>
    /// 停主菜单 BGM(淡出 1.5 秒, TransitionToStop 内部时长)。
    /// </summary>
    private void StopMenuBgm()
    {
        if (_bgm != null && _bgm.Playing)
        {
            _bgm.TransitionToStop();
        }

        _bgm = null;
    }

    //点击开始游戏
    private void OnStartGameClick()
    {
        //先淡出主菜单 BGM, 再进大厅(大厅自己的 BGM 由地牢组 SoundId 决定)
        StopMenuBgm();

        UiManager.Open_Game_Loading();
        GameApplication.Instance.DungeonManager.LoadHall(() =>
        {
            UiManager.Destroy_Game_Loading();
        });
        HideUi();
    }

    //退出游戏
    private void OnExitClick()
    {
        GetTree().Quit();
    }

    //点击开发者工具
    private void OnToolsClick()
    {
        OpenNextUi(UiManager.UiName.Editor_EditorManager);
    }

    //点击设置按钮
    private void OnSettingClick()
    {
        OpenNextUi(UiManager.UiName.Game_Setting);
    }
}
