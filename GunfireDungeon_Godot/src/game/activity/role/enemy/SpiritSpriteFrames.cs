using Godot;

/// <summary>
/// 「刽子手的幽灵」—— 不死刽子手召唤出来的小怪。
///
/// 和刽子手同一份素材(同一页面、同一授权, 见
/// <c>resource/sprite/role/executioner0001/LICENSE.md</c>),
/// 是素材里的 <c>summonAppear / summonIdle / summonDeath</c> 三张图集,
/// 每帧 **50x50** 格子。
///
/// 它没有攻击动画也没有走路动画 —— 所以设计上就是"贴上来撞人"的追兵:
/// 靠 <see cref="SpiritEnemy"/> 里提高的碰撞伤害打人, 移动借用 summonIdle。
/// </summary>
public static class SpiritSpriteFrames
{
    private const string Dir = "res://resource/sprite/role/spirit0001/";

    private const int Cell = 50;

    /// <summary>
    /// 对齐偏移: 让"身体水平中心 + 脚底"落在节点原点。
    ///
    /// 实测(逐帧包围盒): 内容 x[19,28] y[18,37]
    ///   身体水平中心 x=23.5  脚底 y=37
    /// 所有帧(含出现/死亡)的脚底都是 y=37, 只有死亡末帧才收缩。
    ///
    /// ⚠️ 同 <see cref="ExecutionerSpriteFrames"/>: AnimatedSprite2D 是 centered=true,
    ///    偏移 = 格子中心 - 锚点。
    /// </summary>
    public static readonly Vector2 SpriteOffset = new(Cell * 0.5f - 23.5f, Cell * 0.5f - 37f);

    /// <summary>出场动画(召唤时播一次)</summary>
    public static readonly StringName AnimAppear = "spirit_appear";

    private static SpriteFrames _cached;

    public static SpriteFrames Get()
    {
        if (_cached != null)
        {
            return _cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        var idle = GD.Load<Texture2D>(Dir + "SpiritIdle.png");
        var appear = GD.Load<Texture2D>(Dir + "SpiritAppear.png");
        var death = GD.Load<Texture2D>(Dir + "SpiritDeath.png");

        AddStrip(frames, AnimatorNames.Idle, idle, 4, true, 8f);
        AddStrip(frames, AnimatorNames.Run, idle, 4, true, 8f);
        AddStrip(frames, AnimatorNames.ReverseRun, idle, 4, true, 8f, true);
        AddStrip(frames, AnimatorNames.Attack, idle, 4, false, 8f);
        AddStrip(frames, AnimAppear, appear, 6, false, 12f);
        AddStrip(frames, AnimatorNames.Die, death, 5, false, 12f);

        _cached = frames;
        return _cached;
    }

    private static void AddStrip(SpriteFrames frames, StringName name, Texture2D atlas,
        int count, bool loop, float speed, bool reverse = false)
    {
        if (atlas == null)
        {
            GD.PushError($"[ExecutionerSpirit] 找不到贴图, 动画 {name} 会缺失");
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
