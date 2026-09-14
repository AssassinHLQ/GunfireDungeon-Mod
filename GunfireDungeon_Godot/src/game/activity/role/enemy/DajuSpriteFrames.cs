using Godot;

/// <summary>
/// 大橘 BOSS 的精灵帧工厂。
///
/// 移植自 4.2 的 GameApplication.CreateHakimiBossSpriteFrames，做了两处调整：
///   1. 资源路径改成 resource/sprite/role/daju/（素材已像素化到 24 色）
///   2. 单独放在这里而不是塞进 GameApplication，避免动到那个大文件
///
/// 为什么需要这个工厂（而不是直接用 .tres）：
///   boss 的攻击帧来自 DajuBossAttacks.png（128x128 一格），
///   而 idle/walk/attack 原图是 64x64 一格。
///   如果两边尺寸不统一，idle 时 boss 会缩小一半、一放技能又忽然变大。
///   所以这里把 idle/walk/attack 用【最近邻】放大 2 倍，统一到 128x128。
/// </summary>
public static class DajuSpriteFrames
{
    private const string Dir = "res://resource/sprite/role/daju/";
    private const int BossCell = 128;   // boss 帧尺寸
    private const int BossAttackFrames = 7;

    private static SpriteFrames _cached;

    public static SpriteFrames Get()
    {
        if (_cached != null)
        {
            return _cached;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        var idle = Upscale2X(Dir + "DajuIdle.png");
        var walk = Upscale2X(Dir + "DajuWalk.png");
        var attack = Upscale2X(Dir + "DajuAttack.png");
        var bossAttacks = GD.Load<Texture2D>(Dir + "DajuBossAttacks.png");

        // 移动类动画: 每行一个动作, 4 帧
        void Movement(StringName name, Texture2D atlas, int row,
            bool loop, float speed, bool reverse = false)
        {
            frames.AddAnimation(name);
            frames.SetAnimationLoop(name, loop);
            frames.SetAnimationSpeed(name, speed);
            for (var i = 0; i < 4; i++)
            {
                var col = reverse ? 3 - i : i;
                frames.AddFrame(name, new AtlasTexture
                {
                    Atlas = atlas,
                    Region = new Rect2(col * BossCell, row * BossCell,
                        BossCell, BossCell),
                    FilterClip = true
                });
            }
        }

        // 攻击类动画: 每行一个技能, 7 帧, 不循环
        void BossAttack(StringName name, int row, float speed)
        {
            frames.AddAnimation(name);
            frames.SetAnimationLoop(name, false);
            frames.SetAnimationSpeed(name, speed);
            for (var i = 0; i < BossAttackFrames; i++)
            {
                frames.AddFrame(name, new AtlasTexture
                {
                    Atlas = bossAttacks,
                    Region = new Rect2(i * BossCell, row * BossCell,
                        BossCell, BossCell),
                    FilterClip = true
                });
            }
        }

        Movement(AnimatorNames.Idle, idle, 0, true, 4);
        Movement(AnimatorNames.Run, walk, 2, true, 9);
        Movement(AnimatorNames.ReverseRun, walk, 1, true, 9, true);
        Movement(AnimatorNames.Attack, attack, 2, false, 9);
        Movement(AnimatorNames.Die, attack, 3, false, 7);

        // 大橘的四个技能
        BossAttack("boss_claw_combo", 0, 9);
        BossAttack("boss_leap_slam", 1, 9);
        BossAttack("boss_fire_wave", 2, 8);
        BossAttack("boss_rage_roar", 3, 8);

        _cached = frames;
        return _cached;
    }

    /// <summary>
    /// 最近邻放大 2 倍。用最近邻而不是双线性 —— 双线性会把像素画糊掉。
    /// </summary>
    private static Texture2D Upscale2X(string path)
    {
        var src = GD.Load<Texture2D>(path);
        if (src == null)
        {
            GD.PushError($"[Daju] 找不到贴图: {path}");
            return null;
        }

        var img = src.GetImage();
        img.Resize(src.GetWidth() * 2, src.GetHeight() * 2,
            Image.Interpolation.Nearest);
        return ImageTexture.CreateFromImage(img);
    }
}
