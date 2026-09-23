using System.Collections.Generic;
using Godot;

namespace UI.game.Main;

/// <summary>
/// 主菜单背景 —— 进入主菜单时从 11 套背景里随机挑一套, 每套都是【横向循环滚动的视差层】。
///
/// 选项 0 = 原来的视差石墙大厅(天空 / 远山 / 石墙);
/// 选项 1~10 = <c>resource/sprite/ui/mainBackground/variants/</c> 里的 10 套 CraftPix 天空。
///
/// 【为什么是 11】需求是"主页本来只有一张图片, 现在变成 11 张" ——
/// 原来那张视差大厅也算一套, 加上新接入的 10 套。
///
/// ────────────────────────────────────────────────────────────────
/// 【新接入的 10 套为什么是多层而不是一张压平图】
/// CraftPix 那个素材包里每个"background N"目录下有这么几个文件:
///   <c>1.png … N.png</c>  576x324   ← 【视差分层】, 1 = 最远(星空/底色), 编号越大越靠前
///   <c>orig.png</c>        576x324   ← 把上面几层压平的结果
///   <c>orig_big.png</c>   2304x1296  ← 上面那张的 4 倍放大版
/// 一开始按"一张静图"接了 <c>orig_big.png</c>, 结果 11 套里 10 套不会动,
/// 主菜单从"动态循环"变成了"大部分时候是张静止画" —— 这是不对的。
/// 所以改成直接用 <c>1.png…N.png</c> 这几层, 每层单独横向循环滚动、越靠前滚得越快,
/// 这样 11 套背景**全部**是动态循环的, 也才是这个素材包("Parallax Clouds")的本来用法。
///
/// 【层是不是真的能首尾相接循环】验证过: 按 1→N 的顺序做 alpha 叠加, 结果和
/// <c>orig.png</c> 逐像素完全一致(平均差 0.00); 再把每层左右首尾接起来看接缝,
/// 接缝处的列差和普通相邻列的列差同量级, 也就是本来就是为了横向平铺设计的。
///
/// ⚠️ 【这些图不在 git 仓库里】
/// CraftPix 免费素材条款 §2.2.1 禁止再分发美术源文件, 而本仓库是公开仓库,
/// 所以这些 PNG 被 .gitignore 排除, 只在打包发行时从本机磁盘进 pck。
/// 源码构建版(别人 clone 下来跑)会走到 <see cref="BuildVariant"/> 的兜底分支,
/// 退回视差大厅并打一条 warning —— 不会黑屏。
/// 来源与授权见同目录 <c>variants/LICENSE.md</c>, 本机补齐见 <c>variants/restore_variants.ps1</c>。
///
/// 【会不会连着两次看到同一套】不会。用静态字段记住上一次抽到的选项,
/// 抽重了就顺移一位再抽(见 <see cref="PickOption"/>) ——
/// 11 选 1 纯随机会有约 9% 的概率连续两次相同, 放到玩家眼里就像"随机没生效"。
///
/// 【新接入的 10 套为什么用 Linear 过滤】它们是 576 宽的图, 要放大 3.3333 倍
/// (不是整数倍)才铺满 1920 画布。Nearest 会让源像素宽度在 3/4 之间来回变,
/// 而这个花纹会随滚动爬动 —— 看起来就是"云朵在原地抖, 不是平move"。
/// 详见 <see cref="MakeLayerRect"/> 里的说明。
/// </summary>
public partial class MainBackground : Godot.Control
{
    // ───────────────────────── 选项 0: 视差石墙大厅 ─────────────────────────

    /// <summary>大厅: 天空贴图</summary>
    private const string SkyPath = "res://resource/sprite/ui/mainBackground/bg_sky.png";

    /// <summary>大厅: 远山贴图</summary>
    private const string RidgePath = "res://resource/sprite/ui/mainBackground/bg_ridge.png";

    /// <summary>大厅: 石墙贴图(静止)</summary>
    private const string WallPath = "res://resource/sprite/ui/mainBackground/bg_wall.png";

    /// <summary>大厅天空/远山贴图宽度 = 滚动周期</summary>
    private const float HallPeriod = 3840f;

    /// <summary>大厅天空层漂移速度(像素/秒)</summary>
    private const float SkySpeed = 32f;

    /// <summary>
    /// 大厅远山层漂移速度(像素/秒), 要比天空快才有视差纵深。
    /// 原为 90, 用户反馈"远山移动太快了" —— 降到 42, 与天空的 32 拉开适度差距。
    /// </summary>
    private const float RidgeSpeed = 42f;

