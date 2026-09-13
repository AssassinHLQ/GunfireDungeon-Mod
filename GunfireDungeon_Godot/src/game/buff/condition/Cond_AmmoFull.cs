
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
        if (Role.WeaponPack.ActiveItem == null)
        {
            return false;
        }

        return true;
    }
}