
using Godot;

/// <summary>
/// 空闲状态
/// </summary>
public class PlayerIdleState : StateBase<Player, PlayerStateEnum>
{
    public PlayerIdleState() : base(PlayerStateEnum.Idle)
    {
    }

    public override void Enter(PlayerStateEnum prev, params object[] args)
    {
        Master.HandleMoveInput((float)Master.GetProcessDeltaTime());
        Master.AnimatedSprite.Play(AnimatorNames.Idle);
    }

    public override void Process(float delta)
    {
        //翻滚判定必须【放在最前面, 不能套在"有移动输入"里面】。
        //
        //【踩坑记录】原来是这样写的:
        //    var dir = InputManager.MoveAxis;
        //    if (dir != Vector2.Zero) {
        //        if (InputManager.Roll && Master.CanRoll) ChangeState(Roll);
        //        else ChangeState(Move);
        //    } else {
        //        Master.HandleMoveInput(delta);      // <- 站着不动走这里, 根本不检查翻滚
        //    }
        // 于是"站着不动翻滚"永远触发不了 —— 这正是玩家反馈的问题。
        // 翻滚方向由 Player.GetRollDirection() 决定(鼠标优先), 和移动键无关,
        // 所以翻滚本来就不需要任何移动输入。
        if (InputManager.Roll && Master.CanRoll)
        {
            ChangeState(PlayerStateEnum.Roll);
            return;
        }

        if (InputManager.MoveAxis != Vector2.Zero)
        {
            ChangeState(PlayerStateEnum.Move);
        }
        else
        {
            Master.HandleMoveInput(delta);
        }
    }
}