    // ───────────────────────── 选项 1~10: CraftPix 视差天空 ─────────────────────────

    /// <summary>随机池里有几套 CraftPix 背景</summary>
    private const int VariantCount = 10;

    /// <summary>
    /// 一套背景最多取几层。实际有几层是【加载到空为止】探出来的 ——
    /// 换素材包、加减图层都不用改代码。
    /// </summary>
    private const int MaxLayersPerVariant = 8;

    /// <summary>
    /// 最远层的漂移速度(像素/秒)。
    /// 新背景的贴图是 576 宽、拉伸到 1920, 所以滚动周期是 1920(不是大厅的 3840),
    /// 这里比大厅的 32 慢一些, 让最快的前景层大约 64 秒走完一轮。
    /// </summary>
    private const float VariantBackSpeed = 6f;

    /// <summary>最近层的漂移速度(像素/秒), 层与层之间在这个区间里线性分布</summary>
    private const float VariantFrontSpeed = 30f;

    /// <summary>图层路径: bg_v01_L1.png … bg_v10_L5.png</summary>
    private static string VariantLayerPath(int variant, int layer)
    {
        return $"res://resource/sprite/ui/mainBackground/variants/bg_v{variant + 1:D2}_L{layer}.png";
    }

    // ───────────────────────── 公共 ─────────────────────────

    /// <summary>总选项数 = 1 套视差大厅 + N 套 CraftPix</summary>
    private static int OptionCount => 1 + VariantCount;

    /// <summary>
    /// 上一次抽到的选项(0 = 视差大厅)。静态 —— 同一次运行内跨主菜单实例有效。
    /// 主菜单返回一次就重建一次 MainBackground, 用实例字段记不住。
    /// </summary>
    private static int _lastOption = -1;

    /// <summary>UI 画布尺寸(和其它界面一致, 见 project.godot 的窗口/拉伸设置)</summary>
    private static readonly Vector2 CanvasSize = new(1920f, 1080f);

    /// <summary>
    /// 一套横向循环滚动的视差层。
    /// 每层用【两张首尾相接的同一张贴图】铺满画布, 一起左移, 移满一个周期就归零 ——
    /// 因为贴图左右无缝, 归零那一刻画面上看不出任何跳变。
    /// </summary>
    private sealed class ScrollLayer
    {
        /// <summary>左半边贴图</summary>
        public TextureRect A;

        /// <summary>右半边贴图(接在 A 右边)</summary>
        public TextureRect B;

        /// <summary>一张贴图的显示宽度, 也就是循环周期</summary>
        public float Width;

        /// <summary>漂移速度(像素/秒)</summary>
        public float Speed;

        /// <summary>当前偏移量, 始终保持在 [0, Width)</summary>
        public float Offset;
    }

