
using System.Text.Json;
using Godot;

[BuffFragment(
    "ShieldRecoveryTime",
    "减少护盾的恢复延迟(ShieldDelay), 单位: 秒",
    Arg1 = "(int)减少类型, 1: 固定秒数, 2: 当前延迟的百分比",
    Arg2 = "(float)减少量, 类型1是秒数, 类型2是百分比(0.2 表示减少当前值的 20%)"
)]
public class Buff_ShieldRecoveryTime : BuffFragment
{
    private int _type = 1;
    private float _value;
    // 本次实际扣掉了多少秒。
    // 必须记下来, 因为百分比模式下移除道具时不能再用百分比重算
    // (期间延迟可能被别的道具改过), 只能用这个实际值加回去。
    private float _appliedTime;
    
    public override void InitParam(JsonElement[] args)
    {
        if (args.Length >= 2)
        {
            _type = args[0].GetInt32();
            _value = args[1].GetSingle();
        }
        else
        {
            // 兼容只写一个数值的旧配置: 按「固定秒数」处理
            _type = 1;
            _value = args[0].GetSingle();
        }
    }
    
    public override void OnPickUpItem()
    {
        var current = Role.RoleState.ShieldDelay;
        var reduce = _type == 2 ? current * _value : _value;
        // 上限是当前值: 不能把延迟扣成负数, 否则移除道具时会凭空多出恢复延迟
        _appliedTime = Mathf.Min(reduce, current);
        Role.RoleState.ShieldDelay = current - _appliedTime;
    }

    public override void OnRemoveItem()
    {
        Role.RoleState.ShieldDelay += _appliedTime;
    }
}
