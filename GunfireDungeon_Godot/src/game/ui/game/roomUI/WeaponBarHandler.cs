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
            SetWeaponAmmunition(weapon.CurrAmmo, weapon.Attribute.AmmoCapacity);
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
    /// 设置弹药数据。
    /// 法力系统已经移除，原来显示法力值的进度条现在只用来承载【弹夹余弹】数字：
    /// 填充条（原来的黄色/蓝色那条）与子弹图标列都已经去掉，只留数字。
    /// </summary>
    public void SetWeaponAmmunition(int currAmmo, int maxAmmo)
    {
        // 法力缓冲条与法力图标已经没有对应数值了，保持隐藏
        _weaponBar.L_BufferManaProgress.Instance.Visible = false;
        _weaponBar.L_ManaIcon.Instance.Visible = false;

        // CommProgressBar 默认只显示当前值，弹夹要的是「当前 / 上限」
        _weaponBar.L_ManaProgress.Instance.NumberLabel.Text = currAmmo + "/" + maxAmmo;
    }

    public void OnDestroy()
    {
    }
}