    private readonly List<ScrollLayer> _layers = new();

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
            BuildVariant(option - 1);
        }

    }

    /// <summary>
    /// 抽本次要用的背景。返回值 0 = 视差大厅, 1..N = 第几套 CraftPix 背景。
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

        //抽重了就顺移: 在剩下的 count-1 个里再抽一个偏移量, 保证换一套。
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

    // ───────────────────────── 组装 ─────────────────────────

    /// <summary>
    /// 选项 0: 原来的视差大厅 —— 地牢石墙大厅, 左右各一扇拱窗, 窗外是黄昏天空与远山。
    /// 层级(由远及近): 天空(慢) -> 远山(快) -> 石墙(静止)。
    /// </summary>
    private void BuildParallaxHall()
    {
        //注意 Godot 的绘制顺序: 后 AddChild 的画在上层
        //所以按 天空 -> 远山 -> 石墙 的顺序添加
        AddScrollingLayer(SkyPath, HallPeriod, SkySpeed);
        AddScrollingLayer(RidgePath, HallPeriod, RidgeSpeed);

        //最近层: 石墙(静止), 拱窗处透明, 透出后面的天空
        var wall = GD.Load<Texture2D>(WallPath);
        if (wall == null)
        {
            GD.PushWarning($"[MainBackground] 背景贴图加载失败: {WallPath}");
        }
        AddChild(MakeLayerRect(wall, 1920f, false));
    }

    /// <summary>
    /// 选项 1~10: 一套 CraftPix 视差天空。
    /// 图层从 L1 开始加载, 加载不到就停 —— 有几层用几层。
    /// 一层都拿不到(源码构建版没这 10 套图)时退回视差大厅, 不会黑屏。
    /// </summary>
    private void BuildVariant(int variant)
    {
        var textures = new List<Texture2D>();
        for (var i = 1; i <= MaxLayersPerVariant; i++)
        {
            var path = VariantLayerPath(variant, i);

            //⚠️ 必须先 Exists 再 Load。
            //直接 GD.Load 一个不存在的路径, Godot 会打两条 ERROR
            //("Resource file not found" + "Error loading resource"), 每次进主菜单刷一次。
            //ResourceLoader.Exists 只是查表, 不存在时安静返回 false。
            if (!ResourceLoader.Exists(path))
            {
                break;
            }

            var tex = GD.Load<Texture2D>(path);
            if (tex == null)
            {
                break;
            }

            textures.Add(tex);
        }

        if (textures.Count == 0)
        {
            GD.PushWarning($"[MainBackground] 背景 v{variant + 1:D2} 的图层一个都没加载到, 退回视差大厅");
            BuildParallaxHall();
            return;
        }

        for (var i = 0; i < textures.Count; i++)
        {
            //越靠前的层滚得越快, 才有纵深。只有一层时不加速, 用最远层的速度。
            var t = textures.Count > 1 ? i / (float)(textures.Count - 1) : 0f;
            AddScrollingLayer(textures[i], CanvasSize.X, Mathf.Lerp(VariantBackSpeed, VariantFrontSpeed, t), true);
        }
    }

    /// <summary>加一层横向循环滚动的贴图</summary>
    private void AddScrollingLayer(string path, float width, float speed)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null)
        {
            GD.PushWarning($"[MainBackground] 背景贴图加载失败: {path}");
            return;
        }

        AddScrollingLayer(tex, width, speed, false);
    }

    /// <summary>加一层横向循环滚动的贴图</summary>
    private void AddScrollingLayer(Texture2D tex, float width, float speed, bool smooth)
    {
        var a = MakeLayerRect(tex, width, smooth);
        var b = MakeLayerRect(tex, width, smooth);
        AddChild(a);
        AddChild(b);
        _layers.Add(new ScrollLayer { A = a, B = b, Width = width, Speed = speed });
    }

    /// <summary>创建一个铺满整屏高度的图层贴图</summary>
    private static TextureRect MakeLayerRect(Texture2D tex, float width, bool smooth)
    {
        return new TextureRect
        {
            Texture = tex,
            Position = Vector2.Zero,
            Size = new Vector2(width, CanvasSize.Y),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,

            //【过滤方式: 这是"云朵滚动会抖"的关键】
            //
            //这些 CraftPix 图层是 576 宽, 要铺满 1920 画布 = 放大 3.3333 倍(不是整数倍)。
            //用 Nearest 时, 一个源像素有时占 3 个画布像素、有时占 4 个, 而且这个
            //"3,3,4,3,3,4..." 的宽度花纹会随着滚动【一格一格地往前爬】——
            //于是云朵边缘看起来不是平move, 而是在原地抖/发毛。
            //(离线做过时空图验证: 3.333x Nearest 的斜线是台阶状的,
            // 4x 整数 Nearest 和 3.333x Linear 都是笔直的。)
            //
            //整数倍能解决"花纹爬动", 但 4 倍时一个源像素占 4 个画布像素,
            //6 像素/秒的慢速下就是每 0.67 秒才动一次 —— 变成一卡一卡。
            //所以这里用 Linear: 亚像素位置会做插值, 任何速度都是完美平移, 代价是
            //边缘比 Nearest 略软一点(放大 3.33 倍本来就已经不是原始像素密度了)。
            //
            //大厅那三层不走这里(它们是 1:1 的, Nearest 不会产生花纹爬动, 保持原样)。
            TextureFilter = smooth
                ? CanvasItem.TextureFilterEnum.Linear
                : CanvasItem.TextureFilterEnum.Nearest,
        };
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        for (var i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            //偏移量一直留在 [0, Width) 里: 因为贴图左右无缝, 归零那一刻画面看不出跳变
            layer.Offset = Mathf.PosMod(layer.Offset + d * layer.Speed, layer.Width);
            layer.A.Position = new Vector2(-layer.Offset, 0f);
            layer.B.Position = new Vector2(layer.Width - layer.Offset, 0f);
        }
    }
}
