using System.Collections;
using Config;

using DsUi;

namespace UI.game.Encyclopedia;

public class ItemCell : UiCell<Encyclopedia.ObjectButton, ExcelConfig.ActivityBase>
{
    public override void OnInit()
    {
        CellNode.L_Select.Instance.Visible = false;
    }

    public override void OnSetData(ExcelConfig.ActivityBase data)
    {
        // 部分配置(子弹/弹壳/门/伤害数字等)没有图标资源, 直接加载空路径会报错
        CellNode.L_PreviewImage.Instance.Texture = string.IsNullOrEmpty(data.Icon)
            ? null
            : ResourceManager.LoadTexture2D(data.Icon);
    }

    public override IEnumerator OnSetDataCoroutine(ExcelConfig.ActivityBase data)
    {
        CellNode.L_PreviewImage.Instance.Texture = string.IsNullOrEmpty(data.Icon)
            ? null
            : ResourceManager.LoadTexture2D(data.Icon);
        yield break;
    }

    public override void OnDisable()
    {
        CellNode.L_PreviewImage.Instance.Texture = null;
    }

    public override void OnSelect()
    {
        CellNode.L_Select.Instance.Visible = true;
        CellNode.UiPanel.SelectItem(Data);
    }
    
    public override void OnUnSelect()
    {
        CellNode.L_Select.Instance.Visible = false;
    }
}