using System.Collections.Generic;
using Godot;

/// <summary>
/// 宝箱
/// </summary>
public partial class TreasureBox : ObstacleObject
{
    // 宝箱奖励的投掷只保留短暂的弹出表现，避免玩家需要原地等待道具落地。
    private const float RewardThrowAltitude = 2f;
    private const float RewardAnimationSpeedScale = 2f;
    private const float RewardThrowVerticalSpeed = 30f;

    public bool IsOpen { get; private set; }

    public override void OnInit()
    {
        AnimatedSprite.AnimationFinished += OnAnimationFinished;
        DefaultLayer = RoomLayerEnum.YSortLayer;
    }

    public override CheckInteractiveResult CheckInteractive(ActivityObject master)
    {
        return new CheckInteractiveResult(this, !IsOpen, CheckInteractiveResult.InteractiveType.OpenTreasureBox);
    }

    public override void Interactive(ActivityObject master)
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        AnimatedSprite.SpeedScale = RewardAnimationSpeedScale;
        AnimatedSprite.Play(AnimatorNames.Open);
    }

    private void OnAnimationFinished()
    {
        var weapon = Create(World.RandomPool.GetRandomProp());
        weapon.Throw(Position, RewardThrowAltitude, RewardThrowVerticalSpeed, new Vector2(0, 11), 0);
    }

    public override void Hurt(ActivityObject target, List<AttackStats> damages, List<AbnormalData> abnormals, float angle)
    {
        PlayHitAnimation();
    }
}
