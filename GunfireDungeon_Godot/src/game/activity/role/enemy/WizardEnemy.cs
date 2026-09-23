using Config;
using Godot;

/// <summary>
/// 「邪恶法师」—— 精英怪。不拿武器, 站定施法, 朝玩家扇形放三颗火球。
///
/// 【为什么继承 Enemy 而不是 NoWeaponEnemy】
/// NoWeaponEnemy 的 Process 里有一句 <c>DrawLiquid(...)</c> —— 它在给"徒手小怪"留毒液脚印。
/// 法师踩着毒液走明显不对, 所以这里继承 <see cref="Enemy"/>,
/// 自己把那四行"没有武器也要能攻击"的设置补上(和 NoWeaponEnemy.OnInit 里的完全一致)。
/// 这样还能白拿 Enemy.OnDie 的血液 + 碎块死亡表现。
///
/// 【为什么随机刷出来的小怪都有枪, 它却没有】
/// <c>RandomPool.FillBattleRoom</c> 会给每个敌人标记塞一把武器,
/// 但这里 <c>WeaponPack.SetCapacity(0)</c> + <c>CanPickUpWeapon = false</c>,
/// 那件武器它根本捡不起来。现有的 enemy0002「徒手小怪」走的就是同一条路。
///
/// 【攻击流程怎么走】(抄的是 NoWeaponEnemy 的机制, 只是把"动画播完"换成自己的计时器)
///   AiAttackState.NoWeaponRoleProcess 在 AttackTimer &lt;= 0 时【每帧】调 Attack();
///   本类用一个 <c>_castLeft</c> 计时器把一次施法撑住(所以 Attack 必须防重入!),
///   施法结束再把 AttackTimer 设上, 状态机就会自动切回追击。
/// </summary>
public partial class WizardEnemy : Enemy
{
    /// <summary>活动/角色 Id</summary>
    public const string Id = "wizard0001";

    /// <summary>一次施法的总时长(秒), 对应 attack 动画 8 帧 @12fps</summary>
    private const float CastTotalTime = 0.66f;

    /// <summary>第几秒放弹(抬手到位的那一刻)</summary>
    private const float CastReleaseTime = 0.34f;

    /// <summary>一次放几颗火球</summary>
    private const int FireballCount = 3;

    /// <summary>扇形总张角(度)</summary>
    private const float FireballSpreadDegrees = 18f;

    /// <summary>火球弹道配置 —— 见 resource/config/BulletBase.json 的 <c>0012</c></summary>
    private const string FireballBulletId = "0012";

    /// <summary>施法特效(火焰) —— 复用徒手小怪同款</summary>
    private const string CastEffectPath = "res://prefab/effect/weapon/ShotFire0003.tscn";

    /// <summary>施法剩余时间, &lt;= 0 表示没在施法</summary>
    private float _castLeft = -1f;

    /// <summary>这一次施法是否已经把弹放出去了</summary>
    private bool _released;

    public override void OnInit()
    {
        base.OnInit();

        //和 NoWeaponEnemy.OnInit 一样的三件事: 没有武器也能攻击、攻击时站定、不捡武器
        NoWeaponAttack = true;
        FiringStand = true;
        WeaponPack.SetCapacity(0);
        RoleState.CanPickUpWeapon = false;

        AnimatedSprite.SpriteFrames = WizardSpriteFrames.Get();
        //AiTemplate 里 AnimatedSprite 的 (0,-8) 是给 16x24 小怪留的, 法师要自己重新对齐
        AnimatedSprite.Position = Vector2.Zero;
        AnimatedSprite.Offset = WizardSpriteFrames.SpriteOffset;
        AnimatedSprite.Play(AnimatorNames.Idle);
    }

    /// <summary>
    /// 触发一次施法。
    ///
    /// ⚠️ AiAttackState 会在 AttackTimer &lt;= 0 时【每帧】调用本函数,
    /// 所以必须靠 <c>_castLeft</c> 防重入 —— 否则一帧就放一轮弹。
    /// </summary>
    public override void Attack()
    {
        if (_castLeft > 0f)
        {
            return;
        }

        _castLeft = CastTotalTime;
        _released = false;
        BasisVelocity = Vector2.Zero;
        AnimatedSprite.Play(AnimatorNames.Attack);
    }

    protected override void Process(float delta)
    {
        base.Process(delta);

        if (_castLeft <= 0f)
        {
            return;
        }

        _castLeft -= delta;

        if (!_released && _castLeft <= CastTotalTime - CastReleaseTime)
        {
            _released = true;
            ReleaseFireballs();
        }

        if (_castLeft <= 0f)
        {
            AnimatedSprite.Play(AnimatorNames.Idle);
            //把 AttackTimer 设上, AiAttackState 下一帧就会判定"攻击结束"并切回追击状态
            AttackTimer = AttackInterval;
        }
    }

    /// <summary>朝目标扇形放出三颗火球</summary>
    private void ReleaseFireballs()
    {
        var target = LookTarget;
        if (target == null)
        {
            return;
        }

        var origin = GetFirePoint();
        var baseAngle = origin.AngleToPoint(target.GetCenterPosition());

        PlayCastEffect(origin, baseAngle);

        var template = ExcelConfig.BulletBase_Map[FireballBulletId];
        if (template == null)
        {
            GD.PushError($"[Wizard] 找不到火球弹道配置 BulletBase[{FireballBulletId}]");
            return;
        }

        for (var i = 0; i < FireballCount; i++)
        {
            //张成 -1 / 0 / +1 三档, 关于瞄准方向对称
            var t = FireballCount > 1 ? i / (float)(FireballCount - 1) * 2f - 1f : 0f;
            var param = new FireBulletParam(template)
            {
                Position = origin,
                FireRotation = baseAngle + Mathf.DegToRad(t * FireballSpreadDegrees * 0.5f)
            };

            var data = FireManager.GetBulletData(this, param);
            if (data != null)
            {
                FireManager.ShootBullet(data, Camp);
            }
        }
    }

    /// <summary>施法瞬间的火焰特效</summary>
    private void PlayCastEffect(Vector2 position, float angle)
    {
        var effect = ObjectManager.GetPoolItem<IEffect>(CastEffectPath);
        if (effect == null)
        {
            return;
        }

        var node = (Node2D)effect;
        node.GlobalPosition = position;
        node.Rotation = angle;
        node.AddToActivityRoot(RoomLayerEnum.YSortLayer);
        effect.PlayEffect();
    }
}
