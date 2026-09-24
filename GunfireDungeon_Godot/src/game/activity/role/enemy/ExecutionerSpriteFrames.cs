using Godot;

/// <summary>
/// 「不死刽子手」精英怪的精灵帧工厂。
///
/// 素材: <b>Boss: Undead Executioner [FREE]</b> —— 作者 <b>Kronovi-</b>,
/// 页面授权原文: *"you can use it for commercial and non-commercial use,
/// credits are not required ... redistributing and reselling the sprite are restricted."*
/// 见 <c>resource/sprite/role/executioner0001/LICENSE.md</c>。
///
/// 【为什么用工厂而不是 .tres】素材是 6 张横向/网格图集, 每帧 **100x100** 的格子
/// (用"格子边界列是否全透明"验过: cell=100 时 0 处跨界, cell=50/150 都有跨界)。
/// 还要把"倒退跑"单独做一条、再补两条自定义动画, 手写 .tres 容易帧对不上;
/// 项目里的 Boss(Daju / Rhino / Death)与邪恶法师都是代码里建 SpriteFrames, 这里沿用。
///
/// 【格子尺寸 ≠ 角色尺寸】每格 100x100, 但角色本身只有 **45x62**
/// (逐帧包围盒实测 x[34,78] y[21,82], 脚底 y=82, 身体水平中心 x=56)。
/// 所以必须靠 <see cref="SpriteOffset"/> 把"身体中心 + 脚底"挪到节点原点上。
/// </summary>
public static class ExecutionerSpriteFrames
{
    private const string Dir = "res://resource/sprite/role/executioner0001/";

    /// <summary>素材每帧的格子边长(不是角色大小)</summary>
    private const int Cell = 100;

    /// <summary>
    /// 对齐偏移: 让"身体水平中心 + 脚底"落在节点原点。
    ///
    /// 量测结果(逐帧求内容包围盒, 取各帧一致的稳定值):
    ///   Idle     内容 x[34,78] y[21,82]  脚底 y=82  身体水平中心 x=56
    ///   Move     同上(x 恒定在 34..78)
    ///   Attack   刀刃会甩到 x=95 / x=18, 但【身体】仍是 34..78
    ///   所以 pivot 取【身体】中心(x=56)而不是包围盒中心 ——
    ///   按包围盒算的话, 挥刀那一刻角色会突然整体平移。
    ///
    /// ⚠️ AnimatedSprite2D 的 <c>centered</c> 是 <b>true</b>: 贴图以"格子中心"对齐节点原点,
    ///    所以偏移是 <c>格子中心 - 锚点</c>。和两个 Boss
    ///    (<see cref="RhinoSpriteFrames"/> / <see cref="DeathSpriteFrames"/>) 同一套写法。
    ///    写成 <c>-锚点</c> 会整只偏到左上 50 像素去(见 <see cref="WizardSpriteFrames"/> 里记的那个坑)。
    /// </summary>
    public static readonly Vector2 SpriteOffset = new(Cell * 0.5f - 56f, Cell * 0.5f - 82f);

    /// <summary>召唤幽灵的自定义动画名(不在 AnimatorNames 里)</summary>
    public static readonly StringName AnimSummon = "executioner_summon";

    /// <summary>大范围横扫的自定义动画名</summary>
    public static readonly StringName AnimSweep = "executioner_sweep";

    private static SpriteFrames _cached;

    public static SpriteFrames Get()
    {
        if (_cached != null)
        {
            return _cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        var idle = GD.Load<Texture2D>(Dir + "ExecutionerIdle.png");
        var move = GD.Load<Texture2D>(Dir + "ExecutionerMove.png");
        var attack = GD.Load<Texture2D>(Dir + "ExecutionerAttack.png");
        var sweep = GD.Load<Texture2D>(Dir + "ExecutionerSweep.png");
        var summon = GD.Load<Texture2D>(Dir + "ExecutionerSummon.png");
        var death = GD.Load<Texture2D>(Dir + "ExecutionerDeath.png");

        //素材没有走路动画, 用 idle2(8 帧的躯干起伏)当移动 —— 它是"飘"过来的不死者, 不算违和。
        AddStrip(frames, AnimatorNames.Idle, idle, 4, true, 5f);
        AddStrip(frames, AnimatorNames.Run, move, 8, true, 7f);
        AddStrip(frames, AnimatorNames.ReverseRun, move, 8, true, 7f, true);
        AddStrip(frames, AnimatorNames.Attack, attack, 13, false, 14f);
        AddStrip(frames, AnimSweep, sweep, 12, false, 13f);
        AddStrip(frames, AnimSummon, summon, 5, false, 10f);
        AddStrip(frames, AnimatorNames.Die, death, 18, false, 12f);

        _cached = frames;
        return _cached;
    }

    /// <summary>
    /// 把一张网格图集按行优先切成一个动画(每行 <see cref="Cell"/> 一帧)。
    /// </summary>
    private static void AddStrip(SpriteFrames frames, StringName name, Texture2D atlas,
        int count, bool loop, float speed, bool reverse = false)
    {
        if (atlas == null)
        {
            GD.PushError($"[Executioner] 找不到贴图, 动画 {name} 会缺失");
            return;
        }

        var cols = Mathf.Max(1, atlas.GetWidth() / Cell);

        frames.AddAnimation(name);
        //用新 API(SetAnimationLoop 在 4.7 已过时)
        frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
        frames.SetAnimationSpeed(name, speed);

        for (var i = 0; i < count; i++)
        {
            var col = reverse ? count - 1 - i : i;
            frames.AddFrame(name, new AtlasTexture
            {
                Atlas = atlas,
                Region = new Rect2(col % cols * Cell, col / cols * Cell, Cell, Cell),
                FilterClip = true
            });
        }
    }
}
