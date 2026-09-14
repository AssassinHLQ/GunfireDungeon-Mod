using System.Collections;
using System.Collections.Generic;
using Config;
using DsUi;      // 项目自定义的 WaitForSeconds 在这个命名空间里（不是 Godot 自带的）
using Godot;

/// <summary>
/// 大橘 —— 会放四个技能的猫 BOSS。
///
/// 继承 <see cref="Boss"/> 而不是 NoWeaponEnemy，原因有两个（都是编译器和运行时逼出来的）：
///   · BossDisplayName 和 Weight 定义在 Boss/AiRole 这条继承链上，NoWeaponEnemy:Enemy 没有
///   · GameNotificationOverlay 是靠 `is Boss` 找 BOSS 血条的主角的，不继承就找不到
///
/// 但 Boss.OnInit 是给它自己的程序化绘制 boss 用的（隐藏精灵 + 关掉状态机），
/// 大橘是精灵动画 boss，所以在 override 里要把这两项再打开。
///
/// 四个技能（参数取自 4.2 的 BelialEnemy/Hakimi 分支，未改动）：
///   0 boss_claw_combo  直线 172 / 半宽 38 / 起手 0.42s / 冲刺 96   伤害 6+层数
///   1 boss_leap_slam   圆形 128 / 起手 0.54s / 冲刺 48            伤害 6+层数
///   2 boss_fire_wave   直线 285 / 半宽 46 / 起手 0.72s            伤害 6+层数
///   3 boss_rage_roar   圆形  92 / 起手 0.82s / 锁定玩家位置        伤害 9+层数
///
/// 技能释放前会在地面画出【攻击范围预警】(BossAttackWarning)，玩家有时间走位躲开。
/// </summary>
public partial class DajuEnemy : Boss
{
    /// <summary>贴身抓挠的距离（近身普通攻击，不带预警）</summary>
    private const float ClawRange = 105.0f;
    private const float ClawHalfHeight = 44.0f;
    private const int BaseClawDamage = 2;

    /// <summary>Boss 帧 128x128，再放大 1.35 倍</summary>
    private static readonly Vector2 BossSpriteScale = new(1.35f, 1.35f);
    private static readonly Vector2 BossSpriteOffset = new(0, -73);

    private const float PhaseTwoRatio = 0.5f;

    private int _bossAttackIndex;
    private bool _bossAttackPending;
    private int _phase = 1;
    private long _dajuCoroutine = -1;

    public override string BossDisplayName => _phase == 2 ? "大橘 · 狂暴" : "大橘";

    public override void OnInit()
    {
        base.OnInit();

        // Boss.OnInit 把这些关掉了（它是给无精灵的程序化 boss 用的），这里要打开
        AnimatedSprite.Visible = true;
        StateController.Enable = true;
        StateController.Enable = true;

        WeaponPack.SetCapacity(0);
        NoWeaponAttack = true;
        MountLookTarget = true;
        FiringStand = true;
        RoleState.CanPickUpWeapon = false;

        MaxHp = 1200;
        Hp = MaxHp;
        _phase = 1;

        AnimatedSprite.SpriteFrames = DajuSpriteFrames.Get();
        AnimatedSprite.Offset = BossSpriteOffset;
        AnimatedSprite.Scale = BossSpriteScale;
        AnimatedSprite.RotationDegrees = 0;
        AnimatedSprite.Play(AnimatorNames.Idle);

        ConfigureBossHitbox();
    }

    protected override RoleState OnCreateRoleState()
    {
        var state = base.OnCreateRoleState();
        state.MoveSpeed = 70;     // 猫 boss 比默认的 45 灵活
        return state;
    }

    /// <summary>
    /// Boss 帧远比普通敌人（12x18）大，必须换受击框，
    /// 否则打头/打尾巴会直接穿过去，看起来像完全打不动。
    /// </summary>
    private void ConfigureBossHitbox()
    {
        if (HurtCollision == null)
        {
            return;
        }
        HurtCollision.Shape = new RectangleShape2D { Size = new Vector2(164, 158) };
        HurtCollision.Position = new Vector2(0, -73);
    }

