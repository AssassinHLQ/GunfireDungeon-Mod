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
    private string _rewardActivityId = string.Empty;
    private bool _rewardCreated;

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

        var network = LanNetworkManager.Instance;
        if (network != null && network.IsLanConnected && NetworkId != 0 &&
            !network.IsLocalDungeonAuthority)
        {
            network.RequestTreasureBoxOpen(this);
            return;
        }

        OpenWithReward(World?.RandomPool?.GetRandomProp()?.Id ?? string.Empty);
    }

    public void OpenWithReward(string activityId)
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _rewardActivityId = activityId ?? string.Empty;
        AnimatedSprite.SpeedScale = RewardAnimationSpeedScale;
        AnimatedSprite.Play(AnimatorNames.Open);
    }

    private void OnAnimationFinished()
    {
        if (IsDestroyed || _rewardCreated || string.IsNullOrEmpty(_rewardActivityId))
        {
            return;
        }

        _rewardCreated = true;
        var reward = Create(_rewardActivityId);
        if (reward == null)
        {
            return;
        }

        reward.IsPerPlayerLoot = true;
        reward.Throw(Position, RewardThrowAltitude, RewardThrowVerticalSpeed, new Vector2(0, 11), 0);
    }

    protected override void OnDestroy()
    {
        if (GodotObject.IsInstanceValid(AnimatedSprite))
        {
            AnimatedSprite.AnimationFinished -= OnAnimationFinished;
        }

        base.OnDestroy();
    }

    public override void Hurt(ActivityObject target, List<AttackStats> damages, List<AbnormalData> abnormals, float angle)
    {
        PlayHitAnimation();
    }
}
