

using DsUi;
using Godot;
using UI.game.BottomTips;
using UI.game.RoomMap;
using UI.game.WeaponRoulette;

namespace UI.game.RoomUI;

/// <summary>
/// 地牢房间中的ui
/// </summary>
public partial class RoomUIPanel : RoomUI
{
    /// <summary>
    /// 房间小地图
    /// </summary>
    public RoomMapPanel RoomMap { get; private set; }

    /// <summary>
    /// 遮挡Ui数量
    /// </summary>
    public int OcclusionCount { get; set; }
    
    private EventFactory<EventEnum> _factory;

    /// <summary>
    /// 顶部显示的当前层数
    /// </summary>
    private Label _floorLabel;
    //上一次显示的层数, 用于避免重复刷新
    private int _shownFloor = -1;
    
    public override void OnCreateUi()
    {
        GameApplication.Instance.RoomUIPanel = this;
        RoomMap = OpenNestedUi<RoomMapPanel>(UiManager.UiName.Game_RoomMap);

        CreateFloorLabel();
        
        MouseEntered += () => InputManager.SetMouseUiBlockage(false);
        MouseExited += () => InputManager.SetMouseUiBlockage(true);
    }

    /// <summary>
    /// 创建顶部层数文本
    /// </summary>
    private void CreateFloorLabel()
    {
        _floorLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _floorLabel.SetAnchorsPreset(Godot.Control.LayoutPreset.CenterTop);
        //锚点本身不受父节点偏移影响, 显式设置四周偏移避免继承旧值
        //楼层名比「第 N 层」长, 左右各留宽一点
        _floorLabel.OffsetLeft = -260;
        _floorLabel.OffsetTop = 12;
        _floorLabel.OffsetRight = 260;
        _floorLabel.OffsetBottom = 72;
        _floorLabel.AddThemeFontSizeOverride("font_size", 48);
        _floorLabel.AddThemeColorOverride("font_color", new Color("#ffe082"));
        _floorLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _floorLabel.AddThemeConstantOverride("outline_size", 4);
        S_Control.Instance.AddChild(_floorLabel);

        RefreshFloorLabel();
    }

    /// <summary>
    /// 刷新层数显示, 大厅里不显示
    /// </summary>
    public void RefreshFloorLabel()
    {
        if (_floorLabel == null)
        {
            return;
        }

        var dungeonManager = GameApplication.Instance.DungeonManager;
        if (World.Current is Hall || dungeonManager == null || !dungeonManager.IsInDungeon)
        {
            _floorLabel.Visible = false;
            _shownFloor = -1;
            return;
        }

        _shownFloor = dungeonManager.CurrentFloor;
        //显示楼层名(来自 FloorPlan.json)与进度, 同一楼层可能出现两次所以带上序号
        _floorLabel.Text = $"{dungeonManager.CurrentFloorName}  {_shownFloor}/{dungeonManager.TotalFloors}";
        _floorLabel.Visible = true;
    }

    public override void OnShowUi()
    {
        _factory = EventManager.CreateEventFactory();
        _factory.AddEventListener(EventEnum.OnPlayerPickUpProp, OnPlayerPickUpProp);

        //大厅中不显示小地图
        if (World.Current is Hall)
        {
            RoomMap.HideUi();
        }
        else
        {
            RoomMap.ShowUi();
        }

        RefreshFloorLabel();
    }

    public override void OnHideUi()
    {
        _factory.RemoveAllEventListener();
        _factory = null;
    }

    public override void OnDestroyUi()
    {
        GameApplication.Instance.RoomUIPanel = null;
    }

    public override void Process(float delta)
    {
        //层数变化时自动刷新顶部显示
        var dungeonManager = GameApplication.Instance.DungeonManager;
        if (dungeonManager != null && dungeonManager.IsInDungeon && dungeonManager.CurrentFloor != _shownFloor)
        {
            RefreshFloorLabel();
        }

        // 道具背包由全局 BackpackOverlay 处理，保留原有快捷键和暂停入口。
    }

    public override void _GuiInput(InputEvent @event)
    {
        InputManager.RoomInputHandler(@event);
    }

    //玩家拾起道具, 弹出提示
    private void OnPlayerPickUpProp(object propObj)
    {
        var prop = (PropActivity)propObj;
        var message = $"{prop.ActivityBase.Name}\n{prop.ActivityBase.Intro.Code}";
        BottomTipsPanel.ShowTips(prop.GetDefaultTexture(), message);
    }
}