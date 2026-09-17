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
            // 第二个数字给【剩余子弹总量】= 弹夹 + 储备池。
            // 原来传的是 Attribute.AmmoCapacity(弹夹容量), 满弹时永远显示 "12/12",
            // 玩家看不出还剩多少子弹。
            // 储备池就是 Weapon.CurrMana(原「法力」, 会随时间排入缓冲区、开火时消耗),
            // 换弹本身不消耗它, 所以真正会减少的那一份就是它。
            SetWeaponAmmunition(weapon.CurrAmmo, weapon.CurrAmmo + weapon.CurrMana);
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
