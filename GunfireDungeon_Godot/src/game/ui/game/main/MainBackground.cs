using Godot;

namespace UI.game.Main;

/// <summary>
/// 主菜单视差背景 —— 地牢石墙大厅, 左右各一扇拱窗, 窗外是黄昏天空与远山。
/// <para/>
/// 层级(由远及近): 天空(慢) -> 远山(快) -> 石墙(静止)。
/// 天空与远山贴图都是 3840 宽且左右无缝, 各放两份首尾相接循环移动,
/// 这样任何时刻都能铺满 1920 宽的画布。
/// </summary>
public partial class MainBackground : Godot.Control
{
    /// <summary>天空贴图</summary>
    private const string SkyPath = "res://resource/sprite/ui/mainBackground/bg_sky.png";

    /// <summary>远山贴图</summary>
    private const string RidgePath = "res://resource/sprite/ui/mainBackground/bg_ridge.png";

    /// <summary>石墙贴图(静止)</summary>
    private const string WallPath = "res://resource/sprite/ui/mainBackground/bg_wall.png";

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
