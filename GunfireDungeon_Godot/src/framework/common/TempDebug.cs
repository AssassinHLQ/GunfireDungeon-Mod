using Godot;

/// <summary>
/// 临时诊断开关。
///
/// 用途: 商店交互和传送门这两处是【运行时行为】, 光看代码定位不出来。
/// 打开后会在 Godot 输出面板打印关键状态。
///
/// 【当前状态: 已关闭 (2026-09-20)】
///   之前一直开着: 商店每次刷新、玩家每次靠近货架都会打日志,
///   单次游玩就能把日志刷到几十 MB, 还把真正的错误淹掉了。
///   要重新诊断就把下面 Enabled 改回 true(或只把 Shop / Portal 其中一个设为 true)。
/// </summary>
public static class TempDebug
{
    /// <summary>
    /// 总开关。true = 打开全部诊断日志。
    /// </summary>
    public const bool Enabled = false;

    /// <summary>
    /// 商店货架诊断
    /// </summary>
    public static readonly bool Shop = Enabled;

    /// <summary>
    /// 传送门/出口诊断
    /// </summary>
    public static readonly bool Portal = Enabled;

    public static void LogShop(string msg)
    {
        if (Shop)
        {
            GD.Print("[商店诊断] " + msg);
        }
    }

    public static void LogPortal(string msg)
    {
        if (Portal)
        {
            GD.Print("[传送门诊断] " + msg);
        }
    }
}
