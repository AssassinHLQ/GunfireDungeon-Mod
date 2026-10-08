using Config;
using Godot;

/// <summary>
/// 普通的枪
/// </summary>
public partial class Gun : Weapon
{
    protected override int UseAmmoCount()
    {
        return ActivityBase?.Id == "weapon0008" ? 0 : base.UseAmmoCount();
    }

    protected override void OnFire()
    {
        base.OnFire();
        
        if (InputManager.IsJoystickInput && TriggerRole != null && TriggerRole.IsPlayer())
        {
            Input.StartJoyVibration(0, 0, 0.9f, 0.2f);
            // Input.StartJoyVibration(0, 1f, 1f, 0.3f);
        }
    }
}
