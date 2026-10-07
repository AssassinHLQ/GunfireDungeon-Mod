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
        if (_vitruviusSoul != null)
        {
            _vitruviusSoul.Visible = true;
        }
    }

    protected override void OnConceal()
    {
        base.OnConceal();
        if (_vitruviusSoul != null)
        {
            _vitruviusSoul.Visible = false;
        }
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
        if (Master == null || Master.BackMountPoint == null)
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
                ZIndex = -1,
                Visible = false
            };
        }

        if (_vitruviusSoul.GetParent() != Master.BackMountPoint)
        {
            _vitruviusSoul.GetParent()?.RemoveChild(_vitruviusSoul);
            Master.BackMountPoint.AddChild(_vitruviusSoul);
        }
    }
}
