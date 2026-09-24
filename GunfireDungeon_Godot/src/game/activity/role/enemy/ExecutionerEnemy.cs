using System;
using System.Collections.Generic;
using Config;
using Godot;

/// <summary>
/// 「不死刽子手」—— 精英怪 #2。近战重击 + 召唤幽灵。
///
/// 素材: **Boss: Undead Executioner [FREE]** —— 作者 **Kronovi-**,
/// 页面授权原文: *"you can use it for commercial and non-commercial use, credits are not
/// required ... redistributing and reselling the sprite are restricted."*
/// 见 <c>resource/sprite/role/executioner0001/LICENSE.md</c>。
///
/// 【定位】和邪恶法师正好互补:
///   法师 —— 200 血、站远处放火球、脆;
///   刽子手 —— 300 血、贴上来用大镰刀横扫、慢、还带召唤。
///
/// 【为什么继承 Enemy 而不是 NoWeaponEnemy】
/// NoWeaponEnemy.Process 里有一句 <c>DrawLiquid(...)</c> 在给"徒手小怪"留毒液脚印,
/// 刽子手踩着毒液走不对。继承 <see cref="Enemy"/> 还能白拿血液 + 碎块死亡表现。
///
/// 【为什么它没有武器却打得动人】伤害走的是"空手近战"那条路
/// (<see cref="Role.HandlerCollision"/> 里 activeWeapon == null 的分支),
/// 默认只有 2~3 点 —— 太弱。所以重写了 <see cref="Role.BareMeleeDamage"/> 等三个钩子,
/// 把镰刀做成 7~10 点 / 大击退。
///
/// 【两套攻击】素材给了两张攻击图集, 交替使用:
///   attacking (13 帧)  —— 二连挥砍, 扇形 150° / 半径 46, 近身用
///   skill1    (12 帧)  —— 大范围横扫, 扇形 220° / 半径 62, 压走位用
///
/// 【召唤】素材里那 5 帧的 summon + 幽灵的三张图集, 不是白给的:
/// 召唤间隔到了就先召唤一只「刽子手的幽灵」, 幽灵会追着玩家撞。
/// 这样"站在原地绕圈风筝"就不成立了 —— 这也是它不能移动太快却依然有威胁的原因。
/// </summary>
public partial class ExecutionerEnemy : Enemy
{
    /// <summary>活动/角色 Id</summary>
    public new const string Id = "executioner0001";

    /// <summary>召唤出来的幽灵的活动 Id</summary>
    public const string SpiritId = "spirit0001";

    //─────────────────────────── 近战参数 ───────────────────────────

    /// <summary>二连挥砍的扇形角度(度)</summary>
    private const float SwingAngle = 150f;

    /// <summary>二连挥砍的判定半径(像素) —— 镰刀比刀长得多</summary>
    private const float SwingRadius = 46f;

    /// <summary>大范围横扫的扇形角度(度)</summary>
    private const float SweepAngle = 220f;

    /// <summary>大范围横扫的判定半径(像素)</summary>
    private const float SweepRadius = 62f;

    /// <summary>镰刀伤害(空手路径, 覆盖 Role.BareHandMeleeDamageRange 的 2~3)</summary>
    private static readonly int[] MeleeDamage = { 7, 10 };

    /// <summary>镰刀击退(空手路径)</summary>
    private static readonly float[] MeleeRepel = { 90f, 130f };

    /// <summary>常态碰撞伤害 —— 贴到玩家身上额外扣 3 点</summary>
    public override int ContactDamage => 3;

    /// <summary>碰撞判定半径(刽子手体型大)</summary>
    public override float ContactDamageRange => 40f;

    /// <summary>碰撞伤害冷却</summary>
    public override float ContactDamageCooldown => 0.9f;

    public override int[] BareMeleeDamage => MeleeDamage;

    public override float[] BareMeleeRepel => MeleeRepel;

    public override float BareMeleeRadius => SwingRadius;

    //─────────────────────────── 动作节奏 ───────────────────────────

    /// <summary>二连挥砍总时长(13 帧 @14fps)</summary>
    private const float SwingTotalTime = 13f / 14f;

    /// <summary>二连挥砍第 0.30~0.72 段是"刀在身上"的判定窗口</summary>
    private const float SwingHitBegin = 0.30f;
    private const float SwingHitEnd = 0.72f;

    /// <summary>大横扫总时长(12 帧 @13fps)</summary>
    private const float SweepTotalTime = 12f / 13f;

    private const float SweepHitBegin = 0.28f;
    private const float SweepHitEnd = 0.70f;

    /// <summary>召唤总时长(5 帧 @10fps)</summary>
    private const float SummonTotalTime = 5f / 10f;

    /// <summary>召唤动作播到 45% 时幽灵落地</summary>
    private const float SummonReleaseTime = 0.45f;

