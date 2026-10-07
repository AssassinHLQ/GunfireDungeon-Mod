using Godot;

/// <summary>
/// 轴测者: 以角色中心向上、左下、右下持续发射三束激光。
/// </summary>
public partial class AxisometricLaser : FixedLaserWeapon
{
    protected override void OnFire()
    {
        base.OnFire();
        if (Master == null)
        {
            return;
        }

        var origin = Master.GetCenterPosition();
        FireLaser(origin, -Mathf.Pi * 0.5f);
        FireLaser(origin, Mathf.Pi / 6f);
        FireLaser(origin, Mathf.Pi * 5f / 6f);
    }
}
