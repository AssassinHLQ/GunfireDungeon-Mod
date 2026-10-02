using System.Collections.Generic;
using Godot;

/// <summary>
/// 在飞行中的子弹登记表。
///
/// 【为什么需要它】Boss 死亡后, 它已经打出去的子弹、激光、爆炸还留在场景里继续飞行,
/// 于是玩家会遇到"Boss 明明已经死了、尸体也没了, 还在掉盾" —— 尤其是发射量大的 Boss。
/// AiRole.OnDie() 只是把敌人节点移出场景, 不处理已经发射出去的攻击。
///
/// 这里按发射者记录所有在飞的 IBullet, 敌人死亡结算时就能只清掉它自己的攻击,
/// 不会误伤玩家和其他敌人打出去的子弹。
/// </summary>
public static class BulletTracker
{
    //发射者实例 Id -> 该发射者在飞的子弹
    private static readonly Dictionary<ulong, List<IBullet>> _entries = new();
    private static readonly Dictionary<IBullet, ulong> _entryByBullet = new();

    /// <summary>
    /// 登记一发刚发射的子弹。发射者为空时不登记(无法归属, 也就无法在死亡时清理)。
    /// </summary>
    public static void Register(IBullet bullet)
    {
        if (bullet == null || bullet.BulletData?.TriggerRole == null)
        {
            return;
        }

        var role = bullet.BulletData.TriggerRole;
        if (!GodotObject.IsInstanceValid(role))
        {
            return;
        }

        var key = role.GetInstanceId();
        if (!_entries.TryGetValue(key, out var list))
        {
            list = new List<IBullet>();
            _entries.Add(key, list);
        }

        //同一实例从对象池再次取出时, LogicalFinish 会先取消登记。
        if (_entryByBullet.ContainsKey(bullet))
        {
            return;
        }

        _entryByBullet.Add(bullet, key);
        list.Add(bullet);
    }

    /// <summary>
    /// 子弹生命周期结束后取消登记。
    /// </summary>
    public static void Unregister(IBullet bullet)
    {
        if (bullet == null || !_entryByBullet.TryGetValue(bullet, out var key))
        {
            return;
        }

        _entryByBullet.Remove(bullet);
        if (_entries.TryGetValue(key, out var list))
        {
            list.Remove(bullet);
            if (list.Count == 0)
            {
                _entries.Remove(key);
            }
        }
    }

    /// <summary>
    /// 清理某个角色已经发射出去、但仍留在场景里的所有攻击。
    /// 只影响该角色自己的子弹, 其它角色(含其它敌人)的子弹不受影响。
    /// </summary>
    public static void ClearInFlight(Role role)
    {
        if (role == null || !GodotObject.IsInstanceValid(role))
        {
            return;
        }

        PruneInvalid();
        var key = role.GetInstanceId();
        if (!_entries.TryGetValue(key, out var list))
        {
            return;
        }

        var bullets = list.ToArray();
        _entries.Remove(key);
        foreach (var bullet in bullets)
        {
            _entryByBullet.Remove(bullet);
            if (bullet is not Node node || !GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
            {
                continue;
            }

            //先停宽度动画, 再走正常的逻辑结束回收(对象池需要靠它回收)。
            bullet.LogicalFinish();
        }
    }

    //异常移除节点时没有走 LogicalFinish 的保护逻辑; 扫除无效实例。
    private static void PruneInvalid()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var staleKeys = new List<ulong>();
        var staleBullets = new List<IBullet>();
        foreach (var pair in _entries)
        {
            pair.Value.RemoveAll(bullet =>
            {
                if (bullet is not Node node || !GodotObject.IsInstanceValid(node) ||
                    node.IsQueuedForDeletion() || bullet.BulletData?.TriggerRole == null ||
                    !GodotObject.IsInstanceValid(bullet.BulletData.TriggerRole))
                {
                    staleBullets.Add(bullet);
                    return true;
                }

                return false;
            });

            if (pair.Value.Count == 0)
            {
                staleKeys.Add(pair.Key);
            }
        }

        foreach (var bullet in staleBullets)
        {
            _entryByBullet.Remove(bullet);
        }

        foreach (var key in staleKeys)
        {
            _entries.Remove(key);
        }
    }
}
