using Godot;

namespace UI.game.Main;

/// <summary>
/// 主菜单的「修改说明」浮层
/// 显示本修改版相对原项目的全部改动, 满足 AGPL v3 对修改版标注改动的要求
/// </summary>
public partial class ChangelogOverlay : Control
{
    /// <summary>
    /// 修改说明正文
    /// 注意: 这份说明是 AGPL v3 要求的「修改版改动声明」, 必须随二进制发布, 不放入可被替换的外部文件
    /// </summary>
    private const string ChangelogText =
        "本游戏基于开源项目《枪火地牢 GunfireDungeon》修改而成。\n" +
        "原项目作者：哔哩哔哩 小李xlxl\n" +
        "原项目源码遵循 AGPL v3 协议，本修改版同样以 AGPL v3 发布。\n" +
        "\n" +
        "━━━━ 本次修改内容 ━━━━\n" +
        "\n" +
        "【一】流程与玩法\n" +
        "· 新增楼层循环系统，地牢共 10 层\n" +
        "· 到达出口进入下一层，不再直接通关\n" +
        "· 通关条件改为打通第 10 层\n" +
        "· 跨层保留玩家血量、护盾、武器、道具\n" +
        "· 每层敌人血量按层数递增，每层提升 16%\n" +
        "· 进入新层时弹出提示\n" +
        "· 顶部新增当前层数显示\n" +
        "\n" +
        "【二】界面与显示\n" +
        "· 游戏改为全屏显示，等比缩放\n" +
        "· 主菜单重新排布：标题、分隔线、按钮\n" +
        "· 主菜单与设置面板的字号重新调整\n" +
        "· 主菜单新增动态背景：地牢石墙大厅，石墙上有三扇等大的拱形开口，透出黄昏天空与远山\n" +
        "· 背景分三层视差：天空最慢、远山其次、石墙静止\n" +
        "· 文字加入深色描边，在明亮背景上保持清晰\n" +
        "· ESC 菜单新增石质底板，与主菜单、设置菜单风格统一\n" +
        "· 图册的物品名称与说明文字放大，长说明自动换行\n" +
        "· 图册的物品预览图改为居中自适应，不再超出边框\n" +
        "· 新增界面音效：按钮点击、鼠标悬停、勾选框开关、滑块松手\n" +
        "· 界面音效音量由设置里的「音效音量」统一控制\n" +
        "· 新增键位设置，可自定义 17 个操作按键\n" +
        "· 键位设置支持鼠标按键（左键 / 右键 / 中键 / 侧键）\n" +
        "· 键位保存在存档中，重新启动后也会保留\n" +
        "· 改键时按 ESC 取消，鼠标右键可以正常作为待绑定的键\n" +
        "· 一键恢复默认键位，马上生效，不需要重启\n" +
        "\n" +
        "【三】字体与界面素材\n" +
        "· 移除原项目内附带的商业试用版点阵字体\n" +
        "· 替换为「方舟像素字体」，采用 SIL OFL 1.1 协议\n" +
        "· 像素字体关闭抗锯齿与次像素定位，放大后保持锐利\n" +
        "· 字号统一调整为基准字号 12 的整数倍\n" +
        "· 主菜单 / ESC 菜单 / 设置菜单改用新的界面素材\n" +
        "· 三个菜单各用一套不同造型的界面素材，便于区分\n" +
        "· 设置面板的选项框改为正方形，滑块改为简单线条\n" +
        "· 界面素材来自 Kenney（www.kenney.nl），CC0 1.0 协议\n" +
        "· 界面音效来自 Kenney 的 Interface Sounds，CC0 1.0 协议\n" +
        "· 主菜单背景的天空与云来自 ansimuz（ansimuz.com），CC0 1.0 协议\n" +
        "· 主菜单背景的远山与石墙为本修改版程序化生成\n" +
        "· 全部游戏音效替换为 Kenney 的 CC0 音效\n" +
        "· 替换原因：上游音频来源不明，部分文件带有商业音效库的痕迹\n" +
        "· 说明：Kenney 没有写实枪声，枪声改用能量武器音\n" +
        "\n" +
        "【四】问题修复\n" +
        "· 修复设置面板超出屏幕、内容溢出的问题\n" +
        "· 修复物品图册中部分条目缺少图标导致的报错\n" +
        "· 修复个别汉字在像素字体中缺字、显示为方框的问题\n" +
        "· 统一房间类型名称格式为「XX房间」\n" +
        "· 修复开火、冲刺等鼠标按键显示为「未设置」的问题\n" +
        "· 修复改键时点击鼠标没有反应的问题\n" +
        "· 修复改键时按 ESC 取消，ESC 反而被绑定成新键位的问题\n" +
        "· 若之前误绑过，按一次「恢复默认键位」就能复原\n" +
        "\n" +
        "【五】其他\n" +
        "· 清理无用的导入缓存与失效常量\n" +
        "· 删除从未被播放的背景音乐文件\n" +
        "· 本作品只发布源代码，不发布任何打包版本\n" +
        "· 非官方来源的任何程序，均非本作者发布，请勿运行\n" +
        "\n" +
        "━━━━━━━━━━━━━━━━━━━━\n" +
        "本说明仅为改动记录。\n" +
        "原项目版权归原作者所有。\n" +
        "\n" +
        "本修改版由 klhuyjnvbnvbnb 修改制作\n" +
        "修改者主页 space.bilibili.com/1463614316";

    /// <summary>
    /// 本修改版作者主页, 放在正文末尾作为可点击链接
    /// </summary>
    private const string AuthorHomepage = "https://space.bilibili.com/1463614316";

    public static ChangelogOverlay Create()
    {
        var overlay = new ChangelogOverlay
        {
            Name = "ChangelogOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.BuildUi();
        return overlay;
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        //不透明遮罩, 避免主菜单文字透出干扰阅读
        var dim = new ColorRect
        {
            Color = new Color(0.055f, 0.06f, 0.08f, 1f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        //内容容器: 留出边距
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 120);
        margin.AddThemeConstantOverride("margin_right", 120);
        margin.AddThemeConstantOverride("margin_top", 50);
        margin.AddThemeConstantOverride("margin_bottom", 50);
        AddChild(margin);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 20);
        margin.AddChild(box);

        var title = new Label
        {
            Text = "修改说明",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 72);
        box.AddChild(title);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(scroll);

        var content = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(content);

        var text = new Label
        {
            Text = ChangelogText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        text.AddThemeFontSizeOverride("font_size", 30);
        text.AddThemeConstantOverride("line_spacing", 6);
        content.AddChild(text);

        //作者主页链接放在正文末尾, 保持与原作者的署名主次分明
        var authorLink = new LinkButton
        {
            Text = "作者主页（点击访问）",
            Uri = AuthorHomepage,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        authorLink.AddThemeFontSizeOverride("font_size", 30);
        content.AddChild(authorLink);

        //关闭按钮
        var close = new Button
        {
            Text = "关 闭",
            CustomMinimumSize = new Vector2(280, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        close.AddThemeFontSizeOverride("font_size", 36);
        close.Pressed += HideOverlay;
        box.AddChild(close);
    }

    /// <summary>
    /// 显示浮层
    /// </summary>
    public void ShowOverlay()
    {
        Visible = true;
        MoveToFront();
    }

    /// <summary>
    /// 隐藏浮层
    /// </summary>
    public void HideOverlay()
    {
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        //按 Esc 或手柄取消键关闭
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            HideOverlay();
            GetViewport().SetInputAsHandled();
        }
    }
}