    /// <summary>两只手下最多同时存在几只幽灵</summary>
    private const int MaxSpirits = 4;

    /// <summary>召唤冷却(秒)</summary>
    private const float SummonCooldown = 7f;

    /// <summary>开局先等一会再召唤, 别一进门就一堆幽灵</summary>
    private const float SummonFirstDelay = 3.5f;

    //─────────────────────────── 运行时状态 ───────────────────────────

    private enum ActionKind
    {
        None,
        Swing,
        Sweep,
        Summon
    }

    private ActionKind _action = ActionKind.None;

    /// <summary>当前动作剩余时间, &lt;= 0 表示没有动作在进行</summary>
    private float _actionLeft;

    /// <summary>当前动作已经播了多久</summary>
    private float _actionElapsed;

    /// <summary>当前动作是否已经把伤害/召唤放出去了</summary>
    private bool _released;

    /// <summary>下一次是否用大横扫(和挥砍交替)</summary>
    private bool _nextIsSweep;

    /// <summary>距离下一次能召唤还有多久</summary>
    private float _summonCooldown = SummonFirstDelay;

    /// <summary>召唤出来的幽灵(只用来数数量, 会自动剔除已销毁的)</summary>
    private readonly List<SpiritEnemy> _spirits = new();

    /// <summary>起手前 MountLookTarget 的值, 收招要还回去</summary>
    private bool _mountLookBefore = true;

    public override void OnInit()
    {
        base.OnInit();

        //和 NoWeaponEnemy.OnInit 一样: 没有武器也能攻击、攻击时站定、不捡武器
        NoWeaponAttack = true;
        FiringStand = true;
        WeaponPack.SetCapacity(0);
        RoleState.CanPickUpWeapon = false;

        AnimatedSprite.SpriteFrames = ExecutionerSpriteFrames.Get();
        //AiTemplate 里 AnimatedSprite 的位置是给小怪留的, 刽子手要自己重新对齐
        AnimatedSprite.Position = Vector2.Zero;
        AnimatedSprite.Offset = ExecutionerSpriteFrames.SpriteOffset;
        AnimatedSprite.Play(AnimatorNames.Idle);

        //默认的 MeleeAttackAngle 是 120° 且半径是空手的 22 —— 都太小
        MeleeAttackAngle = SwingAngle;
        ApplyMeleeShape(false);
    }

    /// <summary>
    /// 重建近战判定扇形。
    ///
    /// Role.OnChangeActiveItem 只在"切换武器"时算一次形状, 而刽子手永远没有武器,
    /// 所以那次算出来的是空手的 22 半径。这里每次起手都按本次动作重新算一遍。
    /// </summary>
    private void ApplyMeleeShape(bool sweep)
    {
        if (MeleeAttackCollision == null || MeleeAttackArea == null)
        {
            return;
        }

        MeleeAttackAngle = sweep ? SweepAngle : SwingAngle;
        MeleeAttackCollision.Polygon = Utils.CreateSectorPolygon(
            Utils.ConvertAngle(-MeleeAttackAngle / 2f),
            sweep ? SweepRadius : SwingRadius,
            MeleeAttackAngle,
            10
        );
        MeleeAttackArea.CollisionMask = AttackLayer | PhysicsLayer.Bullet;
        MeleeAttackCollision.Disabled = true;
    }

    /// <summary>
    /// AiAttackState.NoWeaponRoleProcess 在 AttackTimer &lt;= 0 时【每帧】调用本函数,
    /// 所以必须靠 <c>_action</c> 防重入 —— 否则一帧就开一次刀。
    /// </summary>
    public override void Attack()
    {
        if (_action != ActionKind.None)
        {
            return;
        }

        if (LookTarget == null)
        {
            AttackTimer = AttackInterval;
            return;
        }

        //召唤优先: 冷却到了而且场上幽灵没满, 就先叫帮手
        if (_summonCooldown <= 0f && CountAliveSpirits() < MaxSpirits)
        {
            StartSummon();
            return;
        }

        //两套攻击交替: 二连挥砍 / 大范围横扫
        if (_nextIsSweep)
        {
            _nextIsSweep = false;
            StartAction(ActionKind.Sweep, SweepTotalTime);
        }
        else
        {
            _nextIsSweep = true;
            StartAction(ActionKind.Swing, SwingTotalTime);
        }
    }

    private void StartAction(ActionKind kind, float totalTime)
    {
        _action = kind;
        _actionLeft = totalTime;
        _actionElapsed = 0f;
        _released = false;
        BasisVelocity = Vector2.Zero;

        //起手就把枪口锁死 —— 否则扇形一直跟着玩家转, 玩家往哪躲都躲不掉。
        //(和 Role.MeleeAttack 里对玩家挥刀的处理一致)
        _mountLookBefore = MountLookTarget;
        MountLookTarget = false;

        ApplyMeleeShape(kind == ActionKind.Sweep);

        if (kind == ActionKind.Swing)
        {
            AnimatedSprite.Play(AnimatorNames.Attack);
        }
        else
        {
            AnimatedSprite.Play(ExecutionerSpriteFrames.AnimSweep);
        }
    }

