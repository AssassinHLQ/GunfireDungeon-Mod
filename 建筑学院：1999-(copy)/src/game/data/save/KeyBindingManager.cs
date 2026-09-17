using System.Collections.Generic;
using Godot;

/// <summary>
/// 键位设置管理, 负责把自定义键位写入 InputMap 以及持久化到存档
/// <para/>
/// 同时支持键盘按键与鼠标按键。存档里用一个 long 表示:
/// <c>&gt;= 0</c> 是键盘键码(<see cref="Key"/>); <c>&lt; 0</c> 是鼠标键,
/// 解码为 <c>MouseButton = -值 - 1</c>。这样旧存档(全是非负键码)依然兼容。
/// </summary>
public static class KeyBindingManager
{
    /// <summary>
    /// 一个可改键的输入动作
    /// </summary>
    public class BindEntry
    {
        /// <summary>
        /// 输入动作名称, 对应 InputMap 里的 action
        /// </summary>
        public StringName Action;

        /// <summary>
        /// 界面上显示的名字
        /// </summary>
        public string DisplayName;

        public BindEntry(StringName action, string displayName)
        {
            Action = action;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// 允许玩家修改键位的动作列表
    /// </summary>
    public static readonly BindEntry[] Entries =
    {
        new(InputAction.MoveUp, "向上移动"),
        new(InputAction.MoveDown, "向下移动"),
        new(InputAction.MoveLeft, "向左移动"),
        new(InputAction.MoveRight, "向右移动"),
        new(InputAction.Fire, "开火"),
        new(InputAction.Reload, "换弹"),
        new(InputAction.Interactive, "互动"),
        new(InputAction.MeleeAttack, "近战攻击"),
        new(InputAction.Roll, "冲刺"),
        new(InputAction.ExchangeWeapon, "切换武器"),
        new(InputAction.ThrowWeapon, "投掷武器"),
        new(InputAction.UseActiveProp, "使用道具"),
        new(InputAction.ExchangeProp, "切换道具"),
        new(InputAction.RemoveProp, "丢弃道具"),
        new(InputAction.Map, "打开地图"),
        new(InputAction.Menu, "打开菜单"),
        new(InputAction.PartPackage, "道具背包")
    };

    //---------------- 存档编码辅助 ----------------

    private static long EncodeMouse(MouseButton button)
    {
        return -(long)button - 1;
    }

    private static bool TryDecodeMouse(long value, out MouseButton button)
    {
        if (value < 0)
        {
            button = (MouseButton)(-value - 1);
            return true;
        }

        button = MouseButton.None;
        return false;
    }

    //---------------- 应用 / 写入 ----------------

    /// <summary>
    /// 把所有已保存的自定义键位应用到 InputMap
    /// </summary>
    public static void ApplyAll()
    {
        var bindings = GameApplication.Instance?.GameSave?.KeyBindings;
        if (bindings == null)
        {
            return;
        }

        foreach (var pair in bindings)
        {
            var action = new StringName(pair.Key);
            if (!InputMap.HasAction(action))
            {
                continue;
            }

            EraseKeyAndMouseEvents(action);

            if (TryDecodeMouse(pair.Value, out var mouseButton))
            {
                InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = mouseButton });
                continue;
            }

            var keycode = (Key)pair.Value;
            if (keycode != Key.None)
            {
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = keycode });
            }
        }
    }

    /// <summary>
    /// 把某个动作的键位改成指定键盘按键, 并写入存档
    /// </summary>
    public static void SetKey(StringName action, Key keycode)
    {
        if (keycode == Key.None || !InputMap.HasAction(action))
        {
            return;
        }

        EraseKeyAndMouseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = keycode });
        Save(action, (long)keycode);
    }

    /// <summary>
    /// 把某个动作的键位改成指定鼠标按键, 并写入存档
    /// </summary>
    public static void SetMouseKey(StringName action, MouseButton button)
    {
        if (button == MouseButton.None || !InputMap.HasAction(action))
        {
            return;
        }

        EraseKeyAndMouseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
        Save(action, EncodeMouse(button));
    }

    private static void Save(StringName action, long value)
    {
        var save = GameApplication.Instance?.GameSave;
        if (save == null)
        {
            return;
        }

        save.KeyBindings[action.ToString()] = value;
        save.Save();
    }

    /// <summary>
    /// 恢复全部默认键位(含鼠标), 直接从项目设置里读回默认绑定, 不需要重启
    /// </summary>
    public static void ResetAll()
    {
        var save = GameApplication.Instance?.GameSave;
        if (save != null)
        {
            save.KeyBindings.Clear();
            save.Save();
        }

        foreach (var entry in Entries)
        {
            if (!InputMap.HasAction(entry.Action))
            {
                continue;
            }

            //清空当前绑定
            foreach (var evt in InputMap.ActionGetEvents(entry.Action))
            {
                InputMap.ActionEraseEvent(entry.Action, evt);
            }

            //从项目设置读回默认绑定
            var setting = ProjectSettings.GetSetting($"input/{entry.Action}");
            if (setting.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var dict = setting.AsGodotDictionary();
            if (!dict.ContainsKey("events"))
            {
                continue;
            }

            foreach (var item in dict["events"].AsGodotArray())
            {
                if (item.As<InputEvent>() is { } inputEvent)
                {
                    InputMap.ActionAddEvent(entry.Action, inputEvent);
                }
            }
        }
    }

    //---------------- 读取用于显示 ----------------

    /// <summary>
    /// 取某个动作当前的绑定描述, 键盘和鼠标都认, 没有则返回「未设置」
    /// </summary>
    public static string GetBindText(StringName action)
    {
        if (!InputMap.HasAction(action))
        {
            return "未设置";
        }

        var parts = new List<string>();
        foreach (var evt in InputMap.ActionGetEvents(action))
        {
            switch (evt)
            {
                case InputEventKey keyEvent:
                {
                    var code = keyEvent.PhysicalKeycode != Key.None
                        ? keyEvent.PhysicalKeycode
                        : keyEvent.Keycode;
                    if (code != Key.None)
                    {
                        parts.Add(GetKeyText(code));
                    }

                    break;
                }
                case InputEventMouseButton mouseEvent:
                    parts.Add(GetMouseText(mouseEvent.ButtonIndex));
                    break;
            }

            if (parts.Count >= 2)
            {
                break;
            }
        }

        return parts.Count == 0 ? "未设置" : string.Join(" / ", parts);
    }

    /// <summary>
    /// 取当前动作绑定的键盘按键, 没有则返回 Key.None (只用于内部判断)
    /// </summary>
    public static Key GetKey(StringName action)
    {
        if (!InputMap.HasAction(action))
        {
            return Key.None;
        }

        foreach (var evt in InputMap.ActionGetEvents(action))
        {
            if (evt is InputEventKey keyEvent)
            {
                return keyEvent.PhysicalKeycode != Key.None
                    ? keyEvent.PhysicalKeycode
                    : keyEvent.Keycode;
            }
        }

        return Key.None;
    }

    /// <summary>
    /// 只清掉键盘与鼠标事件, 保留手柄绑定
    /// </summary>
    private static void EraseKeyAndMouseEvents(StringName action)
    {
        foreach (var evt in InputMap.ActionGetEvents(action))
        {
            if (evt is InputEventKey || evt is InputEventMouseButton)
            {
                InputMap.ActionEraseEvent(action, evt);
            }
        }
    }

    /// <summary>
    /// 把键盘按键显示成可读文本
    /// </summary>
    public static string GetKeyText(Key keycode)
    {
        if (keycode == Key.None)
        {
            return "未设置";
        }

        var text = OS.GetKeycodeString(keycode);
        return string.IsNullOrEmpty(text) ? keycode.ToString() : text;
    }

    /// <summary>
    /// 把鼠标按键显示成可读文本
    /// </summary>
    public static string GetMouseText(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "鼠标左键",
            MouseButton.Right => "鼠标右键",
            MouseButton.Middle => "鼠标中键",
            MouseButton.WheelUp => "滚轮上",
            MouseButton.WheelDown => "滚轮下",
            MouseButton.WheelLeft => "滚轮左",
            MouseButton.WheelRight => "滚轮右",
            MouseButton.Xbutton1 => "侧键1",
            MouseButton.Xbutton2 => "侧键2",
            _ => $"鼠标键{(int)button}"
        };
    }
}
