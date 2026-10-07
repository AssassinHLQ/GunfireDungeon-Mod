using Godot;

/// <summary>
/// 建筑空间组合论的矩形选区预览。
/// </summary>
public partial class AreaSelectionPreview : Node2D
{
    private Vector2 _start;
    private Vector2 _end;

    public void SetSelection(Vector2 start, Vector2 end)
    {
        _start = start;
        _end = end;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var topLeft = new Vector2(Mathf.Min(_start.X, _end.X), Mathf.Min(_start.Y, _end.Y));
        var size = new Vector2(Mathf.Abs(_end.X - _start.X), Mathf.Abs(_end.Y - _start.Y));
        var rect = new Rect2(topLeft, size);
        DrawRect(rect, new Color(1, 1, 1, 0.12f), true);
        DrawRect(rect, Colors.White, false, 1.0f);
    }
}
