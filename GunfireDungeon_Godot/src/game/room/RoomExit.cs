
using DsUi;
using Godot;

/// <summary>
/// 地牢房间出口
/// </summary>
public partial class RoomExit : Area2D
{
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;

        //临时诊断: 出口是 Area2D, 靠 BodyEntered 感知玩家。
        //如果 mask 对不上玩家所在层, 这个信号永远不会触发, 表现就是"进传送门没反应"。
        TempDebug.LogPortal(
            $"出口就绪 位置={GlobalPosition} layer={CollisionLayer} mask={CollisionMask} " +
            $"monitoring={Monitoring} monitorable={Monitorable} 可见={Visible}");
    }
    
    private void OnBodyEntered(Node body)
    {
        TempDebug.LogPortal($"有物体进入: {body?.GetType().Name} name={body?.Name} 是Role={body is Role}");

        if (body is Role role)
        {
            //Debug.Log("::RoomExit::OnBodyEntered");
            var gameApplication = GameApplication.Instance;

            TempDebug.LogPortal(
                $"玩家进入 楼层={gameApplication.DungeonManager.CurrentFloor} " +
                $"是最后一层={gameApplication.DungeonManager.IsLastFloor} " +
                $"编辑器模式={gameApplication.DungeonManager.IsEditorMode}");

            if (gameApplication.DungeonManager.IsEditorMode) //编辑器模式下下一层就是当前层, 相当于重新开始
            {
                EditorPlayManager.Restart();
            }
            else
            {
                var dungeonManager = gameApplication.DungeonManager;
                if (!dungeonManager.IsLastFloor)
                {
                    //还没到最后楼层, 保留玩家状态直接进入下一层
                    TempDebug.LogPortal("-> 调用 AdvanceToNextFloor()");
                    dungeonManager.AdvanceToNextFloor();
                }
                else
                {
                    //最后一层, 通关
                    TempDebug.LogPortal("-> 判定为最后一层, 弹出通关界面");
                    Debug.Log($"{dungeonManager.CurrentFloorName} 完成, 通关!");
                    World.Current.Pause = true;
                    var openVictory = UiManager.Open_Game_Victory();
                    openVictory.Callback = () =>
                    {
                        //先直接返回大厅, 后面再补充流程
                        UiManager.Open_Game_Loading();
                        GameApplication.Instance.DungeonManager.LoadRoleId = ActivityObject.Ids.Id_role0001;
                        GameApplication.Instance.DungeonManager.ExitDungeon(false, () =>
                        {
                            GameApplication.Instance.DungeonManager.LoadHall(() =>
                            {
                                World.Current.Pause = false;
                                UiManager.Destroy_Game_Loading();
                            });
                        });
                    };
                }
            }
        }
    }
}