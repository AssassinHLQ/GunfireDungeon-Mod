
using System;
using System.Collections.Generic;
using System.Linq;
using AiState;
using Godot;

/// <summary>
/// Ai角色
/// </summary>
public abstract partial class AiRole : Role
{
    /// <summary>
    /// 目标是否在视野内, 视野内不能有墙壁遮挡
    /// </summary>
    public bool TargetInView { get; private set; } = false;

    /// <summary>
    /// 目标间是否有墙壁遮挡
    /// </summary>
    public bool TargetHasOcclusion { get; private set; } = false;
    
    /// <summary>
    /// 目标是否在视野范围内, 不会考虑是否被枪遮挡
    /// </summary>
    public bool TargetInViewRange { get; private set; } = false;
    
    /// <summary>
    /// 敌人身上的状态机控制器
    /// </summary>
    public StateController<AiRole, AIStateEnum> StateController { get; private set; }
    
    /// <summary>
    /// 视野检测射线, 朝玩家打射线, 检测是否碰到墙
    /// </summary>
    [Export, ExportFillNode]
    public RayCast2D ViewRay { get; set; }

    /// <summary>
    /// 导航代理
    /// </summary>
    [Export, ExportFillNode]
    public NavigationAgent2D NavigationAgent2D { get; set; }

    /// <summary>
    /// 导航代理中点
    /// </summary>
    [Export, ExportFillNode]
    public Marker2D NavigationPoint { get; set; }

    /// <summary>
    /// 不通过武发射子弹的开火点
    /// </summary>
    [Export, ExportFillNode]
    public Marker2D FirePoint { get; set; }
    
    /// <summary>
    /// 当前敌人所看向的对象, 也就是枪口指向的对象
    /// </summary>
    public ActivityObject LookTarget { get; set; }

    /// <summary>
    /// 攻击锁定目标时间
    /// </summary>
    public float LockingTime { get; set; } = 1f;
    
    /// <summary>
    /// 锁定目标已经走过的时间
    /// </summary>
    public float LockTargetTime { get; set; } = 0;

    /// <summary>
    /// 开火时是否站立不动
    /// </summary>
    public bool FiringStand { get; set; } = false;

    /// <summary>
    /// 视野半径, 单位像素, 发现玩家后改视野范围可以穿墙
    /// </summary>
    public float ViewRange { get; set; } = -1;

    /// <summary>
    /// 默认视野半径
    /// </summary>
    public float DefaultViewRange { get; set; } = 250;

    /// <summary>
    /// 发现玩家后跟随玩家的视野半径
    /// </summary>
    public float TailAfterViewRange { get; set; } = 400;
    
    /// <summary>
    /// 视野角度, 角度制
    /// </summary>
    public float ViewAngleRange { get; set; } = 150;
    
    /// <summary>
    /// 攻击间隔时间, 秒
    /// </summary>
    public float AttackInterval { get; set; } = 0;

    /// <summary>
    /// 当前Ai是否有攻击欲望
    /// </summary>
    public bool HasAttackDesire { get; private set; } = true;
    
    /// <summary>
    /// 是否有移动欲望, 仅在 AiNormal 状态下有效, 其他状态都可以移动
    /// </summary>
    public bool HasMoveDesire { get; private set; } = true;

    //─────────────────────────── 登场静止 ───────────────────────────

    /// <summary>
    /// 登场静止时长(秒): 进 Boss 房后先站着不动, 等 Boss 曲进入高潮再开打。
    ///
    /// 用来配合"前奏很长"的 Boss 曲 —— 例如死神配《东风》, 前 23 秒是舒缓段,
    /// Boss 就静立不动, 到节奏起来的那一刻才开始攻击。
    /// 子类覆盖它即可(默认 0 = 不静止, 不影响现有 Boss)。
    /// </summary>
    public virtual float DormantTime => 0f;

    /// <summary>
    /// 登场静止期间的减伤百分比(0~1), 默认 50%。
    ///
    /// 静止的十几二十秒里 Boss 完全不动, 不给点减伤玩家可以站着把血打空。
    /// 0 = 不减伤, 1 = 无敌。
    /// </summary>
    public virtual float DormantReducePct => 0.5f;

    /// <summary>是否正处在登场静止期(静止期间不移动也不攻击)</summary>
    public bool IsDormant => _dormantActive;

    //-1 = 还没开始计时(玩家还没进房间)
    private float _dormantLeft = -1f;
    private bool _dormantActive;
    //已经静止过一轮了, 不再进入静止(没有这个标记的话 _dormantLeft 会停在负数, 计时会无限重启)
    private bool _dormantDone;

