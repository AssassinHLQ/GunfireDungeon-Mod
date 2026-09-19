using System.Collections;
using System.Collections.Generic;
using Config;
using DsUi;      // 项目自定义的 WaitForSeconds 在这个命名空间里（不是 Godot 自带的）
using Godot;

/// <summary>
/// 死神 —— 最终 BOSS，十个技能。
///
/// 【名字】对外显示名叫「甲方」(<see cref="BossDisplayName"/>)。
///   类名/资源 id 仍然是 Death / death0001，只有玩家看得到的名字改了口径，
///   所以要找这个 BOSS 的代码请看 Death，要找它的显示名请看 BossDisplayName。
///
/// 继承 <see cref="Boss"/>（和大橘 / 犀牛同一条路线）：
///   · BossDisplayName / Weight 定义在 Boss/AiRole 这条链上
///   · GameNotificationOverlay 靠 `is Boss` 找 BOSS 血条的主角
///
/// ── 登场静止（配乐是肖邦《冬风》练习曲 Op.25 No.11）──
///   实测那首曲子前 21.0 秒是引子(平均 −32 dB)，**21.1 秒主部才爆发**(直接跳到 −16 dB)。
///   所以 <see cref="DormantTime"/> = 21.1 秒：进 Boss 房后死神站着不动，
///   到音乐炸开的那一瞬间才开始攻击。静止期间有 50% 减伤
///   (<see cref="AiRole.DormantReducePct"/>)，否则玩家能站着白打 21 秒。
///
///   ⚠️ 因此 `resource/sound/bgm/WinterWind.ogg` **绝对不能裁头或做交叉淡化循环** ——
///      一动首尾，21.1 秒这个时刻就移位，起身就跟不上音乐了。
///
/// ── 技能表（伤害全部 = 1，与犀牛/大橘一致）──
///   0 boss_slash1    镰刀横斩一   短前摇，前方直线(中)
///   1 boss_slash2    镰刀横斩二   前方直线(大)
///   2 boss_chop      镰刀下劈     前冲一点 + 前方直线
///   3 boss_cast      灵魂弹       远距离直线
///   4 boss_cast2     强化灵魂弹   更远
///   5 boss_vortex    重力漩涡     自身周围圆形(中)
///   6 boss_shadow    暗影突袭     瞬移突进 + 圆形伤害
///   7 boss_summon    召唤法阵     自身周围圆形(大)
///   8 boss_harvest   灵魂收割     自身周围圆形(大)
///   9 boss_bone_rain 骨雨         自身周围圆形(最大)
///
/// 另外还有一层【常态碰撞伤害】：身体挨到玩家就扣 1 点，带冷却。
/// 突进类技能(下劈/暗影突袭)也靠这一层补判定，所以不需要各自的碰撞体。
///
/// ── 朝向 ──
/// 素材打包时已翻成朝左，Role.Face 的 setter 会自动镜像，这里不需要手动处理。
/// </summary>
public partial class DeathEnemy : Boss
{
    /// <summary>所有技能的伤害。和其他 Boss 一致，全部为 1。</summary>
    private const int SkillDamage = 1;

    // 常态碰撞伤害现在用 AiRole 里的通用实现(以前这里和犀牛各抄了一份,
    // 见 AiRole.ProcessContactDamage), 这里只需要给出数值。
    /// <summary>身体挨到玩家就扣 1 点。</summary>
    public override int ContactDamage => SkillDamage;

    /// <summary>死神体型大，碰撞判定半径比小怪远一些。</summary>
    public override float ContactDamageRange => 52f;

    /// <summary>常态碰撞伤害的冷却。</summary>
    public override float ContactDamageCooldown => 0.75f;

    /// <summary>钳制回可走区域时，在受击框之外再留多远。</summary>
    private const float WallPad = 20.0f;

    /// <summary>
    /// 登场静止时长（秒）。
    /// 21.1 = 《冬风》练习曲主部爆发的时刻（实测，见类注释）。
    /// </summary>
    public override float DormantTime => 21.1f;

    private static readonly Vector2 BossSpriteOffset = DeathSpriteFrames.SpriteOffset;

