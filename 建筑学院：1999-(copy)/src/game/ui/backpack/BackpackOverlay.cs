using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Config;

/// <summary>
/// Displays the live player inventory and runtime weapon statistics.
/// </summary>
public partial class BackpackOverlay : CanvasLayer
{
    private Control _root;
    private VBoxContainer _weaponList;
    private GridContainer _propList;
    private GridContainer _collectibleList;
    private World _pausedWorld;
    private bool _previousPause;

    public override void _Ready()
    {
        Layer = 34;
        ProcessMode = ProcessModeEnum.Always;
        BuildInterface();
        _root.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo)
        {
            return;
        }

        if (@event.IsActionPressed(InputAction.PartPackage))
        {
            // 有其它 Ui 打开时（设置 / 暂停 / 结算 / 图鉴 ...）不要抢这个快捷键，
            // 否则在设置界面里按背包键会莫名其妙弹出背包。
            // 背包自己打开时 _root.Visible 为 true，仍然允许它被同一个键关掉。
            if (InputManager.HasUiBlockage && !_root.Visible)
            {
                return;
            }

            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else
        {
            var key = keyEvent.PhysicalKeycode != Key.None
                ? keyEvent.PhysicalKeycode
                : keyEvent.Keycode;
            if (key == Key.Escape && _root.Visible)
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _ExitTree()
    {
        RestoreWorldPause();
    }

    private void BuildInterface()
    {
        _root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var shade = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.03f, 0.90f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(shade);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.OffsetLeft = 120;
        panel.OffsetTop = 65;
        panel.OffsetRight = -120;
        panel.OffsetBottom = -65;
        _root.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 34);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 14);
        margin.AddChild(layout);

        var header = new HBoxContainer();
        layout.AddChild(header);
        var title = CreateLabel("背包与角色状态", 32);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);

        var tabs = new TabContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tabs.AddThemeFontSizeOverride("font_size", 23);
        layout.AddChild(tabs);

