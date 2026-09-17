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
            // 第二个数字给【剩余子弹总量】= 弹夹 + 备用弹药。
            // 原来传的是 Attribute.AmmoCapacity(弹夹容量), 满弹时永远显示 "12/12",
            // 玩家看不出还剩多少子弹。
            //
            // 备用弹药就是 Weapon.CurrReserveAmmo(含义见 Weapon.cs 里的说明),
            // 换弹会从它里面扣, 它和弹夹都空了才是真的打不出子弹。
            // 不再从法力池换算 —— 法力系统已经不再限制射击。
            SetWeaponAmmunition(weapon.CurrAmmo, weapon.CurrAmmo + weapon.CurrReserveAmmo);
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
}