    /// <summary>死神比普通敌人高得多，受击框要按帧画布量（身体约 56 宽 100 高）。</summary>
    private static readonly Vector2 BossHitboxSize = new(56, 100);
    private static readonly Vector2 BossHitboxOffset = new(0, -50);

    /// <summary>一个技能的参数。</summary>
    private sealed class SkillDef
    {
        public readonly StringName Anim;
        public readonly float Windup;
        public readonly float Range;
        public readonly float HalfWidth;
        public readonly bool IsLine;
        public readonly float Dash;

        public SkillDef(StringName anim, float windup, float range,
            float halfWidth, bool isLine, float dash)
        {
            Anim = anim;
            Windup = windup;
            Range = range;
            HalfWidth = halfWidth;
            IsLine = isLine;
            Dash = dash;
        }
    }

    private static readonly SkillDef[] Skills =
    {
        // 0 横斩一：短前摇，前方中范围直线
        new(DeathSpriteFrames.AnimSlash1, 0.35f, 115f, 46f, true, 0f),
        // 1 横斩二：前方大范围直线
        new(DeathSpriteFrames.AnimSlash2, 0.50f, 170f, 62f, true, 0f),
        // 2 下劈：往前压一点再劈
        new(DeathSpriteFrames.AnimChop, 0.45f, 130f, 44f, true, 55f),
        // 3 灵魂弹：远距离直线
        new(DeathSpriteFrames.AnimCast, 0.40f, 280f, 34f, true, 0f),
        // 4 强化灵魂弹：更远
        new(DeathSpriteFrames.AnimCast2, 0.55f, 340f, 34f, true, 0f),
        // 5 重力漩涡：自身周围中范围
        new(DeathSpriteFrames.AnimVortex, 0.60f, 125f, 0f, false, 0f),
        // 6 暗影突袭：瞬移突进 + 圆形伤害
        new(DeathSpriteFrames.AnimShadow, 0.45f, 95f, 0f, false, 200f),
        // 7 召唤法阵：自身周围大范围
        new(DeathSpriteFrames.AnimSummon, 0.60f, 135f, 0f, false, 0f),
        // 8 灵魂收割：自身周围大范围
        new(DeathSpriteFrames.AnimHarvest, 0.70f, 180f, 0f, false, 0f),
        // 9 骨雨：自身周围最大范围
        new(DeathSpriteFrames.AnimBoneRain, 0.75f, 160f, 0f, false, 0f),
    };

    private int _skillIndex;
    private bool _attacking;
    private long _coroutine = -1;
    private Rect2? _walkableRect;

    // BOSS 登场横幅上显示的名字（GameNotificationOverlay 读它）。
    // 2026-09-19 由 "Reaper" 改为「甲方」—— 类名和资源 id 不动。
    public override string BossDisplayName => "甲方";

    public override void OnInit()
    {
        base.OnInit();

        // Boss.OnInit 是给"程序化绘制"的 boss 用的，把精灵和状态机都关了，
        // 死神是精灵动画 boss，要再打开（和 DajuEnemy / RhinoEnemy 一样）。
        AnimatedSprite.Visible = true;
        StateController.Enable = true;

        WeaponPack.SetCapacity(0);
        NoWeaponAttack = true;
        MountLookTarget = true;
        FiringStand = true;
        RoleState.CanPickUpWeapon = false;

        //血量走 RoleBase.json 里的 death0001.Hp(现在是 2400), 不在这里写死

        AnimatedSprite.SpriteFrames = DeathSpriteFrames.Get();
        AnimatedSprite.Offset = BossSpriteOffset;
        AnimatedSprite.Scale = Vector2.One;
        AnimatedSprite.RotationDegrees = 0;   // 朝向由 Role.Face 的节点旋转负责
        AnimatedSprite.Play(AnimatorNames.Idle);

        ConfigureBossHitbox();
    }

    protected override RoleState OnCreateRoleState()
    {
        var state = base.OnCreateRoleState();
        state.MoveSpeed = 55;
        return state;
    }

    private void ConfigureBossHitbox()
    {
        if (HurtCollision == null)
        {
            return;
        }
        HurtCollision.Shape = new RectangleShape2D { Size = BossHitboxSize };
        HurtCollision.Position = BossHitboxOffset;
    }

