using System.Collections.Generic;
using Godot;

/// <summary>
/// 可被子弹击中的区域
/// </summary>
public partial class HurtArea : Area2D, IHurt
{
    /// <summary>
    /// 所属角色
    /// </summary>
    public Role Master { get; private set; }

    public void InitRole(Role role)
    {
        Master = role;
    }

    public override void _Ready()
    {
        Monitoring = false;
    }

    public bool CanHurt(CampEnum targetCamp)
    {
        //无敌状态
        if (Master.Invincible)
        {
            return true;
        }
        
        return Master.IsEnemy(targetCamp);
    }

    public void Hurt(ActivityObject target, List<AttackStats> damages, List<AbnormalData> abnormals, float angle)
    {
        Hurt(target, damages, abnormals, angle, false);
    }

    public void Hurt(ActivityObject target, List<AttackStats> damages, List<AbnormalData> abnormals,
        float angle, bool forceLocal)
    {
        var network = LanNetworkManager.Instance;
        if (!forceLocal && network != null && network.IsLanConnected && !network.IsHost &&
            Master.NetworkId != 0 && Master.IsAi)
        {
            if (target is Player)
            {
                network.RequestEnemyDamage(Master.NetworkId, damages, abnormals, angle);
            }

            return;
        }

        if (!forceLocal && network != null && network.IsLanConnected && network.IsHost &&
            Master is Player remotePlayer && network.IsRemotePlayer(remotePlayer) &&
            target is Role source && source.IsAi)
        {
            network.BroadcastRemotePlayerDamage(remotePlayer, source, damages, abnormals, angle);
            return;
        }

        if (damages != null)
        {
            foreach (var item in damages)
            {
                Master.CallDeferred(nameof(Master.HurtHandlerByDeferred), target, new GodotRefValue<AttackStats>(item), angle);
            }
        }
       
        if (abnormals != null)
        {
            foreach (var item in abnormals)
            {
                Master.CallDeferred(nameof(Master.AbnormalStateHandler), (int)item.Type, item.Value);
            }
        }
    }
}
