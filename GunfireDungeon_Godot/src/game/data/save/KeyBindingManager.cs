using System.Collections.Generic;
using Godot;

/// <summary>
/// 键位设置管理, 负责把自定义键位写入 InputMap 以及持久化到存档
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
    /// 允许玩家修改键位的动作列表, 鼠标和摇杆相关的动作不在其中
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
        new(InputAction.Roll, "闪避"),
        new(InputAction.ExchangeWeapon, "切换武器"),
        new(InputAction.ThrowWeapon, "投掷武器"),
        new(InputAction.UseActiveProp, "使用道具"),
        new(InputAction.ExchangeProp, "切换道具"),
        new(InputAction.RemoveProp, "丢弃道具"),
        new(InputAction.Map, "打开地图"),
        new(InputAction.Menu, "打开菜单"),
        new(InputAction.PartPackage, "零件背包")
    };

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

            var keycode = (Key)((long)pair.Value);
            if (keycode == Key.None)
            {
                continue;
            }

            EraseKeyboardEvents(action);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = keycode });
        }
    }

    /// <summary>
    /// 取当前动作绑定的第一个键盘按键, 没有则返回 Key.None
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
    /// 把某个动作的键位改成指定按键, 并写入存档
    /// </summary>
    public static void SetKey(StringName action, Key keycode)
    {
        if (keycode == Key.None || !InputMap.HasAction(action))
        {
            return;
        }

        EraseKeyboardEvents(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = keycode });

        var save = GameApplication.Instance?.GameSave;
        if (save != null)
        {
            save.KeyBindings[action.ToString()] = (long)keycode;
            save.Save();
        }
    }

    /// <summary>
    /// 恢复全部默认键位, 需要重启游戏才能完全生效
    /// </summary>
    public static void ResetAll()
    {
        var save = GameApplication.Instance?.GameSave;
        if (save == null)
        {
            return;
        }

        save.KeyBindings.Clear();
        save.Save();

        foreach (var entry in Entries)
        {
            if (InputMap.HasAction(entry.Action))
            {
                EraseKeyboardEvents(entry.Action);
            }
        }
    }

    /// <summary>
    /// 只清掉键盘事件, 保留鼠标和手柄的绑定
    /// </summary>
    private static void EraseKeyboardEvents(StringName action)
    {
        foreach (var evt in InputMap.ActionGetEvents(action))
        {
            if (evt is InputEventKey)
            {
                InputMap.ActionEraseEvent(action, evt);
            }
        }
    }

    /// <summary>
    /// 把按键显示成可读文本
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
}