    public override void HurtHandler(ActivityObject target, AttackStats attackStats, float f)
    {
        base.HurtHandler(target, attackStats, f);

        if (Hp <= 0 && _coroutine != -1)
        {
            // 死亡后必须停掉自己那条技能协程，否则尸体还会继续放技能。
            StopCoroutine(_coroutine);
            _coroutine = -1;
            _attacking = false;
        }
    }

    public override void Attack()
    {
        if (_attacking || IsDie || IsDormant)
        {
            return;
        }
        _attacking = true;
        _coroutine = StartCoroutine(RunSkill());
    }

    private IEnumerator RunSkill()
    {
        // ActivityObject._Process 先跑本类 Process，再跑 StateController 的状态 Process，
        // 后者会用 DoIdle()/DoMove() 把技能动画整段覆盖掉，所以技能期间要临时禁用状态机。
        var restore = StateController != null && StateController.Enable;

        try
        {
            if (LookTarget is not Role target || target.IsDie ||
                !IsTargetInSameRoom(target))
            {
                yield break;
            }

            if (StateController != null)
            {
                StateController.Enable = false;
            }

            var skill = Skills[_skillIndex % Skills.Length];
            _skillIndex++;

            AnimatedSprite.Play(skill.Anim);
            BasisVelocity = Vector2.Zero;
            AttackTimer = Mathf.Max(1.15f, GetAnimationDuration(skill.Anim) + 0.35f);

            var self = GetCenterPosition();
            var dir = (target.GetCenterPosition() - self).Normalized();
            if (dir.IsZeroApprox())
            {
                dir = Face == FaceDirection.Left ? Vector2.Left : Vector2.Right;
            }

            // 直线技能：预警画在"冲出去之后"的位置；圆形技能画在自己脚下
            var origin = skill.Dash > 0f ? self + dir * skill.Dash : self;
            ShowWarning(origin, skill, dir.Angle());

            yield return new WaitForSeconds(skill.Windup);

            if (LookTarget is not Role t2 || t2.IsDie || !IsTargetInSameRoom(t2))
            {
                yield break;
            }

            if (skill.Dash > 0f)
            {
                // 暗影突袭：瞬移过去（不是慢慢走），落地就结算
                Position = ClampInsideRoom(Position + dir * skill.Dash);
            }

            var targetPos = t2.GetCenterPosition();
            var hit = skill.IsLine
                ? IsInsideLine(targetPos, origin, dir, skill.Range, skill.HalfWidth)
                : targetPos.DistanceTo(origin) <= skill.Range;

            if (hit)
            {
                Hurt(t2);
            }

            SpawnEffect(origin, dir.Angle());
        }
        finally
        {
            // 必须加 !IsDie：boss 在技能中被击杀时 Role.HurtHandler 已经把
            // StateController.Enable 设成 false，这里再恢复会让尸体继续被 AI 推动，
            // 而且 DungeonManager 会误判房间还有活敌，门永远打不开。
            if (restore && StateController != null && !IsDie)
            {
                StateController.Enable = true;
            }
            _attacking = false;
        }
    }

    /// <summary>对目标造成 1 点物理伤害。</summary>
    private void Hurt(Role target)
    {
        var delta = target.GetCenterPosition() - GetCenterPosition();
        target.HurtArea.Hurt(this,
            new List<AttackStats> { new(SkillDamage, DamageType.Physical) },
            null, delta.Angle());
    }

    private float GetAnimationDuration(StringName animation)
    {
        var frames = AnimatedSprite.SpriteFrames;
        if (frames == null || !frames.HasAnimation(animation))
        {
            return 1.0f;
        }
        var speed = Mathf.Max(0.01f, frames.GetAnimationSpeed(animation));
        return (float)(frames.GetFrameCount(animation) / speed) + 0.05f;
    }

    private static bool IsInsideLine(
        Vector2 point, Vector2 origin, Vector2 direction,
        float length, float halfWidth)
    {
        var offset = point - origin;
        var forward = offset.Dot(direction);
        if (forward < 0 || forward > length)
        {
            return false;
        }
        return Mathf.Abs(offset.Cross(direction)) <= halfWidth;
    }

    private bool IsTargetInSameRoom(Role target)
    {
        return target != null && !target.IsDie &&
               AffiliationArea != null &&
               target.AffiliationArea == AffiliationArea;
    }

