using Godot;

/// <summary>
/// Boss 攻击前显示在地面的像素风危险区域。
/// </summary>
public partial class BossAttackWarning : Node2D
{
    private bool _isLine;
    private bool _isSector;
    private float _range;
    private float _halfWidth;
    private float _duration;
    private float _elapsed;

    public void Configure(
        Vector2 globalPosition,
        bool isLine,
        bool isSector,
        float range,
        float halfWidth,
        float duration,
        float rotation
    )
    {
        GlobalPosition = globalPosition;
        _isLine = isLine;
        _isSector = isSector;
        _range = range;
        _halfWidth = halfWidth;
        _duration = Mathf.Max(0.05f, duration);
        Rotation = rotation;
        ZIndex = -1;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= _duration)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var progress = Mathf.Clamp(_elapsed / _duration, 0, 1);
        var pulse = 0.18f + progress * 0.22f +
                    Mathf.Sin(_elapsed * 28.0f) * 0.04f;
        var fill = new Color(0.95f, 0.04f, 0.08f, pulse);
        var border = new Color(1.0f, 0.18f, 0.12f, 0.78f);
        var hot = new Color(1.0f, 0.65f, 0.15f, 0.82f);

        if (_isSector)
        {
            var points = new Vector2[34];
            points[0] = Vector2.Zero;
            for (var i = 0; i <= 32; i++)
            {
                var angle = Mathf.Lerp(-_halfWidth, _halfWidth, i / 32.0f);
                points[i + 1] = Vector2.FromAngle(angle) * _range;
            }
            DrawColoredPolygon(points, fill);
            DrawLine(Vector2.Zero, points[1], border, 3);
            DrawLine(Vector2.Zero, points[^1], border, 3);
            DrawArc(
                Vector2.Zero,
                _range,
                -_halfWidth,
                _halfWidth,
                32,
                border,
                3
            );
            DrawArc(
                Vector2.Zero,
                Mathf.Max(8, _range * progress),
                -_halfWidth,
                _halfWidth,
                24,
                hot,
                3
            );
        }
        else if (_isLine)
        {
            var rect = new Rect2(0, -_halfWidth, _range, _halfWidth * 2);
            DrawRect(rect, fill);
            DrawDashedLine(
                new Vector2(0, -_halfWidth),
                new Vector2(_range, -_halfWidth),
                border,
                3,
                8
            );
            DrawDashedLine(
                new Vector2(0, _halfWidth),
                new Vector2(_range, _halfWidth),
                border,
                3,
                8
            );
            DrawLine(new Vector2(_range, -_halfWidth), new Vector2(_range, _halfWidth), hot, 3);

            for (var x = 12.0f; x < _range; x += 20.0f)
            {
                DrawRect(new Rect2(x, -2, 7, 4), hot);
            }
        }
        else
        {
            DrawCircle(Vector2.Zero, _range, fill);
            DrawArc(Vector2.Zero, _range, 0, Mathf.Tau, 48, border, 3);
            DrawArc(
                Vector2.Zero,
                Mathf.Max(8, _range * progress),
                0,
                Mathf.Tau,
                40,
                hot,
                3
            );

            for (var angle = 0.0f; angle < Mathf.Tau; angle += Mathf.Pi / 4.0f)
            {
                var direction = Vector2.FromAngle(angle);
                var point = direction * (_range * 0.68f);
                DrawRect(new Rect2(point - new Vector2(3, 3), new Vector2(6, 6)), hot);
            }
        }
    }
}
