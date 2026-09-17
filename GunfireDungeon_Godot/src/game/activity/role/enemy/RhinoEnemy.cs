using System.Collections;
using System.Collections.Generic;
using Config;
using DsUi;      // 项目自定义的 WaitForSeconds 在这个命名空间里（不是 Godot 自带的）
using Godot;

/// <summary>
/// Rhino —— 会放七个技能的犀牛 BOSS。
///
/// 继承 <see cref="Boss"/>（和 DajuEnemy 同一条路线），原因一样：
///   · BossDisplayName / Weight 定义在 Boss/AiRole 这条链上
///   · GameNotificationOverlay 靠 `is Boss` 找 BOSS 血条的主角
///
/// ── 技能表（伤害全部 = 1，用户指定）──
///   0 boss_charge     冲撞      长前摇直线，冲出去撞人
///   1 boss_burrow     掘地突现  钻地 → 瞬移到玩家旁边 → 钻出，圆形伤害
///   2 boss_bite_small 小口嚼    短前摇，前方【小】范围直线
///   3 boss_bite_big   大口嚼    前方【大】范围直线
///   4 boss_dash       快速冲    前摇【极短】(0.16s)，冲得更远 —— 就是"快慢刀"里的快刀
///   5 boss_sweep      横扫之角  自身周围【中】范围圆形
///   6 boss_slam       撼地掌    自身周围【大】范围圆形
///
/// 另外还有一层【常态碰撞伤害】：犀牛左右移动时只要身体碰到玩家就扣血，
/// 冲撞 / 快速冲 / 掘地钻出同样靠这一层判定，所以它们不需要各自的碰撞体。
///
/// ── 朝向 ──
/// 素材只有朝左的一套。Role.Face 的 setter 会把整个节点旋转 180° 做水平镜像，
/// 所以朝右时引擎会自动翻，技能动画也跟着翻，这里不需要任何手动镜像。
/// </summary>
public partial class RhinoEnemy : Boss
{
    /// <summary>所有技能的伤害。用户要求全部为 1。</summary>
    private const int SkillDamage = 1;

    /// <summary>常态碰撞伤害的冷却，避免贴着犀牛被连续多帧扣血。</summary>
    private const float ContactCooldown = 0.75f;

    /// <summary>常态碰撞判定半径（以双方身体中心算）。</summary>
    private const float ContactRange = 46.0f;

    /// <summary>掘地：钻下去用多久（= 3 帧 @10fps）。</summary>
    private const float BurrowDigTime = 0.30f;

    /// <summary>掘地：钻出前在地下的预警时间（玩家靠这段时间走开）。</summary>
    private const float BurrowWarnTime = 0.60f;

    /// <summary>掘地落地时距离玩家多远（要小于圆形半径，否则永远打不到）。</summary>
    private const float BurrowLandOffset = 58.0f;

    private static readonly Vector2 BossSpriteOffset =
        new(0, -RhinoSpriteFrames.AnchorY + 38);   // 让"脚下中心"落在节点原点上