    //─────────────────────────── 身体碰撞伤害 ───────────────────────────

    /// <summary>
    /// 身体贴到目标时造成的伤害。0 = 不造成碰撞伤害(默认, 不影响原来任何敌人)。
    /// 打开它的子类见 <see cref="Enemy.ContactDamage"/> 和两个 Boss。
    /// </summary>
    public virtual int ContactDamage => 0;

    /// <summary>碰撞伤害的判定距离(以双方身体中心算)。</summary>
    public virtual float ContactDamageRange => 34f;

    /// <summary>碰撞伤害的冷却(秒), 避免贴身时每帧都扣血。</summary>
    public virtual float ContactDamageCooldown => 0.75f;

    private float _contactDamageTimer;

    public override void OnInit()
    {
        base.OnInit();
        
        StateController = AddComponent<StateController<AiRole, AIStateEnum>>();
        
        //注册Ai状态机
        StateController.Register(new AiNormalState());
        StateController.Register(new AiTailAfterState());
        StateController.Register(new AiFollowUpState());
        StateController.Register(new AiSurroundState());
        StateController.Register(new AiFindAmmoState());
        StateController.Register(new AiAttackState());
        
        //默认状态
        StateController.ChangeStateInstant(AIStateEnum.AiNormal);

        //NavigationAgent2D.VelocityComputed += OnVelocityComputed;
    }

    protected override void Process(float delta)
    {
        base.Process(delta);

        UpdateDormant(delta);
        ProcessContactDamage(delta);

        if (LookTarget != null)
        {
            if (LookTarget.IsDestroyed)
            {
                LookTarget = null;
                TargetInViewRange = false;
                TargetHasOcclusion = false;
                TargetInView = false;
            }
            else
            {
                //判断目标是否被墙壁遮挡
                TargetHasOcclusion = TestViewRayCast(LookTarget.GetCenterPosition());
                TestViewRayCastOver();

                TargetInViewRange = true;
                TargetInView = !TargetHasOcclusion;
            }
        }

        //更新视野范围
        ViewRange = StateController.CurrState == AIStateEnum.AiNormal ? DefaultViewRange : TailAfterViewRange;
    }

    /// <summary>
    /// 登场静止处理。
    ///
    /// 【为什么不在 OnInit 就开始计时】Boss 可能被房间的预加载提前创建出来,
    /// 那样玩家还在走廊上走, 静止时间就被耗光了, 等他进门时 Boss 已经能动了。
    /// 所以先用 CalcAttackTarget() 确认"房间里已经有敌人(玩家)"才开始计时 ——
    /// 也就是玩家真的进门那一刻。
    ///
    /// 静止期间的做法: 直接关掉 AI 状态机(StateController.Enable = false),
    /// 并把速度和外力清零, 保证它是一尊雕像。
    /// ⚠️ 只关"攻击欲望/移动欲望"是拦不住的, 原因见方法里的注释。
    /// </summary>
    private void UpdateDormant(float delta)
    {
        if (DormantTime <= 0f)
        {
            return;
        }

        if (!_dormantActive && !_dormantDone && CalcAttackTarget() != null)
        {
            _dormantActive = true;
            _dormantLeft = DormantTime;
            //静止期间给临时减伤(默认 50%)
            RoleState.ExtraReducePct = DormantReducePct;
        }

        if (!_dormantActive)
        {
            return;
        }

        //静止期里被击杀(1200 血 + 50% 减伤, 玩家够狠是能做到的): 这里必须收手,
        //否则下面每帧一次 Play(idle) 会把死亡动画覆盖掉, 尸体就定在 idle 上。
        if (IsDie)
        {
            return;
        }

        _dormantLeft -= delta;

        // ── 静止期必须"真的动不了" ──
        // 光把攻击欲望/移动欲望关掉是拦不住的:
        //   1) AiNormalState.Process 第一句就是 `LookTarget != null → AiTailAfter`, 根本不看这两个开关;
        //   2) Boss.Process 每帧又把 LookTarget 强制设成玩家, 于是那一句永远成立;
        //   3) AiTailAfter / AiFollowUp / AiSurround 也都不看开关, 只要目标还在就 DoMove()。
        // 结果 Boss 会在静止期里一帧一帧往玩家那边挪 —— 玩家看到的就是"站着不动, 却一直在平移"。
        // 另外中弹会通过 AddRepelForce 给一点击退力, 站着挨打也会被慢慢推走, 一并清掉。
        if (StateController != null && StateController.Enable)
        {
            StateController.Enable = false;
        }
        SetAttackDesire(false);
        SetMoveDesire(false);
        BasisVelocity = Vector2.Zero;
        MoveController?.ClearForce();
        if (AnimatedSprite != null && AnimatedSprite.SpriteFrames != null &&
            AnimatedSprite.SpriteFrames.HasAnimation(AnimatorNames.Idle))
        {
            AnimatedSprite.Play(AnimatorNames.Idle);
        }

        if (_dormantLeft <= 0f)
        {
            _dormantActive = false;
            //【必须记下"已经醒过"】以前只把 _dormantActive 置 false, _dormantLeft 会停在 -0.0x,
            // 下一帧开头的 `_dormantLeft < 0f` 又成立 —— 21.1 秒从头再计时, 永远醒不过来:
            // Boss 全程站着不动、一个技能都不放, 只会一点点平移(就是实测到的那个问题)。
            _dormantDone = true;
            _dormantLeft = 0f;
            SetAttackDesire(true);
            SetMoveDesire(true);
            //静止结束, 撤掉临时减伤
            RoleState.ExtraReducePct = 0f;
            //把 AI 状态机交还回去
            if (StateController != null && !IsDie)
            {
                StateController.Enable = true;
                StateController.ChangeStateInstant(AIStateEnum.AiNormal);
            }
        }
    }

