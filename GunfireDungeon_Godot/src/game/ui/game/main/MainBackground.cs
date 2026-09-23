using Godot;

namespace UI.game.Main;

/// <summary>
/// 主菜单背景 —— 进入主菜单时从 11 张背景里随机挑一张。
///
/// 选项 0 = 原来的【视差石墙大厅】(天空 / 远山 / 石墙三层, 会缓慢横向漂移);
/// 选项 1~10 = <c>resource/sprite/ui/mainBackground/variants/</c> 里的 10 张静图。
///
/// 【为什么是 11】用户要求"主页本来只有一张图片, 现在变成 11 张" ——
/// 也就是原来那张视差大厅也算一版, 加上新接入的 10 张。
///
/// 【会不会连着两次看到同一张】不会。用静态字段记住上一次抽到的选项,
/// 抽重了就顺移一位再抽一次(见 <see cref="PickOption"/>) ——
/// 11 选 1 纯随机会有约 9% 的概率连续两次相同, 放到玩家眼里就像"随机没生效"。
///
/// ⚠️ 【这 10 张图不在 git 仓库里】
/// CraftPix 免费素材条款 §2.2.1 禁止再分发美术源文件, 而本仓库是公开仓库,
/// 所以这 10 个 PNG 被 .gitignore 排除, 只在打包发行时从本机磁盘进 pck。
/// 源码构建版(别人 clone 下来跑)会走到下面 <see cref="BuildStillBackground"/> 的
/// 兜底分支, 退回视差大厅并打一条 warning —— 不会黑屏。
/// 来源与授权见同目录 <c>variants/LICENSE.md</c>, 本机补齐见 <c>variants/restore_variants.ps1</c>。
/// </summary>
public partial class MainBackground : Godot.Control
{
    /// <summary>视差大厅: 天空贴图</summary>
    private const string SkyPath = "res://resource/sprite/ui/mainBackground/bg_sky.png";

    /// <summary>视差大厅: 远山贴图</summary>
    private const string RidgePath = "res://resource/sprite/ui/mainBackground/bg_ridge.png";

    /// <summary>视差大厅: 石墙贴图(静止)</summary>
    private const string WallPath = "res://resource/sprite/ui/mainBackground/bg_wall.png";

    /// <summary>
    /// 随机静图池。顺序和 <c>variants/LICENSE.md</c> 里的编号一一对应。
    /// 这 10 张都是 2304x1296(16:9), 和 UI 画布 1920x1080 同比例, 拉伸铺满不会变形。
    /// </summary>
    private static readonly string[] VariantPaths =
    {
        "res://resource/sprite/ui/mainBackground/variants/bg_v01.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v02.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v03.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v04.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v05.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v06.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v07.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v08.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v09.png",
        "res://resource/sprite/ui/mainBackground/variants/bg_v10.png",
    };

    /// <summary>总选项数 = 1 张视差大厅 + N 张静图</summary>
    private static int OptionCount => 1 + VariantPaths.Length;

    /// <summary>
    /// 上一次抽到的选项(0 = 视差大厅)。静态 —— 同一次运行内跨主菜单实例有效。
    /// 主菜单返回一次就重建一次 MainBackground, 用实例字段记不住。
    /// </summary>
    private static int _lastOption = -1;

    /// <summary>UI 画布尺寸(和其它界面一致, 见 project.godot 的窗口/拉伸设置)</summary>
    private static readonly Vector2 CanvasSize = new(1920f, 1080f);

    /// <summary>滚动周期 = 贴图宽度</summary>
    private const float Period = 3840f;

    /// <summary>天空层漂移速度(像素/秒)</summary>
    private const float SkySpeed = 32f;

    /// <summary>
    /// 远山层漂移速度(像素/秒), 要比天空快才有视差纵深。
    /// 原为 90, 用户反馈"远山移动太快了" —— 降到 42, 与天空的 32 拉开适度差距。
    /// </summary>
    private const float RidgeSpeed = 42f;

    private TextureRect _skyA;
    private TextureRect _skyB;
    private TextureRect _ridgeA;
    private TextureRect _ridgeB;

    private float _skyOffset;
    private float _ridgeOffset;

    public override void _Ready()
    {
        //铺满父节点, 且不吃鼠标事件
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var option = PickOption();
        if (option <= 0)
        {
            BuildParallaxHall();
        }
        else
        {
            BuildStillBackground(VariantPaths[option - 1]);
        }
    }

