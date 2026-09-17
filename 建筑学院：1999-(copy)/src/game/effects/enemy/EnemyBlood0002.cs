using Godot;

/// <summary>
/// 死亡血迹。
/// <para/>
/// 原版做法: 播完 0.2 秒的喷溅动画后, 把最后一帧"烤"进房间的静态画布, 于是血迹
/// 永远留在地上, 一局下来满地鲜红, 既吓人又和场景不搭。
/// <para/>
/// 现在改为: 喷溅结束后把血迹留在场上, 先保持一会儿鲜艳, 再慢慢变黑(现实里血迹
/// 放久了会氧化发黑), 越来越接近地板颜色, 最后整体淡出并移除。
/// </summary>
public partial class EnemyBlood0002 : Sprite2D
{
    /// <summary>喷溅结束后保持鲜艳的时间(秒)</summary>
    private const float HoldSeconds = 8f;

    /// <summary>从开始变暗到完全消失的时间(秒)</summary>
    private const float FadeSeconds = 26f;

    /// <summary>变暗阶段占整个消退过程的比例, 之后才开始淡出</summary>
    private const float DarkenRatio = 0.6f;

    /// <summary>干血的颜色(偏黑褐), 越接近它越像地板</summary>
    private static readonly Color DriedColor = new Color(0.14f, 0.10f, 0.10f, 1f);

    /// <summary>刚喷出来时的颜色, 由 Enemy 通过 Modulate 设置</summary>
    private Color _freshColor;

    private float _timer;
    private bool _decaying;

    /// <summary>
    /// 保留这个接口: Enemy 死亡时会调用它。
    /// 血迹现在不再烤进房间的静态画布, 所以这里不再需要房间信息。
    /// </summary>
    public void InitRoom(RoomInfo roomInfo)
    {
    }

    /// <summary>
    /// 喷溅动画播放完毕(由场景里的动画轨道调用)。
    /// 原来是"烤进画布然后自毁", 改成"留在场上开始消退"。
    /// </summary>
    private void DoDestory()
    {
        _freshColor = Modulate;
        _decaying = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!_decaying)
        {
            return;
        }

        _timer += (float)delta;
        var elapsed = _timer - HoldSeconds;
        if (elapsed <= 0f)
        {
            return;
        }

        var t = Mathf.Clamp(elapsed / FadeSeconds, 0f, 1f);

        // 先变黑(干血), 再淡出 —— 两段分开, 这样"越来越像地板"的过程看得出来
        var color = _freshColor.Lerp(DriedColor, Mathf.Clamp(t / DarkenRatio, 0f, 1f));
        color.A = _freshColor.A * (1f - Mathf.Clamp((t - DarkenRatio) / (1f - DarkenRatio), 0f, 1f));
        Modulate = color;

        if (t >= 1f)
        {
            QueueFree();
        }
    }
}
