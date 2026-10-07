using Config;
using DsUi;
using Godot;

/// <summary>
/// 使用现有白色激光子弹的固定方向武器基类。
/// </summary>
public abstract partial class FixedLaserWeapon : Weapon
{
    protected const string LaserBulletId = "1001";

    public override void OnInit()
    {
        base.OnInit();
        NoMasterCanTrigger = false;
        IsAutoPlaySpriteFrames = false;
    }

    protected override int UseAmmoCount()
    {
        return 0;
    }

    protected void FireLaser(Vector2 position, float rotation)
    {
        if (Master == null || Master.IsDestroyed ||
            !ExcelConfig.BulletBase_Map.TryGetValue(LaserBulletId, out var bulletBase))
        {
            return;
        }

        var param = new FireBulletParam(bulletBase)
        {
            Position = position,
            Altitude = 0,
            FireRotation = rotation,
            Camp = Master.Camp
        };
        var laser = FireManager.ShootBullet(this, param);
        if (laser != null)
        {
            Master.ShootBulletHandler(this, rotation, laser);
        }
    }
}
