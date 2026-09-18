using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// 楼层计划 —— 定义一局游戏要依次经过哪些楼层。
/// <para/>
/// 顺序完全由 <c>res://resource/config/FloorPlan.json</c> 决定, 因此可以做到:
/// <list type="bullet">
/// <item>同一楼层出现多次(例如先上楼经过五楼, 最后折返五楼 505 决战)</item>
/// <item>插入隐藏楼层(负一楼), 插入位置也由配置决定</item>
/// <item>每层有独立的名称、副标题与敌人强度系数</item>
/// </list>
/// 改楼层结构只需要改 JSON, 不需要动代码。
/// </summary>
public class FloorPlan
{
    /// <summary>
    /// 单个楼层的定义
    /// </summary>
    public class FloorDef
    {
        /// <summary>唯一 id, 只用于日志与存档</summary>
        public string Id { get; set; } = "";

        /// <summary>显示的楼层名, 例如「一楼」「五楼 · 505」「负一楼」</summary>
        public string Name { get; set; } = "";

        /// <summary>副标题, 进入该层时提示用, 例如「门厅 · 报到处的横幅还没撤」</summary>
        public string Subtitle { get; set; } = "";

        /// <summary>该层敌人的血量倍率(相对基础值), 1 表示不变</summary>
        public float HpMultiplier { get; set; } = 1f;

        /// <summary>楼层类型: normal / boss / final</summary>
        public string Kind { get; set; } = "normal";

        /// <summary>
        /// 该层是否生成敌人。
        /// false 用于剧情探索层 —— 例如折返时的六楼、神秘人所在的七楼。
        /// 关掉后房间不再生成敌人, 门会直接打开, 玩家可以安心搜查线索。
        /// </summary>
        public bool Enemies { get; set; } = true;

        /// <summary>是否是安全探索层(无怪, 以搜查线索 / 推进剧情为主)</summary>
        public bool Explore { get; set; }

        /// <summary>设计备注, 不进游戏, 只给开发者看</summary>
        public string Note { get; set; } = "";

        /// <summary>是否是 boss 层(含最终层)</summary>
        public bool IsBoss => Kind is "boss" or "final";

        /// <summary>是否是最终层, 走到它的出口即通关</summary>
        public bool IsFinal => Kind == "final";

        /// <summary>
        /// 本层是否要在【出口前面那个房间】放一个 Boss 房。
        /// 普通模式靠它安排中段 Boss(目前是 2 / 4 / 6 层);
        /// 魔王模式本来就每间都是 Boss, 不受这个开关影响。
        /// </summary>
        public bool BossBeforeExit { get; set; }
    }

    /// <summary>配置文件结构</summary>
    private class PlanFile
    {
        public string Title { get; set; }
        public string Remark { get; set; }
        public List<FloorDef> Floors { get; set; }

        /// <summary>隐藏楼层定义, 为空表示没有隐藏层</summary>
        public FloorDef Hidden { get; set; }

        /// <summary>隐藏楼层的插入下标, -1 表示插在最后一层之前</summary>
        public int HiddenInsertIndex { get; set; } = -1;
    }

    /// <summary>配置文件路径</summary>
    public const string ConfigPath = "res://resource/config/FloorPlan.json";

    /// <summary>标题, 取自配置</summary>
    public string Title { get; private set; } = "";

    /// <summary>隐藏楼层定义, 可能为 null</summary>
    public FloorDef Hidden { get; private set; }

    /// <summary>配置里的原始楼层序列(不含隐藏层)</summary>
    private readonly List<FloorDef> _baseFloors = new();

    /// <summary>运行时的楼层序列(可能已插入隐藏层)</summary>
    private readonly List<FloorDef> _floors = new();

    private int _hiddenInsertIndex = -1;

    /// <summary>隐藏层是否已经被插入</summary>
    public bool HiddenInserted { get; private set; }

    /// <summary>运行时楼层序列</summary>
    public IReadOnlyList<FloorDef> Floors => _floors;

    /// <summary>当前楼层下标, 从 0 开始</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>当前显示用的层数, 从 1 开始(仅用于日志与难度兜底)</summary>
    public int CurrentNumber => CurrentIndex + 1;

    /// <summary>当前楼层定义</summary>
    public FloorDef Current =>
        CurrentIndex >= 0 && CurrentIndex < _floors.Count ? _floors[CurrentIndex] : null;

    /// <summary>当前总层数(会随隐藏层插入而变化)</summary>
    public int Count => _floors.Count;

