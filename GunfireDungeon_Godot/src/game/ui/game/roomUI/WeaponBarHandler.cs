using Godot;

using DsUi;

namespace UI.game.RoomUI;

public partial class WeaponBarHandler : Control, IUiNodeScript
{
    private RoomUI.WeaponBar _weaponBar;
    
    public void SetUiNode(IUiNode uiNode)
    {
        _weaponBar = (RoomUI.WeaponBar)uiNode;
        SetWeaponTexture(null);
    }

    public override void _Process(double delta)
    {
        if (_weaponBar == null || !_weaponBar.UiPanel.IsOpen)
        {
            return;
        }
        var weapon = World.Current.Player?.WeaponPack.ActiveItem;
        if (weapon != null)
        {
            SetWeaponTexture(weapon.GetCurrentTexture());
            // 第二个数字给【剩余子弹总量】= 弹夹 + 法力池还能换出来的发数。
            // 原来传的是 Attribute.AmmoCapacity(弹夹容量), 满弹时永远显示 "12/12",
            // 玩家看不出还剩多少子弹。
            //
            // ⚠️ 不能直接把 CurrMana 当子弹数 —— 2000 法力【不等于】2000 发。
            //    法力是按"每开一枪扣掉开火零件的 BaseMana"逐发消耗的
            //    (手枪的开火零件 0003 是 8/发), 所以要换算成"还能打几发"。
            SetWeaponAmmunition(weapon.CurrAmmo, weapon.CurrAmmo + GetReserveShots(weapon));
        }
        else
        {
            SetWeaponTexture(null);
        }
    }

    /// <summary>
    /// 设置显示在 ui 上武器的纹理
    /// </summary>
    /// <param name="texture">纹理</param>
    public void SetWeaponTexture(Texture2D texture)
    {
        if (texture != null)
        {
            _weaponBar.L_WeaponPanel.L_WeaponSprite.Instance.Texture = texture;
            _weaponBar.Instance.Visible = true;
        }
        else
        {
            _weaponBar.Instance.Visible = false;
        }
    }

    /// <summary>
    /// 设置弹药数字。
    /// 法力系统已经移除，原来显示法力值的进度条现在只用来承载【弹夹】数字：
    /// 填充条（原来的黄色/蓝色那条）与子弹图标列都已经去掉，只留数字。
    /// </summary>
    /// <param name="currAmmo">当前弹夹里的子弹数</param>
    /// <param name="totalAmmo">剩余子弹总量（弹夹 + 储备池）</param>
    public void SetWeaponAmmunition(int currAmmo, int totalAmmo)
    {
        // 法力缓冲条与法力图标已经没有对应数值了，保持隐藏
        _weaponBar.L_BufferManaProgress.Instance.Visible = false;
        _weaponBar.L_ManaIcon.Instance.Visible = false;

        // 显示成「弹夹 / 剩余总量」
        _weaponBar.L_ManaProgress.Instance.NumberLabel.Text = currAmmo + "/" + totalAmmo;
    }

    public void OnDestroy()
    {
    }

    /// <summary>
    /// 法力池还能支持多少发。
    ///
    /// 法力池(<c>Weapon.CurrMana</c>)不是子弹数 —— 它是按"每开一枪扣掉开火零件的
    /// <c>GetMana()</c>"逐发消耗的(零件数值见 resource/config/PartBase.json)。
    /// 所以要把"剩余法力"换算成"还能打几发"才是真实数值:
    /// 例: 手枪 CurrMana=2000, 开火零件 0003 是 8/发 -> 还能打 250 发。
    /// 换算不出来的(没有开火零件 / 消耗为 0)就返回 0, 只用弹夹数。
    /// </summary>
    private static int GetReserveShots(Weapon weapon)
    {
        var cost = GetManaCostPerShot(weapon);
        return cost > 0 ? weapon.CurrMana / cost : 0;
    }

    /// <summary>每开一枪消耗的法力 = 开火零件链上所有零件的 GetMana() 之和。</summary>
    private static int GetManaCostPerShot(Weapon weapon)
    {
        var list = weapon?.FirePartList;
        if (list == null)
        {
            return 0;
        }

        var total = 0;
        for (var i = 0; i < list.Length; i++)
        {
            var part = list.GetLogicBlock(i);
            if (part?.PartLogicBase != null)
            {
                total += part.PartLogicBase.GetMana();
            }
        }
        return total;
    }
}