    private void StartSummon()
    {
        _action = ActionKind.Summon;
        _actionLeft = SummonTotalTime;
        _actionElapsed = 0f;
        _released = false;
        BasisVelocity = Vector2.Zero;

        _mountLookBefore = MountLookTarget;
        MountLookTarget = false;

        AnimatedSprite.Play(ExecutionerSpriteFrames.AnimSummon);
    }

    protected override void Process(float delta)
    {
        base.Process(delta);

        if (IsDie)
        {
            return;
        }

        if (_summonCooldown > 0f)
        {
            _summonCooldown -= delta;
        }

        if (_action == ActionKind.None)
        {
            return;
        }

        _actionLeft -= delta;
        _actionElapsed += delta;

        switch (_action)
        {
            case ActionKind.Swing:
                TickMelee(SwingHitBegin, SwingHitEnd);
                break;
            case ActionKind.Sweep:
                TickMelee(SweepHitBegin, SweepHitEnd);
                break;
            case ActionKind.Summon:
                TickSummon();
                break;
        }

        if (_actionLeft <= 0f && _action != ActionKind.None)
        {
            FinishAction();
        }
    }

    /// <summary>挥砍类动作: 到"挥到位"那一段才开判定框</summary>
    private void TickMelee(float hitBegin, float hitEnd)
    {
        if (_released || MeleeAttackCollision == null)
        {
            return;
        }

        var total = _action == ActionKind.Sweep ? SweepTotalTime : SwingTotalTime;
        if (_actionElapsed < total * hitBegin)
        {
            return;
        }

        _released = true;

        //从"挥到位"一直开到收刀 —— 由 Role 的保底计时器自动关闭
        var window = Mathf.Max(0.12f, total * (hitEnd - hitBegin));
        BeginMeleeHitWindow(window);

        PlaySlashEffect();
    }

    private void TickSummon()
    {
        if (_released || _actionElapsed < SummonTotalTime * SummonReleaseTime)
        {
            return;
        }

        _released = true;
        SummonSpirits();
    }

    private void FinishAction()
    {
        var finished = _action;
        _action = ActionKind.None;
        _actionLeft = 0f;
        _released = false;

        AnimatedSprite.Play(AnimatorNames.Idle);
        if (MeleeAttackCollision != null)
        {
            MeleeAttackCollision.Disabled = true;
        }

        //枪口恢复跟踪
        MountLookTarget = _mountLookBefore;

        if (finished == ActionKind.Summon)
        {
            _summonCooldown = SummonCooldown;
        }

        //把 AttackTimer 设上, AiAttackState 下一帧就会判定"攻击结束"并切回追击状态
        AttackTimer = AttackInterval;
    }

    //─────────────────────────── 召唤 ───────────────────────────

    /// <summary>数一下场上还活着几只自己召唤的幽灵(顺手剔除已销毁的)</summary>
    private int CountAliveSpirits()
    {
        _spirits.RemoveAll(s => !IsInstanceValid(s) || s.IsDestroyed);
        return _spirits.Count;
    }

    /// <summary>在身体周围召出 1~2 只幽灵</summary>
    private void SummonSpirits()
    {
        var count = Hp < MaxHp * 0.5f ? 2 : 1;

        for (var i = 0; i < count; i++)
        {
            var spirit = Create<SpiritEnemy>(SpiritId);
            if (spirit == null)
            {
                continue;
            }

            //在自己周围随机方位落地, 不要叠在同一个点上
            var angle = Utils.Random.RandomRangeFloat(0f, Mathf.Pi * 2f);
            var pos = Position + new Vector2(26f, 0f).Rotated(angle);
            spirit.PutDown(pos, RoomLayerEnum.YSortLayer);
            _spirits.Add(spirit);
        }
    }

    /// <summary>挥砍瞬间的斩击特效(复用近战攻击特效)</summary>
    private void PlaySlashEffect()
    {
        var effect = ObjectManager.GetPoolItem<IEffect>(ResourcePath.prefab_effect_weapon_MeleeAttack1_tscn);
        if (effect == null)
        {
            return;
        }

        var node = (Node2D)effect;
        node.GlobalPosition = MountPoint.GlobalPosition;
        node.Rotation = MountPoint.GlobalRotation;
        node.AddToActivityRoot(RoomLayerEnum.YSortLayer);
        effect.PlayEffect();

        GameCamera.Main?.DirectionalShake(Vector2.Right.Rotated(MountPoint.GlobalRotation) * 4f);
    }
}
