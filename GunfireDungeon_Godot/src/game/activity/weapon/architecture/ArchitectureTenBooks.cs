using Godot;

/// <summary>
/// Architecture Ten Books melee weapon with a Vitruvius spirit companion.
/// </summary>
public partial class ArchitectureTenBooks : Knife
{
    private const string VitruviusTexturePath =
        "res://resource/sprite/weapon/weaponARCHITECTURETENBOOKS/Vitruvius.png";

    private Sprite2D _vitruviusSoul;

    protected override void OnActive()
    {
        base.OnActive();
        EnsureVitruviusSoul();
    }

    protected override void OnPickUp(Role master)
    {
        base.OnPickUp(master);
        EnsureVitruviusSoul();
    }

    protected override void OnRemove(Role master)
    {
        if (_vitruviusSoul != null)
        {
            _vitruviusSoul.QueueFree();
            _vitruviusSoul = null;
        }

        base.OnRemove(master);
    }

    private void EnsureVitruviusSoul()
    {
        if (Master == null)
        {
            return;
        }

        if (_vitruviusSoul == null)
        {
            _vitruviusSoul = new Sprite2D
            {
                Texture = ResourceManager.LoadTexture2D(VitruviusTexturePath),
                Position = new Vector2(0, -3),
                Scale = new Vector2(0.5f, 0.5f),
                ZIndex = 0,
                ShowBehindParent = true,
                Visible = true
            };
        }

        if (_vitruviusSoul.GetParent() != Master.BackMountPoint)
        {
            _vitruviusSoul.GetParent()?.RemoveChild(_vitruviusSoul);
            Master.BackMountPoint.AddChild(_vitruviusSoul);
        }

        _vitruviusSoul.Visible = true;
    }
}
