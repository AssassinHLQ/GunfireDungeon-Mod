using Godot;

/// <summary>
/// 平行尺: 在角色上、下边缘沿角色朝向发射两条完全平行的白色激光线。
/// </summary>
public partial class ParallelRulerLaser : FixedLaserWeapon
{
    protected override void OnFire()
    {
        base.OnFire();
        if (Master == null)
        {
            return;
        }

        var origin = Master.GetCenterPosition();
        FireLaser(origin + new Vector2(0, -8), 0f);
        FireLaser(origin + new Vector2(0, 8), 0f);
        FireLaser(origin + new Vector2(0, -8), Mathf.Pi);
        FireLaser(origin + new Vector2(0, 8), Mathf.Pi);
    }
}
