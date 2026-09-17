
using System.Text.Json;

[EffectFragment(
    "TotalAmmo", 
    "修改武器总子弹量, ",
    Arg1 = "(int|null)子弹变化的具体值, 如果不传则表示补满子弹"
)]
public class Eff_TotalAmmo : EffectFragment
{
    private bool _initParam = false;
    private int _value;

    public override void InitParam(JsonElement[] args)
    {
        if (args.Length > 0)
        {
            _initParam = true;
            _value = args[0].GetInt32();
        }
    }

    public override void OnUse()
    {
        // 作用对象是【当前手持武器的备用弹药池】(不是弹夹里的子弹)。
        // 弹药箱 prop5001 的说明就是"使用后补充当前武器备用弹药"。
        var weapon = Role?.WeaponPack?.ActiveItem;
        if (weapon == null)
        {
            return;
        }

        // 带了参数就补充指定数量, 不带参数就补满
        if (_initParam)
        {
            weapon.AddReserveAmmo(_value);
        }
        else
        {
            weapon.FillReserveAmmo();
        }
    }
}