    private void SpawnEffect(Vector2 position, float rotation)
    {
        var effect = ObjectManager.GetPoolItem<IEffect>(
            ResourcePath.prefab_effect_weapon_ShotFire0003_tscn);
        var node = (Node2D)effect;
        node.GlobalPosition = position;
        node.Rotation = rotation;
        node.AddToActivityRoot(RoomLayerEnum.YSortLayer);
        effect.PlayEffect();
    }

    /// <summary>
    /// 画攻击范围预警。直线技能画矩形，圆形技能画圆。
    /// </summary>
    private void ShowWarning(Vector2 position, SkillDef skill, float rotation)
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (world?.YSortLayer == null)
        {
            return;
        }

        var warning = new BossAttackWarning
        {
            Name = $"BossAttackWarning_Death_{_skillIndex}"
        };
        world.YSortLayer.AddChild(warning);
        warning.Configure(position, skill.IsLine, isSector: false,
            skill.Range, skill.HalfWidth, skill.Windup, rotation);
    }

    /// <summary>
    /// 当前房间【可走区域】的世界坐标矩形 —— 取导航多边形顶点的包围盒。
    ///
    /// 【为什么不用房间的 Size】RoomInfo.GetWidth()/GetHeight() 给的是**含墙的外框**，
    /// 拿它做钳制只能保证 BOSS 在外框之内，而墙就在外框里侧 ——
    /// 瞬移/突进一落地就进墙，玩家打不到（犀牛那边踩过这个坑）。
    /// 导航多边形才是真正能走的区域。
    /// </summary>
    private Rect2? GetWalkableRect()
    {
        if (_walkableRect.HasValue)
        {
            return _walkableRect;
        }

        var room = AffiliationArea?.RoomInfo;
        var vertices = room?.RoomSplit?.TileInfo?.NavigationVertices;
        if (room == null || vertices == null || vertices.Count == 0)
        {
            // 不缓存失败结果: 刚进房间时 AffiliationArea 可能还没挂上
            return null;
        }

        var first = room.ToGlobalPosition(vertices[0].AsVector2());
        float minX = first.X, maxX = first.X;
        float minY = first.Y, maxY = first.Y;
        foreach (var v in vertices)
        {
            var p = room.ToGlobalPosition(v.AsVector2());
            minX = Mathf.Min(minX, p.X);
            maxX = Mathf.Max(maxX, p.X);
            minY = Mathf.Min(minY, p.Y);
            maxY = Mathf.Max(maxY, p.Y);
        }

        _walkableRect = new Rect2(minX, minY, maxX - minX, maxY - minY);
        return _walkableRect;
    }

    /// <summary>把一条轴钳到 [lo+pad, hi-pad]; 区间太小时取中点, 避免出现 min &gt; max。</summary>
    private static float ClampAxis(float value, float lo, float hi, float pad)
    {
        var a = lo + pad;
        var b = hi - pad;
        return a >= b ? (lo + hi) * 0.5f : Mathf.Clamp(value, a, b);
    }

    /// <summary>把目标位置钳制回房间内（优先用导航包围盒，拿不到就回退到房间外框）。</summary>
    private Vector2 ClampInsideRoom(Vector2 target)
    {
        var padX = BossHitboxSize.X * 0.5f + WallPad;
        var padY = BossHitboxSize.Y * 0.5f + WallPad;

        var walkable = GetWalkableRect();
        if (walkable.HasValue)
        {
            var r = walkable.Value;
            return new Vector2(
                ClampAxis(target.X, r.Position.X, r.End.X, padX),
                ClampAxis(target.Y, r.Position.Y, r.End.Y, padY));
        }

        var room = AffiliationArea?.RoomInfo;
        if (room == null)
        {
            return target;
        }

        var roomOrigin = room.GetWorldPosition();
        var roomEnd = roomOrigin + new Vector2(room.GetWidth(), room.GetHeight());
        padX = BossHitboxSize.X * 0.5f + GameConfig.TileCellSize * 2f;
        padY = BossHitboxSize.Y * 0.5f + GameConfig.TileCellSize * 2f;
        return new Vector2(
            ClampAxis(target.X, roomOrigin.X, roomEnd.X, padX),
            ClampAxis(target.Y, roomOrigin.Y, roomEnd.Y, padY));
    }
}
