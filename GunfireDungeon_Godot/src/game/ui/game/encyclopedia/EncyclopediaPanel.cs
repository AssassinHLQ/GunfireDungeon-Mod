using System.Linq;
using Config;
using Godot;

using DsUi;

namespace UI.game.Encyclopedia;

/// <summary>
/// 图鉴
/// </summary>
public partial class EncyclopediaPanel : Encyclopedia
{
    //tab网格
    private UiGrid<TabButton, TabData> _tab;
    //item网格
    private UiGrid<ObjectButton, ExcelConfig.ActivityBase> _grid;
    //private long _id;

    public override void OnCreateUi()
    {
        S_CloseButton.Instance.Pressed += OnCloseClick;
        
        _tab = CreateUiGrid<TabButton, TabData, TabCell>(S_TabButton);
        _tab.SetColumns(10);
        _tab.SetCellOffset(new Vector2I(0, 0));
        _tab.Add(new TabData(ResourcePath.resource_sprite_ui_encyclopedia_TabIcon1_png, ActivityType.Weapon));
        _tab.Add(new TabData(ResourcePath.resource_sprite_ui_encyclopedia_TabIcon1_png, ActivityType.Prop));
        _tab.Add(new TabData(ResourcePath.resource_sprite_ui_encyclopedia_TabIcon1_png, ActivityType.Enemy));

        // 说明文字里用了 BBCode(例如武器零件的 "水[color=#7bc1d5]子弹[/color]")。
        // RichTextLabel 的 bbcode_enabled 默认是 false, 不开这个开关就会把标签原样显示出来。
        S_ItemDes.Instance.BbcodeEnabled = true;
        
        _grid = CreateUiGrid<ObjectButton, ExcelConfig.ActivityBase, ItemCell>(S_ObjectButton);
        _grid.SetHorizontalExpand(true);
        _grid.SetAutoColumns(true);
        _grid.SetCellOffset(new Vector2I(10, 10));

        _tab.SelectIndex = 0;
    }

    public override void OnShowUi()
    {
        InputManager.AddBlockageMarking(GetInstanceId());
        GameCamera.Main.LockCamera();
    }
    
    public override void OnHideUi()
    {
        InputManager.RemoveBlockageMarking(GetInstanceId());
        GameCamera.Main.UnLockCamera();
    }

    public override void OnDestroyUi()
    {
        
    }
    
    public override void Process(float delta)
    {
        if (Input.IsActionJustPressed(InputAction.UiCancel))
        {
            OnCloseClick();
        }
    }
    
    /// <summary>
    /// 设置选中的tab
    /// </summary>
    public void SelectTab(ActivityType type)
    {
        // StopCoroutine(_id);
        // _id = StartCoroutine(
        //     _grid.SetDataListCoroutine(
        //         ExcelConfig.ActivityBase_List.Where(data => data.Type == type).ToArray()
        //     )
        // );
        // 敌人页签里连 Boss 一起列出来。
        // Boss 用"有没有图标"再过滤一道: boss0001 是上游留下的空壳配置
        // (名字就叫 Boss, 预制体文件 prefab/role/boss/Boss0001.tscn 根本不存在), 不该出现在图册里。
        _grid.SetDataList(
            type == ActivityType.Enemy
                ? ExcelConfig.ActivityBase_List.Where(data =>
                    data.Type == ActivityType.Enemy ||
                    (data.Type == ActivityType.Boss && !string.IsNullOrEmpty(data.Icon))).ToArray()
                : ExcelConfig.ActivityBase_List.Where(data => data.Type == type).ToArray()
        );
        SelectItem(null);
    }

    /// <summary>
    /// 设置选中的物体
    /// </summary>
    public void SelectItem(ExcelConfig.ActivityBase config)
    {
        if (config != null)
        {
            S_ItemInfoBg.Instance.Visible = true;
            S_ItemName.Instance.Text = config.Name;
            S_ItemTexture.Instance.Texture = ResourceManager.LoadTexture2D(config.Icon);
            S_ItemDes.Instance.Text = config.Intro.Code;
            //S_ItemDes.Instance.Text = config.Details;
        }
        else
        {
            S_ItemInfoBg.Instance.Visible = false;
            S_ItemName.Instance.Text = null;
            S_ItemTexture.Instance.Texture = null;
            S_ItemDes.Instance.Text = null;
        }
    }

    private void OnCloseClick()
    {
        if (PrevUi != null)
        {
            OpenPrevUi();
        }
        else
        {
            Destroy();
        }
    }

}
