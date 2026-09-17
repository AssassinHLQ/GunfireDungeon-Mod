using System.Collections.Generic;
using Godot;

using DsUi;

namespace UI.game.RoomUI;

public partial class WeaponBarHandler : Control, IUiNodeScript
{
    private RoomUI.WeaponBar _weaponBar;

    private int _prevAmmo = -1;
    private UiGrid<RoomUI.BulletItem, bool> _bulletGrid;


    private Weapon _prevWeapon;
    private int _prevBullet;
    
    public void SetUiNode(IUiNode uiNode)
    {
        _weaponBar = (RoomUI.WeaponBar)uiNode;
        _bulletGrid = _weaponBar.UiPanel.CreateUiGrid<RoomUI.BulletItem, bool, GunBulletCell>(_weaponBar.L_VBoxContainer.L_BulletItem);
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

        //武器有变化
        if (_prevWeapon != weapon)
        {
            _prevWeapon = weapon;
            _prevAmmo = weapon?.CurrAmmo ?? -1;
            if (weapon != null)
            {
                var list = new bool[weapon.Attribute.AmmoCapacity];
                for (var i = 0; i < weapon.Attribute.AmmoCapacity; i++)
                {
                    list[weapon.Attribute.AmmoCapacity - i - 1] = i < weapon.CurrAmmo;
                }
                _bulletGrid.SetDataList(list);
            }
        }
        else if (weapon != null && _prevAmmo != weapon.CurrAmmo) //子弹有变化
        {
            int max, min;
            if (weapon.CurrAmmo > _prevAmmo)
            {
                max = weapon.CurrAmmo;
                min = _prevAmmo;
            }
            else
            {
                max = _prevAmmo;
                min = weapon.CurrAmmo;
            }
            for (var i = min; i < max; i++)
            {
                _bulletGrid.UpdateByIndex(weapon.Attribute.AmmoCapacity - i - 1, i < weapon.CurrAmmo);
            }
            _prevAmmo = weapon.CurrAmmo;
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
    /// 弹夹进度条的颜色（原来法力条是蓝色，换成暖色一眼能看出是子弹）
    /// </summary>
    private static readonly Color MagazineColor = new Color(1f, 0.78f, 0.32f, 1f);

    /// <summary>
    /// 弹夹进度条是否已经换过颜色，只需要换一次
    /// </summary>
    private bool _magazineStyled;

    /// <summary>
    /// 设置弹药数据。
    /// 法力系统已经移除，原来显示法力值的进度条现在改成显示【弹夹余弹】。
    /// </summary>
    public void SetWeaponAmmunition(int currAmmo, int maxAmmo)
    {
        // 法力缓冲条与法力图标已经没有对应数值了，保持隐藏
        _weaponBar.L_BufferManaProgress.Instance.Visible = false;
        _weaponBar.L_ManaIcon.Instance.Visible = false;

        var magazine = _weaponBar.L_ManaProgress.Instance;
        magazine.Visible = true;
        if (!_magazineStyled)
        {
            _magazineStyled = true;
            magazine.ValueColor = MagazineColor;
            magazine.ValueRect.Color = MagazineColor;
        }

        // MaxValue 必须先于 Value 设置，Value 会用 _maxValue 做钳制
        magazine.MaxValue = Mathf.Max(1, maxAmmo);
        magazine.Value = currAmmo;
        // CommProgressBar 默认只显示当前值，弹夹要的是「当前 / 上限」
        magazine.NumberLabel.Text = currAmmo + "/" + maxAmmo;
    }

    public void OnDestroy()
    {
    }
}