        _weaponList = AddTab(tabs, "武器状态");
        _propList = AddGridTab(tabs, "当前道具");
        _collectibleList = AddGridTab(tabs, "加成藏品");
    }

    private static VBoxContainer AddTab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        tabs.AddChild(scroll);

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        scroll.AddChild(margin);

        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 15);
        margin.AddChild(list);
        return list;
    }

    private static GridContainer AddGridTab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tabs.AddChild(scroll);
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(grid);
        return grid;
    }

    public void Toggle()
    {
        if (_root.Visible)
        {
            Close();
            return;
        }

        Refresh();
        _root.Visible = true;

        _pausedWorld = GameApplication.Instance?.DungeonManager?.CurrWorld;
        if (_pausedWorld != null)
        {
            _previousPause = _pausedWorld.Pause;
            _pausedWorld.Pause = true;
        }
    }

    private void Close()
    {
        _root.Visible = false;
        RestoreWorldPause();
    }

    private void RestoreWorldPause()
    {
        if (_pausedWorld != null && GodotObject.IsInstanceValid(_pausedWorld))
        {
            _pausedWorld.Pause = _previousPause;
        }
        _pausedWorld = null;
    }

    private void Refresh()
    {
        ClearList(_weaponList);
        ClearList(_propList);
        ClearList(_collectibleList);

        var player = GameApplication.Instance?.DungeonManager?.CurrWorld?.Player as Role;
        if (player == null || !GodotObject.IsInstanceValid(player))
        {
            AddEmpty(_weaponList, "当前没有可查看的游戏角色。");
            AddEmpty(_propList, "当前没有可查看的游戏角色。");
            AddEmpty(_collectibleList, "当前没有可查看的游戏角色。");
            return;
        }

        RefreshWeapons(player);
        RefreshProps(player);
        RefreshCollectibles(player);
    }

    private void RefreshWeapons(Role player)
    {
        var package = player.WeaponPack;
        var any = false;
        for (var index = 0; index < package.Capacity; index++)
        {
            var weapon = package.GetItem(index);
            if (weapon == null)
            {
                continue;
            }

            any = true;
            var info = weapon.ActivityBase;
            var stat = weapon.Attribute;
            var active = package.ActiveItem == weapon ? "【当前使用】" : "【备用】";
            var lines = new List<string>
            {
                $"{active}  槽位 {index + 1}/{package.Capacity}",
                $"品质：{info?.Quality}    ID：{info?.Id}",
                $"弹夹：{weapon.CurrAmmo}/{stat?.AmmoCapacity ?? 0}",
                $"射速：{Number(stat?.StartFiringSpeed)} → {Number(stat?.FinalFiringSpeed)} 发/分钟",
                $"扳机间隔：{Number(stat?.TriggerInterval)} 秒    换弹：{Number(stat?.ReloadTime)} 秒",
                $"散射：{Number(stat?.StartScatteringRange)} → {Number(stat?.FinalScatteringRange)}",
                $"重量：{Number(stat?.Weight)}"
            };
            if (stat?.IsMelee == true)
            {
                lines.Add("类型：近战武器");
            }
            AddCard(_weaponList, info?.Name ?? "未命名武器", lines, info?.Intro?.Code);
        }

        if (!any)
        {
            AddEmpty(_weaponList, $"当前没有武器（容量 {package.Capacity}）。");
        }
    }

    private void RefreshProps(Role player)
    {
        var package = player.ActivePropsPack;
        var any = false;
        for (var index = 0; index < package.Capacity; index++)
        {
            var prop = package.GetItem(index);
            if (prop == null)
            {
                continue;
            }

            any = true;
            var info = prop.ActivityBase;
            var stat = prop.Attribute;
            var lines = new List<string>
            {
                $"{(package.ActiveItem == prop ? "【当前选择】" : "【背包中】")}  槽位 {index + 1}/{package.Capacity}",
                $"品质：{info?.Quality}    数量：{prop.Count}/{prop.MaxCount}",
                $"持续：{Number(stat?.Duration)} 秒    冷却：{Number(stat?.CooldownTime)} 秒",
                $"充能：{prop.ChargeProgress * 100:0.#}%    冷却进度：{prop.GetCooldownProgress() * 100:0.#}%",
                $"消耗品：{(stat?.IsConsumables == true ? "是" : "否")}"
            };
            AddFragmentLines(lines, "被动", stat?.Buff);
            AddFragmentLines(lines, "效果", stat?.Effect);
            AddFragmentLines(lines, "充能", stat?.Charge);
            AddTile(_propList, prop.GetDefaultTexture(), info?.Name ?? "未命名道具", $"数量：{prop.Count}/{prop.MaxCount} | {info?.Intro?.Code}");
        }

        if (!any)
        {
            AddEmpty(_propList, $"当前没有主动道具（容量 {package.Capacity}）。");
        }
    }

    private void RefreshCollectibles(Role player)
    {
        if (player.BuffPropPack.Count == 0)
        {
            AddEmpty(_collectibleList, "当前还没有获得加成藏品。");
            return;
        }

        for (var index = 0; index < player.BuffPropPack.Count; index++)
        {
            var prop = player.BuffPropPack[index];
            if (prop == null)
            {
                continue;
            }
            var info = prop.ActivityBase;
            var lines = new List<string>
            {
                $"藏品 {index + 1}/{player.BuffPropPack.Count}    品质：{info?.Quality}"
            };
            AddFragmentLines(lines, "加成", prop.Attribute?.Buff);
            AddTile(_collectibleList, prop.GetDefaultTexture(), info?.Name ?? "未命名藏品", info?.Intro?.Code);
        }
    }

    private static void AddFragmentLines(
        List<string> lines,
        string label,
        Dictionary<string, JsonElement[]> fragments
    )
    {
        if (fragments == null || fragments.Count == 0)
        {
            return;
        }
        foreach (var entry in fragments)
        {
            lines.Add($"{label} · {entry.Key}：{JsonRange(entry.Value)}");
        }
    }

    private static void AddTile(GridContainer grid, Texture2D texture, string title, string description)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(170, 150) };
        grid.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        box.AddChild(new TextureRect { Texture = texture, CustomMinimumSize = new Vector2(72, 72), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        box.AddChild(CreateLabel(title, 16, new Color(1.0f, 0.82f, 0.32f)));
        box.AddChild(CreateLabel(description, 13, new Color(0.76f, 0.80f, 0.88f)));
    }

    private static void AddCard(
        VBoxContainer list,
        string title,
        IEnumerable<string> lines,
        string description
    )
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        list.AddChild(panel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 7);
        margin.AddChild(box);

        box.AddChild(CreateLabel(title, 25, new Color(1.0f, 0.82f, 0.32f)));
        if (!string.IsNullOrWhiteSpace(description))
        {
            var descriptionLabel = CreateLabel(description, 18);
            descriptionLabel.Modulate = new Color(0.76f, 0.80f, 0.88f);
            box.AddChild(descriptionLabel);
        }
        box.AddChild(new HSeparator());
        foreach (var line in lines)
        {
            box.AddChild(CreateLabel(line, 19));
        }
    }

    private static Label CreateLabel(
        string text,
        int fontSize,
        Color? color = null
    )
    {
        var label = new Label
        {
            Text = text ?? string.Empty,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (color.HasValue)
        {
            label.Modulate = color.Value;
        }
        return label;
    }

    private static void AddEmpty(Control list, string text)
    {
        var label = CreateLabel(text, 22);
        label.Modulate = new Color(0.72f, 0.76f, 0.84f);
        list.AddChild(label);
    }

    private static void ClearList(Control list)
    {
        foreach (var child in list.GetChildren())
        {
            list.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string Number(float? value)
    {
        return value.HasValue ? value.Value.ToString("0.##") : "-";
    }

    private static string Range<T>(T[] values)
    {
        return values == null || values.Length == 0
            ? "-"
            : string.Join(" ~ ", values);
    }

    private static string JsonRange(JsonElement[] values)
    {
        return values == null || values.Length == 0
            ? "-"
            : string.Join(", ", values.Select(value => value.ToString()));
    }

    private static string FirstText(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