    /// <summary>
    /// 身体碰撞伤害: 目标贴到身上就扣 <see cref="ContactDamage"/> 点血, 带冷却。
    ///
    /// 【为什么单独一层】原来只有两只 Boss(犀牛/死神)各自抄了一份这个逻辑, 普通小怪撞上来
    /// 一点伤害都没有 —— 玩家可以拿小怪当垫脚石站着不动。现在统一放在 AiRole:
    /// 默认关闭(ContactDamage = 0), 谁需要谁覆盖(Enemy 全部小怪覆盖成 1)。
    ///
    /// 目标优先取 AI 当前锁定的目标, 没锁定就退回玩家自己 —— 小怪还没"发现"玩家时撞上也照样算。
    /// </summary>
    protected virtual void ProcessContactDamage(float delta)
    {
        if (ContactDamage <= 0 || IsDie || IsDormant)
        {
            return;
        }

        if (_contactDamageTimer > 0f)
        {
            _contactDamageTimer -= delta;
            return;
        }

        var target = LookTarget as Role;
        if (target == null || target.IsDestroyed)
        {
            target = World?.Player;
        }

        if (target == null || target.IsDie || !IsEnemy(target))
        {
            return;
        }

        //只在同一个房间里生效(隔壁房间坐标可能刚好重叠)
        if (AffiliationArea == null || target.AffiliationArea != AffiliationArea)
        {
            return;
        }

        var self = GetCenterPosition();
        var pos = target.GetCenterPosition();
        if (pos.DistanceTo(self) > ContactDamageRange)
        {
            return;
        }

        target.HurtArea.Hurt(this,
            new List<AttackStats> { new(ContactDamage, DamageType.Physical) },
            null, (pos - self).Angle());
        _contactDamageTimer = ContactDamageCooldown;
    }

    /// <summary>
    /// 计算攻击目标
    /// </summary>
    public Role CalcAttackTarget()
    {
        var enemyItems = AffiliationArea?.FindEnterItems(
            o => o is Role role && !role.IsDestroyed && IsEnemy(role));
        if (enemyItems == null || enemyItems.Length == 0) return null;

        try
        {
            var pos = Position;
            //排序距离升序
            var enumerable = enemyItems
                .Select(i => new KeyValuePair<Role, float>((Role)i, i.Position.DistanceSquaredTo(pos)))
                .OrderBy(i => i.Value).ToArray();
            foreach (var pair in enumerable)
            {
                if (TestViewRayCast(pair.Key.GetCenterPosition()))
                {
                    return pair.Key;
                }
            }

            return enumerable.First().Key;
        }
        finally
        {
            TestViewRayCastOver();
        }
    }
    