    public override void HurtHandler(ActivityObject target, AttackStats attackStats, float f)
    {
        base.HurtHandler(target, attackStats, f);

        if (Hp <= 0)
        {
            // Boss.HurtHandler 只停它自己那条协程（private 字段），
            // 大橘这条得自己停，否则死亡后还会继续放技能。
            if (_dajuCoroutine != -1)
            {
                StopCoroutine(_dajuCoroutine);
                _dajuCoroutine = -1;
            }
            return;
        }

        if (_phase == 1 && Hp <= MaxHp * PhaseTwoRatio && !IsDie)
        {
            EnterPhaseTwo();
        }
    }

    private void EnterPhaseTwo()
    {
        _phase = 2;
        RoleState.MoveSpeed *= 1.2f;
        SpawnAttackEffect(GetCenterPosition(), 0);
    }

    /// <summary>
    /// 贴身抓挠。Boss 这条继承链没有 OnAttack()（那是 NoWeaponEnemy 的），
    /// 所以做成私有方法，在每次放技能前先判定一次 —— 玩家贴脸时会先吃一爪。
    /// </summary>
    private void TryClaw()
    {
        if (LookTarget is not Role target || target.IsDie)
        {
            return;
        }

        var delta = target.GetCenterPosition() - GetCenterPosition();
        var facing = (Face == FaceDirection.Right && delta.X >= 0) ||
                     (Face == FaceDirection.Left && delta.X <= 0);
        if (!facing || delta.Length() > ClawRange ||
            Mathf.Abs(delta.Y) > ClawHalfHeight)
        {
            return;
        }

        target.HurtArea.Hurt(this,
            new List<AttackStats> { new(BaseClawDamage + FloorBonus(), DamageType.Physical) },
            null, delta.Angle());
        SpawnAttackEffect(target.GetCenterPosition(), delta.Angle());
    }

    public override void Attack()
    {
        if (_bossAttackPending || IsDie)
        {
            return;
        }
        _bossAttackPending = true;
        _dajuCoroutine = StartCoroutine(RunBossAttack());
    }

    private int FloorBonus()
    {
        var floor = GameApplication.Instance?.DungeonManager?.CurrentFloor ?? 1;
        return Mathf.Max(0, floor - 1);
    }

