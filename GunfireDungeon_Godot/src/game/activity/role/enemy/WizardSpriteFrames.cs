using Godot;

/// <summary>
/// 「邪恶法师」精英怪的精灵帧工厂。
///
/// 素材: **Evil Wizard Asset Pack** —— **CC0**(公有领域), 可商用、可修改、可随仓库分发。
/// 见 <c>resource/sprite/role/wizard0001/LICENSE.txt</c>。
///
/// 【为什么用工厂而不是 .tres】素材是 5 张横向图集(每帧 150x150 的格子),
/// 要切成 5 个动画、还要把"倒退跑"单独做一条(素材只有一条 Move)。
/// 手写 .tres 容易帧对不上; 项目里的 Boss(Daju / Rhino / Death)都是代码里建 SpriteFrames,
/// 这里沿用同一套写法。
///
/// 【格子尺寸 ≠ 角色尺寸】每格 150x150, 但角色本身只有 **32x55** —— 四周全是大片空白。
/// 所以必须靠 <see cref="SpriteOffset"/> 把"身体中心 + 脚底"挪到节点原点上,
/// 否则角色会整体偏到格子的右下方去。
/// </summary>
public static class WizardSpriteFrames
{
    private const string Dir = "res://resource/sprite/role/wizard0001/";

    /// <summary>素材每帧的格子边长(不是角色大小)</summary>
    private const int Cell = 150;

    /// <summary>
    /// 对齐偏移: 让"身体水平中心 + 脚底"落在节点原点。
    ///
    /// 量测结果(逐帧求内容包围盒):
    ///   Idle     内容 x[54,89)  y[44,102)  脚底 y=102  身体中心 x≈71
    ///   Move     内容 x[46,98)  y[33,101)  脚底 y=101  身体中心 x≈72
    ///   Attack   内容 x[59,140) y[51,101)  ← 法杖甩到 x=140
    ///   所以 pivot 取【身体】中心(x=71)而不是包围盒中心 —— 按包围盒算的话,
    ///   放技能那一刻角色会突然整体左移, 看起来像瞬移。
    /// </summary>
    public static readonly Vector2 SpriteOffset = new(-71, -101);

    /// <summary>主动画的帧数(每张图集固定 8 帧)</summary>
    private const int MainFrames = 8;

    private static SpriteFrames _cached;

    public static SpriteFrames Get()
    {
        if (_cached != null)
        {
            return _cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        var idle = GD.Load<Texture2D>(Dir + "WizardIdle.png");
        var move = GD.Load<Texture2D>(Dir + "WizardMove.png");
        var attack = GD.Load<Texture2D>(Dir + "WizardAttack.png");
        var death = GD.Load<Texture2D>(Dir + "WizardDeath.png");

        AddStrip(frames, AnimatorNames.Idle, idle, MainFrames, true, 6f);
        AddStrip(frames, AnimatorNames.Run, move, MainFrames, true, 10f);
        // 素材只有一条 Move, 倒退跑就把帧序倒过来
        AddStrip(frames, AnimatorNames.ReverseRun, move, MainFrames, true, 10f, true);
        AddStrip(frames, AnimatorNames.Attack, attack, MainFrames, false, 12f);
        AddStrip(frames, AnimatorNames.Die, death, 5, false, 8f);

        _cached = frames;
        return _cached;
    }

    /// <summary>把一张横向图集切成一个动画</summary>
    private static void AddStrip(SpriteFrames frames, StringName name, Texture2D atlas,
        int count, bool loop, float speed, bool reverse = false)
    {
        if (atlas == null)
        {
            GD.PushError($"[Wizard] 找不到贴图, 动画 {name} 会缺失");
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
                Region = new Rect2(col * Cell, 0, Cell, Cell),
                FilterClip = true
            });
        }
    }
}
