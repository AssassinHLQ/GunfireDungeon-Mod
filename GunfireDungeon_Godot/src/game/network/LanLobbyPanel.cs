using System;
using Godot;
using DsUi;
using UI.game.Main;

/// <summary>
/// 局域网房间面板。使用代码创建, 不依赖新的场景资源。
/// </summary>
public partial class LanLobbyPanel : Control
{
    public MainPanel MainPanel { get; set; }

    private Label _statusLabel;
    private LineEdit _addressEdit;
    private Button _hostButton;
    private Button _searchButton;
    private Button _joinButton;
    private Button _backButton;
    private bool _closing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi();

        if (LanNetworkManager.Instance != null)
        {
            LanNetworkManager.Instance.StatusChanged += OnStatusChanged;
            LanNetworkManager.Instance.LocalWorldReady += OnLocalWorldReady;
            LanNetworkManager.Instance.HostSearchCompleted += OnHostSearchCompleted;
            OnStatusChanged(LanNetworkManager.Instance.LastStatus);
        }
    }

    public override void _ExitTree()
    {
        if (LanNetworkManager.Instance != null)
        {
            LanNetworkManager.Instance.StatusChanged -= OnStatusChanged;
            LanNetworkManager.Instance.LocalWorldReady -= OnLocalWorldReady;
            LanNetworkManager.Instance.HostSearchCompleted -= OnHostSearchCompleted;
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(760, 520),
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        panel.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        margin.AddChild(column);

        var title = new Label
        {
            Text = "局域网联机",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 62),
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        column.AddChild(title);

        var tip = new Label
        {
            Text = "房主与玩家必须在同一个局域网内。默认端口: 24567",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        tip.AddThemeFontSizeOverride("font_size", 22);
        column.AddChild(tip);

        var addressRow = new HBoxContainer();
        addressRow.AddThemeConstantOverride("separation", 12);
        column.AddChild(addressRow);

        var addressLabel = new Label
        {
            Text = "房主 IP",
            CustomMinimumSize = new Vector2(130, 52),
            VerticalAlignment = VerticalAlignment.Center,
        };
        addressLabel.AddThemeFontSizeOverride("font_size", 26);
        addressRow.AddChild(addressLabel);

        _addressEdit = new LineEdit
        {
            Text = "127.0.0.1",
            PlaceholderText = "例如 192.168.1.23",
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _addressEdit.AddThemeFontSizeOverride("font_size", 26);
        addressRow.AddChild(_addressEdit);

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 16);
        column.AddChild(buttonRow);

        _hostButton = MakeButton("创建房间");
        _hostButton.Pressed += OnHostPressed;
        buttonRow.AddChild(_hostButton);

        _searchButton = MakeButton("自动搜索");
        _searchButton.Pressed += OnSearchPressed;
        buttonRow.AddChild(_searchButton);

        _joinButton = MakeButton("加入房间");
        _joinButton.Pressed += OnJoinPressed;
        buttonRow.AddChild(_joinButton);

        _backButton = MakeButton("返回");
        _backButton.Pressed += OnBackPressed;
        buttonRow.AddChild(_backButton);

        _statusLabel = new Label
        {
            Text = "未连接",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 90),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 24);
        column.AddChild(_statusLabel);

        var hint = new Label
        {
            Text = "连接成功后，房主点击大厅入口进入地牢；客户端会自动跟随房主的场景。",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(hint);
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 64),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.AddThemeFontSizeOverride("font_size", 28);
        return button;
    }

    private void OnHostPressed()
    {
        if (LanNetworkManager.Instance == null || !LanNetworkManager.Instance.StartHost())
        {
            return;
        }

        SetButtonsEnabled(false);
        _statusLabel.Text = "房间已创建，正在进入大厅...";
        UiManager.Open_Game_Loading();
        GameApplication.Instance.DungeonManager.LoadHall(() =>
        {
            UiManager.Destroy_Game_Loading();
            CloseToGame();
        });
    }

    private void OnJoinPressed()
    {
        if (LanNetworkManager.Instance == null)
        {
            return;
        }

        if (!LanNetworkManager.Instance.JoinHost(_addressEdit.Text))
        {
            return;
        }

        SetButtonsEnabled(false);
        _statusLabel.Text = "正在连接房主...";
    }

    private void OnSearchPressed()
    {
        if (LanNetworkManager.Instance == null)
        {
            return;
        }

        _searchButton.Disabled = true;
        _statusLabel.Text = "正在搜索局域网房间...";
        if (!LanNetworkManager.Instance.FindHostOnLan())
        {
            _searchButton.Disabled = false;
            _statusLabel.Text = "搜索启动失败，请手动输入房主 IP";
        }
    }

    private void OnHostSearchCompleted(string address)
    {
        if (!IsInstanceValid(_statusLabel))
        {
            return;
        }

        _searchButton.Disabled = false;
        if (string.IsNullOrEmpty(address))
        {
            _statusLabel.Text = "未找到房间，请确认房主已创建房间，或手动输入 IP";
            return;
        }

        _addressEdit.Text = address;
        _statusLabel.Text = $"已找到房主 {address}，点击“加入房间”即可连接";
    }

    private void OnBackPressed()
    {
        LanNetworkManager.Instance?.Disconnect();
        CloseToMain();
    }

    private void OnStatusChanged(string status)
    {
        if (IsInstanceValid(_statusLabel))
        {
            _statusLabel.Text = status;
        }

        // 异步连接失败或房主断开后允许用户直接重试, 不必退出到主菜单。
        if (status == "未连接" || status.Contains("失败") || status.Contains("断开"))
        {
            SetButtonsEnabled(true);
        }
    }

    private void OnLocalWorldReady(string worldKind)
    {
        if (worldKind == "hall" || worldKind == "dungeon")
        {
            CloseToGame();
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (IsInstanceValid(_hostButton)) _hostButton.Disabled = !enabled;
        if (IsInstanceValid(_joinButton)) _joinButton.Disabled = !enabled;
        if (IsInstanceValid(_backButton)) _backButton.Disabled = false;
    }

    private void CloseToMain()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        MainPanel?.ShowUi();
        QueueFree();
    }

    private void CloseToGame()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        MainPanel?.HideUi();
        QueueFree();
    }
}
