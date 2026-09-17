using System.Collections.Generic;
using System.Linq;
using DsUi;
using Godot;

namespace UI.game.PartPackUI;

/// <summary>
/// 武器列表Cell
/// </summary>
public class WeaponListCell : UiCell<PartPackUI.WeaponItem, Weapon>
{
    public UiGrid<PartPackUI.PartListItem, PartListCellData> PartListGrid;
    
    public override void OnInit()
    {
        PartListGrid = CellNode.UiPanel.CreateUiGrid<PartPackUI.PartListItem, PartListCellData, PartListCell>(CellNode.L_PartListItem);
        PartListGrid.SetColumns(1);
        PartListGrid.SetCellOffset(new Vector2I(0, 0));
        PartListGrid.GridContainer.Resized += OnPartListGridResized;
        CellNode.L_PartListItem.Instance.Visible = false;
        CellNode.L_VBoxContainer.L_WeaponBuffMana.Instance.Visible = false;
    }

    public override void OnSetData(Weapon data)
    {
        //图标
        CellNode.L_Control.L_WeaponIcon.Instance.Texture = data.GetDefaultTexture();

        // 不再展示武器零件树，武器本身作为背包查看对象显示。
        PartListGrid.SetDataList(new List<PartListCellData>());
        RefreshBaseInfo();
    }

    public override void Process(float delta)
    {
        RefreshBaseInfo();
    }
    
    private void RefreshBaseInfo()
    {
        if (Data == null)
        {
            return;
        }
        CellNode.L_VBoxContainer.L_WeaponMana.Instance.Text = "弹夹：" + Data.CurrAmmo + "/" + Data.Attribute.AmmoCapacity;
    }

    private void OnPartListGridResized()
    {
        var minimumSize = CellNode.Instance.CustomMinimumSize;
        minimumSize.Y = CellNode.UiPanel.WeaponCellOriginSize.Y + PartListGrid.GridContainer.Size.Y;
        CellNode.Instance.CustomMinimumSize = minimumSize;
    }
}