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
        "· 主菜单字号放大至三倍\n" +
        "· 设置面板字号放大至四倍\n" +
        "· 新增键位设置，可自定义 17 个操作按键\n" +
        "· 键位保存在存档中，重新启动后也会保留\n" +
        "· 支持一键恢复默认键位\n" +
        "\n" +
        "【三】字体\n" +
        "· 移除原项目内附带的商业试用版点阵字体\n" +
        "· 替换为「方舟像素字体」，采用 SIL OFL 1.1 协议\n" +
        "· 像素字体关闭抗锯齿与次像素定位，放大后保持锐利\n" +
        "· 字号统一调整为基准字号 12 的整数倍\n" +
        "\n" +
        "【四】问题修复\n" +
        "· 修复设置面板超出屏幕、内容溢出的问题\n" +
        "· 修复物品图册中部分条目缺少图标导致的报错\n" +
        "\n" +
        "【五】其他\n" +
        "· 清理无用的导入缓存与失效常量\n" +
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
