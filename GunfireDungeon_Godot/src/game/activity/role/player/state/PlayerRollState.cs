
using System.Collections;
using Godot;

/// <summary>
/// 翻滚状态
/// </summary>
public class PlayerRollState : StateBase<Player, PlayerStateEnum>
{
    private long _coroutineId = -1;
    private Vector2 _moveDir;
    //本次翻滚的倍率, 在 Enter 里取一次。
    //和方向一样, 只取一次 —— 翻滚途中吃到一个移速 buff 不该让动画和速度中途跳变。
    private float _speedScale = 1f;
    
    public PlayerRollState() : base(PlayerStateEnum.Roll)
    {
    }

    public override void Enter(PlayerStateEnum prev, params object[] args)
    {
        if (_coroutineId >= 0)
        {
            Master.StopCoroutine(_coroutineId);
        }

        _coroutineId = Master.StartCoroutine(RunRoll());

        //隐藏武器
        Master.BackMountPoint.Visible = false;
        Master.MountPoint.Visible = false;
        //禁用伤害碰撞
        Master.HurtCollision.Disabled = true;
        
        //翻滚移动方向。
        //原来写的是 InputManager.MoveAxis —— 那要求玩家必须先按 WASD 才能指定方向,
        //站着不动按翻滚会得到零向量, 就完全不动。
        //改成 Player.GetRollDirection(): 鼠标优先、八向吸附, 手柄用右摇杆。
        //只在这里取一次, Process 里不再重算 —— 否则翻滚途中甩鼠标会让方向中途拐弯。
        _moveDir = Master.GetRollDirection();

        //【翻滚速度 / 动画速度与移速成正比】
        //倍率来自 Player.RollSpeedScale(= 当前移速 / 基础移速), 两边用同一个值,
        //所以动画播完的时间点和位移结束的时间点始终对得上。
        //不这么做的话穿两个鞋子(移速 120+30+30 = 180)后翻滚只有 170, 比正常跑还慢。
        _speedScale = Master.RollSpeedScale;
        Master.AnimatedSprite.SpeedScale = _speedScale;

        Master.BasisVelocity = _moveDir * RollSpeed;
    }

    public override void Exit(PlayerStateEnum next)
    {
        //显示武器
        Master.BackMountPoint.Visible = true;
        Master.MountPoint.Visible = true;
        //启用伤害碰撞
        Master.HurtCollision.Disabled = false;
        //恢复动画原速 —— 不恢复的话翻滚之后的移动/待机动画也会一直是加速的
        Master.AnimatedSprite.SpeedScale = 1f;
        Master.BasisVelocity = Master.BasisVelocity.LimitLength(Master.RoleState.MoveSpeed);
    }

    public override void Process(float delta)
    {
        Master.BasisVelocity = _moveDir * RollSpeed;
    }

    /// <summary>
    /// 本次翻滚的实际移动速度 = 配置 RollSpeed × 本次倍率。
    /// 用 Enter 里取的那一份倍率(而不是每帧重算), 保证和动画速度、动画时长一致。
    /// </summary>
    private float RollSpeed => Master.RoleState.RollSpeed * _speedScale;

    //翻滚逻辑处理
    private IEnumerator RunRoll()
    {
        Master.AnimatedSprite.Play(AnimatorNames.Roll);

        //翻滚期间关掉"枪口跟随鼠标"。
        //不再手动翻脸了 —— 原来有一段:
        //    var face = Master.Face;
        //    if (velocity.X > 0 && face == Left) Master.Face = Right; ...
        //    Master.Face = face;                 // 结束时又恢复
        // 三个问题: (1) 翻脸本来由 HandlerAiming 负责, 这里是重复;
        //          (2) 只处理左右, 而翻滚已改成八向, 斜向/竖直照顾不到;
        //          (3) 中途改了又恢复, 视觉上就是"翻过去又翻回来"的闪动。
        // 去掉之后, 翻滚朝向由鼠标直接决定, 和站着不动时一致。
        Master.MountLookTarget = false;

        SoundManager.PlaySoundByConfigDelay("role_rolling", Master.Position, 0.25f);
        
        yield return Master.AnimatedSprite.ToSignal(Master.AnimatedSprite, AnimatedSprite2D.SignalName.AnimationFinished);
        _coroutineId = -1;
        
        Master.MountLookTarget = true;
        Master.OverRoll();
        if (InputManager.MoveAxis != Vector2.Zero) //切换到移动状态
        {
            ChangeState(PlayerStateEnum.Move);
        }
        else //切换空闲状态
        {
            ChangeState(PlayerStateEnum.Idle);
        }
    }
}