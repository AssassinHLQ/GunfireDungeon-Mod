
using System.Text.Json;

[ConditionFragment(
    "AmmoFull", 
    "判断当前武器子弹状态, ",
    Arg1 = "(boolean)是否判断满子弹"
)]
public class Cond_AmmoFull : ConditionFragment
{
    private bool _type;
    
    public override void InitParam(JsonElement[] arg)
    {
        _type = arg[0].GetBoolean();
    }

    public override bool OnCheckUse()
    {
        var weapon = Role?.WeaponPack?.ActiveItem;
        if (weapon == null)
        {
            return false;
        }

        // 参数含义和 Cond_HpFull 一致: true = "判断非满状态"(没满才允许使用)。
        // 弹药箱 prop5001 配的就是 AmmoFull: [true] —— 备用弹药满了就不该浪费。
        //
        // 原来这里是个空壳: 参数 _type 读进来了却没用, 只要身上有武器就能用,
        // 于是备用弹药满的时候也会白白吃掉一个弹药箱。
        // 「满」要同时看弹夹和备用弹药 —— 只看弹夹的话, 弹夹满但备用弹药空的
        // 时候会把弹药箱判成"用不了"。
        var isFull = weapon.CurrAmmo >= weapon.Attribute.AmmoCapacity
                     && weapon.CurrReserveAmmo >= weapon.MaxReserveAmmo;
        return _type ? !isFull : isFull;
    }
}