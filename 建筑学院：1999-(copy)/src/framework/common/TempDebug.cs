using Godot;

/// <summary>
/// 临时诊断开关。
///
/// 用途: 商店交互和传送门这两处是【运行时行为】, 光看代码定位不出来。
/// 打开后会在 Godot 输出面板打印关键状态。
///
/// 【当前状态: 已开启】—— 定位完请把它关掉:
///   方法一: 把下面 Enabled 常量直接改成 false
///   方法二: 搜索并删除所有 TempDebug.LogShop / TempDebug.LogPortal 调用, 再删掉本文件
///   方法三: 删掉本文件后, 重新编译, 编译器会一次性列出所有引用位置
///
/// 日志会打印在 Godot 编辑器的【输出(Output)】面板, 建议在面板右上角过滤器里输入
/// "[商店诊断]" 或 "[传送门诊断]" 只看需要的内容。
/// </summary>
public static class TempDebug
{
    /// <summary>
    /// 总开关。true = 打开全部诊断日志。
    ///
    /// 定位完请改成 false(或删掉本文件与所有引用)。
    /// </summary>
    public const bool Enabled = true;

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
