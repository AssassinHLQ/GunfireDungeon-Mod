using Godot;

/// <summary>
/// 停止移动后自动冻结对象
/// </summary>
public partial class AutoFreezeObject : ActivityObject
{
    /// <summary>
    /// 自动播放的动画, 物体会等待该动画播完完成后进入冻结状态, 该动画不能是循环动画
    /// </summary>
    [Export]
    public string AnimationName { get; set; }
    
    /// <summary>
    /// 在冻结前是否变灰
    /// </summary>
    [Export]
    public bool AutoToGrey { get; set; }

    /// <summary>
    /// 落地静止后多少秒自动消失。0 = 保持原行为(冻结成静态贴图, 永久留在房间里)。
    ///
    /// 弹壳配的是 3 秒 —— 原来打完一梭子地上全是弹壳、永远不会消失,
    /// 因为下面的默认逻辑会把它冻进房间的静态画布(Freeze), 那是擦不掉的。
    /// </summary>
    [Export]
    public float AutoDestroyDelay { get; set; } = 0f;

    /// <summary>
    /// 冻结次数
    /// </summary>
    public int FreezeCount { get; private set; }
    
    private bool _playFlag = false;
    private float _grey = 0;

    //落地静止后已经等了多久(秒), 只在 AutoDestroyDelay > 0 时用
    private float _restTimer;

    /// <summary>
    /// 冻结时调用
    /// </summary>
    protected virtual void OnFreeze()
    {
    }
    
    public override void OnInit()
    {
        if (!string.IsNullOrEmpty(AnimationName))
        {
            _playFlag = true;
            AnimatedSprite.AnimationFinished += OnAnimationFinished;
            AnimatedSprite.Play(AnimationName);
        }
    }

    protected override void Process(float delta)
    {
        //落地静止后将弹壳变为静态贴图
        if (!_playFlag &&!IsThrowing && Altitude <= 0 && MoveController.IsMotionless())
        {
            if (AutoToGrey && _grey < 1)
            {
                //变灰动画时间, 0.5秒
                _grey = Mathf.Min(1, _grey + delta / 0.5f);
                Grey = _grey;
                return;
            }
            if (AffiliationArea != null)
            {
                if (AutoDestroyDelay > 0f)
                {
                    //配了自动消失: 落地后原地停 AutoDestroyDelay 秒, 然后销毁。
                    //
                    //【这里绝对不能调 Freeze()】Freeze 是把弹壳烙进房间的静态画布,
                    //一烙就再也擦不掉 —— 那正是"弹壳一直不消失"的根因。
                    //所以配了自动消失的物体全程不进冻结流程。
                    _restTimer += delta;
                    if (_restTimer >= AutoDestroyDelay)
                    {
                        Destroy();
                    }
                    return;
                }

                OnFreeze();
                Freeze();
                FreezeCount++;
            }
            else
            {
                Debug.Log(Name + "投抛到画布外了, 强制消除...");
                Destroy();
            }
        }
    }

    private void OnAnimationFinished()
    {
        _playFlag = false;
    }
}