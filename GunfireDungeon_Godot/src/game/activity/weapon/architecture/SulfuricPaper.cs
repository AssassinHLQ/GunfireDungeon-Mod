using Config;
using Godot;
using Vector2I = Godot.Vector2I;

/// <summary>
/// 硫酸纸: 近战挥击的同时在角色当前位置留下硫黄色毒液, 只伤害敌人。
/// </summary>
public partial class SulfuricPaper : Knife
{
    private BrushImageData _brushData;
    private ExcelConfig.LiquidLayer _sulfuricLayer;
    private Vector2I? _previousLiquidPosition;

    public override void OnInit()
    {
        base.OnInit();
        _brushData = LiquidBrushManager.GetBrush("0001");
        _sulfuricLayer = ExcelConfig.LiquidLayer_Map[GameConfig.SulfuricLiquidLayerId];
    }

    protected override void Process(float delta)
    {
        base.Process(delta);
        if (Master != null && IsActive)
        {
            var canvas = Master.AffiliationArea?.RoomInfo?.LiquidCanvas;
            if (canvas != null)
            {
                var position = canvas.ToLiquidCanvasPosition(Master.Position);
                canvas.DrawBrush(_brushData, _sulfuricLayer, _previousLiquidPosition, position, Master.Rotation);
                _previousLiquidPosition = position;
            }
        }
        else
        {
            _previousLiquidPosition = null;
        }
    }
}
