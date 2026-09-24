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
        "【一】标题与流程\n" +
        "· 标题改为《建筑学院：1999》\n" +
        "· 背景设定：1999 年恐怖分子占据了建筑学院，学生被迫停课\n" +
        "· 玩家从一楼一路向上打到七楼，再折返六楼、五楼\n" +
        "· 最终决战在五楼 505 的恐怖分子基地\n" +
        "· 正常流程共 9 个楼层\n" +
        "· 另有隐藏楼层「负一楼」，触发后总层数变为 10\n" +
        "· 楼层顺序由配置文件决定，同一楼层可以出现多次\n" +
        "· 到出口进入下一个楼层，不再直接通关\n" +
        "· 跨层保留玩家血量、武器、道具，进入下一层时恢复全部护盾\n" +
        "· 每层敌人强度按楼层配置递增\n" +
        "· 进入新楼层时弹出提示，顶部显示当前楼层\n" +
        "\n" +
        "【二】界面与显示\n" +
        "· 游戏改为全屏显示，等比缩放\n" +
        "· 主菜单重新排布：标题、分隔线、按钮\n" +
        "· 主菜单与设置面板的字号重新调整\n" +
        "· 主菜单新增动态背景：地牢石墙大厅，石墙上有三扇等大的拱形开口，透出黄昏天空与远山\n" +
        "· 背景分三层视差：天空最慢、远山其次、石墙静止\n" +
        "· 文字加入深色描边，在明亮背景上保持清晰\n" +
        "· ESC 菜单按钮改用新的边框素材，去掉底板\n" +
        "· 设置里的勾选框放大一倍，滑块改为细线，更容易看清\n" +
        "· 载入界面的文字放大，并改用像素字体\n" +
        "· 背景的砖墙改为一块块砌出的不规则砖，拱窗由楔形券石砌成\n" +
        "· 图册的物品名称与说明文字放大，长说明自动换行\n" +
        "· 图册的物品预览图改为居中自适应，不再超出边框\n" +
        "· 新增界面音效：按钮点击、鼠标悬停、勾选框开关、滑块松手\n" +
        "· 界面音效音量由设置里的「音效音量」统一控制\n" +
        "· 新增键位设置，可自定义 17 个操作按键\n" +
        "· 新增「自动索敌」：自动锁定最近的敌人并瞄准它，不用手动拖鼠标瞄准，适合触屏与手柄\n" +
        "· 新增「自动换弹」：弹夹打空后自动装填，不用再按一次换弹键\n" +
        "· 打开地图改为按一下展开、再按一下收起（原来是按住才显示）\n" +
        "· 键位设置支持鼠标按键（左键 / 右键 / 中键 / 侧键）\n" +
        "· 键位保存在存档中，重新启动后也会保留\n" +
        "· 改键时按 ESC 取消，鼠标右键可以正常作为待绑定的键\n" +
        "· 一键恢复默认键位，马上生效，不需要重启\n" +
        "\n" +
        "【三】字体与界面素材\n" +
        "· 替换为「方舟像素字体」，采用 SIL OFL 1.1 协议\n" +
        "· 缺字由「缝合像素字体」回退补齐（同样是 SIL OFL 1.1）\n" +
        "· 像素字体关闭抗锯齿与次像素定位，放大后保持锐利\n" +
        "· 字号统一调整为基准字号 12 的整数倍\n" +
        "· 主菜单 / ESC 菜单 / 设置菜单改用新的界面素材\n" +
        "· 三个菜单各用一套不同造型的界面素材，便于区分\n" +
        "· 设置面板的选项框改为正方形，滑块改为简单线条\n" +
        "· 界面素材来自 Kenney（www.kenney.nl），CC0 1.0 协议\n" +
        "· 界面音效来自 Kenney 的 Interface Sounds，CC0 1.0 协议\n" +
        "· 主菜单背景改为随机：原来固定一张视差石墙大厅，现在每次回到主菜单都会\n" +
        "  从 10 套背景里随机挑一套（1 套视差大厅 + 9 套新背景），相邻两次不会重复\n" +
        "· 10 套背景全部是动态循环的：每套都由 3~5 层组成，各层以不同速度横向滚动，\n" +
        "  形成视差纵深（新背景的层取自素材包自带的分层图，不是压平图）\n" +
        "· 修掉云朵滚动时的抖动：新背景的图层是 576 宽放到 1920（3.33 倍，不是整数倍），\n" +
        "  用像素级放大时源像素宽度会在 3/4 之间来回变、而且这个花纹会随着滚动一格格爬，\n" +
        "  看起来就是「云在原地抖」而不是平移过去；改用平滑过滤后是连续的匀速平移\n" +
        "· 主菜单背景的天空与云来自 ansimuz（ansimuz.com），CC0 1.0 协议\n" +
        "· 主菜单背景的远山与石墙为本修改版程序化生成\n" +
        "· 新增的主菜单背景来自 CraftPix 免费素材（New free backgrounds part1~part4）\n" +
        "· AK47 的武器贴图来自 CraftPix 免费素材（Free Guns Icon 32x32 Pixel Pack）\n" +
        "· 不死刽子手的精灵图来自 Boss: Undead Executioner —— Kronovi-（darkpixel-kronovi）\n" +
        "· 游戏音效全部替换为 CC0 素材（界面音与反馈音来自 Kenney，枪声来自 Free Firearm Sound Library）\n" +
        "· 枪声来自 The Free Firearm Sound Library（CC0 1.0）—— 2013 年开源枪械音效众筹项目，\n" +
        "  录音与整理：Ben Jaszczak、Brian Nelson、Kevin Heras、Matthew Nanney 等\n" +
        "  （原素材是 6~30 秒的整段靶场录音，本项目做了起音检测 / 单发切片 / 质量筛选 / 归一化）\n" +
        "· 统一素材授权，便于商业发布\n" +
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
        "· 换弹时间缩短一半，战斗节奏更顺\n" +
        "· 血迹不再永久残留：喷溅后会慢慢变黑、越来越像地板，最后淡出消失\n" +
        "· 清理无用的导入缓存与失效常量\n" +
        "· 删除从未被播放的背景音乐文件\n" +
        "· 本作品只发布源代码，不发布任何打包版本\n" +
        "· 非官方来源的任何程序，均非本作者发布，请勿运行\n" +
        "\n" +
        "【六】音乐\n" +
        "· 全部换成公有领域的古典曲目，AI 生成的音乐已全部移除\n" +
        "· 除《魔王》为本项目自行渲染外，其余曲目来自 Musopen，录音授权为 CC PD\n" +
        "  （Creative Commons Public Domain Mark，不要求署名，这里仍然逐首列出演奏者）\n" +
        "· 主菜单：肖邦 夜曲 Op.55 No.1 —— Luke Faulkner 演奏\n" +
        "· 大厅：肖邦 夜曲 Op. posth. 72 No.1，1.1 倍速 —— Anonymous 演奏\n" +
        "· 普通房间 第 1 层：贝多芬 悲怆奏鸣曲 第三乐章 —— Paul Pitman 演奏\n" +
        "· 普通房间 第 2 层：贝多芬 月光奏鸣曲 第三乐章 Presto —— Paul Pitman 演奏\n" +
        "· Boss 战（大橘）：肖邦 第一谐谑曲 Op.20（37 秒循环段）—— Alice G. Young 演奏\n" +
        "· Boss 战（犀牛）：舒伯特 魔王 D.328 钢琴版 —— 本项目按公有领域乐谱自行渲染\n" +
        "· Boss 战（甲方）：肖邦 冬风练习曲 Op.25 No.11 —— European Archive 演奏\n" +
        "· 三首 Boss 曲各自分开：大橘、犀牛、甲方三个 Boss 房各用一首，不再共用\n" +
        "· 所有背景音乐都会循环播放，不会播完就停\n" +
        "· 魔王钢琴版所用音源的署名：YDP Grand Piano —— FreePats / Zenph Studios（CC-BY 3.0）\n" +
        "\n" +
        "【七】战斗与数值调整\n" +
        "· 新增 Boss「犀牛 Rhino」：1200 点血，7 种技能（冲撞、掘地突袭、小口嚼、大口嚼、快速冲、横扫之角、撼地掌）\n" +
        "· 削弱 Boss「大橘」：爪击与各技能伤害下调，并去掉随楼层递增的加成\n" +
        "· 法力系统改为弹夹系统：每把枪有独立的备用弹药，打空后要用弹药箱补给\n" +
        "· 修复弹药箱：原来使用后没有任何效果\n" +
        "· 修复分裂子弹等道具不生效的问题\n" +
        "· 玩家不再被暴击：原来是伤害乘 1.5，运气不好两枪就死\n" +
        "· 玩家保留 10% 全属性减伤（护盾仍然优先吃伤害）\n" +
        "· 护盾：受伤后 5 秒开始恢复，每秒恢复 1 点；护盾被打破后有 1 秒无敌\n" +
        "· 近战攻击动作加快一倍\n" +
        "· 受伤闪烁由透明改为红色，更容易看清被打中\n" +
        "· 修复近战完全没有伤害：刀（以及按近战键的挥击）以前判定窗口只有 0.0125 秒，\n" +
        "  比一个物理帧还短，挥了刀也打不到人；现在判定覆盖整段挥刀动作\n" +
        "· 翻滚现在真的免伤：BOSS 技能是代码直接结算的、不走碰撞检测，\n" +
        "  以前站在红圈里翻滚时机再准也照样掉血；现在整个翻滚过程都免疫伤害\n" +
        "· 小怪一律手持武器（原来是 80% 概率），空手小怪也能撞出 1 点伤害\n" +
        "· 小怪移速提高：敌人 20→40，敌人2 15→30\n" +
        "· 新增毒液伤害：会留绿色液体的那只怪，留下的液体踩上去每 0.8 秒扣 1 点血\n" +
        "  （只在毒液层生效，水层不伤人；翻滚穿过毒液不会掉血）\n" +
        "· 修复出生房白送一堆补给的问题：有个出生房原本摆着 6 个道具 + 2 把枪，现在已经清掉\n" +
        "· 甲方登场静止期间整体发冷色，一眼能看出减伤还没结束\n" +
        "· 去掉 BOSS 移动时精灵左右摇摆 ±10 度的效果（俯视角看起来像角色在晃）\n" +
        "· 新增最终 BOSS「甲方」：2000 点血、10 个技能，配肖邦《冬风》练习曲\n" +
        "  （原名「死神 Reaper」，2026-09-19 改名；血量、技能、配乐一切未变）\n" +
        "· 数值调整：大橘 1200 → 800 点血、甲方 1200 → 2000 点血（犀牛保持 1200）\n" +
        "· 甲方免疫击退：霰弹/爆炸/近战推不动它了（以前能把最终 BOSS 一路顶到墙角）\n" +
        "· 大橘定位为「低血量、略高攻击」：血量 1200 → 800，四个技能 2/2/2/3、贴身抓挠 1\n" +
        "  （另外两个 BOSS 的技能伤害只有 1，所以大橘已是全场最高；\n" +
        "   曾经翻倍到 4/4/4/6，但玩家 Hp 只有 6，挨 2~3 下就没，已回调）\n" +
        "· 修复 BOSS 不掉金币：BOSS 继承的是 AiRole，而金币初始化写在 Enemy 里，一直是 0；\n" +
        "  现在 BOSS 会掉金币，并额外掉一个随机道具（跟宝箱房同一个掉落池）\n" +
        "· 修复分裂子弹等「子弹伤害」道具把近战伤害一起减掉的问题：现在只影响远程武器\n" +
        "· BOSS 血量改为完全由配置表决定（以前写死在代码里，改配置表不生效）\n" +
        "· 普通模式第 2/4/6 层出口前会出现 BOSS 房；魔王模式每层都是 BOSS 房\n" +
        "· 新增精英怪「邪恶法师」：不拿武器，站定施法朝你放三颗扇形火球（单发 4 点伤害）\n" +
        "  血量 200、移速 34，介于小怪和 BOSS 之间；会随楼层一起变强，随机刷在战斗房里\n" +
        "  精灵图来自 Evil Wizard Asset Pack —— Luiz Melo（LuizMelo）, CC0 1.0 协议\n" +
        "· 新增武器「AK47」：单发威力大、后坐力也大、换弹偏慢\n" +
        "  伤害 18 · 射速 7/秒 · 弹夹 30(备用 180) · 散射 6°→52° · 换弹 1.4 秒\n" +
        "  和步枪（伤害 15 · 射速 8/秒 · 散射 5°→45°）是两条不同的路线：\n" +
        "  步枪更稳更密，AK47 单发更狠但要压得住枪\n" +
        "  贴图来自 CraftPix 免费素材（Free Guns Icon 32x32 Pixel Pack）\n" +
        "· 新增精英怪「不死刽子手」：不拿武器，贴上来用大镰刀近身横扫\n" +
        "  血量 320、移速 26，比法师更厚更慢；镰刀 7~10 点伤害、扇形 150°\n" +
        "  两套攻击交替：二连挥砍（13 帧）和大范围横扫（220°、半径更远）\n" +
        "  每 7 秒召唤一只「刽子手的幽灵」追着你撞（场上最多 4 只，撞到 2 点）\n" +
        "  贴身额外 3 点碰撞伤害；和法师一样随楼层变强、随机刷在战斗房里\n" +
        "  精灵图来自 Boss: Undead Executioner —— Kronovi-（darkpixel-kronovi）, 可商用\n" +
        "· 精英怪不再和小怪一个密度了：原来随机刷怪是均匀抽取，两只精英怪加起来要占掉\n" +
        "  一半的刷怪量，战斗房会变成「精英房」；现在按权重抽（小怪 10、精英 1），\n" +
        "  精英约占总刷怪量的 15%\n" +
        "· 修复「邪恶法师」画偏的问题：它的贴图偏移是用反的（写成 -锚点 而不是 格子中心-锚点），\n" +
        "  整只法师被画到实际位置左上 75 像素处 —— 贴图和它的受击框、影子完全分家，\n" +
        "  看着像「打不到人」或「影子在别处」。现在改成和两个 BOSS 同一套写法\n" +
        "· 修复魔王模式每层都是同一个 BOSS 的问题（第 2 层曾经清一色大橘）：\n" +
        "  楼层计划指定的 BOSS 模板以前对魔王模式的每个 BOSS 房都生效，现在只留给\n" +
        "  该层出口前那一个 BOSS 房（收官 BOSS），其余 BOSS 房按权重随机\n" +
        "· BOSS 出现比例重新配平：大橘两个房间各 20%、犀牛 60%；\n" +
        "  最终 BOSS「甲方」不再进随机池（它的登场静止期和《冬风》引子是对着开场配的）\n" +
        "· 空手也能近战了：把武器全扔了之后仍然可以挥拳（伤害 2~3，带击退），\n" +
        "  以前没有武器就完全无法攻击\n" +
        "· 翻滚速度与翻滚动画速度改为和移动速度成正比：\n" +
        "  以前翻滚固定 170，穿两个鞋子跑起来是 180，翻滚反而不如跑得快；\n" +
        "  现在两者都按「当前移速 ÷ 基础移速 120」放大（限幅 0.5~4 倍）\n" +
        "· 翻滚过程中也可以攻击和开枪了（以前翻滚时会禁用近战与开火）\n" +
        "\n" +
        "【八】图册说明\n" +
        "· 武器补齐说明：伤害、射速、弹夹与备用弹药、散射、换弹时间、特殊效果\n" +
        "· 武器零件（子弹/激光/斩击等）补齐伤害、弹速、射程、击退等数值\n" +
        "· 道具说明补上具体数值（例如「移速 +30」「子弹伤害 +20%」「15% 抵消伤害」）\n" +
        "· 敌人页签加入三个 BOSS，并说明血量、移速、减伤与技能特点\n" +
        "· 修复图册说明里的颜色标记被原样显示的问题（现在会正常上色）\n" +
        "\n" +
        "━━━━━━━━━━━━━━━━━━━━\n" +
        "本说明仅为改动记录。\n" +
        "原项目版权归原作者所有。\n" +
        "\n" +
        "本修改版由 klhuyjnvbnvbnb 修改制作\n" +
        "修改者主页 github.com/AssassinHLQ/GunfireDungeon-Mod";

    /// <summary>
    /// 本修改版(修改者)的主页, 放在正文末尾作为可点击链接。
    ///
    /// 【为什么是 GitHub 而不是哔哩哔哩】原来这里指向修改者的 B 站空间,
    /// 但玩家点进去想看的其实是修订记录 / 下载 / 反馈, 仓库更合适 —— 已按用户要求改成仓库地址。
    /// 正文末尾那行文字用的是同一个地址, 两处保持一致。
    /// (原作者的 B 站链接是主菜单上另一个按钮, 见 prefab/ui/game/Main.tscn, 没动。)
    /// </summary>
    private const string ModHomepage = "https://github.com/AssassinHLQ/GunfireDungeon-Mod";

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

        //修改者主页链接放在正文末尾, 和原作者的署名主次分明
        var authorLink = new LinkButton
        {
            Text = "修改者主页（点击访问）",
            Uri = ModHomepage,
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
