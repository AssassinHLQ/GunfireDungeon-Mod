using Config;
using Godot;

using DsUi;

namespace UI.game.Setting;

public partial class SettingPanel : Setting
{
    public override void OnCreateUi()
    {
        if (PrevUi != null)
        {
            //返回上一级UI
            S_Back.Instance.Pressed += () =>
            {
                OpenPrevUi();
            };
        }
        else
        {
            S_Back.Instance.Pressed += () =>
            {
                Destroy();
            };
        }
        
        //声音设置BGM
        var save = GameApplication.Instance.GameSave;
        S_BGM.Instance.ValueChanged += (double v) =>
        {
            var value = (float)v;
            save.BgmVolume = value;
            SoundManager.SetBusValue(BUS.BGM, value);
        };
        //声音设置SFX
        S_SFX.Instance.ValueChanged += (double v) =>
        {
            var value = (float)v;
            save.SfxVolume = value;
            SoundManager.SetBusValue(BUS.SFX, value);
        };
        //鼠标跟随进度
        S_FollowsMouseAmount.Instance.ValueChanged += (double v) =>
        {
            save.FollowsMouseAmount = (float)v;
            if (GameCamera.Main != null)
            {
                GameCamera.Main.FollowsMouseAmount = (float)v;
            }
        };
        //声音设置设置BGM SFX的值
        S_SFX.Instance.VisibilityChanged += () =>
        {
            S_BGM.Instance.Value = save.BgmVolume;
            S_SFX.Instance.Value = save.SfxVolume;
            S_FollowsMouseAmount.Instance.Value = save.FollowsMouseAmount;
        };


        //---------------------- 视频设置 -----------------------------
        //全屏属性
        S_FullScreen.Instance.ButtonPressed = save.FullScreen;
        S_FullScreen.Instance.Pressed += OnChangeFullScreen;
        
        //垂直同步
        S_VerticalSync.Instance.ButtonPressed = save.VerticalSync;
        S_VerticalSync.Instance.Pressed += OnChangeVerticalSync;
        
        //完美像素
        S_PerfectPixel.Instance.ButtonPressed = save.PerfectPixel;
        S_PerfectPixel.Instance.Pressed += () =>
        {
            save.PerfectPixel = S_PerfectPixel.Instance.ButtonPressed;
            GameApplication.Instance.SetPerfectPixel(save.PerfectPixel);
        };
        
        //----------------------- 自动索敌 -----------------------------
        // 说明: ds_ui 的 S_XXX 包装类是扫描场景自动生成的, 这个复选框是后加的节点,
        // 所以直接用节点路径取, 不等生成器重跑。
        // 该值的读取在 Player.CalcMousePosition 里, 打开后立即生效, 不需要额外应用。
        var autoTargetBox = S_SettingMenu.Instance.GetNodeOrNull<Godot.CheckBox>("BoxContainer9/AutoTarget");
        if (autoTargetBox != null)
        {
            autoTargetBox.ButtonPressed = save.AutoTarget;
            autoTargetBox.Pressed += () =>
            {
                save.AutoTarget = autoTargetBox.ButtonPressed;
            };
        }

        //----------------------- 自动换弹 -----------------------------
        // 该值的读取在 Weapon.GlobalAutoReload 里, 打开后立即生效。
        var autoReloadBox = S_SettingMenu.Instance.GetNodeOrNull<Godot.CheckBox>("BoxContainer10/AutoReload");
        if (autoReloadBox != null)
        {
            autoReloadBox.ButtonPressed = save.AutoReload;
            autoReloadBox.Pressed += () =>
            {
                save.AutoReload = autoReloadBox.ButtonPressed;
            };
        }

        //----------------------- 手柄设置 -----------------------------

        S_LockAiming.Instance.ButtonPressed = save.JoystickAimAssist;
        S_LockAiming.Instance.Pressed += () =>
        {
            save.JoystickAimAssist = S_LockAiming.Instance.ButtonPressed;
            GameApplication.Instance.SetJoystickAimAssist(save.JoystickAimAssist);
        };
        
        S_AimStrength.Instance.Value = save.JoystickAimAssistStrength;
        S_AimStrength.Instance.ValueChanged += (double v) =>
        {
            save.JoystickAimAssistStrength = (float)v;
            GameApplication.Instance.SetJoystickAimAssistStrength(save.JoystickAimAssistStrength);
        };

        //----------------------- 键位设置 -----------------------------
        CreateKeyBindingSection();

        //统一放大设置面板的字号(4 倍)
        ApplyMenuFontSize();
    }

