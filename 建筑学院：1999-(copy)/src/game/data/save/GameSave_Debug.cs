
using System.Text.Json.Serialization;

public partial class GameSave
{
    [JsonInclude]
    public DebugData Debug;
    
    public class DebugData
    {
        [JsonInclude]
        public float X = 500;
        
        [JsonInclude]
        public float Y = 10;

        [JsonInclude]
        public bool ShoFps;

        [JsonInclude]
        public int Fps = 180;

        [JsonInclude]
        public bool DebugDraw;

        /// <summary>
        /// 是否显示【调试器悬浮图标】(左上角那个可拖动的小图标, 点开是日志/工具面板, 也显示 FPS)。
        /// 这是开发者工具, 默认关闭。普通玩家不应该看到它。
        /// </summary>
        [JsonInclude]
        public bool ShowDebuggerIcon;

        /// <summary>
        /// 是否显示【节点检查器】(DsInspector, 带作弊按钮)。
        /// 这是开发者工具, 默认关闭。
        /// </summary>
        [JsonInclude]
        public bool ShowInspector;

        public void Init()
        {
            
        }
    }
}