    /// <summary>
    /// 返回地上的武器是否有可以拾取的, 也包含没有被其他敌人标记的武器
    /// </summary>
    public bool CheckUsableWeaponInUnclaimed()
    {
        foreach (var unclaimedWeapon in World.Weapon_UnclaimedList)
        {
            //判断是否能拾起武器, 条件: 相同的房间
            if (unclaimedWeapon.AffiliationArea == AffiliationArea)
            {
                if (!unclaimedWeapon.IsTotalAmmoEmpty())
                {
                    if (!unclaimedWeapon.HasSign(SignNames.AiFindWeaponSign))
                    {
                        return true;
                    }
                    else
                    {
                        //判断是否可以移除该标记
                        var enemy = unclaimedWeapon.GetSign<Enemy>(SignNames.AiFindWeaponSign);
                        if (enemy == null || enemy.IsDestroyed) //标记当前武器的敌人已经被销毁
                        {
                            unclaimedWeapon.RemoveSign(SignNames.AiFindWeaponSign);
                            return true;
                        }
                        else if (!enemy.IsAllWeaponTotalAmmoEmpty()) //标记当前武器的敌人已经有新的武器了
                        {
                            unclaimedWeapon.RemoveSign(SignNames.AiFindWeaponSign);
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
    
    /// <summary>
    /// 寻找可用的武器
    /// </summary>
    public Weapon FindTargetWeapon()
    {
        Weapon target = null;
        var position = Position;
        foreach (var weapon in World.Weapon_UnclaimedList)
        {
            //判断是否能拾起武器, 条件: 相同的房间, 或者当前房间目前没有战斗, 或者不在战斗房间
            if (weapon.AffiliationArea == AffiliationArea)
            {
                //还有弹药
                if (!weapon.IsTotalAmmoEmpty())
                {
                    //查询是否有其他敌人标记要拾起该武器
                    if (weapon.HasSign(SignNames.AiFindWeaponSign))
                    {
                        var enemy = weapon.GetSign<Enemy>(SignNames.AiFindWeaponSign);
                        if (enemy == this) //就是自己标记的
                        {

                        }
                        else if (enemy == null || enemy.IsDestroyed) //标记当前武器的敌人已经被销毁
                        {
                            weapon.RemoveSign(SignNames.AiFindWeaponSign);
                        }
                        else if (!enemy.IsAllWeaponTotalAmmoEmpty()) //标记当前武器的敌人已经有新的武器了
                        {
                            weapon.RemoveSign(SignNames.AiFindWeaponSign);
                        }
                        else //放弃这把武器
                        {
                            continue;
                        }
                    }

                    if (target == null) //第一把武器
                    {
                        target = weapon;
                    }
                    else if (target.Position.DistanceSquaredTo(position) >
                             weapon.Position.DistanceSquaredTo(position)) //距离更近
                    {
                        target = weapon;
                    }
                }
            }
        }

        return target;
    }

    /// <summary>
    /// 调用视野检测, 如果被墙壁和其它物体遮挡, 则返回true
    /// </summary>
    public bool TestViewRayCast(Vector2 target)
    {
        ViewRay.Enabled = true;
        ViewRay.TargetPosition = ViewRay.ToLocal(target);
        ViewRay.ForceRaycastUpdate();
        return ViewRay.IsColliding();
    }

    /// <summary>
    /// 调用视野检测完毕后, 需要调用 TestViewRayCastOver() 来关闭视野检测射线
    /// </summary>
    public void TestViewRayCastOver()
    {
        ViewRay.Enabled = false;
    }

    /// <summary>
    /// AI 拾起武器操作
    /// </summary>
    public void DoPickUpWeapon()
    {
        //这几个状态不需要主动拾起武器操作
        var state = StateController.CurrState;
        if (state == AIStateEnum.AiNormal || state == AIStateEnum.AiAttack)
        {
            return;
        }
        
        //拾起地上的武器
        foreach (var interactive in InteractiveItemList)
        {
            if (interactive is Weapon weapon)
            {
                if (WeaponPack.ActiveItem == null) //手上没有武器, 无论如何也要拾起
                {
                    TriggerInteractive();
                    return;
                }

                //没弹药了
                if (weapon.IsTotalAmmoEmpty())
                {
                    return;
                }

                // var index2 = Holster.FindWeapon((we, i) =>
                //     we.Attribute.WeightType == weapon.Attribute.WeightType && we.IsTotalAmmoEmpty());
                var index2 = WeaponPack.FindIndex((we, i) => we.IsTotalAmmoEmpty());
                if (index2 != -1) //扔掉没子弹的武器
                {
                    ThrowWeapon(index2);
                    TriggerInteractive();
                    return;
                }
            
                // if (Holster.HasVacancy()) //有空位, 拾起武器
                // {
                //     TriggerInteractive();
                //     return;
                // }
            }
        }
        
    }
    
    /// <summary>
    /// 获取锁定目标的剩余时间
    /// </summary>
    public float GetLockRemainderTime()
    {
        var weapon = WeaponPack.ActiveItem;
        if (weapon == null)
        {
            return LockingTime - LockTargetTime;
        }
        return weapon.Attribute.AiAttackAttr.LockingTime - LockTargetTime;
    }

    public override void LookTargetPosition(Vector2 pos)
    {
        LookTarget = null;
        base.LookTargetPosition(pos);
    }
    
    /// <summary>
    /// 执行移动操作
    /// </summary>
    public void DoMove()
    {
        // //计算移动
        // NavigationAgent2D.MaxSpeed = EnemyRoleState.MoveSpeed;
        // var nextPos = NavigationAgent2D.GetNextPathPosition();
        // NavigationAgent2D.Velocity = (nextPos - Position - NavigationPoint.Position).Normalized() * RoleState.MoveSpeed;
        
        AnimatedSprite.Play(AnimatorNames.Run);
        //计算移动
        var nextPos = NavigationAgent2D.GetNextPathPosition();
        BasisVelocity = (nextPos - Position - NavigationPoint.Position).Normalized() * RoleState.MoveSpeed;
    }

    /// <summary>
    /// 执行站立操作
    /// </summary>
    public void DoIdle()
    {
        AnimatedSprite.Play(AnimatorNames.Idle);
        BasisVelocity = Vector2.Zero;
    }
        
    /// <summary>
    /// 更新房间中标记的目标位置
    /// </summary>
    public void UpdateMarkTargetPosition()
    {
        if (LookTarget != null && AffiliationArea != null)
        {
            AffiliationArea.RoomInfo.MarkTargetPosition[LookTarget.Id] = LookTarget.Position;
        }
    }

    protected override void OnDie()
    {
        //扔掉所有武器
        ThrowAllWeapon();
        //创建金币
        Gold.CreateGold(Position, RoleState.Gold);
        //移出场景，但是不销毁
        GetParent().RemoveChild(this);
    }

    /// <summary>
    /// 设置Ai是否有攻击欲望
    /// </summary>
    public void SetAttackDesire(bool v)
    {
        if (v != HasAttackDesire)
        {
            HasAttackDesire = v;
            StateController.ChangeState(AIStateEnum.AiNormal);
        }
    }

    /// <summary>
    /// 设置Ai是否有移动欲望
    /// </summary>
    public void SetMoveDesire(bool v)
    {
        HasMoveDesire = v;
    }

    /// <summary>
    /// 更新玩家脸的朝向
    /// </summary>
    public void UpdateFace()
    {
        //看向目标
        if (LookTarget != null && MountLookTarget)
        {
            var pos = LookTarget.Position;
            LookPosition = pos;
            //脸的朝向
            var gPos = Position;
            if (pos.X > gPos.X && Face == FaceDirection.Left)
            {
                Face = FaceDirection.Right;
            }
            else if (pos.X < gPos.X && Face == FaceDirection.Right)
            {
                Face = FaceDirection.Left;
            }

            //枪口跟随目标
            MountPoint.SetLookAt(pos);
        }
    }
    
    protected override void OnHit(ActivityObject target, DamageCalcResult damageCalcResult, float angle)
    {
        //受到伤害
        var state = StateController.CurrState;
        if (state == AIStateEnum.AiNormal)
        {
            if (target is Role role)
            {
                //进入跟随状态
                StateController.ChangeState(AIStateEnum.AiTailAfter, role);
            }
        }
        else if (state == AIStateEnum.AiFindAmmo)
        {
            if (LookTarget == null)
            {
                if (target is Role role)
                {
                    LookTarget = target;
                }
                var findAmmo = (AiFindAmmoState)StateController.CurrStateBase;
                StateController.ChangeState(AIStateEnum.AiFindAmmo, findAmmo.TargetWeapon);
            }
        }
        else if (TargetHasOcclusion || !TargetInView)
        {
            if (target is Role role)
            {
                LookTarget = target;
            }
        }
    }
    
    // private void OnVelocityComputed(Vector2 velocity)
    // {
    //     if (Mathf.Abs(velocity.X) >= 0.01f && Mathf.Abs(velocity.Y) >= 0.01f)
    //     {
    //         AnimatedSprite.Play(AnimatorNames.Run);
    //         BasisVelocity = velocity;
    //     }
    // }
}