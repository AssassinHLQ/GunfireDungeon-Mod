using Godot;

namespace UI.game;

public static class WeaponIconScale
{
    public static Vector2 GetScale(Weapon weapon)
    {
        return weapon?.ActivityBase?.Id switch
        {
            "weapon0055" => new Vector2(0.5f, 0.5f),
            "weapon0048" => new Vector2(1f / 3f, 1f / 3f),
            _ => Vector2.One
        };
    }
}
