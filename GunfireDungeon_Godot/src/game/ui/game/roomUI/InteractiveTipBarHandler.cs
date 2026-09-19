using Godot;

using DsUi;

namespace UI.game.RoomUI;

/// <summary>
/// 互动提示文本
/// </summary>
public partial class InteractiveTipBarHandler : Control, IUiNodeScript
{
    private RoomUI.InteractiveTipBar _interactiveTipBar;
    private EventBinder<EventEnum> _binder;

    /// <summary>
    /// 当前提示的目标。
    /// 这里存 Node2D(取 GlobalPosition 用)而不是 ActivityObject:
    /// 商店货架 ShopItemSlot 是 Area2D, 不是 ActivityObject,
    /// 原来用 ActivityObject 存会导致商店的互动提示永远不显示。
    /// </summary>
    private Node2D _interactiveNode;

    /// <summary>
    /// 当前提示的互动物体。取名字用 —— ActivityObject 走 ActivityBase.Name,
    /// 其他实现走各自的显示名。
    /// </summary>
    private IInteractive _interactiveItem;
    
    public void SetUiNode(IUiNode uiNode)
    {
        _interactiveTipBar = (RoomUI.InteractiveTipBar)uiNode;
        _interactiveTipBar.Instance.Visible = false;
        _interactiveTipBar.UiPanel.OnShowUiEvent += OnShow;
        _interactiveTipBar.UiPanel.OnHideUiEvent += OnHide;
    }

    public void OnShow()
    {
        GameCamera.Main.OnPositionUpdateEvent += OnCameraPositionUpdate;
        _binder = EventManager.AddEventListener(EventEnum.OnPlayerChangeInteractiveItem, OnPlayerChangeInteractiveItem);
    }

    public void OnHide()
    {
        GameCamera.Main.OnPositionUpdateEvent -= OnCameraPositionUpdate;
        _binder.RemoveEventListener();
        _binder = null;
    }
    
    /// <summary>
    /// 隐藏互动提示ui
    /// </summary>
    public void HideBar()
    {
        _interactiveTipBar.Instance.Visible = false;
    }

    /// <summary>
    /// 显示互动提示ui
    /// </summary>
    /// <param name="target">所在坐标</param>
    /// <param name="showText">显示文本</param>
    /// <param name="icon">显示图标</param>
    public void ShowBar(string showText, Texture2D icon)
    {
        if (_interactiveNode == null)
        {
            return;
        }

        _interactiveTipBar.Instance.GlobalPosition = GameApplication.Instance.WorldToUiPosition(_interactiveNode.GlobalPosition);
        _interactiveTipBar.L_Icon.Instance.Texture = icon;
        _interactiveTipBar.Instance.Visible = true;
        _interactiveTipBar.L_NameLabel.Instance.Text = showText;
    }

    public void OnPlayerChangeInteractiveItem(object o)
    {
        if (o == null)
        {
            _interactiveNode = null;
            _interactiveItem = null;
            //隐藏互动提示
            HideBar();
            return;
        }

        var result = (CheckInteractiveResult)o;

        //必须是 Node2D 才能取到 GlobalPosition 来定位提示条。
        //ActivityObject 是 CharacterBody2D, ShopItemSlot 是 Area2D, 两者都满足。
        if (result.Target is not Node2D node)
        {
            _interactiveNode = null;
            _interactiveItem = null;
            return;
        }

        var icon = result.GetIcon();
        if (icon == null)
        {
            _interactiveNode = null;
            _interactiveItem = null;
            return;
        }

        _interactiveNode = node;
        _interactiveItem = result.Target;
        //显示互动提示
        ShowBar(GetDisplayName(result.Target), icon);
    }

    /// <summary>
    /// 取互动物体在提示条上显示的名字。
    /// ActivityObject 用配置表里的名字; ShopItemSlot 不是 ActivityObject, 走它自己的 DisplayName。
    /// ShopBoss 额外要把"下次刷新多少钱"带上, 否则玩家不知道按 E 会扣钱。
    /// </summary>
    private static string GetDisplayName(IInteractive target)
    {
        if (target is ShopBoss shopBoss)
        {
            return shopBoss.InteractiveTipText;
        }

        if (target is ActivityObject ao)
        {
            return ao.ActivityBase?.Name ?? "";
        }

        if (target is ShopItemSlot slot)
        {
            return slot.DisplayName;
        }

        return "";
    }
    
    /// <summary>
    /// 相机更新回调
    /// </summary>
    public void OnCameraPositionUpdate(float delta)
    {
        if (_interactiveNode != null && GodotObject.IsInstanceValid(_interactiveNode))
        {
            _interactiveTipBar.Instance.GlobalPosition = GameApplication.Instance.WorldToUiPosition(_interactiveNode.GlobalPosition);
        }
    }


    public void OnDestroy()
    {
        
    }
}