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
            // 显示「弹夹 / 备用弹药」—— 后面这个数字是【只算备用弹药】, 不含弹夹里那几发。
            // 总弹药 = 前面的数字 + 后面的数字, 玩家自己一眼能加出来。
            //
            // 【为什么不是"弹夹+备用"的总量】之前两个数字都是总量, 结果开一枪两个数一起掉,
            // 而且换弹时数字几乎不动, 看不出消耗。
            // 现在: 开枪只减前一个数, 换弹只从后一个数里扣, 两个数字各自只反映一件事。
            SetWeaponAmmunition(weapon.CurrAmmo, weapon.CurrReserveAmmo);
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
    /// <param name="totalAmmo">备用弹药量(不含弹夹)</param>
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
}
