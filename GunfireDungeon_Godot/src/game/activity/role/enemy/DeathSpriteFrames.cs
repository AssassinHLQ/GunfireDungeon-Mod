using Godot;

/// <summary>
/// 死神(最终 BOSS)的精灵帧工厂。
///
/// 素材来源: GPT 生成的 reaper-final-boss-2048-16x16.png (2048x2048, 纯洋红背景)。
/// 由 _music/slice_death.py + pack_death_strips.py 处理:
///   1. 洋红硬色度键(实测破洞率 2.39%, 小碎洞只有 721px)
///   2. 按【实测】切帧, 不是它声称的 16x16:
///      · 纵向 15 条实测行带(行距约 144px, 不是 128)
///      · 横向每行自己量出来的帧距(146~186px, 不是 128)
///   3. 所有动作共用同一个画布 186x123, 锚点 = 角色"脚下中心" (93, 122)
///      —— 共用画布是为了切动作时不跳; 帧不按包围盒裁紧, 保留在原格里的位置
///   4. 每个动作一张横向长条图, 这里用 AtlasTexture 按 (i*CellW, 0, CellW, CellH) 取帧
///
/// ⚠️ 【朝向】原图是**朝右**的, 打包时已经水平翻转成**朝左** ——
///   本引擎的约定是"素材朝左"(见 <see cref="Role.Face"/> 的 setter -> _SetFace()
///   会把整个节点旋转 180° 做镜像)。所以这里和其他敌人一样, 不需要任何手动镜像,
///   朝右时引擎自动翻, 技能动画也跟着翻。
/// </summary>
public static class DeathSpriteFrames
{
    private const string Dir = "res://resource/sprite/role/death0001/";

    /// <summary>帧画布(所有动作共用)。</summary>
    public const int CellW = 186;
    public const int CellH = 123;

    /// <summary>角色"脚下中心"在帧画布上的位置(打包脚本实测)。</summary>
    public const int AnchorX = 93;
    public const int AnchorY = 122;

    public const int FrameWidth = CellW;
    public const int FrameHeight = CellH;

    /// <summary>
    /// AnimatedSprite2D 的 Offset —— 让"脚下中心"落在节点原点上。
    /// AnimatedSprite2D 把贴图居中画在 Offset 处, 所以
    /// Offset.Y = -(AnchorY - FrameHeight/2) = -(122 - 61.5) = -60.5。
    /// 用算式而不是写死数字, 换帧高时不会悄悄错位。
    /// </summary>
    public static readonly Vector2 SpriteOffset =
        new(0f, -(AnchorY - FrameHeight * 0.5f));

    // ── 技能动画名(一行一个动作) ──
    public static readonly StringName AnimSlash1 = "boss_slash1";      // 镰刀横斩一
    public static readonly StringName AnimSlash2 = "boss_slash2";      // 镰刀横斩二
    public static readonly StringName AnimChop = "boss_chop";          // 镰刀下劈
    public static readonly StringName AnimCast = "boss_cast";          // 灵魂弹
    public static readonly StringName AnimCast2 = "boss_cast2";        // 强化灵魂弹
    public static readonly StringName AnimVortex = "boss_vortex";      // 重力漩涡
    public static readonly StringName AnimShadow = "boss_shadow";      // 暗影突袭
    public static readonly StringName AnimSummon = "boss_summon";      // 召唤法阵
    public static readonly StringName AnimHarvest = "boss_harvest";    // 灵魂收割
    public static readonly StringName AnimBoneRain = "boss_bone_rain"; // 骨雨

    private static SpriteFrames _cached;

    public static SpriteFrames Get()
    {
        if (_cached != null)
        {
            return _cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        // 移动: 第 1 行 walk, 14 帧
        AddStrip(frames, AnimatorNames.Idle, "idle.png", 13, true, 6);
        AddStrip(frames, AnimatorNames.Run, "walk.png", 14, true, 11);
        AddStrip(frames, AnimatorNames.ReverseRun, "walk.png", 14, true, 11, reverse: true);

        // 死亡: 第 15 行(跪倒 -> 灵魂升起 -> 消散), 14 帧, 只播一次
        AddStrip(frames, AnimatorNames.Die, "death.png", 14, false, 8);

        // 十个技能, 每个动作一行, 不循环
        AddStrip(frames, AnimSlash1, "slash1.png", 12, false, 12);
        AddStrip(frames, AnimSlash2, "slash2.png", 12, false, 12);
        AddStrip(frames, AnimChop, "chop_then_burrow.png", 12, false, 10);
        AddStrip(frames, AnimCast, "cast.png", 12, false, 10);
        AddStrip(frames, AnimCast2, "cast2.png", 13, false, 10);
        AddStrip(frames, AnimVortex, "vortex.png", 12, false, 12);
        AddStrip(frames, AnimShadow, "shadow_burst.png", 16, false, 12);
        AddStrip(frames, AnimSummon, "summon.png", 11, false, 10);
        AddStrip(frames, AnimHarvest, "cast3.png", 11, false, 10);
        AddStrip(frames, AnimBoneRain, "bone_rain.png", 11, false, 10);

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
            GD.PushError($"[Death] 找不到贴图: {Dir}{file}");
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
