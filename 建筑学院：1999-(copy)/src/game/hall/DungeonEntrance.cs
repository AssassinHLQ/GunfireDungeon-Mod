using DsUi;
using Godot;

/// <summary>
/// 地牢入口节点(大厅里的"光环")。
///
/// 一个入口对应一种地牢模式: 场景里用 Mode 这个导出字段区分。
/// 默认光环用 Normal(普通模式), 红色光环用 Erlkoenig(魔王模式 —— 每个房间基本都是 Boss)。
/// </summary>
public partial class DungeonEntrance : Area2D
{
    /// <summary>
    /// 这个入口进入的地牢模式。
    /// 注意: 每次进入都会用 GameApplication.GetDungeonConfig 现场重新生成配置,
    /// 而不是复用启动时缓存的 FirstDungeonConfig —— 否则两种模式会串味。
    /// </summary>
    [Export]
    public DungeonMode Mode = DungeonMode.Normal;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
        {
            // 现场按本入口的模式取配置
            var baseConfig = GameApplication.Instance.FirstDungeonConfig;
            var config = GameApplication.Instance.GetDungeonConfig(baseConfig.GroupName, 1, Mode);

            // 验证该组是否满足生成地牢的条件
            var result = DungeonManager.CheckDungeon(config.GroupName);
            if (result.HasError)
            {
                UiManager.Destroy_Game_Loading();
                EditorWindowManager.ShowTips("警告", "当前组'" + config.GroupName + "'" + result.ErrorMessage + ", 不能生成地牢!");
            }
            else
            {
                UiManager.Open_Game_Loading();
                GameApplication.Instance.DungeonManager.ResetFloor(); //每局从第 1 层开始
                GameApplication.Instance.DungeonManager.ExitHall(true, () =>
                {
                    GameApplication.Instance.DungeonManager.LoadDungeon(config, () =>
                    {
                        UiManager.Destroy_Game_Loading();
                    });
                });
            }
        }
    }
}
