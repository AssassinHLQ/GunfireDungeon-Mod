using Godot;

/// <summary>
/// Rhino BOSS 的精灵帧工厂。
///
/// 素材来源: Codex 生成的 rhino-boss-overview-10x10-cute-white.png (1024x1024, 品红背景)。
/// 由 _music/rhino_extract_frames.py 处理:
///   1. 品红抠图 + despill(去溢色)
///   2. 按【连通域】切帧 —— 不能用 102.4 的固定栅格:
///      原图每行不是 10 格(实测 9/9/8/-/7/8/8/8/8/7), 而且尘土/点阵会把相邻格连成一片
///   3. 统一画布 271x77, 锚点 = 犀牛的"脚下中心" (136, 74)
///   4. 每个动作输出一张横向长条图, 这里用 AtlasTexture 按 (i*CellW, 0, CellW, CellH) 取帧
///
/// ⚠️ 【朝向 / 不需要镜像素材】
///   原图所有帧都是【朝左】的。看起来"向右时要镜像", 但本引擎是
///   <see cref="Role.Face"/> 的 setter -> _SetFace() 把【整个节点】旋转 180° + 翻转 Y,
///   即引擎已经自动做了水平镜像。所以:
///     · 这里只装载朝左的那一套;
///     · Rhino 朝右时由引擎自动翻, 技能动画也会跟着翻过去;
///     · 原图第 2 行(朝右的那套)【故意不用】—— 用了会翻两次, 变成反的。
///   Run / ReverseRun 是"朝前走 / 倒退走", 与左右朝向无关, 所以两者共用同一行。
/// </summary>
public static class RhinoSpriteFrames
{
    private const string Dir = "res://resource/sprite/role/rhino/";
    private const int CellW = 271;
    private const int CellH = 77;

    /// <summary>犀牛"脚下中心"在帧画布上的位置(切图脚本实测)。</summary>
    public const int AnchorX = 136;
    public const int AnchorY = 74;

    // ── 技能动画名 ──
    public static readonly StringName AnimCharge = "boss_charge";
    public static readonly StringName AnimBurrow = "boss_burrow";
    public static readonly StringName AnimBiteSmall = "boss_bite_small";
    public static readonly StringName AnimBiteBig = "boss_bite_big";
    public static readonly StringName AnimDash = "boss_dash";
    public static readonly StringName AnimSweep = "boss_sweep";
    public static readonly StringName AnimSlam = "boss_slam";

    /// <summary>
    /// 掘地动画里"犀牛在地下"的帧下标 —— 这几帧画面上只有土石, 没有犀牛本体。
    /// 切图脚本按"内容高度 < 40px"自动判定得到。
    /// </summary>
    public static readonly int[] BurrowUndergroundFrames = { 3, 4, 5 };

    private static SpriteFrames _cached;

    public static SpriteFrames Get()
    {
        if (_cached != null)
        {
            return _cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        // 移动: 第 1 行(朝左那一套), 9 帧
        AddStrip(frames, AnimatorNames.Idle, "RhinoMoveL.png", 9, true, 6);
        AddStrip(frames, AnimatorNames.Run, "RhinoMoveL.png", 9, true, 11);
        AddStrip(frames, AnimatorNames.ReverseRun, "RhinoMoveL.png", 9, true, 11,
            reverse: true);

        // 死亡: 素材里没有死亡动画, 用单帧让死亡立刻结算
        // (AnimatedSprite 只有 1 帧时 Role.DoDieWithAnimatedSprite 不会等 AnimationFinished,
        //  会立刻走 OnDie -> Enemy.OnDie 的血液 + 碎块特效)
        AddStrip(frames, AnimatorNames.Die, "RhinoMoveL.png", 1, false, 1);

        // 七个技能, 每个动作一行, 不循环
        AddStrip(frames, AnimCharge, "RhinoCharge.png", 8, false, 10);
        AddStrip(frames, AnimBurrow, "RhinoBurrow.png", 7, false, 10);
        AddStrip(frames, AnimBiteSmall, "RhinoBiteS.png", 8, false, 10);
        AddStrip(frames, AnimBiteBig, "RhinoBiteL.png", 8, false, 10);
        AddStrip(frames, AnimDash, "RhinoDash.png", 8, false, 12);
        AddStrip(frames, AnimSweep, "RhinoSweep.png", 8, false, 10);
        AddStrip(frames, AnimSlam, "RhinoSlam.png", 7, false, 9);

        _cached = frames;
        return _cached;
    }

    /// <summary>
    /// 把一张横向长条图装配成一个动画。每帧区域固定为 (i*CellW, 0, CellW, CellH)。
    /// </summary>
    private static void AddStrip(
        SpriteFrames frames, StringName name, string file,
        int count, bool loop, float speed, bool reverse = false)
    {
        var atlas = GD.Load<Texture2D>(Dir + file);
        if (atlas == null)
        {
            GD.PushError($"[Rhino] 找不到贴图: {Dir}{file}");
            return;
        }

        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, loop);
        frames.SetAnimationSpeed(name, speed);

        for (var i = 0; i < count; i++)
        {
            var col = reverse ? count - 1 - i : i;
            frames.AddFrame(name, new AtlasTexture
            {
                Atlas = atlas,
                Region = new Rect2(col * CellW, 0, CellW, CellH),
                FilterClip = true
            });
        }
    }
}
