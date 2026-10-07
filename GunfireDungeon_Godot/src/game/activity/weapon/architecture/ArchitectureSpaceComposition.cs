using System.Collections.Generic;
using DsUi;
using Godot;

/// <summary>
/// 建筑空间组合论: 按住左键拖出矩形选区, 松开后对区域内目标造成范围伤害。
/// </summary>
public partial class ArchitectureSpaceComposition : Weapon
{
    private const int AreaDamage = 20;
    private AreaSelectionPreview _preview;
    private Vector2 _selectionStart;
    private Vector2 _selectionEnd;

    public override void OnInit()
    {
        base.OnInit();
        NoMasterCanTrigger = false;
        IsAutoPlaySpriteFrames = false;
    }

    protected override void OnBeginCharge()
    {
        if (Master == null)
        {
            return;
        }

        _selectionStart = Master.GetCenterPosition();
        _selectionEnd = GetGlobalMousePosition();
        EnsurePreview();
        _preview.Visible = true;
        _preview.SetSelection(_selectionStart, _selectionEnd);
    }

    protected override void OnChargeProcess(float delta, float charge)
    {
        if (_preview == null || Master == null)
        {
            return;
        }

        _selectionEnd = GetGlobalMousePosition();
        _preview.SetSelection(_selectionStart, _selectionEnd);
    }

    protected override void OnFire()
    {
        base.OnFire();
        if (Master == null || Master.IsDestroyed)
        {
            return;
        }

        _selectionEnd = GetGlobalMousePosition();
        ApplyAreaDamage(_selectionStart, _selectionEnd);
        HidePreview();
    }

    protected override void OnEndCharge()
    {
        base.OnEndCharge();
        HidePreview();
    }

    protected override void OnConceal()
    {
        base.OnConceal();
        HidePreview();
    }

    protected override void OnRemove(Role master)
    {
        if (_preview != null)
        {
            _preview.QueueFree();
            _preview = null;
        }

        base.OnRemove(master);
    }

    private void EnsurePreview()
    {
        if (_preview != null)
        {
            return;
        }

        _preview = new AreaSelectionPreview
        {
            TopLevel = true,
            ZIndex = 50,
            Visible = false
        };
        var parent = World.Current ?? GetTree().CurrentScene;
        parent?.AddChild(_preview);
    }

    private void HidePreview()
    {
        if (_preview != null)
        {
            _preview.Visible = false;
        }
    }

    private void ApplyAreaDamage(Vector2 start, Vector2 end)
    {
        var topLeft = new Vector2(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        var size = new Vector2(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y));
        size.X = Mathf.Max(size.X, 4);
        size.Y = Mathf.Max(size.Y, 4);

        var shape = new RectangleShape2D { Size = size };
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape,
            Transform = new Transform2D(0, topLeft + size * 0.5f),
            CollisionMask = Role.AttackLayer,
            CollideWithAreas = true,
            CollideWithBodies = true
        };
        var results = GetWorld2D().DirectSpaceState.IntersectShape(query, 128);
        var hitIds = new HashSet<ulong>();
        var damage = new List<AttackStats>
        {
            new(Master.RoleState.CalcDamage(AreaDamage, DamageType.Physical), DamageType.Physical)
        };

        foreach (var result in results)
        {
            if (!result.TryGetValue("collider", out var colliderValue))
            {
                continue;
            }

            if (colliderValue.AsGodotObject() is not IHurt hurt || !hurt.CanHurt(Master.Camp))
            {
                continue;
            }

            var activityObject = hurt.GetActivityObject();
            var id = activityObject?.GetInstanceId() ?? 0;
            if (id != 0 && !hitIds.Add(id))
            {
                continue;
            }

            hurt.Hurt(Master, damage, null, start.AngleToPoint(end));
        }
    }
}