    /// <summary>
    /// 设置面板普通文字的字号, 原来是 16
    /// 取值必须是像素字体基准字号(12)的整数倍, 否则放大后笔画会糊
    /// </summary>
    private const int MenuFontSize = 60;

    /// <summary>
    /// 把设置菜单里所有控件字号放大, 只改字号不动布局属性
    /// </summary>
    private void ApplyMenuFontSize()
    {
        foreach (var child in S_SettingMenu.Instance.GetChildren())
        {
            ApplyFontSizeRecursive(child);
        }
    }

    private static void ApplyFontSizeRecursive(Node node)
    {
        if (node is Control control)
        {
            //标题自己有更大的字号, 不要覆盖
            if (!control.HasThemeFontSizeOverride("font_size"))
            {
                control.AddThemeFontSizeOverride("font_size", MenuFontSize);
            }
        }

        foreach (var child in node.GetChildren())
        {
            ApplyFontSizeRecursive(child);
        }
    }

    /// <summary>
    /// 当前正在等待玩家按键的动作, 为 null 表示不在改键状态
    /// </summary>
    private KeyBindingManager.BindEntry _waitingEntry;
    //动作对应的按键按钮, 用于刷新显示
    private readonly System.Collections.Generic.Dictionary<string, Button> _keyButtons = new();
    //键位区域里可聚焦的控件, 按顺序排列, 用于拼接焦点链
    private readonly System.Collections.Generic.List<Control> _keyFocusables = new();
    //等待改键时的闪烁计时
    private float _blinkTimer;
    private bool _blinkOn;

    /// <summary>
    /// 构建键位设置区域
    /// </summary>
    private void CreateKeyBindingSection()
    {
        var menu = S_SettingMenu.Instance;

        var title = new Godot.Label
        {
            Text = "键位设置",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        menu.AddChild(title);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 40);
        grid.AddThemeConstantOverride("v_separation", 12);
        menu.AddChild(grid);

        foreach (var entry in KeyBindingManager.Entries)
        {
            var nameLabel = new Godot.Label
            {
                Text = entry.DisplayName,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.AddChild(nameLabel);

            var button = new Button
            {
                Text = KeyBindingManager.GetBindText(entry.Action),
                CustomMinimumSize = new Vector2(300, 0)
            };
            var captured = entry;
            button.Pressed += () => StartRebind(captured, button);
            grid.AddChild(button);
            _keyFocusables.Add(button);

            _keyButtons[entry.Action.ToString()] = button;
        }

        var resetButton = new Button
        {
            Text = "恢复默认键位",
            CustomMinimumSize = new Vector2(360, 0)
        };
        resetButton.Pressed += OnResetKeyBindings;
        menu.AddChild(resetButton);
        _keyFocusables.Add(resetButton);

        //把返回按钮挪到最后, 让键位区域在它上面
        var back = S_Back.Instance;
        menu.RemoveChild(back);
        menu.AddChild(back);
        _keyFocusables.Add(back);
    }

    /// <summary>
    /// 进入等待按键状态
    /// </summary>
    private void StartRebind(KeyBindingManager.BindEntry entry, Button button)
    {
        _waitingEntry = entry;
        button.Text = "请按新键...";
        _blinkTimer = 0;
        _blinkOn = true;
    }

    /// <summary>
    /// 恢复默认键位
    /// </summary>
    private void OnResetKeyBindings()
    {
        KeyBindingManager.ResetAll();
        _waitingEntry = null;
        RefreshKeyButtons();
    }

    /// <summary>
    /// 刷新所有按键按钮的显示
    /// </summary>
    private void RefreshKeyButtons()
    {
        foreach (var entry in KeyBindingManager.Entries)
        {
            var key = entry.Action.ToString();
            if (_keyButtons.TryGetValue(key, out var button) && GodotObject.IsInstanceValid(button))
            {
                button.Text = KeyBindingManager.GetBindText(entry.Action);
            }
        }
    }

    /// <summary>
    /// 等待玩家按键期间捕获输入
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_waitingEntry == null)
        {
            return;
        }