    /// <summary>
    /// 抽本次要用的背景。返回值 0 = 视差大厅, 1..N = <see cref="VariantPaths"/> 的第几个。
    /// </summary>
    private static int PickOption()
    {
        var count = OptionCount;
        if (count <= 1)
        {
            _lastOption = 0;
            return 0;
        }

        //用项目自己的随机源(Utils.Random), 这样和其它随机逻辑共享同一个种子流。
        //理论上 InitRandom() 一定先跑过, 但拿不到时不能崩, 退回 Godot 全局随机。
        int option;
        if (Utils.Random != null)
        {
            option = Utils.Random.RandomRangeInt(0, count - 1);
        }
        else
        {
            option = (int)(GD.Randi() % (uint)count);
        }

        //抽重了就顺移: 在剩下的 count-1 个里再抽一个偏移量, 保证换一张。
        //(不用 do/while 重抽, 免得随机源退化时死循环)
        if (option == _lastOption && Utils.Random != null)
        {
            option = (option + 1 + Utils.Random.RandomRangeInt(0, count - 2)) % count;
        }
        else if (option == _lastOption)
        {
            option = (option + 1) % count;
        }

        _lastOption = option;
        return option;
    }

    /// <summary>
    /// 选项 0: 原来的视差大厅 —— 地牢石墙大厅, 左右各一扇拱窗, 窗外是黄昏天空与远山。
    /// 层级(由远及近): 天空(慢) -> 远山(快) -> 石墙(静止)。
    /// 天空与远山贴图都是 3840 宽且左右无缝, 各放两份首尾相接循环移动,
    /// 这样任何时刻都能铺满 1920 宽的画布。
    /// </summary>
    private void BuildParallaxHall()
    {
        //注意 Godot 的绘制顺序: 后 AddChild 的画在上层
        //所以按 天空 -> 远山 -> 石墙 的顺序添加

        //最远层: 天空
        _skyA = MakeLayer(SkyPath, Period);
        _skyB = MakeLayer(SkyPath, Period);
        AddChild(_skyA);
        AddChild(_skyB);

        //中层: 远山
        _ridgeA = MakeLayer(RidgePath, Period);
        _ridgeB = MakeLayer(RidgePath, Period);
        AddChild(_ridgeA);
        AddChild(_ridgeB);

        //最近层: 石墙(静止), 拱窗处透明, 透出后面的天空
        var wall = MakeLayer(WallPath, 1920f);
        AddChild(wall);

        Apply();
    }

    /// <summary>
    /// 选项 1..N: 一张铺满全屏的静图。
    /// 加载失败时退回视差大厅 —— 宁可少一张, 也不能让主菜单变成黑屏。
    /// </summary>
    private void BuildStillBackground(string path)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null)
        {
            GD.PushWarning($"[MainBackground] 背景贴图加载失败, 退回视差大厅: {path}");
            BuildParallaxHall();
            return;
        }

        AddChild(new TextureRect
        {
            Name = "StillBackground",
            Texture = tex,
            Position = Vector2.Zero,
            Size = CanvasSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,

            //这些图不是像素画(边缘本来就是抗锯齿的), 而且 2304 -> 1920 不是整数倍。
            //项目全局的 default_texture_filter 是 Nearest(给像素画用的),
            //直接套上去会在缩小时随机丢像素列, 边缘发毛。
            //所以单独给这一层开线性过滤 —— 只影响这一张图, 不动全局设置。
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        });

        //静图不需要每帧漂移
        SetProcess(false);
    }

    /// <summary>创建一个铺满整屏高度的图层贴图</summary>
    private static TextureRect MakeLayer(string path, float width)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null)
        {
            GD.PushWarning($"[MainBackground] 背景贴图加载失败: {path}");
        }

        return new TextureRect
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(path),
            Texture = tex,
            Position = Vector2.Zero,
            Size = new Vector2(width, 1080f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        _skyOffset = Mathf.PosMod(_skyOffset + d * SkySpeed, Period);
        _ridgeOffset = Mathf.PosMod(_ridgeOffset + d * RidgeSpeed, Period);
        Apply();
    }

    private void Apply()
    {
        if (_skyA == null)
        {
            return;
        }

        _skyA.Position = new Vector2(-_skyOffset, 0f);
        _skyB.Position = new Vector2(Period - _skyOffset, 0f);
        _ridgeA.Position = new Vector2(-_ridgeOffset, 0f);
        _ridgeB.Position = new Vector2(Period - _ridgeOffset, 0f);
    }
}