    /// <summary>当前是否已经在最后一层</summary>
    public bool IsLast => _floors.Count == 0 || CurrentIndex >= _floors.Count - 1;

    /// <summary>是否还有下一层</summary>
    public bool HasNext => !IsLast;

    /// <summary>
    /// 从配置文件加载楼层计划, 失败时退回到内置的默认序列
    /// </summary>
    public static FloorPlan Load()
    {
        var plan = new FloorPlan();

        try
        {
            var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
            if (file != null)
            {
                var text = file.GetAsText();
                file.Dispose();

                var data = JsonSerializer.Deserialize<PlanFile>(
                    text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null)
                {
                    plan.Title = data.Title ?? string.Empty;
                    plan.Hidden = data.Hidden;
                    plan._hiddenInsertIndex = data.HiddenInsertIndex;
                    if (data.Floors != null)
                    {
                        plan._baseFloors.AddRange(data.Floors);
                    }
                }
            }
            else
            {
                GD.PushWarning($"[FloorPlan] 读不到配置文件: {ConfigPath}, 使用内置默认序列");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[FloorPlan] 解析失败: {e}");
        }

        if (plan._baseFloors.Count == 0)
        {
            plan.BuildFallback();
        }

        plan.Reset();
        return plan;
    }

    /// <summary>
    /// 配置缺失时的兜底序列, 保证游戏仍然能跑
    /// </summary>
    private void BuildFallback()
    {
        Title = "建筑学院：1999";
        var names = new[]
        {
            "一楼", "二楼", "三楼", "四楼", "五楼",
            "六楼", "七楼", "六楼", "五楼"
        };
        for (var i = 0; i < names.Length; i++)
        {
            _baseFloors.Add(new FloorDef
            {
                Id = $"F{i + 1}",
                Name = names[i],
                Subtitle = string.Empty,
                HpMultiplier = 1f + i * 0.2f,
                Kind = i == names.Length - 1 ? "final" : "normal"
            });
        }

        Hidden = new FloorDef
        {
            Id = "B1",
            Name = "负一楼",
            Subtitle = string.Empty,
            HpMultiplier = 2.1f,
            Kind = "normal"
        };
        _hiddenInsertIndex = -1;
    }

    /// <summary>
    /// 重置到第一层, 并撤掉已插入的隐藏层
    /// </summary>
    public void Reset()
    {
        _floors.Clear();
        _floors.AddRange(_baseFloors);
        CurrentIndex = 0;
        HiddenInserted = false;
    }

    /// <summary>
    /// 前进到下一层, 返回是否成功
    /// </summary>
    public bool MoveNext()
    {
        if (!HasNext)
        {
            return false;
        }

        CurrentIndex++;
        return true;
    }

    /// <summary>
    /// 把隐藏楼层插入序列。已经插入过、或配置里没有隐藏层时返回 false。
    /// <para/>
    /// 插入位置取配置的 HiddenInsertIndex, -1 表示插在最后一层之前。
    /// 若插入点在当前层之前, 当前下标会同步后移, 保证玩家依然停在同一层。
    /// </summary>
    public bool TryInsertHidden()
    {
        if (HiddenInserted || Hidden == null)
        {
            return false;
        }

        var index = _hiddenInsertIndex;
        if (index < 0 || index > _floors.Count)
        {
            //默认插在最后一层之前, 让最终层依然是最终层
            index = Math.Max(0, _floors.Count - 1);
        }

        _floors.Insert(index, Hidden);
        HiddenInserted = true;

        //插入点在当前层或之前, 当前层被顶后一位
        if (index <= CurrentIndex)
        {
            CurrentIndex++;
        }

        return true;
    }

    /// <summary>
    /// 取当前层的敌人血量倍率, 取不到时按层数线性兜底
    /// </summary>
    public float CurrentHpMultiplier => Current?.HpMultiplier ?? (1f + CurrentIndex * 0.2f);

    /// <summary>
    /// 当前楼层名, 取不到时用「第 N 层」兜底
    /// </summary>
    public string CurrentName => Current != null && !string.IsNullOrEmpty(Current.Name)
        ? Current.Name
        : $"第 {CurrentNumber} 层";

    /// <summary>
    /// 当前层是否不生成敌人(剧情探索层)
    /// </summary>
    public bool CurrentHasNoEnemies => Current != null && !Current.Enemies;

    /// <summary>
    /// 当前层是否是安全探索层
    /// </summary>
    public bool CurrentIsExplore => Current != null && Current.Explore;
}