        //键盘: ESC 取消改键, 其它键作为新键位
        //注意不能用鼠标右键取消, 因为右键本身就是要被绑定的键(例如冲刺默认就是右键)
        if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            var keycode = keyEvent.PhysicalKeycode != Key.None
                ? keyEvent.PhysicalKeycode
                : keyEvent.Keycode;

            if (keycode == Key.Escape)
            {
                _waitingEntry = null;
            }
            else if (keycode != Key.None)
            {
                KeyBindingManager.SetKey(_waitingEntry.Action, keycode);
                _waitingEntry = null;
            }

            RefreshKeyButtons();
            //吃掉这次输入, 避免同一个事件又被界面处理一遍
            GetViewport()?.SetInputAsHandled();
            return;
        }

        //鼠标: 直接作为新键位(开火默认就是鼠标左键)
        if (@event is InputEventMouseButton { Pressed: true } mouseEvent
            && mouseEvent.ButtonIndex != MouseButton.None)
        {
            KeyBindingManager.SetMouseKey(_waitingEntry.Action, mouseEvent.ButtonIndex);
            _waitingEntry = null;
            RefreshKeyButtons();
            GetViewport()?.SetInputAsHandled();
        }
    }
    
    public override void OnShowUi()
    {
        InputManager.AddBlockageMarking(GetInstanceId());
        HandlerFocusList();
    }

    public override void OnHideUi()
    {
        InputManager.RemoveBlockageMarking(GetInstanceId());
    }

    public override void OnDestroyUi()
    {
        GameApplication.Instance.GameSave.Save();
    }

    public override void Process(float delta)
    {
        //等待改键时让按钮闪烁, 明确提示正在监听
        if (_waitingEntry != null)
        {
            _blinkTimer += delta;
            if (_blinkTimer >= 0.4f)
            {
                _blinkTimer = 0;
                _blinkOn = !_blinkOn;
                var key = _waitingEntry.Action.ToString();
                if (_keyButtons.TryGetValue(key, out var button) && GodotObject.IsInstanceValid(button))
                {
                    button.Text = _blinkOn ? "请按新键..." : "（或 ESC 取消）";
                }
            }
            return;
        }

        if (Input.IsActionJustPressed(InputAction.UiCancel))
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

    private void HandlerFocusList()
    {
        Control prev = null;
        foreach (var child in S_SettingMenu.Instance.GetChildren())
        {
            if (child is HBoxContainer box && box.Visible)
            {
                foreach (var node in child.GetChildren())
                {
                    if (node is Control temp && (temp is CheckBox || temp is HSlider))
                    {
                        LinkFocus(ref prev, temp);
                        break;
                    }
                }
            }
        }

        //接上键位区域和返回按钮
        foreach (var control in _keyFocusables)
        {
            if (GodotObject.IsInstanceValid(control) && control.Visible)
            {
                LinkFocus(ref prev, control);
            }
        }

        if (prev != null)
        {
            prev.FocusNext = prev.GetPathTo(_keyFocusables.Count > 0 ? _keyFocusables[0] : prev);
        }
    }

    /// <summary>
    /// 把控件接到焦点链尾部, 并把焦点给第一个控件
    /// </summary>
    private static void LinkFocus(ref Control prev, Control current)
    {
        if (prev == null)
        {
            prev = current;
            current.GrabFocus();
        }
        else
        {
            prev.FocusNext = prev.GetPathTo(current);
            prev = current;
        }
    }

    //切换全屏/非全屏
    private void OnChangeFullScreen()
    {
        var pressed = S_FullScreen.Instance.ButtonPressed;
        GameApplication.Instance.GameSave.FullScreen = pressed;
        DisplayServer.WindowSetMode(pressed ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    //切换垂直同步
    private void OnChangeVerticalSync()
    {
        var pressed = S_VerticalSync.Instance.ButtonPressed;
        GameApplication.Instance.GameSave.VerticalSync = pressed;
        DisplayServer.WindowSetVsyncMode(pressed ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
    }

}
