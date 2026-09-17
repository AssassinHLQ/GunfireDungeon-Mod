using System.Collections.Generic;
using Godot;

using DsUi;

namespace UI.game.RoomUI;

public partial class LifeBarHandler : Control, IUiNodeScript
{

    private RoomUI.LifeBar _bar;
    private EventFactory<EventEnum> _eventFactory;
    private bool _refreshHpFlag = false;
    private bool _refreshGoldFlag = false;
    private bool _refreshArmorFlag = false;
    private HBoxContainer _lifeIcons;
    private HBoxContainer _shieldIcons;

    private Role _player;

    public void SetUiNode(IUiNode uiNode)
    {
        _bar = (RoomUI.LifeBar)uiNode;
        _bar.UiPanel.OnShowUiEvent += OnShow;
        _bar.UiPanel.OnHideUiEvent += OnHide;
        
        var container = _bar.L_VBoxContainer;
        ConfigureIconRow(container.L_LifeContainer.Instance, container.L_LifeContainer.L_LifeProgressBar.Instance, container.L_LifeContainer.L_TextureRect.Instance, false);
        ConfigureIconRow(container.L_ShieldContainer.Instance, container.L_ShieldContainer.L_ShieldProgressBar.Instance, container.L_ShieldContainer.L_TextureRect.Instance, true);
        container.L_ArmorContainer.Instance.Visible = false;
    }
    
    public void OnShow()
    {
        _eventFactory = EventManager.CreateEventFactory();
        _eventFactory.AddEventListener(EventEnum.OnPlayerHpChange, o => RefreshLife());
        _eventFactory.AddEventListener(EventEnum.OnPlayerMaxHpChange, o => RefreshLife());
        _eventFactory.AddEventListener(EventEnum.OnPlayerShieldChange, o => RefreshLife());
        _eventFactory.AddEventListener(EventEnum.OnPlayerMaxShieldChange, o => RefreshLife());
        _eventFactory.AddEventListener(EventEnum.OnPlayerArmorChange, o => RefreshArmor());
        _eventFactory.AddEventListener(EventEnum.OnPlayerMaxArmorChange, o => RefreshArmor());
        _eventFactory.AddEventListener(EventEnum.OnPlayerGoldChange, o => RefreshGold());
        RefreshLife();
        RefreshGold();
        RefreshArmor();
    }

    public void OnHide()
    {
        _eventFactory.RemoveAllEventListener();
    }

    public override void _Process(double delta)
    {
        if (_bar == null || !_bar.UiPanel.IsOpen)
        {
            return;
        }
        if (!_refreshGoldFlag && World.Current != null && _player != World.Current.Player)
        {
            _player = World.Current.Player;
            _refreshHpFlag = true;
            _refreshArmorFlag = true;
        }
        
        if (_refreshHpFlag)
        {
            _refreshHpFlag = false;
            HandlerRefreshLife();
        }

        if (_refreshGoldFlag)
        {
            _refreshGoldFlag = false;
            HandlerRefreshGold();
        }

        if (_refreshArmorFlag)
        {
            _refreshArmorFlag = false;
            HandlerRefreshArmor();
        }
    }

    public void RefreshGold()
    {
        _refreshGoldFlag = true;
    }
    
    public void RefreshLife()
    {
        _refreshHpFlag = true;
    }

    public void RefreshArmor()
    {
        _refreshArmorFlag = true;
    }

    private void HandlerRefreshLife()
    {
        var player = World.Current.Player;
        if (player == null) return;

        RefreshIcons(_lifeIcons, player.MaxHp, player.Hp, false);
        RefreshIcons(_shieldIcons, player.MaxShield, Mathf.RoundToInt(player.RealShield), true);
        _bar.L_VBoxContainer.L_ShieldContainer.Instance.Visible = player.MaxShield > 0;
    }

    private void ConfigureIconRow(HBoxContainer container, Control progress, TextureRect labelIcon, bool shield)
    {
        progress.Visible = false;
        labelIcon.Visible = false;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        container.AddChild(row);
        if (shield) _shieldIcons = row; else _lifeIcons = row;
    }

    private static void RefreshIcons(HBoxContainer row, int maxValue, int currentValue, bool shield)
    {
        if (row == null) return;
        foreach (var child in row.GetChildren()) child.QueueFree();
        var count = shield ? maxValue : Mathf.CeilToInt(maxValue / 2f);
        for (var i = 0; i < count; i++)
        {
            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Keep
            };
            var texture = ResourceManager.LoadTexture2D(shield
                ? ResourcePath.resource_sprite_ui_roomUI_Shield_full_png
                : ResourcePath.resource_sprite_ui_roomUI_Life_full_png);
            if ((shield && currentValue <= i) || (!shield && currentValue <= i * 2))
            {
                texture = ResourceManager.LoadTexture2D(shield
                    ? ResourcePath.resource_sprite_ui_roomUI_Shield_empty_png
                    : ResourcePath.resource_sprite_ui_roomUI_Life_empty_png);
            }
            else if (!shield && currentValue == i * 2 + 1)
            {
                texture = ResourceManager.LoadTexture2D(ResourcePath.resource_sprite_ui_roomUI_Life_half_png);
            }
            icon.Texture = texture;
            // The HUD textures are authored at their intended on-screen pixel size.
            var nativeSize = texture.GetSize();
            icon.CustomMinimumSize = nativeSize;
            icon.Size = nativeSize;
            icon.Scale = Vector2.One;
            row.AddChild(icon);
        }
    }
    
    private void HandlerRefreshGold()
    {
        var player = World.Current.Player;
        if (player == null)
        {
            return;
        }

        _bar.L_VBoxContainer.L_Gold.L_GoldText.Instance.Text = player.RoleState.Gold.ToString();
    }

    private void HandlerRefreshArmor()
    {
        var player = World.Current.Player;
        if (player == null)
        {
            return;
        }

        _bar.L_VBoxContainer.L_ArmorContainer.Instance.Visible = false;
    }

    public void OnDestroy()
    {
        
    }
}