    private static readonly Vector2 BossHitboxSize = new(88, 56);
    private static readonly Vector2 BossHitboxOffset = new(0, -28);

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
        // 0 冲撞：长前摇，直线，冲出去 150
        new(RhinoSpriteFrames.AnimCharge, 0.62f, 190f, 40f, true, 150f),
        // 1 掘地：单独的协程处理，这里的数值只有 Range 有用
        new(RhinoSpriteFrames.AnimBurrow, BurrowDigTime + BurrowWarnTime,
            72f, 0f, false, 0f),
        // 2 小口嚼：短前摇，前方小范围
        new(RhinoSpriteFrames.AnimBiteSmall, 0.30f, 95f, 42f, true, 0f),
        // 3 大口嚼：前方大范围
        new(RhinoSpriteFrames.AnimBiteBig, 0.44f, 150f, 58f, true, 0f),
        // 4 快速冲：前摇极短（快慢刀），冲得更远
        new(RhinoSpriteFrames.AnimDash, 0.16f, 230f, 36f, true, 190f),
        // 5 横扫之角：自身周围中范围
        new(RhinoSpriteFrames.AnimSweep, 0.38f, 105f, 0f, false, 0f),
        // 6 撼地掌：自身周围大范围
        new(RhinoSpriteFrames.AnimSlam, 0.55f, 160f, 0f, false, 0f),
    };

    private int _skillIndex;
    private bool _attacking;
    private long _coroutine = -1;
    private float _contactTimer;
    private bool _underground;

    public override string BossDisplayName => "犀牛";

    public override void OnInit()
    {
        base.OnInit();

        // Boss.OnInit 是给"程序化绘制"的 boss 用的，把精灵和状态机都关了，
        // 犀牛是精灵动画 boss，要再打开（和 DajuEnemy 一样）。
        AnimatedSprite.Visible = true;
        StateController.Enable = true;

        WeaponPack.SetCapacity(0);
        NoWeaponAttack = true;
        MountLookTarget = true;
        FiringStand = true;
        RoleState.CanPickUpWeapon = false;

        MaxHp = 1200;
        Hp = MaxHp;

        AnimatedSprite.SpriteFrames = RhinoSpriteFrames.Get();
        AnimatedSprite.Offset = BossSpriteOffset;
        AnimatedSprite.Scale = Vector2.One;
        AnimatedSprite.RotationDegrees = 0;   // 朝向由 Role.Face 的节点旋转负责，这里保持 0
        AnimatedSprite.Play(AnimatorNames.Idle);

        ConfigureBossHitbox();
    }

    protected override RoleState OnCreateRoleState()
    {
        var state = base.OnCreateRoleState();
        state.MoveSpeed = 62;
        return state;
    }

    /// <summary>犀牛帧比普通敌人（12x18）大得多，必须换受击框。</summary>
    private void ConfigureBossHitbox()
    {
        if (HurtCollision == null)
        {
            return;
        }
        HurtCollision.Shape = new RectangleShape2D { Size = BossHitboxSize };
        HurtCollision.Position = BossHitboxOffset;
    }

    protected override void Process(float delta)
    {
        base.Process(delta);
        UpdateContactDamage(delta);
    }

    public override void HurtHandler(ActivityObject target, AttackStats attackStats, float f)
    {
        base.HurtHandler(target, attackStats, f);

        if (Hp <= 0 && _coroutine != -1)
        {
            // Boss.HurtHandler 只停它自己那条协程，犀牛这条得自己停，
            // 否则死亡后还会继续放技能。
            StopCoroutine(_coroutine);
            _coroutine = -1;
            _attacking = false;
        }
    }

    /// <summary>
    /// 常态碰撞伤害：身体挨到玩家就扣 1 点，带冷却。
    /// 冲撞 / 快速冲 / 掘地钻出都靠这一层判定，所以不需要各自的碰撞体。
    /// </summary>
    private void UpdateContactDamage(float delta)
    {
        if (IsDie || _underground)
        {
            return;
        }

        if (_contactTimer > 0f)
        {
            _contactTimer -= delta;
            return;
        }

        if (LookTarget is not Role target || target.IsDie || !IsTargetInSameRoom(target))
        {
            return;
        }

        if (target.GetCenterPosition().DistanceTo(GetCenterPosition()) > ContactRange)
        {
            return;
        }

        Hurt(target);
        _contactTimer = ContactCooldown;
    }

    public override void Attack()
    {
        if (_attacking || IsDie)
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

            if (skill.Anim == RhinoSpriteFrames.AnimBurrow)
            {
                yield return RunBurrow(skill, target, dir);
                yield break;
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
                Position = ClampPositionToRoom(Position + dir * skill.Dash);
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

    /// <summary>
    /// 掘地突现：钻下去 → 在地下的那几帧里挪到玩家旁边 → 钻出来 → 圆形伤害。
    /// 土石帧（RhinoSpriteFrames.BurrowUndergroundFrames）画面上本来就没有犀牛，
    /// 所以不用手动隐藏精灵；只需要在这段时间关掉受击框和常态碰撞。
    /// </summary>
    private IEnumerator RunBurrow(SkillDef skill, Role target, Vector2 dir)
    {
        // 1) 钻下去
        yield return new WaitForSeconds(BurrowDigTime);

        // 2) 挪到玩家旁边（挪短一点，保证落在圆形半径之内）
        var dest = ClampPositionToRoom(
            target.GetCenterPosition() - dir * BurrowLandOffset);
        Position = dest;

        _underground = true;
        if (HurtCollision != null)
        {
            HurtCollision.Disabled = true;
        }

        // 3) 地下的这几帧同时充当预警时间，玩家看到脚下的圈可以走开
        ShowWarning(dest, skill, 0f);
        yield return new WaitForSeconds(BurrowWarnTime);

        // 4) 钻出来
        _underground = false;
        if (HurtCollision != null)
        {
            HurtCollision.Disabled = false;
        }

        if (LookTarget is Role t2 && !t2.IsDie && IsTargetInSameRoom(t2) &&
            t2.GetCenterPosition().DistanceTo(dest) <= skill.Range)
        {
            Hurt(t2);
        }

        SpawnEffect(dest, 0f);
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
    /// 画攻击范围预警。直线技能画矩形，圆形技能画圆（isLine=false 且 isSector=false）。
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
            Name = $"BossAttackWarning_Rhino_{_skillIndex}"
        };
        world.YSortLayer.AddChild(warning);
        warning.Configure(position, skill.IsLine, isSector: false,
            skill.Range, skill.HalfWidth, skill.Windup, rotation);
    }

    /// <summary>冲刺只允许在当前房间的实际矩形内移动，避免撞进墙里或跳到别的房间。</summary>
    private Vector2 ClampPositionToRoom(Vector2 target)
    {
        var room = AffiliationArea?.RoomInfo;
        if (room == null)
        {
            return target;
        }

        var roomOrigin = room.GetWorldPosition();
        var roomEnd = roomOrigin + new Vector2(room.GetWidth(), room.GetHeight());
        var padX = BossHitboxSize.X * 0.5f + 4f;
        var padY = BossHitboxSize.Y * 0.5f + 4f;
        return new Vector2(
            Mathf.Clamp(target.X, roomOrigin.X + padX, roomEnd.X - padX),
            Mathf.Clamp(target.Y, roomOrigin.Y + padY, roomEnd.Y - padY));
    }
}