    private IEnumerator RunBossAttack()
    {
        // ActivityObject._Process 会先跑本类 Process 设 boss_* 攻击帧，
        // 再跑 StateController 的状态 Process，后者用 DoIdle()/DoMove()
        // 把攻击动画整段覆盖掉 —— 这是"攻击动画看起来不播放"的根因。
        // 所以要临时禁用状态机，结束后恢复。
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

            var attack = _bossAttackIndex % 4;
            _bossAttackIndex++;

            // 贴脸时先补一爪
            TryClaw();

            var animation = attack switch
            {
                0 => new StringName("boss_claw_combo"),
                1 => new StringName("boss_leap_slam"),
                2 => new StringName("boss_fire_wave"),
                _ => new StringName("boss_rage_roar")
            };

            ConfigureAttack(attack, out var range, out var halfWidth,
                out var windup, out var damage, out var isLine,
                out var targetCentered, out var dashDistance);

            AnimatedSprite.Play(animation);
            AttackTimer = Mathf.Max(1.15f, GetAnimationDuration(animation) + 0.25f);
            BasisVelocity = Vector2.Zero;

            var attackStart = GetCenterPosition();
            var locked = target.GetCenterPosition();
            var dir = (locked - attackStart).Normalized();
            if (dir.IsZeroApprox())
            {
                dir = Face == FaceDirection.Left ? Vector2.Left : Vector2.Right;
            }

            var origin = targetCentered
                ? locked
                : (!isLine && dashDistance > 0
                    ? attackStart + dir * dashDistance
                    : attackStart);

            // 先画攻击范围预警，给玩家反应时间
            ShowAttackWarning(origin, isLine, range, halfWidth, windup, dir.Angle());

            yield return new WaitForSeconds(windup);

            if (LookTarget is not Role t2 || t2.IsDie || !IsTargetInSameRoom(t2))
            {
                yield break;
            }

            if (dashDistance > 0)
            {
                Position = ClampPositionToRoom(Position + dir * dashDistance);
            }

            var targetPos = t2.GetCenterPosition();
            var hit = isLine
                ? IsInsideLineAttack(targetPos, origin, dir, range, halfWidth)
                : targetPos.DistanceTo(origin) <= range;

            if (hit)
            {
                t2.HurtArea.Hurt(this,
                    new List<AttackStats> { new(damage + FloorBonus(), DamageType.Physical) },
                    null, (targetPos - GetCenterPosition()).Angle());
            }

            SpawnAttackEffect(
                targetCentered ? origin : (isLine ? attackStart : GetCenterPosition()),
                dir.Angle());
        }
        finally
        {
            // 必须加 !IsDie：boss 在攻击中被击杀时 Role.HurtHandler 已经把
            // StateController.Enable 设成 false，这里再恢复会让尸体继续被 AI 推动，
            // 而且 DungeonManager 会误判房间还有活敌，门永远打不开。
            if (restore && StateController != null && !IsDie)
            {
                StateController.Enable = true;
            }
            _bossAttackPending = false;
        }
    }

    /// <summary>大橘四个技能的参数。数值取自 4.2 原版，未改动。</summary>
    private void ConfigureAttack(
        int attack,
        out float range, out float halfWidth, out float windup,
        out int damage, out bool isLine, out bool targetCentered,
        out float dashDistance)
    {
        isLine = false;
        targetCentered = false;
        dashDistance = 0;
        halfWidth = 44;
        range = 100;
        windup = 0.5f;
        damage = 6;

        switch (attack)
        {
            case 0:
                range = 172; halfWidth = 38; windup = 0.42f;
                damage = 6; isLine = true; dashDistance = 96;
                break;
            case 1:
                range = 128; windup = 0.54f;
                damage = 6; dashDistance = 48;
                break;
            case 2:
                range = 285; halfWidth = 46; windup = 0.72f;
                damage = 6; isLine = true;
                break;
            default:
                range = 92; windup = 0.82f;
                damage = 9; targetCentered = true;
                break;
        }

        // 二阶段：起手缩短、范围略增、伤害提高
        if (_phase == 2)
        {
            windup *= 0.85f;
            range *= 1.1f;
            damage += 2;
        }
    }

    private float GetAnimationDuration(StringName animation)
    {
        var frames = AnimatedSprite.SpriteFrames;
        if (frames == null || !frames.HasAnimation(animation))
        {
            return 1.05f;
        }
        var speed = Mathf.Max(0.01f, frames.GetAnimationSpeed(animation));
        return (float)(frames.GetFrameCount(animation) / speed) + 0.05f;
    }

    private static bool IsInsideLineAttack(
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

    private void SpawnAttackEffect(Vector2 position, float rotation)
    {
        var effect = ObjectManager.GetPoolItem<IEffect>(
            ResourcePath.prefab_effect_weapon_ShotFire0003_tscn);
        var node = (Node2D)effect;
        node.GlobalPosition = position;
        node.Rotation = rotation;
        node.AddToActivityRoot(RoomLayerEnum.YSortLayer);
        effect.PlayEffect();
    }

    private void ShowAttackWarning(
        Vector2 position, bool isLine,
        float range, float halfWidth, float duration, float rotation)
    {
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (world?.YSortLayer == null)
        {
            return;
        }

        var warning = new BossAttackWarning
        {
            Name = $"BossAttackWarning_Daju_{_bossAttackIndex}"
        };
        world.YSortLayer.AddChild(warning);
        warning.Configure(position, isLine, isSector: false, range, halfWidth,
            duration, rotation);
    }

    /// <summary>冲刺可能把 Boss 推出房间导致穿墙，用房间内壁钳制。</summary>
    private Vector2 ClampPositionToRoom(Vector2 target)
    {
        var room = AffiliationArea?.RoomInfo;
        var world = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (room == null || world == null)
        {
            return target;
        }

        var used = world.GetUsedRect();
        const float pad = 24f;
        return new Vector2(
            Mathf.Clamp(target.X, used.Position.X + pad, used.End.X - pad),
            Mathf.Clamp(target.Y, used.Position.Y + pad, used.End.Y - pad));
    }
}
