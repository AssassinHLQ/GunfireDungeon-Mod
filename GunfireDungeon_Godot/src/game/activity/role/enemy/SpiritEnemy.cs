using Godot;

/// <summary>
/// 「刽子手的幽灵」—— 不死刽子手召唤出来的追兵。
///
/// 素材和刽子手同一份(同一页面、同一授权, 见
/// <c>resource/sprite/role/executioner0001/LICENSE.md</c>), 用的是里面那三张
/// <c>summonAppear / summonIdle / summonDeath</c> 图集。
///
/// 【它不会"攻击"】素材里没有幽灵的攻击动画, 所以设计上就是**撞人**:
/// 靠 <see cref="ContactDamage"/> 打伤害(基类 <see cref="AiRole.ProcessContactDamage"/>
/// 已经处理了判定距离和冷却), 移动借用 summonIdle 那 4 帧。
///
/// 【为什么 FiringStand 是 false】<c>AiAttackState.NoWeaponRoleProcess</c> 在攻击状态里
/// 会对 FiringStand 的角色执行 <c>BasisVelocity = Vector2.Zero</c> —— 幽灵要是站定,
/// 就变成一只站着不动的靶子了。设成 false 它才会一边撞一边追。
/// </summary>
public partial class SpiritEnemy : Enemy
{
    /// <summary>活动/角色 Id</summary>
    public new const string Id = "spirit0001";

    /// <summary>撞人伤害(比普通小怪的 1 点高)</summary>
    public override int ContactDamage => 2;

    /// <summary>碰撞判定半径 —— 幽灵体型小</summary>
    public override float ContactDamageRange => 26f;

    /// <summary>碰撞伤害冷却</summary>
    public override float ContactDamageCooldown => 0.7f;

    /// <summary>出生动画(召唤出现)时长, 6 帧 @12fps</summary>
    private const float AppearTime = 6f / 12f;

    public override void OnInit()
    {
        base.OnInit();

        //和刽子手一样: 没有武器、不捡武器
        NoWeaponAttack = true;
        WeaponPack.SetCapacity(0);
        RoleState.CanPickUpWeapon = false;
        //⚠️ 不能站定, 否则追不上人(见类注释)
        FiringStand = false;

        AnimatedSprite.SpriteFrames = SpiritSpriteFrames.Get();
        AnimatedSprite.Position = Vector2.Zero;
        AnimatedSprite.Offset = SpiritSpriteFrames.SpriteOffset;
        AnimatedSprite.Play(SpiritSpriteFrames.AnimAppear);

        //出场这几帧不要让状态机抢动画, 播完再交还控制权
        StateController.Enable = false;
        this.CallDelay(AppearTime, () =>
        {
            StateController.Enable = true;
            AnimatedSprite.Play(AnimatorNames.Idle);
        });
    }

    /// <summary>
    /// 幽灵没有攻击动作 —— 这里只是把 AttackTimer 设上, 让
    /// <c>AiAttackState.NoWeaponRoleProcess</c> 判定"攻击结束"并回到追击状态。
    /// 不设的话 Attack() 会被每帧调用, AI 会永远卡在攻击状态里。
    /// </summary>
    public override void Attack()
    {
        AttackTimer = AttackInterval;
    }
}
